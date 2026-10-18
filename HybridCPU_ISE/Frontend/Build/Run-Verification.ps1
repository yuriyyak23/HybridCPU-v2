param([switch]$TestsOnly, [string]$EvidenceRoot = '')
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
if (!$EvidenceRoot) { $EvidenceRoot = Join-Path $repoRoot ('HybridCPU_ISE\Frontend\docs\verification-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff')) }
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
New-Item -ItemType Directory -Path $EvidenceRoot -Force | Out-Null
function Inputs {
    $files = @(Get-ChildItem (Join-Path $repoRoot 'HybridCPU_ISE\Frontend') -Recurse -File | Where-Object Extension -In '.cs','.csproj','.ps1') +
        @(Get-ChildItem (Join-Path $repoRoot 'ShadowFrontend.Tests') -File | Where-Object Extension -In '.cs','.csproj') +
        @(Get-Item (Join-Path $repoRoot 'global.json'),(Join-Path $repoRoot 'Directory.Build.props'),(Join-Path $repoRoot 'HybridCPU_ISE\HybridCPU_ISE.csproj'))
    $files | Sort-Object FullName | ForEach-Object { [pscustomobject]@{ Path=$_.FullName.Substring($repoRoot.Length+1); SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } }
}
Push-Location $repoRoot
try {
    $before = @(Inputs)
    & dotnet build 'HybridCPU_ISE\Frontend\Build\ShadowFrontend.csproj' *> (Join-Path $EvidenceRoot 'build.log')
    $buildExit = $LASTEXITCODE
    Set-Content (Join-Path $EvidenceRoot 'build.exit') $buildExit
    if ($buildExit -ne 0) { throw 'Standalone build failed' }
    $runArguments = @('run','--project','ShadowFrontend.Tests\ShadowFrontend.Tests.csproj','--',$EvidenceRoot)
    if ($TestsOnly) { $runArguments += '--tests-only' }
    & dotnet @runArguments *> (Join-Path $EvidenceRoot 'checks.log')
    $testExit = $LASTEXITCODE
    Set-Content (Join-Path $EvidenceRoot 'checks.exit') $testExit
    if ($testExit -ne 0) { throw 'Standalone checks failed' }
    & (Join-Path $PSScriptRoot 'Verify-Isolation.ps1') -RepositoryRoot $repoRoot -EvidenceRoot $EvidenceRoot *> (Join-Path $EvidenceRoot 'isolation.log')
    if ((ConvertTo-Json -InputObject $before -Compress) -ne (ConvertTo-Json -InputObject @(Inputs) -Compress)) { throw 'Sources changed during verification' }
    $resultFile = if ($TestsOnly) { 'correctness-results.json' } else { 'test-results.json' }
    [pscustomobject]@{ CompletedUtc=[DateTime]::UtcNow.ToString('o'); BuildExit=$buildExit; TestExit=$testExit; IsolationExit=0;
        ResultFile=$resultFile; ResultHash=(Get-FileHash (Join-Path $EvidenceRoot $resultFile) -Algorithm SHA256).Hash; Inputs=$before } |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $EvidenceRoot 'verified-run.json')
    & (Join-Path $PSScriptRoot 'Write-PhaseEvidence.ps1') -EvidenceRoot $EvidenceRoot
} finally { Pop-Location }
