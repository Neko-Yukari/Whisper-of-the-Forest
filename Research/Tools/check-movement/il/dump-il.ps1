param(
  [string]$Assembly = 'E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll',
  [string]$TypeName,
  [string]$MethodName,
  [string]$OutFile
)

Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'

$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Assembly)
$type = $asm.MainModule.GetType($TypeName)
if (-not $type) { Write-Error "type not found: $TypeName"; exit 1 }

$methods = $type.Methods
if ($MethodName) { $methods = $methods | Where-Object { $_.Name -eq $MethodName } }

$sb = New-Object System.Text.StringBuilder
foreach ($m in $methods) {
  [void]$sb.AppendLine("=== $TypeName::$($m.Name) ($($m.ReturnType.FullName))")
  foreach ($p in $m.Parameters) { [void]$sb.AppendLine("  param $($p.ParameterType.FullName) $($p.Name)") }
  if ($m.HasBody) {
    foreach ($v in $m.Body.Variables) { [void]$sb.AppendLine("  local [$($v.Index)] $($v.VariableType.FullName)") }
    foreach ($i in $m.Body.Instructions) {
      [void]$sb.AppendLine(("  IL_{0:x4}: {1} {2}" -f $i.Offset, $i.OpCode, $i.Operand))
    }
    foreach ($h in $m.Body.ExceptionHandlers) {
      [void]$sb.AppendLine("  EH $($h.HandlerType) try $($h.TryStart.Offset)-$($h.TryEnd.Offset) handler $($h.HandlerStart.Offset)-$($h.HandlerEnd.Offset)")
    }
  }
  [void]$sb.AppendLine("")
}

if ($OutFile) { $sb.ToString() | Set-Content -LiteralPath $OutFile -Encoding utf8; Write-Output "wrote $OutFile" }
else { $sb.ToString() }
