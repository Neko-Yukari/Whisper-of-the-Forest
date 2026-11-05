# 1.7 实验集成、构建与 Lab 协议

本文件是集成层（工程、Bootstrap、Lab、构建/启动脚本、About/LoadFolders）的交付说明。
功能实现由各自负责人写入 `Source/WhisperOfTheForest/{Missiles,Movement,UI}` 与 `1.7/Defs/Experiments`。

## 1. 工具链（已核验）

| 项 | 值 |
| --- | --- |
| SDK | **10.0.402**（runtime 10.0.13，官方 stable） |
| 来源 | `https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.402/dotnet-sdk-10.0.402-win-x64.zip` |
| SHA512 | `ae4fd528cb7e9f6af6aaf7de36715a69f761f183744f1bb3025d19f5a92447090ec561410e46266c1d10a63781ed6f3de771760bc76c876d87daabc367b898cd`（`Get-FileHash -Algorithm SHA512` 实测一致） |
| 位置 | `Moddev/tools/dotnet/dotnet.exe`，便携解压；未改 PATH、未运行安装器、未注册 |
| 隔离 | 构建脚本设置 `DOTNET_CLI_HOME=Moddev/tools/dotnet-home`、`DOTNET_MULTILEVEL_LOOKUP=0` |

功能方在自己的 check 目录编译时可复用同一 SDK；不要使用系统 `dotnet`（最高 9.0.319）。

## 2. 工程与产物

- 工程：`Source/WhisperOfTheForest/WhisperOfTheForest.csproj`
- `net10.0`，默认 glob 编译 `Source/WhisperOfTheForest/**/*.cs`（功能方新增 `.cs` 无需改工程）
- 单程序集输出：`1.7/Assemblies/WhisperOfTheForest.dll`
- 所有游戏/依赖程序集引用 `Private=false`，不会复制进 repo
- 引用：`Assembly-CSharp`、`UnityStubs`、`Waylith`、`com.rlabrecque.steamworks.net`、`0Harmony`(3.0.0.0)、`RimUnity.Core`、`RimUnity`、`AlienRace`
- 构建：

```powershell
pwsh -File Tools/build.ps1            # Release，增量
pwsh -File Tools/build.ps1 -Clean     # 清 obj/bin/DLL 后重建
pwsh -File Tools/build.ps1 -Verify    # 构建 + 静态布局检查
```

独立静态检查：

```powershell
pwsh -File Tools/verify.ps1
pwsh -File Tools/verify.ps1 -RequireFeatureDefs   # 最终验收：9 个实验 Def 必须存在
```

## 3. 版本隔离

`LoadFolders.xml`：

- 1.6：只加载 `/`（原精灵种族 About/Defs/Languages/Textures）
- 1.7：`/` + `1.7`（追加 Assemblies、Defs/Experiments、Data、实验本地化）

`About/About.xml` 保留原 1.6 内容，仅对 1.7 声明依赖：Harmony、HAR、RimUnity、Royalty。
`1.7` 目录内是 net10 程序集，因此 1.6 永远看不到实验 DLL。

## 4. 公共 C# 契约（已实现）

根命名空间：`WhisperOfTheForest.Experimental`。

```csharp
public static class ExperimentLog
{
    public static void Record(string category, Verse.Pawn actor, string detail); // actor 可为 null
}

public static class LabActions
{
    // UI 绑定用：失败时抛 InvalidOperationException(LastError)，UI 的 try/catch 可显示失败。
    public static void GrantPsycast(Pawn pawn);
    public static void EquipTerrainStride(Pawn pawn);
    public static void EquipBlinkStride(Pawn pawn);
    public static void RemoveExperimentApparel(Pawn pawn);

    // Lab 用：不抛异常，返回是否成功。
    public static bool TryGrantPsycast(Pawn pawn);
    public static bool TryEquipTerrainStride(Pawn pawn);
    public static bool TryEquipBlinkStride(Pawn pawn);
    public static bool TryRemoveExperimentApparel(Pawn pawn);

    public static string LastError { get; }
}
```

- `Bootstrap` 只执行一次 `new Harmony("NekoYukari.WhisperOfTheForest.Experiments").PatchAll()`。
- `LabActions` 公开三个 defName 常量：`ArcaneMissilesDefName`、`ArcaneMissilesHeavyDefName`、`ArcaneMissilesMenderDefName`；grant 使用这三者。
- `GrantPsycast`：只作用于传入 Pawn；用原版方式在 **brain** 部位创建 `PsychicAmplifier`（`HediffMaker.MakeHediff(def, pawn, brain)` + `AddHediff(hediff, brain, ...)`），再用 `Hediff_Psylink.ChangeLevel` 提升到**所有已加载导弹 AbilityDef** 的最高 `level`，补满 psyfocus，逐个 `GainAbility`。授予集合在运行时从 `DefDatabase<AbilityDef>` 中发现：凡是 `comps` 含 `CompProperties_AbilityWotFArcaneMissiles` 的 Def 都算，新增 XML 预设无需改 C# 名单；三个旧常量仅作默认入口/明确测试。已有技能跳过不重复添加；一个都没发现或 `GainAbility` 后仍缺失都返回 false 并写入明确 `LastError`（不静默 partial 成功）。已有 Part 为 null 的旧损坏植入体会先移除再重建。
- `Equip*`：只给传入 Pawn 穿对应 Def；已穿则视为成功。
- `RemoveExperimentApparel`：只丢弃/移除 `WotF_TerrainStride`、`WotF_BlinkStride`，不碰其他装备。
- `ExperimentLog` 写入有界 JSONL（2 MiB 上限、单份 `.1` 轮转、相同事件最多 1 条/秒），不会每帧刷盘。

## 5. Lab：命令行开关、文件请求、JSON 结果

### 启用

仅在启动参数包含 `-wotflab=<绝对或相对目录>` 时启用；**默认禁用**（不读文件、不执行命令）。
关闭时只保留一个 `Root_Play.Update` postfix，第一行 `if (!Enabled) return;`，无文件/游戏状态访问。
启动时在该目录写 `lab-status.json`。

### 文件

| 文件 | 方向 | 说明 |
| --- | --- | --- |
| `requests.txt` | 外部 → 游戏 | UTF-8 追加，一行一条；只处理完整行（以 `\n` 结尾） |
| `results.jsonl` | 游戏 → 外部 | 每条命令一个 JSON 对象，立即 flush；8 MiB 上限、轮转到 `results.jsonl.1` |
| `experiment.jsonl` | 游戏 → 外部 | 事件日志，缓冲 flush、2 MiB 上限 |
| `lab-status.json` | 游戏 → 外部 | 启动快照（协议版本、路径、时间） |

### 请求格式

```
<id> <command> [key=value ...]
```

- `id`：调用方自选，结果原样带回；建议唯一
- 启动时已存在的请求行会被忽略（只执行本次启动后追加的行），避免复用 LabDir 时重放旧命令
- 一行最多 1024 字符；每次主线程 pump 最多执行 8 条；每次最多读 64 KiB
- 未知命令返回 `ok:false`；没有任意代码执行、没有网络端口、没有 lab 目录外文件访问

### 命令（白名单）

