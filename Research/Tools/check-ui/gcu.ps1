Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.GlobalControlsUtility' }
foreach ($f in $t.Fields) { "field $($f.FieldType.FullName) $($f.Name)" }
foreach ($m in $t.Methods) { "method $($m.Name)($(($m.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', '))" }
"===== DoPlaySettings IL ====="
$m = $t.Methods | Where-Object { $_.Name -eq 'DoPlaySettings' }
foreach ($i in $m.Body.Instructions) { "  $($i)" }
