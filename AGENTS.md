# Whisper of the Forest · 开发规则

本文件约束所有在本仓库工作的代理与评审。设定与施工路线以 Obsidian 库 `环世界模组-森林的低语/` 为准（`设定/` 与 `技术/`）；本文件只管怎么写代码、怎么验收。

## 范围与授权

- 只实现用户明确批准的内容。设定、种族特性、玩法、视觉效果，没批准就先提方案，不写进模组。
- 项目仍在开发，不处理旧版本兼容：不写存档迁移，不为旧 Def 名或旧类名加 fallback，不在 `_Deprecated/` 之外保留历史兼容路径。旧存档加载报 `Can't find class WhisperOfTheForest.Experimental.*` 直接忽略；改类名、改命名空间、删组件都是干净的破坏性变更，只把玩家真实反馈的当前版本崩溃当问题。
- 修一个缺陷只改这个缺陷。顺手看到的问题记下来汇报，不顺手改。
- 不得用删除或隐藏已有功能、视效的方式“修好”缺陷。
- 测试失败要找到真实原因；不许靠删断言、跳过测试或反复重跑变绿。
- 不 commit、不 push、不发布，除非用户当次明确要求。

## 目录

| 路径 | 内容 |
| --- | --- |
| `Source/WhisperOfTheForest/` | 产品代码 |
| `Source/Lab/` | 测试宿主，只有 `Tools/build.ps1 -Lab` 才编进程序集，运行时还需 `-wotflab` 启动参数 |
| `Source/LabTests/` | L2 交接冒烟测试，一个测试一个类 |
| `1.7/` | 1.7 专用 Def、语言、程序集 |
| `Tools/` | 构建与测试脚本：`build.ps1`（L0；只有带 `-Verify` 才调 `check-defs`，`-Lab` 不调，所以改 Def 后要单独跑一次 `check-defs`）、`check-defs.ps1`（Def 类型与贴图引用、Def 结构规则、程序集规则）、`test.ps1`（L1 + L2） |
| `../Graphify/native/source/` | 整个 `Assembly-CSharp.dll` 的反编译源码，按命名空间分目录；只读参考，不进仓库。查原版行为先看这里，图谱查询用 `project_path=…/Moddev/Graphify` |
| `docs/` | 架构决策（`docs/adr/`）与参考资料；施工进度和设计以 Obsidian 为准，仓库里不再写进度日志 |
| `_Deprecated/` | 已废弃内容，不编译、不加载；`_Deprecated/README.md` 逐项写明来源与原因 |

## 编码规则

1. **数值只写在 Def 里。** 可调参数只出现在 XML；C# 不再写一份默认值。
2. **配置错误在加载时报出。** Def 的必填项与引用在 `ConfigErrors` 里检查。运行时不为 Def 已保证的东西加空值判断，不静默回退（例如不写 `?? DamageDefOf.Blunt`）。
3. **只在真正的输入边界处理错误**：存档读取、Lab 命令解析、第三方回调。其余异常直接抛出，不吞、不“只记一次然后继续”。
4. **法术与视效分离。** 法术只发出视效请求（位置、方向、宽度、长度）；蓄能、电弧、压暗及其时长归视效 Def。
5. **Lab 单向依赖。** 产品代码不得引用 `Lab/`、`LabTests/`；测试只通过公开接口操作游戏、通过只读诊断观察状态。
6. **废弃不删除。** 用 `git mv` 移进 `_Deprecated/`，保留原相对路径，并在 README 里登记。
7. 注释只写能省下读者多次查找、或防止非显然错误的内容。
8. **日志分级。** 本模组日志统一走一个分级日志入口，按模块带前缀（如 `[WotF/Spell]`、`[WotF/Vfx]`）：
   - Error：本模组出错，L1 计数为 0 才算通过；
   - Warning：可恢复的异常情况；
   - Info：一次关键动作一条摘要（施法、结算、存读档），写清数量和结果；
   - Debug：逐对象明细（命中了哪些东西、各自伤害），默认关闭，排查时打开。
   系统内部的运行情况靠日志检查，不靠写测试。
9. **符文兼容只在 XML 声明一次。** 每枚符文的 `compatibleAugments`、`excludesAugments` 是唯一来源；书页灰显、红字原因、保存校验、施放都调用 `SpellCompiler.CanAppend`，UI 不另写一份判断。

