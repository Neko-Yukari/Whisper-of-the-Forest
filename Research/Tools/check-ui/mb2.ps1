Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.MainButtonDef' }
$m = $t.Methods | Where-Object { $_.Name -eq '.cctor' }
$m.Body.Instructions | ForEach-Object { "  $_" }
"=== MainButtonsRoot.DoButtons ==="
$t2 = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.MainButtonsRoot' }
$m2 = $t2.Methods | Where-Object { $_.Name -eq 'DoButtons' }
$m2.Body.Instructions | ForEach-Object { "  $_" } | Select-Object -First 70
