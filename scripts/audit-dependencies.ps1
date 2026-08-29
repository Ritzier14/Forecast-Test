[CmdletBinding()]
param(
    [string]$OutputPath = 'artifacts/luna21/dependency-audit.json',

    [switch]$NoRestore,

    [switch]$FailOnVulnerability
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$solutionPath = Join-Path $repoRoot 'ProjectCostForecast.sln'
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
}
else {
    Join-Path $repoRoot $OutputPath
}
$projectPaths = @(
    'src/ProjectCostForecast.App/ProjectCostForecast.App.csproj',
    'tests/ProjectCostForecast.Tests/ProjectCostForecast.Tests.csproj',
    'tests/ProjectCostForecast.UnitTests/ProjectCostForecast.UnitTests.csproj'
)
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source

function Invoke-DotnetCommand {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code ${LASTEXITCODE}: dotnet $($Arguments -join ' ')"
    }
}

function Invoke-DotnetJson {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $output = @(& $dotnet @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        $text = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
        throw "dotnet JSON command failed with exit code ${exitCode}: dotnet $($Arguments -join ' ')`n$text"
    }

    $json = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    try {
        return ConvertFrom-Json -InputObject $json
    }
    catch {
        throw "dotnet returned invalid JSON: dotnet $($Arguments -join ' ')`n$json"
    }
}

function Get-VulnerabilityCount {
    param(
        [AllowNull()]
        [object]$Node
    )

    if ($null -eq $Node) {
        return 0
    }

    $count = 0
    if ($Node -is [System.Collections.IEnumerable] -and $Node -isnot [string]) {
        foreach ($item in $Node) {
            $count += Get-VulnerabilityCount -Node $item
        }

        return $count
    }

    if (@($Node.PSObject.Properties).Count -eq 0) {
        return 0
    }

    foreach ($property in $Node.PSObject.Properties) {
        if ($property.Name -in @('vulnerablePackages', 'vulnerabilities')) {
            if ($null -ne $property.Value) {
                if ($property.Value -is [System.Collections.IEnumerable] -and $property.Value -isnot [string]) {
                    $count += @($property.Value).Count
                }
                else {
                    $count++
                }
            }
        }

        $count += Get-VulnerabilityCount -Node $property.Value
    }

    return $count
}

if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw "Solution was not found: $solutionPath"
}

if (-not $NoRestore) {
    Invoke-DotnetCommand -Arguments @('restore', $solutionPath, '--locked-mode')
}

$projectReports = foreach ($projectPath in $projectPaths) {
    $absoluteProjectPath = Join-Path $repoRoot ($projectPath -replace '/', '\')
    if (-not (Test-Path -LiteralPath $absoluteProjectPath -PathType Leaf)) {
        throw "Project was not found: $absoluteProjectPath"
    }

    $inventory = Invoke-DotnetJson -Arguments @(
        'list', $absoluteProjectPath, 'package', '--include-transitive', '--format', 'json', '--no-restore'
    )
    $vulnerabilities = Invoke-DotnetJson -Arguments @(
        'list', $absoluteProjectPath, 'package', '--vulnerable', '--include-transitive', '--format', 'json', '--no-restore'
    )

    foreach ($document in @($inventory, $vulnerabilities)) {
        foreach ($listedProject in @($document.projects)) {
            $pathProperty = $listedProject.PSObject.Properties['path']
            if ($null -ne $pathProperty) {
                $listedProject.path = $projectPath
            }
        }
    }

    [ordered]@{
        Project = $projectPath
        Inventory = $inventory
        Vulnerabilities = $vulnerabilities
    }
}

$packageMap = [ordered]@{}
foreach ($projectReport in $projectReports) {
    foreach ($project in @($projectReport.Inventory.projects)) {
        foreach ($framework in @($project.frameworks)) {
            foreach ($packageKind in @('topLevelPackages', 'transitivePackages')) {
                $property = $framework.PSObject.Properties[$packageKind]
                if ($null -eq $property) {
                    continue
                }

                foreach ($package in @($property.Value)) {
                    $id = [string]$package.id
                    if ([string]::IsNullOrWhiteSpace($id)) {
                        continue
                    }

                    if (-not $packageMap.Contains($id)) {
                        $packageMap[$id] = [ordered]@{
                            Id = $id
                            ResolvedVersions = @()
                            DirectProjects = @()
                            TransitiveProjects = @()
                        }
                    }

                    $entry = $packageMap[$id]
                    $version = [string]$package.resolvedVersion
                    if (-not [string]::IsNullOrWhiteSpace($version) -and $entry.ResolvedVersions -notcontains $version) {
                        $entry.ResolvedVersions += $version
                    }

                    if ($packageKind -eq 'topLevelPackages') {
                        if ($entry.DirectProjects -notcontains $projectReport.Project) {
                            $entry.DirectProjects += $projectReport.Project
                        }
                    }
                    elseif ($entry.TransitiveProjects -notcontains $projectReport.Project) {
                        $entry.TransitiveProjects += $projectReport.Project
                    }
                }
            }
        }
    }
}

$vulnerabilityCount = 0
foreach ($projectReport in $projectReports) {
    $vulnerabilityCount += Get-VulnerabilityCount -Node $projectReport.Vulnerabilities
}

if ($FailOnVulnerability -and $vulnerabilityCount -gt 0) {
    throw "NuGet vulnerability audit found $vulnerabilityCount vulnerable package record(s)."
}

$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Could not determine the repository commit.'
}

$report = [ordered]@{
    SchemaVersion = 1
    RecordedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
    RepositoryCommit = $commit
    DotnetSdk = (& $dotnet --version).Trim()
    LockedRestore = $true
    VulnerabilityCount = $vulnerabilityCount
    Packages = @($packageMap.Values)
    Projects = @($projectReports)
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$report | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8
Write-Host "Dependency audit report: $resolvedOutputPath"
Write-Host "Packages inventoried: $($packageMap.Count); vulnerability records: $vulnerabilityCount"
