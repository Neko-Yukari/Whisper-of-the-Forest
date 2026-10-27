Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.Game' }
foreach ($m in $t.Methods) { if ($m.Name -eq 'GetComponent' -and $m.Parameters.Count -eq 1) { "--- GetComponent(Type)"; $m.Body.Instructions | ForEach-Object { "  $_" } } }
"=== FillComponents"
foreach ($m in $t.Methods) { if ($m.Name -eq 'FillComponents') { $m.Body.Instructions | ForEach-Object { "  $_" } } }
"=== GameComponent ctor"
$gc = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.GameComponent' }
foreach ($m in $gc.Methods) { if ($m.IsConstructor) { "--- GameComponent ctor params: $(($m.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', ')"; $m.Body.Instructions | ForEach-Object { "  $_" } } }
