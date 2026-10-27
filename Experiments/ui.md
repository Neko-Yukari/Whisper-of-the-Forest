# UI 实验台实现记录（原生显示开关那排入口 + 悬浮入口）

范围：`Source/WhisperOfTheForest/UI/**`、`Textures/Experiments/UI/**`、`1.7/Languages/{ChineseSimplified,English}/Keyed/WotF_UI.xml`。
UI 不提供任何 Def（原 `1.7/Defs/Experiments/UI.xml` 的 `MainButtonDef` 已按用户要求移除），也没有 UI DefInjected 文件。
全部 C# 位于命名空间 `WhisperOfTheForest.Experimental`。本文件只记录本 worker 的实现事实，不改 `IMPLEMENTATION_PLAN.md` / `MODLOG.md`。

## 文件

| 文件 | 作用 |
| --- | --- |
| `UI/TechLabMod.cs` | 唯一的 `Mod` 子类（`TechLabMod(ModContentPack)`），持有 `TechLabSettings`，提供模组设置页 |
| `UI/TechLabSettings.cs` | `ModSettings` 持久化字段 |
| `UI/TechLabStyle.cs` | 配色、尺寸常量、图标查找（`ContentFinder<Texture2D>`，失败返回 null 时退回文字字形） |
| `UI/TechLabUi.cs` | 打开/关闭窗口、取选中角色、本地化取值、重置入口位置 |
| `UI/TechLabEntryOverlay.cs` | `GameComponent` 悬浮入口：拖动、点击、隐藏、屏内 clamp |
| `UI/Window_TechLab.cs` | 实验台窗口 |
| `UI/TechLabDrawScope.cs` | 保存/恢复 `Text.Font/Anchor/WordWrap` 与 `GUI.color`，避免绘制状态泄漏 |
| `UI/Patch_PlaySettingsRowEntry.cs` | Harmony postfix（`GlobalControlsUtility.DoPlaySettings`）：向原生 `WidgetRow` 追加实验台图标 + 回传 `y` + 记录行几何 |
| `Textures/Experiments/UI/TechLabIcon.png` | 128×128 图标（深林底 + 灵光点环），同时用于原生那排图标与悬浮入口 |
| `1.7/Languages/ChineseSimplified/Keyed/WotF_UI.xml`、`1.7/Languages/English/Keyed/WotF_UI.xml` | UI 文本（33 个 key，只在 1.7 load folder 生效） |

## 交互规则（已实现）

1. **进入游戏窗口关闭**：没有任何自动 `Open` 路径；`PostOpen`/`GameComponent` 构造/加载回调都不开窗。窗口开闭状态不写入任何持久化数据。
2. **打开方式（两条，都在右下角原生控件区附近）**
   - 原版“显示开关”那排（PlaySettings）：新增一个 24×24 图标，**由原生 `WidgetRow` 追加**，随该排的间距/方向/换行排布；点击 → `TechLabUi.OpenWindowAndRestoreEntry()`（打开窗口，并若悬浮入口被隐藏则恢复它）。
   - 右下角悬浮入口：单击 → `TechLabUi.ToggleWindow()`（开关窗口）；可拖动、可隐藏。
   - 底栏 `MainButtonDef` 入口已移除（用户截图里的红叉位置），底栏恢复原版布局。
3. **悬浮入口拖动**
   - 左键按下记录起点；位移超过 5px（`TechLabStyle.EntryDragThreshold`）才算拖动。
   - 只有"未拖动"的抬起才触发点击（打开窗口 / 角落 × 隐藏），拖动结束只持久化位置，不会误开窗口。
   - 每帧把矩形 clamp 到 `[4, screenW/H - size - 4]`，分辨率变化后自然回到屏内；非自定义位置时按屏幕尺寸变化重算默认位置。
   - 默认位置取原版显示开关那排控件的左上方（`row.FinalX/FinalY`，在追加图标之前采样），首次绘制用估算值，探针数据到齐后对齐一次。
4. **隐藏与恢复**
   - 入口右上角 16×16 的 `×` 是 show/hide 控件：置 `showFloatingEntry=false` 并立即写盘，同时弹一条静默提示。
   - 恢复：点原版显示开关那排里的实验台图标（同时开窗并恢复入口），或窗口内“显示右下角悬浮入口”勾选（模组设置页同名字段）。