## 已知陷阱

原版行为先在 `../Graphify/native/source/` 里核对，再写依赖它的代码。

实机报的错修好后按能稳定抓住它的最便宜一层归档：能从 Def 或程序集静态判定的，写成 `check-defs.ps1` 规则并注明原版源码位置；落在系统交接或玩家操作入口上的，补 L2 测试；两者都不是的只记在本节。每条都在本节留一行，能自动查的标“（`check-defs` 会查）”。

### 写代码

- **Harmony 按参数名绑定。** 补丁方法的形参名必须和目标方法一致（`DoPlaySettings` 的是 `rowVisibility`、`curBaseY`，不是 `row`、`y`）；名字错会让整个 `PatchAll` 失败。
- **1.7 的 `StatRequest.Pawn` 不是被计算的小人。** `StatPart` 里读 `req.Thing`。
- **自定义 Def 类型的 DefInjected 目录要写完整类名**，例如 `DefInjected/WhisperOfTheForest.Spells.SpellGlyphDef`，否则翻译不生效。
- **声明了 `<comps>` 的 HediffDef 必须把 `hediffClass` 设为 `HediffWithComps` 或其子类。**（`check-defs` 会查）
- **武器 Def 写 `<comps Inherit="False">` 会丢掉 `BaseWeapon` 的 `CompProperties_Styleable`**，武器进入意识形态圣物列表时报配置错误；要补回来。
- **`GenRadial` 的半径上限约 79.8 格。** 超出会报错，大范围查找要限制半径或改用 `map.AllCells`。
- **伤害打掉整个部位时只留 `Hediff_MissingPart`，没有 `Hediff_Injury`。** 只累加伤口严重度会把这次伤害读成 0。
- **本模组的 Def 根元素写完整类型名**，例如 `<WhisperOfTheForest.Spells.SpellGlyphDef>`。短名只对 `Verse`、`RimWorld` 里的类型有效，写错会被静默跳过。（`check-defs` 会查）
- **`GameComponent` 子类必须有 `public X(Game game)` 构造函数。** 游戏用 `Activator.CreateInstance(type, game)` 创建，缺了组件根本不会出现。（`check-defs` 会查）
- **类里有静态的贴图、材质、Shader 字段就要加 `[StaticConstructorOnStartup]`**，否则启动时报警告，资源也可能在错误线程加载。（`check-defs` 会查）
- **存档里可能为空的字典，`Scribe_Collections.Look` 要传一组 working lists 并设 `logNullErrors=false`。** 只加空值判断会把 `Cannot fill dictionary` 换成 `You need to provide working lists`，症状不变。写法见 `GameComponent_Spellbook.ExposeData`。
- **继承 `BasePawn` 的人形种族要自己写 `<tools>`。** `BasePawn` 不带徒手攻击，缺了这个种族没有近战。（`check-defs` 会查）
- **没有材料来源的服装、装备要写 `<smeltable>false</smeltable>`**，否则加载时报“可熔炼但熔不出东西”。（`check-defs` 会查）
- **可搬运（`haulable`）且 `madeFromStuff` 的 ThingDef 必须自己写 `<mass>`。** `BaseGun`/`BaseWeapon` 不带质量，只写 `stuffCategories` 和 `costStuffCount` 会报 `is stuffable but has no mass`。
- **非头部层的服装穿戴图会按体型加后缀。** `wornGraphicPath` 除了本体图，还要有 `_Male`、`_Female`、`_Thin`、`_Hulk`、`_Fat` 五张，缺图会报错并显示坏材质。（`check-defs` 会查）
- **每个 IMGUI 绘制入口包一层 `TechLabDrawScope`**，进出时保存并还原 `Text.Font`、`Text.Anchor`、`Text.WordWrap`、`GUI.color`。漏还原 `WordWrap` 会在帧末报错。
- **1.7 的 `Translator.Translate(key, args)` 已过期**，带 `{0}` 的文本用 `string.Format(key.Translate(), args)`。
- **改移动速度不要动共享的 `PathGrid`。** 它是全图共享缓存，重算会让全图失效。要按小人改地形成本，补 `Pawn_PathFollower.GetPawnCellBaseCostOverride`；墙、深水的不可通行判定在它之前，不受影响。
- **`PawnPath.nodes` 是反序的**（`nodes[0]` 是终点），下一格是 `Peek(1)`。`Notify_Teleported` 会清掉当前路径并重新寻路，不能用来沿原路线跳格。
- **原版有些代码写死了 verb 类型，认不出本模组的 verb。** 本模组的 verb 直接继承 `Verb`，不是 `Verb_LaunchProjectile` 或 `Verb_Shoot`。原版按类型判断的地方，多数只是跳过（如护盾腰带的 `CompAllowVerbCast` 只拦 `Verb_LaunchProjectile`），少数拿到 null 直接用：`PlaceWorker_ShowTurretRadius` 只认 `Verb_Shoot`（类型严格相等）和 `Verb_Spray` 系，建造预览每帧报 `Exception in UIRootUpdate`。用自定义 verb 的炮塔改用模组自己读炮管 verb 射程的 PlaceWorker。新发现这类会空引用的原版类，加进 `check-defs.ps1` 的 `$hardVerbConsumers` 表，写明源码行。（`check-defs` 会查）
- **原版右键“吃”（`FloatMenuOptionProvider_Ingest`）不能用在活植物上。** 点击后它先对目标 `SetForbidden(false)`，植物没有 `CompForbiddable`，报 `Tried to SetForbidden on non-Forbiddable Thing`；接着下发 `JobDefOf.Ingest` 把植物拿进物品栏，被携带的植物在 `Plant.TickLong` 空引用。种族 `foodType` 含 `Plant` 或 `Tree` 时，要把这个菜单项的点击动作换成原地啃食的 Job。
- **不要用把 `Moving` 能力上限设成 0 的 Hediff 来定身。** 原版 `Pawn_HealthTracker.ShouldBeDowned`（`:614-616`）在小人不能移动时直接判定倒地，目标会表现为被击倒而不是定身。定身走 `SpellCastingService.IsRooted`（拦 `Pawn_PathFollower.PatherTick`），命中时结束纯移动 Job；战斗 AI 还会立刻重派找掩体的 Goto，见 `Patch_EnsnareCombat.cs`。

