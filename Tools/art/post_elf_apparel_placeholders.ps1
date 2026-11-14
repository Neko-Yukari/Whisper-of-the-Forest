#requires -Version 7.0
<#
  Turns the AI-generated elf apparel art into the in-game worn textures.

  Input  : C:\Users\81004\AppData\Local\Temp\opencode\elf_apparel
           <Name>_<dir>.png    worn graphic, one per direction (south / north / east), any canvas size
           icon_<Name>.png     one item icon per piece (only read with -Icons)
  Output : <Repo>/Textures/Apparel/<Name>/<Name>_<BodyType>_<dir>.png   body garments
           <Repo>/Textures/Movement/TerrainStride_<BodyType>_<dir>.png  existing mantle
           <Repo>/Textures/Apparel/<Name>/<Name>_<dir>.png              Overhead apparel (no body type)
           <Repo>/Textures/<same folder>/<Name>.png                     128 px item icon (-Icons only)

  RimWorld reads <wornGraphicPath>_<BodyType>_<dir> for body garments and <wornGraphicPath>_<dir> for
  Overhead apparel (ApparelGraphicRecordGetter.cs:20, Graphic_Multi.cs:80-83). Apparel, body and head
  draw with the same 1.5 x 1.5 mesh on one 512 canvas, so a worn texture is aligned when its opaque
  box sits where it should against Textures/Placeholder/Milira Naked_Thin_<dir> (body) or
  MiliraHead0_<dir> (head). The elf race only uses the Thin body type; the other four body types are
  copies of the Thin image.

  Placement is derived per direction from those reference boxes, never from fixed pixel numbers:
    body garments  top  = body top + TopFrac * body height  (the collar, hidden under the head layer)
                   hem  = body bottom - Drop, or body top + HemFrac * body height for short pieces
                   the art is scaled uniformly so its box spans top..hem, but no wider than the body
                   width + 2 * SideAllow; when width limits, the hem stays put and the top drops.
                   Centred on the body box centre.
    Overhead       the art's box is fitted (uniform scale, centred) into a box given as fractions of
                   the head box; east has its own fractions because the head box is narrower there.
  Opaque box convention matches the art preview platform: alpha > 8, right/bottom exclusive.
  -Report writes the per-piece, per-direction deltas against the reference box.
  -Only limits the run to the given cells ("Piece" or "Piece:dir") and leaves every other texture
  untouched; without it all 15 pieces are written.
#>
[CmdletBinding()]
param(
    [string]$Repo = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$Source = 'C:\Users\81004\AppData\Local\Temp\opencode\elf_apparel',
    [switch]$Icons,
    [switch]$Preview,
    [string[]]$Only = @(),
    [string]$Report
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
public static class AlphaScan
{
    // minX, minY, maxX+1, maxY+1, centroid x, centroid y of the pixels with alpha > 8; null when empty.
    public static double[] Scan(byte[] bgra, int w, int h)
    {
        int minX = w, maxX = -1, minY = h, maxY = -1;
        double sx = 0, sy = 0;
        long n = 0;
        for (int y = 0; y < h; y++)
        {
            int row = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                if (bgra[row + x * 4 + 3] <= 8) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                sx += x + 0.5;
                sy += y + 0.5;
                n++;
            }
        }
        if (n == 0) return null;
        return new double[] { minX, minY, maxX + 1, maxY + 1, sx / n, sy / n };
    }

    // Body pixels (alpha > bodyMin) in rows yFrom..yTo-1, and how many of them the cover image leaves
    // open (cover alpha <= 8). bodyMin 128 counts solid body, ignoring the 1-2 px soft rim.
    public static long[] Exposed(byte[] body, byte[] cover, int w, int yFrom, int yTo, int bodyMin)
    {
        long total = 0, open = 0;
        for (int y = yFrom; y < yTo; y++)
        {
            int row = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                int i = row + x * 4 + 3;
                if (body[i] < bodyMin) continue;
                total++;
                if (cover[i] <= 8) open++;
            }
        }
        return new long[] { total, open };
    }

    // Longest run of uncovered solid body on one row (how wide the body shows through), rows yFrom..yTo-1.
    public static int MaxRun(byte[] body, byte[] cover, int w, int yFrom, int yTo, int bodyMin)
    {
        int best = 0;
        for (int y = yFrom; y < yTo; y++)
        {
            int run = 0;
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                if (body[i + 3] >= bodyMin && cover[i + 3] <= 8) { run++; if (run > best) best = run; }
                else run = 0;
            }
        }
        return best;
    }
}
'@

