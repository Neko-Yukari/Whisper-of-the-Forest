Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'Verse.WidgetRow' }
foreach ($f in $t.Fields) { "field $($f.FieldType.Name) $($f.Name)" }
foreach ($m in $t.Methods) { if ($m.Name -in @('Init','.ctor','IncrementY','IncrementYIfWillExceedMaxWidth','IncrementPosition','get_FinalX','get_FinalY','ToggleableIcon','ButtonIcon','ButtonIconRect')) { "--- $($m.Name)($(($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '))"; foreach ($i in $m.Body.Instructions) { "    $($i)" } } }
