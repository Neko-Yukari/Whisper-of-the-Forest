Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')

foreach ($typeName in @('RimWorld.Pawn_ApparelTracker', 'RimWorld.Apparel', 'Verse.ThingWithComps')) {
  $type = $asm.MainModule.GetType($typeName)
  if (-not $type) { "MISSING $typeName"; continue }
  foreach ($method in $type.Methods) {
    if (-not $method.HasBody) { continue }
    foreach ($instr in $method.Body.Instructions) {
      $operand = $instr.Operand
      if ($operand -is [Mono.Cecil.MethodReference] -and $operand.Name -match 'Notify_Equipped|Notify_Unequipped') {
        "$typeName::$($method.Name) calls $($operand.DeclaringType.Name)::$($operand.Name)"
      }
    }
  }
}