### 测试与运行

- **`t.SpawnCaster` 生成的是人类殖民者（`PawnKindDefOf.Colonist`），只带林庭血脉基因。** 依赖精灵种族本身的行为（`foodType` 含 `Plant`/`Tree`、HAR 外观与骨架）要用 `t.SpawnElf`（`WotF_Elf_Colonist`）生成真精灵，否则原版前置检查先把场景挡掉，测试永远走不到要测的地方。右键吃草的测试第一版就是这样空转的。
- **测试装备直接从 `ThingMaker` 交给装备栏。** 先生成到地图上再 `AddEquipment` 会被 `ThingOwner.TryAdd` 静默拒绝。
- **测试结束前让小人走完 `Stance_Cooldown`。** 世界小人不 tick，指向已销毁武器的冷却姿态会一直留在存档里报警告。
- **暂停状态下 `DoSingleTick` 不更新 `Pawn.DrawPos`。** 要观察移动就正常推进时间。
- **启动隔离实例不要带 `-mod=`。** 它只在本地 `Mods/` 找模组，会跳过创意工坊里的 Harmony、HAR、RimUnity。
- **从终端启动游戏用 `Invoke-CimMethod Win32_Process Create`。** `Start-Process` 的子进程会随终端任务退出被带走。
- **游戏占用 DLL 时 `build.ps1` 拒绝构建**，先关游戏。
- **游戏内日志队列只保留最近 1000 行**，加载期错误早被挤掉，所以 L1 扫磁盘上的 `Player.log`。
- **Lab 的 `load` 只是把读档排进队列。** 它立刻返回 `queued:true`，紧接着的命令可能拿到 `no map loaded`；要等新地图就绪（`test.ps1` 已处理）。
- **`check-defs.ps1` 只认模组 `Textures/` 下的贴图。** Def 引用原版贴图路径会被判缺失，改用模组内贴图。

### 结算与视效