| 命令 | 参数 | 作用 |
| --- | --- | --- |
| `status` | — | 协议/版本、Harmony 是否已 patch、游戏是否加载、运行时 10 个 Def（5 能力/基础 + 2 visual + 3 impact；不含 UI MainButtonDef `WotF_TechLab`）是否存在、tick、选中 Pawn、setup 当前 HP |
| `observe` | — | 有界状态快照：地图、选中 Pawn（位置/征召/Job/pather/psylink/psyfocus/技能/装备/伤口总severity/`moveSpeed`/`ticksPerMoveCardinal`/`drawPos`/`traits[]`/`hediffs[]`/StrideDiagnostics/当前格地形与成本）、殖民者摘要、所有 `WotF_*` spawned Thing、飞弹 live/launched/impacts/expired 与 live frames |
| `select` | `id=<thingIDNumber>` | 先 `ClearSelection` 再选中指定 Pawn |
| `spawn` | 可选 `x= z=` | 在当前地图生成并选中一个 `ElfColonist`（命名 `Lab Subject <id>`）；缺省在中心附近找可站立格 |
| `grant` | — | 对**选中** Pawn 执行 `LabActions.GrantPsycast`（brain 部位 psylink） |
| `equip` | `which=terrain\|blink` | 对选中 Pawn 穿实验装备 |
| `unequip` | — | 移除两件实验装备 |
| `move` | `x= z=` | 按原版 `FloatMenuOptionProvider_DraftedMove` 下发 `JobDefOf.Goto` 有序任务；保留征召状态，不再直接 `pather.StartPath` |
| `path` | `x= z=` | 输出目标格 `canReach`（可开门通行）、`canReachNoClosedDoors`、terrain/pathCost/effectiveTerrainCost、standable、门 id/open/blocksPawn |
| `draft` | `value=0\|1` | 设置征召 |
| `attack` | `id=<thingIDNumber>` | 征召并下发 `AttackStatic` Job（射击观察入口） |
| `sow` | `x= z=` | 下发 `Sow` Job（种植观察入口，需要有效种植区/可种植格） |
| `cast` | `id=<thingIDNumber>` 或 `x= z=`；可选 `preset=<AbilityDef.defName>`（默认 `WotF_ArcaneMissiles`） | 激活选中 Pawn 的指定导弹预设，目标为 Thing 或格（正常能力路径）；返回实际 `preset` 与 Clamp 后的有效 `settings`（含 `ignoreFlightLimits`/`expireOnTargetLost`） |
| `fire` | `preset=<AbilityDef.defName>`；`mode=damage\|heal\|explode`（仅显式传入时覆盖 `impactDef`），可选 `visual=rimunity\|native`；目标 `id=` 或 `x= z=`；可选 `count= speed= turn= wobble= damage= heal= radius= reset=1` | 有 `preset` 时复制该 Def 的 `CompProperties_AbilityWotFArcaneMissiles.settings.Copy()` 作为基底，无 `preset` 保持 `ArcaneMissileLauncher.CreateDefaultSettings()`；其余参数只覆盖本次副本；`reset=1` 清零计数 |
| `missiles` | 可选 `reset=1` | 读取/清零 `LiveMissileCount`、`TotalLaunched`、`TotalImpacts`、`TotalExpired`；并返回 `frameCount` 与最多 16 条 live `frames`（含 pos/heading/target/age 与有效 settings） |
| `setup` | `confirm=1` | **仅显式调用**：在中心附近找 15×5 空地，建普通地面/泥地直道、墙、关闭门，生成 Human 与 Elf 测试 pawn；返回精确坐标/IDs；只用于一次性 lab/quicktest 地图 |
| `behaviors` | `confirm=1` | 在已 setup 的隔离地图准备 A17 fixture：给 `Lab Human` 一把原版 `Gun_BoltActionRifle`（已有远程武器则复用）、在 setup 小范围铺 Soil 建 3 格 `Zone_Growing`（`Plant_Potato`）、生成固定非 Pawn 木墙靶；返回实际 pawn/weapon/target IDs、zone cells/crop；重复调用 `reused:true` |
| `warp` | `x= z=` | 将选中 Pawn 传送到合法可站立空格（`Notify_Teleported`），返回 from/to |
| `hurt` | 可选 `amount=0.1..4` | 在非致命部位（优先手臂/腿）加真实 `Cut` 伤口，返回部位/severity/总伤口 severity |
| `camera` | — | 相机跳到选中 Pawn |
| `time` | `speed=0..4` | 设置 `TimeSpeed`（0 暂停、1 正常、2 快速、3 超快、4 极速），便于录制 |
| `save` | `name=<安全名>` | `GameDataSaveLoader.SaveGame(name)` |
| `load` | `name=<安全名>` | 仅加载隔离 `-savedatafolder/Saves` 中已存在的 1..40 位字母/数字/`_`/`-`/`.` 名称（禁止 `..`）；先校验文件存在，再 `GameDataSaveLoader.LoadGame(name)` 排队 1.7 LongEvent，响应在加载执行前写出 |
| `ui` | `action=open\|close\|show\|hide\|reset\|restore\|status` | 调用原 UI 公开入口（`WindowStack` + `Window_TechLab` + `TechLabMod.Settings` + `TechLabUi.ResetEntryPosition` + `TechLabUi.OpenWindowAndRestoreEntry`），返回实际窗开闭、入口设置与原生行探针；不抢桌面、不使用反射 |
| `step` | `n=1..600` | 暂停状态下推进 `n` 个游戏 tick（`DoSingleTick`），用于确定性验收 |

### A17 现有 sow/attack 入口前提与观测

- 先 `behaviors confirm=1`（必须已有 `setup`）：给 `Lab Human` 加原版 `Gun_BoltActionRifle`（已有远程武器则复用），
  在 setup 的 15×5 内铺 Soil 建 3 格 `Zone_Growing`（`Plant_Potato`），生成固定非 Pawn 木墙靶；返回实际
  human/elf/weapon/target IDs、zone cells/crop。只改 setup 生成的 pawn 和这个小范围，不碰普通地图/其他角色。
- `sow x=<x> z=<z>`：选中可种植的 setup Pawn；目标格必须在 growing zone 内，`GetPlantDefToGrow()` 非空、格上无植物/阻挡；
  Lab 现在显式设置 `job.plantDefToSow`（1.7 `JobDriver_PlantSow` 必需），否则会失败。命令会先解除征召。
- `attack id=<thingIDNumber>`：选中 Pawn 必须已装备**远程武器**（`AttackStatic` 走远程 verb；空手不会自动改近战），
  目标已 spawn、在射程/视线内；命令会设征召并下 `AttackStatic`。
- 真实观测（仅 behaviors 注册的 setup pawn，Lab 开启）：`observe.behaviors[]` 的 `sowStarts`/`sowCompletions`
  来自实际 `JobDefOf.Sow` 与目标格真实 Plant；`shots` 由 `Projectile.Launch` 后置计数，只有真实弹丸离膛才增加，
  不以 `AttackStatic` 下达本身当作射击发生。

### `fire` 参数映射

`preset` → 从该 `AbilityDef` 的 `CompProperties_AbilityWotFArcaneMissiles.settings` 取 `Copy()` 作为基底；没有该 comp 或 Def 未加载时命令直接报错，不回退默认值。
`mode` → 仅在**显式传入**时设置 `impactMode` 与 `impactDef = ArcaneMissileDefs.ImpactForMode(mode)`（Def 缺失时保留 enum 回退）；不传时保留 preset 自带的 `impactDef`，避免把 Mender 的治疗覆盖成伤害。
`visual=rimunity|native` → `visualDef`（`WotF_Visual_RimUnity` / `WotF_Visual_Native`，Def 缺失时命令报错）；
`count` → `missileCount`（1..12）；`speed` → `speedCellsPerSecond`（1..60）；
`turn` → `maxTurnDegreesPerSecond`（10..1440）；`wobble` → `wobbleAmplitude`（0..1.5）；
`damage`/`heal`/`radius` → 对应 payload 字段。最终值由 `ArcaneMissileSettings.Clamp()` 收敛后传给 launcher，
Def 本身始终不被修改（launcher 自己再 `Copy()+Clamp()`）。
响应含 `preset`、`settings{count,speed,turn,hitRadius,lifetime,maxRange,radius,damage,heal,ignoreFlightLimits,expireOnTargetLost,impactDef,impactMode,visualDef}`、
`visualDef`/`visualEffective`/`impactDef`/`impactMode`/`impactEffective`（实际所选 Def 名，Def 缺失时
`impactEffective=enum:<mode>`）。

### `load` 时序

`load name=<name>` 只接受 1..40 位字母/数字/`_`/`-`，先用 `GenFilePaths.FilePathForSavedGame(name)` 定位并确认
文件在隔离 `Saves` 中存在，再调用 `GameDataSaveLoader.LoadGame(name)`；该 API 只把 1.7 `LongEventHandler`
加载任务排队并 Dispose 当前 Game，随即返回。Lab 在返回后立即写 `results.jsonl`，所以客户端先收到
`{"ok":true,"queued":true,...}`，加载在下一 LongEvent 执行；不提供额外 load CLI 参数。

### `ui` 控制

