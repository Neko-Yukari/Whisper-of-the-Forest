#requires -Version 7.0
<#
  Cross-checks 1.7 Def XML against the actual assemblies and texture files:
    - Class="..." / <thingClass> / <workerClass> must exist in our DLL, Assembly-CSharp or AlienRace
    - <texPath>/<iconPath>/<wornGraphicPath> must resolve to a file under Textures/
#>
[CmdletBinding()]
param(
    [string]$CecilPath = 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$gameRoot = 'E:/SteamLibrary/steamapps/common/RimWorld'
$workshop = 'E:/SteamLibrary/steamapps/workshop/content/294100'

if (-not (Test-Path -LiteralPath $CecilPath)) {
    throw "Mono.Cecil not found: $CecilPath"
}

Add-Type -Path $CecilPath

$assemblies = @(
    (Join-Path $repo '1.7/Assemblies/WhisperOfTheForest.dll'),
    (Join-Path $gameRoot 'Assembly-CSharp.dll'),
    (Join-Path $workshop '839005762/1.7/Assemblies/AlienRace.dll')
)

$typeNames = New-Object System.Collections.Generic.HashSet[string]
$shortTypeNames = New-Object System.Collections.Generic.HashSet[string]
foreach ($path in $assemblies) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Assembly not found: $path"
    }

    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
    $queue = New-Object System.Collections.Generic.Queue[object]
    foreach ($type in $assembly.MainModule.Types) { $queue.Enqueue($type) }
    while ($queue.Count -gt 0) {
        $type = $queue.Dequeue()
        [void]$typeNames.Add($type.FullName)
        [void]$shortTypeNames.Add($type.Name)
        foreach ($nested in $type.NestedTypes) { $queue.Enqueue($nested) }
    }
}

$failures = New-Object System.Collections.Generic.List[string]
$checked = 0

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo '1.7/Defs') -Recurse -Filter '*.xml' -File) {
    $raw = Get-Content -LiteralPath $file.FullName -Raw
    $classes = New-Object System.Collections.Generic.List[string]
    foreach ($match in [regex]::Matches($raw, 'Class="([^"]+)"')) { $classes.Add($match.Groups[1].Value) }
    foreach ($match in [regex]::Matches($raw, '<(?:thingClass|workerClass)>([^<]+)</')) { $classes.Add($match.Groups[1].Value.Trim()) }

    foreach ($className in $classes) {
        $checked++
        if (-not $typeNames.Contains($className) -and -not $shortTypeNames.Contains($className)) {
            $failures.Add("missing type '$className' referenced by $($file.Name)")
        }
    }

    foreach ($match in [regex]::Matches($raw, '<(?:texPath|iconPath|wornGraphicPath)>([^<]+)</')) {
        $texture = $match.Groups[1].Value.Trim()
        $base = Join-Path $repo ('Textures/' + $texture)
        $found = (Test-Path -LiteralPath ($base + '.png')) -or
                 (Test-Path -LiteralPath ($base + '.dds')) -or
                 (Test-Path -LiteralPath ($base + '.jpg'))
        $checked++
        if (-not $found) {
            $failures.Add("missing texture '$texture' referenced by $($file.Name)")
        }
    }
}

Write-Host "Checked $checked Def type/texture references in 1.7/Defs"
foreach ($failure in $failures) { Write-Host "FAIL  $failure" -ForegroundColor Red }

if ($failures.Count -gt 0) {
    Write-Host "Summary: $($failures.Count) missing"
    exit 1
}

Write-Host 'Summary: 0 missing'
exit 0
