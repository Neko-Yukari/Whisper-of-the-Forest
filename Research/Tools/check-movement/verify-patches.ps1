Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'

$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$pather = $game.MainModule.GetType('Verse.AI.Pawn_PathFollower')

"--- patch target: GetPawnCellBaseCostOverride ---"
foreach ($m in $pather.Methods) {
  if ($m.Name -eq 'GetPawnCellBaseCostOverride') {
    "static=$($m.IsStatic) public=$($m.IsPublic) ret=$($m.ReturnType.Name) params=$(($m.Parameters | ForEach-Object { $_.Name + ':' + $_.ParameterType.Name }) -join ',')"
  }
}

"--- patch target: TryEnterNextPathCell ---"
foreach ($m in $pather.Methods) {
  if ($m.Name -eq 'TryEnterNextPathCell') {
    "static=$($m.IsStatic) public=$($m.IsPublic) params=$($m.Parameters.Count) hasBody=$($m.HasBody)"
  }
}
foreach ($m in $pather.Methods) {
  if ($m.Name -eq 'SetupMoveIntoNextCell') {
    "SetupMoveIntoNextCell: static=$($m.IsStatic) public=$($m.IsPublic) params=$($m.Parameters.Count)"
  }
}

"--- fields used ---"
foreach ($f in $pather.Fields) {
  if ($f.Name -in @('pawn','nextCell','curPath','nextCellCostLeft','nextCellCostTotal')) {
    "$($f.Name): public=$($f.IsPublic) type=$($f.FieldType.Name)"
  }
}

"--- built check assembly: Harmony patches ---"
$check = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/check-movement/bin/Debug/net10.0/check-movement.dll')
foreach ($t in $check.MainModule.Types) {
  $attrs = $t.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'HarmonyPatch' }
  if ($attrs) {
    $args = ($attrs | ForEach-Object { ($_.ConstructorArguments | ForEach-Object { "$($_.Value)" }) -join ',' }) -join ' | '
    "PATCH TYPE $($t.FullName) -> $args"
  }
  foreach ($m in $t.Methods) {
    $ma = $m.CustomAttributes | Where-Object { $_.AttributeType.Name -in @('HarmonyPrefix','HarmonyPostfix') }
    if ($ma) { "  $($t.Name).$($m.Name): $(($ma | ForEach-Object { $_.AttributeType.Name }) -join ',') params=$(($m.Parameters | ForEach-Object { $_.Name }) -join ',')" }
  }
}
