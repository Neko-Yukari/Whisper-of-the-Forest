Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$w = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.Widgets' }
foreach ($m in $w.Methods) {
  if ($m.Name -eq 'ButtonImage' -and $m.Parameters.Count -eq 6) {
    "--- ButtonImage(6)"
    $m.Body.Instructions | ForEach-Object { "   $_" }
  }
}
