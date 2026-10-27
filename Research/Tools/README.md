# Research/Tools — 实验工具

这些是旧机器 `Moddev/tools/` 下自写工具的**原样副本（未改一行内容）**，用于逻辑校验、静态检查、IL 探针和产物比对。原件路径映射见 `../SOURCES.md`。文本按仓库 `core.autocrlf` 规范为 LF 入库，与原件只差换行符。

## 迁移必读：需要改的常量

复制时没有改写脚本，所以里面仍是旧机器路径。用之前按下表替换（`<repo>` = 本仓库根，`<game>` = 本机 RimWorld 根，`<workshop>` = `<Steam>/steamapps/workshop/content/294100`）：

| 工具 | 需要改的常量 | 旧值 |
| --- | --- | --- |
| `check-missiles/check_defs.py`、`check_lang.py`、`check_refactor.py` | `BASE`、`MOD` | `E:/SteamLibrary/.../Moddev/tools/check-missiles`、`E:/.../Moddev/Whisper-of-the-Forest` |
| `check-missiles/motion-harness/WotF_MissileMotionHarness.csproj` | `ModSource`（默认按旧目录层级推算） | `$(MSBuildThisFileDirectory)../../../Whisper-of-the-Forest/Source/WhisperOfTheForest` |
| `check-missiles/motion-harness/analysis/WotF_MissileAnalysis.csproj` | `ModSource` | `$(MSBuildThisFileDirectory)../../../../Whisper-of-the-Forest/Source/WhisperOfTheForest` |
| `check-missiles/WotF_MissilesCheck.csproj` | `ModSource`、`GameRoot`、`WorkshopRoot` | 同上 / `E:/SteamLibrary/steamapps/common/RimWorld` |
| `check-movement/*.csproj` | 各 `HintPath`（写死绝对路径） | `E:/SteamLibrary/...` |
| `check-movement/il/*.ps1`、`verify-*.ps1`、`list-*.ps1` | `Assembly-CSharp.dll` 与本项目 `bin/` 输出路径 | `E:/SteamLibrary/...` |
| `check-ui/*.ps1` | `$AssemblyPath`、`$mod` | `E:/.../tools/check-ui/proj-real/bin/Debug/net10.0/WotF_UI_Check_Real.dll` |
| `check-ui/proj/*.csproj`、`proj-real/*.csproj` | `GameDir`、`HarmonyDir` | `E:/...`、`<workshop>/2009463077/1.7/Assemblies` |
| `check-elf/*.ps1` | `-Repo`、`-GameData` 参数默认值 | `E:/.../Whisper-of-the-Forest`、`E:/.../RimWorld/Data` |
| `evidence-driftcheck/DriftCheck.csproj` | `Compile Include` 的模组源码绝对路径 + 各 `HintPath` | `E:/.../Moddev/Whisper-of-the-Forest/Source/...` |
| 所有用到 Mono.Cecil 的 `.ps1` | `Add-Type -Path .../dotnet-ildasm/0.12.2/.../Mono.Cecil.dll` | 旧机器 `~/.dotnet/tools/.store/...`（脚本里写的是旧机用户目录的绝对路径，非凭据） |
| `Graphify/refresh-graph.ps1` | `-Python`（venv）、`-ModRepo`、`-GameAssembly` | 旧机器 venv 与 `E:/.../RimWorld` |

也可以用命令行覆盖而不改文件，例如：

```powershell
dotnet build Research/Tools/check-missiles/motion-harness/WotF_MissileMotionHarness.csproj `
  -p:ModSource=<repo>/Source/WhisperOfTheForest
```

## 各工具用途

| 路径 | 用途 | 外部依赖 |
| --- | --- | --- |
| `check-missiles/motion-harness/` | 把**生产逻辑文件**（`ArcaneMissile*`、`Projectile_ArcaneMissiles`、`MapComponent`、launcher、impact worker）原样编译进来，只替换 Verse 引擎类型（`Shims.cs`），对转向/子步碰撞/命中结算/上限/寿命逐项跑断言。a20 为 52 项。 | 无游戏 DLL（这是它最大的价值：换机器也能跑） |
| `check-missiles/motion-harness/analysis/` | 只读推算：两个候选参数档在"人类 MoveSpeed 4.6 土壤成本"下的逃逸/命中边界（关掉射程与寿命，只由到达或离图结束）。 | 同上 |
| `check-missiles/check_defs.py` | XML → C# 字段映射校验：每个写在 XML 里的元素必须能对上公开字段，`Class` 必须可解析。a20：19 引用 / 0 缺失。 | `abilitydef.txt` 等字段 dump（未上传，见下） |
| `check-missiles/check_lang.py` | DefInjected/Keyed 语言键与 Def 字段的一致性检查。 | 同上 |
| `check-missiles/check_refactor.py` | 校验预设的默认 `visualDef`/`impactDef` 指向，防止重构后默认值漂移。 | 同上 |
| `check-missiles/dump.ps1`、`il.ps1`、`callsites.ps1` | 用 Mono.Cecil 导出类型/方法/IL/调用点（产出上面那些 `.txt`）。 | Mono.Cecil、游戏程序集 |
| `check-missiles/make_icon.py` | 生成 AbilityDef 图标 PNG。 | Pillow |
| `check-movement/` | 移动/装备 Patching 校验（`verify-patches.ps1` 检查 Harmony 目标与参数绑定、`verify-worn-textures.ps1` 检查穿着贴图、`check-integration.csproj` 编译集成校验）。 | 游戏程序集 + Harmony DLL |
| `check-ui/` | UI 静态校验（Harmony 目标与参数绑定、语言键扫描、入口行 patch 检查）与两个最小编译项目。 | 游戏程序集 + Harmony DLL + Mono.Cecil |
| `check-elf/` | 精灵种族 DefInjected 与语言注入检查。 | 本仓库 + `RimWorld/Data` |
| `evidence-driftcheck/DriftCheck.csproj` | 编译一份"当前源码"并和已部署 DLL 比对哈希/类型，验证产物没有漂移。 | 游戏程序集 + 各模组 DLL |

## 未随工具上传的输入文件

`check_defs.py` 等脚本依赖旧机器上由 `dump.ps1`/`il.ps1` 生成的字段与 IL dump（如 `abilitydef.txt`、`myfields.txt`、`ru-all.txt`）。这些是**反编译游戏/程序集输出**，按规则不入库。重建方式：

```powershell
# 1) 装 Mono.Cecil（或复用已有 dotnet-ildasm 安装）
dotnet tool install --global dotnet-ildasm
# 2) 指向本机程序集后重跑 dump
pwsh -File Research/Tools/check-missiles/dump.ps1      # 生成字段/类型清单
pwsh -File Research/Tools/check-missiles/il.ps1        # 生成 IL 片段
```

不重建也可以直接用 `motion-harness`（自包含）和仓库根 `Tools/verify.ps1`、`Tools/check-defs.ps1` 做主要校验。