`ui action=open|close` 直接使用 `Find.WindowStack` 与 `Window_TechLab`（open 时不存在才 Add，close 时 `Close(true)`）；
`show|hide` 改 `TechLabMod.Settings.showFloatingEntry` 并 `Write()`；`reset` 调 `TechLabUi.ResetEntryPosition`；
`restore` 调 `TechLabUi.OpenWindowAndRestoreEntry`（原生 PlaySettings 图标点击时走的同一真实回调：取消隐藏并开窗，
用于验证"隐藏后从原生入口恢复"，**不是**鼠标点击实测）。
`status` 返回 `windowOpen`（真实 WindowStack）、`showFloatingEntry`、`hasCustomEntryPosition`、`entryX/Y`、
`entryLayoutVersion`、`selectedId`，以及原生行探针 `hasRow`、`lastRowLeftX`、`topY`、`screenWidth`、`screenHeight`、
`rowEntryDraws`（来自 `Patch_PlaySettingsRowEntry`，只在非 worldView 且图标实际绘制时增长）；不读私有入口字段。
窗口/入口的 OnGUI 由游戏下一帧正常绘制，lab 不注入输入。

### `setup` 边界

- 必须带 `confirm=1`；没有隐式测试地图检测，**只允许在可丢弃的 dev/quicktest 地图调用**。
- 只在中心附近寻找 15×5 可站立的矩形；范围内只允许 `Plant`/`Filth`（天然植被、污物），
  **施工前仅在这 15×5 内清除它们**；出现 Pawn、Building、物品或其他 Thing 则视为不可用并继续找。
  不删除范围外植物，也不提供任意 destroy 接口。
- 构造：`Concrete` 直道 8 格 + `Mud` 4 格、门（默认关闭）+ 5 面木墙、额外 1 面侧墙；
  `Lab Human`（原版 Colonist）与 `Lab Elf`（`ElfColonist`，缺失时回退 Colonist）。
- 结果包含 `origin`、`lane{z,normalFrom,normalTo,mudFrom,mudTo,doorX}`、pawns/walls/door 的 id+pos、
  `cleared{plants,filth}`，以及 `current{door,walls,pawns}` 的真实 `hp/maxHp`。
- 同一 map 重复调用返回 `reused:true` + 当前 `current` 状态（不重复建造）；换图/重载进程后重新 setup。

### 结果

成功：`{"id":"...","ok":true,...}`；失败：`{"id":"...","ok":false,"error":"..."}`。
`observe` 字段：`tick`、`timeSpeed`、`map{uniqueId,size,isPlayerHome}`、
`selected{id,name,def,pos,drafted,dead,downed,job,pather{moving,dest},psylink,psyfocus,abilities[],apparel[],
equipment,hediffCount,faction,moveSpeed,ticksPerMoveCardinal,drawPos{x,z},traits[{def,degree}],traitCount,
hediffs[{def,severity,permanent,part}],
wounds{count,totalSeverity},stride{walked,blinked,blocked,terrainCellsFreed,lastBlock,firstTick,lastTick,terrainStride,blinkStride},
cell{pos,terrain,pathCost,terrainStride,effectiveTerrainCost,standable}}`、
`colonists[]`（有界简表，不含 traits/hediffs/equipment 详情）、`wotfThings[{id,def,pos,hp}]`、`colonistCount`、`wotfThingCount`、
`missiles{live,launched,impacts,expired,labFired,frameCount,frames[{x,z,headingX,headingZ,targetX,targetZ,ageTicks,targetThingId,targetCell,settings{...}}]}`（`frames` 仅 live、最多 16 条）、
`setup{present,origin,laneZ,cleared,door,walls,pawns}`（墙/门/测试 pawn 的 `id/def/pos/hp/maxHp`，按 setup 记录的 ID 读取真实状态）、
`behaviors[{id,name,state{sowStarts,sowCompletions,shots,sowActive,sowCell,crop,lastShotTarget}}]`（只含 behaviors 注册的测试 Pawn）。

飞弹计数语义：`launched` 为真正加入 map 的导弹数（96 上限丢弃不计）；`impacts` 为每枚结算一次；
`expired` 只含超时/越界/超距；计数不写存档，读档/新组件后从 0 开始，同 map 切走再回来保留。
`labFired` 是本次进程内 lab `fire` 命令累计发射数。

### 辅助脚本

```powershell
pwsh -File Tools/lab.ps1 -LabDir '<LabDir>' -Command 'status'
pwsh -File Tools/lab.ps1 -LabDir '<LabDir>' -Command 'observe'
pwsh -File Tools/lab.ps1 -LabDir '<LabDir>' -Command 'setup confirm=1'
pwsh -File Tools/lab.ps1 -LabDir '<LabDir>' -Command 'fire mode=explode x=120 z=130 count=3 speed=40 reset=1'
```

## 6. 启动（已核实参数语义）

对 `Assembly-CSharp.dll` 的 IL 核实：

- `GenFilePaths.get_SaveDataFolderPath` 使用 `GenCommandLine.TryGetCommandLineArg("savedatafolder", ...)`，
  因此 `-savedatafolder=<dir>` 生效；`get_ConfigFolderPath` = `<savedatafolder>/Config`，
  **ModsConfig 也被隔离**，不会改正常配置。
- `ModsConfig.TryGetCommandLineModList` / `ApplyCommandLineModList`：解析 `-mod=`（分号分隔），
  清空 ActiveMods 后加入 `ludeon.rimworld` + 全部已装 Official（DLC）+ 列表模组，再 `TrySortMods`；
  未发现写 ModsConfig 的调用。
- `-quicktest` **支持**：`Verse.QuickStarter.CheckQuickStart` 调用 `GenCommandLine.CommandLineArgPassed("quicktest")`，
  命中后从 Entry 场景直接进入 Play，并由 `Root_Play.SetupForQuickTestPlay` 生成 world/地图与初始状态；
  这是 dev 快速地图路径，适合本实验的一次性场地。它不是任意代码执行，也不写 ModsConfig。
- `-savedatafolder` 与 `-mod=` 的快速测试组合已核实；`setup` 场地命令只应在该一次性地图调用。
- `-loadgame` **不受支持**：1.7.4985 的 C# 只通过 `GenCommandLine` 读取 `quicktest`/`savedatafolder`，
  未发现 `-loadgame` 解析；飞行中保存后重载用 lab `load name=<safe>` 或游戏内 Load Game，CLI 无 `-loadgame`。

基线依赖验证（不含本模组）：

```powershell
Start-Process -FilePath 'E:/SteamLibrary/steamapps/common/RimWorld/RimWorld.exe' `
  -WorkingDirectory 'E:/SteamLibrary/steamapps/common/RimWorld' `
  -ArgumentList @(
    '-savedatafolder=E:/SteamLibrary/steamapps/common/RimWorld/Moddev/lab-profile',
    '-mod=brrainz.harmony;Chezhou.RimUnity;erdelf.HumanoidAlienRaces'
  )
```

完整实验（需先构建、junction 已存在）：

```powershell
pwsh -File Tools/launch-lab.ps1 `
  -LabDir 'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/lab-run' `
  -SaveDataFolder 'E:/SteamLibrary/steamapps/common/RimWorld/Moddev/lab-profile'
```

等价参数：`-mod=NekoYukari.WhisperOfTheForest;brrainz.harmony;Chezhou.RimUnity;erdelf.HumanoidAlienRaces`
（Royalty 等 DLC 会自动加入），`-savedatafolder=<dir>`，`-wotflab=<dir>`。
加 `-QuickTest`（或原生命令行 `-quicktest`）可从 Entry 直接生成一次性 dev 地图，适合 `setup confirm=1`。

`launch-lab.ps1` 只校验 `/Mods/Whisper-of-the-Forest` junction 与 DLL 是否存在，不创建 junction、不启动后自动操作。
Windows 上脚本用 `Invoke-CimMethod Win32_Process Create`（WMI detached）启动，避免 enhanced_terminal/Start-Process
任务生命周期导致游戏进程立刻退出；`-Wait` 仍按返回的 PID 等待。

## 7. 已验证 / 未验证

已运行：

