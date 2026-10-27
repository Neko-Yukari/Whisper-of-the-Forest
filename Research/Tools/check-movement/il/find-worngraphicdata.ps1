Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
foreach ($t in $asm.MainModule.GetTypes()) {
  foreach ($m in $t.Methods) {
    if (-not $m.HasBody) { continue }
    foreach ($i in $m.Body.Instructions) {
      $o = $i.Operand
      if ($o -is [Mono.Cecil.MemberReference] -and $o.Name -match 'wornGraphicData|GetDirectionalData') {
        "$($t.FullName)::$($m.Name) -> $($o.DeclaringType.Name)::$($o.Name)"
      }
    }
  }
}
