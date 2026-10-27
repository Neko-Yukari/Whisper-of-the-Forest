Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.Window' }
foreach ($m in $t.Methods) { if ($m.IsConstructor) { foreach ($p in $m.Parameters) { "ctor param $($p.Name) type=$($p.ParameterType.Name) hasDefault=$($p.HasDefault) const=$($p.Constant)" } } }
"--- some vanilla Window subclass ctor"
$t2 = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.Dialog_ModSettings' }
foreach ($m in $t2.Methods) { if ($m.IsConstructor) { foreach ($i in $m.Body.Instructions) { "  $($i)" } } }
