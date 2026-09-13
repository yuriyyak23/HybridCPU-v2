[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [Parameter(Mandatory = $true)]
    [string]$SdkPackRoot,

    [switch]$Diagnostic
)

$ErrorActionPreference = 'Stop'

function Require-ChildPath {
    param([string]$Path, [string]$Parent, [string]$Label)
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullParent = [IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($fullParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must be beneath $fullParent"
    }
    return $fullPath
}

$sourceRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$doomRoot = [IO.Path]::GetFullPath((Join-Path $sourceRoot '..'))
$repoRoot = [IO.Path]::GetFullPath((Join-Path $doomRoot '..'))
$tempRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'TempEnv'))
$guestProject = Join-Path $sourceRoot 'DoomSharp.HybridCpu.Guest/DoomSharp.HybridCpu.Guest.csproj'
$output = Require-ChildPath $OutputRoot $tempRoot 'OutputRoot'
$pack = [IO.Path]::GetFullPath($SdkPackRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$buildName = Split-Path -Leaf $output.TrimEnd([IO.Path]::DirectorySeparatorChar)
$copyRoot = Join-Path $sourceRoot 'DoomSharp.HybridCpu.Windows/HCEXEBULDS'
$copyDestination = Join-Path $copyRoot $buildName

if (Test-Path -LiteralPath $output) { throw "OutputRoot already exists: $output" }
if (Test-Path -LiteralPath $copyDestination) { throw "HCEXEBULDS destination already exists: $copyDestination" }
if (-not (Test-Path -LiteralPath $guestProject -PathType Leaf)) { throw "Guest project is missing: $guestProject" }
foreach ($file in @('HybridCPU.Sdk.props', 'HybridCPU.Sdk.targets', 'hybridcpu.runtime-pack.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $pack $file) -PathType Leaf)) {
        throw "SDK pack is incomplete; missing $file beneath $pack"
    }
}

$competing = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -match '^(dotnet|ilc|csc)\.exe$' -and
    $_.CommandLine -match '(publish-graph|compile-image|dotnet publish| ilc\.dll )'
})
if ($competing.Count -ne 0) {
    $detail = $competing | Select-Object ProcessId, Name, CreationDate, CommandLine | ConvertTo-Json -Depth 3
    throw "A publish/compile-image process is already active; no second publication was started.`n$detail"
}

New-Item -ItemType Directory -Path $output, (Join-Path $output 'tmp'), (Join-Path $output 'cli'),
    (Join-Path $output 'packages'), (Join-Path $output 'publish'), (Join-Path $output 'artifacts') | Out-Null
$env:TEMP = Join-Path $output 'tmp'
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = Join-Path $output 'cli'
$env:NUGET_PACKAGES = Join-Path $output 'packages'

$preflight = [ordered]@{
    Schema = 'hybridcpu.doom-hcexe-build/v1'
    StartedUtc = [DateTime]::UtcNow.ToString('O')
    Repository = $repoRoot
    GuestProject = $guestProject
    SdkPackRoot = $pack
    SdkPropsSha256 = (Get-FileHash -LiteralPath (Join-Path $pack 'HybridCPU.Sdk.props') -Algorithm SHA256).Hash.ToLowerInvariant()
    SdkTargetsSha256 = (Get-FileHash -LiteralPath (Join-Path $pack 'HybridCPU.Sdk.targets') -Algorithm SHA256).Hash.ToLowerInvariant()
    SdkManifestSha256 = (Get-FileHash -LiteralPath (Join-Path $pack 'hybridcpu.runtime-pack.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    Diagnostic = [bool]$Diagnostic
    CompetingProcesses = @()
}
$preflight | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'preflight.json')

$publishArguments = @(
    'publish', $guestProject, '-c', 'Release', '-r', 'hybridcpu',
    '--disable-build-servers', '-m:1',
    '--artifacts-path', (Join-Path $output 'artifacts'),
    "-p:PublishDir=$(Join-Path $output 'publish')$([IO.Path]::DirectorySeparatorChar)",
    "-p:HybridCpuSdkPackRoot=$pack",
    '-p:UseSharedCompilation=false'
)
if ($Diagnostic) { $publishArguments += '-p:HybridCpuManagedRequirements=' }
$publishArguments | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'publish-arguments.json')

Push-Location $sourceRoot
try {
    & dotnet @publishArguments 1> (Join-Path $output 'publish.log') 2> (Join-Path $output 'publish.err')
    $publishExit = $LASTEXITCODE
}
finally { Pop-Location }
$publishExit | Set-Content -LiteralPath (Join-Path $output 'publish-exit.txt')
if ($publishExit -ne 0) {
    [pscustomobject]@{ CompletedUtc = [DateTime]::UtcNow.ToString('O'); PublishExit = $publishExit; Copied = $false } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'result.json')
    exit $publishExit
}

$images = @(Get-ChildItem -LiteralPath (Join-Path $output 'publish') -Filter '*.hcexe' -File -Recurse)
if ($images.Count -ne 1) { throw "Expected exactly one HCEXE beneath publish; found $($images.Count)." }

New-Item -ItemType Directory -Path $copyDestination | Out-Null
Get-ChildItem -LiteralPath (Join-Path $output 'publish') -Force |
    Copy-Item -Destination $copyDestination -Recurse -Force
$image = $images[0]
$relativeImage = [IO.Path]::GetRelativePath((Join-Path $output 'publish'), $image.FullName)
$copiedImage = Join-Path $copyDestination $relativeImage
$identity = [ordered]@{
    Schema = 'hybridcpu.doom-hcexe-copy/v1'
    CompletedUtc = [DateTime]::UtcNow.ToString('O')
    Diagnostic = [bool]$Diagnostic
    Qualification = if ($Diagnostic) { 'DiagnosticUnqualified' } else { 'PublishOutcomeRequiresSeparateQualification' }
    PublishExit = $publishExit
    SourcePublishRoot = Join-Path $output 'publish'
    CopyDestination = $copyDestination
    Image = $copiedImage
    ImageSha256 = (Get-FileHash -LiteralPath $copiedImage -Algorithm SHA256).Hash.ToLowerInvariant()
    ImageBytes = (Get-Item -LiteralPath $copiedImage).Length
    Files = @(Get-ChildItem -LiteralPath $copyDestination -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{
            RelativePath = [IO.Path]::GetRelativePath($copyDestination, $_.FullName)
            Bytes = $_.Length
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
}
$identity | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $output 'copy-manifest.json')
$identity | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $copyDestination 'copy-manifest.json')
Write-Output $copiedImage
exit 0
