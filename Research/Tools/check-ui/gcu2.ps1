Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
foreach ($t in $asm.MainModule.Types) {
  $stack = [System.Collections.Generic.Stack[Mono.Cecil.TypeDefinition]]::new(); $stack.Push($t)
  while ($stack.Count -gt 0) {
    $ct = $stack.Pop()
    if ($ct.HasNestedTypes) { foreach ($n in $ct.NestedTypes) { $stack.Push($n) } }
    foreach ($m in $ct.Methods) {
      if (-not $m.HasBody) { continue }
      foreach ($i in $m.Body.Instructions) {
        if ($i.Operand -and $i.Operand.ToString() -match 'GlobalControlsUtility::DoPlaySettings') {
          "CALLER $($ct.FullName)::$($m.Name)"
        }
      }
    }
  }
}
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.WidgetRow' }
foreach ($m in $t.Methods) { "row $($m.Name)($(($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', ')) : $($m.ReturnType.Name)" }