5. **窗口**
   - `draggable=true`（本体可拖）、`doCloseX=true`、`doCloseButton=false`、`forcePause=false`、`onlyOneOfTypeAllowed=true`、
     `preventCameraMotion=false`、`absorbInputAroundWindow=false`、`closeOnClickedOutside=false`（默认值）、`closeOnAccept=false`。
   - 关闭后入口保留；`WindowStack.WindowOfType<Window_TechLab>()` 判重，悬浮入口的点击只切换。
   - 初始位置：屏幕中心偏右 60px、上移 30px，并 clamp 在屏内；`Notify_ResolutionChanged()` 走 `SetInitialSizeAndPosition()` 重算。
6. **不与相机拖动冲突**：入口只在自己的矩形内 `Event.Use()`（鼠标事件），矩形外不消费事件；窗口 `preventCameraMotion=false` 且不吸收窗口外输入。
   悬浮入口绘制在 `GameComponentOnGUI`（早于 `WindowStackOnGUI`），窗口永远画在入口之上；鼠标下方有窗口时入口不自作响应（`WindowStack.GetWindowAt`）。
7. **保留原版控件布局**：只在原版那排末尾**追加**一个 `WidgetRow` 图标，没有移动/替换任何原版图标；没有新增底栏主按钮，也没有任何绝对坐标定位。

## 持久化字段（`TechLabSettings`，ModSettings）

| 字段 | 默认 | 说明 |
| --- | --- | --- |
| `showFloatingEntry` | `true` | 悬浮入口显示开关 |
| `hasCustomEntryPosition` | `false` | 用户是否拖过入口 |
| `entryX` / `entryY` | `-1` | 入口左上角 GUI 坐标；`hasCustomEntryPosition=false` 时忽略 |
| 窗口开闭 | —— | **不持久化** |

写入时机：拖动结束、勾选/取消显示、重置位置。`ModSettings.Write()` 会立刻写
`Config/Mod_<模组文件夹名>_TechLabMod.xml`（见下方 IL 证据）；`showFloatingEntry` 的重置逻辑在模组设置页与窗口内共用同一字段。

## 窗口内容（全部为真实操作，无占位）

- 选中角色卡片：`Find.Selector.SingleSelectedThing`，否则取 `SelectedPawns[0]`；无选中时显示提示且按钮 `active=false`。
- 灵能飞弹：`LabActions.GrantPsycast(Pawn)` + 一段规则/施放指引（飞弹 Gizmo 由飞弹 worker 提供，本窗口不依赖任何 Missile 类型）。
- 移动装备：`LabActions.EquipTerrainStride(Pawn)`、`LabActions.EquipBlinkStride(Pawn)`，同一行两个半宽按钮。
- `LabActions.RemoveExperimentApparel(Pawn)`。
- 入口设置：显示开关 + “重置位置”（仅自定义位置时可点）。
- 底部状态行：成功显示“已执行：<操作>（<角色>）”，异常捕获后 `Log.Error` 并在窗口内显示失败，不炸窗口。

调用 `LabActions` 的事实（已核对 `Source/WhisperOfTheForest/Lab/LabActions.cs` 现版本）：四个方法都是 `public static void X(Pawn)`，
失败时 `ThrowIfFailed` 抛 `InvalidOperationException(LastError)`；本窗口用 `catch` 显示 `exception.Message`，因此不额外依赖 `LastError` 成员，
同一失败仍由 `LabActions` 自己写 `ExperimentLog.Record`（窗口不重复记录）。

## 真实 API 证据（Mono.Cecil 读 `Assembly-CSharp.dll`，1.7.9790.38077 / .NETCoreApp v10.0）

