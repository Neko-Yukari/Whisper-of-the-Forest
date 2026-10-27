# Diagnostic only: find English DefInjected nodes that RimWorld cannot inject.
# RimWorld node syntax is <defName>.<fieldPath>; a leading Def type name is NOT stripped
# (DefInjectionPackage.SetDefFieldAtPath uses path[0] as the def name).
$roots = @(
  'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest',
  'E:/SteamLibrary/steamapps/workshop/content/294100/839005762',
  'E:/SteamLibrary/steamapps/workshop/content/294100/3819570122',
  'E:/SteamLibrary/steamapps/workshop/content/294100/2009463077'
)
$defTypeLike = @('AbilityDef','ThingDef','DamageDef','RecipeDef','HediffDef','PawnKindDef','JobDef','TraitDef','ThoughtDef','StatDef','ResearchProjectDef','TerrainDef','FactionDef','BodyDef','BackstoryDef','GeneDef','XenotypeDef','MainButtonDef','WeatherDef','SoundDef','EffecterDef','IncidentDef','QuestScriptDef','PawnRelationDef','MentalStateDef','DesignationDef','LifeStageDef','TattooDef','HairDef','BeardDef','SkillDef','WorkTypeDef','WorkGiverDef','RulePackDef','LetterDef','StorytellerDef','DifficultyDef','RitualDef','RitualPatternDef','PreceptDef','IdeoIconDef','MemeDef','IssueDef','AbilityDef','MoteDef','FleckDef','TaleDef','InteractionDef','TrainableDef','NeedDef','RecordDef','StatCategoryDef','BodyPartDef','BodyPartGroupDef','ChemicalDef','DrugPolicyDef','AreaDef','ZoneDef','RoofDef','MemeDef','ArchitectCategoryDef','DesignationCategoryDef','KeyBindingDef','MainTabDef','TimeAssignmentDef','JoyKindDef','JoyGiverDef','ThinkTreeDef','PawnGroupMakerDef','SitePartDef','WorldObjectDef','TileMutatorDef','RoomRoleDef','RoomStatDef','GeneCategoryDef','GeneTemplateDef','AbilityGroupDef')
$hits = New-Object System.Collections.Generic.List[string]
foreach ($root in $roots) {
  if (-not (Test-Path $root)) { continue }
  $files = Get-ChildItem $root -Recurse -File -Filter '*.xml' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -replace '\\','/' -match '/Languages/English/DefInjected/' }
  foreach ($f in $files) {
    try { [xml]$x = Get-Content -Raw -Encoding UTF8 $f.FullName } catch { $hits.Add("PARSE-ERROR $($f.FullName)"); continue }
    foreach ($n in $x.LanguageData.ChildNodes) {
      if ($n.NodeType -ne 'Element') { continue }
      $name = $n.Name
      $segments = $name.Split('.')
      if ($segments.Count -ge 3) { $hits.Add("TWO+DOTS  $($f.Name) :: $name") }
      elseif ($defTypeLike -contains $segments[0]) { $hits.Add("TYPE-PREFIX $($f.Name) :: $name") }
    }
  }
}

"possible injection errors: $($hits.Count)"
$hits | ForEach-Object { "  $_" }
