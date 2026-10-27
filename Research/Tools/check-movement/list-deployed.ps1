Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest/1.7/Assemblies/WhisperOfTheForest.dll')
foreach ($t in $a.MainModule.Types) { if ($t.Namespace -like '*Movement*') { $t.FullName } }
"--- patches with HarmonyPatch ---"
foreach ($t in $a.MainModule.Types) { $at = $t.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'HarmonyPatch' }; if ($at) { "$($t.FullName)" } }
