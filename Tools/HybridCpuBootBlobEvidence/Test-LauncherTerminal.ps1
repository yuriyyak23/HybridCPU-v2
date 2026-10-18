param([Parameter(Mandatory)][string]$Directory, [Parameter(Mandatory)]$Expected)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$reportPath = Join-Path $Directory 'terminal.json'
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$finished = Get-Content -LiteralPath (Join-Path $Directory 'finished.json') -Raw | ConvertFrom-Json
$started = Get-Content -LiteralPath (Join-Path $Directory 'started.json') -Raw | ConvertFrom-Json
$native = Get-Content -LiteralPath (Join-Path $Directory 'native-exit.json') -Raw | ConvertFrom-Json
foreach ($field in @('RunId','ScriptSha256','Runner','RunnerSha256','Image','ImageSha256')) {
    if ($report.$field -cne $Expected.$field) { throw "Terminal identity mismatch: $field" }
}
if ($report.Schema -cne 'hybridcpu.launcher-fixture/v1' -or $report.Outcome -cne 'Passed' -or
    $report.NativeExitCode -cne 0 -or $null -ne $report.Failure -or $report.Qualified -cne $false -or
    $report.CycleBudget -cne 20000 -or $native.ExitCode -cne 0 -or $native.RunId -cne $Expected.RunId -or
    $started.RunId -cne $Expected.RunId -or $finished.RunId -cne $Expected.RunId -or
    $started.ScriptSha256 -cne $Expected.ScriptSha256 -or $started.StartedUtc -cne $report.StartedUtc -or
    [DateTime]::Parse($report.StartedUtc).ToUniversalTime() -lt [DateTime]::Parse($Expected.CreatedUtc).ToUniversalTime() -or
    [DateTime]::Parse($report.FinishedUtc) -lt [DateTime]::Parse($report.StartedUtc) -or
    $finished.TerminalSha256 -ne (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash) {
    throw 'Terminal state, freshness or durable marker invalid.'
}
foreach ($file in $Expected.Files) {
    if ((Get-FileHash -LiteralPath $file.Path -Algorithm SHA256).Hash -ne $file.Sha256) { throw 'Frozen input changed or missing.' }
}
$paths=@('native.stdout.log','native.stderr.log','native-started.json','native-exit.json','loader/loader-transport-evidence.json')
if ($report.Artifacts.Count -ne $paths.Count) { throw 'Terminal artifact inventory incomplete.' }
for ($index=0; $index -lt $paths.Count; $index++) {
    $path=Join-Path (Split-Path $Expected.Output) $paths[$index]
    if ($report.Artifacts[$index].Path -cne $path -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $report.Artifacts[$index].Sha256) { throw 'Terminal artifact absent or mismatched.' }
}
$loader=Get-Content -LiteralPath (Join-Path $Expected.Output 'loader-transport-evidence.json') -Raw | ConvertFrom-Json
if ($loader.Image -cne $Expected.Image -or $loader.ImageSha256 -cne $Expected.ImageSha256 -or
    $loader.ClientExitCode -cne 0 -or $loader.Observed.IsSuccess -cne $true -or
    $loader.Observed.ObservationDiagnostics.SegmentsObserved -cne 3) { throw 'Loader evidence invalid.' }
$report
