Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.DefInjectionPackage' }
foreach ($m in $t.Methods) {
  if (-not $m.HasBody) { continue }
  $hits = @()
  foreach ($i in $m.Body.Instructions) { if ($i.Operand -and $i.Operand.ToString() -match 'loadErrors') { $hits += $i.Operand.ToString() } }
  if ($hits.Count -gt 0) { "$($m.Name): $($hits -join '; ')" }
}
"=== LoadedLanguage: which methods touch loadErrors"
$t2 = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.LoadedLanguage' }
foreach ($m in $t2.Methods) {
  if (-not $m.HasBody) { continue }
  $n = 0
  foreach ($i in $m.Body.Instructions) { if ($i.Operand -and $i.Operand.ToString() -match 'loadErrors') { $n++ } }
  if ($n -gt 0) { "$($m.Name): $n refs" }
}
