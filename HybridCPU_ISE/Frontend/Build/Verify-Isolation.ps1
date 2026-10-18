param([string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path, [string]$EvidenceRoot = '')
$ErrorActionPreference = 'Stop'
$baselineRoot = Join-Path $RepositoryRoot 'HybridCPU_ISE\Frontend\docs\ShadowFrontend-Refactoring-Plan\evidence'
if (!$EvidenceRoot) { $EvidenceRoot = $baselineRoot }
New-Item -ItemType Directory -Path $EvidenceRoot -Force | Out-Null
function Evaluate([string]$project, [string]$configuration, [string]$name) {
    $lines = & dotnet msbuild (Join-Path $RepositoryRoot $project) "-p:Configuration=$configuration" '-getItem:Compile,ProjectReference' '-getProperty:BaseIntermediateOutputPath,MSBuildProjectExtensionsPath,OutputPath,DefineTestSupport,TargetFramework'
    if ($LASTEXITCODE -ne 0) { throw "Evaluation failed: $project" }
    $json = $lines -join [Environment]::NewLine
    Set-Content -LiteralPath (Join-Path $evidenceRoot "$name.json") -Value $json
    return ($json | ConvertFrom-Json)
}
$iseDebug = Evaluate 'HybridCPU_ISE\HybridCPU_ISE.csproj' 'Debug' 'final-ise-debug-items'
$iseRelease = Evaluate 'HybridCPU_ISE\HybridCPU_ISE.csproj' 'Release' 'final-ise-release-items'
$oldTests = Evaluate 'HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj' 'Debug' 'final-existing-tests-items'
$shadow = Evaluate 'HybridCPU_ISE\Frontend\Build\ShadowFrontend.csproj' 'Debug' 'final-shadow-items'
$tests = Evaluate 'ShadowFrontend.Tests\ShadowFrontend.Tests.csproj' 'Debug' 'final-standalone-tests-items'
$audit = Evaluate 'HybridCPU_ISE\Frontend\Build\Audit\FrontendAudit.csproj' 'Debug' 'final-audit-items'
foreach ($evaluated in @($iseDebug,$iseRelease,$oldTests)) {
    foreach ($item in $evaluated.Items.Compile) {
        if ($item.FullPath -match '[\\/]Frontend[\\/](Contracts|Semantic|Cache|Invalidation|Replay|Telemetry|Modeling|Build)[\\/]' -or $item.FullPath -match 'artifacts[\\/]shadow-frontend' -or $item.FullPath -match 'ShadowFrontend.Tests') {
            throw "Isolation failure: $($item.FullPath)"
        }
    }
    foreach ($item in $evaluated.Items.ProjectReference) { if ($item.Identity -match 'ShadowFrontend') { throw 'Reverse runtime dependency' } }
}
if (@($shadow.Items.ProjectReference).Count -ne 0) { throw 'Shadow production references external implementation' }
if (@($shadow.Items.Compile | Where-Object { $_.Identity -notlike '*.cs' }).Count -ne 0 -or @($shadow.Items.Compile).Count -ne 7) { throw 'Unexpected autonomous compile list' }
foreach ($evaluated in @($shadow,$tests,$audit)) {
    foreach ($property in @('BaseIntermediateOutputPath','MSBuildProjectExtensionsPath','OutputPath')) {
        $full = [IO.Path]::GetFullPath($evaluated.Properties.$property)
        $allowed = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot 'artifacts\shadow-frontend')) + [IO.Path]::DirectorySeparatorChar
        if (-not $full.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw "Generated/output path escaped: $full" }
    }
}
$baseline = Get-Content -LiteralPath (Join-Path $baselineRoot 'baseline-runtime-hashes.json') -Raw | ConvertFrom-Json
$changes = @($baseline | Where-Object { (Get-FileHash -LiteralPath (Join-Path $RepositoryRoot $_.Path) -Algorithm SHA256).Hash -ne $_.Sha256 })
ConvertTo-Json -InputObject $changes | Set-Content -LiteralPath (Join-Path $evidenceRoot 'runtime-hash-changes.json')
$packagingHash = (Get-Content -LiteralPath (Join-Path $baselineRoot 'authorized-packaging-project.sha256') -Raw).Trim()
$unexpected = @($changes | Where-Object { $_.Path -ne 'HybridCPU_ISE\HybridCPU_ISE.csproj' -or
    (Get-FileHash -LiteralPath (Join-Path $RepositoryRoot $_.Path) -Algorithm SHA256).Hash -ne $packagingHash })
if ($unexpected.Count -ne 0) { throw 'Unexpected runtime sources/build properties drift' }
$manifest = @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'HybridCPU_ISE\Frontend') -Recurse -File | Where-Object Extension -In '.cs','.csproj','.ps1') + @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'ShadowFrontend.Tests') -File | Where-Object Extension -In '.cs','.csproj')
$manifest | ForEach-Object { [pscustomobject]@{ Path=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidenceRoot 'final-source-manifest.json')
Write-Output 'PASS: Debug/Release ISE and existing test globs exclude Shadow; seven explicit sources; no production references; external outputs. Baseline drift retained in runtime-hash-changes.json; only exact authorized packaging project hash is allowed.'