| 结论 | 证据 |
| --- | --- |
| 窗口字段与虚方法 | `Verse.Window`：`windowRect`、`doCloseX`、`draggable`、`forcePause`、`preventCameraMotion`、`absorbInputAroundWindow`、`onlyOneOfTypeAllowed`；`DoWindowContents(Rect)`、`PostOpen()`、`WindowUpdate()`、`OnCloseRequest()` 均 virtual |
| 初始尺寸/位置可覆盖 | `Verse.Window.SetInitialSizeAndPosition()` 是 `protected virtual`，且 `PreOpen()` 第一句 `callvirt SetInitialSizeAndPosition()`；`Notify_ResolutionChanged()` 同样调用它 |
| 窗口拖动 | `InnerWindowOnGUI`：`draggable` 为真时执行 `GUI.DragWindow()`（整窗可拖） |
| 关闭按钮语义 | `Window..cctor`：`CloseButSize = (120,40)`；`InnerWindowOnGUI` 里 `doCloseX` → `IWindowDrawing.DoClostButtonSmall(windowRect)`（右上角小 X），`doCloseButton` → 底部大按钮 |
| 显示开关那排控件 = 右下角 | `RimWorld.GlobalControls::GlobalControlsOnGUI`：`y = screenHeight - 35`；`DoPlaySettings(ref y)` 内 `row.Init(UI.screenWidth, y - TimeButSize.y, UIDirection 0, 141, 4)`；`TimeControls.TimeButSize = (32,24)`；行从右向左增长、超 141px 向上换行 |
| 原生那排的绘制入口（现用） | `RimWorld.GlobalControlsUtility.DoPlaySettings(WidgetRow rowVisibility, bool worldView, ref float curBaseY)` 是 **public static**。Postfix 使用同一个 `rowVisibility` 追加图标，再将 `rowVisibility.FinalY` 写回 `curBaseY`，让时间和日期控件跟随换行。Harmony 按参数名绑定，名称必须与真实目标一致 |
| 原生图标控件语义 | `WidgetRow.ButtonIcon` → `ButtonIconRect`（先 `IncrementYIfWillExceedMaxWidth`，再取 `LeftX(size)`）+ `Widgets.ButtonImage`；返回 `ButtonInvisible` 的点击结果。`ButtonInvisible` 用 `GUI.Button(rect, "", EmptyStyle)`，会正常消费事件，不会漏成地图点击 |
| GameComponent 自动注册 | `Verse.Game::FillComponents()` → `GenTypes.AllSubclassesNonAbstract(typeof(GameComponent))`，再 `Activator.CreateInstance(type, new object[] { this })`：**子类必须提供 `ctor(Game)`**（实测曾因无参构造报 MissingMethodException，见缺陷记录） |
| 静态资源字段限制 | `Verse.StaticConstructorOnStartupUtility.ReportProbablyMissingAttributes()`：凡是"无 `[StaticConstructorOnStartup]` 且有 static 字段类型可赋给 `Texture/Material/Shader/Graphic/GameObject/MaterialPropertyBlock`"的类型，启动时 `Log.Warning("Type X probably needs a StaticConstructorOnStartup attribute...")` |
| GUI 顺序 | `UIRoot.UIRootOnGUI()` 调 `GameComponentUtility.GameComponentOnGUI()`；`UIRoot_Play.UIRootOnGUI()` 之后才 `WindowStack.WindowStackOnGUI()` → 入口在下、窗口在上 |
| Mod / ModSettings | `Verse.Mod` 有 `GetSettings<T>()`、`WriteSettings()`、`DoSettingsWindowContents(Rect)`、`SettingsCategory()`；`ModSettings.Write()` → `LoadedModManager.WriteModSettings(folderName, modTypeName, settings)` |
| Mod 类实例化 | `LoadedModManager.CreateModClasses()` 遍历 `GenTypes.InstantiableDescendantsAndSelf(typeof(Mod))`，逐个 `Activator.CreateInstance(type, content)`；同一 DLL 内多个 Mod 子类都合法 |
| 本地化格式化 | `Translator.Translate(string, params object[])` 在 1.7 已标记过期，改用 `string.Format(Translator.Translate(key), args)` 处理 `{0}`（`TranslatorFormattedStringExtensions` 只接受 `NamedArgument`） |
| 音效 | `Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDef, Map = null)`（扩展方法，需 `using Verse.Sound`） |

## 编译证据（实际执行）

- 工具链：`E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/dotnet/dotnet.exe`（SDK 10.0.402，集成 worker 提供）。
- 主检查工程（用**仓库里真实的** `LabActions`/`ExperimentLog`/`Json`/`BoundedJsonlWriter`）：`Moddev/tools/check-ui/proj-real/UI-check-real.csproj`，
  `net10.0`，`Compile Include` 本 worker 的 `Source/WhisperOfTheForest/UI/*.cs` 与上述 Lab 文件，
  引用 `Assembly-CSharp.dll`、`UnityStubs.dll`、`Waylith.dll`、`.../2009463077/1.7/Assemblies/0Harmony.dll`。
  结果：`已成功生成`，**0 warning / 0 error**（`WotF_UI_Check_Real.dll`）。
