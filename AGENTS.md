# Whisper of the Forest · 开发规则

本文件约束所有在本仓库工作的代理与评审。设定与施工路线以 Obsidian 库 `环世界模组-森林的低语/` 为准（`设定/` 与 `技术/`）；本文件只管怎么写代码、怎么验证。

## 范围与授权

- 只实现用户明确批准的内容。设定、种族特性、玩法、视觉效果，没批准就先提方案，不写进模组。
- 项目仍在开发，不对任何旧产品做兼容：不写存档迁移，不为旧 Def 名或旧类名加 fallback，不保留历史兼容路径。改类名、改命名空间、删 Def、删组件都是干净的破坏性变更。
- 修一个缺陷只改这个缺陷。顺手看到的问题记下来汇报，不顺手改。
- 不得用删除或隐藏已有功能、视效的方式“修好”缺陷。
- `build.ps1`、`check-defs.ps1` 报错要找到真实原因；不许靠删规则、放宽规则或加白名单变绿。
- 不 commit、不 push、不发布，除非用户当次明确要求。

## 目录

| 路径 | 内容 |
| --- | --- |
| `Source/WhisperOfTheForest/` | 产品代码 |
| `1.7/` | 1.7 专用 Def、语言、程序集 |
| `Tools/` | `build.ps1`（编译；带 `-Verify` 时顺带跑 `check-defs`）、`check-defs.ps1`（Def 类型与贴图引用、Def 结构规则、程序集规则、Harmony 补丁绑定）、`test-offline.ps1`（离线交接测试，支持 `-Filter`） |
| `Tests/WhisperOfTheForest.Tests/` | 离线交接测试（net10 控制台，引用已构建的模组 DLL 与游戏程序集，不进 `1.7/Assemblies`） |
| `../Graphify/native/source/` | 整个 `Assembly-CSharp.dll` 的反编译源码，按命名空间分目录；只读参考，不进仓库。查原版行为先看这里，图谱查询用 `project_path=…/Moddev/Graphify` |
| `docs/` | 架构决策（`docs/adr/`）与参考资料；施工进度和设计以 Obsidian 为准，仓库里不再写进度日志 |

## 编码规则

1. **数值只写在 Def 里。** 可调参数只出现在 XML；C# 不再写一份默认值。
2. **配置错误在加载时报出。** Def 的必填项与引用在 `ConfigErrors` 里检查。运行时不为 Def 已保证的东西加空值判断，不静默回退（例如不写 `?? DamageDefOf.Blunt`）。
3. **只在真正的输入边界处理错误**：存档读取、第三方回调。其余异常直接抛出，不吞、不“只记一次然后继续”。
4. **边界校验一次，内部信任契约。** 在系统边界（XML `ConfigErrors`、UI/命令入口、存档读取）一次性完成校验并转换为不可变强类型对象；内部调用链信任上游类型契约，禁止层层重复判空、重复重编译或写 `??` 静默回退兜底，禁止用塞满可空字段的万能 Context 代替窄接口。
5. **法术与视效分离。** 法术只发出视效请求（位置、方向、宽度、长度）；蓄能、电弧、压暗及其时长归视效 Def。
6. **废弃直接删除。** 移除的功能连同它的 Def、代码、贴图、语言词条一起删掉，不留归档目录；要找旧实现看 git 历史。
7. 注释只写能省下读者多次查找、或防止非显然错误的内容。
8. **日志分级。** 本模组日志统一走一个分级日志入口，按模块带前缀（如 `[WotF/Spell]`、`[WotF/Vfx]`）：
   - Error：本模组出错；
   - Warning：可恢复的异常情况；
   - Info：一次关键动作一条摘要（施法、结算、存读档），写清数量和结果；
   - Debug：逐对象明细（命中了哪些东西、各自伤害），默认关闭，排查时打开。
   系统内部的运行情况靠日志检查，不靠写测试。
