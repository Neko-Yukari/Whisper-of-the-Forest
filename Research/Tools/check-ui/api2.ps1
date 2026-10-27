Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')
function TF($n) { $t = $asm.MainModule.Types | Where-Object { $_.FullName -eq $n }; "== $n"; foreach ($f in $t.Fields) { $s = if ($f.IsStatic) {'static '} else {''}; "  field $s$($f.FieldType.Name) $($f.Name)" } }
TF 'RimWorld.TimeControls'
TF 'RimWorld.GlobalControls'
TF 'RimWorld.MainButtonDef'
'== TimeButSize value? find stsfld'
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.TimeControls' }
$m = $t.Methods | Where-Object { $_.Name -eq '.cctor' }
if ($m) { $m.Body.Instructions | ForEach-Object { "  $_" } }
'== Listing_Standard methods'
$t = $asm.MainModule.Types | Where-Object { $_.FullName -eq 'RimWorld.Listing_Standard' }
foreach ($m in $t.Methods) { if ($m.Name -in @('Begin','End','Label','CheckboxLabeled','ButtonText','Gap','GetRect','.ctor')) { "  $($m.ReturnType.Name) $($m.Name)($(($m.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '))" } }
