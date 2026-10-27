<#
.SYNOPSIS
Index the Whisper-of-the-Forest C# and a bounded native projectile slice into a
local graphify snapshot under Moddev/Graphify/graphify-out.

.DESCRIPTION
Reproducible local refresh for the Phase 5 migration graph. It does NOT touch the
mod implementation, the global graphify config, or any cloud backend.

  * mod/     - every *.cs under <ModRepo>/Source/WhisperOfTheForest (obj/bin skipped)
  * native/  - decompiled reference for a small, explicit set of Verse types needed
               by the Projectile migration (bodies are decompiler output, clearly
               marked in each file header; not shipped, not a full game index)

Each run copies sources to runs/<utc>/source so the snapshot is reproducible,
extracts a code-only graph, then rewrites staged paths back to the real absolute
directories (see tools/rewrite-paths.py) before publishing graph.json/metadata.json.

.EXAMPLE
  pwsh -File refresh-graph.ps1
.EXAMPLE
  pwsh -File refresh-graph.ps1 -RefreshNative -Verify
#>
[CmdletBinding()]
param(
    [string]$ModRepo = 'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest',
    [string]$OutDir = (Join-Path $PSScriptRoot 'graphify-out'),
    [string]$Python = 'C:/Users/81004/Documents/Codex/2026-10-17/task-21/.venv/Scripts/python.exe',
    [string]$GameAssembly = 'E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll',
    [string[]]$NativeTypes = @(
        'Verse.Projectile',
        'Verse.ThingWithComps',
        'Verse.Thing',
        'Verse.LocalTargetInfo',
        'Verse.ProjectileHitFlags'
    ),
    [switch]$RefreshNative,
    [switch]$Verify,
    [int]$KeepRuns = 5
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$modSourceDir = Join-Path $ModRepo 'Source/WhisperOfTheForest'
$nativeDir = Join-Path $root 'native'
$nativeSourceDir = Join-Path $nativeDir 'source'
$decompiler = Join-Path $root 'tools/decompiler/ilspycmd.exe'
$rewriteScript = Join-Path $root 'tools/rewrite-paths.py'
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$runDir = Join-Path (Join-Path $OutDir 'runs') $stamp
$stageDir = Join-Path $runDir 'source'

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function ConvertTo-RelativePath([string]$Base, [string]$Path) {
    $Path.Substring($Base.Length).TrimStart('\', '/')
}

foreach ($required in @($modSourceDir, $Python, $rewriteScript)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Missing required path: $required" }
}

# ---------------------------------------------------------------- native slice
if ($RefreshNative -or -not (Test-Path -LiteralPath $nativeSourceDir)) {
    if (-not (Test-Path -LiteralPath $decompiler)) {
        throw ("Decompiler not found: $decompiler`n" +
            'Install it inside this folder with: dotnet tool install ilspycmd ' +
            "--tool-path '$root/tools/decompiler' --version 9.1.0.7988")
    }
    if (-not (Test-Path -LiteralPath $GameAssembly)) { throw "Game assembly not found: $GameAssembly" }
    New-Item -ItemType Directory -Force -Path $nativeSourceDir | Out-Null
    $asmVersion = (Get-Item -LiteralPath $GameAssembly).VersionInfo.ProductVersion
    $asmHash = Get-Sha256 $GameAssembly
    $toolVersion = (& $decompiler --version | Select-Object -First 1)
    $nativeFiles = @()
    foreach ($type in $NativeTypes) {
        $target = Join-Path $nativeSourceDir ($type + '.cs')
        $body = & $decompiler -t $type $GameAssembly 2>&1 |
            Where-Object { $_ -notmatch '^You are not using the latest version' -and $_ -notmatch '^Latest version is' }
        $header = @(
            '// GRAPHIFY NATIVE REFERENCE - decompiled, READ-ONLY, never shipped with the mod.',
            "// Source: $GameAssembly (RimWorld Assembly-CSharp $asmVersion, sha256 $asmHash)",
            "// Tool: ilspycmd 9.1.0.7988, -t $type. Bodies are decompiler output, not original source.",
            '// Graph node line numbers for this file refer to THIS file, not to the game binary.',
            ''
        )
        $header + $body | Set-Content -LiteralPath $target -Encoding utf8
        $nativeFiles += [ordered]@{
            type     = $type
            path     = ('native/source/' + $type + '.cs')
            bytes    = (Get-Item -LiteralPath $target).Length
            sha256   = Get-Sha256 $target
            lineCount = @(Get-Content -LiteralPath $target).Count
        }
    }
    $nativeMetadata = [ordered]@{
        role               = 'bounded decompiled reference for migration-relevant native types'
        assembly_path      = $GameAssembly
        assembly_bytes     = (Get-Item -LiteralPath $GameAssembly).Length
        assembly_sha256    = $asmHash
        assembly_version   = $asmVersion
        decompiler         = $decompiler
        decompiler_version = $toolVersion
        decompiled_at_utc  = (Get-Date).ToUniversalTime().ToString('o')
        types              = $nativeFiles
    }
    $nativeMetadata | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $nativeDir 'metadata.json') -Encoding utf8
    Write-Host "[native] decompiled $($NativeTypes.Count) types from $GameAssembly"
}

# -------------------------------------------------------------------- staging
New-Item -ItemType Directory -Force -Path (Join-Path $stageDir 'mod') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stageDir 'native') | Out-Null

