[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [Parameter(Mandatory = $true)]
    [string]$WadPath,

    [string]$ObservationManifest,

    [ValidateRange(250, 60000)]
    [int]$SampleIntervalMilliseconds = 1000
)

$ErrorActionPreference = 'Stop'

function Require-ChildPath {
    param([string]$Path, [string]$Parent, [string]$Label)
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullParent = [IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($fullParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must be beneath $fullParent"
    }
    $fullPath
}

function Quote-WindowsArgument {
    param([string]$Value)
    '"' + $Value.Replace('"', '\\"') + '"'
}

$sourceRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$doomRoot = [IO.Path]::GetFullPath((Join-Path $sourceRoot '..'))
$repoRoot = [IO.Path]::GetFullPath((Join-Path $doomRoot '..'))
$tempRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'TempEnv'))
$output = Require-ChildPath $OutputRoot $tempRoot 'OutputRoot'
$image = [IO.Path]::GetFullPath($ImagePath)
$wad = [IO.Path]::GetFullPath($WadPath)
$manifest = if ([string]::IsNullOrWhiteSpace($ObservationManifest)) { $null } else { [IO.Path]::GetFullPath($ObservationManifest) }

if (Test-Path -LiteralPath $output) { throw "OutputRoot already exists: $output" }
if (-not (Test-Path -LiteralPath $image -PathType Leaf)) { throw "HCEXE image is missing: $image" }
if ([IO.Path]::GetExtension($image) -ine '.hcexe') { throw "Image must have the .hcexe extension: $image" }
if (-not (Test-Path -LiteralPath $wad -PathType Leaf)) { throw "WAD is missing: $wad" }
if ($manifest -and -not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw "Observation manifest is missing: $manifest" }

New-Item -ItemType Directory -Path $output, (Join-Path $output 'run'), (Join-Path $output 'tmp') | Out-Null
$launchRecord = [ordered]@{
    Schema = 'hybridcpu.doom-gui-autostart/v1'
    StartedUtc = [DateTime]::UtcNow.ToString('O')
    Repository = $repoRoot
    Image = $image
    ImageSha256 = (Get-FileHash -LiteralPath $image -Algorithm SHA256).Hash.ToLowerInvariant()
    Wad = $wad
    WadSha256 = (Get-FileHash -LiteralPath $wad -Algorithm SHA256).Hash.ToLowerInvariant()
    ObservationManifest = $manifest
    SampleIntervalMilliseconds = $SampleIntervalMilliseconds
    Qualification = 'GUI launch and host CPU sampling are diagnostic only.'
}
$launchRecord | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'launch-preflight.json')

$builder = Join-Path $doomRoot 'tools/Build-HybridCpuGui.ps1'
if (-not (Test-Path -LiteralPath $builder -PathType Leaf)) { throw "GUI build script is missing: $builder" }
& $builder -OutputRoot (Join-Path $output 'gui-build') *>&1 | Tee-Object -FilePath (Join-Path $output 'gui-build-invocation.log')
$buildExit = $LASTEXITCODE
$buildExit | Set-Content -LiteralPath (Join-Path $output 'gui-build.exit.txt')
if ($buildExit -ne 0) { exit $buildExit }

$exe = Join-Path $output 'gui-build/artifacts/bin/DoomSharp.HybridCpu.Windows/release/DoomSharp.HybridCpu.Windows.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "GUI build reported success but executable is missing: $exe" }

$runDirectory = Join-Path $output 'run'
$profileScript = Join-Path $runDirectory 'Monitor-GuiProcess.ps1'
@'
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$CsvPath,
    [Parameter(Mandatory = $true)][string]$ResultPath,
    [Parameter(Mandatory = $true)][int]$IntervalMilliseconds
)
$ErrorActionPreference = 'Stop'
'Utc,ProcessId,TotalProcessorTimeSeconds,CpuDeltaSeconds,WorkingSetBytes,PrivateMemoryBytes,HandleCount,ThreadCount' |
    Set-Content -LiteralPath $CsvPath
$previousCpu = $null
try {
    while ($true) {
        $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($null -eq $process -or $process.HasExited) { break }
        $cpu = $process.TotalProcessorTime.TotalSeconds
        $delta = if ($null -eq $previousCpu) { '' } else { [Math]::Round($cpu - $previousCpu, 6) }
        $previousCpu = $cpu
        ('{0},{1},{2},{3},{4},{5},{6},{7}' -f [DateTime]::UtcNow.ToString('O'), $ProcessId, $cpu, $delta,
            $process.WorkingSet64, $process.PrivateMemorySize64, $process.HandleCount, $process.Threads.Count) |
            Add-Content -LiteralPath $CsvPath
        Start-Sleep -Milliseconds $IntervalMilliseconds
    }
    [pscustomobject]@{ CompletedUtc = [DateTime]::UtcNow.ToString('O'); ProcessId = $ProcessId; ObservedExit = $true } |
        ConvertTo-Json | Set-Content -LiteralPath $ResultPath
}
catch {
    [pscustomobject]@{ CompletedUtc = [DateTime]::UtcNow.ToString('O'); ProcessId = $ProcessId; ObservedExit = $false; Error = $_.Exception.Message } |
        ConvertTo-Json | Set-Content -LiteralPath $ResultPath
    exit 1
}
'@ | Set-Content -LiteralPath $profileScript -Encoding utf8

$arguments = @('--run', $image, $wad)
if ($manifest) { $arguments += @('--observation-manifest', $manifest) }
$guiArgumentLine = ($arguments | ForEach-Object { Quote-WindowsArgument $_ }) -join ' '
$gui = Start-Process -FilePath $exe -ArgumentList $guiArgumentLine -WorkingDirectory (Split-Path -Parent $exe) -PassThru `
    -RedirectStandardOutput (Join-Path $runDirectory 'gui.stdout.log') `
    -RedirectStandardError (Join-Path $runDirectory 'gui.stderr.log')

$monitorArguments = @(
    '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $profileScript,
    '-ProcessId', $gui.Id,
    '-CsvPath', (Join-Path $runDirectory 'cpu-profile.csv'),
    '-ResultPath', (Join-Path $runDirectory 'cpu-profile-result.json'),
    '-IntervalMilliseconds', $SampleIntervalMilliseconds)
$monitorArgumentLine = ($monitorArguments | ForEach-Object { Quote-WindowsArgument ([string]$_) }) -join ' '
$monitor = Start-Process -FilePath (Get-Process -Id $PID).Path -WindowStyle Hidden -PassThru -ArgumentList $monitorArgumentLine

[pscustomobject]@{
    DispatchedUtc = [DateTime]::UtcNow.ToString('O')
    GuiProcessId = $gui.Id
    MonitorProcessId = $monitor.Id
    GuiExecutable = $exe
    GuiArguments = $arguments
    CpuProfile = Join-Path $runDirectory 'cpu-profile.csv'
    MonitorResult = Join-Path $runDirectory 'cpu-profile-result.json'
    StandardOutput = Join-Path $runDirectory 'gui.stdout.log'
    StandardError = Join-Path $runDirectory 'gui.stderr.log'
    Note = 'The GUI and monitor are detached. Their running state is not a guest completion or qualification claim.'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'launch-dispatch.json')

Write-Output (Join-Path $output 'launch-dispatch.json')
exit 0
