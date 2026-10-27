# Whisper of the Forest 交接说明（dev 分支）

本文件面向"换一台电脑继续做"的人：说明仓库里有什么、当前实现到哪一步、证据在哪、怎么最短复现，以及哪些东西**故意没有**上传。

- 仓库：`https://github.com/Neko-Yukari/Whisper-of-the-Forest`
- 本分支：`dev`
- 基线提交：`2a44f27c73d5eb3754b60d475ca476a87c97097e`（原 `feat/rimworld-native-projectile-lab-a20-20261027` 末端，仍是稳定模组快照）
- 主线 `main`：`8646749bbf73872641d91390d5498c52bfd0650b`（未改动）

## 1. 落地步骤（新机器）

已有干净目录时（**不要**对带本地改动的仓库用 `reset --hard`）：

```powershell
git fetch origin
git switch dev            # 若本地还没有 dev，用 git switch -c dev --track origin/dev
git pull --ff-only origin dev
```

全新克隆：

```powershell
git clone https://github.com/Neko-Yukari/Whisper-of-the-Forest.git
cd Whisper-of-the-Forest
git switch dev
```

拿到 `dev` 之后，`1.7/Assemblies/WhisperOfTheForest.dll` 是已构建产物，部署进游戏即可跑（见第 8.1 节）；只有重新编译才需要本机游戏程序集（见第 7 节）。

## 2. 目录地图

| 路径 | 内容 |
| --- | --- |
| `About/`、`Defs/`、`Languages/`、`Textures/` | 模组本体（精灵种族、命名规则、Trait 注入） |
| `1.7/` | 1.7 版加载内容：`Assemblies/WhisperOfTheForest.dll`、`Defs/Experiments/*.xml`、中英本地化 |
| `Source/WhisperOfTheForest/` | 全部自主 C# 源码（实验/射弹/移动/UI） |
| `Tools/*.ps1` | 构建、Def 校验、实验室启动与验证脚本（仓库内版本） |
| `Experiments/*.md` | 各阶段实验记录与结论（a18/a19/a20 为主） |
| `IMPLEMENTATION_PLAN.md`、`MODLOG.md` | 计划与变更日志 |
| `Research/` | **本次新增**：可迁移的探索成果（工具源码、结构化结果、图谱重建脚本、本说明） |

`Research/` 的来源映射与"未上传清单"见 `Research/SOURCES.md`；原始素材在旧机器上的 `Moddev/` 目录，不在本仓库内。

**整个仓库根**就是 mod 目录：`LoadFolders.xml` 的 `v1.7` 同时加载 `/` 和 `1.7`，所以 `About/About.xml`、`Defs/`、`Languages/`、`Textures/` 与 `1.7/` 必须在一起才能跑，只拷 `1.7/` 不完整。部署方式见第 8.1 节；`Research/`、`Source/`、`Tools/`、`Experiments/` 不参与游戏加载。

## 3. 当前架构（a20 原生射弹迁移后的状态）

射弹已经改成**原版 `Projectile` 子类**，不再由自建模拟列表驱动：

- `Source/WhisperOfTheForest/Missiles/Projectile_ArcaneMissiles.cs`：`Projectile_ArcaneMissiles : Projectile`。飞行状态、转向、命中判定全部由该对象自己持有；每 tick 由原版 `TickInterval` 进入既有运动逻辑，命中结算交给 `impactDef` 指定的 worker。
- `CompAbilityEffect_WotFArcaneMissiles.cs`：Ability 施放入口，读取 `settings` 后用 `ThingMaker`/`GenSpawn` 生成射弹实体。
- `MapComponent_ArcaneMissiles.cs`：**只读**——查询/枚举存活射弹，不保存模拟列表、不驱动飞行、不逐弹驱动视觉。
- 视觉后端由 `visualDef` 选择：`ArcaneMissileVisualRimUnity`（RimUnity 粒子）与 `ArcaneMissileVisualNative`（原版 fleck）。命中由 `impactDef` 选择：`WotF_Impact_Damage` / `WotF_Impact_Heal` / `WotF_Impact_Explode`。两个 Def 字段都用 `Scribe_Defs` 存档，读档后后端一致。
- **未接入**：原版直线推进与概率命中没有启用（保留自定义几何碰撞与转向）；原版护盾拦截链也没有接，没有对应实测。旧 a19 自定义射弹在飞行中不会被转换（设计上不转换，未实机验证迁移表现）。
- 实验/观测设施：`Bootstrap` + `Lab/LabHost.cs`、`LabCommands.cs`、`ExperimentLog.cs`、`BoundedJsonlWriter.cs`。用启动参数 `-wotflab=<目录>` 打开，命令以文本行写入 `<目录>/requests.txt`，结果写 `results.jsonl`，事件写 `experiment.jsonl`，启动状态写 `lab-status.json`。日志有界，避免长跑膨胀。
- UI：`UI/Window_TechLab.cs` 实验窗口、`UI/Patch_PlaySettingsRowEntry.cs` 原生行入口、`UI/TechLabSettings.cs` 位置与可见性持久化、`UI/TechLabStyle.cs` 样式。
- 移动实验：`Movement/BlinkRule.cs`（瞬移逐格推进）、`Movement/MovementPatches.cs`、`StrideApparel.cs`（地形成本归零装备）、`CompStrideLog.cs`/`StrideDiagnostics.cs`（观测）。Def 在 `1.7/Defs/Experiments/Movement.xml`。

