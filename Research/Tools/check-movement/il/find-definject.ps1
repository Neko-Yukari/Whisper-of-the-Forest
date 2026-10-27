Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
foreach ($t in $asm.MainModule.GetTypes()) {
  foreach ($m in $t.Methods) {
    if ($m.Name -match 'SetDefFieldAtPath|DefInjectionPackage') { "$($t.FullName)::$($m.Name)" }
  }
  if ($t.Name -match 'DefInjection') { "TYPE $($t.FullName)" }
}
