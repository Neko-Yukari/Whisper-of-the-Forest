param(
  [Parameter(Mandatory=$true)][string]$Assembly,
  [Parameter(Mandatory=$true)][string]$Type,
  [string]$Method = '',
  [string]$Out = ''
)

Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'

$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Assembly)
$sb = [System.Text.StringBuilder]::new()

function Walk($types) {
  foreach ($t in $types) {
    if ($t.FullName -eq $Type -or $t.FullName -like $Type) {
      foreach ($m in $t.Methods) {
        if ($Method -ne '' -and $m.Name -ne $Method) { continue }
        [void]$sb.AppendLine("=== $($t.FullName)::$($m.Name) ===")
        if (-not $m.HasBody) { [void]$sb.AppendLine('  (no body)'); continue }
        foreach ($i in $m.Body.Instructions) {
          $op = $i.Operand
          $text = if ($null -eq $op) { '' } elseif ($op -is [Mono.Cecil.Cil.Instruction]) { "IL_$($op.Offset.ToString('x4'))" } else { $op.ToString() }
          [void]$sb.AppendLine("  IL_$($i.Offset.ToString('x4')): $($i.OpCode) $text")
        }
        if ($m.Body.HasExceptionHandlers) {
          foreach ($h in $m.Body.ExceptionHandlers) {
            [void]$sb.AppendLine("  EH $($h.HandlerType) try IL_$($h.TryStart.Offset.ToString('x4'))-IL_$($h.TryEnd.Offset.ToString('x4'))")
          }
        }
        [void]$sb.AppendLine('')
      }
    }
    if ($t.HasNestedTypes) { Walk $t.NestedTypes }
  }
}

Walk $asm.MainModule.Types

if ($Out -ne '') {
  [System.IO.File]::WriteAllText($Out, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
  "wrote $Out ($($sb.Length) chars)"
} else {
  $sb.ToString()
}
