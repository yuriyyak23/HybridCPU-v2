param([Parameter(Mandatory)][string]$EvidenceRoot, [Parameter(Mandatory)][string]$Runner)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed = (Join-Path $repo 'TempEnv/doom-continuation-20260905') + [IO.Path]::DirectorySeparatorChar
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
if (!$EvidenceRoot.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $EvidenceRoot)) {
    throw 'A new continuation evidence directory is required.'
}
New-Item -ItemType Directory -Path $EvidenceRoot | Out-Null
$env:TEMP = Join-Path $EvidenceRoot 'tmp'; $env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = Join-Path $EvidenceRoot 'cli'; $env:NUGET_PACKAGES = Join-Path $EvidenceRoot 'packages'
foreach ($path in @($env:TEMP,$env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES)) { New-Item -ItemType Directory -Path $path | Out-Null }
$shell = (Get-Process -Id $PID).Path
$dotnet = (Get-Command dotnet.exe).Source
$launcher = Join-Path $PSScriptRoot 'Start-IndependentRun.ps1'
$validator = Join-Path $PSScriptRoot 'Test-LauncherTerminal.ps1'
$checks = [Collections.Generic.List[object]]::new()
function Write-New($Path, $Value) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Depth 40))
        $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true)
    } finally { $stream.Dispose() }
}
function Check($Name, [bool]$Condition) {
    $checks.Add(@{Name=$Name;Passed=$Condition})
    if (!$Condition) { throw "Failed: $Name" }
    Write-Output "PASS $Name"
}
function Invoke-Dispatcher($Name, $Script, $Hash) {
    $argsText = '-NoProfile -NonInteractive -File "'+$launcher+'" -RunScript "'+$Script+'" -ExpectedScriptSha256 '+$Hash
    $process = Start-Process -FilePath $shell -ArgumentList $argsText -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $EvidenceRoot ($Name+'.stdout.log')) -RedirectStandardError (Join-Path $EvidenceRoot ($Name+'.stderr.log'))
    $null = $process.Handle # Retain the native handle so Windows PowerShell preserves ExitCode.
    $identity = @{Pid=$process.Id; StartUtc=$process.StartTime.ToUniversalTime().ToString('O'); Executable=$shell; Arguments=$argsText}
    $process.WaitForExit()
    $identity.ExitCode=$process.ExitCode; $identity.ExitObservedUtc=[DateTime]::UtcNow.ToString('O')
    Write-New (Join-Path $EvidenceRoot ($Name+'.json')) $identity
    return $identity
}
$failure=$null
try {
    $imageDir=Join-Path $EvidenceRoot 'image'
    $createArgs=@($Runner,'test-launcher-create-image',$imageDir)
    Write-New (Join-Path $EvidenceRoot 'create-command.json') @{Executable=$dotnet;Arguments=$createArgs}
    & $dotnet @createArgs 1> (Join-Path $EvidenceRoot 'create.stdout.log') 2> (Join-Path $EvidenceRoot 'create.stderr.log')
    $createExit=$LASTEXITCODE
    Write-New (Join-Path $EvidenceRoot 'create-exit.json') @{ExitCode=$createExit}
    Check 'tiny image creation' ($createExit -eq 0)
    $runDir=Join-Path $EvidenceRoot 'run'
    New-Item -ItemType Directory -Path $runDir | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $runDir 'tmp') | Out-Null
    $runScript=Join-Path $runDir 'Run.ps1'
    $stream=[IO.File]::Open($runScript,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write)
    try { $bytes=[IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'LauncherFixture.Run.ps1')); $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally {$stream.Dispose()}
    $image=Join-Path $imageDir 'observation-lifecycle.hcexe'
    $files=@(Get-ChildItem -LiteralPath (Split-Path $Runner) -File | Where-Object {$_.Extension -in @('.dll','.json','.exe')} | ForEach-Object {
        @{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    }) + @(@{Path=$image;Sha256=(Get-FileHash -LiteralPath $image -Algorithm SHA256).Hash},
        @{Path=$dotnet;Sha256=(Get-FileHash -LiteralPath $dotnet -Algorithm SHA256).Hash})
    $expected=[pscustomobject]@{
        RunId=[Guid]::NewGuid().ToString(); CreatedUtc=[DateTime]::UtcNow.ToString('O')
        ScriptSha256=(Get-FileHash -LiteralPath $runScript -Algorithm SHA256).Hash
        Runner=$Runner;RunnerSha256=(Get-FileHash -LiteralPath $Runner -Algorithm SHA256).Hash
        Image=$image;ImageSha256=(Get-FileHash -LiteralPath $image -Algorithm SHA256).Hash
        Dotnet=$dotnet;Output=(Join-Path $runDir 'loader');Files=$files
        CliHome=$env:DOTNET_CLI_HOME;Packages=$env:NUGET_PACKAGES
    }
    Write-New (Join-Path $runDir 'inputs.json') $expected
    $invalid=Invoke-Dispatcher 'invalid-path' $launcher $expected.ScriptSha256
    Check 'outside-root script rejected' ($invalid.ExitCode -ne 0)
    $invalid=Invoke-Dispatcher 'invalid-sha' $runScript ('0'*64)
    Check 'SHA mismatch rejected before claim' ($invalid.ExitCode -ne 0 -and !(Test-Path (Join-Path $runDir 'scheduler-launch.claim')))
    $dispatch=Invoke-Dispatcher 'dispatch' $runScript $expected.ScriptSha256
    Check 'dispatcher native exit zero' ($dispatch.ExitCode -eq 0)
    $registration=Get-Content (Join-Path $runDir 'scheduler-registration.json') -Raw | ConvertFrom-Json
    $record=Get-Content (Join-Path $runDir 'scheduler-dispatch.json') -Raw | ConvertFrom-Json
    Check 'registration and dispatch persisted before exit' ($registration.DispatcherPid -eq $dispatch.Pid -and
        $registration.RunScript -eq $runScript -and $registration.ScriptSha256 -eq $expected.ScriptSha256 -and
        $registration.TaskName -eq $record.TaskName -and ![string]::IsNullOrWhiteSpace($record.InstanceGuid) -and
        [DateTime]::Parse($registration.RegisteredUtc) -le [DateTime]::Parse($record.DispatchedUtc) -and
        [DateTime]::Parse($record.DispatchedUtc) -le [DateTime]::Parse($dispatch.ExitObservedUtc))
    [xml]$xml=$registration.TaskXml
    Check 'scheduler no triggers retry or timeout' ($xml.Task.Settings.ExecutionTimeLimit -eq 'PT0S' -and
        $null -eq $xml.Task.Settings.SelectSingleNode('*[local-name()="RestartOnFailure"]') -and
        $xml.SelectNodes('//*[local-name()="Triggers"]/*').Count -eq 0)
    $deadline=[DateTime]::UtcNow.AddSeconds(20)
    while (!(Test-Path (Join-Path $runDir 'started.json')) -and [DateTime]::UtcNow -lt $deadline) {Start-Sleep -Milliseconds 200}
    $started=Get-Content (Join-Path $runDir 'started.json') -Raw | ConvertFrom-Json
    $owned=Get-Process -Id $started.Pid
    Check 'frozen fixture continues after dispatcher exit' ($owned.StartTime.ToUniversalTime() -eq [DateTime]::Parse($started.ProcessStartUtc).ToUniversalTime() -and
        $started.RunId -eq $expected.RunId -and !(Test-Path (Join-Path $runDir 'finished.json')))
    Write-New (Join-Path $EvidenceRoot 'after-dispatch.json') @{ObservedUtc=[DateTime]::UtcNow.ToString('O');Dispatcher=$dispatch;Fixture=$started;TerminalAbsent=$true}
    $duplicate=Invoke-Dispatcher 'duplicate' $runScript $expected.ScriptSha256
    Check 'duplicate launch claim rejected' ($duplicate.ExitCode -ne 0)
    $deadline=[DateTime]::UtcNow.AddSeconds(180)
    while (!(Test-Path (Join-Path $runDir 'finished.json')) -and [DateTime]::UtcNow -lt $deadline) {Start-Sleep -Milliseconds 500}
    $terminal=& $validator -Directory $runDir -Expected $expected
    Check 'exact durable bounded terminal result' ($terminal.Outcome -eq 'Passed')
    foreach($kind in @('missing','partial','malformed','mismatched','stale','missing-finished')) {
        $negativeDir=Join-Path $EvidenceRoot ('negative-'+$kind)
        New-Item -ItemType Directory -Path $negativeDir | Out-Null
        foreach($name in @('started.json','native-exit.json')) {[IO.File]::Copy((Join-Path $runDir $name),(Join-Path $negativeDir $name),$false)}
        $copy=$terminal | ConvertTo-Json -Depth 40 | ConvertFrom-Json
        if($kind -eq 'mismatched') {$copy.ImageSha256='0'*64}
        if($kind -eq 'stale') {$copy.RunId=[Guid]::NewGuid().ToString()}
        if($kind -eq 'partial') {$copy=[pscustomobject]@{Schema=$copy.Schema;RunId=$copy.RunId}}
        if($kind -ne 'missing') {
            if($kind -eq 'malformed') {[IO.File]::WriteAllText((Join-Path $negativeDir 'terminal.json'),'{')}
            else {Write-New (Join-Path $negativeDir 'terminal.json') $copy}
        }
        if($kind -ne 'missing-finished') {Write-New (Join-Path $negativeDir 'finished.json') @{
            RunId=$expected.RunId;TerminalSha256=$(if($kind -eq 'missing') {'0'*64} else {(Get-FileHash (Join-Path $negativeDir 'terminal.json')).Hash})
        }}
        $rejected=$false
        try {$null=& $validator -Directory $negativeDir -Expected $expected} catch {$rejected=$true}
        Check ('reject '+$kind+' terminal') $rejected
    }
    # Only this test registration is addressed; no enumeration or control of other tasks/processes.
    $scheduler=New-Object -ComObject Schedule.Service; $scheduler.Connect(); $folder=$scheduler.GetFolder('\')
    $task=$folder.GetTask($registration.TaskName)
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    while($task.State -eq 4 -and [DateTime]::UtcNow -lt $deadline) {Start-Sleep -Milliseconds 200; $task=$folder.GetTask($registration.TaskName)}
    Check 'scheduler terminal exit zero' ($task.State -ne 4 -and $task.LastTaskResult -eq 0)
    $null=& $validator -Directory $runDir -Expected $expected
    Write-New (Join-Path $EvidenceRoot 'cleanup-authorized.json') @{TaskName=$registration.TaskName;State=$task.State;LastTaskResult=$task.LastTaskResult;TerminalSha256=(Get-FileHash (Join-Path $runDir 'terminal.json')).Hash}
    $folder.DeleteTask($registration.TaskName,0)
    Write-New (Join-Path $EvidenceRoot 'cleanup-completed.json') @{TaskName=$registration.TaskName;Utc=[DateTime]::UtcNow.ToString('O')}
    Check 'cleanup follows verified terminal completion' $true
} catch { $failure=$_.Exception.ToString(); Write-Output $failure }
Write-New (Join-Path $EvidenceRoot 'acceptance.json') @{
    Passed=@($checks | Where-Object {$_.Passed}).Count;Failed=[Math]::Max(@($checks | Where-Object {!$_.Passed}).Count,[int]($null -ne $failure))
    Skipped=[Math]::Max(0,17-$checks.Count)
    Checks=$checks.ToArray();Failure=$failure;Qualified=$false;CodexApplicationExitSurvival='Unproven'
}
if($null -ne $failure) {exit 1}
exit 0
