Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
"### callers of GameComponentOnGUI / MapComponentOnGUI"
foreach ($t in $asm.MainModule.Types) {
  $stack = [System.Collections.Generic.Stack[Mono.Cecil.TypeDefinition]]::new(); $stack.Push($t)
  while ($stack.Count -gt 0) {
    $ct = $stack.Pop()
    if ($ct.HasNestedTypes) { foreach ($n in $ct.NestedTypes) { $stack.Push($n) } }
    foreach ($m in $ct.Methods) {
      if (-not $m.HasBody) { continue }
      $ops = @()
      foreach ($i in $m.Body.Instructions) { if ($i.Operand) { $ops += $i.Operand.ToString() } }
      $joined = $ops -join "`n"
      if ($joined -match 'GameComponentOnGUI|MapComponentOnGUI') { "CALLER $($ct.FullName)::$($m.Name)" }
    }
  }
}
"### draggable refs"
foreach ($t in $asm.MainModule.Types) {
  $stack = [System.Collections.Generic.Stack[Mono.Cecil.TypeDefinition]]::new(); $stack.Push($t)
  while ($stack.Count -gt 0) {
    $ct = $stack.Pop()
    if ($ct.HasNestedTypes) { foreach ($n in $ct.NestedTypes) { $stack.Push($n) } }
    foreach ($m in $ct.Methods) {
      if (-not $m.HasBody) { continue }
      foreach ($i in $m.Body.Instructions) {
        if ($i.Operand -and $i.Operand.ToString() -eq 'System.Boolean Verse.Window::draggable') { "DRAG $($ct.FullName)::$($m.Name)" }
      }
    }
  }
}
