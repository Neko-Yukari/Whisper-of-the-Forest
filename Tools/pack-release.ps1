# Packages Whisper of the Forest into a clean, Steam Workshop upload-ready folder (default: Dist\Whisper-of-the-Forest)
# and generates Dist\workshop.vdf for SteamCMD.
# ASCII-only source so Windows PowerShell 5.1 and pwsh 7 parse it identically.
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [ValidateSet(0, 1, 2, 3)]
    [int]$Visibility = 3,
    [string]$ChangeNote = '',
    [string]$OutDir = (Join-Path (Split-Path -Parent $PSScriptRoot) (Join-Path 'Dist' 'Whisper-of-the-Forest'))
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$MaxPreviewBytes = 1MB
$RimWorldAppId = '294100'

function Invoke-Pwsh([string]$ScriptName, [string]$What) {
    $script = Join-Path $PSScriptRoot $ScriptName
    & pwsh -NoProfile -File $script
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed with exit code $LASTEXITCODE ($ScriptName)"
    }
}

function Get-RelativePath([string]$Root, [string]$Path) {
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $pathFull = [IO.Path]::GetFullPath($Path)
    return $pathFull.Substring($rootFull.Length).TrimStart('\', '/')
}

function Copy-ToPackage([string]$RelativePath) {
    $src = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $src -PathType Leaf)) {
        throw "Release input missing: $RelativePath"
    }
    $dst = Join-Path $script:PackageRoot $RelativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dst) | Out-Null
    Copy-Item -LiteralPath $src -Destination $dst -Force
}

function Copy-FilesToPackage([string]$RelativeDir) {
    $srcDir = Join-Path $RepoRoot $RelativeDir
    if (-not (Test-Path -LiteralPath $srcDir -PathType Container)) {
        throw "Release input directory missing: $RelativeDir"
    }
    $files = @(Get-ChildItem -LiteralPath $srcDir -File -Recurse)
    if ($files.Count -eq 0) {
        throw "Release input directory is empty: $RelativeDir"
    }
    foreach ($file in $files) {
        Copy-ToPackage (Get-RelativePath $RepoRoot $file.FullName)
    }
}

function Escape-Vdf([string]$Value) {
    return ($Value -replace '\\', '\\' -replace '"', '\"')
}

# Step 1: build
Write-Host '== [1/5] build =='
if (Get-Process -Name 'RimWorld' -ErrorAction SilentlyContinue) {
    throw 'RimWorld is running: cannot refresh 1.7/Assemblies/WhisperOfTheForest.dll for release packaging.'
}
Invoke-Pwsh 'build.ps1' 'Build'

# Step 2: check-defs
Write-Host '== [2/5] check-defs =='
Invoke-Pwsh 'check-defs.ps1' 'Def and assembly check'

# Step 3: offline tests
if ($SkipTests) {
    Write-Host '== [3/5] offline tests: SKIPPED (-SkipTests) =='
}
else {
    Write-Host '== [3/5] offline tests =='
    Invoke-Pwsh 'test-offline.ps1' 'Offline tests'
}

