Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.Game' }
foreach ($m in $t.Methods) {
  if (-not $m.HasBody) { continue }
  foreach ($i in $m.Body.Instructions) {
    if ($i.Operand -and $i.Operand.ToString() -match 'AllSubclasses|FillComponents|CreateComponent|GameComponent') { "$($m.Name): $($i)" }
  }
}