$onlyMap = @{}
$onlySpecs = @()
foreach ($s in $Only) { $onlySpecs += ($s -split ',') }
foreach ($spec in $onlySpecs) {
    $parts = $spec.Trim() -split ':'
    if (-not $onlyMap.ContainsKey($parts[0])) { $onlyMap[$parts[0]] = @() }
    $onlyMap[$parts[0]] += $(if ($parts.Count -gt 1) { $parts[1] } else { '*' })
}

function Test-Cell([string]$piece, [string]$dir) {
    if ($onlyMap.Count -eq 0) { return $true }
    if (-not $onlyMap.ContainsKey($piece)) { return $false }
    return ($onlyMap[$piece] -contains '*') -or ($onlyMap[$piece] -contains $dir)
}

$canvas = 512
$bodyTypes = @('Male', 'Female', 'Thin', 'Hulk', 'Fat')
$directions = @('south', 'north', 'east')
$textures = Join-Path $Repo 'Textures'

$topFrac = 0.045      # collar line below the body-box top, as a fraction of body height
$sideAllow = 32       # px a garment may extend past the body box on each side (sleeves, flare)
$maxStretch = 1.12    # widest horizontal stretch applied to a garment narrower than the body
$exposedLimit = 3     # % of the solid body below the shoulder line that a garment may leave open
$runLimit = 8         # px widest strip of body a garment may leave open
$bodyAlphaMin = 128   # solid body, ignoring the antialiased 1-2 px rim
$ankleMargin = 12     # rows above the body bottom excluded from the coverage check (feet show below a hem)

# Body garments: Drop = px above the body-box bottom for the hem (feet bottom at 0); HemFrac = hem line
# as a fraction of body height for pieces that stop short (the mantle).
$bodyPieces = @(
    @{ Name = 'TraditionalDressI';  Folder = 'Apparel/TraditionalDressI';  Drop = 8 },
    @{ Name = 'TraditionalDressII'; Folder = 'Apparel/TraditionalDressII'; Drop = 8 },
    @{ Name = 'ApronDress';         Folder = 'Apparel/ApronDress';         Drop = 8 },
    @{ Name = 'HunterUndershirt';   Folder = 'Apparel/HunterUndershirt';   Drop = 4 },
    @{ Name = 'FormalGown';         Folder = 'Apparel/FormalGown';         Drop = 8; StretchCap = @{ east = 1.05 } },
    @{ Name = 'TravelCloak';        Folder = 'Apparel/TravelCloak';        Drop = 8 },
    @{ Name = 'TerrainStride';      Folder = 'Movement';                   HemFrac = 0.50 },
    @{ Name = 'LightArmor';         Folder = 'Apparel/LightArmor';         Drop = 4 },
    @{ Name = 'MageRobe';           Folder = 'Apparel/MageRobe';           Drop = 6; StretchCap = @{ east = 1.05 } },
    @{ Name = 'WarArmor';           Folder = 'Apparel/WarArmor';           Drop = 4 }
)

# Overhead apparel: target box as @(left, right, top, bottom) fractions of the head box of the same
# direction. SN = south and north, E = east. The art is scaled to fit the box and its top edge sits on
# the box top, so the top fraction is where the piece starts on the head (0 = head top, negative =
# above it).
$headPieces = @(
    @{ Name = 'FlowerWreath';   Folder = 'Apparel/FlowerWreath';   SN = @(0.10, 0.90, 0.10, 0.60);  E = @(0.00, 1.00, 0.10, 0.60) },
    @{ Name = 'SunHat';         Folder = 'Apparel/SunHat';         SN = @(-0.08, 1.08, 0.00, 0.80); E = @(-0.46, 1.46, 0.00, 0.80) },
    @{ Name = 'TravelHood';     Folder = 'Apparel/TravelHood';     SN = @(0.04, 0.96, -0.10, 0.92); E = @(-0.20, 1.04, -0.10, 0.92) },
    @{ Name = 'EntropyCirclet'; Folder = 'Apparel/EntropyCirclet'; SN = @(0.00, 1.00, 0.08, 0.72);  E = @(-0.10, 1.10, 0.08, 0.72) },
    @{ Name = 'WarCrown';       Folder = 'Apparel/WarCrown';       SN = @(0.06, 0.94, 0.10, 0.64);  E = @(-0.08, 1.08, 0.02, 0.62) }
)

