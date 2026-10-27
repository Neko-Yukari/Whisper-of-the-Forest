Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.LoadedModManager' }
foreach ($m in $t.Methods) { "--- $($m.Name)"; if ($m.HasBody) { foreach ($i in $m.Body.Instructions) { if ($i.Operand -and $i.Operand.ToString() -match 'Mod::|AllSubclasses|Activator|GetTypes|ModContentPack') { "    $($i)" } } } }
