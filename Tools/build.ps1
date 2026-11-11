#requires -Version 7.0
<#
  Builds the single 1.7 experiment assembly with the repo-local .NET 10 SDK.
  Never uses the machine-wide SDK and never installs or registers anything.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean,
    [switch]$Verify,
    # Compile the test host (Source/Lab, Source/LabTests) into the assembly. Release builds leave it out.
    [switch]$Lab
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$moddevRoot = Split-Path -Parent $repo
$dotnet = Join-Path $moddevRoot 'tools/dotnet/dotnet.exe'
$project = Join-Path $repo 'Source/WhisperOfTheForest/WhisperOfTheForest.csproj'
$outputDir = Join-Path $repo '1.7/Assemblies'
$outputDll = Join-Path $outputDir 'WhisperOfTheForest.dll'

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "Repo-local .NET SDK not found: $dotnet. Expected the portable SDK 10.0.402 under Moddev/tools/dotnet."
}

$running = Get-Process -Name 'RimWorld' -ErrorAction SilentlyContinue
if ($running) {
    throw ("RimWorld is running (PID " + (($running | ForEach-Object Id) -join ',') + "). Close it before building so the final DLL is not locked or half-replaced.")
}

$env:DOTNET_CLI_HOME = Join-Path $moddevRoot 'tools/dotnet-home'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME | Out-Null

if ($Clean) {
    foreach ($path in @(
        (Join-Path $repo 'Source/WhisperOfTheForest/obj'),
        (Join-Path $repo 'Source/WhisperOfTheForest/bin'),
        (Join-Path $outputDir 'WhisperOfTheForest.dll'),
        (Join-Path $outputDir 'WhisperOfTheForest.deps.json'),
        (Join-Path $outputDir 'WhisperOfTheForest.pdb')
    )) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

Write-Host "SDK: $(& $dotnet --version)"
Write-Host "Project: $project"
Write-Host "Configuration: $Configuration"
Write-Host "Lab sources: $(if ($Lab) { 'compiled in' } else { 'excluded' })"

& $dotnet build $project -c $Configuration -v minimal --nologo /p:GenerateFullPaths=false $(if ($Lab) { '/p:WotfLab=true' })
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $outputDll)) {
    throw "Build reported success but $outputDll is missing"
}

$file = Get-Item -LiteralPath $outputDll
Write-Host ''
Write-Host 'BUILD OK'
Write-Host "dll=$($file.FullName)"
Write-Host "size=$($file.Length)"

$copied = Get-ChildItem -LiteralPath $outputDir -File | Where-Object {
    $_.Name -ne 'WhisperOfTheForest.dll'
}
if ($copied) {
    Write-Warning ("Unexpected files in 1.7/Assemblies: " + (($copied | ForEach-Object Name) -join ', '))
}

if ($Verify) {
    # Assembly-level check: every Class/thingClass/workerClass and texPath in 1.7/Defs must
    # resolve. Source-text greps were removed; behaviour is covered by Tools/test.ps1 in a real
    # game instance.
    & (Join-Path $PSScriptRoot 'check-defs.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw "check-defs.ps1 reported missing types or textures"
    }
}