- **最终全量构建（root 授权统一 build）**：`1.7/Assemblies/WhisperOfTheForest.dll`
  113664 bytes，`2026-10-25T12:53:32Z`，sha256 `446990baf101288fd0f29691ffafdf2f1541f32a470eb6b52269f15da8f237f2`。
- 构建后核对：Source 下无 `.cs` 晚于 DLL；1.7/Assemblies 仅此单 DLL。
- `Tools/verify.ps1 -RequireFeatureDefs`：24 PASS / 0 WARN / 0 FAIL；`Tools/check-defs.ps1`：15 个 Def 类型/贴图引用 0 missing。
- 有界 JSONL 写入器独立运行测试：40 行、300 字节上限触发轮转，转义/换行/文件内容正确。
- Lab 文件协议独立运行测试（真实 Lab 源码 + 游戏程序集，无游戏循环）：
  `status` 返回合法 JSON、`observe` 无地图时 `no map loaded`、未知命令 `unknown command`，request id 与结果一一对应。
- 静态核对 5 个 `[HarmonyPatch]` 目标均存在于 `Assembly-CSharp`：`Root_Play.Update`、
  `PlaySettings.DoPlaySettingsGlobalControls`、`Pawn_PathFollower.GetPawnCellBaseCostOverride`、
  `Pawn_PathFollower.TryEnterNextPathCell`、`Projectile.Launch(...8 args)`。
- 本轮新增（brain psylink 修复、select/move/path/fire/missiles/setup/warp/hurt/camera/time、observe 扩展、
  `fire visual/impact`、`load`、`ui`、setup 植被清理与场地 HP）已用独立临时工程编译全部 `Source/**/*.cs`：
  **0 error**（不触碰最终 obj/bin/DLL）。
- 已按源码核对原版 drafted 移动：`FloatMenuOptionProvider_DraftedMove.PawnGotoAction` 使用
  `JobMaker.MakeJob(JobDefOf.Goto, cell)` + `TryTakeOrderedJob(job, null, false)`，不改变征召状态。
- Missiles 已提供 public `TotalLaunched/TotalImpacts/TotalExpired/ResetLabCounters`；Lab `observe` 读取这些真实计数。
- SDK `dotnet --version` = 10.0.402；官方 zip SHA512 匹配。

当前未验证 / 待主代理：

- 实机已验证六组合、cell/高速墙、移动目标、飞行存档/读档、UI 读档、Terrain/Blink 基本路径与 A03 cast（见 §9）；
  仍待/未证：闭门穿门夹具缺口、墙角完整运行时中间采样仅部分、`setup/behaviors` 其他边界、外观视觉。
- 新实例日志/精灵 Def/UI ctor 由主代理验收；飞弹计数不 Scribe，读档后从 0 起算。
- `setup` 会修改当前地图且不自动清理；只允许一次性 lab/quicktest 地图，必须 `confirm=1`。
- 功能行为正确性（转向、命中、地形成本、走跳、窗口拖动等）由对应负责人与主代理验收。

## 8. 最终验收步骤（主代理）

0. 最终构建已完成：sha256 `446990baf101288fd0f29691ffafdf2f1541f32a470eb6b52269f15da8f237f2`（113664 bytes，12:53:32Z）；
   `verify.ps1 -RequireFeatureDefs` 24 PASS / 0 FAIL。重编先关闭游戏（build.ps1 会拒绝运行中构建）。
1. 启动：`pwsh -File Tools/launch-lab.ps1 -QuickTest -LabDir '<Moddev>/lab-run' -SaveDataFolder '<Moddev>/lab-profile'`。
2. 入图后：`status` → `observe` → `setup confirm=1`（记录 origin/lane、human/elf、wall/door IDs）→ `behaviors confirm=1`（记录 weapon/target/zone）。
3. **最短能力链（A04/A06/A07/A09）**：
   - `select id=<human>` → `grant` → `observe`（psylink>=1、ability、Player.log 无 null Part）
   - `select id=<elf>` → `equip which=terrain` → `path x=<mud cell> z=<lane>` → `move x=<mud end> z=<lane>` → `time speed=1` → `step n=240` → `observe`（stride、cell.effectiveTerrainCost）
   - `unequip` → 重复上一条 `move`/`step`/`observe` 作 A09 对照
   - `select id=<human>` → `fire mode=damage count=3 speed=60 turn=720 reset=1 id=<wall>` → `step n=30` → `observe`（missiles impacts、墙 hp；A06 高速不穿墙）
   - `fire mode=damage count=1 speed=8 reset=1 id=<elf>` → 立刻 `select id=<elf>` → `move`（移动目标）→ `step n=120` → `observe`（A04 追踪命中/过期）
   - `select id=<elf>` → `hurt amount=2` → `observe`（wounds.totalSeverity）→ `select id=<human>` → `fire mode=heal reset=1 id=<elf>` → `step n=30` → `observe`（总 severity 下降；A07）
   - `fire mode=explode reset=1 x=<cell> z=<cell> count=2` → `step n=30` → `observe`（impacts、周边 hp；A07）
4. 录制：`select id=<elf>` → `camera` → `time speed=0`；证据（results.jsonl、experiment.jsonl、Player.log、截图）留在 LabDir/Moddev。

> 命令文本可用 `Tools/lab.ps1 -LabDir '<LabDir>' -Command '<command>'` 发送；每条等待返回 JSON 再发下一条。
> `move` 使用原版 drafted Goto，不再直接改 pather；`fire` 是独立于 `cast` 的直接发射入口。

## 9. 2026-10-25 非视觉实机证据（lab-run）

- 最终验收轮 PID 71488（`-QuickTest`，隔离 lab-run/lab-profile）；最终存档 `a18-final`，游戏已暂停，
  选中 `Lab Elf` id=37175，位置 `(163,0,136)`，穿戴 `WotF_BlinkStride`。
- 场地：`setup confirm=1` id `c132723365192`，origin `(149,0,135)`，Human 37171，Elf 37175，door 37170，
  墙 37164-37169；`behaviors confirm=1` id `c132730668851`，步枪 37180，靶墙 37181，
  Potato zone `(150..152,0,139)`。
- 六组合（每次 `reset=1`，真实 impact 日志一次）：
  - RimUnity damage `c132820616418`：靶墙 195→188；Native damage `c132833566946`：188→181。
  - RimUnity explode cell `(153,0,135)` `c132841864453`：181→177；Native explode `c132843351296`：177→173。
  - RimUnity heal（Torso 3）`c132859075879`：wounds 3→0；Native heal `c132910579648`：3→0。
  - 首次 RimUnity damage 从 `(145,144)` 发射被路过的 Kelly 挡住（impact target=Kelly）；移动施法者到
    `(151,135)` 后重测，属真实碰撞而非穿墙。
- 高速墙 `c132933398534` speed=60：靶墙 173→166；`missiles` `c132934365873` live0/impact1/expired0。
- 移动目标（补强）：RimUnity damage `c134655260706` 发射时 target `(158,137)`；1 秒后 observe
  `c134656856429` 仍 **live=1** 且 target 已到 `(160,137)`（离开 2 格）；step `c134710346654` 后
  `missiles c134711018745` live0/impacts1/expired0；impact 日志 `cell=(163,135)`，最终 target `(171,139)`，
  wounds 由先前 2/14 增至 5/33.416。旧的 `c132947531804` 只作历史记录，不作为命中前追踪证据。
- A03 能力链：`grant c134825861990` → `cast id=37181 c134826412825`（响应 `ok=true`，走 Psycast/Job 路径）
  → step `c134826937223`；experiment 明确 `volley launched=3/3 impact=WotF_Impact_Damage` 和 3 条 impact。
- 墙角：door 37170 `open=false`；旧 U 形 setup 门后紧贴墙 37167，不能测穿门（已如实作为夹具缺口）。
  `path (161,137)` `c133339626742`/`c133351035130` `canReachNoClosedDoors=false`；墙角 `(163,136)`
  `c133428902749` canReachNoClosedDoors=true；后续采样 c134948813348 / c134949848373 / c134950857511
  分别记录 `(161,135)→(163,135)→(163,136)`，blink 46→47→47、blocked0，沿墙外绕行。
- 最终状态：`a18-final.rws`（12,783,682 B，`2026-10-25T14:00:19Z`），PID62836 暂停，tick19871，
  Elf 37175 在 `(163,136)`、BlinkStride、psylink1、`WotF_ArcaneMissiles` 可用；窗口关闭、入口 show=true。
