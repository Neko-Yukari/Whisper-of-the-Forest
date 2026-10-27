# 只读校验 Whisper-of-the-Forest 原精灵 Def 的两类已知错误来源：
#   1) 命名 RulePack 中 rulesStrings 引用的符号是否都有定义，引用的名字文件是否存在；
#   2) 种族 Def 是否有近战 tools，且 linkedBodyPartsGroup 是否在其 body 中存在。
# 用法: pwsh -NoProfile -File ./check_elf.ps1 [-Repo <path>]
param(
  [string]$Repo = 'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest',
  [string]$GameData = 'E:/SteamLibrary/steamapps/common/RimWorld/Data'
)

$errors = [System.Collections.Generic.List[string]]::new()
$notes = [System.Collections.Generic.List[string]]::new()

function Token-Symbols([string]$rule) {
  [regex]::Matches($rule, '\[([A-Za-z_][A-Za-z0-9_]*)') | ForEach-Object { $_.Groups[1].Value }
}

# ---------- 收集游戏自带 RulePack 符号（用于排除全局符号误报） ----------
$globalSymbols = [System.Collections.Generic.HashSet[string]]::new()
Get-ChildItem -Path $GameData -Recurse -Filter *.xml | Where-Object { $_.FullName -match 'RulePack' } | ForEach-Object {
  try { $doc = [xml](Get-Content $_.FullName -Raw) } catch { return }
  foreach ($pack in $doc.Defs.RulePackDef) {
    foreach ($li in $pack.rulePack.rulesStrings.li) {
      if ($li -match '^\s*([^\(\s]+)') { [void]$globalSymbols.Add($Matches[1]) }
    }
    foreach ($li in $pack.rulePack.rulesFiles.li) {
      if ($li -match '^\s*([^\-]+?)\s*->') { [void]$globalSymbols.Add($Matches[1].Trim()) }
    }
  }
}

# ---------- 命名 RulePack ----------
$packFiles = Get-ChildItem -Path (Join-Path $Repo 'Defs') -Recurse -Filter '*.xml' | Where-Object { $_.Name -match 'RulePack' }
$namePacks = @{}
foreach ($f in $packFiles) {
  $doc = [xml](Get-Content $f.FullName -Raw)
  foreach ($pack in $doc.Defs.RulePackDef) {
    $defined = [System.Collections.Generic.HashSet[string]]::new()
    $files = @{}
    foreach ($li in $pack.rulePack.rulesStrings.li) {
      if ($li -match '^\s*([^\(\s]+)') { [void]$defined.Add($Matches[1]) }
    }
    foreach ($li in $pack.rulePack.rulesFiles.li) {
      if ($li -match '^\s*([^\-]+?)\s*->\s*(.+?)\s*$') {
        $sym = $Matches[1].Trim(); $path = $Matches[2].Trim()
        [void]$defined.Add($sym); $files[$sym] = $path
      }
    }
    $used = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($li in $pack.rulePack.rulesStrings.li) { foreach ($s in Token-Symbols $li) { [void]$used.Add($s) } }
    foreach ($s in $used) {
      if (-not $defined.Contains($s) -and -not $globalSymbols.Contains($s)) {
        $errors.Add("[$($pack.defName)] rulesStrings 引用的符号 [$s] 在 rulesFiles/rulesStrings 中未定义")
      }
    }
    foreach ($sym in $files.Keys) {
      foreach ($lang in Get-ChildItem -Path (Join-Path $Repo 'Languages') -Directory) {
        $p = Join-Path $lang.FullName ("Strings/{0}.txt" -f $files[$sym])
        if (-not (Test-Path -LiteralPath $p)) {
          $notes.Add("[$($pack.defName)] $($lang.Name) 缺少名字文件: Strings/$($files[$sym]).txt")
        }
      }
    }
    $namePacks[$pack.defName] = @{ RootRules = $pack.rulePack.rulesStrings.li.Count; Files = $files }
    $notes.Add("[$($pack.defName)] r_name 规则 $($pack.rulePack.rulesStrings.li.Count) 条; 名字文件映射: " + (($files.Keys | Sort-Object | ForEach-Object { "$_->$($files[$_])" }) -join ', '))
  }
}

