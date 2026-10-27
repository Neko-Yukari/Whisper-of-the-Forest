Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.MainButtonDef' }
foreach ($m in $t.Methods) { if ($m.Name -in @('get_TabWindow','Notify_ClearingAllMapsMemory','Notify_SwitchedMap','get_LabelCapWidth','get_ShortenedLabelCap')) { "--- $($m.Name)"; $m.Body.Instructions | ForEach-Object { "    $_" } } }
