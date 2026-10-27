Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/check-movement/bin/Debug/net10.0/check-integration-movement.dll')
$a.MainModule.Types | Where-Object { $_.Namespace -like '*Movement*' -or $_.Name -like '*Patch*' } | ForEach-Object { $_.FullName }