- **结算时不要直接遍历会变化的游戏集合。** 轨道炮曾直接按下标遍历 `map.listerThings.AllThings` 并在循环里结算伤害（`RailgunCastService.cs`）。目标被打死时会被移出列表、尸体和血迹会被加进列表，下标随之错位：后面的对象被跳过，新生成的对象又可能被重复命中，表现为“命中数忽多忽少、墙有时不掉血”。做法是先筛出全部命中对象存成独立列表，再逐个结算。同类场景：爆炸、范围效果、任何“找到一批对象后造成伤害或生成新物体”的逻辑。
- **全屏压暗不要做成采样画面的后处理。** 轨道炮旧版这样做导致整屏上下翻转。现在是 GUI 阶段先画一层半透明纯色，再画光束、电弧、余辉，光束不会被压暗。
- **法术和视效不得移动、缩放或重置地图相机。** 视效只能读相机做坐标换算。
- **视效按 `TicksGame` 计时，不按帧时间。** `MapComponentUpdate` 每帧对每张地图都会调用，暂停时也一样（`Game.UpdatePlay` → `Map.MapUpdate`）；按 tick 差推进，暂停自然冻结。地图移除时要停掉并清空该图的全部视效。
- **本模组飞弹不走原版拦截链。** `Projectile_ArcaneMissiles.TickInterval` 不调基类，护盾腰带、拦截器、友军误伤修正都不生效；不要写成“兼容护盾”。

## 开发节奏

- **先写完一块再构建。** 不每改一处就构建、跑测试或回读文件；一次构建报出整块的错误。
- **模块未闭合时只做冒烟。** `build.ps1 -Lab` 编译通过，Lab 实例进图后 L1 计数为 0，新东西能在游戏里出现、能打开（Def 能生成、建造菜单里有、窗口能弹出），就算暂时通过。不给半成品写 L2。
- **模块闭合后再写测试。** 一个对象类、一个建筑的完整行为、一条玩家操作链做完，再按下文 L2 规则设计交接测试，并把边界情况过一遍。
- **接口先排除不合法状态。** 通过类型、参数、构造方式和所有权，让错误情况无法表达或无法发生；已经被接口杜绝的情况，不再为它写运行时防御、兜底分支或测试。防御只留给真实输入边界。
- **想得清的边界直接写进代码。** 空列表、上限、重复触发、先后顺序这类逻辑问题，写代码时就处理，不先写测试看它失败再改。
- **跑测试有预算，回归范围由依赖决定。** 开发和排查时用 Graphify 查本次改动函数、类型的调用关系，用 grep 补齐 LabTests 和未入图代码的调用方；只跑被测模块及经过同一入口的相关测试。改动不涉及其它模块，就不跑其它模块的回归，也不默认跑全量。共享基础设施、全局入口或确有跨模块交接时才跑全量，且交接前最多一次。失败先读结果文件定位，修完用 `-Filter` 验证；已有结果文件能回答的问题直接读结果，不为凑证据重跑。临时证明用的测试只在任务书明确要求时写。
- 实机报来的缺陷仍先复现再修，见 L2 规则“缺陷的测试按需补”。给子代理的任务书按同样节奏写验收，不要求逐步红绿。

## 验收分层

每个关注点交给能稳定抓住它的最便宜一层：

| 层 | 做法 | 抓什么 |
| --- | --- | --- |
| **L0 编译** | 引用真实游戏程序集编译，0 警告 0 错误 | 类型错误、API 不存在 |
| **L1 加载** | 隔离实例进入地图后，扫描本次进程的磁盘 `Player.log`，本模组相关的 Error/Exception、配置错误、翻译错误为 0；日志缺失、为空或属于别的进程算“未验证”并失败。Harmony 补丁全部应用；本模组 Def 全部加载 | Def 类型名、`MayRequire`、DefInjected、Harmony 参数名、缺构造函数 |
| **L2 交接** | 在真实游戏里批量运行 `LabTests/` 冒烟测试 | 系统之间的交接是否接通 |
| **L3 观感** | 画面、动画、手感、声音，由用户判断 | 排查故障时才截图 |

### 不算验收证据

- 用 `Shims.cs` 重写 `Verse`/`RimWorld` 类型的 harness。
- 匹配源码文本的检查（“源码里有某个字符串”）。
- 一次性 IL 转储、探针脚本。
- 子代理“已完成”的口头说明。
- Lab 或测试直接调用 `SpellService.TryCast`：只证明服务和执行链，不证明玩家点击时的目标合法性校验。这类结论要注明“只走了回调路径”。

