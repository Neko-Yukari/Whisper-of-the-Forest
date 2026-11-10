#requires -Version 7.0
<#
  One command for the mod's L1 and L2 acceptance:

    1. build when the assembly is older than any source/def file or is a product build without the Lab
       host (L0), unless -NoBuild; then run check-defs before the game starts
    2. start the isolated lab instance, or reuse the one this lab folder already has running
    3. run every LabTest inside that instance (optionally repeated) and load the fixture once
    4. print per-test results, the L1 load checks, the L1 disk-log scan and a nonzero exit code when
       anything failed

  The disk scan is what catches load-time Def errors: the in-game queue keeps only the last 1000
  lines, so errors logged while the mods load are gone by the time a test runs. The whole Player.log
  of the tested process is scanned for ERROR blocks that name this mod; a missing/empty/foreign log
  counts as unverified, never as zero errors. Pass -InspectLog <path> to scan one log without
  starting the game.

  The game stays open afterwards: the next call reuses it, so a filter run costs seconds instead of
  a cold start. Pass -StopAfter to close it, -Restart to replace it, -Reload to re-read the fixture
  save (tests otherwise build and clean their own scenes inside the instance that is already up).

  The run itself is synchronous inside the game: tests advance game time with single ticks. The
  report is written by the game to <lab>/test-runs/<run>.json (and latest.json) and is the
  authoritative record; with -Repeat N it also carries a per-test stability table.

  Examples:
    pwsh -File Tools/test.ps1                          # full suite in the running instance
    pwsh -File Tools/test.ps1 -Filter railgun
    pwsh -File Tools/test.ps1 -Filter "railgun." -Repeat 8
    pwsh -File Tools/test.ps1 -Restart -Reload -Repeat 3
    pwsh -File Tools/test.ps1 -StopAfter
#>
[CmdletBinding()]
param(
    [string]$Filter = '',
    [string]$Fixture = 'a25-fixture',
    [int]$Repeat = 1,
    [switch]$NoBuild,
    [switch]$Restart,
    [switch]$Reload,
    [switch]$StopAfter,
    [int]$TimeoutSec = 900,
    # How long a cold start may take before a map exists. Tests may not run earlier.
    [int]$MapWaitSec = 240,
    [string]$GameRoot = 'E:/SteamLibrary/steamapps/common/RimWorld',
    [string]$LabDir,
    [string]$SaveDataFolder,
    # Read-only: scan one Player.log and print this mod's ERROR blocks, then exit.
    [string]$InspectLog = '',
    # Override the log the main run checks (default: the game's Player.log).
    [string]$PlayerLog = ''
)

$ErrorActionPreference = 'Stop'

