[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Destination,
    [Parameter(Mandatory = $true)]
    [string] $IlCompilerDirectory,
    [Parameter(Mandatory = $true)]
    [string] $RuntimeReferenceDirectory,
    [string] $ArtifactsPath,
    [ValidateSet('Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$packSource = $PSScriptRoot
$adapterSource = [IO.Path]::GetFullPath((Join-Path $packSource '..'))
$destinationRoot = [IO.Path]::GetFullPath($Destination)
$ilCompilerSource = [IO.Path]::GetFullPath($IlCompilerDirectory)
$runtimeReferenceSource = [IO.Path]::GetFullPath($RuntimeReferenceDirectory)
if (!(Test-Path -LiteralPath (Join-Path $ilCompilerSource 'ilc.dll'))) { throw 'HCPUB1205: exact ILCompiler directory is invalid.' }
if (!(Test-Path -LiteralPath $runtimeReferenceSource)) { throw 'HCPUB1206: runtime reference directory is invalid.' }
$pinnedIlCompilerHashes = [ordered]@{
    'ilc.dll' = '016ee2333b776357abf1d4c8b19a7f2a2e05f5a22ad046d1266c2468888ccd3d'
    'ILCompiler.Compiler.dll' = '1ff1572b32567c9d93ef30b9fe1a9df2944596bb669efa6bd1f24835e62aadb0'
    'ILCompiler.RyuJit.dll' = '04ff4cd4d0719a6463b95a81fcd1b4ce482e64524c2e26096fa037e3cba7765e'
}
foreach ($entry in $pinnedIlCompilerHashes.GetEnumerator()) {
    $binary = Join-Path $ilCompilerSource $entry.Key
    if (!(Test-Path -LiteralPath $binary) -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath $binary).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw "HCPUB1208: ILCompiler binary skew in $($entry.Key); the exact Phase 23+26 build is required."
    }
}
if ([string]::IsNullOrWhiteSpace($destinationRoot) -or $destinationRoot -eq [IO.Path]::GetPathRoot($destinationRoot)) {
    throw 'HCPUB1201: destination must be an explicit non-root directory.'
}
if (Test-Path -LiteralPath $destinationRoot) {
    if ((Get-ChildItem -LiteralPath $destinationRoot -Force | Measure-Object).Count -ne 0) {
        throw 'HCPUB1202: destination already contains files; versioned packs are never overwritten in place.'
    }
} else {
    New-Item -ItemType Directory -Path $destinationRoot | Out-Null
}

$project = Join-Path $adapterSource 'HybridCPU.Compiler.NativeAot.Adapter.csproj'
$buildArguments = @('build', $project, '-c', $Configuration, '/p:UseSharedCompilation=false', '/m:1')
if (![string]::IsNullOrWhiteSpace($ArtifactsPath)) {
    $artifactsRoot = [IO.Path]::GetFullPath($ArtifactsPath)
    $buildArguments += @('--artifacts-path', $artifactsRoot)
} else {
    $buildArguments += '--no-restore'
}
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'HCPUB1203: compiler pack build failed.' }
$binaryRoot = if ([string]::IsNullOrWhiteSpace($ArtifactsPath)) {
    Join-Path $adapterSource "bin\$Configuration\net10.0"
} else {
    Join-Path $artifactsRoot "bin\HybridCPU.Compiler.NativeAot.Adapter\$($Configuration.ToLowerInvariant())"
}
$inputs = @(
    (Join-Path $packSource 'HybridCPU.Sdk.props'),
    (Join-Path $packSource 'HybridCPU.Sdk.targets'),
    (Join-Path $packSource 'runtime.json'),
    (Join-Path $packSource 'hybridcpu.runtime-pack.json'),
    (Join-Path $packSource 'PHASE26_SDK_PACK_V1.md'),
    (Join-Path $binaryRoot 'HybridCPU.Compiler.NativeAot.Adapter.dll'),
    (Join-Path $binaryRoot 'HybridCPU.Compiler.NativeAot.Adapter.deps.json'),
    (Join-Path $binaryRoot 'HybridCPU.Compiler.NativeAot.Adapter.runtimeconfig.json'),
    (Join-Path $binaryRoot 'HybridCPU.Compiler.Cil.dll'),
    (Join-Path $binaryRoot 'HybridCPU.Compiler.Core.dll'),
    (Join-Path $binaryRoot 'HybridCPU.Platform.Contracts.dll'),
    (Join-Path $adapterSource 'Patches\0001-hybridcpu-phase23-single-method-seam.patch'),
    (Join-Path $adapterSource 'Patches\0002-hybridcpu-phase26-publish-image.patch')
)
foreach ($input in $inputs) {
    if (!(Test-Path -LiteralPath $input)) { throw "HCPUB1204: required pack input is missing: $input" }
    Copy-Item -LiteralPath $input -Destination (Join-Path $destinationRoot ([IO.Path]::GetFileName($input)))
}
$installedAdapter = Join-Path $destinationRoot 'HybridCPU.Compiler.NativeAot.Adapter.dll'
$installedRuntimeManifest = Join-Path $destinationRoot 'hybridcpu.runtime-pack.json'
& dotnet $installedAdapter write-pack-manifest --out $installedRuntimeManifest
if ($LASTEXITCODE -ne 0) { throw 'HCPUB1207: current compiler could not generate the installed runtime-pack manifest.' }
$installedIlCompiler = Join-Path $destinationRoot 'ilcompiler'
$installedReferences = Join-Path $destinationRoot 'references'
New-Item -ItemType Directory -Path $installedIlCompiler, $installedReferences | Out-Null
Get-ChildItem -LiteralPath $ilCompilerSource -File | Sort-Object Name | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $installedIlCompiler $_.Name)
}
Get-ChildItem -LiteralPath $runtimeReferenceSource -Filter '*.dll' -File | Sort-Object Name | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $installedReferences $_.Name)
}
$files = foreach ($file in Get-ChildItem -LiteralPath $destinationRoot -File | Sort-Object Name) {
    [ordered]@{
        name = $file.Name
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
        bytes = $file.Length
    }
}
$nestedFiles = foreach ($file in Get-ChildItem -LiteralPath $installedIlCompiler, $installedReferences -File -Recurse | Sort-Object FullName) {
    [ordered]@{
        name = [IO.Path]::GetRelativePath($destinationRoot, $file.FullName).Replace('\', '/')
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
        bytes = $file.Length
    }
}
$runtimePack = Get-Content -LiteralPath (Join-Path $destination 'hybridcpu.runtime-pack.json') -Raw | ConvertFrom-Json
[ordered]@{
    schema = 'hybridcpu.installed-pack-files/v1'
    packVersion = $runtimePack.PackVersion
    targetRid = 'hybridcpu'
    owners = [ordered]@{
        compiler = @('HybridCPU.Compiler.NativeAot.Adapter.dll', 'HybridCPU.Compiler.Cil.dll', 'HybridCPU.Compiler.Core.dll', 'ilcompiler/*')
        runtime = @('hybridcpu.runtime-pack.json', 'references/*')
        sdk = @('HybridCPU.Sdk.props', 'HybridCPU.Sdk.targets', 'runtime.json')
        targetEnablement = @('0001-hybridcpu-phase23-single-method-seam.patch', '0002-hybridcpu-phase26-publish-image.patch')
    }
    files = @($files) + @($nestedFiles)
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destinationRoot 'hybridcpu.pack-files.json') -Encoding utf8NoBOM

Write-Output $destinationRoot
