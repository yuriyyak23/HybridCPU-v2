param([Parameter(Mandatory)][string]$EvidenceRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$receipt = Get-Content -LiteralPath (Join-Path $EvidenceRoot 'verified-run.json') -Raw | ConvertFrom-Json
if ($receipt.BuildExit -ne 0 -or $receipt.TestExit -ne 0 -or $receipt.IsolationExit -ne 0) { throw 'Run did not pass all requested checks' }
foreach ($input in $receipt.Inputs) {
    if ((Get-FileHash -LiteralPath (Join-Path $repoRoot $input.Path) -Algorithm SHA256).Hash -ne $input.SHA256) { throw "Stale evidence input: $($input.Path)" }
}
$resultPath = Join-Path $EvidenceRoot $receipt.ResultFile
if ((Get-FileHash -LiteralPath $resultPath -Algorithm SHA256).Hash -ne $receipt.ResultHash) { throw 'Result file changed since verified run' }
$results = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (@($results.tests).Count -eq 0 -or @($results.tests | Where-Object status -ne 'PASS').Count -ne 0) { throw 'Empty or failing result set' }
$text = @"
# Verified standalone check receipt

Completed UTC: $($receipt.CompletedUtc). Results: $($receipt.ResultFile).
Actual PASS groups: $(@($results.tests).Count). Assertions: $($results.assertions).
Build/test/isolation exits: $($receipt.BuildExit)/$($receipt.TestExit)/$($receipt.IsolationExit).
Input hashes and result hash are recorded in verified-run.json and revalidated.

This receipt does not close SF phases. Plan status remains PARTIAL until each exit
criterion and open audit finding is reviewed. Repository-wide tests, live CPU
execution, calibrated energy and RTL/PPA are NOT RUN / NOT EVALUATED here.
Historical phase reports are not overwritten. See 03-Audit-Remediation.md for
requirement/finding status. IMPLEMENTED-STANDALONE / NOT INTEGRATED.
"@
Set-Content -LiteralPath (Join-Path $EvidenceRoot 'verified-checks.md') -Value $text
Write-Output 'Verified fresh evidence receipt; no automatic phase closure.'