# ---------- 种族 Def ----------
$racePath = Join-Path $Repo 'Defs/ThingDefs_Races/ElfRace.xml'
$race = ([xml](Get-Content $racePath -Raw)).Defs.'AlienRace.ThingDef_AlienRace'
$parent = $race.ParentName
$tools = @($race.tools.li)
$genM = $race.race.nameGenerator
$genF = $race.race.nameGeneratorFemale
$notes.Add("Elf ParentName=$parent, tools=$($tools.Count), nameGenerator=$genM/$genF, body=$($race.race.body)")
if ($tools.Count -eq 0) { $errors.Add("Elf 无 <tools>，Pawn.verbTracker.AllVerbs 为空 -> ChooseMeleeVerb 会报 no available melee attack") }
foreach ($g in $genM, $genF) {
  if ($g -and -not $namePacks.ContainsKey($g)) { $errors.Add("race.nameGenerator 引用的 RulePackDef $g 不在本模组 RulePack 中") }
}

$bodyDoc = [xml](Get-Content (Join-Path $Repo 'Defs/Bodies/Bodies_Elf.xml') -Raw)
$groups = [System.Collections.Generic.HashSet[string]]::new()
function Collect-Groups([System.Xml.XmlNode]$node) {
  if ($null -eq $node) { return }
  foreach ($child in $node.ChildNodes) {
    if ($child.Name -eq 'groups') { foreach ($g in $child.ChildNodes) { if ($g.Name -eq 'li') { [void]$groups.Add($g.InnerText.Trim()) } } }
    Collect-Groups $child
  }
}
Collect-Groups $bodyDoc.Defs.BodyDef

# 部位组与近战招式（capacity->ManeuverDef）在原版 Def 中存在，tool 才会生成 Verb_MeleeAttack
$groupDefs = [System.Collections.Generic.HashSet[string]]::new()
foreach ($f in Get-ChildItem -Path (Join-Path $GameData 'Core/Defs/Bodies') -Filter 'BodyPartGroups.xml') {
  foreach ($g in ([xml](Get-Content $f.FullName -Raw)).Defs.BodyPartGroupDef) { [void]$groupDefs.Add([string]$g.defName) }
}
$maneuverCaps = [System.Collections.Generic.HashSet[string]]::new()
foreach ($f in Get-ChildItem -Path $GameData -Recurse -Filter '*.xml' | Where-Object { $_.FullName -match '\\Maneuvers\\' }) {
  foreach ($m in ([xml](Get-Content $f.FullName -Raw)).Defs.ManeuverDef) { [void]$maneuverCaps.Add([string]$m.requiredCapacity) }
}
foreach ($t in $tools) {
  foreach ($c in @($t.capacities.li)) {
    if (-not $maneuverCaps.Contains([string]$c)) { $errors.Add("tool '$($t.label)' 的 capacity=$c 没有对应 ManeuverDef，无法生成近战动词") }
  }
  $g = [string]$t.linkedBodyPartsGroup
  if ($g -and -not $groupDefs.Contains($g)) { $errors.Add("linkedBodyPartsGroup=$g 不是原版 BodyPartGroupDef") }
}
foreach ($t in $tools) {
  $g = [string]$t.linkedBodyPartsGroup
  if ($g -and -not $groups.Contains($g)) { $errors.Add("tool '$($t.label)' 的 linkedBodyPartsGroup=$g 在 Bodies_Elf.xml 中不存在") }
  if ($g) { $notes.Add("tool '$($t.label)': group=$g capacity=$(@($t.capacities.li) -join '/') power=$($t.power)") }
}

$notes | ForEach-Object { "NOTE $_" }
if ($errors.Count -eq 0) { "PASS: 命名符号/名字文件/近战 tools 检查全部通过" } else { $errors | ForEach-Object { "FAIL $_" }; exit 1 }