- 门夹具：专用 `a18-door.rws` 从 a18-final 复制，原 `a18-final.rws.bak` 保留；仅移除已知 Wall37167
  `(162,137)`，未改生产代码/正常存档。Terrain 实测门 initial closed→`door.open=true`；positions
  `(159,137)→(160,137)→(161,137)→(162,137)→(163,137)`，blink 未增加。Blink 实测 blocked reason
  `door-cell`（160,137）和 `leaving-door-cell`（159/161,137），door 于 tick20606 打开；随后
  `(160,137)→(161,137)→(162,137)→(164,137)→(165,137)`，门格仅原版开门步行通过，blink 在离开门后
  162→164 才增加（1→2）。
- 代码漂移：隔离 rebuild 与最终 DLL 均为 349 methods，逐方法 IL **0 diffs**；最终 DLL hash 仍
  `446990baf101288fd0f29691ffafdf2f1541f32a470eb6b52269f15da8f237f2`。
- 飞行中存档/读档：save `a18-flight` `c133017632555`，load `c133018557127` 先返回 `queued:true`；
  rws 内 `visualDef=WotF_Visual_RimUnity`、`impactDef=WotF_Impact_Damage`、目标墙 37181、
  `arcaneMissiles` 存在。load 后 observe `c133216759828` live=1（launched 计数因未 Scribe 为 0），
  step `c133225336514` 后 missiles `c133225876465` live0/impacts1；impact 日志 target=Kelly。
- 窗口：`ui show` `c133242279433`、`ui open` `c133242987953`（windowOpen=true）；save/load `a18-ui` 后
  `ui status` `c133302408151`：windowOpen=false、showFloatingEntry=true、custom `(1599,746)` 保留。
- 地形/瞬移：Terrain 穿戴后 `path (161,137)` `c133339626742` effectiveTerrainCost=0、
  `canReachNoClosedDoors=false`；step 后 `c133341280945` walked8/terrainFreed9。Blink 穿戴后同门
  `c133351035130` noClosedDoors=false；移动后 `c133352563956` walked12/blinked3；墙后合法目标 `(163,136)`
  `c133428902749` canReachNoClosedDoors=true，实际走到该格 `c133430577358` walked18/blinked7，未穿墙。

## 10. A/B 预设与 Lab 集成（2026-10-26，最终构建与实机验证完成）

### Def 与入口契约

- `WotF_ArcaneMissiles_Heavy`：13 cells/s、10°/s、单发、`impactDef=WotF_Impact_Explode`、`ignoreFlightLimits=true`；`expireOnTargetLost=false`。
- `WotF_ArcaneMissiles_Mender`：4 cells/s、720°/s、单发、`impactDef=WotF_Impact_Heal`、`ignoreFlightLimits=true`、`expireOnTargetLost=true`。
- 旧 `WotF_ArcaneMissiles` 三发/300°/s 不变；两个 bool 默认 false，旧 Def 行为不受影响。
- 数值/命中/视觉只来自 Def 的 `CompProperties_AbilityWotFArcaneMissiles.settings`；Lab 只 `Copy()`，不加硬编码工厂，也不改 Def。

### Lab 行为

- `grant`：运行时发现所有含 `CompProperties_AbilityWotFArcaneMissiles` 的 AbilityDef 并一次授予；按最高 `level` 走既有 brain psylink 流程；已有技能跳过；一个都没发现或 `GainAbility` 后仍缺失都返回明确错误。新增 XML 预设不需要补 C# 名单。
- `fire preset=<defName>`：以 Def settings 副本为基底；不传 `preset` 仍是 `CreateDefaultSettings()`；只有显式 `mode=` 才覆盖 `impactDef`，其余 `speed/turn/count/...` 只覆盖本次副本；launcher 再 `Copy()+Clamp()`。
- `cast preset=<defName>`：默认旧 `WotF_ArcaneMissiles`；返回实际 defName 与 Clamp 后的 settings（含两 bool）。
- `Status` 新增 Heavy/Mender 存在性；`Tools/verify.ps1` 静态列表同步，并移除已删除的 `WotF_TechLab` 要求，改为检查原生 `Patch_PlaySettingsRowEntry`/`WidgetRow.ButtonIcon`/`OpenWindowAndRestoreEntry` 存在且 `<MainButtonDef>`/旧 worker/UI.xml 不存在。
- 观测：`selected` 增加真实 `moveSpeed`（`StatExtension.GetStatValue`）、`ticksPerMoveCardinal`、`drawPos`、`traits[{def,degree}]`、`hediffs[{def,severity,permanent,part}]`；殖民者简表保持有界。`missiles`/`observe` 增加 live `frames` 与必要 settings。

### 测试角色

- 现有 `Lab Human` 37171 有伤，不能当“健康普通 Human”基线；本轮改用隔离档已有的 **Human 840 Samantha**：`def=Human`、`downed=false`、无永久伤、无速度类 trait、无仿生/药物 hediff，实测 `moveSpeed=4.6`、`ticksPerMoveCardinal=14.493`、所在格 `Soil`/`effectiveTerrainCost=2`。
- 未新增通用角色编辑器、未随机 spawn、未新增 prepare-dodge 命令；只使用现有 `warp/draft/move`。
- 命中爆炸测试使用已知受伤的 `37171`，与规避目标分离，避免伤势污染 840 的速度基线。

### 横移测试（正常时间，最终版）

- 按精简验收约定，0.25 s 仅作计算维度；最终采用“目标先持续横移、正常时间发射”，不依赖发射后 15 tick 的精确延迟。
- 实测：`fire preset=WotF_ArcaneMissiles_Heavy id=840 reset=1` 于 tick19905；840 在 tick19874 已下 `Goto (248,240)` 并持续南移；导弹实际 `expired reason=outOfBounds cell=(250,0,229)`（tick20004），`live0/impact0`，840 伤口始终 `0/0`，采样最小间距 2.647（6 个 `observe` 采样，tick19935–20010）。
- 证据边界：正常时间下 `Pawn.DrawPos` 有真实逐帧插值，本轮采样间隔为 15 tick，因此只能声明“采样最小距离”，不是连续路径最小距离；但终止是结构化 `expired/outOfBounds` 且 `impact0`、目标 `0` 伤，足以证明未命中。
- **无效试验记录**：早期“暂停 + 每 tick `DoSingleTick`”时 `Pawn.DrawPos` 被冻结在旧位置，导弹按冻结 DrawPos 命中旧格（`impact1`、目标 2 格外的 0 伤）。该结果不作为规避或命中证据；暂停批处理不能替代正常帧时间。

### 最终构建与实机（2026-10-26）

