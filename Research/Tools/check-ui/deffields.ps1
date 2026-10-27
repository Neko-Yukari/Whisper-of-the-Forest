Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.Def' }
foreach ($f in $t.Fields) { if ($f.Name -in @('label','description','defName')) { "Def.$($f.Name) : $($f.FieldType.FullName)" } }
$mb = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.MainButtonDef' }
"MainButtonDef base: $($mb.BaseType.FullName)"
