Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.LoadedLanguage' }
foreach ($m in $t.Methods) { if ($m.Name -eq 'InjectIntoData_AfterImpliedDefs') { $m.Body.Instructions | ForEach-Object { "  $_" } } }
"=== DefInjectionPackage fields"
$t2 = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.DefInjectionPackage' }
foreach ($f in $t2.Fields) { "  field $($f.FieldType.Name) $($f.Name)" }
foreach ($m in $t2.Methods) { if ($m.Name -match 'AddError|Error|InjectInto') { "  m $($m.Name)($(($m.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', '))" } }