$modFiles = @()
$csFiles = @(Get-ChildItem -LiteralPath $modSourceDir -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' })
foreach ($file in $csFiles) {
    $rel = ConvertTo-RelativePath $modSourceDir $file.FullName
    $dest = Join-Path (Join-Path $stageDir 'mod') $rel
    New-Item -ItemType Directory -Force -Path (Split-Path -LiteralPath $dest) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $dest
    $modFiles += [ordered]@{
        path   = ('mod/' + $rel.Replace('\', '/'))
        source = $file.FullName.Replace('\', '/')
        bytes  = $file.Length
        sha256 = Get-Sha256 $file.FullName
    }
}
Get-ChildItem -LiteralPath $nativeSourceDir -File -Filter '*.cs' | Copy-Item -Destination (Join-Path $stageDir 'native')

# ------------------------------------------------------------------- git state
$headCommit = $null
$dirtyPaths = @()
try {
    $headCommit = (& git -C $ModRepo rev-parse HEAD 2>$null).Trim()
    $dirtyPaths = @(& git -C $ModRepo status --porcelain 2>$null | ForEach-Object { $_.TrimEnd() })
} catch {
    Write-Warning "git state unavailable for $ModRepo : $($_.Exception.Message)"
}

# ------------------------------------------------------------------- extraction
$env:GRAPHIFY_BACKEND = 'none'
$env:GRAPHIFY_NO_AUTO_REFRESH = '1'
$env:GRAPHIFY_QUERY_LOG_DISABLE = '1'
$env:PYTHONHASHSEED = '0'
$env:PYTHONIOENCODING = 'utf-8'
$logPath = Join-Path $runDir 'extract.log'
$extractArgs = @('-m', 'graphify', 'extract', $stageDir, '--out', $runDir, '--code-only', '--no-cluster', '--max-workers', '1')
& $Python @extractArgs *>&1 | Tee-Object -FilePath $logPath | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) { throw "graphify extract failed ($LASTEXITCODE); see $logPath" }

$stagedGraph = Join-Path (Join-Path $runDir 'graphify-out') 'graph.json'
if (-not (Test-Path -LiteralPath $stagedGraph)) { throw "graphify produced no graph at $stagedGraph" }

# --------------------------------------------------------------- path rewriting
$outDirAbs = (Resolve-Path -LiteralPath $OutDir -ErrorAction SilentlyContinue)
if (-not $outDirAbs) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null; $outDirAbs = (Resolve-Path -LiteralPath $OutDir) }
$graphMetaPath = Join-Path $runDir 'graph-meta.json'
$graphMeta = [ordered]@{
    hybrid_source_root   = $modSourceDir.Replace('\', '/')
    hybrid_indexed_at_utc = (Get-Date).ToUniversalTime().ToString('o')
    hybrid_head_commit   = $headCommit
    hybrid_native_root   = $nativeSourceDir.Replace('\', '/')
}
$graphMeta | ConvertTo-Json | Set-Content -LiteralPath $graphMetaPath -Encoding utf8

$nextGraph = Join-Path $OutDir 'graph.next.json'
& $Python $rewriteScript --graph-in $stagedGraph --graph-out $nextGraph `
    --map ("mod=" + $modSourceDir) --map ("native=" + $nativeSourceDir) `
    --graph-meta $graphMetaPath
if ($LASTEXITCODE -ne 0) { throw "path rewrite failed ($LASTEXITCODE)" }

$stats = & $Python -c @"
import json,sys
g=json.load(open(sys.argv[1],encoding='utf-8'))
nodes=g.get('nodes',[])
edges=g.get('edges',[])
csharp={n['source_file'] for n in nodes if str(n.get('source_file','')).endswith('.cs')}
rels={}
for e in edges: rels[e.get('relation')]=rels.get(e.get('relation'),0)+1
print(json.dumps({'nodes':len(nodes),'edges':len(edges),'csharp_files_with_symbols':len(csharp),'callable_nodes':sum(1 for n in nodes if n.get('_callable')),'relations':rels},sort_keys=True))
"@ $nextGraph
$extractStats = $stats | ConvertFrom-Json

$metadata = [ordered]@{
    generated_by        = 'Moddev/Graphify/refresh-graph.ps1'
    indexed_at_utc      = (Get-Date).ToUniversalTime().ToString('o')
    snapshot_source_dir = $stageDir.Replace('\', '/')
    graphify            = [ordered]@{
        python        = $Python
        command       = ($Python + ' ' + ($extractArgs -join ' '))
        backend       = 'none (code-only AST, no cloud, no LLM)'
        extract_log   = $logPath.Replace('\', '/')
        environment   = @('GRAPHIFY_BACKEND=none', 'GRAPHIFY_NO_AUTO_REFRESH=1', 'GRAPHIFY_QUERY_LOG_DISABLE=1', 'PYTHONHASHSEED=0')
    }
    mod                 = [ordered]@{
        repository       = $ModRepo
        source_root      = $modSourceDir.Replace('\', '/')
        head_commit      = $headCommit
        worktree_dirty   = ($dirtyPaths.Count -gt 0)
        dirty_entries    = $dirtyPaths
        file_count       = $modFiles.Count
        files            = $modFiles
    }
    native              = (Get-Content -LiteralPath (Join-Path $nativeDir 'metadata.json') -Raw | ConvertFrom-Json)
    path_map            = [ordered]@{
        mod    = $modSourceDir.Replace('\', '/')
        native = $nativeSourceDir.Replace('\', '/')
    }
    coverage            = [ordered]@{
        mod_csharp        = 'all *.cs under Source/WhisperOfTheForest (excluding obj/bin)'
        native_csharp     = ('decompiled reference for: ' + ($NativeTypes -join ', '))
        nodes             = $extractStats.nodes
        edges             = $extractStats.edges
        csharp_files_with_symbols = $extractStats.csharp_files_with_symbols
        callable_nodes    = $extractStats.callable_nodes
        relations         = $extractStats.relations
    }
    not_covered         = @(
        'rest of Assembly-CSharp.dll: no full-game decompile, no asset/XML/Def scanning',
        'native files are decompiled bodies of the listed types only; game XAML/Unity types (IntVec3, Vector3, Material) stay external',
        'runtime behaviour, IL of the mod DLL and Harmony patch targets are not modelled by a static AST graph',
        'shaders, textures, Def XML and RimUnity workshop assemblies are outside the snapshot'
    )
}
$nextMetadata = Join-Path $OutDir 'metadata.next.json'
$metadata | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $nextMetadata -Encoding utf8

Move-Item -LiteralPath $nextGraph -Destination (Join-Path $OutDir 'graph.json') -Force
Move-Item -LiteralPath $nextMetadata -Destination (Join-Path $OutDir 'metadata.json') -Force

# ------------------------------------------------------------- run housekeeping
$runs = @(Get-ChildItem -LiteralPath (Join-Path $OutDir 'runs') -Directory | Sort-Object Name -Descending)
if ($KeepRuns -gt 0 -and $runs.Count -gt $KeepRuns) {
    $runs | Select-Object -Skip $KeepRuns | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Recurse -Force
    }
}

if ($Verify) {
    $verifyPath = Join-Path $OutDir 'verify.json'
    & $Python -c @"
import json,sys
g=json.load(open(sys.argv[1],encoding='utf-8'))
out=sys.argv[2]
nodes=g['nodes']
probes=[]
for want in ['Projectile','ArcaneMissile','MapComponent_ArcaneMissiles','TickInterval','CheckForFreeInterceptBetween','Launch','Impact']:
    hits=[n for n in nodes if want.lower() in str(n.get('label','')).lower()]
    probes.append({'probe':want,'count':len(hits),'examples':[{'label':h.get('label'),'file':h.get('source_file'),'line':h.get('source_location')} for h in hits[:3]]})
relative=[n.get('source_file') for n in nodes if n.get('source_file') and not str(n['source_file']).startswith(('E:/','C:/','/')) and ':' not in str(n['source_file'])][:5]
json.dump({'probes':probes,'relative_path_examples':relative},open(out,'w',encoding='utf-8'),ensure_ascii=False,indent=2)
print('verify written:',out)
"@ (Join-Path $OutDir 'graph.json') $verifyPath
}

[ordered]@{
    graph    = (Join-Path $OutDir 'graph.json').Replace('\', '/')
    metadata = (Join-Path $OutDir 'metadata.json').Replace('\', '/')
    snapshot = $stageDir.Replace('\', '/')
    nodes    = $extractStats.nodes
    edges    = $extractStats.edges
} | ConvertTo-Json | Write-Host

