param(
  [string]$ModRoot = 'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest',
  [string]$BodyTypesXml = 'E:/SteamLibrary/steamapps/common/RimWorld/Data/Core/Defs/Misc/BodyTypeDefs/BodyTypes.xml'
)

# Static check for worn apparel graphics:
# ApparelGraphicRecordGetter.TryGetGraphicApparel builds
#   wornGraphicPath + "_" + BodyTypeDef.defName
# for every apparel whose last layer is not Overhead/EyeCover and which is not rendered as a pack,
# then loads it through Graphic_Multi, which resolves _north/_east/_south/_west and finally the
# plain path, and logs "Failed to find any textures at ..." when all of them are missing.
$defs = ([xml](Get-Content -Raw (Join-Path $ModRoot '1.7/Defs/Experiments/Movement.xml'))).Defs.ThingDef
$bodyTypes = ([xml](Get-Content -Raw $BodyTypesXml)).Defs.BodyTypeDef | ForEach-Object { $_.defName }
$textureRoot = Join-Path $ModRoot 'Textures'
$failures = New-Object System.Collections.Generic.List[string]

function Test-Png($path) {
  if (-not (Test-Path -LiteralPath $path)) { return "missing" }
  $bytes = [System.IO.File]::ReadAllBytes($path)
  if ($bytes.Length -lt 33) { return "too-small" }
  if (-not ($bytes[0] -eq 0x89 -and $bytes[1] -eq 0x50 -and $bytes[2] -eq 0x4E -and $bytes[3] -eq 0x47)) { return "not-png" }
  $w = [System.Net.IPAddress]::NetworkToHostOrder([System.BitConverter]::ToInt32($bytes, 16))
  $h = [System.Net.IPAddress]::NetworkToHostOrder([System.BitConverter]::ToInt32($bytes, 20))
  if ($w -le 0 -or $h -le 0) { return "bad-ihdr" }
  return "ok ${w}x${h}"
}

foreach ($def in $defs) {
  $name = $def.defName
  $worn = $def.apparel.wornGraphicPath
  $ground = $def.graphicData.texPath
  $layers = @($def.apparel.layers.li)
  $suffixRequired = -not (($layers -contains 'Overhead') -or ($layers -contains 'EyeCover'))

  $groundState = Test-Png (Join-Path $textureRoot ($ground + '.png'))
  $line = "{0}: layers=[{1}] wornGraphicPath={2} ground=-{3}-> {4}" -f $name, ($layers -join ','), $worn, $ground, $groundState
  if ($groundState -ne 'ok' -and $groundState -notlike 'ok *') { $failures.Add("$name ground $groundState") }
  Write-Output $line

  foreach ($bt in $bodyTypes) {
    $path = if ($suffixRequired) { $worn + '_' + $bt } else { $worn }
    $state = Test-Png (Join-Path $textureRoot ($path + '.png'))
    $ok = $state -like 'ok *'
    if (-not $ok) { $failures.Add("$name bodyType=$bt path=$path $state") }
    Write-Output ("  {0,-7} {1,-28} {2}" -f $bt, ($path + '.png'), $state)
  }
}

if ($failures.Count -gt 0) {
  Write-Output ''
  Write-Output 'FAIL:'
  $failures | ForEach-Object { Write-Output ("  " + $_) }
  exit 1
}

Write-Output ''
Write-Output ("PASS: all {0} apparel defs have ground + {1} body-type worn textures" -f $defs.Count, $bodyTypes.Count)
exit 0
