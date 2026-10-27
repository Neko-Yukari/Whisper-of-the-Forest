param(
  [string]$Assembly = 'E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll',
  [string]$TypeName,
  [string]$Filter
)
Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Assembly)
$type = $asm.MainModule.GetType($TypeName)
if (-not $type) { Write-Error "type not found: $TypeName"; exit 1 }
$type.Methods | Where-Object { -not $Filter -or $_.Name -like "*$Filter*" } | ForEach-Object {
  $vis = if ($_.IsPublic) { 'public' } elseif ($_.IsAssembly) { 'internal' } elseif ($_.IsFamily) { 'protected' } else { 'private' }
  $params = ($_.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
  "{0} {1} {2}({3})" -f $vis, $_.ReturnType.Name, $_.Name, $params
}