## 4. 三个预设与主要参数

三个 `AbilityDef`（`1.7/Defs/Experiments/Missiles.xml`），都继承 `PsycastBase`（`MayRequire="Ludeon.RimWorld.Royalty"`），`level=1`、`range 24.9`、`warmupTime 0.5`、`cooldownTicksRange 90~120`，实验室用 `Ability_EntropyGain=0`、`Ability_PsyfocusCost=0`（正式玩法应上调）。快捷键只有基础技能设了 `hotKey Misc11`（第 95 行）；`_Heavy` 和 `_Mender` 没有设 hotKey。

| Def | 弹数 | 速度（格/秒） | 转向（°/秒） | 命中 | 关键覆写 |
| --- | --- | --- | --- | --- | --- |
| `WotF_ArcaneMissiles` | 3 | 13 | 300 | `WotF_Impact_Damage` | 伤害 7、破甲 0.5、散布 8°、摆动 0.14@2.2Hz、`lifetimeSeconds 10`、`maxRangeCells 34`、`explosionRadius 1.7`、`healAmount 10`、`missileSize 0.55`、`trailTime 0.35`、视觉 `WotF_Visual_RimUnity` |
| `WotF_ArcaneMissiles_Heavy` | 1 | 13 | 10 | `WotF_Impact_Explode` | `ignoreFlightLimits=true`、`expireOnTargetLost=false`（慢转向重弹，按"13/10"记录） |
| `WotF_ArcaneMissiles_Mender` | 1 | 4 | 720 | `WotF_Impact_Heal` | `ignoreFlightLimits=true`、`expireOnTargetLost=true`，只可对 pawn/animal/human/self，治疗量沿用默认 10 |

命中 Def 与视觉 Def 都是可替换的：新增后端只需实现 `ArcaneMissileVisualSource` 或 `ArcaneMissileImpactWorker` 的基础类，在 XML 里给 workerClass 并让预设字段指向新 Def，不需要改追踪/碰撞/存档/清理核心（接入步骤见 `Experiments/missiles.md` 第 2 节）。

## 5. 结论与证据层级

可信度从高到低，写报告时按这个顺序引用：

1. **实机结构化结果**：`Research/Results/<run>/results.jsonl`（命令回显 + 世界状态）、`experiment.jsonl`（事件流，带 tick）。每条有 `c...` 命令 ID，可按 ID 复查。
2. **存档快照**：只在旧机器上（未上传，见第 9 节），用于证明"飞行中对象可存档、可恢复"。
3. **生产代码 harness**：`Research/Tools/check-missiles/motion-harness`，直接编译并运行生产逻辑文件（只替换 Verse 引擎类型），a20 对应 52 项。
4. **静态检查**：`Tools/verify.ps1`（a20：29 PASS / 0 WARN / 0 FAIL）、`Tools/check-defs.ps1`（19 个引用 / 0 缺失）。
5. **IL / 反编译阅读**：仅作为"原版为什么这样表现"的旁证，不单独作为通过依据；反编译产物本身没有上传。

