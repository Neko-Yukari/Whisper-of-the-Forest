# Diagnostic only: look for Simplified Chinese data that RimWorld would count as translation load errors.
# Covers: DefInjected node syntax (leading Def-type prefix / extra dots) and duplicate Keyed keys.
param([string]$Lang = 'ChineseSimplified')

$roots = @(
  'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest',
  'E:/SteamLibrary/steamapps/workshop/content/294100/839005762',
  'E:/SteamLibrary/steamapps/workshop/content/294100/3819570122',
  'E:/SteamLibrary/steamapps/workshop/content/294100/2009463077'
)
$defTypeLike = @('AbilityDef','ThingDef','DamageDef','RecipeDef','HediffDef','PawnKindDef','JobDef','TraitDef','ThoughtDef','StatDef','ResearchProjectDef','TerrainDef','FactionDef','BodyDef','BackstoryDef','GeneDef','XenotypeDef','MainButtonDef','WeatherDef','SoundDef','EffecterDef','IncidentDef','QuestScriptDef','PawnRelationDef','MentalStateDef','DesignationDef','LifeStageDef','TattooDef','HairDef','BeardDef','SkillDef','WorkTypeDef','WorkGiverDef','RulePackDef','LetterDef','StorytellerDef','DifficultyDef','RitualDef','PreceptDef','MemeDef','MoteDef','FleckDef','TaleDef','InteractionDef','TrainableDef','NeedDef','RecordDef','BodyPartDef','BodyPartGroupDef','ChemicalDef','ArchitectCategoryDef','KeyBindingDef','TimeAssignmentDef','JoyKindDef','ThinkTreeDef','WorldObjectDef','RoomRoleDef','RoomStatDef')

$hits = New-Object System.Collections.Generic.List[string]
$keyed = @{}
foreach ($root in $roots) {
  if (-not (Test-Path $root)) { continue }
  $files = Get-ChildItem $root -Recurse -File -Filter '*.xml' -ErrorAction SilentlyContinue |
    Where-Object { ($_.FullName -replace '\\','/') -match "/Languages/$Lang/" }
  foreach ($f in $files) {
    $rel = $f.FullName.Replace($root, '').Replace('\','/')
    try { [xml]$x = Get-Content -Raw -Encoding UTF8 $f.FullName } catch { $hits.Add("PARSE-ERROR $rel"); continue }
    if ($rel -match '/DefInjected/') {
      foreach ($n in $x.LanguageData.ChildNodes) {
        if ($n.NodeType -ne 'Element') { continue }
        $seg = $n.Name.Split('.')
        if ($seg.Count -ge 3) { $hits.Add("TWO+DOTS  $rel :: $($n.Name)") }
        elseif ($defTypeLike -contains $seg[0]) { $hits.Add("TYPE-PREFIX $rel :: $($n.Name)") }
      }
    }
    elseif ($rel -match '/Keyed/') {
      foreach ($n in $x.LanguageData.ChildNodes) {
        if ($n.NodeType -ne 'Element') { continue }
        if ($keyed.ContainsKey($n.Name)) { $hits.Add("DUP-KEY $($n.Name) in $rel (also $($keyed[$n.Name]))") }
        else { $keyed[$n.Name] = $rel }
      }
    }
  }
}

"[${Lang}] keyed keys: $($keyed.Count), suspicious injection/dup hits: $($hits.Count)"
$hits | ForEach-Object { "  $_" }
