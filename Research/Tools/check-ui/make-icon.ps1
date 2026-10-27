Add-Type -AssemblyName System.Drawing

$outDir = 'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest/Textures/Experiments/UI'
New-Item -ItemType Directory -Force $outDir | Out-Null

$size = 128
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

function New-RoundedPath([int]$w, [int]$h, [int]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $r * 2
  $p.AddArc(0, 0, $d, $d, 180, 90)
  $p.AddArc($w - $d, 0, $d, $d, 270, 90)
  $p.AddArc($w - $d, $h - $d, $d, $d, 0, 90)
  $p.AddArc(0, $h - $d, $d, $d, 90, 90)
  $p.CloseFigure()
  return $p
}

$card = New-RoundedPath 128 128 26
$cardBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 18, 32, 26))
$g.FillPath($cardBrush, $card)
$cardPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 111, 163, 127), 3)
$g.DrawPath($cardPen, $card)

# soft glow
$glow1 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 191, 232, 198))
$g.FillEllipse($glow1, 26, 26, 76, 76)
$glow2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(60, 191, 232, 198))
$g.FillEllipse($glow2, 40, 40, 48, 48)

# wisp ring
$ringPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(205, 191, 232, 198), 5)
$g.DrawEllipse($ringPen, 30, 30, 68, 68)

# spiral trail of dots
$dots = @(
  @{ a = 0.5; r = 21.0; d = 10 },
  @{ a = 2.3; r = 16.0; d = 8 },
  @{ a = 4.1; r = 11.0; d = 7 },
  @{ a = 5.7; r = 6.5; d = 6 }
)
$textureBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(235, 226, 255, 233))
foreach ($dot in $dots) {
  $cx = 64 + $dot.r * [Math]::Cos($dot.a)
  $cy = 64 + $dot.r * [Math]::Sin($dot.a)
  $g.FillEllipse($textureBrush, [single]($cx - $dot.d / 2), [single]($cy - $dot.d / 2), [single]$dot.d, [single]$dot.d)
}

# bright core
$core = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 244, 255, 246))
$g.FillEllipse($core, 60, 60, 8, 8)

$g.Dispose()
$target = Join-Path $outDir 'TechLabIcon.png'
$bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$info = Get-Item $target
"wrote $($info.FullName) $($info.Length) bytes"
