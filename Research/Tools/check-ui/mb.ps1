Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
function DM($type,$names) {
  $t = $asm.MainModule.Types | Where-Object { $_.FullName -eq $type }
  foreach ($m in $t.Methods) { if ($names -contains $m.Name) { "--- $type::$($m.Name)"; if ($m.HasBody) { foreach ($i in $m.Body.Instructions) { "    $($i)" } } } }
}
DM 'RimWorld.MainButtonDef' @('get_Icon','get_Worker','PostLoad')
DM 'RimWorld.MainButtonWorker' @('get_Visible','get_Disabled','get_ButtonBarPercent','Activate','InterfaceTryActivate','DoButton')
