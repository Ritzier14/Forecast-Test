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

    # With fewer than 20 samples, the nearest-rank p95 is the maximum sample.
    # That makes the default three-sample desktop run an outlier gate rather
    # than a useful percentile comparison. Keep p95 in every report, but use
    # the median for short runs and reserve p95 enforcement for two reports
    # that both have enough samples for a meaningful percentile.
    $useP95 = [int]$current.Runtime.Iterations -ge 20 -and [int]$baseline.Runtime.Iterations -ge 20
    $comparisonMetric = if ($useP95) { 'P95Milliseconds' } else { 'MedianMilliseconds' }
    $comparisonLabel = if ($useP95) { 'p95' } else { 'median' }

    foreach ($scenario in $current.Scenarios) {
        $key = "$($scenario.Dataset)/$($scenario.Name)"
        if (-not $baselineByKey.ContainsKey($key)) {
            throw "Baseline report has no scenario '$key'."
        }

        $baselineScenario = $baselineByKey[$key]
        $baselineMeasurement = [double]$baselineScenario.$comparisonMetric
        $currentMeasurement = [double]$scenario.$comparisonMetric
        $allowed = [math]::Max(50.0, $baselineMeasurement * 0.25)
        $limit = $baselineMeasurement + $allowed
        if ($currentMeasurement -gt $limit) {
            throw "LUNA-20 regression for ${key}: $comparisonLabel $currentMeasurement ms exceeds baseline $baselineMeasurement ms plus tolerance $allowed ms."
        }
    }

    if ($useP95) {
        Write-Host 'LUNA-20 baseline comparison passed: no p95 scenario exceeded baseline + max(50 ms, 25%).'
    }
    else {
        Write-Host 'LUNA-20 baseline comparison passed: no median scenario exceeded baseline + max(50 ms, 25%); p95 remains diagnostic for short runs.'
    }
}

Write-Host 'LUNA-20 performance verification passed.'
