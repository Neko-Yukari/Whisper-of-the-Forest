Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/check-ui/proj-real/bin/Debug/net10.0/WotF_UI_Check_Real.dll')
foreach ($n in @('WhisperOfTheForest.Experimental.TechLabEntryOverlay','WhisperOfTheForest.Experimental.TechLabStyle')) {
  $t = $asm.MainModule.Types | Where-Object { $_.FullName -eq $n }
  "== $n"
  "  attrs: " + (($t.CustomAttributes | ForEach-Object { $_.AttributeType.Name }) -join ', ')
  foreach ($m in $t.Methods) { if ($m.IsConstructor) { "  ctor(" + (($m.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ', ') + ")" } }
}