- 最终构建：`1.7/Assemblies/WhisperOfTheForest.dll` **121344 B**，sha256 `ce991a866e9160f69f0cd7d72c522ceb9d4e01b55c75a60d372acc017cce3074`；部署到 `Mods/Whisper-of-the-Forest` 的 DLL hash 相同。
- `verify.ps1 -RequireFeatureDefs`：29 PASS / 0 WARN / 0 FAIL；`check-defs.ps1`：17 个引用 0 missing；`Moddev/tools/check-ui/check-ui.ps1 -AssemblyPath <最终DLL>`：UI CHECK OK（真实 Harmony 目标 `DoPlaySettings(WidgetRow, Boolean, Single&)`，Postfix 参数名 `rowVisibility/worldView/curBaseY` 全部匹配）。
- `status`：`harmonyPatched=true`，10 Def 全 true；`grant` 一次授予 `WotF_ArcaneMissiles`,`WotF_ArcaneMissiles_Heavy`,`WotF_ArcaneMissiles_Mender`；再次 `grant`（`c180307587244`）后 abilities 仍为 3 个导弹技能 + 既有 SolarPinhole，无重复。
- UI：`ui action=hide`（`c175848741995`，show=false）→ `ui action=restore`（`c175849320529`，windowOpen=true、show=true、rowEntryDraws 增加）→ `ui action=close`（`c175849704802`）→ save/load `a19-ui` 后 `ui action=status`（`c175904738995`）windowOpen=false、show=true、hasRow=true、lastRowLeftX=1812、topY=961、screen 1920×1080、rowEntryDraws=9985（最终 `c180347926747` 为 60568）。未做鼠标点击；主代理随后以一次只读截图确认原生区位置及底栏旧入口移除。
- A 命中爆炸：对静止 `37171` 发射 Heavy，伤口 `5/33.272 → 6/40.272`（+7，正好 `damageAmount`），`impact1/expired0/live0`；再等 1 秒计数不变，未重复爆炸。
- B：Mender 从 `(228,195)` 追击移动目标，实测飞行 **720 tick（12 s）** 才命中，远超旧 10 s；命中后 `live0/impact1`，总 severity 由 40.272 降至 29.912（含 12 s 自然愈合，heal worker 上限 10）。第二次 Mender 采样 heading 由 `(0.551,0.834)` 变到 `(0.416,0.909)`（181 tick 内持续 live=1），证明有明显转向且仍在追踪；飞行中 save `a19-mender-flight` 的 rws 内 `ignoreFlightLimits=True`、`expireOnTargetLost=True`、`impactDef=WotF_Impact_Heal`，load 后 live=1，继续命中后 `imp=1`、severity 29.768→19.696，再等 1 秒仍 `imp=1`（只结算一次）。
- 目标失效（despawn/死亡/离图）：现有 Lab 没有安全的 despawn/离图命令，未新增通用编辑器；该类边界由既有 46 项 harness 覆盖，本轮不重复造工具。
- 最终状态：`a19-final.rws`（12,760,089 B，`2026-10-25T18:16:03Z`），PID25772 暂停，tick24937，选中已 grant 的 Elf37175，窗口关闭、悬浮入口可见、无 live 弹。旧的 `a18-final.rws`、`a19-base/UI/preui/mender-flight` 保留。

### 爆炸音效修复复核（2026-10-26）

- root 在 `WotF_ArcaneMissileDamage` 增加 `<soundExplosion>Explosion_Bomb</soundExplosion>`；无 C# 变更，DLL 未重编（仍 121344 B / `ce991a86…`）。
- 安全结束旧隔离 PID74968 后用既有脚本启动新隔离实例 **PID25772**，load `a19-final`；使用真实 Ability 入口 `cast preset=WotF_ArcaneMissiles_Heavy id=37171`（`c181519081870`），`activated/ok=true`，settings 为 Explode/13/10/单发。
- 单次命中：tick24729、cell `(247,0,240)`、age65；Lab Human 伤口 `4/19.696 → 5/26.696`（+7，正好 `damageAmount`），`impact1/expired0/live0`；再等 1 秒计数不变。
- 新日志 `Moddev/evidence/a19-soundfix-player.log` 中 **0 条 ERROR/Exception/null SoundDef**；旧失败现场 `a19-final-player.log` 原样保留。`status.harmonyPatched=true`。

## 11. 原生 Projectile 迁移接入与 a20 实机（2026-10-27）

### 构建与静态检查

- `Tools/build.ps1`（仓库内 SDK 10.0.402）→ `1.7/Assemblies/WhisperOfTheForest.dll` **122880 B**，sha256 `c08464fdfb2a51bf77951623d0a7c35b33ff91d18b2bc9df86c45876f51eb0c1`，0 warning / 0 error；与迁移任务的隔离输出同尺寸同 hash，确定性构建成立。
- `Tools/verify.ps1`：**29 passed / 0 warnings / 0 failures**；`Tools/check-defs.ps1`：19 个 Def 类型/贴图引用 **0 missing**。
- 首次跑 check-defs 时发现迁移新增的 `WotF_ArcaneMissileProjectile` 用了原版贴图 `Things/Projectile/Bullet_Small`；该脚本只认模组 `Textures/` 下的路径，判为 missing。改成已有 `Experiments/Missiles/ArcaneMissiles`（`drawerType=None`，本来就由视觉后端绘制）后通过；只改这一行 texPath，未动其它 Def 语义。
- harness 与 `check_refactor.py`/`check_defs.py`/`check_lang.py` 的结果见 missiles.md §6、§9；本轮未重跑移动与 UI 验收。

### 隔离启动（本轮实测到的参数语义）

- 命令：`RimWorld.exe -savedatafolder=Moddev/lab-profile -wotflab=Moddev/lab-run -quicktest`，PID **13364**。
- **不要再用 `-mod=` 传依赖**：1.7 的 `ModsConfig.ApplyCommandLineModList` 只接受本地 `Mods/` 下的 mod，日志逐条 `-mod: 'brrainz.harmony' is not in the local Mods folder, ignoring`；Harmony/RimUnity/HAR 全被忽略后本模组因缺依赖没有加载（`Initializing new game with mods` 只剩官方包）。
- 改依赖隔离 `lab-profile/Config/ModsConfig.xml`（a19 会话留下的清单，含 harmony/rimunity/HAR/本模组）。启动后 `status`：`harmonyPatched=true`、`gameLoaded=true`、10 个特性 Def 全 true。
- 旧日志先归档到 `Moddev/evidence/a20-prelaunch/*.a19`；a18/a19 存档与证据未动。

### 实机证据（真实 Ability 入口 `cast preset=...`，正常时间）

| 检查 | 观察 | 证据 id |
| --- | --- | --- |
| load a19-final | tick 24938、Paused、未选中；该存档本就 `live=0`，不触发旧对象转换 | `c065409076611` |
| Mender 单发 | settings heal/4/720/heal10/两 flag=true；飞行中 `live=1`（age25）；`observe.wotfThings` 出现实体 **id126852 `WotF_ArcaneMissileProjectile` (236,0,240)**；5 s 后 `live0/impact1/expired0`；37171 总 severity 27.096 → 16.952（单次 heal 10） | `c065432664213`、`c065433140733`、`c065439145851` |
| 三发 Damage 齐射 | `launched=3/live=3`，三枚真实 Thing 126880–126882；3 s 后 `live0/impact3/expired0`；37171 severity 16.952 → 44.88 | `c065458589351`、`c065459068636`、`c065503070442` |
| Heavy 单发 | settings explode/13/10/单发/`ignoreFlightLimits=true`；实体 126893；`impact def=WotF_Impact_Explode age=64`；`live0/impact1`；severity 44.88 → 48.736 | `c065513178980`、`c065518203312` |
| 飞行中存档/读档 | tick28168 暂停下 `cast` 后 `save a20-flight`（`a20-flight.rws` 12764121 B）；`load` 后 `missiles live=1/launched=0`，frame 与存档前一致（x233.05、heading(1,0.003)、targetThing 37171、settings 全保留）；恢复时间后 `live0/impact1`，命中日志 `age=210`，severity 48.736 → 38.664（只结算一次） | `c065527032994`、`c065528008888`、`c065549272263`、`c065603083778` |
| 最终态 | `a20-final.rws` 12756887 B，tick 29059，Paused，选中 37175（三技能 Elf），窗口关闭，`live=0` | `c065611822585`、`c065612587399` |

- 粒子/拖尾只验证到「生成、跟随、随射弹销毁不报错」；RimUnity 实际观感仍由用户判断，本轮无截图或录屏。
- 日志：`Moddev/evidence/a20-player.log` **0 条 ERROR/Exception**（只有 Waylith/Vulkan 启动告警）；事件流 `a20-experiment.jsonl`、命令流 `a20-results.jsonl` 同目录。

### 未验证/已知边界（不要据此扩大结论）

- 护盾与拦截器：`Projectile_ArcaneMissiles.TickInterval` 不调 `base`，原版 `CheckForFreeInterceptBetween`/`ImpactSomething`/`ProjectileHitFlags` 链不生效；本模组飞弹不会被护盾挡住，也不做友军误伤修正。
- 旧 a19 存档中若存在飞行中旧对象不会被转换成原生射弹（本轮 a19-final 恰好 `live=0`，该路径未触发）；不提供旧格式转换器。
- 真实多地图切换 / `MapRemoved` / 界面暂停读档观感只有代码与 harness 证据；本轮只跑单图。
- 目标失效（死亡/离图）本轮未单独实机，由 harness 覆盖；Heavy 横移规避与 Mender 12 s 长追踪仍以 a19 证据为准，本轮未重做。

