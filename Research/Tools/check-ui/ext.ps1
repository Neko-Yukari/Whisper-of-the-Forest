Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
foreach ($t in $asm.MainModule.Types) { if ($t.FullName -match 'TranslatorFormattedStringExtensions|SoundDefExtension|SoundStarter') { "== $($t.FullName)"; foreach ($m in $t.Methods) { "  $($m.ReturnType.Name) $($m.Name)($(($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '))" } } }