## L2 测试规则

- **脚本化、一次跑完。** `Tools/test.ps1` 负责构建、启动或复用隔离实例、载入固定夹具存档、在游戏内批量执行测试并写出结果文件，退出码表示成败。禁止代理逐条手发 Lab 命令、逐 tick 观察来“测试”。手发命令只用于排查一个已失败的测试。
- **测试在游戏内同步执行。** 时间推进用同步 tick，不等真实时间；一次全量回归以秒到几十秒计。
- **复用同一个实例。** `test.ps1` 默认保留游戏实例，下次调用直接复用；只在重新构建、`-Restart` 或实例异常时重启，`-StopAfter` 才关闭。夹具每个进程只载入一次。
- **尽量在同一个存档里测完。** 新测试默认在已载入的夹具里自建场地、测完清理，不新开存档、不读别的档、不带 `-Reload`。必须存读档的只有“存档 → 读档”交接本身：声明 `Reload` 并写 `AfterReload`，由运行器统一存读，测试自己不调 `SaveGame`/`LoadGame`。运行器先把所有 `Reload` 测试的场地停放，再只存一次档，所以 `Run` 里推进的游戏时间会作用到别人停放的场地（在飞的飞弹会提前落地，炮塔会打别的场地里的人）：`Run` 只推进被测交接本身需要的几 tick，长流程放进不带 `Reload` 的测试，停放前关掉会自己行动的东西（如炮塔的目标类别）。只有这几种情况才换新存档或重载夹具：改了 DLL（程序集不能热加载，必须重启实例）、夹具存档本身要变、实例已坏。
- **只测系统交接。** 测试覆盖系统之间的交接点：魔术书槽位 → 施法、施法 → 射弹 → 命中结算、存档 → 读档、装备 → 寻路移动。系统内部的运行（单个符文怎么算、命中了谁）靠分级日志检查。
- **不测设计参数。** 不为单个法术、符文写完整测试；不断言穿透方式、射程、伤害数值、增幅倍率、兼容表里的具体组合。这些随设计调整，写成测试只会在改设计时误报。
- **小而独立。** 一个测试一个类，只测一件事：自建场地，执行，断言，清理。测试之间不共享状态、不依赖顺序，可按名字或前缀单独运行。
- **测试期间日志出现 Error 就算失败，不论是谁报的。** 原版代码被我们触发后报的错，游戏内 L1 不算“本模组的错”，靠这条兜住。测试本来就要触发报错时，用 `[LabTest(ExpectedLogErrors = new[] { @"正则" })]` 声明；只豁免本次匹配上的条目，`test.ps1` 的磁盘日志闸门按首行全等跟着豁免。不设全局白名单。
- **只断言可观察结果**：生命值、伤口、位置、存档读档前后是否一致；不断言内部字段怎么算出来。
- **失败信息带现场数据**：伤口严重度、各部位伤口、总体健康等，不用重跑就能定位。
- **缺陷的测试按需补。** 实机发现的缺陷，若落在某个交接点上，就补一个先失败、修后通过的交接测试；落在系统内部的，修好后用日志确认。
- **从玩家的入口进。** 玩家能直接操作的功能，至少一条测试走原版入口：右键菜单用 `t.RightClick`（走原版 `FloatMenuMakerMap.GetOptions`，含全部 provider），建造用 `t.CanPlace`（走 `GenConstruct.CanPlaceBlueprintAt`，会跑全部 PlaceWorker），不能只用 `JobMaker`、`GenSpawn` 绕过去。本模组的建筑和物品另有 `smoke.mod-things` 统一扫一遍放置、生成、tick 和按钮，新加的物体不用再单写冒烟测试。右键吃草和炮塔放置的报错都是因为测试绕过了入口才漏掉。
- **纯数学可单元测试。** 不碰游戏运行时的纯函数（如几何计算）可以写单元测试，但必须直接引用产品程序集，不许 Shim。

## 汇报格式

- 每条验收给出：执行的命令、结果文件或日志的原始片段、L1 日志计数。
- 通过、失败、未运行分开列；未运行的不写成通过。
- 写明改动文件清单是否落在授权范围内。
