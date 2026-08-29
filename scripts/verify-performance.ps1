[CmdletBinding()]
param(
    [string]$OutputPath = 'artifacts/luna20/performance-run.json',

    [string]$BaselinePath = 'docs/audit/LUNA-20-PERFORMANCE-BASELINE.json',

    [switch]$EnforceRegression,

    [int]$Iterations = 3,

    [int]$MemoryCycles = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$solutionPath = Join-Path $repoRoot 'ProjectCostForecast.sln'
$benchmarkProjectPath = Join-Path $repoRoot 'tests\ProjectCostForecast.Tests\ProjectCostForecast.Tests.csproj'
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }

function Invoke-RequiredNativeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Host "==> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath"
    }
}

if ($Iterations -le 0 -or $MemoryCycles -le 0) {
    throw 'Iterations and MemoryCycles must be positive.'
}

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
Invoke-RequiredNativeCommand -FilePath $dotnet -Arguments @(
    'build', $solutionPath, '-c', 'Release', '--no-restore'
)
Invoke-RequiredNativeCommand -FilePath $dotnet -Arguments @(
    'run', '--project', $benchmarkProjectPath, '-c', 'Release', '--no-build', '--no-restore', '--',
    '--luna20', '--mode', 'post-change', '--output', $resolvedOutputPath,
    '--iterations', $Iterations.ToString(), '--memory-cycles', $MemoryCycles.ToString(), '--commit', $commit
)

$current = Get-Content -LiteralPath $resolvedOutputPath -Raw | ConvertFrom-Json
if ($current.Scenarios.Count -ne 21) {
    throw "Expected 21 LUNA-20 scenario measurements, found $($current.Scenarios.Count)."
}
if ($current.Datasets.Count -ne 3) {
    throw "Expected three LUNA-20 workload profiles, found $($current.Datasets.Count)."
}

if ($EnforceRegression) {
    $resolvedBaselinePath = if ([System.IO.Path]::IsPathRooted($BaselinePath)) { $BaselinePath } else { Join-Path $repoRoot $BaselinePath }
    if (-not (Test-Path -LiteralPath $resolvedBaselinePath -PathType Leaf)) {
        throw "Baseline report was not found: $resolvedBaselinePath"
    }

    $baseline = Get-Content -LiteralPath $resolvedBaselinePath -Raw | ConvertFrom-Json
    $baselineByKey = @{}
    foreach ($scenario in $baseline.Scenarios) {
        $baselineByKey["$($scenario.Dataset)/$($scenario.Name)"] = $scenario
    }

    foreach ($scenario in $current.Scenarios) {
        $key = "$($scenario.Dataset)/$($scenario.Name)"
        if (-not $baselineByKey.ContainsKey($key)) {
            throw "Baseline report has no scenario '$key'."
        }

        $baselineScenario = $baselineByKey[$key]
        $allowed = [math]::Max(10.0, [double]$baselineScenario.P95Milliseconds * 0.25)
        $limit = [double]$baselineScenario.P95Milliseconds + $allowed
        if ([double]$scenario.P95Milliseconds -gt $limit) {
            throw "LUNA-20 regression for ${key}: p95 $($scenario.P95Milliseconds) ms exceeds baseline $($baselineScenario.P95Milliseconds) ms plus tolerance $allowed ms."
        }
    }

    Write-Host 'LUNA-20 baseline comparison passed: no p95 scenario exceeded baseline + max(10 ms, 25%).'
}

Write-Host 'LUNA-20 performance verification passed.'
