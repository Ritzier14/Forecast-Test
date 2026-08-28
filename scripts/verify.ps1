[CmdletBinding()]
param(
    [switch]$NoRestore,

    # The legacy executable is retained as an opt-in smoke check. It is not the
    # authoritative automated-test path; discovered tests below are.
    [switch]$RunLegacySmoke,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    # This seam lets the verification entry point be tested with a deliberately
    # failing executable without changing the repository's build commands.
    [string]$DotnetCommand = 'dotnet'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$locationPushed = $false
$exitCode = 1

function Invoke-RequiredNativeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $displayArguments = ($Arguments | ForEach-Object {
        if ($_ -match '\s') { "'$_'" } else { $_ }
    }) -join ' '

    Write-Host "==> $FilePath $displayArguments"
    & $FilePath @Arguments
    $commandExitCode = $LASTEXITCODE

    if ($commandExitCode -ne 0) {
        throw "Command failed with exit code ${commandExitCode}: $FilePath $displayArguments"
    }
}

try {
    $repoRoot = (Resolve-Path -LiteralPath (Join-Path -Path $PSScriptRoot -ChildPath '..')).Path
    $solutionPath = Join-Path -Path $repoRoot -ChildPath 'ProjectCostForecast.sln'
    $unitTestProjectPath = Join-Path -Path $repoRoot -ChildPath 'tests\ProjectCostForecast.UnitTests\ProjectCostForecast.UnitTests.csproj'

    foreach ($requiredPath in @($solutionPath, $unitTestProjectPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required repository file was not found: $requiredPath"
        }
    }

    if ($RunLegacySmoke) {
        $harnessProjectPath = Join-Path -Path $repoRoot -ChildPath 'tests\ProjectCostForecast.Tests\ProjectCostForecast.Tests.csproj'
        if (-not (Test-Path -LiteralPath $harnessProjectPath -PathType Leaf)) {
            throw "Legacy smoke project was not found: $harnessProjectPath"
        }
    }

    $resolvedDotnet = (Get-Command -Name $DotnetCommand -ErrorAction Stop).Source
    Push-Location -LiteralPath $repoRoot
    $locationPushed = $true

    if ($NoRestore) {
        Write-Host 'Skipping restore (-NoRestore).'
    }
    else {
        Invoke-RequiredNativeCommand -FilePath $resolvedDotnet -Arguments @(
            'restore',
            $solutionPath
        )
    }

    Invoke-RequiredNativeCommand -FilePath $resolvedDotnet -Arguments @(
        'build',
        $solutionPath,
        '-c',
        $Configuration,
        '--no-restore'
    )

    Invoke-RequiredNativeCommand -FilePath $resolvedDotnet -Arguments @(
        'test',
        $unitTestProjectPath,
        '-c',
        $Configuration,
        '--no-build',
        '--no-restore'
    )

    if ($RunLegacySmoke) {
        Write-Host 'Running retained legacy console smoke check (not the authoritative test gate).'
        Invoke-RequiredNativeCommand -FilePath $resolvedDotnet -Arguments @(
            'run',
            '--project',
            $harnessProjectPath,
            '-c',
            $Configuration,
            '--no-build',
            '--no-restore'
        )
    }

    Write-Host 'Verification passed.'
    $exitCode = 0
}
catch {
    Write-Error $_
}
finally {
    if ($locationPushed) {
        Pop-Location
    }
}

exit $exitCode