- 备用工程（`proj/UI-check.csproj`，LabActions 空桩）同样 0/0，用于隔离确认 UI 本身不依赖 Lab 其他实现细节。
- 两个临时工程都只写到 `Moddev/tools/check-ui/**`，不触碰最终 `1.7/Assemblies`、项目 `obj/bin`。
- 首轮静态检查输出 `UI CHECK OK`，但遗漏了 Harmony 参数名检查，未能发现缺陷 7。现有 `Moddev/tools/check-ui/check-ui.ps1` 已支持 `-AssemblyPath <最终 DLL>`，直接检查交付产物；校验项如下，最终执行结果见本轮集成记录：
  - 错误底栏入口已移除：`1.7/Defs/Experiments/UI.xml` 不存在、`TechLabMainButtonWorker.cs` 不存在、`1.7/Defs/**` 内无 `<MainButtonDef>`；
  - 源码里存在原生 `rowVisibility.ButtonIcon(...)`；
  - 源码引用的 33 个 `WotF_UI.*` key 在 zh/en 两份 Keyed 中都存在且无多余 key；
  - Harmony 特性目标解析为 `RimWorld.GlobalControlsUtility.DoPlaySettings(WidgetRow, Boolean, Single&)` 且 `public=True static=True`；Postfix 的每个参数名和类型均与真实目标一致，失败返回非零退出码；
  - `TechLabEntryOverlay` 基类 `Verse.GameComponent` 且存在 `ctor(Verse.Game)`（`FillComponents` 用 `Activator.CreateInstance(type, new object[]{game})`）；`Window_TechLab` 基类 `Verse.Window`；
  - 所有含 static `Texture2D/Texture` 字段的类型都带 `[StaticConstructorOnStartup]`（当前只有 `TechLabStyle`）；
  - 无 `Text.WordWrap = false`，且四个绘制入口（overlay/window/settings/row patch）都使用 `TechLabDrawScope`。

## 实机缺陷与修复（2026-10-25 17:54 启动）

1. **`MissingMethodException`：入口完全不出现**
   `Player.log 17:55:49 ERROR Could not instantiate a GameComponent of type WhisperOfTheForest.Experimental.TechLabEntryOverlay: System.MissingMethodException: Constructor on type ... not found.`
   根因：`Game.FillComponents()` 用 `Activator.CreateInstance(type, new object[] { this })`，`GameComponent` 子类必须是 `ctor(Game)`；原先只有隐式无参构造。
   修复：`public TechLabEntryOverlay(Game game) { }`（基类 `GameComponent` 无参构造隐式调用）。已用 Cecil 复核编译产物：该类型只有 `ctor(Verse.Game)`，无参构造已不存在。这是"入口未出现"的唯一原因，绘制逻辑本身没执行过。
2. **`Type TechLabStyle probably needs a StaticConstructorOnStartup attribute ... of type Texture2D`**
   根因：`TechLabStyle` 有 `private static Texture2D icon`；`StaticConstructorOnStartupUtility.ReportProbablyMissingAttributes()` 对"无该特性的静态资源字段类型"逐个发 Warning（同一次启动 17:54:12）。
   修复：给 `TechLabStyle` 加 `[StaticConstructorOnStartup]`；其静态构造只创建 `Color` 常量，`icon` 仍是 null，纹理由 `ContentFinder<Texture2D>.Get(...)` 在 OnGUI 主线程惰性加载。
3. **语言文件移到 `1.7/Languages`**
   `WotF_UI.xml`（zh/en Keyed + zh DefInjected MainButtonDef）从根 `Languages/` 移到 `1.7/Languages/` 同结构。依据：`LoadedLanguage.AllDirectories` 按 `ModContentPack.foldersToLoadDescendingOrder` 枚举语言目录，`LoadFolders.xml` 的 `v1.7` 含 `1.7`、`v1.6` 只含 `/`，所以 1.6 不会再加载这批带 Def 依赖的文本；`Textures/` 未移动。