场景编号 A01–A33 与逐条结论在 `Experiments/acceptance.md`，命中/伤害/边界的细节在 `Experiments/integration.md`（§9、§11）与 `Experiments/missiles.md`。

a20 主结论（对应 A26–A33）：

- 射弹所有权已交给原生对象（A26）。
- 三种预设都用真实 Ability 施放过，`impact=1` 或三发三命中（A28）。
- 原生对象能存档、能读档继续飞（A29、A32：`age=152 → 153`、位置推进一 tick）。
- 追踪移动目标：目标 DrawPos 连续变化时射弹 `heading` 从 `(0.124,-0.992)` 转到 `(-0.572,-0.82)`，全程 `live=1`（A31）。
- 最终 DLL `122,880` 字节，SHA256 `c08464fdfb2a51bf77951623d0a7c35b33ff91d18b2bc9df86c45876f51eb0c1`，与本仓库 `1.7/Assemblies/WhisperOfTheForest.dll` 一致（A27）。

## 6. 已测 / 未测

已实测（有实机证据）：施放与三发齐射、追踪移动目标、地面/建筑命中、高速子步碰撞、伤害/治疗/爆炸三种结算、飞行中存档读档、射弹越过地图边界、Mender 的 `cast` 入口（a20 `c065432664213`，a19 只测过同 Def 的 `fire`）、UI 窗口开关与入口位置持久化、地形装备与瞬移的路径/门/墙角行为。

未实测或只有代码级证据（**不要**当成已验证）：

- 原版护盾/拦截链（未接入，也无实测）。
- 真实多地图切换、`MapRemoved`、目标死亡/离图清理（只有 harness 与 IL 证据）。
- 旧 a19 自定义射弹在飞行中的迁移（设计上不转换，未实机验证）。
- 粒子与拖尾的观感、残留（生命周期代码与"无报错"有证据，画面由人验收）。
- 暂停单步时 DrawPos 冻结，不作为规避结论。

## 7. 依赖与环境适配

- 游戏：RimWorld `1.7.4990.1069`（日志里还有 `1.7.4985 rev1269` 的读档提示，属正常）。运行时是 `.NET 10.0.13`（`waylithclr` 分支）。
- 编译目标 `net10.0`，需要自备 .NET 10 SDK（`dotnet --version` 应为 10.0.x）。
- **不要用 `Tools/build.ps1` 当换机入口**：它的 `param` 只有 `-Configuration/-Clean/-Verify`，没有透传 MSBuild 属性的开关；而且它固定调用旧机器父目录的便携 SDK（`Moddev/tools/dotnet/dotnet.exe`，未上传），所以在别的机器上会直接报"SDK not found"。
- 换机器直接在仓库根用 dotnet CLI（先关掉游戏，否则 `1.7/Assemblies/WhisperOfTheForest.dll` 被占用会失败）：
  ```powershell
  dotnet build ./Source/WhisperOfTheForest/WhisperOfTheForest.csproj -c Release `
    -p:GameRoot="D:/Games/RimWorld" `
    -p:WorkshopRoot="D:/Steam/steamapps/workshop/content/294100"
  ```
  `OutputPath` 已写在 csproj 里（`1.7/Assemblies/`），产物会直接落在那。
- 两个属性的默认值写死在 csproj（`GameRoot` 默认 `E:/SteamLibrary/steamapps/common/RimWorld`，`WorkshopRoot` 默认 `E:/SteamLibrary/steamapps/workshop/content/294100`），换机器用 `-p:` 覆盖即可，不必改文件：
  - `GameRoot` → 本机 RimWorld 根目录，取 `Assembly-CSharp.dll`、`UnityStubs.dll`、`Waylith.dll`、`com.rlabrecque.steamworks.net.dll`。
  - `WorkshopRoot` → 创意工坊 `workshop/content/294100`，需要 `2009463077`（Harmony）、`3819570122`（RimUnity）、`839005762`（AlienRaces/HAR）。
- 游戏程序集、Workshop DLL、SDK、存档、纹理缓存都**不打包**：新机器本地安装后填路径即可。
- 旧构建脚本 `Tools/build.ps1` 保留历史说明用；它只在原 `Moddev/` 目录布局（含 `tools/dotnet` 便携 SDK）下可运行。

## 8. 最短复现 / 观察流程（只写步骤，本次未执行）

### 8.1 部署成 mod 目录

