Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -match 'StaticConstructorOnStartup' }
foreach ($x in $t) { "== $($x.FullName)"; foreach ($m in $x.Methods) { "  $($m.Name)"; if ($m.Name -match 'Check|Warn|All|ctor') { $m.Body.Instructions | ForEach-Object { "     $_" } } } }
