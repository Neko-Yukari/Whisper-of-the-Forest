#requires -Version 7.0
<#
  Builds the single 1.7 assembly with the repo-local .NET 10 SDK.
  Never uses the machine-wide SDK and never installs or registers anything.

  While RimWorld is running it holds 1.7/Assemblies/WhisperOfTheForest.dll, so the build goes to a
  temporary folder instead: the compile result is real, the deployed DLL is left alone.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean,
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$moddevRoot = Split-Path -Parent $repo
$dotnet = Join-Path $moddevRoot 'tools/dotnet/dotnet.exe'
$project = Join-Path $repo 'Source/WhisperOfTheForest/WhisperOfTheForest.csproj'
$deployDir = Join-Path $repo '1.7/Assemblies'

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "Repo-local .NET SDK not found: $dotnet. Expected the portable SDK 10.0.402 under Moddev/tools/dotnet."
}

$gameRunning = [bool](Get-Process -Name 'RimWorld' -ErrorAction SilentlyContinue)
# One temporary folder per checkout: worktrees that build at the same time must not overwrite each other's DLL.
$repoHash = [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($repo.ToLowerInvariant()))).Substring(0, 8)
$outputDir = if ($gameRunning) { Join-Path ([System.IO.Path]::GetTempPath()) "wotf-build-check-$repoHash" } else { $deployDir }
$outputDll = Join-Path $outputDir 'WhisperOfTheForest.dll'

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

$buildArguments = @('build', $project, '-c', $Configuration, '-v', 'minimal', '--nologo', '/p:GenerateFullPaths=false')
if ($gameRunning) {
    # Trailing slash: MSBuild appends file names to these properties as-is.
    $buildArguments += "/p:OutputPath=$outputDir/", "/p:OutDir=$outputDir/"
}

& $dotnet @buildArguments
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

if ($gameRunning) {
    # The ASCII half stays readable when a caller captures this output under another code page.
    Write-Host 'RimWorld 正在运行，仅验证编译通过，1.7/Assemblies DLL 未更新 (RimWorld is running: compile check only, deployed DLL not updated)'
}

$copied = Get-ChildItem -LiteralPath $outputDir -File | Where-Object {
    $_.Name -ne 'WhisperOfTheForest.dll'
}
if ($copied) {
    Write-Warning ("Unexpected files in $($outputDir): " + (($copied | ForEach-Object Name) -join ', '))
}

if ($Verify) {
    # Checks the Defs against the assembly this run just built, which is the temporary one while
    # RimWorld is running.
    & (Join-Path $PSScriptRoot 'check-defs.ps1') -ModAssembly $outputDll
    if ($LASTEXITCODE -ne 0) {
        throw "check-defs.ps1 reported missing types or textures"
    }
}
