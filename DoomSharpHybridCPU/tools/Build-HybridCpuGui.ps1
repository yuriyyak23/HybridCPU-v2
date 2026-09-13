[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputRoot)
$ErrorActionPreference='Stop'
$doomRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repo=[IO.Path]::GetFullPath((Join-Path $doomRoot '..'))
$allowed=[IO.Path]::GetFullPath((Join-Path $repo 'TempEnv'))+'\'
$output=[IO.Path]::GetFullPath($OutputRoot)
if(!$output.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'OutputRoot must be below HybridCPU ISE\TempEnv.' }
if(Test-Path -LiteralPath $output) { throw 'Choose a new OutputRoot; existing artifacts are preserved.' }
New-Item -ItemType Directory -Path $output,(Join-Path $output 'tmp'),(Join-Path $output 'source') | Out-Null
$oldTemp=$env:TEMP; $oldTmp=$env:TMP; $oldReuse=$env:MSBUILDDISABLENODEREUSE
try {
 $env:TEMP=Join-Path $output 'tmp'; $env:TMP=$env:TEMP; $env:MSBUILDDISABLENODEREUSE='1'
 $source=Join-Path $doomRoot 'src\DoomSharp.HybridCpu.Windows'
 # Stage WPF input: SDK may create a transient .csproj beside its source project.
 foreach($file in Get-ChildItem -LiteralPath $source -File) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $output 'source') }
 # Relative project references belong to the original project, not the staged WPF directory.
 $stagedProject=Join-Path $output 'source\DoomSharp.HybridCpu.Windows.csproj'
 [xml]$projectXml=Get-Content -LiteralPath $stagedProject -Raw
 foreach($reference in $projectXml.SelectNodes('//ProjectReference')) {
  $include=$reference.GetAttribute('Include')
  if(!$include.Contains('$(') -and ![IO.Path]::IsPathRooted($include)) {
   $resolved=[IO.Path]::GetFullPath((Join-Path $source $include))
   if(!(Test-Path -LiteralPath $resolved -PathType Leaf)) { throw "Missing source project reference: $resolved" }
   $reference.SetAttribute('Include',$resolved)
  }
 }
 $projectXml.Save($stagedProject)
 Push-Location $doomRoot
 try {
  & dotnet build (Join-Path $output 'source\DoomSharp.HybridCpu.Windows.csproj') -c Release --artifacts-path (Join-Path $output 'artifacts') "-p:HybridCpuRepositoryRoot=$repo\" -p:DefineTestSupport=false -p:EnableInternalTestHooks=false -p:UseSharedCompilation=false /m:1 /nr:false *> (Join-Path $output 'build.log')
  $code=$LASTEXITCODE
  Set-Content -LiteralPath (Join-Path $output 'build.exit.txt') -Value $code
  Get-Content -LiteralPath (Join-Path $output 'build.log') -Tail 8
  if($code -ne 0) { throw "GUI build failed: $code; see build.log" }
 } finally { Pop-Location }
 Write-Output (Join-Path $output 'artifacts\bin\DoomSharp.HybridCpu.Windows\release\DoomSharp.HybridCpu.Windows.exe')
} finally { $env:TEMP=$oldTemp; $env:TMP=$oldTmp; $env:MSBUILDDISABLENODEREUSE=$oldReuse }