#### 补验：移动目标追踪与已推进飞行存档（2026-10-27 15:35–15:38 本地）

主代理指出 a20 原证据里 37171 始终静止、`a20-flight` 的 age=0，只证明「生成后可读档」。本轮用同一隔离实例（PID13364，参数与 §11 相同）补一个短场景，未改代码/DLL/Def。

- 目标真的在走：两次 `observe`（`c073523155123`、`c073524559636`）分别为 tick 32848、32924，目标 `pather.moving=true`，`drawPos` (236.671,236.543) → (238.998,233.01)，移动约 4.23 格。此处相隔 76 个游戏 tick，不用命令等待时长推算实际间隔。原交接引用的 `c073512368391` 是路径检查，已由主代理按原始 JSON 更正。
- 追踪移动目标（`cast preset=WotF_ArcaneMissiles_Mender id=37171`，`c073611333474`，默认 preset 未传 mode）：5 次 `missiles` 采样里导弹 age 61→313、live 恒 1，帧内 `targetX/targetZ` 连续变化 235.107→232.784→230.797→228.684→226.361（每个采样间隔 ≥2.4 格），`heading` 由 (0.124,-0.992) 连续转到 (-0.572,-0.82)（`c073613537658`、`c073614730435`、`c073615655944` 及同批次采样）。目标在移动、航向在跟随，两项同时成立。
- 已推进的飞行存档：第三次 `cast`（`c073700309295`）后在 **age=152、pos(232.711,229.879)（距发射点约 10.6 格）、heading(0.263,-0.965)、target(234.685,222.685)、targetThingId=37171、severity=0.4** 时 `time speed=0` 暂停并 `save a20-moving-flight`（`c073711738358`）。
- 读档对比（`load a20-moving-flight`，`c073712357288` → `c073730994927`）：`live=1`、age=153、pos(232.726,229.814)、heading(0.236,-0.972)、target(234.5,222.5)、settings=`WotF_Impact_Heal/4/ignoreFlightLimits=true/expireOnTargetLost=true` 全部保留；age/pos/heading 的差正好是一个 tick 的推进（读档瞬间走过 1 tick），不是重新生成（新弹会是 age=0 且在发射点）。
- 续飞结算：`time speed=1` 后 `live=0 / impacts=1 / expired=0`（`c073737058377`），severity 0.4 → 0。这里 heal 上限 10 点被 0.4 点伤口截断，差值同时包含这段时间的自然愈合；因此只声明「一次结算、命中医治生效」，不把差值当作 heal 数值验证。
- 说明：第一次尝试的 `a20-moving-flight` 因为采样太慢在命中后才保存（当时 live=0，`c073625521230`），已用同一文件名覆盖为上面这次有效存档；旧 `a20-flight`（age=0 那次）保持原样不动。
- 粒子仍只声明「生成/跟随/随弹销毁无报错」，观感由用户判断；本轮无截图/录屏，未改正常角色配置。
- 证据文件（已按边界隔离旧 a18/a19 记录）：`Moddev/evidence/a20-moving-results.jsonl`（73 条，按命令 id `c0735`–`c0738` 取本次）、`Moddev/evidence/a20-moving-experiment.jsonl`（31 条，按 `ts ≥ 2026-10-27T07:35` 取本次，最早 tick 32772）。
- 最终态：重新 `load a20-final`（未覆盖，`c073748075200`）→ tick 29060、Paused、选中 37175（三技能 Elf）、`windowOpen=false`/`showFloatingEntry=true`、`live=0`（`c073808175495`、`c073808639348`、`c073809112432`）。
- 主代理复核：`a20-moving-flight.rws` 实含一个 `Projectile_ArcaneMissiles`，id146887、age=152、pos(232.710632,229.878464)，目标引用及 Heal/4/720/两 flag 均保存；读档后 age=153、位移约一 tick，再于 age=253 命中一次。补验数据与旧累计日志分开，最终 DLL 哈希仍为 `c08464fd…51eb0c1`。粒子只核对生命周期代码、已执行的 RimUnity 设置及无错误日志，没有通过画面核实形状或残留。

## 12. 植物催熟技能接入与 a21 实机（2026-10-27）

### 代码接入（只动 Lab，不改功能实现）

- `Lab/LabActions.cs`：授予发现从「携带飞弹 comp」改为共享基类 `CompProperties_WotFAbilityEffect`，仍按 defName 排序、去重、走原 psylink/psyfocus 流程；新增常量 `MaturePlantDefName`。没有硬编码 `WotF_MaturePlant` 名单。
- `Lab/LabCommands.cs`：`cast preset=...` 对任意已授予的本实验技能直接调用真实 `Ability.Activate`；仅当 Def 携带 `CompProperties_AbilityWotFArcaneMissiles` 时才附带既有 `settings`（新增私有 `HasMissileProps`）。非飞弹技能返回 `"settings":null`，并新增顶层 `"radius":null`；飞弹的 `settings` 字段保持原样，`radius` 仍可在 `settings.radius` 读到。`fire` 仍是飞弹专用，没有扩成通用命令。
- 新增有界 `plants` 命令：无参列出最多 16 株未成熟植物加 `plantCount/immatureCount/shown`；`id=` 报告单目标（`present/def/pos/hp/isPlant/growth/lifeStage/harvestableNow/isTree/sown/matureable/failureReasonKey`）；`confirm=1` 在已登记 `setup` 矩形内生成一株 fixture（`kind=tree` 用 `Plant_TreeOak`，否则 `Plant_Potato`，同图同 def 复用）。该命令只读 growth，催熟只能来自真实施法。
- `status.defs` 增加 `WotF_MaturePlant` 存在字段，未重构既有列表。

### 构建与静态检查

- 隔离重建（输出到临时目录，未覆盖交付 DLL）与部署件均为 **127,488 字节**、SHA256 `a1a877c60e18fabc678b5b888521a742740940caa666d817df4bd0f0d2372b5`，说明部署件就是当前源码的确定性产物；0 warning / 0 error。
- `Tools/verify.ps1`：**31 passed / 0 warnings / 0 failures**；`Tools/check-defs.ps1`：21 个 Def 类型/贴图引用 **0 missing**（含新 comp 类）。
- 未重跑旧 52 项飞弹 harness、UI 与移动场景。

### 现场、退出与隔离重启

- 旧现场先存为 `a21-before`（13,301,179 字节）。该会话 14:53 启动、只加载了旧 Def（`status` 中 `WotF_MaturePlant=false`），存盘后才由集成任务关闭。
- 旧隔离实例（`-savedatafolder=E:/…/Moddev/lab-profile -wotflab=E:/…/Moddev/lab-run -quicktest`，**无 `-mod`**）安全退出后构建部署，再用同一组参数启动新实例 **PID33332**（23:18:07 本地）；`status` 为 `harmonyPatched=true`、11 个特性 Def 全 true（含 `WotF_MaturePlant`）。旧会话与 a20 日志归档在 `../evidence/a21-prelaunch/`，a18/a19/a20 存档未动。
- 新实例是独立 quicktest 地图；`setup confirm=1` 在 15×5 实验矩形内清掉 49 株植物与 4 处污渍，生成围挡、门与 Lab Human 45293 / Lab Elf 45296。
- 说明：a21 命令批次的前半段（`c1a21st1`–`c1a21miss1`、`c1a21fix2`–`c1a21miss3`）由被中断的原集成 worker 在同一新实例上发出；本 worker 接手后重新读取状态、补齐重复授予、保存读档与最终态，并逐条核对上述原始 JSON，未改动这些命令或结果。

### 实机证据（真实 Ability 入口，暂停/正常时间按需切换）

