Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
function Fmt-M($m) {
  $st = if ($m.IsStatic) { 'static ' } else { '' }
  $ps = ($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
  "  $st$($m.ReturnType.Name) $($m.Name)($ps)"
}
foreach ($n in @('Verse.Widgets','Verse.Text','Verse.Scribe_Values','RimWorld.Selector','Verse.UI','Verse.MouseoverSounds')) {
  $t = $asm.MainModule.Types | Where-Object { $_.FullName -eq $n }
  "==== $n ===="
  foreach ($f in $t.Fields) { "  field $($f.FieldType.Name) $($f.Name)" }
  foreach ($p in $t.Properties) { "  prop $($p.PropertyType.Name) $($p.Name)" }
  foreach ($m in $t.Methods) { Fmt-M $m }
}
