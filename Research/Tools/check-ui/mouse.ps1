Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.Mouse' }
foreach ($m in $t.Methods) { if ($m.Name -in @('IsOver','get_Position')) { "--- Mouse.$($m.Name)"; $m.Body.Instructions | ForEach-Object { "   $_" } } }
$t2 = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.UI' }
foreach ($m in $t2.Methods) { if ($m.Name -eq 'get_MousePositionOnUI') { "--- UI.$($m.Name)"; $m.Body.Instructions | ForEach-Object { "   $_" } } }
$t3 = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.WindowStack' }
foreach ($m in $t3.Methods) { if ($m.Name -eq 'GetWindowAt') { "--- WindowStack.GetWindowAt"; $m.Body.Instructions | ForEach-Object { "   $_" } } }
