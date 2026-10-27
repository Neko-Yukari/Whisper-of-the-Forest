# 只读校验 Languages/*/DefInjected 是否与真实游戏元数据和本模组 Def 一致。
# 校验依据：
#   * 目录名必须是 Def 类型名（Verse.LoadedLanguage.LoadData 用 GenTypes.GetTypeInAnyAssembly(dirName) 解析，
#     失败即 "dir ... doesn't correspond to any def type. Skipping..."，计 1 条语言加载错误）。
#   * 列表用 0 基下标（原版 Data/Core/Languages/English/DefInjected/TraitDef/Example_Traits.xml 写明 listname.n 且示例为 .0.）。
#   * DefInjectionPackage.SetDefFieldAtPath：index < list.Count，否则 "at index N but the list only has M entries"。
# 用法: pwsh -NoProfile -File ./check_injections.ps1
param(
  [string]$Repo = 'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest',
  [string]$GameRoot = 'E:/SteamLibrary/steamapps/common/RimWorld'
)

Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'

$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameRoot 'Assembly-CSharp.dll'))
$typeByName = @{}
function Index-Types($types) {
  foreach ($t in $types) {
    if (-not $typeByName.ContainsKey($t.Name)) { $typeByName[$t.Name] = @() }
    $typeByName[$t.Name] += $t
    if ($t.HasNestedTypes) { Index-Types $t.NestedTypes }
  }
}
Index-Types $asm.MainModule.Types

function Get-TypeDef([string]$name) {
  if (-not $typeByName.ContainsKey($name)) { return $null }
  $c = @($typeByName[$name])
  $pref = $c | Where-Object { $_.Namespace -eq 'RimWorld' -or $_.Namespace -eq 'Verse' } | Select-Object -First 1
  if ($pref) { return $pref }
  return $c[0]
}

function Resolve-Member($typeDef, [string]$name) {
  $t = $typeDef
  while ($t) {
    $f = $t.Fields | Where-Object { $_.Name -eq $name } | Select-Object -First 1
    if ($f) { return @{ Kind = 'field'; Type = $f.FieldType } }
    $p = $t.Properties | Where-Object { $_.Name -eq $name } | Select-Object -First 1
    if ($p) { return @{ Kind = 'property'; Type = $p.PropertyType } }
    try { $t = $t.BaseType.Resolve() } catch { $t = $null }
  }
  return $null
}

function Get-ListElementType($typeRef) {
  if ($null -eq $typeRef) { return $null }
  if ($typeRef -is [Mono.Cecil.GenericInstanceType] -and $typeRef.ElementType.FullName -eq 'System.Collections.Generic.List`1') {
    return $typeRef.GenericArguments[0]
  }
  return $null
}

# 本模组 Def 索引
$defs = @{}
foreach ($f in Get-ChildItem -Path (Join-Path $Repo 'Defs') -Recurse -Filter '*.xml') {
  $doc = [xml](Get-Content $f.FullName -Raw)
  foreach ($d in $doc.Defs.ChildNodes) {
    if ($d.NodeType -ne 'Element') { continue }
    $dn = $d.SelectSingleNode('defName')
    if ($dn) { $defs[[string]$dn.InnerText] = @{ Node = $d; TypeName = $d.Name } }
  }
}

$errors = [System.Collections.Generic.List[string]]::new()
$notes = [System.Collections.Generic.List[string]]::new()
$checked = 0

foreach ($langDir in Get-ChildItem -Path (Join-Path $Repo 'Languages') -Directory) {
  $injRoot = Join-Path $langDir.FullName 'DefInjected'
  if (-not (Test-Path $injRoot)) { continue }
  foreach ($typeDir in Get-ChildItem -Path $injRoot -Directory) {
    $typeName = $typeDir.Name
    $typeDef = Get-TypeDef $typeName
    if (-not $typeDef) {
      $errors.Add("[$($langDir.Name)] 目录 $typeName 不是任何 Def 类型名（游戏会跳过并记 1 条语言加载错误）")
      continue
    }
    foreach ($file in Get-ChildItem -Path $typeDir.FullName -Filter '*.xml') {
      $doc = [xml](Get-Content $file.FullName -Raw)
      foreach ($node in $doc.LanguageData.ChildNodes) {
        if ($node.NodeType -ne 'Element') { continue }
        $checked++
        $key = $node.Name
        $parts = $key.Split('.')
        if ($parts.Count -lt 2) { $notes.Add("整 def 注入，跳过路径校验: $key"); continue }
        $defName = $parts[0]
        if (-not $defs.ContainsKey($defName)) { $errors.Add("${key}: Def '$defName' 在本模组 Defs 中不存在"); continue }
        $def = $defs[$defName]
        $defType = Get-TypeDef $def.TypeName
        $xmlNode = $def.Node
        $curType = $defType
        $pendingListElem = $null
        $segments = $parts[1..($parts.Count - 1)]
        $pathSoFar = $defName
        $ok = $true
        for ($i = 0; $i -lt $segments.Count; $i++) {
          $seg = $segments[$i]
          if ($seg -match '^\d+$') {
            $idx = [int]$seg
            $items = @($xmlNode.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.Name -eq 'li' })
            if ($idx -ge $items.Count) {
              $errors.Add("${key}: 列表 '$pathSoFar' 只有 $($items.Count) 项（0 基，最大下标 $($items.Count-1)），却使用下标 $idx")
              $ok = $false; break
            }
            $xmlNode = $items[$idx]
            if ($pendingListElem) { $curType = Get-TypeDef $pendingListElem.Name; $pendingListElem = $null }
            $pathSoFar += ".$seg"
            continue
          }
          $member = if ($curType) { Resolve-Member $curType $seg } else { $null }
          if ($curType -and -not $member) {
            $errors.Add("${key}: 类型 $($curType.FullName) 不存在字段/属性 '$seg'")
            $ok = $false; break
          }
          $child = $xmlNode.SelectSingleNode($seg)
          if (-not $child) {
            $errors.Add("${key}: Def XML 中 '$pathSoFar' 下没有元素 '$seg'")
            $ok = $false; break
          }
          $xmlNode = $child
          $pendingListElem = $null
          if ($member) {
            $elem = Get-ListElementType $member.Type
            if ($elem) { $pendingListElem = $elem; $curType = $null }
            else { $curType = Get-TypeDef $member.Type.Name }
          } else { $curType = $null }
          $pathSoFar += ".$seg"
        }
        if ($ok -and [string]::IsNullOrWhiteSpace($node.InnerText)) {
          $errors.Add("${key}: 翻译内容为空")
        }
        if ($ok) { $notes.Add("OK $($langDir.Name): $key") }
      }
    }
  }
}

$notes | ForEach-Object { "NOTE $_" }
"checked $checked injection keys"
if ($errors.Count -eq 0) { "PASS: 目录类型/Def/字段/列表下标检查全部通过" } else { $errors | ForEach-Object { "FAIL $_" }; exit 1 }
