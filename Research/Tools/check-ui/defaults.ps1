Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.Widgets' }
foreach ($m in $t.Methods) {
  if ($m.Name -in @('ButtonText','CheckboxLabeled','Label','DrawBoxSolidWithOutline','DrawBoxSolid','DrawRectFast','ButtonImage','DrawTextureFitted','LabelEllipses','ButtonInvisible')) {
    $ps = ($m.Parameters | ForEach-Object { $p=$_; $d = if ($p.HasConstant) { "= $($p.Constant)" } else { '' }; "$($p.ParameterType.Name) $($p.Name)$d" }) -join ', '
    "  $($m.ReturnType.Name) $($m.Name)($ps)"
  }
}
