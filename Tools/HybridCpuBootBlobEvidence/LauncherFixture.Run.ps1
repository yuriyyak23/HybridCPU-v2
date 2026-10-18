$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Write-New($Path, $Value) {
    $bytes = [Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Depth 40))
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}
function Require-Hash($Path, $Expected) {
    if ($Expected -notmatch '^[a-fA-F0-9]{64}$' -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) {
        throw "Identity mismatch: $Path"
    }
}
$root = $PSScriptRoot
$inputIdentity = Get-Content -LiteralPath (Join-Path $root 'inputs.json') -Raw | ConvertFrom-Json
$env:TEMP = Join-Path $root 'tmp'; $env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = $inputIdentity.CliHome; $env:NUGET_PACKAGES = $inputIdentity.Packages
$nativeExit = $null
$artifacts = @()
$failure = $null
$startedUtc = [DateTime]::UtcNow.ToString('O')
try {
    Require-Hash $PSCommandPath $inputIdentity.ScriptSha256
    foreach ($file in $inputIdentity.Files) { Require-Hash $file.Path $file.Sha256 }
    $self = Get-Process -Id $PID
    Write-New (Join-Path $root 'started.json') ([ordered]@{
        RunId=$inputIdentity.RunId; Pid=$PID; ProcessStartUtc=$self.StartTime.ToUniversalTime().ToString('O')
        Command=[Environment]::CommandLine; StartedUtc=$startedUtc; ScriptSha256=$inputIdentity.ScriptSha256
    })
    # Test-only delay makes dispatcher/fixture lifetime separation observable; no CPU is running here.
    Start-Sleep -Seconds 12
    foreach ($file in $inputIdentity.Files) { Require-Hash $file.Path $file.Sha256 }
    $arguments = '"' + $inputIdentity.Runner + '" test-launcher-loader "' + $inputIdentity.Output + '" "' + $inputIdentity.Image + '"'
    $native = Start-Process -FilePath $inputIdentity.Dotnet -ArgumentList $arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $root 'native.stdout.log') -RedirectStandardError (Join-Path $root 'native.stderr.log')
    $null = $native.Handle
    Write-New (Join-Path $root 'native-started.json') ([ordered]@{
        RunId=$inputIdentity.RunId; Pid=$native.Id; ProcessStartUtc=$native.StartTime.ToUniversalTime().ToString('O')
        Executable=$inputIdentity.Dotnet; Arguments=$arguments
    })
    $native.WaitForExit()
    $nativeExit = $native.ExitCode
    Write-New (Join-Path $root 'native-exit.json') @{ExitCode=$nativeExit; RunId=$inputIdentity.RunId}
    if ($nativeExit -ne 0) { throw "Native fixture exit: $nativeExit" }
    $evidence = Get-Content -LiteralPath (Join-Path $inputIdentity.Output 'loader-transport-evidence.json') -Raw | ConvertFrom-Json
    if ($evidence.Image -ne $inputIdentity.Image -or $evidence.ImageSha256 -ne $inputIdentity.ImageSha256 -or
        $evidence.ClientExitCode -ne 0 -or $evidence.Observed.IsSuccess -ne $true -or
        $evidence.Observed.ObservationDiagnostics.SegmentsObserved -ne 3) { throw 'Loader outcome/identity absent or inconsistent.' }
    foreach ($file in $inputIdentity.Files) { Require-Hash $file.Path $file.Sha256 }
    $artifacts = @('native.stdout.log','native.stderr.log','native-started.json','native-exit.json','loader/loader-transport-evidence.json') | ForEach-Object {
        $path = Join-Path $root $_
        @{Path=$path;Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
    }
} catch { $failure = $_.Exception.ToString() }
$report = [ordered]@{
    Schema='hybridcpu.launcher-fixture/v1'; RunId=$inputIdentity.RunId; ScriptSha256=$inputIdentity.ScriptSha256
    Runner=$inputIdentity.Runner; RunnerSha256=$inputIdentity.RunnerSha256
    Image=$inputIdentity.Image; ImageSha256=$inputIdentity.ImageSha256
    StartedUtc=$startedUtc; FinishedUtc=[DateTime]::UtcNow.ToString('O'); NativeExitCode=$nativeExit
    Outcome=$(if ($null -eq $failure) {'Passed'} else {'Failed'}); Failure=$failure
    CycleBudget=20000; Qualified=$false; Artifacts=@($artifacts)
}
Write-New (Join-Path $root 'terminal.pending.json') $report
[IO.File]::Move((Join-Path $root 'terminal.pending.json'), (Join-Path $root 'terminal.json'))
Write-New (Join-Path $root 'finished.json') @{
    RunId=$inputIdentity.RunId; TerminalSha256=(Get-FileHash -LiteralPath (Join-Path $root 'terminal.json') -Algorithm SHA256).Hash
}
if ($null -ne $failure) { exit 1 }
exit 0