# Step 4: assemble the distributable folder
Write-Host "== [4/5] assemble $OutDir =="
$outFull = [IO.Path]::GetFullPath($OutDir).TrimEnd('\', '/')
$repoFull = [IO.Path]::GetFullPath($RepoRoot).TrimEnd('\', '/')
$sep = [string][IO.Path]::DirectorySeparatorChar
$distFull = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'Dist')).TrimEnd('\', '/')

# Inside the repo only a subdirectory of Dist may be wiped.
$insideRepo = $outFull.StartsWith($repoFull + $sep, [StringComparison]::OrdinalIgnoreCase)
$insideDist = $outFull.StartsWith($distFull + $sep, [StringComparison]::OrdinalIgnoreCase)
$unsafe = ($outFull -eq $repoFull) -or
    $repoFull.StartsWith($outFull + $sep, [StringComparison]::OrdinalIgnoreCase) -or
    ($outFull.Length -le 3) -or
    ($insideRepo -and -not $insideDist)
if ($unsafe) {
    throw "Refusing to wipe unsafe output directory: $outFull (inside the repo only a subdirectory of Dist is allowed)"
}

if (Test-Path -LiteralPath $outFull) {
    Remove-Item -LiteralPath $outFull -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $outFull | Out-Null
$script:PackageRoot = $outFull

foreach ($relative in @(
        'About/About.xml',
        'About/Preview.png',
        'LoadFolders.xml',
        'README.md',
        '1.7/Assemblies/WhisperOfTheForest.dll')) {
    Copy-ToPackage $relative
}
$pubIdPath = Join-Path $RepoRoot 'About/PublishedFileId.txt'
$publishedFileId = '0'
if (Test-Path -LiteralPath $pubIdPath -PathType Leaf) {
    Copy-ToPackage 'About/PublishedFileId.txt'
    $rawId = (Get-Content -LiteralPath $pubIdPath -Raw).Trim()
    if ($rawId -match '^\d+$') {
        $publishedFileId = $rawId
    }
}

foreach ($dir in @(
        'Textures',
        '1.7/Data',
        '1.7/Defs',
        '1.7/Languages',
        '1.7/Shaders',
        '1.7/Sounds')) {
    Copy-FilesToPackage $dir
}

# Step 5: verification gate + Workshop VDF generation
Write-Host '== [5/5] verify & workshop.vdf =='
$failures = New-Object System.Collections.Generic.List[string]

foreach ($required in @(
        'About/About.xml',
        'About/Preview.png',
        'LoadFolders.xml',
        'README.md',
        '1.7/Assemblies/WhisperOfTheForest.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $outFull $required) -PathType Leaf)) {
        $failures.Add("missing required file: $required")
    }
}

$packedPreview = Join-Path $outFull 'About/Preview.png'
if (Test-Path -LiteralPath $packedPreview -PathType Leaf) {
    $previewBytes = (Get-Item -LiteralPath $packedPreview).Length
    if ($previewBytes -ge $MaxPreviewBytes) {
        $failures.Add("About/Preview.png is $previewBytes bytes, must be < $MaxPreviewBytes")
    }
}

$packed = @(Get-ChildItem -LiteralPath $outFull -File -Recurse)
foreach ($file in $packed) {
    $relative = (Get-RelativePath $outFull $file.FullName).Replace('\', '/')
    $forbidden = ($file.Extension -in @('.cs', '.pdb', '.sln', '.csproj', '.log', '.bak')) -or
        ($file.Name -like '*.deps.json') -or
        ($relative -like '.git*') -or
        ($relative -like '.claude*') -or
        ($relative -like '.scratch*') -or
        ($relative -like '.cortexkit*') -or
        ($relative -like 'Source/*') -or
        ($relative -like 'Tests/*') -or
        ($relative -like 'Tools/*') -or
        ($relative -like 'Research/*')
    if ($forbidden) {
        $failures.Add("forbidden file in package: $relative")
    }
    if ($file.Extension -eq '.xml') {
        try {
            $xmlDoc = [System.Xml.XmlDocument]::new()
            $xmlDoc.Load($file.FullName)
        }
        catch {
            $failures.Add("invalid XML ($($_.Exception.Message)): $relative")
        }
    }
    if ($file.Extension -in @('.xml', '.json', '.glsl', '.txt', '.md')) {
        $txt = [IO.File]::ReadAllText($file.FullName)
        if ($txt -match '(C:\\Users\\|E:\\Steam|sk-[A-Za-z0-9]{20,}|ghp_[A-Za-z0-9]{20,}|BEGIN PRIVATE KEY)') {
            $failures.Add("potential local path or secret match ($($Matches[1])) in $relative")
        }
    }
}

$aboutXml = [System.Xml.XmlDocument]::new()
$aboutXml.Load((Join-Path $outFull 'About/About.xml'))
$modName = $aboutXml.ModMetaData.name.Trim()
$modVersion = $aboutXml.ModMetaData.modVersion.Trim()
$modDesc = $aboutXml.ModMetaData.description.Trim()
$versions = @($aboutXml.ModMetaData.supportedVersions.li | ForEach-Object { $_.Trim() })
if ($versions -contains '1.6') {
    $failures.Add('About/About.xml still declares unsupported 1.6 version')
}
if (-not ($versions -contains '1.7')) {
    $failures.Add('About/About.xml must declare 1.7 in supportedVersions')
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "VERIFY FAIL: $failure"
    }
    throw "Release verification failed with $($failures.Count) problem(s)."
}

# Generate Dist\workshop.vdf (only set title/description on initial upload or let Steam preserve page text)
if ([string]::IsNullOrWhiteSpace($ChangeNote)) {
    $ChangeNote = "v$modVersion"
}
$vdfPath = Join-Path (Split-Path -Parent $outFull) 'workshop.vdf'
$vdfLines = New-Object System.Collections.Generic.List[string]
$vdfLines.Add('"workshopitem"')
$vdfLines.Add('{')
$vdfLines.Add("`t`"appid`"`t`t`"$RimWorldAppId`"")
$vdfLines.Add("`t`"publishedfileid`"`t`"$publishedFileId`"")
$vdfLines.Add("`t`"contentfolder`"`t`t`"$(Escape-Vdf $outFull)`"")
$vdfLines.Add("`t`"previewfile`"`t`t`"$(Escape-Vdf $packedPreview)`"")
$vdfLines.Add("`t`"visibility`"`t`t`"$Visibility`"")
if ($publishedFileId -eq '0') {
    $vdfLines.Add("`t`"title`"`t`t`"$(Escape-Vdf $modName)`"")
    $vdfLines.Add("`t`"description`"`t`t`"$(Escape-Vdf $modDesc)`"")
}
$vdfLines.Add("`t`"changenote`"`t`t`"$(Escape-Vdf $ChangeNote)`"")
$vdfLines.Add('}')
[IO.File]::WriteAllText($vdfPath, ($vdfLines -join "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))

$totalBytes = ($packed | Measure-Object -Property Length -Sum).Sum
$totalMB = [Math]::Round($totalBytes / 1MB, 2)
Write-Host "PACK OK: $outFull ($($packed.Count) files, $totalBytes bytes / $totalMB MB)"
Write-Host "VDF OK:  $vdfPath (publishedfileid=$publishedFileId, visibility=$Visibility, version=$modVersion)"