# The game's own text (translated refusal reasons) is UTF-8: print it as UTF-8 on this console.
try {
    [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
}
catch {
    # A redirected console may refuse the change; the report file is always UTF-8.
}

$repo = Split-Path -Parent $PSScriptRoot
$moddev = Split-Path -Parent $repo
$dotnet = Join-Path $moddev 'tools/dotnet/dotnet.exe'
$project = Join-Path $repo 'Source/WhisperOfTheForest/WhisperOfTheForest.csproj'
$dll = Join-Path $repo '1.7/Assemblies/WhisperOfTheForest.dll'
$deployed = Join-Path $GameRoot 'Mods/Whisper-of-the-Forest'

if (-not $LabDir) { $LabDir = Join-Path $moddev 'lab-run' }
if (-not $SaveDataFolder) { $SaveDataFolder = Join-Path $moddev 'lab-profile' }

$resultDir = Join-Path $LabDir 'test-runs'
$latestReport = Join-Path $resultDir 'latest.json'

function Write-Step([string]$text) {
    Write-Host ''
    Write-Host "== $text" -ForegroundColor Cyan
}

function Get-LabProcesses {
    Get-CimInstance Win32_Process -Filter "Name='RimWorld.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains("-wotflab=$LabDir") }
}

function Get-LabPid {
    (Get-LabProcesses | Select-Object -First 1).ProcessId
}

function Get-ForeignGameProcesses {
    Get-CimInstance Win32_Process -Filter "Name='RimWorld.exe'" -ErrorAction SilentlyContinue |
        Where-Object { -not ($_.CommandLine -and $_.CommandLine.Contains("-wotflab=$LabDir")) }
}

function Stop-LabInstance([string]$why) {
    $lab = Get-LabProcesses
    if (-not $lab) {
        return
    }

    Write-Host ("Stopping lab instance PID {0} ({1})" -f (($lab | ForEach-Object ProcessId) -join ','), $why)
    $lab | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 2
}

function Get-PlayerLogPaths {
    $logDir = Join-Path $env:USERPROFILE 'AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Logs'
    [pscustomobject]@{
        Current = Join-Path $logDir 'Player.log'
        Previous = Join-Path $logDir 'Player-prev.log'
    }
}

<#
  A declared expected error reaches this script as one sample from the run report: the queue's copy
  of the entry, whose first line is the message and whose remaining lines are joined with " | " and
  cut at 1200 characters. A block is exempt only when that first line equals the block head exactly;
  a prefix test would let one sample excuse a different message that merely starts the same way.
#>
function Test-ExpectedLogBlock([string]$head, [string[]]$samples) {
    foreach ($sample in $samples) {
        if (-not $sample) { continue }
        $first = ($sample -split ' \| ')[0].Trim()
        if ($first -and $first -eq $head) { return $true }
    }

    return $false
}

<#
  The in-game queue only keeps the last 1000 lines, so a Def error logged while the mods load can
  be pushed out before the run starts. The disk log keeps the whole process, so it is the authority
  for load-time errors.

  Reads the log line by line and treats a timestamped "ERROR" line plus its following stack-trace
  lines as one block; the block counts as ours when any of its lines names this mod (an exception
  thrown inside our assembly has its name only in the stack).
#>
function Read-WotfLogErrors([string]$path, [int]$SkipLines = 0, [string[]]$ExpectedSamples = @()) {
    $result = [pscustomobject]@{
        Path = $path
        Status = 'missing'
        ProcessId = $null
        Started = $null
        TotalLines = 0
        ScannedLines = 0
        ErrorBlocks = 0
        Exempted = 0
        Messages = @()
    }

    if (-not $path -or -not (Test-Path -LiteralPath $path)) {
        return $result
    }

    $item = Get-Item -LiteralPath $path
    if ($item.Length -eq 0) {
        $result.Status = 'empty'
        return $result
    }

    $lines = Get-Content -LiteralPath $path -Encoding utf8
    $result.TotalLines = $lines.Count
    # SkipLines lets a repeat call on the same process read only what was appended since the last one.
    $scanFrom = [Math]::Max(0, [Math]::Min($SkipLines, $lines.Count))
    $result.ScannedLines = $lines.Count - $scanFrom

    $header = $lines | Select-Object -First 20
    $processLine = $header | Where-Object { $_ -match '^Process:\s+pid\s+(\d+)' } | Select-Object -First 1
    if ($processLine -and $processLine -match '^Process:\s+pid\s+(\d+)') {
        $result.ProcessId = [int]$Matches[1]
    }

    $startedLine = $header | Where-Object { $_ -match '^Started:\s+(.+)$' } | Select-Object -First 1
    if ($startedLine -and $startedLine -match '^Started:\s+(.+)$') {
        $result.Started = $Matches[1].Trim()
    }

    if (-not $processLine) {
        # No header at all: not a game log this script can attribute to an instance.
        $result.Status = 'unreadable'
        return $result
    }

    $result.Status = 'ok'
    $messages = New-Object System.Collections.Generic.List[string]
    $index = $scanFrom
    while ($index -lt $lines.Count) {
        $line = $lines[$index]
        if ($line -notmatch '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+\s+ERROR\b') {
            $index++
            continue
        }

        # The whole block until the next timestamped line.
        $block = New-Object System.Collections.Generic.List[string]
        $block.Add($line)
        $index++
        while ($index -lt $lines.Count -and $lines[$index] -notmatch '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+') {
            if ($lines[$index].Trim()) { $block.Add($lines[$index]) }
            $index++
        }

        # The project does not support old saves. A pre-rename save still names the old
        # WhisperOfTheForest.Experimental.* classes; those load errors (and the abstract-class
        # failures they cause) are expected and are not this run's defects.
        if (($block -match 'WhisperOfTheForest\.Experimental\.') -or
            ($block -match "Can't load abstract class Verse\.(GameComponent|MapComponent)")) {
            continue
        }

        $result.ErrorBlocks++
        $head = ($block[0] -replace '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+\s+', '') -replace '^ERROR\s+(\[[^\]]*\]\s+)?', ''
        $head = $head.Trim()
        if (Test-ExpectedLogBlock $head $ExpectedSamples) {
            $result.Exempted++
            continue
        }

        $ours = $block | Where-Object { $_ -match 'WotF|WhisperOfTheForest' }
        if (-not $ours) {
            continue
        }

        $frame = $ours | Where-Object { $_ -match 'at .*(WotF|WhisperOfTheForest)' } | Select-Object -First 1
        $entry = if ($frame -and $frame -ne $block[0]) { $head + ' << ' + $frame.Trim() } else { $head }
        if (-not $messages.Contains($entry)) {
            $messages.Add($entry)
        }
    }

    $result.Messages = $messages.ToArray()
    return $result
}

<# A log message without its timestamp/severity scaffolding, so a disk line and a queue sample of the
   same event compare equal and are reported once. #>
function Get-LogMessageKey([string]$text) {
    if (-not $text) { return '' }
    $first = ($text -split ' \| ')[0]
    $first = ($first -split ' << ')[0]
    $stripped = $first -replace '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+\s+', ''
    $stripped = $stripped -replace '^(ERROR|WARN|WARNING|MESSAGE)\s+(\[[^\]]*\]\s+)?', ''
    $stripped = $stripped.Trim()
    if ($stripped.Length -gt 90) { $stripped = $stripped.Substring(0, 90) }
    return $stripped
}

function Get-GameLogTail([int]$lines = 30) {
    $logDir = Join-Path $env:USERPROFILE 'AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Logs'
    $text = ''
    foreach ($name in @('Player.log', 'Player-prev.log')) {
        $path = Join-Path $logDir $name
        if (-not (Test-Path -LiteralPath $path)) { continue }
        $item = Get-Item -LiteralPath $path
        if ($item.Length -eq 0) { continue }
        $text += "`n--- $path`n"
        $text += ((Get-Content -LiteralPath $path -Tail $lines) -join "`n")
    }

    return $text
}

function Show-GameLogHints {
    $tail = Get-GameLogTail 40
    if (-not $tail) {
        Write-Host 'No game log on disk (the game writes Player.log only after a clean start).'
        return
    }

    foreach ($pattern in @('ReflectionTypeLoadException', 'is not in the local Mods folder', 'Could not load', 'Exception')) {
        $hits = ($tail -split "`n") | Where-Object { $_ -like "*$pattern*" } | Select-Object -First 3
        foreach ($hit in $hits) {
            Write-Host "  log: $hit" -ForegroundColor DarkYellow
        }
    }

    Write-Host 'Log tail:'
    Write-Host $tail
}

function Start-LabInstance {
    $foreign = Get-ForeignGameProcesses
    if ($foreign) {
        throw ("RimWorld is running outside this lab (PID " + (($foreign | ForEach-Object ProcessId) -join ',') +
            '). Close it first: the lab needs the game to itself for build and save access.')
    }

    New-Item -ItemType Directory -Force -Path $LabDir, $SaveDataFolder | Out-Null

    # A fresh instance starts with empty request/result files: old lines belong to earlier sessions
    # and would only make a stuck run harder to read.
    Remove-Item -LiteralPath (Join-Path $LabDir 'requests.txt'), (Join-Path $LabDir 'results.jsonl') -Force -ErrorAction SilentlyContinue

    try {
        & (Join-Path $PSScriptRoot 'launch-lab.ps1') -GameRoot $GameRoot -DeployedModPath $deployed `
            -LabDir $LabDir -SaveDataFolder $SaveDataFolder -QuickTest
    }
    catch {
        throw "launch-lab.ps1 failed: $($_.Exception.Message)"
    }

    # launch-lab.ps1 reports the PID on its own output; the process must exist for the wait below.
    $deadline = (Get-Date).AddSeconds(30)
    while (-not (Get-LabPid) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 300
    }

    if (-not (Get-LabPid)) {
        throw 'The lab instance did not start; see the launch-lab.ps1 output above.'
    }
}

<#
  Waits until the mod's own assembly wrote lab-status.json for this process (proof that the assembly
  and its dependencies loaded) and the lab answered one status request. Returns the status JSON text
  or $null when the instance never reached a map.
#>
function Wait-LabReady {
    $statusFile = Join-Path $LabDir 'lab-status.json'
    $pidForStart = Get-LabPid
    if (-not $pidForStart) {
        return $null
    }

    $processStart = (Get-Process -Id $pidForStart).StartTime.ToUniversalTime()

    $initDeadline = (Get-Date).AddSeconds(120)
    while ((-not (Test-Path -LiteralPath $statusFile)) -or
           ((Get-Item -LiteralPath $statusFile).LastWriteTimeUtc -lt $processStart)) {
        if (-not (Get-LabPid)) {
            Write-Host 'The lab instance exited while starting; the assembly probably failed to load.' -ForegroundColor Red
            return $null
        }

        if ((Get-Date) -gt $initDeadline) {
            Write-Host "The mod never wrote $statusFile for this process: the assembly did not load." -ForegroundColor Red
            return $null
        }

        Start-Sleep -Milliseconds 500
    }

    # The lab answers as soon as the assembly is up, which on a cold start is before the map exists.
    # gameLoaded only means a Game object exists (quick test creates it before generating the map);
    # mapReady matches the test runner's own precondition. Waiting for less makes every test fail
    # with "no game/map is loaded".
    $readyDeadline = (Get-Date).AddSeconds($MapWaitSec)
    $answeredOnce = $false
    while ((Get-Date) -lt $readyDeadline) {
        if (-not (Get-LabPid)) {
            return $null
        }

        try {
            $answer = & (Join-Path $PSScriptRoot 'lab.ps1') -LabDir $LabDir -Command 'status' -TimeoutSec 25
            if ($LASTEXITCODE -eq 0 -and $answer -match '"ok":true') {
                $answeredOnce = $true
                if ($answer -match '"mapReady":true') {
                    return $answer
                }
            }
        }
        catch {
            # No answer in this attempt; keep waiting until the deadline.
        }

        Start-Sleep -Seconds 3
    }

    if ($answeredOnce) {
        Write-Host "The lab answered but stayed at mapReady=false for $MapWaitSec s: no map was loaded, so no test can run." -ForegroundColor Red
    }

    return $null
}

function Test-BuildStale {
    if (-not (Test-Path -LiteralPath $dll)) {
        return $true
    }

    $built = (Get-Item -LiteralPath $dll).LastWriteTimeUtc
    $inputs = @()
    $inputs += Get-ChildItem -LiteralPath (Join-Path $repo 'Source/WhisperOfTheForest') -Recurse -File -Include '*.cs', '*.csproj' |
        Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
    foreach ($labDir in @('Source/Lab', 'Source/LabTests')) {
        if (Test-Path -LiteralPath (Join-Path $repo $labDir)) {
            $inputs += Get-ChildItem -LiteralPath (Join-Path $repo $labDir) -Recurse -File -Include '*.cs'
        }
    }
    if (Test-Path -LiteralPath (Join-Path $repo '1.7/Defs')) {
        $inputs += Get-ChildItem -LiteralPath (Join-Path $repo '1.7/Defs') -Recurse -File -Filter '*.xml'
    }
    $about = Join-Path $repo 'About/About.xml'
    if (Test-Path -LiteralPath $about) {
        $inputs += Get-Item -LiteralPath $about
    }

    $newest = ($inputs | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1)
    if (-not $newest) {
        return $true
    }

    return $newest.LastWriteTimeUtc -gt $built
}

# A product build (build.ps1 without -Lab) is newer than the sources but has no test host to run.
function Test-LabAssembly {
    if (-not (Test-Path -LiteralPath $dll)) {
        return $false
    }

    return [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll)).Contains('LabTestRunner')
}

# --- read-only log inspection ----------------------------------------------------
if ($InspectLog) {
    $found = Read-WotfLogErrors $InspectLog
    Write-Host ("log {0}: status={1} pid={2} started={3} lines={4} errorBlocks={5} ours={6}" -f `
        $found.Path, $found.Status, $found.ProcessId, $found.Started, $found.ScannedLines, $found.ErrorBlocks, $found.Messages.Count)
    foreach ($message in $found.Messages) { Write-Host "  ERROR $message" }

    exit ($(if ($found.Messages.Count -gt 0 -or $found.Status -ne 'ok') { 1 } else { 0 }))
}

# --- deployment facts ---------------------------------------------------------
if (-not (Test-Path -LiteralPath (Join-Path $deployed 'About/About.xml'))) {
    throw "Deployed mod not found at $deployed. The orchestrator owns the junction into RimWorld/Mods."
}

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "Repo-local .NET SDK not found: $dotnet"
}

