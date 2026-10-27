Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
foreach ($n in @('Verse.ModContentPack','Verse.LanguageDatabase','Verse.LoadedLanguage')) {
  $t = $asm.MainModule.Types | Where-Object { $_.FullName -eq $n }
  "== $n"
  foreach ($m in $t.Methods) { if ($m.Name -match 'Language|Folder|AbsPath') { "  $($m.Name)($(($m.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', '))" } }
  foreach ($f in $t.Fields) { if ($f.Name -match 'Language|Folder') { "  field $($f.FieldType.Name) $($f.Name)" } }
}