4. **英文 9 条 translation error 的归因（我的文件无关）**
   `LoadedLanguage.InjectIntoData_AfterImpliedDefs()` 统计的是 `DefInjectionPackage.loadErrors`；`SetDefFieldAtPath` 把节点名按 `.` 切分后**用第一段当 defName**，不会剥离 Def 类型前缀（`BackCompatibleKey` 也不剥离）。因此 `<ThingDef.X.label>` 这类写法会被判成 defName=`ThingDef`。
   扫描当前 profile 的英文 DefInjected（含本仓库与 HAR/RimUnity/Harmony）恰好 9 条命中，全部属于另外两个 worker：
   - 4 条：`1.7/Languages/English/DefInjected/ThingDef/WotF_Movement.xml`（`ThingDef.WotF_TerrainStride.label/description`、`ThingDef.WotF_BlinkStride.label/description`）
   - 5 条：`Languages/English/DefInjected/AbilityDef/WotF_ArcaneMissiles.xml`（2）+ `Languages/English/DefInjected/DamageDef/WotF_ArcaneMissileDamage.xml`（3）
   我的文件贡献 0：我只新增 Keyed（不参与注入，key 唯一）与**中文** DefInjected，且中文文件用的是正确语法 `<WotF_TechLab.label>`。
   修法（属他人写域，我未改动）：节点名去掉 Def 类型前缀，写成 `<WotF_TerrainStride.label>` 等；中文同名文件同样需要改，否则切中文后会出现同类错误。
5. **`Word wrap was false at end of frame.`（18:41:16，点开入口后）**
   根因：`Verse.Text.StartOfOnGUI()` 在每帧开头断言 `WordWrap == true`、`Anchor == UpperLeft`（否则 `Log.ErrorOnce`），而 `Window_TechLab` 的 `DrawPawnCard`/`DrawHint` 在收尾把 `Text.WordWrap` 硬写回 `false`（上一版 `Window_TechLab.cs` 第 164、184 行），窗口一绘制就把全局状态泄漏到帧末。
   修复（新文件 `UI/TechLabDrawScope.cs`）：`TechLabDrawScope.Begin()` 快照 `Text.Font / Text.Anchor / Text.WordWrap / GUI.color`，`Dispose()` 原样恢复；三个绘制入口各包一层：
   `TechLabEntryOverlay.Draw`（第 246 行）、`Window_TechLab.DoWindowContents`（第 48 行）、`TechLabMod.DoSettingsWindowContents`（第 33 行）。
   同时删掉所有 `Text.WordWrap = false` 与助手方法里的硬编码字体/对齐收尾（现仅保留两处 `WordWrap = true` 用于开启换行：`Window_TechLab.cs:156/179`，退出时由 scope 还原）。
   `check-ui.ps1` 增加回归守卫：源码出现 `Text.WordWrap = false` 或三个入口缺少 `TechLabDrawScope.Begin()` 都判失败。
6. **入口位置错误（2026-10-26 用户截图标注）**
   现象：底栏主按钮里的实验台图标被红叉标出（放错位置）；用户指出原生“显示开关”那排（时间/速度控制下方、主菜单栏上方）才是入口该在的位置。
   修复：删除底栏 `MainButtonDef`、专用 worker 和对应中文注入。`UI/Patch_PlaySettingsRowEntry.cs` 在原生 `WidgetRow` 中追加 24×24 图标。点击调用 `TechLabUi.OpenWindowAndRestoreEntry()`，打开窗口并恢复被隐藏的悬浮入口。图标随原生行布局，并将 `rowVisibility.FinalY` 写回 `curBaseY`。
7. **Harmony 参数名不匹配（a19 首次启动）**
   实机报告 `Parameter "row" not found`，导致 `PatchAll()` 未完成、`harmonyPatched=false`。首版 Postfix 使用了 `row/y`，游戏真实参数为 `rowVisibility/curBaseY`。源码已修正名称及引用；检查脚本新增真实参数名和类型匹配。编译成功本身不能证明 Harmony 运行时绑定成功。

### 中文资源核验（本功能）

| 项 | 结果 |
| --- | --- |
| `1.7/Languages/ChineseSimplified/Keyed/WotF_UI.xml` | 33 个 key，唯一、无空值，与源码引用一一对应（新增 `RowEntryTooltip` / `RowEntryTooltipRestore`） |
| `1.7/Languages/ChineseSimplified/DefInjected/**` | 本功能已无 DefInjected（`MainButtonDef` 注入随底栏 Def 一起删除）；上一轮验证过的节点语法/字段存在性记录见缺陷 3/4 |
| 重复 key | 1.7 中文 Keyed 全量 36 个 key（本功能 33 + 移动 3），无重复 |
| 结论 | 本功能中文资源无注入/格式错误；日志里那 1 条 Simplified Chinese error 与 UI 无关（仓库内唯一结构可疑的中文注入是既有精灵内容 `Languages/ChineseSimplified/DefInjected/ThingDefs_Trait/Trait_Trance.xml`，指向 `Elf_Trance.degreeDatas.1.*` 而该 Def 只有 1 个 `degreeDatas` 元素；非本 worker 写域，需游戏内 translation report 定论） |

