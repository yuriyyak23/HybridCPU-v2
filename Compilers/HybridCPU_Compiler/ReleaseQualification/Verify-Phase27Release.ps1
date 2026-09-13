[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $CompilerCommit,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string] $Phase26EvidenceSha256
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'HybridCPU.Compiler.Release.csproj'
& dotnet build $project -c Release --no-restore /p:UseSharedCompilation=false /m:1
if ($LASTEXITCODE -ne 0) { throw 'HCRL1201: release qualification tool build failed.' }
$tool = Join-Path $PSScriptRoot 'bin\Release\net10.0\HybridCPU.Compiler.Release.dll'
$verificationRoot = Join-Path ([IO.Path]::GetTempPath()) "hybridcpu-phase27-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $verificationRoot | Out-Null
try {
    $outputs = @()
    foreach ($run in 1..2) {
        $manifest = Join-Path $verificationRoot "run-$run\release-manifest.json"
        & dotnet $tool --commit $CompilerCommit --phase26-evidence $Phase26EvidenceSha256 --out $manifest | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "HCRL1202: release qualification run $run failed." }
        $telemetry = Get-Content -Raw -LiteralPath ($manifest + '.telemetry.json') | ConvertFrom-Json
        if ($telemetry.SelectsProductionOutput) { throw 'HCRL1203: telemetry was marked as production-selecting.' }
        $outputs += [ordered]@{
            manifest = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifest).Hash.ToLowerInvariant()
            features = (Get-FileHash -Algorithm SHA256 -LiteralPath ($manifest + '.features.json')).Hash.ToLowerInvariant()
            compileMilliseconds = [double]$telemetry.CompileMilliseconds
            peakWorkingSetBytes = [long]$telemetry.PeakWorkingSetBytes
        }
    }
    if ($outputs[0].manifest -ne $outputs[1].manifest -or $outputs[0].features -ne $outputs[1].features) {
        throw 'HCRL1204: deterministic release artifacts differ.'
    }
    [ordered]@{
        schema = 'hybridcpu.phase27-release-verification/v1'
        compilerCommit = $CompilerCommit
        phase26EvidenceSha256 = $Phase26EvidenceSha256
        deterministicRuns = 2
        releaseManifestSha256 = $outputs[0].manifest
        featureMatrixSha256 = $outputs[0].features
        compileMillisecondsTelemetry = @($outputs[0].compileMilliseconds, $outputs[1].compileMilliseconds)
        peakWorkingSetBytesTelemetry = @($outputs[0].peakWorkingSetBytes, $outputs[1].peakWorkingSetBytes)
        telemetrySelectsProductionOutput = $false
    } | ConvertTo-Json
}
finally {
    if (Test-Path -LiteralPath $verificationRoot) {
        $resolved = [IO.Path]::GetFullPath($verificationRoot)
        $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (!$resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFileName($resolved) -notlike 'hybridcpu-phase27-*') {
            throw "HCRL1205: refusing unsafe cleanup target $resolved."
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
