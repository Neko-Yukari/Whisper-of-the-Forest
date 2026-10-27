#requires -Version 7.0
<#
  Static checks for the experiment package layout. Complements the C# build:
  1.7-only experiment defs, versioned About dependencies, no game DLLs in the repo,
  no experiment defs leaking into the shared (1.6) Defs folder.
#>
[CmdletBinding()]
param(
    [switch]$RequireFeatureDefs
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]
$passed = New-Object System.Collections.Generic.List[string]

function Fail([string]$message) { $script:failures.Add($message) }
function Warn([string]$message) { $script:warnings.Add($message) }
function Pass([string]$message) { $script:passed.Add($message) }

# --- XML well-formedness -----------------------------------------------------
$xmlFiles = @(
    (Join-Path $repo 'About/About.xml'),
    (Join-Path $repo 'LoadFolders.xml')
)
$defs17 = Join-Path $repo '1.7/Defs'
if (Test-Path -LiteralPath $defs17) {
    $xmlFiles += Get-ChildItem -LiteralPath $defs17 -Recurse -Filter '*.xml' -File | ForEach-Object FullName
}

$parsed = @{}
foreach ($file in $xmlFiles) {
    if (-not (Test-Path -LiteralPath $file)) {
        Fail "missing XML: $file"
        continue
    }

    try {
        $parsed[$file] = [xml](Get-Content -LiteralPath $file -Raw)
        Pass "XML parses: $file"
    }
    catch {
        Fail "XML parse error in $file : $($_.Exception.Message)"
    }
}

# --- About.xml ---------------------------------------------------------------
$aboutPath = Join-Path $repo 'About/About.xml'
if ($parsed.ContainsKey($aboutPath)) {
    $meta = $parsed[$aboutPath].ModMetaData
    if ([string]$meta.packageId -ne 'NekoYukari.WhisperOfTheForest') {
        Fail "packageId changed: $($meta.packageId)"
    }
    else {
        Pass 'packageId = NekoYukari.WhisperOfTheForest'
    }

    $versions = @($meta.supportedVersions.li)
    if ($versions -notcontains '1.6' -or $versions -notcontains '1.7') {
        Fail "supportedVersions must contain 1.6 and 1.7, got: $($versions -join ',')"
    }
    else {
        Pass 'supportedVersions = 1.6, 1.7'
    }

    $v17 = $meta.modDependenciesByVersion.'v1.7'
    if (-not $v17) {
        Fail 'modDependenciesByVersion/v1.7 is missing'
    }
    else {
        $depIds = @($v17.li | ForEach-Object { [string]$_.packageId })
        foreach ($required in @('brrainz.harmony', 'erdelf.HumanoidAlienRaces', 'Chezhou.RimUnity', 'ludeon.rimworld.royalty')) {
            if ($depIds -notcontains $required) {
                Fail "1.7 dependency missing: $required"
            }
        }
        Pass "1.7 dependencies: $($depIds -join ', ')"
    }

    $loadAfter = @($meta.loadAfter.li)
    foreach ($required in @('brrainz.harmony', 'erdelf.HumanoidAlienRaces', 'Chezhou.RimUnity')) {
        if ($loadAfter -notcontains $required) {
            Fail "loadAfter missing: $required"
        }
    }
    Pass "loadAfter: $($loadAfter -join ', ')"
}

# --- LoadFolders.xml ---------------------------------------------------------
$loadFoldersPath = Join-Path $repo 'LoadFolders.xml'
if ($parsed.ContainsKey($loadFoldersPath)) {
    $folders = $parsed[$loadFoldersPath].loadFolders
    $v16 = @($folders.'v1.6'.li)
    $v17Folders = @($folders.'v1.7'.li)
    if ($v16 -notcontains '/') { Fail "LoadFolders v1.6 must include '/'" } else { Pass "LoadFolders v1.6: $($v16 -join ', ')" }
    if ($v17Folders -notcontains '/' -or $v17Folders -notcontains '1.7') {
        Fail "LoadFolders v1.7 must include '/' and '1.7', got: $($v17Folders -join ', ')"
    }
    else {
        Pass "LoadFolders v1.7: $($v17Folders -join ', ')"
    }
}

# --- Experiment def isolation ------------------------------------------------
$featureDefs = @(
    'WotF_ArcaneMissiles', 'WotF_ArcaneMissiles_Heavy', 'WotF_ArcaneMissiles_Mender',
    'WotF_TerrainStride', 'WotF_BlinkStride',
    'WotF_Visual_RimUnity', 'WotF_Visual_Native',
    'WotF_Impact_Damage', 'WotF_Impact_Heal', 'WotF_Impact_Explode'
)
$all17Text = ''
if (Test-Path -LiteralPath $defs17) {
    $all17Text = (Get-ChildItem -LiteralPath $defs17 -Recurse -Filter '*.xml' -File | Get-Content -Raw) -join "`n"
}

$sharedDefs = Join-Path $repo 'Defs'
$sharedText = ''
if (Test-Path -LiteralPath $sharedDefs) {
    $sharedText = (Get-ChildItem -LiteralPath $sharedDefs -Recurse -Filter '*.xml' -File | Get-Content -Raw) -join "`n"
}

foreach ($defName in $featureDefs) {
    $in17 = $all17Text -match [regex]::Escape("<defName>$defName</defName>")
    $inShared = $sharedText -match [regex]::Escape("<defName>$defName</defName>")
    if ($inShared) {
        Fail "$defName is defined under shared Defs/ (1.6 would load it)"
    }
    if ($in17) {
        Pass "def present in 1.7/Defs: $defName"
    }
    elseif ($RequireFeatureDefs) {
        Fail "def missing from 1.7/Defs: $defName"
    }
    else {
        Warn "def not present yet in 1.7/Defs: $defName"
    }
}

