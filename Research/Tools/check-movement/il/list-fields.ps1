param(
  [string]$Assembly = 'E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll',
  [string]$TypeName
)
Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Assembly)
$type = $asm.MainModule.GetType($TypeName)
if (-not $type) { Write-Error "type not found: $TypeName"; exit 1 }
$type.Fields | ForEach-Object {
  $vis = if ($_.IsPublic) { 'public' } elseif ($_.IsAssembly) { 'internal' } elseif ($_.IsFamily) { 'protected' } else { 'private' }
  "{0} {1} {2}" -f $vis, $_.FieldType.Name, $_.Name
}
"--- properties ---"
$type.Properties | ForEach-Object { "{0} {1}" -f $_.PropertyType.Name, $_.Name }
