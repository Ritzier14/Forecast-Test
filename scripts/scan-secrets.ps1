[CmdletBinding()]
param(
    [string]$OutputPath = 'artifacts/luna22/secret-scan.json',

    [switch]$FailOnMatch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
}
else {
    Join-Path $repoRoot $OutputPath
}
$dotnetSafeTextExtensions = @(
    '.cs', '.csproj', '.json', '.md', '.props', '.ps1', '.sln', '.targets',
    '.txt', '.xaml', '.xml', '.yml', '.yaml'
)
$patterns = @(
    [pscustomobject]@{
        Name = 'private-key-block'
        Regex = '-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----'
        CaseInsensitive = $false
    },
    [pscustomobject]@{
        Name = 'aws-access-key'
        Regex = '\bAKIA[0-9A-Z]{16}\b'
        CaseInsensitive = $false
    },
    [pscustomobject]@{
        Name = 'github-token'
        Regex = '\bgh[pousr]_[A-Za-z0-9_]{20,}\b'
        CaseInsensitive = $false
    },
    [pscustomobject]@{
        Name = 'generic-secret-assignment'
        Regex = '\b(api[_-]?key|secret|password|token)\b\s*[:=]\s*["''][^"'']{16,}["'']'
        CaseInsensitive = $true
    }
)
$findings = [System.Collections.Generic.List[object]]::new()

function Invoke-GitOutput {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $output = @(& git -C $repoRoot @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        $text = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
        throw "git command failed with exit code ${exitCode}: git $($Arguments -join ' ')`n$text"
    }

    return $output | ForEach-Object { $_.ToString() }
}

$trackedFiles = @(Invoke-GitOutput -Arguments @('ls-files'))
foreach ($relativePath in $trackedFiles) {
    $extension = [System.IO.Path]::GetExtension($relativePath).ToLowerInvariant()
    if ($dotnetSafeTextExtensions -notcontains $extension) {
        continue
    }

    $absolutePath = Join-Path $repoRoot ($relativePath -replace '/', '\')
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
        continue
    }

    $lineNumber = 0
    foreach ($line in (Get-Content -LiteralPath $absolutePath)) {
        $lineNumber++
        foreach ($pattern in $patterns) {
            if ($line -match $pattern.Regex) {
                $findings.Add([ordered]@{
                    Scope = 'working-tree'
                    Type = $pattern.Name
                    File = $relativePath
                    Line = $lineNumber
                })
            }
        }
    }
}

$commits = @(Invoke-GitOutput -Arguments @('rev-list', '--all'))
foreach ($commit in $commits) {
    foreach ($pattern in $patterns) {
        $grepArguments = @('grep', '-I', '-l', '-E')
        if ($pattern.CaseInsensitive) {
            $grepArguments += '-i'
        }
        $grepArguments += @('-e', $pattern.Regex, $commit, '--')
        $historyOutput = @(& git -C $repoRoot @grepArguments 2>$null)
        $exitCode = $LASTEXITCODE
        if ($exitCode -gt 1) {
            throw "git history scan failed with exit code ${exitCode} for commit $commit."
        }

        foreach ($relativePath in $historyOutput) {
            $findings.Add([ordered]@{
                Scope = 'git-history'
                Type = $pattern.Name
                Commit = [string]$commit
                File = [string]$relativePath
                Line = $null
            })
        }
    }
}

$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Could not determine the repository commit.'
}

$report = [ordered]@{
    SchemaVersion = 1
    RecordedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
    RepositoryCommit = $commit
    WorkingTreeFilesScanned = $trackedFiles.Count
    HistoryCommitsScanned = $commits.Count
    FindingCount = $findings.Count
    Findings = @($findings)
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8
Write-Host "Secret scan report: $resolvedOutputPath"
Write-Host "Scanned $($trackedFiles.Count) tracked files across $($commits.Count) commits; findings: $($findings.Count)"

if ($FailOnMatch -and $findings.Count -gt 0) {
    throw "Secret scan found $($findings.Count) potential secret location(s); see the redacted report for file/type locations."
}
