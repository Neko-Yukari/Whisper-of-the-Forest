Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.GlobalControlsUtility' }
foreach ($m in $t.Methods) {
  if ($m.Name -eq 'DoPlaySettings') {
    $vis = if ($m.IsPublic) {'public'} elseif ($m.IsAssembly) {'internal'} elseif ($m.IsPrivate) {'private'} else {'other'}
    "$vis static=$($m.IsStatic) $($m.Name)($(($m.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', '))"
    $m.Body.Instructions | ForEach-Object { "   $_" }
  }
}
"=== WidgetRow.ButtonIconRect"
$w = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.WidgetRow' }
foreach ($m in $w.Methods) { if ($m.Name -eq 'ButtonIconRect') { $m.Body.Instructions | ForEach-Object { "   $_" } } }
"=== WidgetRow.ButtonIcon tail (return)"
foreach ($m in $w.Methods) { if ($m.Name -eq 'ButtonIcon') { $m.Body.Instructions | Select-Object -Last 25 | ForEach-Object { "   $_" } } }
