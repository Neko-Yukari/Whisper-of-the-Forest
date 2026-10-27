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
        if ($i.OpCode.Code -eq 'Ldstr' -and $i.Operand -match 'Word wrap was false') { "HIT $($ct.FullName)::$($m.Name)"; foreach ($j in $m.Body.Instructions) { "   $j" } }
      }
    }
  }
}
