Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$w = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.Widgets' }
foreach ($m in $w.Methods) { if ($m.Name -eq 'ButtonInvisible' -and $m.Parameters.Count -eq 2) { "--- ButtonInvisible"; $m.Body.Instructions | ForEach-Object { "   $_" } } }
"=== MapInterface.HandleMapClicks (first 40)"
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.MapInterface' }
$m2 = $t.Methods | Where-Object { $_.Name -eq 'HandleMapClicks' }
$m2.Body.Instructions | Select-Object -First 40 | ForEach-Object { "   $_" }