`LoadFolders.xml` 的 `v1.7` 同时加载 `/` 和 `1.7`，**整个仓库根**才是 mod 目录；只拷 `1.7/` 会缺 `About/About.xml`、根 `Defs/`、`Languages/`、`Textures/`。两种做法：

- 把仓库工作目录整个放进 `<RimWorld>/Mods/`；或
- 在 mods 目录做一个指向仓库的联接：
  ```powershell
  New-Item -ItemType Junction -Path "<RimWorld>/Mods/Whisper-of-the-Forest" -Target "<repo>"
  ```

部署后核对：`About/About.xml`、`Defs/`、`1.7/Defs/Experiments/*.xml`、`1.7/Assemblies/WhisperOfTheForest.dll` 都在同一棵树里。`Research/`、`Source/`、`Tools/`、`Experiments/` 不参与加载。

### 8.2 依赖与隔离配置

必需：`brrainz.harmony`（workshop 2009463077）、`chezhou.rimunity`（3819570122）、`erdelf.humanoidalienraces`（839005762，HAR），以及官方 `ludeon.rimworld` + `ludeon.rimworld.royalty`（三个技能继承 `PsycastBase`，**必须启用 Royalty**）。

旧机器的隔离清单（只读核对，未上传全文）是 harmony → rimunity → 核心/DLC → HAR → 本 mod，测试时另挂了 CharacterEditor。新机器**新建**一个隔离 profile，只放这份 `Config/ModsConfig.xml`，不要动你正常游戏的配置：

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData>
  <version>1.7.4990 rev1069</version>
  <activeMods>
    <li>brrainz.harmony</li>
    <li>chezhou.rimunity</li>
    <li>ludeon.rimworld</li>
    <li>ludeon.rimworld.royalty</li>
    <li>erdelf.humanoidalienraces</li>
    <li>nekoyukari.whisperoftheforest</li>
  </activeMods>
  <knownExpansions>
    <li>ludeon.rimworld.royalty</li>
  </knownExpansions>
