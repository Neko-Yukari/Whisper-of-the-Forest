param([string]$OutDir)
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function New-ApparelTex {
  param([string]$Path, [System.Drawing.Color]$Body, [System.Drawing.Color]$Accent, [string]$Shape)
  $bmp = New-Object System.Drawing.Bitmap 64, 64
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)
  $brushMain = New-Object System.Drawing.SolidBrush $Body
  $brushAcc = New-Object System.Drawing.SolidBrush $Accent
  $penAcc = New-Object System.Drawing.Pen $Accent, 2.0

  if ($Shape -eq 'mantle') {
    # shoulder mantle: trapezoid over torso, plus leaf trim
    $pts = @(
      (New-Object System.Drawing.Point 16, 10),
      (New-Object System.Drawing.Point 48, 10),
      (New-Object System.Drawing.Point 54, 40),
      (New-Object System.Drawing.Point 10, 40)
    )
    $g.FillPolygon($brushMain, $pts)
    $g.DrawLine($penAcc, 12, 34, 52, 34)
    $g.DrawLine($penAcc, 22, 14, 22, 40)
    $g.DrawLine($penAcc, 42, 14, 42, 40)
  } elseif ($shape -eq 'sash') {
    # diagonal sash across waist
    $pts = @(
      (New-Object System.Drawing.Point 12, 22),
      (New-Object System.Drawing.Point 22, 18),
      (New-Object System.Drawing.Point 52, 44),
      (New-Object System.Drawing.Point 42, 48)
    )
    $g.FillPolygon($brushMain, $pts)
    $g.FillEllipse($brushAcc, 24, 24, 8, 8)
    $g.FillEllipse($brushAcc, 34, 32, 8, 8)
  }
  $g.Dispose()
  $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

New-ApparelTex -Path (Join-Path $OutDir 'TerrainStride.png') -Body ([System.Drawing.Color]::FromArgb(255, 92, 122, 74)) -Accent ([System.Drawing.Color]::FromArgb(255, 201, 176, 96)) -Shape 'mantle'
New-ApparelTex -Path (Join-Path $OutDir 'BlinkStride.png') -Body ([System.Drawing.Color]::FromArgb(255, 88, 76, 140)) -Accent ([System.Drawing.Color]::FromArgb(255, 190, 233, 255)) -Shape 'sash'
Get-ChildItem $OutDir | Select-Object Name, Length