function Ensure-Dir([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { [void](New-Item -ItemType Directory -Path $path -Force) }
}

function Get-Pixels([System.Drawing.Bitmap]$bmp) {
    $rect = [System.Drawing.Rectangle]::new(0, 0, $bmp.Width, $bmp.Height)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $bytes = [byte[]]::new($data.Stride * $bmp.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    }
    finally { $bmp.UnlockBits($data) }
    return , $bytes
}

function Measure-Opaque([System.Drawing.Bitmap]$bmp) {
    $r = [AlphaScan]::Scan((Get-Pixels $bmp), $bmp.Width, $bmp.Height)
    if ($null -eq $r) { throw 'image has no opaque pixel' }
    return @{ L = $r[0]; T = $r[1]; R = $r[2]; B = $r[3]; Gx = $r[4]; Gy = $r[5] }
}

function Measure-File([string]$path) {
    $bmp = [System.Drawing.Bitmap]::new($path)
    try { return Measure-Opaque $bmp } finally { $bmp.Dispose() }
}

function New-Transparent([int]$w, [int]$h) {
    return [System.Drawing.Bitmap]::new($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function Save-Png([System.Drawing.Bitmap]$bmp, [string]$path) {
    Ensure-Dir (Split-Path -Parent $path)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

# Draws the opaque box of $src into the destination rectangle on a transparent 512 canvas.
function Draw-Fitted([System.Drawing.Bitmap]$src, $box, [int]$x, [int]$y, [int]$w, [int]$h) {
    $bmp = New-Transparent $canvas $canvas
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $attr = [System.Drawing.Imaging.ImageAttributes]::new()
    $attr.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($src, [System.Drawing.Rectangle]::new($x, $y, $w, $h),
        $box.L, $box.T, ($box.R - $box.L), ($box.B - $box.T), [System.Drawing.GraphicsUnit]::Pixel, $attr)
    $attr.Dispose()
    $g.Dispose()
    return $bmp
}

function Place-Body([System.Drawing.Bitmap]$src, $art, $ref, $piece, [double]$stretchCap) {
    $bodyW = $ref.R - $ref.L
    $bodyH = $ref.B - $ref.T
    $artW = $art.R - $art.L
    $artH = $art.B - $art.T
    $topY = $ref.T + [Math]::Round($topFrac * $bodyH)
    $hemY = if ($piece.ContainsKey('Drop')) { $ref.B - $piece.Drop } else { $ref.T + [Math]::Round($piece.HemFrac * $bodyH) }
    $scale = [Math]::Min(($hemY - $topY) / $artH, ($bodyW + 2 * $sideAllow) / $artW)
    # The generated silhouettes run a few px slimmer than the reference body; widen a too-narrow
    # garment to the body width, by at most $stretchCap, rather than leave the body showing at the sides.
    $sx = 1.0
    if ($artW * $scale -lt $bodyW) { $sx = [Math]::Min($bodyW / ($artW * $scale), $stretchCap) }
    $w = [int][Math]::Round($artW * $scale * $sx)
    $h = [int][Math]::Round($artH * $scale)
    $x = [int][Math]::Round(($ref.L + $ref.R) / 2 - $w / 2)
    $y = [int]($hemY - $h)
    return @{ Bitmap = (Draw-Fitted $src $art $x $y $w $h); Scale = $scale; Sx = $sx; HemY = $hemY }
}

function Place-Head([System.Drawing.Bitmap]$src, $art, $ref, $fractions) {
    $headW = $ref.R - $ref.L
    $headH = $ref.B - $ref.T
    $x0 = $ref.L + $fractions[0] * $headW
    $x1 = $ref.L + $fractions[1] * $headW
    $y0 = $ref.T + $fractions[2] * $headH
    $y1 = $ref.T + $fractions[3] * $headH
    $artW = $art.R - $art.L
    $artH = $art.B - $art.T
    $scale = [Math]::Min(($x1 - $x0) / $artW, ($y1 - $y0) / $artH)
    $w = [int][Math]::Round($artW * $scale)
    $h = [int][Math]::Round($artH * $scale)
    $x = [int][Math]::Round(($x0 + $x1) / 2 - $w / 2)
    $y = [int][Math]::Round($y0)
    return @{ Bitmap = (Draw-Fitted $src $art $x $y $w $h); Scale = $scale; Box = @($x0, $y0, $x1, $y1) }
}

# Icons are generated edge-to-edge, so crop to the art and re-pad with a margin before downscaling.
function Build-Icon([string]$srcPath) {
    $src = [System.Drawing.Bitmap]::new($srcPath)
    $b = Measure-Opaque $src
    $crop = [System.Drawing.Rectangle]::new($b.L, $b.T, $b.R - $b.L, $b.B - $b.T)
    $side = [int]([Math]::Max($crop.Width, $crop.Height) * 1.16)
    $square = New-Transparent $side $side
    $g = [System.Drawing.Graphics]::FromImage($square)
    $g.Clear([System.Drawing.Color]::Transparent)
    $dst = [System.Drawing.Rectangle]::new([int](($side - $crop.Width) / 2), [int](($side - $crop.Height) / 2), $crop.Width, $crop.Height)
    $g.DrawImage($src, $dst, $crop, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $src.Dispose()

    $icon = New-Transparent 128 128
    $g = [System.Drawing.Graphics]::FromImage($icon)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($square, [System.Drawing.Rectangle]::new(0, 0, 128, 128))
    $g.Dispose()
    $square.Dispose()
    return $icon
}

# Reference boxes per direction: Naked_Thin for body garments, MiliraHead0 for Overhead apparel.
$bodyRef = @{}
$headRef = @{}
foreach ($dir in $directions) {
    $bodyRef[$dir] = Measure-File (Join-Path $textures "Placeholder/Milira/Bodies/Naked_Thin_$dir.png")
    $headRef[$dir] = Measure-File (Join-Path $textures "Placeholder/Milira/Heads/MiliraHead0_$dir.png")
}

$bodyPixels = @{}
foreach ($dir in $directions) {
    $b = [System.Drawing.Bitmap]::new((Join-Path $textures "Placeholder/Milira/Bodies/Naked_Thin_$dir.png"))
    $bodyPixels[$dir] = Get-Pixels $b
    $b.Dispose()
}

$rows = @()
$written = 0
$problems = @()

function Get-Verdict($row, $isHead, $extra) {
    $why = @()
    if ($row.L -lt 4 -or $row.T -lt 4 -or $row.R -gt $canvas - 4 -or $row.B -gt $canvas - 4) { $why += 'touches canvas edge' }
    if ([Math]::Abs($row.dCx) -gt 8) { $why += 'box centre x off' }
    if ($isHead) {
        if ($row.dT -lt -20 -or $row.dT -gt 24) { $why += 'top off head band' }
        if ($row.dB -gt 0) { $why += 'below head bottom' }
    }
    else {
        if ($row.dB -gt 0) { $why += 'hem past feet' }
        if ([Math]::Abs($row.B - $extra.HemY) -gt 3) { $why += 'hem off design line' }
        if ($row.dT -lt 4 -or $row.dT -gt 36) { $why += 'top off neck/shoulder band' }
        if ($row.ExpPct -gt $exposedLimit) { $why += ('body uncovered ' + $row.ExpPct + '%') }
        if ($row.MaxRun -gt $runLimit) { $why += ('body strip ' + $row.MaxRun + 'px') }
        if ($row.dL -lt -$sideAllow - 2 -or $row.dR -gt $sideAllow + 2) { $why += 'wider than allowance' }
    }
    return $why
}

foreach ($piece in ($bodyPieces + $headPieces)) {
    $isHead = $piece.ContainsKey('SN')
    foreach ($dir in $directions) {
        if (-not (Test-Cell $piece.Name $dir)) { continue }
        $srcPath = Join-Path $Source ($piece.Name + '_' + $dir + '.png')
        if (-not (Test-Path -LiteralPath $srcPath)) { $problems += "MISSING SOURCE $srcPath"; continue }

        $src = [System.Drawing.Bitmap]::new($srcPath)
        $art = Measure-Opaque $src
        if ($isHead) {
            $ref = $headRef[$dir]
            $placed = Place-Head $src $art $ref $(if ($dir -eq 'east') { $piece.E } else { $piece.SN })
        }
        else {
            $ref = $bodyRef[$dir]
            # StretchCap lowers the cap for cells whose side silhouette must stay as generated.
            $cap = if ($piece.ContainsKey('StretchCap') -and $piece.StretchCap.ContainsKey($dir)) { $piece.StretchCap[$dir] } else { $maxStretch }
            $placed = Place-Body $src $art $ref $piece $cap
        }
        $src.Dispose()
        $bmp = $placed.Bitmap

        if ($bmp.GetPixel(0, 0).A -ne 0) { $problems += ("corner not transparent: " + $piece.Name + '_' + $dir) }
        $m = Measure-Opaque $bmp
        $row = [ordered]@{
            Piece = $piece.Name; Dir = $dir
            L = $m.L; T = $m.T; R = $m.R; B = $m.B
            dL = $m.L - $ref.L; dT = $m.T - $ref.T; dR = $m.R - $ref.R; dB = $m.B - $ref.B
            dCx = (($m.L + $m.R) - ($ref.L + $ref.R)) / 2; dCy = (($m.T + $m.B) - ($ref.T + $ref.B)) / 2
            dGx = [Math]::Round($m.Gx - $ref.Gx, 1); dGy = [Math]::Round($m.Gy - $ref.Gy, 1)
            Scale = [Math]::Round($placed.Scale, 3)
            Sx = $(if ($placed.ContainsKey('Sx')) { [Math]::Round($placed.Sx, 3) } else { $null })
            ExpPct = $null
            MaxRun = $null
        }
        if (-not $isHead) {
            # Body from the shoulder line down to the hem, minus the feet that a hem legitimately leaves
            # visible: solid body the garment leaves open here is the body showing through.
            $yFrom = [int]($ref.T + [Math]::Round(0.25 * ($ref.B - $ref.T)))
            $yTo = [int][Math]::Min($m.B, $ref.B - $ankleMargin)
            $cover = Get-Pixels $bmp
            $ex = [AlphaScan]::Exposed($bodyPixels[$dir], $cover, $canvas, $yFrom, $yTo, $bodyAlphaMin)
            $row.ExpPct = if ($ex[0] -gt 0) { [Math]::Round(100.0 * $ex[1] / $ex[0], 1) } else { 0 }
            $row.MaxRun = [AlphaScan]::MaxRun($bodyPixels[$dir], $cover, $canvas, $yFrom, $yTo, $bodyAlphaMin)
        }
        $extra = @{ CenterX = ($ref.L + $ref.R) / 2; HemY = $placed.HemY }
        $row.Verdict = (Get-Verdict $row $isHead $extra) -join '; '
        $rows += [pscustomobject]$row

        if ($isHead) {
            Save-Png $bmp (Join-Path $textures ($piece.Folder + '/' + $piece.Name + '_' + $dir + '.png'))
            $written++
        }
        else {
            foreach ($bodyType in $bodyTypes) {
                Save-Png $bmp (Join-Path $textures ($piece.Folder + '/' + $piece.Name + '_' + $bodyType + '_' + $dir + '.png'))
                $written++
            }
        }
        $bmp.Dispose()
    }

    if ($Icons) {
        $iconSrc = Join-Path $Source ('icon_' + $piece.Name + '.png')
        if (Test-Path -LiteralPath $iconSrc) {
            $icon = Build-Icon $iconSrc
            if ($icon.GetPixel(0, 0).A -ne 0) { $problems += ('icon corner not transparent: ' + $piece.Name) }
            Save-Png $icon (Join-Path $textures ($piece.Folder + '/' + $piece.Name + '.png'))
            $icon.Dispose()
            $written++
        }
        else { $problems += "MISSING ICON $iconSrc" }
    }
}

# Preview: the game's stack per direction in body-texture space - body, worn layer, head, Overhead
# layer. Head and Overhead nodes sit 0.34 world units above the body, which on the 1.5 unit mesh over
# the 512 canvas is 116 px (plus 31 px to the right facing east); body garments are not shifted.
# Red outline = reference opaque box (body for garments, head for Overhead apparel).
if ($Preview) {
    $previewDir = Join-Path $Source 'preview'
    Ensure-Dir $previewDir
    $headShift = @{ south = @(0, -116); north = @(0, -116); east = @(31, -116) }
    $crop = [System.Drawing.Rectangle]::new(110, 30, 292, 460)
    $cellW = [int]($crop.Width * 0.8)
    $cellH = [int]($crop.Height * 0.8)
    $groups = @(
        @{ Name = 'bodyA'; Pieces = $bodyPieces[0..4] },
        @{ Name = 'bodyB'; Pieces = $bodyPieces[5..9] },
        @{ Name = 'head';  Pieces = $headPieces }
    )
    foreach ($group in $groups) {
        $picked = @($group.Pieces | Where-Object { $pn = $_.Name; (Test-Cell $pn 'south') -or (Test-Cell $pn 'north') -or (Test-Cell $pn 'east') })
        if ($picked.Count -eq 0) { continue }
        $sheet = New-Transparent ($cellW * 3) ($cellH * $picked.Count)
        $sg = [System.Drawing.Graphics]::FromImage($sheet)
        $sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $row = 0
        foreach ($piece in $group.Pieces) {
            if (-not ($picked -contains $piece)) { continue }
            $isHead = $piece.ContainsKey('SN')
            $col = 0
            foreach ($dir in $directions) {
                $shift = $headShift[$dir]
                $cell = New-Transparent $canvas $canvas
                $g = [System.Drawing.Graphics]::FromImage($cell)
                $g.Clear([System.Drawing.Color]::FromArgb(255, 52, 56, 66))
                $layers = @(, @((Join-Path $textures "Placeholder/Milira/Bodies/Naked_Thin_$dir.png"), 0, 0))
                if (-not $isHead) { $layers += , @((Join-Path $textures ($piece.Folder + '/' + $piece.Name + '_Thin_' + $dir + '.png')), 0, 0) }
                $layers += , @((Join-Path $textures "Placeholder/Milira/Heads/MiliraHead0_$dir.png"), $shift[0], $shift[1])
                if ($isHead) { $layers += , @((Join-Path $textures ($piece.Folder + '/' + $piece.Name + '_' + $dir + '.png')), $shift[0], $shift[1]) }
                foreach ($l in $layers) {
                    $img = [System.Drawing.Bitmap]::new($l[0])
                    $g.DrawImage($img, $l[1], $l[2], $canvas, $canvas)
                    $img.Dispose()
                }
                $ref = if ($isHead) { $headRef[$dir] } else { $bodyRef[$dir] }
                $ox = if ($isHead) { $shift[0] } else { 0 }
                $oy = if ($isHead) { $shift[1] } else { 0 }
                $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(160, 255, 60, 60), 1)
                $g.DrawRectangle($pen, $ref.L + $ox, $ref.T + $oy, $ref.R - $ref.L, $ref.B - $ref.T)
                $pen.Dispose()
                $g.Dispose()
                $sg.DrawImage($cell, [System.Drawing.Rectangle]::new($col * $cellW, $row * $cellH, $cellW, $cellH), $crop, [System.Drawing.GraphicsUnit]::Pixel)
                $cell.Dispose()
                $col++
            }
            $row++
        }
        $sg.Dispose()
        $sheet.Save((Join-Path $previewDir ('worn_' + $group.Name + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
        $sheet.Dispose()
    }
    Write-Host "previews: $previewDir"
}
if ($Report) {
    $lines = @('| piece | dir | bbox l,t,r,b | dL | dT | dR | dB | dCx | dCy | dGx | dGy | open% | maxRun | scale | sx | verdict |', '| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |')
    foreach ($r in $rows) {
        $lines += ('| {0} | {1} | {2},{3},{4},{5} | {6} | {7} | {8} | {9} | {10} | {11} | {12} | {13} | {14} | {15} | {16} | {17} | {18} |' -f
            $r.Piece, $r.Dir, $r.L, $r.T, $r.R, $r.B, $r.dL, $r.dT, $r.dR, $r.dB, $r.dCx, $r.dCy, $r.dGx, $r.dGy, $r.ExpPct, $r.MaxRun, $r.Scale, $r.Sx, $(if ($r.Verdict) { $r.Verdict } else { 'ok' }))
    }
    Set-Content -LiteralPath $Report -Value $lines -Encoding UTF8
    Write-Host "report: $Report"
}

Write-Host "files written: $written"
Write-Host ("rows: $($rows.Count), flagged: " + @($rows | Where-Object { $_.Verdict }).Count)
if ($problems) { $problems | ForEach-Object { Write-Host $_ } }