</ModsConfigData>
```

`<version>` 用本机游戏版本串，`knownExpansions` 按实际装了的扩展追加。

### 8.3 启动（不传 `-mod`）

1.7 的 `ModsConfig.ApplyCommandLineModList` 只认 `Mods/` 目录下的包，`-mod=` 里的 Workshop 包会被忽略（旧机器日志原文：`-mod: 'brrainz.harmony' is not in the local Mods folder, ignoring`，见 `Experiments/integration.md` 第 424 行的启动参数小结）。所以顺序只能靠 ModsConfig.xml，命令行不要带 `-mod`：

```
<RimWorld>/RimWorld.exe -savedatafolder=<profileRoot> -wotflab=<labRoot> -quicktest
```

`<profileRoot>` = 8.2 的隔离配置目录，`<labRoot>` = 实验目录（游戏会往里写 `requests.txt`/`results.jsonl`/`experiment.jsonl`/`lab-status.json`）。前台直接跑或后台拉起都行，例如 `Start-Process -FilePath "<RimWorld>/RimWorld.exe" -ArgumentList "-savedatafolder=...","-wotflab=...","-quicktest"`，或用 WMI `Win32_Process.Create` 起（这样不继承当前控制台）。

**不要复用 `Tools/launch-lab.ps1`**：它的第 77 行仍拼 `-mod=$Mods`，按上面的原因会被整条忽略，结果只加载官方包；该脚本只作为历史说明保留。

### 8.4 观察流程（用仓库内 `Tools/lab.ps1` 发命令）

`pwsh -File Tools/lab.ps1 -LabDir <labRoot> -Command "<命令>"` 会把 `<id> <命令>` 追加进 `requests.txt` 并等待同 id 的结果行。协议是 `<id> <verb> [key=value ...]`（**没有位置参数**，目标也是 key=value），动词清单见 `Source/WhisperOfTheForest/Lab/LabCommands.cs` 第 33 行起的 `switch`。

最短一轮，所有 ID 都取**本次真实返回**：

1. `status` → 确认 `harmonyPatched=true`、`gameLoaded=true`、`defs` 里三个预设为 `true`。
2. 进一张**可丢弃**的图（`-quicktest` 新图或新建），发 `setup confirm=1` → 返回里 `setup.pawns[]`（`role` 为 `human`/`elf` + `id`）、`setup.walls[]`、`setup.door`、`setup.lane`。实体 ID 每次随机，必须现取。
3. `select id=<elfId>` → 选中施法者；`grant` → 给选中的小人授予三个飞弹技能（无参数）。
4. `cast preset=WotF_ArcaneMissiles_Mender id=<targetId>`；对地面/坐标目标改成 `x=<格子x> z=<格子z>`；换弹种就换 `preset=WotF_ArcaneMissiles` / `..._Heavy`。
5. `time speed=1` 让它飞；期间反复 `missiles` 取 `missiles.frames[0].ageTicks / headingX / targetX`，需要完整世界快照时用 `observe`（`missiles reset=1` 清计数）。
6. 命中后看 `missiles.live=0`、`impacts=1`、`expired` 的变化；然后 `time speed=0`、`save name=<新名字>`（写到 `<profileRoot>/Saves`，**不要**沿用旧名）。

`Research/Results/lab-run/requests.txt` 只是历史 transcript：里面的实体 ID、坐标、存档名（如 `a20-moving-flight`）都是旧会话的，`setup` 之后新 ID 随机，**不能原样重放**，只能当命令写法参考。旧 a18/a19/a20 存档没有上传，所以**无法恢复旧现场**，只能在新区图里建等价场景，别把新结果说成"精确重放"。

### 8.5 静态核对

`Tools/verify.ps1`、`Tools/check-defs.ps1` 是旧机器目录结构下的脚本，都带本机绝对路径（`check-defs.ps1` 第 9 行 `CecilPath`、第 14 行 `gameRoot`），换机器先改这两处再跑。更硬的逻辑核对用 `Research/Tools/check-missiles/motion-harness`（自包含，编进生产逻辑文件 + 引擎 shim，不需要游戏 DLL）。

## 9. 明确未上传的内容

完整清单和逐个哈希在 `Research/SOURCES.md`（旧机器路径 + 大小 + SHA256 + 原因）。要点：

- **存档**（`lab-profile/Saves/*.rws`，单个 6–22 MB）：含实验现场，体积大且是机器本地状态。已记录文件名、字节数与哈希；旧现场无法恢复，重建等价场景见第 8.4 节。
- **完整 Player.log**：只上传脱敏摘录（`Research/Results/Logs/`），原始日志留在旧机器。
- **反编译产物**：`Graphify/native/source/Verse.*.cs`、`*.il`、`*.txt` API dump 都是第三方游戏代码/反编译输出，不上传；重建方法见 `Research/Graphify/NOTES.md` 与 `Research/Tools/README.md`。
- **Graphify 缓存**：`graphify-out/`（含 `graph.json`、AST cache、venv）不上传，可重建。
- **SDK 与构建产物**：便携 .NET SDK、`bin/`、`obj/`、`out*/WhisperOfTheForest.dll`（模组自身的 DLL 只保留 `1.7/Assemblies/WhisperOfTheForest.dll`）。
- **维护会话的进度条改动**：另一维护会话把 `1.7/Defs/Experiments/Missiles.xml` 里三个 AbilityDef 的 `showCastingProgressBar` 改成 `false` 并加了三行注释。这属于那边的改动，**本分支仍是 `true` 且没有那三行注释**；本地机器上的工作区保留 `false` 版本，未提交。

## 10. 已知局限与下一步

- 护盾/拦截链未接入：要让射弹受原版护盾拦截，需要在发射与命中路径上对接 shield intercept，并补实机验收。
- 多地图/目标失效清理只有代码级证据，值得补一轮实机（切图、地图移除、目标死亡）。
- `MapComponent_ArcaneMissiles` 目前是只读视图，若以后要做"全局统计/批量清理"，注意不要退回成模拟列表。
- 旧 a19 存档里飞行中的自定义射弹不会被转换（设计上不转换；旧档读入后的具体表现未实机验证）；如需兼容要写迁移代码。
- 移动实验（Blink/Stride）仍是实验性质，未并入正式玩法平衡。
- `Research/Tools/` 里的脚本保留了旧机器的绝对路径（`E:/SteamLibrary/...`、`C:/Users/<user>/...`），迁移时要按 `Research/Tools/README.md` 逐项替换；本次没有改动这些脚本内容。