$callStarted = Get-Date

# --- L0 build -----------------------------------------------------------------
$rebuilt = $false
if (-not $NoBuild) {
    $productDll = (Test-Path -LiteralPath $dll) -and -not (Test-LabAssembly)
    if ($productDll -or (Test-BuildStale)) {
        Write-Step ('L0 build ({0})' -f $(if ($productDll) { 'the assembly is a product build without the Lab host' } else { 'assembly is older than the sources' }))
        # The running game holds the DLL: only a rebuild may stop an instance by itself.
        Stop-LabInstance 'rebuild needs the assembly'

        $foreign = Get-ForeignGameProcesses
        if ($foreign) {
            throw ("RimWorld is running outside this lab (PID " + (($foreign | ForEach-Object ProcessId) -join ',') +
                '). Close it before building; this script never stops a game it did not start.')
        }

        # The csproj reads GameRoot/WorkshopRoot as MSBuild properties; build against the game this run tests.
        $env:GameRoot = $GameRoot
        $env:WorkshopRoot = Join-Path (Split-Path (Split-Path $GameRoot)) 'workshop/content/294100'
        & (Join-Path $PSScriptRoot 'build.ps1') -Lab
        if (-not (Test-Path -LiteralPath $dll)) {
            throw "build.ps1 finished but $dll is missing"
        }
        $rebuilt = $true
    }
    else {
        Write-Host "L0 build: up to date ($dll)"
    }
}
elseif (-not (Test-LabAssembly)) {
    throw "-NoBuild was given but $dll has no Lab host (the last build was a product build); run without -NoBuild."
}

