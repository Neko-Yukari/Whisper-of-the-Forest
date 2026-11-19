#requires -Version 7.0
<#
  Cross-checks 1.7 Def XML against the actual assemblies and texture files:
    - Class="..." / <thingClass> / <workerClass> must exist in our DLL, Assembly-CSharp or AlienRace
    - <texPath>/<iconPath> must resolve to a file under Textures/; <wornGraphicPath> is checked
      together with its body-type and direction suffixes by rule 2

  Structural rules mirror the decompiled vanilla load path
  (../Graphify/native/source, read-only reference):
    1. Def root element must resolve to a Def type.
       Verse/DirectXmlToObjectNew.cs:185 - "Type X is not a Def type or could not be found"
       GenTypes.cs:53 - short names only resolve inside RIMWORLD/VERSE/LUDEONTK/... namespaces
    2. Apparel worn graphics are suffixed with _<BodyType> unless the last layer is
       Overhead/EyeCover, RenderAsPack() is true, or the path is a vanilla placeholder, and are then
       loaded as a Graphic_Multi in four directions.
       RimWorld/ApparelGraphicRecordGetter.cs:20 builds "<wornGraphicPath>_<BodyType>" (Verse/PawnRenderUtility.cs:38,
       RimWorld/ApparelProperties.cs:105 LastLayer = last entry of <layers>), and
       Verse/Graphic_Multi.cs:80-83 reads "_north/_east/_south/_west" from that path.
       Graphic_Multi.cs:128-134 mirrors _west from _east, so _west is optional but
       _south/_north/_east are required. Non-headed apparel therefore needs
       <path>_<BodyType>_<south|north|east>; Overhead/EyeCover apparel needs
       <path>_<south|north|east> and ignores BodyTypeDef entirely.
    3. Races: Core BasePawn declares no <tools>, so a race with no tools has no melee verb.
    4. ThingDef.ConfigErrors: Verse/ThingDef.cs:1820
       "is smeltable but does not give anything for smelting."
    5. HediffDef.ConfigErrors: Verse/HediffDef.cs:390
       "has comps but hediffClass is not HediffWithComps or subclass thereof"
    6. Verse/Game.cs:475-486 instantiates every non-abstract GameComponent with
       Activator.CreateInstance(type, game); StaticConstructorOnStartupUtility.cs:32-51
       warns about types with static asset fields and no StaticConstructorOnStartup.
    7. Vanilla classes that look up one hard-coded verb type and use the result without a null
       check never match a mod verb ($hardVerbConsumers, each with its source line). Today only
       RimWorld/PlaceWorker_ShowTurretRadius.cs:9 (exactly Verb_Shoot, or a Verb_Spray subclass).
    8. ParentName must name a node that carries a Name attribute; XmlInheritance indexes Name only.
       Verse/XmlInheritance.cs:79-95, :222-260.
    9. A key used as "WotF_...".Translate( in Source/WhisperOfTheForest must exist in the Keyed files
       of English and ChineseSimplified. Verse/Translator.cs:53-66, Verse/LoadedLanguage.cs:488-492.
   10. SitePartDef.siteTexture / expandingIconTexture may only name a file shipped under Textures/ or a
       path some Def under Data/Core/Defs references; the vanilla asset bundle also carries DLC art, so a
       DLC-only path loads only with that DLC active.
    11. Every <li> under rulesStrings / rulesFiles (Defs and DefInjected) must read keyword->text.
        Verse.Grammar/Rule_String.cs:18,49-54, Verse.Grammar/RulePack.cs:91-119.
    12. Every <li> under placeWorkers / inspectorTabs must resolve to a type of the right base.
        Verse/ParseHelper.cs:219-235 parses a Type name through GenTypes.GetTypeInAnyAssembly
        (short names only get the IgnoredNamespaceNames prefixes, Verse/GenTypes.cs:53-57), logs
        "Could not find a type named X" and keeps a null list entry. Verse/BuildableDef.cs:152-170
        then hits Activator.CreateInstance(null) on the first placement check and throws
        ArgumentNullException (Parameter 'type') with nothing to catch it; the inspector tab path
        does the same at load but its exception is swallowed per tab
        (Verse/ThingDef.cs:1653-1667), so the tab just disappears.
#>
[CmdletBinding()]
param(
    [string]$CecilPath = 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll',
    [string]$Repo = (Split-Path -Parent $PSScriptRoot),
    # The mod assembly to check the Defs against. build.ps1 passes its temporary build here while
    # RimWorld holds the deployed DLL.
    [string]$ModAssembly
)

$ErrorActionPreference = 'Stop'
$repo = $Repo
$gameRoot = 'E:/SteamLibrary/steamapps/common/RimWorld'
$workshop = 'E:/SteamLibrary/steamapps/workshop/content/294100'

if (-not (Test-Path -LiteralPath $CecilPath)) {
    throw "Mono.Cecil not found: $CecilPath"
}

Add-Type -Path $CecilPath

# Short type names resolve only inside these namespaces (Verse/GenTypes.cs:53).
$ignoredNamespaces = @(
    'RimWorld', 'Verse', 'LudeonTK', 'Verse.AI', 'Verse.AI.Group', 'Verse.Sound', 'Verse.Grammar',
    'RimWorld.Planet', 'RimWorld.BaseGen', 'RimWorld.QuestGen', 'RimWorld.SketchGen', 'System'
)

# Vanilla skip list for the body-type suffix (ApparelGraphicRecordGetter.cs:20).
$headOnlyLayers = @('Overhead', 'EyeCover')
$placeholderGraphicPaths = @('PlaceholderImage', 'PlaceholderImage_Gear')
$bodyTypes = @('Male', 'Female', 'Thin', 'Hulk', 'Fat')
# Graphic_Multi.cs:80-83. _west is optional: Graphic_Multi.cs:128-134 mirrors it from _east.
$wornGraphicDirections = @('south', 'north', 'east')

# StaticConstructorOnStartupUtility.cs:44-51 field-type rule.
$assetFieldTypes = @(
    'UnityEngine.Texture', 'UnityEngine.Material', 'UnityEngine.Shader', 'Verse.Graphic',
    'UnityEngine.GameObject', 'UnityEngine.MaterialPropertyBlock'
)

# Vanilla classes that look up one hard-coded verb type and dereference the result without a null
# check. A mod verb never matches, so using one of these with our verbs throws. The other vanilla
# verb-type checks (CompShield.CompAllowVerbCast, AttackTargetFinder, VerbUtility.GetProjectile...)
# skip a non-matching verb and do not belong here.
$hardVerbConsumers = @(
    @{
        Class   = 'RimWorld.PlaceWorker_ShowTurretRadius'
        Source  = 'RimWorld/PlaceWorker_ShowTurretRadius.cs:9'
        Needs   = 'a verb whose class is exactly Verb_Shoot or derives from Verb_Spray'
        Accepts = { param($verbType) ($verbType.Full -ieq 'Verse.Verb_Shoot') -or (Test-IsA $verbType 'Verse.Verb_Spray') }
        Fix     = 'Build placement throws every frame in UIRootUpdate; draw the range ring with a mod PlaceWorker that reads the gun verb itself.'
    }
)

$modAssemblyPath = if ($ModAssembly) { $ModAssembly } else { Join-Path $repo '1.7/Assemblies/WhisperOfTheForest.dll' }
$rimLumenRoot = @(
    'C:/Users/81004/orca/RimLumen',
    (Join-Path $gameRoot 'Mods/RimLumen'),
    (Join-Path $workshop '3839499092')
) | Where-Object { Test-Path -LiteralPath (Join-Path $_ '1.7/Assemblies/RimLumen.Core.dll') } | Select-Object -First 1
if (-not $rimLumenRoot) {
    throw "RimLumen.Core.dll not found under GameRoot/Mods/RimLumen, C:/Users/81004/orca/RimLumen, or Workshop 3839499092"
}
$rimLumenAssemblyPath = Join-Path $rimLumenRoot '1.7/Assemblies/RimLumen.Core.dll'
$catalogAssemblies = @(
    @{ Path = $modAssemblyPath; Mod = $true; Required = $true },
    @{ Path = (Join-Path $gameRoot 'Assembly-CSharp.dll'); Mod = $false; Required = $true },
    @{ Path = (Join-Path $workshop '839005762/1.7/Assemblies/AlienRace.dll'); Mod = $false; Required = $true },
    @{ Path = $rimLumenAssemblyPath; Mod = $false; Required = $true },
    @{ Path = (Join-Path $gameRoot 'UnityStubs.dll'); Mod = $false; Required = $false },
    @{ Path = (Join-Path $workshop '3819570122/1.7/Assemblies/RimUnity.Core.dll'); Mod = $false; Required = $false },
    @{ Path = (Join-Path $workshop '3819570122/1.7/Assemblies/RimUnity.dll'); Mod = $false; Required = $false }
)

$typeNames = New-Object System.Collections.Generic.HashSet[string]
$shortTypeNames = New-Object System.Collections.Generic.HashSet[string]
$typeByFull = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($assemblyInfo in $catalogAssemblies) {
    if (-not (Test-Path -LiteralPath $assemblyInfo.Path)) {
        if ($assemblyInfo.Required) {
            throw "Assembly not found: $($assemblyInfo.Path)"
        }
        continue
    }

    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($assemblyInfo.Path)
    $queue = New-Object System.Collections.Generic.Queue[object]
    foreach ($type in $assembly.MainModule.Types) { $queue.Enqueue($type) }
    while ($queue.Count -gt 0) {
        $type = $queue.Dequeue()

        $fullName = $type.FullName.Replace('/', '+')
        $attributes = @()
        foreach ($attribute in $type.CustomAttributes) { $attributes += $attribute.AttributeType.Name }

        $fields = @()
        $constructors = @()
        if ($assemblyInfo.Mod) {
            foreach ($field in $type.Fields) {
                if (-not $field.IsStatic) { continue }
                $fieldTypeName = $field.FieldType.FullName
                if ($field.FieldType -is [Mono.Cecil.ArrayType]) {
                    $fieldTypeName = $field.FieldType.ElementType.FullName
                }
                $fields += [pscustomobject]@{ Name = $field.Name; Type = $fieldTypeName }
            }
            foreach ($method in $type.Methods) {
                if (-not $method.IsConstructor -or $method.IsStatic) { continue }
                $constructors += [pscustomobject]@{
                    Public = $method.IsPublic
                    Parameters = @($method.Parameters | ForEach-Object { $_.ParameterType.FullName })
                }
            }
        }

        if (-not $typeByFull.ContainsKey($fullName)) {
            $typeByFull[$fullName] = [pscustomobject]@{
                Full       = $fullName
                Short      = $type.Name
                Namespace  = $type.Namespace
                Base       = $(if ($null -ne $type.BaseType) { $type.BaseType.FullName.Replace('/', '+') } else { $null })
                Abstract   = $type.IsAbstract
                IsClass    = $type.IsClass
                FromMod    = $assemblyInfo.Mod
                Attributes = $attributes
                Fields     = $fields
                Constructors = $constructors
            }
        }

        if ($assemblyInfo.Required) {
            [void]$typeNames.Add($type.FullName)
            [void]$shortTypeNames.Add($type.Name)
        }

        foreach ($nested in $type.NestedTypes) { $queue.Enqueue($nested) }
    }
    $assembly.Dispose()
}

function Resolve-TypeName([string]$name) {
    if ([string]::IsNullOrWhiteSpace($name)) { return $null }
    $trimmed = $name.Trim()
    $hit = $null
    if ($typeByFull.TryGetValue($trimmed, [ref]$hit)) { return $hit }
    foreach ($namespace in $ignoredNamespaces) {
        if ($typeByFull.TryGetValue("$namespace.$trimmed", [ref]$hit)) { return $hit }
    }
    return $null
}

function Test-IsA([object]$type, [string]$targetFullName) {
    $current = $type
    while ($null -ne $current) {
        if ($current.Full -ieq $targetFullName) { return $true }
        if ([string]::IsNullOrEmpty($current.Base)) { return $false }
        $next = $null
        if (-not $typeByFull.TryGetValue($current.Base, [ref]$next)) { return $false }
        $current = $next
    }
    return $false
}

$failures = New-Object System.Collections.Generic.List[string]
$checked = 0
$ruleChecks = 0

function Add-Failure([string]$message, [string]$file) {
    $failures.Add("$message in $file")
}

function Get-RelativePath([string]$fullPath) {
    return [System.IO.Path]::GetRelativePath($repo, $fullPath).Replace('\', '/')
}

$defIndex = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)

function Add-DefToIndex([System.Xml.XmlElement]$element, [string]$file) {
    $keys = New-Object System.Collections.Generic.List[string]
    $nameAttribute = $element.GetAttribute('Name')
    if (-not [string]::IsNullOrEmpty($nameAttribute)) { $keys.Add($nameAttribute) }
    $defNameNode = $element.SelectSingleNode('defName')
    if ($null -ne $defNameNode) { $keys.Add($defNameNode.InnerText.Trim()) }
    foreach ($key in $keys) {
        if ([string]::IsNullOrEmpty($key)) { continue }
        if (-not $defIndex.ContainsKey($key)) {
            $defIndex[$key] = [pscustomobject]@{ Node = $element; File = $file; Key = $key }
        }
    }
}

function Get-DefChain([object]$entry) {
    $chain = New-Object System.Collections.Generic.List[object]
    $visited = New-Object System.Collections.Generic.HashSet[string]
    $current = $entry
    while ($null -ne $current) {
        $chain.Add($current)
        [void]$visited.Add($current.Key)
        $parentName = $current.Node.GetAttribute('ParentName')
        if ([string]::IsNullOrEmpty($parentName)) { break }
        if ($visited.Contains($parentName)) { break }
        $parent = $null
        if (-not $defIndex.TryGetValue($parentName, [ref]$parent)) { break }
        $current = $parent
    }
    $chain.Reverse()
    return ,$chain
}

function Get-NodeAt([System.Collections.Generic.List[object]]$chain, [string[]]$path) {
    for ($i = $chain.Count - 1; $i -ge 0; $i--) {
        $node = $chain[$i].Node
        foreach ($step in $path) {
            if ($null -eq $node) { break }
            $node = $node.SelectSingleNode($step)
        }
        if ($null -ne $node) { return $node }
    }
    return $null
}

function Get-ScalarAt([System.Collections.Generic.List[object]]$chain, [string[]]$path) {
    $node = Get-NodeAt $chain $path
    if ($null -eq $node) { return $null }
    return $node.InnerText.Trim()
}

function Get-ListAt([System.Collections.Generic.List[object]]$chain, [string[]]$path) {
    $items = New-Object System.Collections.Generic.List[object]
    foreach ($entry in $chain) {
        $node = $entry.Node
        $missing = $false
        foreach ($step in $path) {
            if ($null -eq $node) { $missing = $true; break }
            $node = $node.SelectSingleNode($step)
        }
        if ($missing -or $null -eq $node) { continue }
        # Inherit="False" replaces the parent's list instead of appending to it.
        if ($node.GetAttribute('Inherit') -ieq 'False') { $items.Clear() }
        foreach ($item in $node.SelectNodes('li')) { $items.Add($item) }
    }
    return ,$items
}

function Test-ElementListAt([System.Collections.Generic.List[object]]$chain, [string[]]$path) {
    # costList/smeltProducts are written as children named after thing defs (<Steel>30</Steel>),
    # not as <li> entries; any element child means the list is not empty.
    $node = Get-NodeAt $chain $path
    if ($null -eq $node) { return $false }
    foreach ($child in $node.ChildNodes) {
        if ($child.NodeType -eq 'Element') { return $true }
    }
    return $false
}

function ConvertTo-XmlBool([string]$text) {
    return ($text -ieq 'true')
}

# Vanilla textures ship packed in <game>/Assets.zip as Resources/Textures/<path>.<ext...>; a Def may
# point at one of those (e.g. a culture or faction icon) as well as at the mod's own Textures/.
$vanillaTextures = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$assetsZip = [IO.Compression.ZipFile]::OpenRead((Join-Path $gameRoot 'Assets.zip'))
try {
    foreach ($entry in $assetsZip.Entries) {
        if (-not $entry.FullName.StartsWith('Resources/Textures/')) { continue }
        $path = $entry.FullName.Substring('Resources/Textures/'.Length)
        $dot = $path.IndexOf('.', $path.LastIndexOf('/') + 1)
        if ($dot -gt 0) { [void]$vanillaTextures.Add($path.Substring(0, $dot)) }
    }
}
finally {
    $assetsZip.Dispose()
}

function Test-TextureExists([string]$texturePath) {
    $base = Join-Path (Join-Path $repo 'Textures') $texturePath
    return (Test-Path -LiteralPath ($base + '.png')) -or
           (Test-Path -LiteralPath ($base + '.dds')) -or
           (Test-Path -LiteralPath ($base + '.jpg')) -or
           $vanillaTextures.Contains($texturePath)
}

# ---------------------------------------------------------------- mod Def files

$defRoot = Join-Path $repo '1.7/Defs'
$modDefs = New-Object System.Collections.Generic.List[object]

foreach ($file in Get-ChildItem -LiteralPath $defRoot -Recurse -Filter '*.xml' -File | Sort-Object FullName) {
    $raw = Get-Content -LiteralPath $file.FullName -Raw
    $relativeFile = Get-RelativePath $file.FullName

    # Existing reference checks keep their string-based form.
    $classes = New-Object System.Collections.Generic.List[string]
    foreach ($match in [regex]::Matches($raw, 'Class="([^"]+)"')) { $classes.Add($match.Groups[1].Value) }
    foreach ($match in [regex]::Matches($raw, '<(?:thingClass|workerClass)>([^<]+)</')) { $classes.Add($match.Groups[1].Value.Trim()) }

    foreach ($className in $classes) {
        $checked++
        if (-not $typeNames.Contains($className) -and -not $shortTypeNames.Contains($className)) {
            $failures.Add("missing type '$className' referenced by $($file.Name)")
        }
    }

    # wornGraphicPath is not checked here: only the firmly suffixed files (_<BodyType>_<dir> or, for
    # head layers, _<dir>) are required by Graphic_Multi, so the plain base path may not exist at all.
    # The apparel rule below checks the suffixed set.
    foreach ($match in [regex]::Matches($raw, '<(?:texPath|iconPath)>([^<]+)</')) {
        $texture = $match.Groups[1].Value.Trim()
        $checked++
        if (-not (Test-TextureExists $texture)) {
            $failures.Add("missing texture '$texture' referenced by $($file.Name)")
        }
    }

    [xml]$document = $raw
    $root = $document.DocumentElement
    if ($null -eq $root -or $root.get_Name() -ne 'Defs') {
        $rootName = $(if ($null -ne $root) { $root.get_Name() } else { '<empty document>' })
        Add-Failure "root element is '$rootName' instead of 'Defs'" $relativeFile
        continue
    }

    foreach ($node in $root.ChildNodes) {
        if ($node.NodeType -ne 'Element') { continue }

        # .get_Name(), not .Name: PowerShell's XML adapter returns the Name attribute for .Name.
        $writtenName = $node.get_Name()
        $classAttribute = $node.GetAttribute('Class')
        if (-not [string]::IsNullOrEmpty($classAttribute)) { $writtenName = $classAttribute }

        $defNameNode = $node.SelectSingleNode('defName')
        $defLabel = $(if ($null -ne $defNameNode) { $defNameNode.InnerText.Trim() } else { $node.GetAttribute('Name') })

        $ruleChecks++
        $rootType = Resolve-TypeName $writtenName
        if ($null -eq $rootType) {
            Add-Failure "Def root element '$writtenName' does not resolve to any type (defName '$defLabel')" $relativeFile
            continue
        }
        if (-not (Test-IsA $rootType 'Verse.Def')) {
            Add-Failure "Def root element '$writtenName' resolves to '$($rootType.Full)', which is not a Def type (defName '$defLabel')" $relativeFile
            continue
        }

        $modDefs.Add([pscustomobject]@{
            Node       = $node
            File       = $relativeFile
            RootType   = $rootType
            DefName    = $defLabel
            IsAbstract = (ConvertTo-XmlBool $node.GetAttribute('Abstract'))
        })
    }
}

# Vanilla parents are needed to resolve smeltable/layers/tools inherited by our Defs.
$coreDefRoot = Join-Path $gameRoot 'Data/Core/Defs'
if (Test-Path -LiteralPath $coreDefRoot) {
    foreach ($file in Get-ChildItem -LiteralPath $coreDefRoot -Recurse -Filter '*.xml' -File) {
        [xml]$document = Get-Content -LiteralPath $file.FullName -Raw
        $root = $document.DocumentElement
        if ($null -eq $root -or $root.get_Name() -ne 'Defs') { continue }
        foreach ($node in $root.ChildNodes) {
            if ($node.NodeType -ne 'Element') { continue }
            Add-DefToIndex $node ([System.IO.Path]::GetRelativePath($gameRoot, $file.FullName).Replace('\', '/'))
        }
    }
}

foreach ($entry in $modDefs) {
    Add-DefToIndex $entry.Node $entry.File
}

# ------------------------------------------------------------ parent names

# XmlInheritance indexes nodes by their Name attribute alone: TryRegister keys nodesByName on Name
# (Verse/XmlInheritance.cs:79-95) and TryGetParent resolves ParentName through that same index
# (:222-260). A ParentName that matches a defName resolves to nothing, and the child silently keeps
# its own root type's defaults instead of the parent's class and fields; the error only shows up when
# that field is used (quest generation throws on a layout without workerClass). Vanilla has one such
# trap: Data/Ideology/Defs/LayoutDefs/Layouts_Misc.xml:4 carries Name="AncientComplex" while :49 is
# only defName="AncientComplex_Loot".
$namedNodes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$nameAttributePattern = [regex]'(?s)<[A-Za-z][^<>]*?(?<![\w.])Name\s*=\s*"([^"]+)"'

function Add-NamedNodesFrom([string]$file) {
    $raw = Get-Content -LiteralPath $file -Raw
    if ($raw.IndexOf('Name', [System.StringComparison]::Ordinal) -lt 0) { return }

    foreach ($match in $nameAttributePattern.Matches($raw)) {
        [void]$namedNodes.Add($match.Groups[1].Value.Trim())
    }
}

foreach ($expansion in Get-ChildItem -LiteralPath (Join-Path $gameRoot 'Data') -Directory) {
    $expansionDefs = Join-Path $expansion.FullName 'Defs'
    if (-not (Test-Path -LiteralPath $expansionDefs)) { continue }

    foreach ($file in Get-ChildItem -LiteralPath $expansionDefs -Recurse -Filter '*.xml' -File) {
        Add-NamedNodesFrom $file.FullName
    }
}

foreach ($file in Get-ChildItem -LiteralPath $defRoot -Recurse -Filter '*.xml' -File) {
    Add-NamedNodesFrom $file.FullName
}

foreach ($entry in $modDefs) {
    $ruleChecks++
    $parentName = $entry.Node.GetAttribute('ParentName')
    if ([string]::IsNullOrEmpty($parentName)) { continue }

    if (-not $namedNodes.Contains($parentName)) {
        Add-Failure "ParentName '$parentName' matches no node carrying a Name attribute in vanilla Data or this mod; XmlInheritance indexes only Name (Verse/XmlInheritance.cs:79-95), so this parent resolves to nothing and the Def keeps its own defaults" $entry.File
    }
}

# ------------------------------------------------------------ structural rules

foreach ($entry in $modDefs) {
    if ($entry.IsAbstract) { continue }
    $chain = Get-DefChain $entry
    $isThingDef = Test-IsA $entry.RootType 'Verse.ThingDef'
    $isHediffDef = Test-IsA $entry.RootType 'Verse.HediffDef'

    if ($isThingDef) {
        $thingClass = Get-ScalarAt $chain @('thingClass')
        $thingClassType = Resolve-TypeName $thingClass
        $isApparel = ($null -ne $thingClassType) -and (Test-IsA $thingClassType 'RimWorld.Apparel')

        # Rule 2: worn graphics are looked up per body type.
        if ($isApparel) {
            $wornGraphicPath = Get-ScalarAt $chain @('apparel', 'wornGraphicPath')
            if (-not [string]::IsNullOrEmpty($wornGraphicPath) -and ($placeholderGraphicPaths -notcontains $wornGraphicPath)) {
                $layers = Get-ListAt $chain @('apparel', 'layers')
                # ApparelProperties.LastLayer falls back to Belt when <layers> is empty.
                $lastLayer = $(if ($layers.Count -gt 0) { $layers[$layers.Count - 1].InnerText.Trim() } else { 'Belt' })
                $wornGraphicData = Get-NodeAt $chain @('apparel', 'wornGraphicData')

                $skipBodyTypeSuffix = $false
                if ($headOnlyLayers -contains $lastLayer) {
                    $skipBodyTypeSuffix = $true
                } elseif ($lastLayer -ieq 'Belt') {
                    # IsUtilityLayer is "== ApparelLayerDefOf.Belt"; RenderAsPack then depends on
                    # wornGraphicData (absent -> true, present -> renderUtilityAsPack, default false).
                    if ($null -eq $wornGraphicData) {
                        $skipBodyTypeSuffix = $true
                    } else {
                        $renderUtilityAsPack = Get-ScalarAt $chain @('apparel', 'wornGraphicData', 'renderUtilityAsPack')
                        $skipBodyTypeSuffix = ConvertTo-XmlBool $renderUtilityAsPack
                    }
                }

                $ruleChecks++
                # ApparelGraphicRecordGetter.cs:20 appends "_<BodyType>" only for non-headed apparel;
                # Graphic_Multi.cs:80-83 then loads _south/_north/_east (and an optional _west, which
                # Graphic_Multi.cs:128-134 mirrors from _east when absent).
                if ($skipBodyTypeSuffix) {
                    $missing = @()
                    foreach ($direction in $wornGraphicDirections) {
                        if (-not (Test-TextureExists ($wornGraphicPath + '_' + $direction))) {
                            $missing += ('_' + $direction)
                        }
                    }
                    if ($missing.Count -gt 0) {
                        Add-Failure "apparel '$($entry.DefName)' wornGraphicPath '$wornGraphicPath' (layer '$lastLayer') is missing directional textures: $($missing -join ', ')" $entry.File
                    }
                }
                else {
                    $missing = @()
                    foreach ($bodyType in $bodyTypes) {
                        foreach ($direction in $wornGraphicDirections) {
                            $suffixed = $wornGraphicPath + '_' + $bodyType + '_' + $direction
                            if (-not (Test-TextureExists $suffixed)) {
                                $missing += ('_' + $bodyType + '_' + $direction)
                            }
                        }
                    }
                    if ($missing.Count -gt 0) {
                        Add-Failure "apparel '$($entry.DefName)' wornGraphicPath '$wornGraphicPath' (layer '$lastLayer') is missing directional textures: $($missing -join ', ')" $entry.File
                    }
                }
            }
        }

        # Rule 3: anything descending from Core BasePawn needs its own <tools>.
        $isPawnRace = $false
        foreach ($ancestor in $chain) { if ($ancestor.Key -ieq 'BasePawn') { $isPawnRace = $true; break } }
        if ($isPawnRace) {
            $ruleChecks++
            $tools = Get-ListAt $chain @('tools')
            if ($tools.Count -eq 0) {
                Add-Failure "pawn ThingDef '$($entry.DefName)' descends from BasePawn but has no <tools>; melee has no verb" $entry.File
            }
        }

        # Rule 4: ThingDef.ConfigErrors smelting condition.
        $smeltable = ConvertTo-XmlBool (Get-ScalarAt $chain @('smeltable'))
        if ($smeltable) {
            $ruleChecks++
            $hasSmeltProducts = Test-ElementListAt $chain @('smeltProducts')
            $hasCostList = Test-ElementListAt $chain @('costList')
            $hasDifficultyCostList = Test-ElementListAt $chain @('costListForDifficulty', 'costList')
            $stuffProps = Get-NodeAt $chain @('stuffProps')
            $stuffCategories = Get-ListAt $chain @('stuffCategories')
            $destroyOnDrop = ConvertTo-XmlBool (Get-ScalarAt $chain @('destroyOnDrop'))

            $givesSomething = $hasSmeltProducts -or $hasCostList -or $hasDifficultyCostList -or
                ($null -ne $stuffProps) -or ($stuffCategories.Count -gt 0) -or $destroyOnDrop
            if (-not $givesSomething) {
                Add-Failure "ThingDef '$($entry.DefName)' is smeltable but does not give anything for smelting" $entry.File
            }
        }

        # Rule 12: placeWorkers / inspectorTabs are Type lists. Verse/ParseHelper.cs:219-235 resolves each
        # name through GenTypes.GetTypeInAnyAssembly, which only prefixes GenTypes.IgnoredNamespaceNames
        # (Verse/GenTypes.cs:53-57) for a short name — our own classes must be written with their full name.
        # An unresolvable name is logged as "Could not find a type named X" and stays in the list as null.
        # Verse/BuildableDef.cs:152-170 then calls Activator.CreateInstance on every entry with no null
        # check, so the first build-placement check (GenConstruct.cs:509-519) throws
        # ArgumentNullException (Parameter 'type') and nothing catches it. inspectorTabs go through the
        # same parse but ThingDef.cs:1653-1667 wraps each resolution in try/catch, so a bad tab is only
        # logged ("Could not instantiate inspector tab of type ...") and then silently missing from the
        # building. Neither shows up in ConfigErrors, so nothing but this rule catches it before the game.
        $placeWorkers = Get-ListAt $chain @('placeWorkers')
        if ($placeWorkers.Count -gt 0) {
            $ruleChecks++
            foreach ($placeWorker in $placeWorkers) {
                $workerName = $placeWorker.InnerText.Trim()
                $workerType = Resolve-TypeName $workerName
                if ($null -eq $workerType) {
                    Add-Failure "ThingDef '$($entry.DefName)' lists placeWorker '$workerName', which resolves to no type; a short name only gets the GenTypes.cs:53-57 namespace prefixes, so our own place workers need their full name. The load logs 'Could not find a type named $workerName' and keeps a null entry, and the first placement check throws ArgumentNullException (Parameter 'type') (Verse/ParseHelper.cs:219-235, Verse/BuildableDef.cs:152-170)" $entry.File
                } elseif (-not (Test-IsA $workerType 'Verse.PlaceWorker')) {
                    Add-Failure "ThingDef '$($entry.DefName)' lists placeWorker '$workerName', which resolves to '$($workerType.Full)' and is not a Verse.PlaceWorker; the cast in Verse/BuildableDef.cs:165 throws on the first placement check" $entry.File
                }
            }
        }

        $inspectorTabs = Get-ListAt $chain @('inspectorTabs')
        if ($inspectorTabs.Count -gt 0) {
            $ruleChecks++
            foreach ($inspectorTab in $inspectorTabs) {
                $tabName = $inspectorTab.InnerText.Trim()
                $tabType = Resolve-TypeName $tabName
                if ($null -eq $tabType) {
                    Add-Failure "ThingDef '$($entry.DefName)' lists inspectorTab '$tabName', which resolves to no type; the null entry makes Verse/InspectTabManager.cs:12-16 throw ArgumentNullException (Parameter 'type'), which Verse/ThingDef.cs:1653-1667 catches and logs, so the tab silently never appears on the building" $entry.File
                } elseif (-not (Test-IsA $tabType 'Verse.InspectTabBase')) {
                    Add-Failure "ThingDef '$($entry.DefName)' lists inspectorTab '$tabName', which resolves to '$($tabType.Full)' and is not a Verse.InspectTabBase; the cast in Verse/InspectTabManager.cs:16 throws, Verse/ThingDef.cs:1653-1667 catches it, and the tab silently never appears" $entry.File
                }
            }
        }

        # Rule 7: vanilla classes that need one hard-coded verb type on the turret gun.
        foreach ($consumer in $hardVerbConsumers) {
            $usesConsumer = $false
            foreach ($placeWorker in (Get-ListAt $chain @('placeWorkers'))) {
                $placeWorkerType = Resolve-TypeName $placeWorker.InnerText
                if ($null -ne $placeWorkerType -and $placeWorkerType.Full -ieq $consumer.Class) {
                    $usesConsumer = $true
                    break
                }
            }
            if (-not $usesConsumer) { continue }

            $ruleChecks++
            $consumerName = $consumer.Class.Substring($consumer.Class.LastIndexOf('.') + 1)
            $gunDefName = Get-ScalarAt $chain @('building', 'turretGunDef')
            $gunEntry = $null
            if ([string]::IsNullOrEmpty($gunDefName) -or -not $defIndex.TryGetValue($gunDefName, [ref]$gunEntry)) {
                Add-Failure "turret '$($entry.DefName)' uses $consumerName but turretGunDef '$gunDefName' was not found" $entry.File
                continue
            }

            $accepted = $false
            $verbNames = @()
            foreach ($verb in (Get-ListAt (Get-DefChain $gunEntry) @('verbs'))) {
                $verbClassNode = $verb.SelectSingleNode('verbClass')
                if ($null -eq $verbClassNode) { continue }
                $verbNames += $verbClassNode.InnerText.Trim()
                $verbType = Resolve-TypeName $verbClassNode.InnerText
                if ($null -ne $verbType -and (& $consumer.Accepts $verbType)) {
                    $accepted = $true
                    break
                }
            }
            if (-not $accepted) {
                Add-Failure "turret '$($entry.DefName)' uses $consumerName, which needs $($consumer.Needs) on turretGunDef '$gunDefName' ($($consumer.Source)); found: $($verbNames -join ', '). $($consumer.Fix)" $entry.File
            }
        }
    }

    if ($isHediffDef) {
        $comps = Get-ListAt $chain @('comps')
        if ($comps.Count -gt 0) {
            # HediffDef.hediffClass defaults to typeof(Hediff).
            $hediffClassName = Get-ScalarAt $chain @('hediffClass')
            if ([string]::IsNullOrEmpty($hediffClassName)) { $hediffClassName = 'Hediff' }
            $ruleChecks++
            $hediffClassType = Resolve-TypeName $hediffClassName
            if ($null -eq $hediffClassType) {
                Add-Failure "HediffDef '$($entry.DefName)' has comps but hediffClass '$hediffClassName' could not be resolved" $entry.File
            } elseif (-not (Test-IsA $hediffClassType 'Verse.HediffWithComps')) {
                Add-Failure "HediffDef '$($entry.DefName)' has comps but hediffClass is not HediffWithComps or subclass thereof" $entry.File
            }
        }
    }

    if (Test-IsA $entry.RootType 'Verse.ShaderDef') {
        $shaderProgram = Get-NodeAt $chain @('shaderProgram')
        if ($null -ne $shaderProgram) {
            foreach ($stageTag in @('vertexShaderFileName', 'fragmentShaderFileName')) {
                $shaderFile = Get-ScalarAt $chain @('shaderProgram', $stageTag)
                if ([string]::IsNullOrEmpty($shaderFile) -or $shaderFile.StartsWith('RimUnity/', [System.StringComparison]::OrdinalIgnoreCase)) { continue }
                $ruleChecks++
                $modShader = Join-Path (Join-Path $repo '1.7/Shaders') $shaderFile
                $coreShadersDir = Join-Path $gameRoot 'Data/Core/Shaders'
                $foundShader = (Test-Path -LiteralPath $modShader)
                if (-not $foundShader -and (Test-Path -LiteralPath $coreShadersDir)) {
                    $foundShader = $null -ne (Get-ChildItem -LiteralPath $coreShadersDir -Recurse -Filter $shaderFile -File | Select-Object -First 1)
                }
                if (-not $foundShader) {
                    Add-Failure "ShaderDef '$($entry.DefName)' <$stageTag>$shaderFile</$stageTag> was not found under 1.7/Shaders/ or Core/Shaders/" $entry.File
                }
            }
            if ($entry.DefName.StartsWith('WotF_Shader_Lumen', [System.StringComparison]::OrdinalIgnoreCase)) {
                $ruleChecks++
                $depthWrite = Get-ScalarAt $chain @('shaderProgram', 'depthWrite')
                if ($depthWrite -ine 'false') {
                    Add-Failure "Layer 14/15 ShaderDef '$($entry.DefName)' must set <depthWrite>false</depthWrite> so RimLumen does not trip FailSafeTripped" $entry.File
                }
                if ($entry.DefName.Equals('WotF_Shader_LumenDistortion', [System.StringComparison]::OrdinalIgnoreCase)) {
                    $ruleChecks++
                    $depthTest = Get-ScalarAt $chain @('shaderProgram', 'depthTest')
                    if ($depthTest -ine 'false') {
                        Add-Failure "Layer 14 ShaderDef '$($entry.DefName)' must set <depthTest>false</depthTest> because RimLumen DistortionPass has VkFormat.Undefined depth" $entry.File
                    }
                }
            }
        }
    }

    if (Test-IsA $entry.RootType 'Verse.SoundDef') {
        $onCamera = ConvertTo-XmlBool (Get-ScalarAt $chain @('onCamera'))
        $contextText = Get-ScalarAt $chain @('context')
        if (-not $onCamera) {
            $ruleChecks++
            if ($contextText -ine 'MapOnly') {
                Add-Failure "SoundDef '$($entry.DefName)' is a map sound and must declare <context>MapOnly</context>" $entry.File
            }
        }
        foreach ($grainNode in $entry.Node.SelectNodes('.//grains/li')) {
            $clipPathNode = $grainNode.SelectSingleNode('clipPath')
            $clipFolderNode = $grainNode.SelectSingleNode('clipFolderPath')
            $soundsRoot = Join-Path $repo '1.7/Sounds'
            if ($null -ne $clipPathNode) {
                $clipRel = $clipPathNode.InnerText.Trim()
                if ($clipRel.StartsWith('Spells/', [System.StringComparison]::OrdinalIgnoreCase) -or $clipRel.StartsWith('Shields/', [System.StringComparison]::OrdinalIgnoreCase) -or $clipRel.StartsWith('WotF/', [System.StringComparison]::OrdinalIgnoreCase)) {
                    $ruleChecks++
                    $baseClip = Join-Path $soundsRoot $clipRel
                    if (-not ((Test-Path -LiteralPath ($baseClip + '.ogg')) -or (Test-Path -LiteralPath ($baseClip + '.wav')))) {
                        Add-Failure "SoundDef '$($entry.DefName)' clipPath '$clipRel' has no .ogg or .wav under 1.7/Sounds/" $entry.File
                    }
                }
            }
            if ($null -ne $clipFolderNode) {
                $folderRel = $clipFolderNode.InnerText.Trim()
                if ($folderRel.StartsWith('Spells/', [System.StringComparison]::OrdinalIgnoreCase) -or $folderRel.StartsWith('WotF/', [System.StringComparison]::OrdinalIgnoreCase)) {
                    $ruleChecks++
                    $folderFull = Join-Path $soundsRoot $folderRel
                    $hasAudio = (Test-Path -LiteralPath $folderFull) -and (@(Get-ChildItem -LiteralPath $folderFull -File | Where-Object { $_.Extension -in '.ogg', '.wav' }).Count -gt 0)
                    if (-not $hasAudio) {
                        Add-Failure "SoundDef '$($entry.DefName)' clipFolderPath '$folderRel' contains no .ogg or .wav under 1.7/Sounds/" $entry.File
                    }
                }
            }
        }
    }
}

# ------------------------------------------------------------ stuff named next to a thing

# Scenario parts (ScenPart_StartingThing_Defined) and stock generators name a thing and its material side by
# side as <thingDef> + <stuff>. ThingMaker.MakeThing (Verse/ThingMaker.cs:10-24) repairs a bad pair with an
# error each time: a stuff without stuffProps (ThingDef.IsStuff, Verse/ThingDef.cs:908) is swapped for the
# default, and a stuff given to a def without stuffCategories (BuildableDef.MadeFromStuff,
# Verse/BuildableDef.cs:142) is dropped. A stuff whose categories the thing does not accept
# (StuffProperties.CanMake, Verse/StuffProperties.cs:102) is never offered by the game itself.
foreach ($entry in $modDefs) {
    foreach ($stuffNode in $entry.Node.SelectNodes('.//stuff')) {
        $thingDefNode = $stuffNode.ParentNode.SelectSingleNode('thingDef')
        if ($null -eq $thingDefNode) { continue }
        $ruleChecks++
        $thingName = $thingDefNode.InnerText.Trim()
        $stuffName = $stuffNode.InnerText.Trim()
        $thingEntry = $null
        $stuffEntry = $null
        if (-not $defIndex.TryGetValue($thingName, [ref]$thingEntry) -or -not $defIndex.TryGetValue($stuffName, [ref]$stuffEntry)) {
            Add-Failure "'$($entry.DefName)' names thingDef '$thingName' with stuff '$stuffName', but one of them is not a Core or mod Def this check can read" $entry.File
            continue
        }

        $thingCategories = @((Get-ListAt (Get-DefChain $thingEntry) @('stuffCategories')) | ForEach-Object { $_.InnerText.Trim() })
        $stuffChain = Get-DefChain $stuffEntry
        if ($thingCategories.Count -eq 0) {
            Add-Failure "'$($entry.DefName)' gives stuff '$stuffName' to '$thingName', which is not madeFromStuff; ThingMaker logs 'is not madeFromStuff but stuff=' and drops it (Verse/ThingMaker.cs:20-23)" $entry.File
        }
        elseif ($null -eq (Get-NodeAt $stuffChain @('stuffProps'))) {
            Add-Failure "'$($entry.DefName)' makes '$thingName' from '$stuffName', which has no stuffProps; ThingMaker logs 'which is not a stuff' and uses the default (Verse/ThingMaker.cs:10-14)" $entry.File
        }
        else {
            $stuffCategories = @((Get-ListAt $stuffChain @('stuffProps', 'categories')) | ForEach-Object { $_.InnerText.Trim() })
            if (@($thingCategories | Where-Object { $stuffCategories -contains $_ }).Count -eq 0) {
                Add-Failure "'$($entry.DefName)' makes '$thingName' (stuffCategories $($thingCategories -join ', ')) from '$stuffName' (categories $($stuffCategories -join ', ')); StuffProperties.CanMake (Verse/StuffProperties.cs:102) rejects the pair" $entry.File
            }
        }
    }
}

# ------------------------------------------------------- assembly-level rules

$modAssemblyName = Split-Path -Leaf $modAssemblyPath
$modTypes = @($typeByFull.Values | Where-Object { $_.FromMod })

# Rule 6a: the game creates each concrete GameComponent with Activator.CreateInstance(type, game).
foreach ($type in $modTypes) {
    if ($type.Abstract -or -not $type.IsClass) { continue }
    if (-not (Test-IsA $type 'Verse.GameComponent')) { continue }
    $ruleChecks++

    $hasGameConstructor = $false
    foreach ($constructor in $type.Constructors) {
        if (-not $constructor.Public) { continue }
        if ($constructor.Parameters.Count -eq 1 -and $constructor.Parameters[0] -ieq 'Verse.Game') {
            $hasGameConstructor = $true
            break
        }
    }
    if (-not $hasGameConstructor) {
        Add-Failure "GameComponent '$($type.Full)' has no public constructor taking Verse.Game (Activator.CreateInstance(type, game) fails)" $modAssemblyName
    }
}

# Rule 6b: mirror StaticConstructorOnStartupUtility.ReportProbablyMissingAttributes.
foreach ($type in $modTypes) {
    $hasStaticConstructorAttribute = $false
    $current = $type
    while ($null -ne $current) {
        if ($current.Attributes -contains 'StaticConstructorOnStartup') {
            $hasStaticConstructorAttribute = $true
            break
        }
        if ([string]::IsNullOrEmpty($current.Base)) { break }
        $next = $null
        if (-not $typeByFull.TryGetValue($current.Base, [ref]$next)) { break }
        $current = $next
    }
    if ($hasStaticConstructorAttribute) { continue }

    foreach ($field in $type.Fields) {
        $fieldType = $null
        if (-not $typeByFull.TryGetValue($field.Type, [ref]$fieldType)) { continue }
        $matchesAssetField = $false
        foreach ($assetType in $assetFieldTypes) {
            if (Test-IsA $fieldType $assetType) { $matchesAssetField = $true; break }
        }
        if ($matchesAssetField) {
            $ruleChecks++
            $shortFieldType = $field.Type.Substring($field.Type.LastIndexOf('.') + 1)
            Add-Failure "Type '$($type.Short)' probably needs a StaticConstructorOnStartup attribute, because it has a field '$($field.Name)' of type '$shortFieldType'; all assets must be loaded in the main thread" $modAssemblyName
        }
    }
}

# ------------------------------------------------------------ Harmony patch bindings

# Harmony 3.0 (0Harmony.dll in workshop 2009463077/1.7) finds the original in PatchTools.GetOriginalMethod
# with AccessTools.DeclaredMethod / DeclaredPropertyGetter / DeclaredPropertySetter / DeclaredConstructor:
# only members declared on the named type count, a method name without argument types must have exactly
# one overload ("Ambiguous match for HarmonyMethod[...]"), and a constructor without argument types means
# the parameterless one. Prefix/Postfix/Finalizer arguments bind by name: an ordinary parameter must carry
# the name of an original parameter, __instance/__result/__N/___field have their own meaning. A wrong name
# or a missing target makes PatchAll throw on the first bad class, so every later patch is lost too.
# Classes that pick their target in code (TargetMethod/TargetMethods) are listed, not checked.
$cecilResolver = New-Object Mono.Cecil.DefaultAssemblyResolver
foreach ($searchDirectory in @(
    $gameRoot,
    (Split-Path -Parent $modAssemblyPath),
    (Split-Path -Parent $rimLumenAssemblyPath),
    (Join-Path $workshop '2009463077/1.7/Assemblies'),
    (Join-Path $workshop '839005762/1.7/Assemblies'),
    (Join-Path $workshop '3819570122/1.7/Assemblies')
)) {
    $cecilResolver.AddSearchDirectory($searchDirectory)
}
$readerParameters = New-Object Mono.Cecil.ReaderParameters
$readerParameters.AssemblyResolver = $cecilResolver
$harmonyAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($modAssemblyPath, $readerParameters)

$harmonySpecialArguments = @('__state', '__args', '__originalMethod', '__runOriginal', '__exception', '__resultRef')
$harmonyPatchKinds = @{
    'HarmonyLib.HarmonyPrefix' = 'Prefix'; 'HarmonyLib.HarmonyPostfix' = 'Postfix'
    'HarmonyLib.HarmonyFinalizer' = 'Finalizer'; 'HarmonyLib.HarmonyTranspiler' = 'Transpiler'
}
$dynamicPatchClasses = New-Object System.Collections.Generic.List[string]
$harmonyClassesChecked = 0

function Get-TypeLineage([Mono.Cecil.TypeReference]$type) {
    $names = New-Object System.Collections.Generic.List[string]
    $current = $type
    while ($null -ne $current) {
        $names.Add($current.FullName)
        $definition = $current.Resolve()
        foreach ($implementation in $definition.Interfaces) { $names.Add($implementation.InterfaceType.FullName) }
        $current = $definition.BaseType
    }
    return ,$names
}

# Harmony loads the original value into the patch argument as-is, so the patch type has to be the original
# type, one of its bases or interfaces, or object; ref on either side is allowed.
function Test-PatchArgumentFits([Mono.Cecil.TypeReference]$patchType, [Mono.Cecil.TypeReference]$originalType) {
    if ($patchType.IsByReference) { $patchType = $patchType.ElementType }
    if ($originalType.IsByReference) { $originalType = $originalType.ElementType }
    if ($patchType.FullName -eq 'System.Object') { return $true }
    return (Get-TypeLineage $originalType).Contains($patchType.FullName)
}

function Test-HasField([Mono.Cecil.TypeDefinition]$type, [string]$fieldName) {
    $current = $type
    while ($null -ne $current) {
        foreach ($field in $current.Fields) { if ($field.Name -ceq $fieldName) { return $true } }
        if ($null -eq $current.BaseType) { return $false }
        $current = $current.BaseType.Resolve()
    }
    return $false
}

function Get-PatchKind([Mono.Cecil.MethodDefinition]$method) {
    foreach ($attribute in $method.CustomAttributes) {
        $kind = $harmonyPatchKinds[$attribute.AttributeType.FullName]
        if ($null -ne $kind) { return $kind }
    }
    if (@('Prefix', 'Postfix', 'Finalizer', 'Transpiler') -ccontains $method.Name) { return $method.Name }
    return $null
}

$harmonyTypes = New-Object System.Collections.Generic.List[object]
$harmonyQueue = New-Object System.Collections.Generic.Queue[object]
foreach ($type in $harmonyAssembly.MainModule.Types) { $harmonyQueue.Enqueue($type) }
while ($harmonyQueue.Count -gt 0) {
    $type = $harmonyQueue.Dequeue()
    $harmonyTypes.Add($type)
    foreach ($nested in $type.NestedTypes) { $harmonyQueue.Enqueue($nested) }
}

foreach ($patchClass in $harmonyTypes) {
    $patchAttributes = @($patchClass.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' })
    if ($patchAttributes.Count -eq 0) { continue }
    $ruleChecks++
    $className = $patchClass.FullName.Replace('/', '+')

    if (@($patchClass.Methods | Where-Object { $_.Name -cin 'TargetMethod', 'TargetMethods' }).Count -gt 0) {
        $dynamicPatchClasses.Add($className)
        continue
    }
    if (@($patchClass.Methods | Where-Object { @($_.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' }).Count -gt 0 }).Count -gt 0) {
        Add-Failure "Harmony class '$className' puts [HarmonyPatch] on a method; check-defs only reads class-level targets, extend the rule before using this form" $modAssemblyName
        continue
    }

    # Harmony merges every class-level [HarmonyPatch] into one target description.
    $targetType = $null; $methodName = $null; $methodType = 0; $argumentTypes = $null; $argumentVariations = $null
    $unsupported = $null
    foreach ($attribute in $patchAttributes) {
        foreach ($argument in $attribute.ConstructorArguments) {
            switch ($argument.Type.FullName) {
                'System.Type' { $targetType = $argument.Value }
                'System.String' { $methodName = $argument.Value }
                'HarmonyLib.MethodType' { $methodType = [int]$argument.Value }
                'System.Type[]' { $argumentTypes = @($argument.Value | ForEach-Object { $_.Value.FullName }) }
                'HarmonyLib.ArgumentType[]' { $argumentVariations = @($argument.Value | ForEach-Object { [int]$_.Value }) }
                default { $unsupported = $argument.Type.FullName }
            }
        }
    }
    if ($null -ne $unsupported) {
        Add-Failure "Harmony class '$className' passes a $unsupported to [HarmonyPatch], which check-defs does not read; extend the rule" $modAssemblyName
        continue
    }
    if ($null -eq $targetType) {
        Add-Failure "Harmony class '$className' names no target type and has no TargetMethod" $modAssemblyName
        continue
    }
    if ($null -ne $argumentTypes -and $null -ne $argumentVariations) {
        # ArgumentType: 0 Normal, 1 Ref, 2 Out, 3 Pointer.
        for ($i = 0; $i -lt $argumentTypes.Count; $i++) {
            if ($argumentVariations[$i] -in 1, 2) { $argumentTypes[$i] += '&' }
            elseif ($argumentVariations[$i] -eq 3) { $argumentTypes[$i] += '*' }
        }
    }

    $targetDefinition = $targetType.Resolve()
    $targetLabel = "$($targetType.FullName).$methodName"
    # MethodType: 0 Normal, 1 Getter, 2 Setter, 3 Constructor; the others are not used here.
    $original = $null
    if ($methodType -in 0, 3) {
        $candidates = @($targetDefinition.Methods | Where-Object {
            if ($methodType -eq 3) { $_.IsConstructor -and -not $_.IsStatic } else { -not $_.IsConstructor -and $_.Name -ceq $methodName }
        })
        if ($methodType -eq 3) {
            $targetLabel = "$($targetType.FullName)..ctor"
            if ($null -eq $argumentTypes) { $argumentTypes = @() }
        }
        if ($null -ne $argumentTypes) {
            $wanted = $argumentTypes -join ','
            $candidates = @($candidates | Where-Object { (@($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -ceq $wanted })
            $targetLabel += "($wanted)"
        }
        if ($candidates.Count -gt 1) {
            Add-Failure "Harmony class '$className' targets $targetLabel, which has $($candidates.Count) declared overloads; Harmony throws 'Ambiguous match', so name the argument types" $modAssemblyName
            continue
        }
        if ($candidates.Count -eq 1) { $original = $candidates[0] }
    }
    elseif ($methodType -in 1, 2) {
        $property = @($targetDefinition.Properties | Where-Object { $_.Name -ceq $methodName })
        if ($property.Count -eq 1) { $original = $(if ($methodType -eq 1) { $property[0].GetMethod } else { $property[0].SetMethod }) }
        $targetLabel += $(if ($methodType -eq 1) { ' (getter)' } else { ' (setter)' })
    }
    else {
        Add-Failure "Harmony class '$className' uses MethodType $methodType, which check-defs does not resolve; extend the rule" $modAssemblyName
        continue
    }
    if ($null -eq $original) {
        Add-Failure "Harmony class '$className' targets $targetLabel, which is not declared on that type; PatchAll throws and every later patch is lost" $modAssemblyName
        continue
    }

    $patchMethods = @($patchClass.Methods | Where-Object { $null -ne (Get-PatchKind $_) })
    if ($patchMethods.Count -eq 0) {
        Add-Failure "Harmony class '$className' has [HarmonyPatch] but no Prefix/Postfix/Finalizer/Transpiler" $modAssemblyName
        continue
    }

    $harmonyClassesChecked++
    $returnsValue = $original.ReturnType.FullName -ne 'System.Void'
    foreach ($patchMethod in $patchMethods) {
        if ((Get-PatchKind $patchMethod) -eq 'Transpiler') { continue }
        foreach ($parameter in $patchMethod.Parameters) {
            $name = $parameter.Name
            $where = "Harmony $className.$($patchMethod.Name) argument '$name' (target $targetLabel)"
            # Harmony matches every name below case-sensitively; PowerShell's -eq would not.
            if ($name -ceq '__instance') {
                if ($original.IsStatic) { Add-Failure "$where asks for __instance of a static method" $modAssemblyName }
                elseif (-not (Test-PatchArgumentFits $parameter.ParameterType $targetType)) { Add-Failure "$where is $($parameter.ParameterType.FullName), which $($targetType.FullName) cannot be passed as" $modAssemblyName }
            }
            elseif ($name -ceq '__result') {
                if (-not $returnsValue) { Add-Failure "$where asks for __result of a void method" $modAssemblyName }
                elseif (-not (Test-PatchArgumentFits $parameter.ParameterType $original.ReturnType)) { Add-Failure "$where is $($parameter.ParameterType.FullName) but the original returns $($original.ReturnType.FullName)" $modAssemblyName }
            }
            elseif ($harmonySpecialArguments -ccontains $name) { continue }
            elseif ($name.StartsWith('___')) {
                if (-not (Test-HasField $targetDefinition $name.Substring(3))) { Add-Failure "$where injects field '$($name.Substring(3))', which $($targetType.FullName) and its bases do not have" $modAssemblyName }
            }
            elseif ($name -match '^__(\d+)$') {
                $index = [int]$Matches[1]
                if ($index -ge $original.Parameters.Count) { Add-Failure "$where is past the original's $($original.Parameters.Count) parameters" $modAssemblyName }
                elseif (-not (Test-PatchArgumentFits $parameter.ParameterType $original.Parameters[$index].ParameterType)) { Add-Failure "$where is $($parameter.ParameterType.FullName) but parameter $index is $($original.Parameters[$index].ParameterType.FullName)" $modAssemblyName }
            }
            elseif ($name.StartsWith('__')) {
                Add-Failure "$where is not an argument name Harmony injects" $modAssemblyName
            }
            else {
                $originalParameter = @($original.Parameters | Where-Object { $_.Name -ceq $name })
                if ($originalParameter.Count -eq 0) {
                    $originalNames = @($original.Parameters | ForEach-Object { $_.Name }) -join ', '
                    Add-Failure "$where matches no parameter of the original ($originalNames); Harmony binds by name and PatchAll throws" $modAssemblyName
                }
                elseif (-not (Test-PatchArgumentFits $parameter.ParameterType $originalParameter[0].ParameterType)) {
                    Add-Failure "$where is $($parameter.ParameterType.FullName) but the original parameter is $($originalParameter[0].ParameterType.FullName)" $modAssemblyName
                }
            }
        }
    }
}
$harmonyAssembly.Dispose()

# DefInjectionPackage.AddDataFromFile treats any element with children as a list key and keeps only <li>,
# so a type wrapper like <ThingDef> around the entries silently drops every translation inside it.
foreach ($langFile in Get-ChildItem (Join-Path $repo '1.7/Languages') -Recurse -Filter *.xml | Where-Object { $_.FullName -match '[\\/]DefInjected[\\/]' }) {
    $ruleChecks++
    [xml]$langXml = Get-Content -LiteralPath $langFile.FullName -Raw -Encoding UTF8
    foreach ($entry in $langXml.DocumentElement.ChildNodes) {
        if ($entry.NodeType -ne 'Element' -or $entry.Name -like '*.slateRef') { continue }
        $nonLi = @($entry.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.Name -ne 'li' })
        if ($nonLi.Count -gt 0) {
            Add-Failure "DefInjected entry '$($entry.Name)' wraps non-li elements; the game drops them" $langFile.Name
        }
    }
}

# ---------------------------------------------------------------- grammar rule strings

# Every <li> under rulesStrings has to match Rule_String.Pattern, `keyword->output`, where keyword is
# [a-zA-Z0-9_/]+ optionally followed by a (name=value, ...) list (Verse.Grammar/Rule_String.cs:18, applied
# at :49-54). A string that does not match is not rejected at load: the constructor logs "Bad string pass
# when reading rule ..." and returns a rule with a null keyword and output (:52-53). RulePack then calls
# Init() and adds it to the pack (Verse.Grammar/RulePack.cs:95-97), GrammarResolver.AddRule reads
# rule.keyword as a dictionary key (GrammarResolver.cs:381) and throws ArgumentNullException, which
# QuestGen reports as "Error while generating quest description" (RimWorld.QuestGen/QuestGen.cs:134-139).
# That is what `questDescription>text` (missing the dash) produced.
# rulesFiles is parsed differently: split on "->", [0] is the keyword and [1] the path, both trimmed
# (RulePack.cs:108-111), and a missing "->" throws IndexOutOfRange (:115-118). Rule_File does not read a
# parameter list, so there the keyword must be plain and both halves non-empty.
# DefInjected replaces the same lists (a <li> list under a key ending .rulesStrings / .rulesFiles,
# Verse/DefInjectionPackage.cs:135-163), so those entries are held to the same format.
$ruleStringPattern = [regex]::new(@'
    ^
    (?<keyword>[a-zA-Z0-9_/]+)
    (
        \(
            (
                [ ]*
                (?<paramname>[a-zA-Z0-9_/]+)
                (?<paramoperator>==|=|!=|>=|<=|>|<|\[less_than\]|\[greater_than\])
                (?<paramvalue>[^\,\)]*)
                ,?
            )*
        \)
    )?
    [ ]*
    ->(?<output>.*)
    $
'@, [System.Text.RegularExpressions.RegexOptions]'ExplicitCapture, Singleline, IgnorePatternWhitespace')

function Test-RuleItem([string]$listKind, [string]$text) {
    if ($listKind -eq 'rulesStrings') { return $ruleStringPattern.IsMatch($text) }
    $parts = $text -split '->'
    return ($parts.Count -ge 2) -and ($parts[0].Trim() -match '^[a-zA-Z0-9_/]+$') -and ($parts[1].Trim().Length -gt 0)
}

function Test-RuleList([string]$listKind, [string]$owner, [string[]]$items, [string]$where) {
    foreach ($item in $items) {
        $script:ruleChecks++
        if (-not (Test-RuleItem $listKind $item)) {
            $shown = if ($item.Length -gt 60) { $item.Substring(0, 60) + '...' } else { $item }
            Add-Failure "$owner <$listKind> entry '$shown' is not 'keyword->text'; vanilla logs an error (rulesStrings: 'Bad string pass when reading rule', null keyword; rulesFiles: IndexOutOfRange) and the rule is unusable" $where
        }
    }
}

foreach ($file in Get-ChildItem -LiteralPath $defRoot -Recurse -Filter '*.xml' -File) {
    [xml]$defXml = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($listKind in 'rulesStrings', 'rulesFiles') {
        foreach ($list in $defXml.SelectNodes("//$listKind")) {
            $owner = $list.ParentNode.ParentNode.SelectSingleNode('defName')
            $label = if ($owner) { $owner.InnerText } else { $list.ParentNode.Name }
            $items = @($list.SelectNodes('li') | ForEach-Object { $_.InnerText })
            Test-RuleList $listKind $label $items (Get-RelativePath $file.FullName)
        }
    }
}

foreach ($langFile in Get-ChildItem (Join-Path $repo '1.7/Languages') -Recurse -Filter *.xml | Where-Object { $_.FullName -match '[\\/]DefInjected[\\/]' }) {
    [xml]$langXml = Get-Content -LiteralPath $langFile.FullName -Raw -Encoding UTF8
    foreach ($entry in $langXml.DocumentElement.ChildNodes) {
        if ($entry.NodeType -ne 'Element' -or $entry.Name -notmatch '\.(rulesStrings|rulesFiles)(\.\d+)?$') { continue }
        $listKind = $Matches[1]
        $items = @($entry.SelectNodes('li') | ForEach-Object { $_.InnerText })
        if ($items.Count -eq 0) { $items = @($entry.InnerText) }
        Test-RuleList $listKind $entry.Name $items (Get-RelativePath $langFile.FullName)
    }
}

# ------------------------------------------------------- world-map site textures

# SitePartDef.siteTexture and .expandingIconTexture are plain path strings: nothing checks them at load
# time, so a path that resolves nowhere shows up as a missing-texture error only once the world map is
# opened. ContentFinder looks in the mod's Textures/ first and then in the vanilla asset bundle, and that
# bundle also holds DLC art (e.g. World/WorldObjects/Expanding/Sites/AncientComplex ships there, yet only
# Data/Ideology/Defs references it). Such a path therefore loads only while the expansion is active.
# Rule: the field may name a file this mod ships, or a path some Def under Data/Core/Defs already uses.
$coreReferencedTextures = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $gameRoot 'Data/Core/Defs') -Recurse -Filter '*.xml' -File) {
    $raw = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in [regex]::Matches($raw, '<(?:texPath|iconPath|expandingIconTexture|siteTexture)>([^<]+)</')) {
        [void]$coreReferencedTextures.Add($match.Groups[1].Value.Trim())
    }
}

$worldMapTexturePattern = [regex]'<(expandingIconTexture|siteTexture)>([^<]+)</'
foreach ($file in Get-ChildItem -LiteralPath $defRoot -Recurse -Filter '*.xml' -File) {
    $raw = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in $worldMapTexturePattern.Matches($raw)) {
        $field = $match.Groups[1].Value
        $texture = $match.Groups[2].Value.Trim()
        $ruleChecks++
        if ($coreReferencedTextures.Contains($texture)) { continue }

        # Test-TextureExists would accept the DLC art above, because it reads the whole asset bundle;
        # here only the mod's own Textures/ counts.
        $base = Join-Path (Join-Path $repo 'Textures') $texture
        if ((Test-Path -LiteralPath ($base + '.png')) -or (Test-Path -LiteralPath ($base + '.dds')) -or
            (Test-Path -LiteralPath ($base + '.jpg'))) { continue }

        Add-Failure "world-map texture '<$field>$texture</$field>' is neither shipped under Textures/ nor referenced by any Def in Data/Core/Defs; it loads only through another expansion's art" (Get-RelativePath $file.FullName)
    }
}

# ------------------------------------------------------- faction settlement textures

# RimWorld.Planet/Settlement.cs:74 passes the raw FactionDef field straight to
# MaterialPool.MatFrom(path, ShaderDatabase.WorldOverlayTransparentLit, faction.Color, ...), which reaches
# ContentFinder.Get with no null check. FactionDef only fills settlementTexture when the path is
# non-empty (RimWorld/FactionDef.cs:350-352); FactionDef.SettlementTexture does return
# BaseContent.BadTex for an empty path (:344-362), but Settlement.Material does not use that property.
# A settlement-owning or player faction with an empty path therefore throws
# ArgumentNullException (Parameter 'key') inside Page_CreateWorldParams.cs:211
# Find.World.renderer.RegenerateAllLayersNow(), which also skips the Close() on :212; the world-map GUI
# then throws again every frame through Settlement.Material.
# FactionGenerator.cs:40 gives settlements to factions by settlementGenerationWeight, and a hidden
# faction owns none.
# For player factions this block also holds factionIconPath and the three name makers. Vanilla falls
# back instead of throwing for those (FactionDef.cs:364-383 returns BaseContent.BadTex;
# ScenPart_PlayerFaction.cs:57-64 and SettlementNameGenerator.cs:10-20 fall through to
# settlementNameMaker and then the world object label), so a missing one is a pink faction icon or an
# unnamed settlement, not a crash. They are still required here: a player faction inheriting the bare
# FactionBase has no other source for them.
foreach ($entry in $modDefs) {
    if ($entry.IsAbstract) { continue }
    if (-not (Test-IsA $entry.RootType 'RimWorld.FactionDef')) { continue }
    $chain = Get-DefChain $entry

    if (ConvertTo-XmlBool (Get-ScalarAt $chain @('hidden'))) { continue }

    $factionIsPlayer = ConvertTo-XmlBool (Get-ScalarAt $chain @('isPlayer'))
    $generationWeightText = Get-ScalarAt $chain @('settlementGenerationWeight')
    $generationWeight = 0.0
    if (-not [string]::IsNullOrEmpty($generationWeightText)) {
        $generationWeight = [double]::Parse($generationWeightText, [System.Globalization.CultureInfo]::InvariantCulture)
    }
    if (-not $factionIsPlayer -and $generationWeight -le 0.0) { continue }

    $ruleChecks++
    $settlementTexture = Get-ScalarAt $chain @('settlementTexturePath')
    if ([string]::IsNullOrEmpty($settlementTexture)) {
        Add-Failure "FactionDef '$($entry.DefName)' can own a settlement but has no settlementTexturePath; Settlement.Material (RimWorld.Planet/Settlement.cs:74) throws on world creation (Page_CreateWorldParams.cs:211-212)" $entry.File
    }
    elseif (-not (Test-TextureExists $settlementTexture)) {
        Add-Failure "FactionDef '$($entry.DefName)' settlementTexturePath '$settlementTexture' resolves to no texture under Textures/ or in the vanilla bundle" $entry.File
    }

    if (-not $factionIsPlayer) { continue }

    $playerFactionFields = @(
        @('factionIconPath', 'FactionDef.FactionIcon falls back to BaseContent.BadTex (RimWorld/FactionDef.cs:364-383), so the faction tab shows a missing-texture icon'),
        @('playerInitialSettlementNameMaker', 'ScenPart_PlayerFaction.PreMapGenerate names the first settlement with it (RimWorld/ScenPart_PlayerFaction.cs:57-64)'),
        @('factionNameMaker', 'FactionGenerator names the faction with it'),
        @('settlementNameMaker', 'SettlementNameGenerator names its settlements with it (RimWorld.Planet/SettlementNameGenerator.cs:10-20)')
    )
    foreach ($playerFactionField in $playerFactionFields) {
        $ruleChecks++
        $playerFactionValue = Get-ScalarAt $chain @($playerFactionField[0])
        if ([string]::IsNullOrEmpty($playerFactionValue)) {
            Add-Failure "player FactionDef '$($entry.DefName)' has no $($playerFactionField[0]); $($playerFactionField[1])" $entry.File
        }
        elseif ($playerFactionField[0] -eq 'factionIconPath' -and -not (Test-TextureExists $playerFactionValue)) {
            Add-Failure "player FactionDef '$($entry.DefName)' factionIconPath '$playerFactionValue' resolves to no texture under Textures/ or in the vanilla bundle" $entry.File
        }
    }
}

# ------------------------------------------------------------ keyed translation keys

# A key written as a string literal in front of .Translate( has to exist in the Keyed files of both
# shipped languages. Vanilla never complains about a missing key: Translator.Translate asks the
# active language first (Verse/Translator.cs:55), then the default language (:59-60, English), and
# LoadedLanguage.TryGetTextFromKey (Verse/LoadedLanguage.cs:488-492) hands back the key itself when
# neither has it. No log line is written; in DevMode PseudoTranslated (Translator.cs:109-) then
# rewrites the key letter by letter into accented lookalikes, which is the gibberish seen on a
# panel. A key missing only from ChineseSimplified is worse for being quiet: Chinese players get the
# English text. Only literals are checked: "WotF_x".Translate(...), and both arms of a
# (cond ? "WotF_a" : "WotF_b").Translate(...). Keys built by concatenation, TryTranslate/CanTranslate
# probes and wrappers such as SpellText.T are not looked at.
$keyedLanguages = @('English', 'ChineseSimplified')
$keyedKeys = @{}
foreach ($language in $keyedLanguages) {
    $languageKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo "1.7/Languages/$language/Keyed") -Recurse -Filter '*.xml' -File) {
        [xml]$keyedXml = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        foreach ($entry in $keyedXml.DocumentElement.ChildNodes) {
            if ($entry.NodeType -eq 'Element') { [void]$languageKeys.Add($entry.LocalName) }
        }
    }
    $keyedKeys[$language] = $languageKeys
}

$translateLiteralPatterns = @(
    [regex]'"(WotF_[A-Za-z0-9_.]+)"\s*\)?\s*\.Translate\(',
    [regex]'"(WotF_[A-Za-z0-9_.]+)"\s*:\s*"WotF_[A-Za-z0-9_.]+"\s*\)\s*\.Translate\('
)
$literalKeyUses = @{}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo 'Source/WhisperOfTheForest') -Recurse -Filter '*.cs' -File) {
    $raw = Get-Content -LiteralPath $file.FullName -Raw
    if ($raw.IndexOf('.Translate(', [System.StringComparison]::Ordinal) -lt 0) { continue }

    # Whole-line comments are blanked, not removed, so the line numbers below stay true.
    $code = [regex]::Replace($raw, '(?m)^[ \t]*//[^\r\n]*', '')
    foreach ($pattern in $translateLiteralPatterns) {
        foreach ($match in $pattern.Matches($code)) {
            $key = $match.Groups[1].Value
            if ($literalKeyUses.ContainsKey($key)) { continue }
            $line = ($code.Substring(0, $match.Index) -split "`n").Count
            $literalKeyUses[$key] = "$(Get-RelativePath $file.FullName):$line"
        }
    }
}

foreach ($key in ($literalKeyUses.Keys | Sort-Object)) {
    $ruleChecks++
    $missingIn = @($keyedLanguages | Where-Object { -not $keyedKeys[$_].Contains($key) })
    if ($missingIn.Count -gt 0) {
        Add-Failure "Keyed key '$key' is translated by literal but missing from 1.7/Languages/{$($missingIn -join ',')}/Keyed; the game falls back to English and, without an English entry, shows the raw key (Verse/Translator.cs:53-66, Verse/LoadedLanguage.cs:488-492)" $literalKeyUses[$key]
    }
}

Write-Host "Checked $checked Def type/texture references in 1.7/Defs"
Write-Host "Checked $ruleChecks Def structure and assembly rules"
Write-Host "Checked $harmonyClassesChecked Harmony patch classes; target chosen in code, not checked: $($dynamicPatchClasses -join ', ')"
foreach ($failure in $failures) { Write-Host "FAIL  $failure" -ForegroundColor Red }

if ($failures.Count -gt 0) {
    Write-Host "Summary: $($failures.Count) failed"
    exit 1
}

Write-Host 'Summary: 0 failed'
exit 0
