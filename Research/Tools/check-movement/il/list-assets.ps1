Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead('E:/SteamLibrary/steamapps/common/RimWorld/Assets.zip')
$patterns = @('ShieldBelt','/Shirt','FlakVest','Duster')
foreach ($e in $zip.Entries) {
  foreach ($p in $patterns) {
    if ($e.FullName -like "*$p*") { $e.FullName; break }
  }
}
$zip.Dispose()
