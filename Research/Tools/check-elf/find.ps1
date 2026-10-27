param(
  [Parameter(Mandatory=$true)][string]$Assembly,
  [Parameter(Mandatory=$true)][string]$Pattern
)

Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'

$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Assembly)
$rx = [regex]$Pattern
$sb = [System.Text.StringBuilder]::new()

function Walk($types, $prefix) {
  foreach ($t in $types) {
    $full = if ($prefix -ne '') { "$prefix/$($t.Name)" } else { $t.FullName }
    if ($rx.IsMatch($t.FullName)) {
      [void]$sb.AppendLine("TYPE $($t.FullName)")
      foreach ($m in $t.Methods) { [void]$sb.AppendLine("  M $($m.Name)($(($m.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', '))") }
      foreach ($f in $t.Fields) { [void]$sb.AppendLine("  F $($f.FieldType.Name) $($f.Name)") }
    }
    if ($t.HasNestedTypes) { Walk $t.NestedTypes $t.FullName }
  }
}

Walk $asm.MainModule.Types ''
$sb.ToString()
