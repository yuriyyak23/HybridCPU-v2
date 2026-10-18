param(
    [Parameter(Mandatory)][string]$RunScript,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedScriptSha256
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'TempEnv/doom-continuation-20260905')) + [IO.Path]::DirectorySeparatorChar
$script = (Resolve-Path -LiteralPath $RunScript).Path
if (-not $script.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($script) -ne '.ps1') {
    throw 'Run script must be a frozen ps1 beneath the continuation evidence root.'
}
$directory = Split-Path -Parent $script
$env:TEMP = Join-Path $directory 'tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
$hash = (Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash
if ($hash -ne $ExpectedScriptSha256) { throw 'Run script SHA256 mismatch.' }
$claim = [IO.File]::Open((Join-Path $directory 'scheduler-launch.claim'), [IO.FileMode]::CreateNew)
$claim.Dispose()
$scheduler = New-Object -ComObject 'Schedule.Service'
$scheduler.Connect()
$folder = $scheduler.GetFolder('\')
$name = 'HybridCpuOwnedRun-' + [Guid]::NewGuid().ToString('N')
$definition = $scheduler.NewTask(0)
$definition.RegistrationInfo.Description = 'Explicit one-shot HybridCPU evidence script; no triggers or automatic retries.'
$definition.Principal.UserId = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$definition.Principal.LogonType = 3 # Existing interactive token; no stored password or elevation.
$definition.Settings.ExecutionTimeLimit = 'PT0S' # Guest cycle budget belongs to the run script.
$definition.Settings.DisallowStartIfOnBatteries = $false
$definition.Settings.StopIfGoingOnBatteries = $false
$definition.Settings.AllowDemandStart = $true
$definition.Settings.MultipleInstances = 2 # IgnoreNew: never duplicate an already running instance.
$action = $definition.Actions.Create(0)
$action.Path = (Get-Process -Id $PID).Path
$action.Arguments = '-NoProfile -NonInteractive -File "' + $script + '"'
$action.WorkingDirectory = $repo
$registered = $folder.RegisterTaskDefinition($name, $definition, 2, $definition.Principal.UserId, $null, 3)
# Persist discovery before dispatch so a disconnected client can find the task.
[pscustomobject]@{TaskName=$name;RunScript=$script;ScriptSha256=$hash.ToLowerInvariant();
    DispatcherPid=$PID;RegisteredUtc=[DateTime]::UtcNow.ToString('O');TaskXml=$registered.Xml} |
    ConvertTo-Json -Depth 4 | Set-Content (Join-Path $directory 'scheduler-registration.json')
$instance = $registered.Run($null)
[pscustomobject]@{TaskName=$name;InstanceGuid=$instance.InstanceGuid;DispatchedUtc=[DateTime]::UtcNow.ToString('O')} |
    ConvertTo-Json | Set-Content (Join-Path $directory 'scheduler-dispatch.json')
Write-Output $name
# Keep registration for observation; delete only this task after verified terminal outcome.
exit 0
