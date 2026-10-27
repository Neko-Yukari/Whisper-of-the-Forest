Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -like 'Verse.LoadedLanguage/<get_AllDirectories>*' }
foreach ($m in $t) { "== $($m.FullName)"; foreach ($mm in $m.Methods) { if ($mm.Name -eq 'MoveNext') { $mm.Body.Instructions | ForEach-Object { "  $_" } | Select-Object -First 80 } } }
