Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
foreach ($n in @('RimWorld.SoundDefOf','RimWorld.MessageTypeDefOf')) {
  $t = $asm.MainModule.Types | Where-Object { $_.FullName -eq $n }
  "$n fields:"; ($t.Fields | ForEach-Object { $_.Name }) -join ', '
}
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.GenUI' }
"GenUI.Rounded: " + (($t.Methods | Where-Object { $_.Name -eq 'Rounded' }).Count)
