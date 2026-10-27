Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.Translator' }
foreach ($m in $t.Methods) { "  $($m.ReturnType.Name) $($m.Name)($(($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '))" }
"=== Thing/Pawn label props"
foreach ($n in @('Verse.Thing','Verse.Pawn')) {
  $tt = $asm.MainModule.Types | Where-Object { $_.FullName -eq $n }
  "== $n"
  foreach ($p in $tt.Properties) { if ($p.Name -match 'Label|Name') { "  prop $($p.PropertyType.Name) $($p.Name)" } }
  foreach ($f in $tt.Fields) { if ($f.Name -match 'Label|Name') { "  field $($f.FieldType.Name) $($f.Name)" } }
}
