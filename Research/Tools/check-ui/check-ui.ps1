param(
  [string]$AssemblyPath = 'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/check-ui/proj-real/bin/Debug/net10.0/WotF_UI_Check_Real.dll'
)

$mod = 'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest'
$errors = New-Object System.Collections.Generic.List[string]
$srcFiles = Get-ChildItem "$mod/Source/WhisperOfTheForest/UI" -Filter *.cs

# --- the wrong bottom-bar MainButtonDef entry must be gone ---------
if (Test-Path "$mod/1.7/Defs/Experiments/UI.xml") {
  $errors.Add('1.7/Defs/Experiments/UI.xml still exists; the bottom-bar MainButtonDef entry should be removed')
}
if (Test-Path "$mod/Source/WhisperOfTheForest/UI/TechLabMainButtonWorker.cs") {
  $errors.Add('TechLabMainButtonWorker.cs still exists')
}
$mainButtonDecl = Get-ChildItem "$mod/1.7/Defs" -Recurse -Filter *.xml -ErrorAction SilentlyContinue |
  Select-String -Pattern '<MainButtonDef>' -SimpleMatch
if ($mainButtonDecl) { $errors.Add("MainButtonDef still declared in: $($mainButtonDecl.Path)") }
if (-not (Select-String -Path "$mod/Source/WhisperOfTheForest/UI/*.cs" -Pattern 'rowVisibility\.ButtonIcon\(' -Quiet)) {
  $errors.Add('no native WidgetRow entry found (expected rowVisibility.ButtonIcon(...))')
}

# --- referenced keyed strings exist in both languages -----------
$used = New-Object System.Collections.Generic.HashSet[string]
foreach ($f in $srcFiles) {
  $text = Get-Content -Raw -Encoding UTF8 $f.FullName
  foreach ($m in [regex]::Matches($text, '"WotF_UI\.[A-Za-z0-9_.]+"')) {
    [void]$used.Add($m.Value.Trim('"'))
  }
}
foreach ($lang in @('ChineseSimplified', 'English')) {
  [xml]$keyed = Get-Content -Raw -Encoding UTF8 "$mod/1.7/Languages/$lang/Keyed/WotF_UI.xml"
  $keys = @{}
  foreach ($n in $keyed.LanguageData.ChildNodes) { if ($n.NodeType -eq 'Element') { $keys[$n.Name] = $n.InnerText } }
  foreach ($k in $used) { if (-not $keys.ContainsKey($k)) { $errors.Add("$lang missing key: $k") } }
  foreach ($k in $keys.Keys) { if (-not $used.Contains($k)) { $errors.Add("$lang unused key: $k") } }
}

# --- built assembly contracts -----------------------------------
Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath)
$types = @{}
foreach ($t in $asm.MainModule.Types) { $types[$t.FullName] = $t }

$patch = $types['WhisperOfTheForest.Experimental.Patch_PlaySettingsRowEntry']
if ($null -eq $patch) { $errors.Add('patch type missing') }
else {
  $attr = $patch.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'HarmonyPatch' }
  if ($null -eq $attr) { $errors.Add('patch lacks HarmonyPatch attribute') }
  else {
    $args = $attr.ConstructorArguments
    $target = $args[0].Value
    $methodName = $args[1].Value
    $found = $target.Resolve().Methods | Where-Object { $_.Name -eq $methodName }
    if ($null -eq $found) { $errors.Add("Harmony target not found: $($target.FullName).$methodName") }
    else {
      "harmony target: $($target.FullName).$methodName($(($found.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', ')) public=$($found.IsPublic) static=$($found.IsStatic)"
      $expect = @('WidgetRow', 'Boolean', 'Single&')
      $actual = @($found.Parameters | ForEach-Object { $_.ParameterType.Name })
      if (($actual -join ',') -ne ($expect -join ',')) { $errors.Add("unexpected target parameters: $($actual -join ',')") }
    }
  }

  $postfix = $patch.Methods | Where-Object { $_.Name -eq 'Postfix' }
  if ($null -eq $postfix) { $errors.Add('patch lacks Postfix') }
  else {
    "postfix parameters: $(($postfix.Parameters | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }) -join ', ')"
    foreach ($parameter in $postfix.Parameters) {
      $original = @($found.Parameters | Where-Object { $_.Name -eq $parameter.Name })
      if ($original.Count -ne 1 -or $original[0].ParameterType.FullName -ne $parameter.ParameterType.FullName) {
        $errors.Add("Harmony parameter does not bind to target: $($parameter.ParameterType.FullName) $($parameter.Name)")
      }
    }
  }
}

# --- GameComponent auto-registration contract -------------------
$overlay = $types['WhisperOfTheForest.Experimental.TechLabEntryOverlay']
if ($null -eq $overlay) { $errors.Add('overlay type missing') }
elseif ($overlay.BaseType.FullName -ne 'Verse.GameComponent') { $errors.Add("overlay base type $($overlay.BaseType.FullName)") }
else {
  # Game.FillComponents: Activator.CreateInstance(type, new object[] { this }) -> ctor(Game) required.
  $ctor = $overlay.Methods | Where-Object {
    $_.IsConstructor -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'Verse.Game'
  }
  if ($null -eq $ctor) { $errors.Add('overlay needs a ctor(Verse.Game) for Game.FillComponents') }
}

$win = $types['WhisperOfTheForest.Experimental.Window_TechLab']
if ($null -eq $win) { $errors.Add('window type missing') }
elseif ($win.BaseType.FullName -ne 'Verse.Window') { $errors.Add("window base type $($win.BaseType.FullName)") }

# --- static asset fields require StaticConstructorOnStartup -----
foreach ($t in $types.Values) {
  if (-not $t.HasFields) { continue }
  $assetField = $null
  foreach ($f in $t.Fields) {
    if (-not $f.IsStatic) { continue }
    $ft = $f.FieldType
    if ($ft.IsArray) { $ft = $ft.GetElementType() }
    if ($null -eq $ft) { continue }
    if ($ft.FullName -eq 'UnityEngine.Texture2D' -or $ft.FullName -eq 'UnityEngine.Texture') { $assetField = $f; break }
  }
  if ($null -ne $assetField) {
    $hasAttr = $t.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'StaticConstructorOnStartup' }
    if ($null -eq $hasAttr) { $errors.Add("type $($t.Name) has static asset field $($assetField.Name) but no [StaticConstructorOnStartup]") }
  }
}

# --- text/colour state must not leak into the next frame ---------
# Verse.Text.StartOfOnGUI(): WordWrap must be true and Anchor UpperLeft, else Log.ErrorOnce.
foreach ($f in $srcFiles) {
  $text = Get-Content -Raw -Encoding UTF8 $f.FullName
  if ($text -match 'Text\.WordWrap\s*=\s*false') {
    $errors.Add("$($f.Name) sets Text.WordWrap = false (leaks into the next frame)")
  }
}
foreach ($entry in @('TechLabEntryOverlay.cs', 'Window_TechLab.cs', 'TechLabMod.cs', 'Patch_PlaySettingsRowEntry.cs')) {
  $entryPath = "$mod/Source/WhisperOfTheForest/UI/$entry"
  if (-not (Select-String -Path $entryPath -Pattern 'TechLabDrawScope\.Begin\(\)' -Quiet)) {
    $errors.Add("$entry does not save/restore text state via TechLabDrawScope")
  }
}

if ($errors.Count -eq 0) { 'UI CHECK OK' } else { 'UI CHECK FAILED'; $errors | ForEach-Object { "  - $_" }; exit 1 }