## a19 最终验证与边界

- 旧悬浮入口的打开、关闭、拖动和持久化沿用 a18 实机证据。修复后的最终 DLL 已通过参数绑定检查，实机 harmonyPatched=true，原生行绘制计数持续增长。
- c175848741995 隐藏入口，c175849320529 调用实际恢复回调后窗口打开、入口可见；加载 a19-ui 后窗口默认关闭。最终 c181603975660 为 windowOpen=false、showFloatingEntry=true、hasRow=true。
- 主代理已用一次只读窗口截图确认：固定入口在用户标注的右侧原生小图标区，底部主菜单旧入口消失。1920×1080 下追加前的行探针为 (1812,961)，该坐标不是最终图标矩形。
- 未验证中文/英文 tooltip 文案在原生 `ButtonIcon` 里的显示（`rowVisibility.ButtonIcon` 用 `TipRegion`，与原生图标同一机制）。
- 未验证 `×` 字形与 `Tiny` 字号在默认字体下的可读性（图标 PNG 已在本地渲染确认）。
- 原生图标的鼠标点击和悬停提示未实测；Lab 的 `ui action=restore` 调用同一恢复回调，但不能替代鼠标点击证据。

## 人工试玩参考（未全量重跑，不作为交付阻塞）

1. 加载存档：确认进入游戏时**没有**实验台窗口；**底栏不再有** WotF 图标（原版主按钮宽度恢复）。
2. 右下角原生那排（时间/速度控制下方、主菜单栏上方）最左侧应出现 24×24 灵光点环图标；鼠标悬停有中/英 tooltip。
3. 点该图标：打开窗口且悬浮入口可见（若之前被隐藏则应恢复）；再点只开窗、不重复开窗。
4. 窗口：拖标题/窗口体移动；点右上角 X 关闭；确认游戏未被暂停；窗口外拖动相机正常。
5. 悬浮入口：默认在原生那排左上方；单击打开/关闭窗口；按住拖动只移动不弹窗；右上角 × 隐藏后提示出现，再用第 3 步图标恢复。
6. 持久化：拖动位置与显示开关在退出重进后保持；窗口仍默认关闭。
7. 改分辨率（1920×1080 → 1280×720）：图标随原版那排重排（不重叠时间控件），悬浮入口不跑出屏幕。
8. 动作：选中角色后逐个点四个按钮，确认 `LabActions` 的授予/装备/卸下真实生效（飞弹用 Gizmo 施放）。

## 集成状态

1. 现有 `WhisperOfTheForest.csproj` 用默认 glob（`EnableDefaultCompileItems=true`），已自动包含 `Source/WhisperOfTheForest/UI/**`，无需改工程；DLL 输出到 `1.7/Assemblies`。
2. `Tools/verify.ps1` 已删除旧 `WotF_TechLab` 要求，改查原生入口及旧入口移除情况；最终 29 项检查通过。UI 现在不提供 Def 或 DefInjected。
3. 现有 `Bootstrap.cs` 的单次 `PatchAll()` 自动应用 `Patch_PlaySettingsRowEntry`；补丁追加图标并回写 `curBaseY`，无需另加 `PatchAll`。
4. `LabActions` 四个方法请保持 `public static void X(Pawn)`，否则窗口调用处需要同步修改。
5. 若设置页分类标题需要显示在“选项 → 模组设置”，`SettingsCategory()` 已返回 `WotF_UI.SettingsCategory`（无需 `About.xml` 改动）。
6. 我的 UI 文本已只在 `1.7/Languages/**`；1.6 不加载 `WotF_UI` 文本。
7. 英文/中文 DefInjected 前缀问题不在 UI 写域，若仍存在由对应 owner 处理。
8. Lab 的 `ui status` 已输出 `HasRow`、行坐标、屏幕尺寸和 `RowEntryDraws`（GUI 通过次数），`ui action=restore` 调用实际恢复回调；它不等于鼠标点击验证。
