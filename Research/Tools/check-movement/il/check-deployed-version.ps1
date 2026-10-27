Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$d = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest/1.7/Assemblies/WhisperOfTheForest.dll')
$t = $d.MainModule.GetType('WhisperOfTheForest.Experimental.Movement.StrideDiagnostics')
foreach ($m in $t.Methods) { if ($m.Name -eq 'NoteWalked' -or $m.Name -match 'LogFailure') { $m.Name + ' params=' + $m.Parameters.Count } }
