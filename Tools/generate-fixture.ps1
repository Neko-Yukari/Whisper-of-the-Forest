#requires -Version 7.0
# Rebuild from a fresh vanilla quicktest, never by loading/resaving the old fixture.
[CmdletBinding()]
param([int]$TimeoutSec = 300)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$moddev = Split-Path -Parent $repo
$lab = Join-Path $moddev 'lab-run'
$profile = Join-Path $moddev 'lab-profile'
$fixture = Join-Path $profile 'Saves/a25-fixture.rws'
if (Get-Process -Name RimWorld -ErrorAction SilentlyContinue) {
    throw 'Close RimWorld before generating the fixture; this command needs a fresh instance.'
}
& (Join-Path $PSScriptRoot 'build.ps1') -Lab
& (Join-Path $PSScriptRoot 'check-defs.ps1')
if ($LASTEXITCODE -ne 0) { throw 'check-defs failed' }
Remove-Item -LiteralPath (Join-Path $lab 'requests.txt'), (Join-Path $lab 'results.jsonl') -Force -ErrorAction SilentlyContinue
$started = Get-Date
try {
    & (Join-Path $PSScriptRoot 'launch-lab.ps1') -LabDir $lab -SaveDataFolder $profile -QuickTest
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $ready = $false
    while ((Get-Date) -lt $deadline) {
        $status = Join-Path $lab 'lab-status.json'
        if ((Test-Path $status) -and (Get-Item $status).LastWriteTime -ge $started) {
            $raw = & (Join-Path $PSScriptRoot 'lab.ps1') -LabDir $lab -Command 'status' -TimeoutSec 30
            if ($LASTEXITCODE -ne 0) { throw 'Lab status request failed' }
            $answer = $raw | ConvertFrom-Json
            if ($answer.ok -and $answer.mapReady) { $ready = $true; break }
        }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw 'Fresh quicktest map did not become ready' }
    foreach ($command in @('time speed=0', 'save name=a25-fixture')) {
        $raw = & (Join-Path $PSScriptRoot 'lab.ps1') -LabDir $lab -Command $command -TimeoutSec 60
        if ($LASTEXITCODE -ne 0 -or -not ($raw | ConvertFrom-Json).ok) { throw "Lab command failed: $raw" }
        Write-Host $raw
    }
    [xml]$save = Get-Content -LiteralPath $fixture -Raw
    if ($save.OuterXml.Contains('WhisperOfTheForest.Experimental.')) { throw 'Fixture contains a removed namespace' }
    if ($save.SelectNodes('//unlockedGlyphs/li').Count -ne 0) { throw 'Fixture must start with no unlocked runes' }
    $file = Get-Item $fixture
    if ($file.LastWriteTime -lt $started) { throw 'Fixture was not regenerated' }
    Write-Host "FIXTURE OK path=$fixture size=$($file.Length) time=$($file.LastWriteTime.ToString('o')) oldClasses=0 unlockedRunes=0"
}
finally {
    Get-CimInstance Win32_Process -Filter "Name='RimWorld.exe'" |
        Where-Object { $_.CommandLine.Contains("-wotflab=$lab") } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
}
