Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
function DumpT($name) {
  $t = $asm.MainModule.Types | Where-Object { $_.FullName -eq $name }
  "==== $name ===="
  foreach ($m in $t.Methods) {
    "--- $($m.Name)($(($m.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', '))"
    if ($m.HasBody) { foreach ($i in $m.Body.Instructions) { "    $($i)" } }
  }
}
DumpT 'Verse.Window'