9. **符文兼容只在 XML 声明一次。** 每枚符文的 `compatibleAugments`、`excludesAugments` 是唯一来源；书页灰显、红字原因、保存校验、施放都调用 `SpellCompiler.CanAppend`，UI 不另写一份判断。

## 已知陷阱

原版行为先在 `../Graphify/native/source/` 里核对，再写依赖它的代码。

实机报的错修好后在本节留一行。能从 Def 或程序集静态判定、而且会复发的，再写成 `check-defs.ps1` 规则，注明原版源码位置，并标“（`check-defs` 会查）”。

### 写代码

- **Harmony 按参数名绑定。** 补丁方法的形参名必须和目标方法一致（`DoPlaySettings` 的是 `rowVisibility`、`curBaseY`，不是 `row`、`y`）；名字错会让整个 `PatchAll` 失败。目标只算本类声明的成员，重载不写参数类型会报 Ambiguous match。`TargetMethod` 动态选目标的类只列出、不核对。（`check-defs` 会查）
- **1.7 的 `StatRequest.Pawn` 不是被计算的小人。** `StatPart` 里读 `req.Thing`。
- **自定义 Def 类型的 DefInjected 目录要写完整类名**，例如 `DefInjected/WhisperOfTheForest.Spells.SpellGlyphDef`，否则翻译不生效。
- **`ParentName` 只能写带 `Name` 属性的节点，不能写 `defName`。** `XmlInheritance` 只按 `Name` 建索引（`Verse/XmlInheritance.cs:79-95`，解析在 `:222-260`），写成 `defName` 时父节点找不到，子 Def 静默拿不到父节点的 `workerClass` 等字段，任务生成时才炸；原版 `AncientComplex_Loot`（`Data/Ideology/Defs/LayoutDefs/Layouts_Misc.xml:49`）就只有 `defName`，要继承它得换成 `Name="AncientComplex"`（`:4`）并自己写要改的字段。（`check-defs` 会查）
- **代码里 `"WotF_…".Translate()` 的字面量键，English 和 ChineseSimplified 的 `Keyed` 里都要有同名词条。** 原版缺词条时不报错：`Translate` 先查当前语言再查英文（`Verse/Translator.cs:53-66`），都没有就把键本身当文本返回（`Verse/LoadedLanguage.cs:488-492`），开发模式下还会被 `PseudoTranslated`（`Translator.cs:109-`）逐字母换成带音标的字符，面板上就是乱码；只缺中文则静默显示英文。仪式 Assess 的问题词条写 `WotF_Ritual_…` 并放进对应功能的 Keyed 文件。只查字面量，拼接键和 `SpellText.T` 一类包装不查。（`check-defs` 会查）
- **`rulesStrings` 的每个 `<li>` 必须是 `keyword->text`（`keyword` 可带 `(p=2)` 之类参数），Def 和 `DefInjected` 里的都一样。** 少一个 `-` 写成 `questDescription>…` 时，`Rule_String` 构造函数只记一条 `Bad string pass when reading rule`（`Verse.Grammar/Rule_String.cs:18,49-54`），规则的 keyword 为 null 仍被加进规则包（`RulePack.cs:95-97`），`GrammarResolver.AddRule` 拿它当字典键抛 `ArgumentNullException`（`GrammarResolver.cs:381`），任务描述生成失败（`QuestGen.cs:134-139`），每次生成报 3 条错。`rulesFiles` 同理，按 `->` 切分，缺了会 IndexOutOfRange（`RulePack.cs:108-118`）。（`check-defs` 会查）
- **声明了 `<comps>` 的 HediffDef 必须把 `hediffClass` 设为 `HediffWithComps` 或其子类。**（`check-defs` 会查）
- **武器 Def 写 `<comps Inherit="False">` 会丢掉 `BaseWeapon` 的 `CompProperties_Styleable`**，武器进入意识形态圣物列表时报配置错误；要补回来。
- **`GenRadial` 的半径上限约 79.8 格。** 超出会报错，大范围查找要限制半径或改用 `map.AllCells`。
- **伤害打掉整个部位时只留 `Hediff_MissingPart`，没有 `Hediff_Injury`。** 只累加伤口严重度会把这次伤害读成 0。
- **本模组的 Def 根元素写完整类型名**，例如 `<WhisperOfTheForest.Spells.SpellGlyphDef>`。短名只对 `Verse`、`RimWorld` 里的类型有效，写错会被静默跳过。（`check-defs` 会查）
- **`placeWorkers`、`inspectorTabs` 里指向本模组类型的 `<li>` 必须写全名**，例如 `WhisperOfTheForest.Mana.PlaceWorker_ManaNetwork`。短名只补 `Verse`、`RimWorld` 等命名空间（`Verse/GenTypes.cs:53-57`），解析不到时加载期只记一条不带模组名的 `Could not find a type named X`（`Verse/ParseHelper.cs:219-235`）并在列表里留 null，`BuildableDef.PlaceWorkers` 对它 `Activator.CreateInstance` 抛 `ArgumentNullException (Parameter 'type')`（`Verse/BuildableDef.cs:152-170`），第一次放置检查就炸；`inspectorTabs` 同样解析但被 `ThingDef.cs:1653-1667` 吞掉，只是 tab 悄悄不出现。（`check-defs` 会查）
- **`GameComponent` 子类必须有 `public X(Game game)` 构造函数。** 游戏用 `Activator.CreateInstance(type, game)` 创建，缺了组件根本不会出现。（`check-defs` 会查）
- **类里有静态的贴图、材质、Shader 字段就要加 `[StaticConstructorOnStartup]`**，否则启动时报警告，资源也可能在错误线程加载。（`check-defs` 会查）
- **存档里可能为空的字典，`Scribe_Collections.Look` 要传一组 working lists 并设 `logNullErrors=false`。** 只加空值判断会把 `Cannot fill dictionary` 换成 `You need to provide working lists`，症状不变。写法见 `GameComponent_Spellbook.ExposeData`。
- **继承 `BasePawn` 的人形种族要自己写 `<tools>`。** `BasePawn` 不带徒手攻击，缺了这个种族没有近战。（`check-defs` 会查）
- **没有材料来源的服装、装备要写 `<smeltable>false</smeltable>`**，否则加载时报“可熔炼但熔不出东西”。（`check-defs` 会查）
- **`<thingDef>` 旁边写 `<stuff>`（剧本起始物品、商队库存）时，物品必须 `madeFromStuff`，材料必须有 `stuffProps` 且类别对得上。** 否则 `ThingMaker.MakeThing`（`Verse/ThingMaker.cs:10-24`）每次生成报错并丢掉或换掉材料；短法杖被填活木就是这样。（`check-defs` 会查）
- **可搬运（`haulable`）且 `madeFromStuff` 的 ThingDef 必须自己写 `<mass>`。** `BaseGun`/`BaseWeapon` 不带质量，只写 `stuffCategories` 和 `costStuffCount` 会报 `is stuffable but has no mass`。
- **非头部层的服装穿戴图会按体型加后缀。** `wornGraphicPath` 除了本体图，还要有 `_Male`、`_Female`、`_Thin`、`_Hulk`、`_Fat` 五张，缺图会报错并显示坏材质。（`check-defs` 会查）
- **每个 IMGUI 绘制入口包一层 `TechLabDrawScope`**，进出时保存并还原 `Text.Font`、`Text.Anchor`、`Text.WordWrap`、`GUI.color`。漏还原 `WordWrap` 会在帧末报错。
- **1.7 的 `Translator.Translate(key, args)` 已过期**，带 `{0}` 的文本用 `string.Format(key.Translate(), args)`。
- **改移动速度不要动共享的 `PathGrid`。** 它是全图共享缓存，重算会让全图失效。要按小人改移动耗时，补 `Pawn_PathFollower.CostToMoveIntoCell`（见 `Movement/MovementPatches.cs`）：它在原版封顶前汇总地形、物件、雪沙减速，门等待和同行速度匹配在它之后，不受影响。`GetPawnCellBaseCostOverride` 只覆盖地皮成本，物件与雪沙仍由 `PathGrid` 加入，不要用。寻路选路仍按原版代价。
- **`PawnPath.nodes` 是反序的**（`nodes[0]` 是终点），下一格是 `Peek(1)`。`Notify_Teleported` 会清掉当前路径并重新寻路，不能用来沿原路线跳格。
- **原版有些代码写死了 verb 类型，认不出本模组的 verb。** 本模组的 verb 直接继承 `Verb`，不是 `Verb_LaunchProjectile` 或 `Verb_Shoot`。原版按类型判断的地方，多数只是跳过（如护盾腰带的 `CompAllowVerbCast` 只拦 `Verb_LaunchProjectile`），少数拿到 null 直接用：`PlaceWorker_ShowTurretRadius` 只认 `Verb_Shoot`（类型严格相等）和 `Verb_Spray` 系，建造预览每帧报 `Exception in UIRootUpdate`。用自定义 verb 的炮塔改用模组自己读炮管 verb 射程的 PlaceWorker。新发现这类会空引用的原版类，加进 `check-defs.ps1` 的 `$hardVerbConsumers` 表，写明源码行。（`check-defs` 会查）
- **原版右键“吃”（`FloatMenuOptionProvider_Ingest`）不能用在活植物上。** 点击后它先对目标 `SetForbidden(false)`，植物没有 `CompForbiddable`，报 `Tried to SetForbidden on non-Forbiddable Thing`；接着下发 `JobDefOf.Ingest` 把植物拿进物品栏，被携带的植物在 `Plant.TickLong` 空引用。种族 `foodType` 含 `Plant` 或 `Tree` 时，要把这个菜单项的点击动作换成原地啃食的 Job。
- **不要用把 `Moving` 能力上限设成 0 的 Hediff 来定身。** 原版 `Pawn_HealthTracker.ShouldBeDowned`（`:614-616`）在小人不能移动时直接判定倒地，目标会表现为被击倒而不是定身。定身走 `SpellCastingService.IsRooted`（拦 `Pawn_PathFollower.PatherTick`），命中时结束纯移动 Job；战斗 AI 还会立刻重派找掩体的 Goto，见 `Patch_EnsnareCombat.cs`。

