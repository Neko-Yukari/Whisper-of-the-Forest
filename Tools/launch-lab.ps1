#requires -Version 7.0
<#
  Isolated 1.7 experiment launch.

  Verified against RimWorld 1.7.4985 / Assembly-CSharp:
    - GenFilePaths.SaveDataFolderPath honours "-savedatafolder=<dir>"
    - GenFilePaths.ConfigFolderPath = <savedatafolder>/Config, so ModsConfig is isolated too
    - ModsConfig.ApplyCommandLineModList replaces active mods with ludeon.rimworld + official
      DLC + the semicolon-separated "-mod=" list, without writing ModsConfig.xml

  The repo itself stays inside Moddev. Deployment (junction/copy into RimWorld/Mods) is owned by
  the orchestrator; this script only validates the deployed path and starts the game.
#>
[CmdletBinding()]
param(
    [string]$GameRoot = 'E:/SteamLibrary/steamapps/common/RimWorld',
    [string]$DeployedModPath,
    [string]$LabDir,
    [string]$SaveDataFolder,
    [string]$Mods,
    [switch]$QuickTest,
    [switch]$Wait
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$gameExe = Join-Path $GameRoot 'RimWorld.exe'

if (-not $DeployedModPath) {
    $DeployedModPath = Join-Path $GameRoot 'Mods/Whisper-of-the-Forest'
}
if (-not $LabDir) {
    $LabDir = Join-Path $repo 'LabRuns/lab'
}
if (-not $SaveDataFolder) {
    $SaveDataFolder = Join-Path $LabDir 'savedata'
}
if (-not $Mods) {
    $Mods = 'NekoYukari.WhisperOfTheForest;brrainz.harmony;erdelf.HumanoidAlienRaces;Chezhou.RimUnity'
}

if (-not (Test-Path -LiteralPath $gameExe)) {
    throw "RimWorld.exe not found: $gameExe"
}

$about = Join-Path $DeployedModPath 'About/About.xml'
if (-not (Test-Path -LiteralPath $about)) {
    throw @"
Deployed mod not found at $DeployedModPath.
Create the junction first (orchestrator-owned), for example:
  New-Item -ItemType Junction -Path '$DeployedModPath' -Target '$repo'
"@
}

[xml]$aboutXml = Get-Content -LiteralPath $about -Raw
$packageId = [string]$aboutXml.ModMetaData.packageId
if ($packageId -ne 'NekoYukari.WhisperOfTheForest') {
    throw "Unexpected packageId '$packageId' in $about"
}

$dll = Join-Path $DeployedModPath '1.7/Assemblies/WhisperOfTheForest.dll'
if (-not (Test-Path -LiteralPath $dll)) {
    throw "Missing built assembly: $dll (run Tools/build.ps1 first)"
}

foreach ($dir in @($LabDir, $SaveDataFolder)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

if (Get-Process -Name 'RimWorld' -ErrorAction SilentlyContinue) {
    Write-Warning 'A RimWorld process is already running. Close it first, or the lab folder may be shared by two sessions.'
}

$arguments = @(
    "-savedatafolder=$SaveDataFolder",
    "-mod=$Mods",
    "-wotflab=$LabDir"
)
if ($QuickTest) {
    $arguments += '-quicktest'
}

Write-Host "Game: $gameExe"
Write-Host "Deployed mod: $DeployedModPath"
Write-Host "Lab: $LabDir"
Write-Host "Save data: $SaveDataFolder"
Write-Host "Mods: $Mods"
Write-Host "QuickTest: $QuickTest"
Write-Host ''

function Quote-Argument([string]$value) {
    if ($value -match '[\s"]') {
        return '"' + ($value -replace '"', '\"') + '"'
    }
    return $value
}

$commandLine = ('"' + $gameExe + '"') + ' ' + (($arguments | ForEach-Object { Quote-Argument $_ }) -join ' ')
Write-Host "CommandLine: $commandLine"

# Start-Process from a terminal job can die with the job's process tree on this host.
# Win32_Process.Create makes RimWorld a detached child of the WMI provider instead.
$result = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
    CommandLine      = $commandLine
    CurrentDirectory = $GameRoot
}
if ($null -eq $result -or $result.ReturnValue -ne 0) {
    throw "Win32_Process.Create failed with ReturnValue=$($result.ReturnValue). RimWorld was not started."
}

$processId = [int]$result.ProcessId
Write-Host "Started RimWorld PID=$processId (detached via Win32_Process.Create)"
Write-Host "Wait for lab-status.json: $(Join-Path $LabDir 'lab-status.json')"
Write-Host "Send commands: pwsh -File Tools/lab.ps1 -LabDir '$LabDir' -Command 'status'"

if ($Wait) {
    Wait-Process -Id $processId
}