| 检查 | 观察 | 证据 id |
| --- | --- | --- |
| 授予含新技能 | `grant` 后 `psylink=1`、`psyfocus≈1`，abilities 为 `WotF_ArcaneMissiles_Mender`、`WotF_ArcaneMissiles`、`WotF_ArcaneMissiles_Heavy`、`WotF_MaturePlant` | `c1a21grant`、`c1a21obs1` |
| 地图植物可观测 | 地图共 32827 株、其中 20362 株可催熟；列表前 16 株均 `growth<1`、`lifeStage=Growing`、`harvestableNow=false` | `c1a21plantlist` |
| 未成熟作物催熟 | `Plant_Potato` 45297 @ (101,0,129)：`growth 0.15 / Growing / hp85 / harvestableNow=false` → 施法 → `growth 1 / Mature / harvestableNow=true / hp85`，thingId 与位置不变 | `c1a21fix1`、`c1a21cast1`、`c1a21after1` |
| 未成熟树催熟 | `Plant_TreeOak` 45298 @ (102,0,129)：同样 `0.15 → 1`，`isTree=true`、`Mature`、`harvestableNow=true`、`hp200` 不变 | `c1a21fix2`、`c1a21cast2`、`c1a21after2` |
| 催熟不产生飞弹 | 两次植物施法后 `live=0 / launched=0 / impacts=0 / expired=0`，无新 Thing、无多余 impact | `c1a21miss1`、`c1a21miss2` |
| 非植物目标 | 对门 45292 施法：目标 `isPlant=false`，施法返回 ok，门与植物状态均未变（直调 `Activate` 不走 `Valid`，属原版行为；玩家点击路径的拒绝由 `Verb_CastAbility.ValidateTarget → CompAbilityEffect_WotFPlantGrowth.Valid` 承担，见 §12 代码证据） | `c1a21door`、`c1a21castdoor` |
| 已成熟再施法 | 对已成熟 45297 再次施法：`growth 1 / Mature` 不变，`failureReasonKey=WotF_MaturePlant_Failed_AlreadyMature` | `c1a21castmature`、`c1a21after3` |
| 重复授予不重复 | 第二次 `grant` 后 abilities 仍为 4 项，无重复 | `c2a21grant2`、`c2a21obs2` |
| 旧飞弹仍工作 | `cast preset=WotF_ArcaneMissiles id=45286` 返回原 settings（3 发/13/300/Damage/radius1.7），`live=3` 后结算 `launched=3 / impacts=3 / live=0` | `c1a21castmiss`、`c1a21miss3`、`c2a21miss-a` |
| 保存读档保留成熟 | `save a21-final` → `load a21-final` 后 45297/45298 仍 `growth 1 / Mature / harvestableNow=true`、hp 不变，abilities 仍 4 项 | `c2a21save1`、`c2a21load1`、`c2a21p5`、`c2a21p6`、`c2a21obs3` |
| 存档内的原生字段 | `a21-final.rws` 中 (101,0,129) 与 (102,0,129) 两株为 `<health>85/200</health>`、`<growth>1</growth>`、`<age>2000</age>`（只改 growth，未强改年龄），Elf45296 在同一存档 | `a21-final.rws` |
| 最终态 | 重新加载后 `tick 5019`、`Paused`、`selectedId 45296`、abilities 4 项、`windowOpen=false`、入口可见、`live=0` | `c2a21st5`、`c2a21obs4`、`c2a21ui4`、`c2a21miss-d` |

- 日志：`../evidence/a21-player.log` 中 **0 条 ERROR/Exception**（仅 Waylith/Vulkan 启动告警）；本轮命令流 `a21-results.jsonl`、事件流 `a21-experiment.jsonl`。

### 未验证/已知边界

- 没有鼠标键盘：玩家点击选植物、`targetParams` 是否真的把植物高亮为合法目标、中文 label/description 与三条失败提示的实际显示，均未实机点击确认；由 XML 参数、`TargetingParameters.CanTarget` 与 `check-defs` 覆盖。
- 「已成熟/非植物」的运行期证据是直调 `Ability.Activate` 后状态不变；`Valid` 返回 false 的那条路径靠编译检查与反编译调用链支撑，未做点击实测。
- 只验证一株普通作物与一株橡树；特殊植物（灵能树、菌菇、仙灵树等）按普通 Plant 处理，未单独实机，也没有专门兼容层。
- 收获动作本身未执行：只观测 `HarvestableNow=true`，没有让小人去割/收，也不自动收获。
- 未重做旧飞弹的移动目标追踪、飞行存档、UI 拖动与移动装备场景。

## 13. a24 触发链与书页实机（2026-10-29）

### 构建与入口

- 最终 DLL：`1.7/Assemblies/WhisperOfTheForest.dll` **199168 B**，`2026-10-29T16:52:16Z`，sha256 `F3F4924161C780A8A2B641BE7973C748CEB28FE8DED5A6C4028E0EDB104CB941`；部署 junction 读到同一 hash。
- `build.ps1 -Verify`：47 PASS/0 FAIL；`check-defs`：37 引用 0 missing；`check-ui.ps1 -AssemblyPath <最终DLL>`：UI CHECK OK。
- 环境故障与修复：首次 `launch-lab.ps1` 的 `-mod=` 列表只解析本地 Mods，Harmony/AlienRace/RimUnity（workshop）被忽略，导致 `HarmonyLib.HarmonyPatch` TypeLoad 并使 ModsConfig 重置。`Tools/launch-lab.ps1` 增加 `-UseModsConfig`（不传 `-mod=`，使用隔离 `lab-profile/Config/ModsConfig.xml`），重启后 `harmonyPatched=true`。未改产品代码。

### 书页与兼容（a、b）

- `spellbook action=select slot=3`（经 `Window_Spellbook.SelectBookmark`）：`activeSlot 2→3`，gizmo label `T03-ProjHarm→T04-ProjHeal`；`slot=0` → `T01-SelfHeal`。`clear slot=9` 后 `select slot=9`：gizmo label 变为“未选择法术”、`disabled=true`、reason 指明第 10 个书签为空。
- validate：`[Projectile][Harm][AOE]` false，`[Touch][Grow][Amplify]` false，`[Projectile][Explosion][AOE]` true。

### 触发链（c）

- plan（Lab 已扩展输出 segments）：`segmentCount=2`；segment0 Projectile/Harm amount9 `triggerOnHit=true`，segment1 Projectile/Explosion amount12 radius1.9。
- `cast slot=9 id=40078`（真实 Ability 入口，slot 9 = `T11-ProjHarmTrigProjExpl`）：segment0 impact tick13381 cell `(240,0,220)`，Lab Human 伤口 `1/8.92→2/17.92`（+9）；日志 `trigger ... segment=1 launched=1/1 from (241.04,0.00,221.01) towards (249,0,226) direction=0.86,0.5`（施法者在西侧，spawn 在目标东侧）；segment1 impact tick13428 cell `(249,0,226) hit=ground explosion=1`；`live=0`、`impacts=2`（一次 Harm、一次 Explosion）。
- 第二段图边生成失败（d）：目标在 `(249,220)` 时日志 `trigger ... not triggered: spawn point 250.09,220.54 is outside the map`，只有 segment0 impact，无第二段。

### 飞行存档（e）

- 第一段飞行中 `a24-flight1.rws`：`spellPlan` 含 `segments` 与 `triggerOnHit=True`；load 后只结算一次：segment0 Harm → trigger segment1 → Explosion 一次（`live0/impact2`）。
- 第二段飞行中 `a24-flight2.rws`：rws 明确含 `<spellSegmentIndex>1</spellSegmentIndex>`；load 后 segment1 命中一次（`live0/impact1`），无重复 segment0/爆炸。

### 旧四技能与截图（f、g）

- 真实 `cast`：`WotF_ArcaneMissiles` 3 impacts；`WotF_ArcaneMissiles_Heavy` 1 impact；`WotF_ArcaneMissiles_Mender` 使 Elf 伤口 `8/52.8→7/42.8`（-10）；`WotF_MaturePlant` 对植物 14241 `activated=True`。
  - 注：Lab Human 40078 在长时间正常时间测试中因累计伤势死亡，之后旧技能改用地面/自身目标；不影响触发链证据。
- 书页截图：`Moddev/evidence/a24-spellbook-trigger.png`，2560×1440，PrintWindow 只读捕获；停在“触发”分类（5 个标签可见），目录显示 `On hit/触发`，法术栏为 5 符文触发链。
- 最终：`a24-final.rws` 12,228,269 B，`2026-10-29T16:53:42Z`，tick17596，暂停，选中 Elf40081，书页关闭，`live=0`；本次 Player.log `ERROR` 计数 0（`a24-final-player.log`）。a18–a23 存档与 `Missiles.xml`（`650B6B59…`）未改。