### 工具与日志

- **`check-defs.ps1` 只认模组 `Textures/` 下的贴图。** Def 引用原版贴图路径会被判缺失，改用模组内贴图。
- **加载期的错误看磁盘上的 `Player.log`。** 游戏内日志队列只保留最近 1000 行，加载期错误早被挤掉。
- **Def 字段写错（`XML error: <x>…</x> doesn't correspond to any field`）的报错里没有模组名。** 读用户给的日志时按报错元素名回查本模组 `1.7/Defs`，不能只搜 `WotF`；静头环的 `hatRenderedFrontOfFace`（1.7 无此字段）就是这样漏掉的。
- **用户要求启动游戏时不要带 `-mod=`。** 它只在本地 `Mods/` 找模组，会跳过创意工坊里的 Harmony、HAR、RimUnity。
- **用户要求从终端启动游戏时用 `Invoke-CimMethod Win32_Process Create`。** `Start-Process` 的子进程会随终端任务退出被带走。

### 结算与视效

- **结算时不要直接遍历会变化的游戏集合。** 轨道炮曾直接按下标遍历 `map.listerThings.AllThings` 并在循环里结算伤害（`RailgunCastService.cs`）。目标被打死时会被移出列表、尸体和血迹会被加进列表，下标随之错位：后面的对象被跳过，新生成的对象又可能被重复命中，表现为“命中数忽多忽少、墙有时不掉血”。做法是先筛出全部命中对象存成独立列表，再逐个结算。同类场景：爆炸、范围效果、任何“找到一批对象后造成伤害或生成新物体”的逻辑。
- **全屏压暗不要做成采样画面的后处理。** 轨道炮旧版这样做导致整屏上下翻转。现在是 GUI 阶段先画一层半透明纯色，再画光束、电弧、余辉，光束不会被压暗。
- **法术和视效不得移动、缩放或重置地图相机。** 视效只能读相机做坐标换算。
- **视效按 `TicksGame` 计时，不按帧时间。** `MapComponentUpdate` 每帧对每张地图都会调用，暂停时也一样（`Game.UpdatePlay` → `Map.MapUpdate`）；按 tick 差推进，暂停自然冻结。地图移除时要停掉并清空该图的全部视效。
- **本模组飞弹不走原版拦截链。** `Projectile_ArcaneMissiles.TickInterval` 不调基类，护盾腰带、拦截器、友军误伤修正都不生效；不要写成“兼容护盾”。