# --- UI entry lives in the native play-settings row ---------------------------
# Same predicates as Moddev/tools/check-ui/check-ui.ps1, kept source-level here so the
# package check does not depend on that separate built check assembly.
$mainButtonDecl = Get-ChildItem -LiteralPath $defs17 -Recurse -Filter '*.xml' -File -ErrorAction SilentlyContinue |
    Select-String -Pattern '<MainButtonDef>' -SimpleMatch
if ($mainButtonDecl) {
    Fail ("MainButtonDef still declared in: " + (($mainButtonDecl.Path | Select-Object -Unique) -join ', '))
}
else {
    Pass 'no MainButtonDef declared under 1.7/Defs'
}

if (Test-Path -LiteralPath (Join-Path $repo '1.7/Defs/Experiments/UI.xml')) {
    Fail '1.7/Defs/Experiments/UI.xml still exists; the bottom-bar MainButtonDef entry should be removed'
}
else {
    Pass 'no bottom-bar UI.xml experiment def'
}

$uiDir = Join-Path $repo 'Source/WhisperOfTheForest/UI'
$mainButtonWorker = Join-Path $uiDir 'TechLabMainButtonWorker.cs'
if (Test-Path -LiteralPath $mainButtonWorker) {
    Fail 'TechLabMainButtonWorker.cs still exists'
}
else {
    Pass 'no bottom-bar MainButton worker source'
}

$rowPatch = Join-Path $uiDir 'Patch_PlaySettingsRowEntry.cs'
if (-not (Test-Path -LiteralPath $rowPatch)) {
    Fail 'Patch_PlaySettingsRowEntry.cs is missing'
}
elseif (-not (Select-String -LiteralPath $rowPatch -Pattern 'class Patch_PlaySettingsRowEntry' -SimpleMatch -Quiet)) {
    Fail 'Patch_PlaySettingsRowEntry type missing'
}
elseif (-not (Select-String -LiteralPath $rowPatch -Pattern '\.ButtonIcon\(' -Quiet)) {
    Fail 'no native WidgetRow entry found (expected row.ButtonIcon(...))'
}
else {
    Pass 'native PlaySettings row entry exists (WidgetRow.ButtonIcon)'
}

$techLabUi = Join-Path $uiDir 'TechLabUi.cs'
if (-not (Test-Path -LiteralPath $techLabUi)) {
    Fail 'TechLabUi.cs is missing'
}
elseif (-not (Select-String -LiteralPath $techLabUi -Pattern 'public static void OpenWindowAndRestoreEntry(' -SimpleMatch -Quiet)) {
    Fail 'TechLabUi.OpenWindowAndRestoreEntry is missing'
}
elseif (-not (Select-String -LiteralPath $rowPatch -Pattern 'OpenWindowAndRestoreEntry(' -SimpleMatch -Quiet)) {
    Fail 'native row entry does not call TechLabUi.OpenWindowAndRestoreEntry'
}
else {
    Pass 'native row entry calls TechLabUi.OpenWindowAndRestoreEntry'
}

# --- No game assemblies copied into the repo --------------------------------
$forbidden = @(
    'Assembly-CSharp.dll', 'UnityStubs.dll', 'Waylith.dll', '0Harmony.dll',
    'RimUnity.dll', 'RimUnity.Core.dll', 'AlienRace.dll', 'RimWorld.exe'
)
$found = Get-ChildItem -LiteralPath $repo -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' -and $forbidden -contains $_.Name }
if ($found) {
    Fail ("game/dependency assemblies present in repo: " + (($found | ForEach-Object FullName) -join '; '))
}
else {
    Pass 'no game/dependency assemblies copied into repo'
}

# --- Project references ------------------------------------------------------
$csproj = Join-Path $repo 'Source/WhisperOfTheForest/WhisperOfTheForest.csproj'
if (-not (Test-Path -LiteralPath $csproj)) {
    Fail "missing project: $csproj"
}
else {
    [xml]$project = Get-Content -LiteralPath $csproj -Raw
    $references = @($project.Project.ItemGroup.Reference | Where-Object { $_ })
    $publicRefs = @($references | Where-Object { [string]$_.Private -ne 'false' })
    if ($publicRefs.Count -gt 0) {
        Fail ("references without Private=false: " + (($publicRefs | ForEach-Object Include) -join ', '))
    }
    else {
        Pass "all $($references.Count) references are Private=false"
    }

    if ([string]$project.Project.PropertyGroup.TargetFramework -ne 'net10.0') {
        Fail "TargetFramework is not net10.0"
    }
    else {
        Pass 'TargetFramework = net10.0'
    }
}

# --- Built output ------------------------------------------------------------
$dll = Join-Path $repo '1.7/Assemblies/WhisperOfTheForest.dll'
if (Test-Path -LiteralPath $dll) {
    $item = Get-Item -LiteralPath $dll
    Pass "assembly exists: $($item.Length) bytes, $($item.LastWriteTimeUtc.ToString('o'))"
}
else {
    Warn '1.7/Assemblies/WhisperOfTheForest.dll not built yet'
}

# --- Report ------------------------------------------------------------------
foreach ($message in $passed) { Write-Host "PASS  $message" }
foreach ($message in $warnings) { Write-Host "WARN  $message" -ForegroundColor Yellow }
foreach ($message in $failures) { Write-Host "FAIL  $message" -ForegroundColor Red }

Write-Host ''
Write-Host "Summary: $($passed.Count) passed, $($warnings.Count) warnings, $($failures.Count) failures"

if ($failures.Count -gt 0) {
    exit 1
}

exit 0
