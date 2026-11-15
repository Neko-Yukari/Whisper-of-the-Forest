#requires -Version 7.0
<#
.SYNOPSIS
    Runs the offline seam tests in Tests/WhisperOfTheForest.Tests against the built mod DLL.
    No game process: the tests load 1.7/Assemblies/WhisperOfTheForest.dll and the game's assemblies
    read-only, so build first (Tools/build.ps1).
.PARAMETER Filter
    Runs only the tests whose name contains this text, e.g. -Filter grammar.
#>
[CmdletBinding()]
param(
    [string]$Filter = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$moddevRoot = Split-Path -Parent $repo
$dotnet = Join-Path $moddevRoot 'tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "Repo-local .NET SDK not found: $dotnet (the same SDK Tools/build.ps1 uses)"
}

$env:DOTNET_CLI_HOME = Join-Path $moddevRoot 'tools/dotnet-home'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

# The tests reference the deployed DLL. While RimWorld holds it, build.ps1 compiles elsewhere and leaves
# this one stale, so refuse to report on a DLL older than the source it is supposed to contain.
$modDll = Get-Item -LiteralPath (Join-Path $repo '1.7/Assemblies/WhisperOfTheForest.dll')
$newestSource = Get-ChildItem -LiteralPath (Join-Path $repo 'Source/WhisperOfTheForest') -Recurse -File -Include *.cs, *.csproj |
    Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($newestSource.LastWriteTimeUtc -gt $modDll.LastWriteTimeUtc) {
    throw "1.7/Assemblies/WhisperOfTheForest.dll is older than $($newestSource.Name); run Tools/build.ps1 first (with RimWorld running the DLL cannot be updated: report the offline tests as not run)"
}

$project = Join-Path $repo 'Tests/WhisperOfTheForest.Tests/WhisperOfTheForest.Tests.csproj'
$runArgs = @('run', '--project', $project, '-c', 'Release', '--')
if ($Filter) { $runArgs += @('--filter', $Filter) }
& $dotnet @runArgs
exit $LASTEXITCODE