## 开发节奏

- 理想节奏是快、成批、精简。一块功能写完再构建一次，一次构建报出整块的错；不每改一处就构建或回读文件。
- 进度优先于证明。一块功能写完、两条离线检查通过，就往下一块走；不为一个功能反复补证据。
- 接口先排除不合法状态，想得清的边界（空列表、上限、重复触发、先后顺序）写代码时直接处理。防御只留给真实输入边界。

## 验证

日常跑三条离线命令，都是秒级，按顺序：

| 命令 | 通过标准 |
| --- | --- |
| `pwsh -File Tools/build.ps1` | 0 警告 0 错误 |
| `pwsh -File Tools/check-defs.ps1` | `Summary: 0 failed` |
| `pwsh -File Tools/test-offline.ps1` | `SUMMARY ... failed=0` |

- `check-defs.ps1` 和 `test-offline.ps1` 都拿 `1.7/Assemblies` 里的 DLL，所以改了 C# 先构建再跑它们。
- 分层：静态可见的（Def 结构、引用、Harmony 补丁绑定）归编译器和 `check-defs`；`Tests/WhisperOfTheForest.Tests` 只测静态看不见的跨模块交接与纯规则（法术语法、载体准入、魔源连网、轨道炮走廊）；实机表现归用户。测试用真实 `DefDatabase` 注册最小 Def，不读模组 XML，不断言设计数值。
- 交接逻辑硬绑在 `ThingComp`/`Find.*` 里时，只把那一小段规则提成产品与测试共用的无状态静态方法（如 `ManaTopology.Group`）；不加接口包装、Mock、Shim 或测试专用成员。
- **RimWorld 正在运行时**，`build.ps1` 编译到临时目录，只验证编译通过，不更新 `1.7/Assemblies` 的 DLL。这时改用 `pwsh -File Tools/build.ps1 -Verify`，它拿刚编出的临时 DLL 跑 `check-defs`。新 DLL 要等游戏关掉后再构建一次才生效，汇报里写明“DLL 未更新”。`test-offline.ps1` 只认 `1.7/Assemblies` 的 DLL，这时会因 DLL 旧于源码而拒跑，汇报写“未运行”。
- 代理不启动游戏，不关闭、不结束任何 RimWorld 进程，也不要求用户关游戏来配合验证。
- 不为验证写游戏内探针命令或测试宿主。本仓库没有游戏内测试，也不再加；离线测试只在新增跨模块交接时补，一种行为一条。
- 实机表现（能不能用、画面、动画、手感、声音）由用户在游戏里看。用户当次明确要求时才启动游戏。
- 实机报来的错：按用户给的现象和日志定位，修完跑上面三条命令。
- 不算验证证据：用 `Shims.cs` 重写原版类型的 harness、匹配源码文本、一次性探针脚本、子代理口头“已完成”。

## 汇报格式

- 一句话结果、改动文件清单。
- 三条命令的原始输出（警告与错误数、`Summary`/`SUMMARY` 行）。没跑的写“未运行”，不写成通过。
- 实机表现写“未验证，待用户看”。
