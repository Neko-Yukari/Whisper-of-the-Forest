#requires -Version 7.0
<#
  Cross-checks 1.7 Def XML against the actual assemblies and texture files:
    - Class="..." / <thingClass> / <workerClass> must exist in our DLL, Assembly-CSharp or AlienRace
    - <texPath>/<iconPath>/<wornGraphicPath> must resolve to a file under Textures/

  Structural rules mirror the decompiled vanilla load path
  (../Graphify/native/source, read-only reference):
    1. Def root element must resolve to a Def type.
       Verse/DirectXmlToObjectNew.cs:185 - "Type X is not a Def type or could not be found"
       GenTypes.cs:53 - short names only resolve inside RIMWORLD/VERSE/LUDEONTK/... namespaces
    2. Apparel worn graphics are suffixed with _<BodyType> unless the last layer is
       Overhead/EyeCover, RenderAsPack() is true, or the path is a vanilla placeholder.
       RimWorld/ApparelGraphicRecordGetter.cs:20, Verse/PawnRenderUtility.cs:38,
       RimWorld/ApparelProperties.cs:105 (LastLayer = last entry of <layers>)
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
#>
[CmdletBinding()]
param(
    [string]$CecilPath = 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll',
    [string]$Repo = (Split-Path -Parent $PSScriptRoot)
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

$modAssemblyPath = Join-Path $repo '1.7/Assemblies/WhisperOfTheForest.dll'
$catalogAssemblies = @(
    @{ Path = $modAssemblyPath; Mod = $true; Required = $true },
    @{ Path = (Join-Path $gameRoot 'Assembly-CSharp.dll'); Mod = $false; Required = $true },
    @{ Path = (Join-Path $workshop '839005762/1.7/Assemblies/AlienRace.dll'); Mod = $false; Required = $true },
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
    $guard = 0
    while ($null -ne $current -and $guard -lt 64) {
        if ($current.Full -ieq $targetFullName) { return $true }
        if ([string]::IsNullOrEmpty($current.Base)) { return $false }
        $next = $null
        if (-not $typeByFull.TryGetValue($current.Base, [ref]$next)) { return $false }
        $current = $next
        $guard++
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

function Test-TextureExists([string]$texturePath) {
    $base = Join-Path (Join-Path $repo 'Textures') $texturePath
    return (Test-Path -LiteralPath ($base + '.png')) -or
           (Test-Path -LiteralPath ($base + '.dds')) -or
           (Test-Path -LiteralPath ($base + '.jpg'))
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

    foreach ($match in [regex]::Matches($raw, '<(?:texPath|iconPath|wornGraphicPath)>([^<]+)</')) {
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
        try {
            [xml]$document = Get-Content -LiteralPath $file.FullName -Raw
        } catch {
            continue
        }
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
                if (-not $skipBodyTypeSuffix) {
                    $missing = @()
                    foreach ($bodyType in $bodyTypes) {
                        $suffixed = $wornGraphicPath + '_' + $bodyType
                        if (-not (Test-TextureExists $suffixed)) {
                            $missing += ('_' + $bodyType)
                        }
                    }
                    if ($missing.Count -gt 0) {
                        Add-Failure "apparel '$($entry.DefName)' wornGraphicPath '$wornGraphicPath' (layer '$lastLayer') is missing body-type textures: $($missing -join ', ')" $entry.File
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
    $guard = 0
    while ($null -ne $current -and $guard -lt 64) {
        if ($current.Attributes -contains 'StaticConstructorOnStartup') {
            $hasStaticConstructorAttribute = $true
            break
        }
        if ([string]::IsNullOrEmpty($current.Base)) { break }
        $next = $null
        if (-not $typeByFull.TryGetValue($current.Base, [ref]$next)) { break }
        $current = $next
        $guard++
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

Write-Host "Checked $checked Def type/texture references in 1.7/Defs"
Write-Host "Checked $ruleChecks Def structure and assembly rules"
foreach ($failure in $failures) { Write-Host "FAIL  $failure" -ForegroundColor Red }

if ($failures.Count -gt 0) {
    Write-Host "Summary: $($failures.Count) failed"
    exit 1
}

Write-Host 'Summary: 0 failed'
exit 0
