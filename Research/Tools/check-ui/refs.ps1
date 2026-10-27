Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
foreach ($p in @('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll','E:/SteamLibrary/steamapps/common/RimWorld/UnityStubs.dll')) {
  $a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($p)
  "=== $($a.Name.Name) v$($a.Name.Version)"
  foreach ($c in $a.CustomAttributes) { if ($c.AttributeType.Name -match 'TargetFramework|AssemblyFileVersion|AssemblyInformationalVersion') { "  $($c.AttributeType.Name) :: $($c.ConstructorArguments | ForEach-Object { $_.Value })" } }
  foreach ($r in $a.MainModule.AssemblyReferences) { "  ref $($r.Name) $($r.Version)" }
}
$a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/UnityStubs.dll')
"UnityStubs types sample:"; ($a.MainModule.Types | ForEach-Object { $_.FullName } | Where-Object { $_ -match 'Rect|Color|Texture2D|Event$|GUI$' }) -join ', '
