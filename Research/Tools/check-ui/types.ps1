Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/check-ui/proj-real/bin/Debug/net10.0/WotF_UI_Check_Real.dll')
($a.MainModule.Types | ForEach-Object { $_.Name }) -join ', '