# --- static Def checks ------------------------------------------------------------
# Seconds, and it catches what would otherwise cost a game start (missing types, textures, verb shapes).
Write-Step 'check-defs'
& (Join-Path $PSScriptRoot 'check-defs.ps1') -Repo $repo
if ($LASTEXITCODE -ne 0) {
    throw 'check-defs.ps1 failed; fix the Defs before starting the game.'
}

# --- instance -----------------------------------------------------------------
Write-Step 'lab instance'
if ($Restart) {
    Stop-LabInstance '-Restart'
}

$labProcess = Get-LabProcesses
if ($labProcess) {
    # An instance started with -mod= cannot see workshop mods (the game resolves that list only in
    # Mods/), so Harmony/HAR/RimUnity are missing and the assembly never loads. Do not reuse it.
    $broken = @($labProcess) | Where-Object { $_.CommandLine -match '-mod=' }
    if ($broken) {
        $broken | ForEach-Object {
            Write-Host "Stopping lab instance PID $($_.ProcessId): started with -mod= (known broken launch)"
            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Seconds 3
        $labProcess = Get-LabProcesses
    }
}

# Translations and textures load only at game start: a reused instance would test the old ones.
if ($labProcess) {
    $labStarted = (Get-Process -Id @($labProcess)[0].ProcessId).StartTime
    $changedAsset = @(foreach ($dir in 'Languages', 'Textures', '1.7/Languages', '1.7/Textures') {
        $path = Join-Path $repo $dir
        if (Test-Path -LiteralPath $path) {
            Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object { $_.LastWriteTime -gt $labStarted }
        }
    }) | Select-Object -First 1
    if ($changedAsset) {
        Stop-LabInstance "asset changed after the instance started: $($changedAsset.Name)"
        $labProcess = Get-LabProcesses
    }
}

$wasReused = $false
if ($labProcess) {
    $labProcess | ForEach-Object { Write-Host "Reusing lab instance PID $($_.ProcessId)" }
    $wasReused = $true
    if (@($labProcess).Count -gt 1) {
        # Two instances would consume the same request file; keep the oldest and stop the rest.
        $keep = @($labProcess)[0].ProcessId
        Write-Warning "More than one lab instance is running; keeping PID $keep"
        @($labProcess) | Where-Object { $_.ProcessId -ne $keep } | ForEach-Object {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
        }
    }
}
else {
    Start-LabInstance
}

$pidAtStart = Get-LabPid
$startedHere = $pidAtStart -and -not $wasReused

try {
    Write-Step 'waiting for the lab to answer'
    $status = Wait-LabReady
    if (-not $status -and $startedHere -eq $false -and (Get-LabPid)) {
        # An instance that no longer answers is not worth a rerun: replace it once.
        Write-Host 'The reused instance did not answer; restarting it.' -ForegroundColor Yellow
        Stop-LabInstance 'unhealthy instance'
        Start-LabInstance
        $status = Wait-LabReady
    }

    if (-not $status) {
        Show-GameLogHints
        throw ('The lab never processed a status request although the mod loaded. The game did not reach ' +
               'a map (main menu?). Check ' + $SaveDataFolder + '/Config/ModsConfig.xml and ' +
               (Join-Path $LabDir 'results.jsonl') + '.')
    }

    $pidNow = Get-LabPid
    $statusJson = $status | ConvertFrom-Json
    Write-Host ("pid={0} game loaded={1} harmonyPatched={2} tick={3}" -f $pidNow, $statusJson.gameLoaded, $statusJson.harmonyPatched, $statusJson.tick)

    # --- run ----------------------------------------------------------------------
    Write-Step ("L2 run filter='$Filter' fixture='$Fixture' repeat=$Repeat reload=$([bool]$Reload)")
    if (Test-Path -LiteralPath $latestReport) {
        Remove-Item -LiteralPath $latestReport -Force
    }

    $startedAt = Get-Date
    $command = 'test action=run'
    if ($Filter) { $command += " filter=$Filter" }
    if ($Fixture) { $command += " fixture=$Fixture" }
    else { $command += ' fixture=none' }
    if ($Repeat -gt 1) { $command += " repeat=$Repeat" }
    if ($Reload) { $command += ' reload=1' }

    $begin = & (Join-Path $PSScriptRoot 'lab.ps1') -LabDir $LabDir -Command $command -TimeoutSec 60
    if ($LASTEXITCODE -ne 0) {
        throw "The lab rejected the run request: $begin"
    }

    $beginJson = $begin | ConvertFrom-Json
    if (-not $beginJson.ok) {
        throw "The lab rejected the run request: $($beginJson.error)"
    }

    Write-Host ("run {0}: {1} test(s) x{2}, fixture loaded this run={3}" -f `
        $beginJson.run, $beginJson.tests, $beginJson.repeat, $beginJson.fixtureLoaded)

    # --- wait for the report ------------------------------------------------------
    $reportDeadline = (Get-Date).AddSeconds($TimeoutSec)
    $report = $null
    while ((Get-Date) -lt $reportDeadline) {
        if (Test-Path -LiteralPath $latestReport) {
            try {
                $report = Get-Content -LiteralPath $latestReport -Raw | ConvertFrom-Json
                if ($report.run -eq $beginJson.run) {
                    break
                }
            }
            catch {
                # Half-written file: read it again on the next pass.
            }
        }

        if (-not (Get-LabPid)) {
            throw 'The lab instance exited during the run; check the game log.'
        }

        Start-Sleep -Milliseconds 500
    }

    if (-not $report) {
        throw "No report appeared within $TimeoutSec s. The run may still be going; check $resultDir."
    }

    $elapsed = ((Get-Date) - $callStarted).TotalSeconds
    $runSeconds = ((Get-Date) - $startedAt).TotalSeconds

    # --- summary ------------------------------------------------------------------
    Write-Step 'results'
    $repeated = [int]$report.summary.repeat -gt 1
    foreach ($test in $report.tests) {
        if ($repeated -and $test.status -eq 'passed') {
            continue
        }

        $color = switch ($test.status) {
            'passed' { 'Green' }
            'expectedFailure' { 'Yellow' }
            default { 'Red' }
        }
        $pass = if ($null -ne $test.pass) { "#$($test.pass) " } else { '' }
        $line = "{0,-16} {1}{2}  {3} ms" -f $test.status.ToUpperInvariant(), $pass, $test.name, $test.elapsedMs
        Write-Host $line -ForegroundColor $color
        if ($test.measurements) {
            Write-Host ("    " + ($test.measurements -join ' ')) -ForegroundColor DarkGray
        }
        if ($test.message) {
            $lines = ($test.message -split ' \| ')
            Write-Host ("    " + ($lines[0])) -ForegroundColor $color
            if ($lines.Count -gt 1) {
                Write-Host ("    " + (($lines | Select-Object -Skip 1) -join ' | ')) -ForegroundColor $color
            }
        }
        if ($test.errors) {
            foreach ($logError in $test.errors) {
                Write-Host ("    log: " + $logError) -ForegroundColor DarkYellow
            }
        }
    }

    if ($repeated) {
        Write-Step 'stability'
        foreach ($row in $report.stability) {
            $color = if ($row.failures -eq 0) { 'Green' } else { 'Red' }
            Write-Host ("{0,-40} {1}/{2} passed" -f $row.name, ($row.runs - $row.failures), $row.runs) -ForegroundColor $color
        }
    }

    Write-Step 'L1 load checks'
    $loadColor = if ($report.load.passed) { 'Green' } else { 'Red' }
    Write-Host ("passed={0} defs={1} ownErrors run/load={2}/{3} (all errors run/load={4}/{5})" -f $report.load.passed, $report.load.defsChecked, $report.load.ownErrorsDuringRun, $report.load.ownErrorsDuringLoad, $report.load.logErrorsDuringRun, $report.load.logErrorsDuringLoad) -ForegroundColor $loadColor
    foreach ($note in $report.load.notes) { Write-Host "  $note" }
    foreach ($failure in $report.load.failures) { Write-Host "  FAIL $failure" -ForegroundColor Red }

    Write-Step 'L1 disk log (this process)'
    $logPaths = Get-PlayerLogPaths
    if ($PlayerLog) { $logPaths.Current = $PlayerLog }
    $pidForRun = Get-LabPid

    # The log covers the whole process, so a repeat call against the same instance would report the
    # errors of an earlier call again. Read only what was appended since the last scan of this pid.
    $scanStatePath = Join-Path $resultDir ("logscan-{0}.json" -f $pidForRun)
    $skipLines = 0
    if ((Test-Path -LiteralPath $scanStatePath) -and (Test-Path -LiteralPath $logPaths.Current)) {
        try {
            $previousScan = Get-Content -LiteralPath $scanStatePath -Raw | ConvertFrom-Json
            $logLength = (Get-Item -LiteralPath $logPaths.Current).Length
            if ($previousScan.path -eq $logPaths.Current -and $logLength -ge [long]$previousScan.length) {
                $skipLines = [int]$previousScan.lines
            }
        }
        catch {
            $skipLines = 0
        }
    }

    # A test may declare an error it is meant to trigger; the report lists the entries whose text
    # matched such a declaration, and only those blocks are exempt from the L1 gate.
    $expectedSamples = @()
    if ($report.load -and $report.load.expectedErrorSamples) {
        $expectedSamples = @($report.load.expectedErrorSamples)
    }

    $disk = Read-WotfLogErrors $logPaths.Current -SkipLines $skipLines -ExpectedSamples $expectedSamples
    $diskGate = 'ok'
    $diskNote = ''
    switch ($disk.Status) {
        'missing' { $diskGate = 'unverified'; $diskNote = "log file not found: $($logPaths.Current)" }
        'empty' { $diskGate = 'unverified'; $diskNote = "log file is empty (nothing was verified): $($logPaths.Current)" }
        'unreadable' { $diskGate = 'unverified'; $diskNote = "log has no process header: $($logPaths.Current)" }
        'ok' {
            if ($disk.ProcessId -ne $pidForRun) {
                $diskGate = 'unverified'
                $diskNote = "log belongs to pid $($disk.ProcessId), this run used pid $pidForRun"
            }
        }
    }

    $diskColor = if ($diskGate -eq 'ok' -and $disk.Messages.Count -eq 0) { 'Green' } else { 'Red' }
    Write-Host ("status={0} pid={1} started={2} lines={3} (new this call={4}) errorBlocks={5} exempted={6} ourErrors={7}" -f `
        $diskGate, $disk.ProcessId, $disk.Started, $disk.TotalLines, $disk.ScannedLines, $disk.ErrorBlocks, $disk.Exempted, $disk.Messages.Count) -ForegroundColor $diskColor
    if ($diskNote) { Write-Host "  $diskNote" -ForegroundColor Red }

    # One failure list: the disk log covers the whole process (including mod loading, which the
    # 1000-line queue drops), the queue samples cover anything the file did not flush yet. Deduplicated
    # by message text so one event is not reported twice.
    $logFailures = New-Object System.Collections.Generic.List[string]
    $logKeys = @{}
    $printBudget = 12
    foreach ($message in $disk.Messages) {
        $key = Get-LogMessageKey $message
        if ($logKeys.ContainsKey($key)) { continue }
        $logKeys[$key] = $true
        $logFailures.Add($message)
    }

    foreach ($sample in @($report.load.errorSamples)) {
        if (-not $sample) { continue }
        # Old-save class names are unsupported; they are not this run's failures.
        if (($sample -match 'WhisperOfTheForest\.Experimental\.') -or
            ($sample -match "Can't load abstract class Verse\.(GameComponent|MapComponent)")) {
            continue
        }

        $key = Get-LogMessageKey $sample
        if ($logKeys.ContainsKey($key)) { continue }
        $logKeys[$key] = $true
        $logFailures.Add($sample)
    }

    foreach ($failure in $logFailures) {
        if ($printBudget-- -le 0) {
            Write-Host ("  ... and {0} more" -f ($logFailures.Count - 12)) -ForegroundColor Red
            break
        }

        Write-Host "  ERROR $failure" -ForegroundColor Red
    }

    # Remember how far this pid's log was read, so the next call on the same instance starts after it.
    $logLengthNow = 0
    if (Test-Path -LiteralPath $logPaths.Current) {
        $logLengthNow = (Get-Item -LiteralPath $logPaths.Current).Length
    }
    [pscustomobject]@{
        path = $logPaths.Current
        lines = $disk.TotalLines
        length = $logLengthNow
    } | ConvertTo-Json | Set-Content -LiteralPath $scanStatePath -Encoding utf8

    $diskReport = Join-Path $resultDir ($report.run + '.disklog.json')
    [pscustomobject]@{
        run = $report.run
        checkedAt = (Get-Date).ToString('o')
        status = $diskGate
        note = $diskNote
        path = $disk.Path
        processId = $disk.ProcessId
        labProcessId = $pidForRun
        started = $disk.Started
        scannedLines = $disk.ScannedLines
        errorBlocks = $disk.ErrorBlocks
        exempted = $disk.Exempted
        ourErrors = $disk.Messages
        queueSamples = @($report.load.errorSamples)
        failures = $logFailures
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $diskReport -Encoding utf8
    Write-Host ("Disk log report: {0}" -f $diskReport)

    Write-Host ''
    Write-Host ("Summary: {0} total, {1} passed, {2} failed, {3} expected failure(s)" -f `
        $report.summary.total, $report.summary.passed, $report.summary.failed, $report.summary.expectedFailure)
    Write-Host ("Report: {0}" -f $latestReport)
    Write-Host ("lab pid={0}  run={1:N1}s  call={2:N1}s  (run {3})" -f `
        (Get-LabPid), $runSeconds, $elapsed, $report.run)

    # One line per call, so the time spent on tests can be counted without opening every report.
    $ledger = Join-Path $resultDir 'ledger.tsv'
    if (-not (Test-Path -LiteralPath $ledger)) {
        Set-Content -LiteralPath $ledger -Encoding utf8 -Value "run`tfilter`ttotal`tfailed`trunSec`tcallSec`trebuilt`treusedInstance"
    }
    Add-Content -LiteralPath $ledger -Encoding utf8 -Value ("{0}`t{1}`t{2}`t{3}`t{4:F1}`t{5:F1}`t{6}`t{7}" -f `
        $report.run, $(if ($Filter) { $Filter } else { '*' }), $report.summary.total, $report.summary.failed, $runSeconds, $elapsed, $rebuilt, $wasReused)

    $failed = ($report.summary.failed -gt 0) -or (-not $report.load.passed) `
        -or ([int]$report.load.ownErrorsDuringLoad -gt 0) -or ([int]$report.load.ownErrorsDuringRun -gt 0) `
        -or ($diskGate -ne 'ok') -or ($logFailures.Count -gt 0)
    if ($failed) {
        $global:LASTEXITCODE = 1
    }
    else {
        $global:LASTEXITCODE = 0
    }
}
catch {
    Show-GameLogHints
    throw
}
finally {
    if ($StopAfter) {
        Stop-LabInstance '-StopAfter'
    }
    elseif (Get-LabPid) {
        Write-Host ("Lab instance left running: PID {0} (lab {1}); pass -StopAfter to close it." -f (Get-LabPid), $LabDir)
    }
}

exit $LASTEXITCODE
