# 移动装备实验（TerrainStride / BlinkStride）

状态：已实现，独立编译与补丁目标静态核对通过；**未做游戏内实机验证**（交由集成与主代理的真实运行验收）。

归属：`Source/WhisperOfTheForest/Movement/**`、`1.7/Defs/Experiments/Movement.xml`、`Textures/Experiments/Movement/**`、`1.7/Languages/{ChineseSimplified,English}/{Keyed,DefInjected/ThingDef}/WotF_Movement.xml`、本文件。根命名空间 `WhisperOfTheForest.Experimental`。

## 1. 方法体发现（本地 Assembly-CSharp.dll，Mono.Cecil/dotnet-ildasm 读取）

工具：`Moddev/tools/check-movement/il/dump-il.ps1`（Cecil 逐条 IL + 局部变量 + EH；IL 转储文本同目录）。以下均为 1.7.4985 实际方法体。

**`Pawn_PathFollower.CostToMoveIntoCell(Pawn, IntVec3)`（static，private）**

1. 基准 `ticks = TicksPerMoveCardinal/Diagonal`（按目标格与当前格同 x 或同 z 判断）。
2. `ticks += map.pathing.For(pawn).pathGrid.CalculatedCostAt(c, false, pawn.Position, GetPawnCellBaseCostOverride(pawn, c))`。
3. `ticks += GridsUtility.GetEdifice(c, map).PathWalkCostFor(pawn)`（建筑附加成本，独立于地形）。
4. 遍历 `terrain.tags`，`kindDef.moveSpeedFactorByTerrainTag` 命中则 `ticks /= factor`。
5. 上限 450，再按 `job.locomotionUrgency` 乘 3 / 2 / 1 / 0.75（0.75 分支 `RoundToInt`），最后 `Max(1f)`。

结论：地形减速只来自第 2 步的 base（地形 pathCost）与第 4 步的种族 tag 因子；两者都不是可写死的常量。

**`Pawn_PathFollower.GetPawnCellBaseCostOverride(Pawn, IntVec3)`（static，public）**
只有当 `terrain.IsWater()` 且 `pawn.WaterCellCost.HasValue` 时返回该值，否则返回 null（= 使用 `terrain.pathCost`）。

**`PathGrid.CalculatedCostAt(c, perceivedStatic, prevCell, baseCostOverride)`**

- 先做通行性判定：地形或任一 thing 为 `Impassable`（且非飞行/非 `forcePassableByFlyingPawns`）、围栏对非 fencePassable 的 context 直接 `return 10000`。**这一步在 base 覆盖之前**，所以覆盖 base 不会让墙/深水变得可走。
- `base = baseCostOverride ?? terrain.pathCost`（飞行 context 下取 0）。
- 之后逐 thing 取 `pathCost` 的**最大值**（不是累加），再对雪/沙（`MovementTicksAddOn`）取最大值，门邻接 +45，火邻近另加。

因此「忽略地形、保留其他障碍成本」可以把 **base 覆盖为 0** 精确实现，不需要重写 `PathGrid`，也不需要清共享缓存（`PathGrid.Cost` 是共享 NativeArray，`RecalculatePerceivedPathCostAt` 会全图失效，绝不能因穿着而重置）。

**`Pawn_PathFollower.TryEnterNextPathCell`（private）**顺序：
阻塞建筑→碰撞→（提前 return，未进入格子）→ 门前等待/开门 → `lastCell = pawn.Position`；`pawn.Position = nextCell`；朝向/入格 tick → clamor、`filth.Notify_EnteredNewCell`、雪 → 离开门的关闭逻辑（会 return）→ 绳索 → `AtDestinationPosition`→`PatherArrived` → `NodesLeftCount <= 1`→`TrySetNewPathRequest` → `SetupMoveIntoNextCell`。

**`SetupMoveIntoNextCell`（private）**
守卫 `NodesLeftCount <= 1` → `Log.Error` + `PatherFailed`；`nextCell = curPath.ConsumeNextNode()`；若与当前位置相同则再消费一个；`!WalkableBy(nextCell)`→`TrySetNewPathRequest`；`cost = CostToMoveIntoCell(nextCell)`；`nextCellCostTotal = cost`，`nextCellCostLeft = Max(cost + Min(旧left,0), 1)`；重算碰撞缓存与移动百分比；下一格有门则 `door.Notify_PawnApproaching`。

**`PawnPath`**：`nodes` 是**反序**（`nodes[0]`=终点，末尾=起点），`curNodeIndex` 指向“当前所在节点”，`NodesLeftCount = curNodeIndex + 1`，`ConsumeNextNode() = Peek(1)` 且 `curNodeIndex--`。所以“下一格 = `Peek(1)`”，而“给后续准备的那一格”由 `SetupMoveIntoNextCell` 消费。

**`Notify_Teleported_Int()` = `StopDead()` + `ResetToCurrentPosition()`**，会销毁 `curPath` 并重建路径请求；`Pawn.Notify_Teleported(endCurrentJob, resetTweenedPos)` 内部也调用它。因此两者都**不能**用于“沿原路线跳一格”，否则每次跳跃都会丢弃路径节点和重算路线。

## 2. 补丁与实现理由

两个补丁都只带 Harmony 特性，由公共 Bootstrap 的 `PatchAll()` 发现，本功能不创建 Harmony 实例、不自己 PatchAll。

**TerrainStride** — `[HarmonyPostfix] Pawn_PathFollower.GetPawnCellBaseCostOverride(pawn, c, ref int? __result)`

- 仅当 `__result` 无值且穿戴者穿 `WotF_TerrainStride` 时写入 `0`。
- 已有值（水体成本等种族/状态覆盖）保持原样：那是佩带着自身的属性，不是地形。
- 不改 `PathGrid`、不改 `TicksPerMove*`、不改任何 stat。脱装后下一次计算立刻回到原版（无状态）。
- 已知取舍：寻路**规划**仍使用共享 `PathGrid` 成本，所以穿戴者可能仍会绕开泥地；被消除的是“走上去之后的额外耗时”。这是刻意的（避免全局共享缓存按 pawn 变化）。
- 调用面核对（全模块扫描 `Moddev/tools/check-movement/il/scan-callers.ps1`）：`GetPawnCellBaseCostOverride` 只被两处调用 —— `CostToMoveIntoCell`（移动成本）与 `RCellFinder.CanWanderToCell`（闲逛目标：`override ?? terrain.pathCost > 20` 则放弃该格，且 `terrain.avoidWander` 在更前面直接排除）。因此穿着者不会被允许闲逛到 `avoidWander` 地形；只有 pathCost>20 的非 avoidWander 地形会因覆盖 0 而变得可被选中，vanilla 地形 pathCost 最大 14，实际无可观察变化。
- `moveSpeedFactorByTerrainTag` 只在地形 tag + 该 PawnKindDef 有因子时生效；vanilla 仅昆虫/部分动物定义（`Races_Animal_Insect.xml`、Odyssey 动物），humanlike 没有该字典项。因此对测试用人类不构成地形减速，未做处理；对异种（昆虫类）穿着者仍保留该生物特性。

**BlinkStride** — `[HarmonyPrefix]` 记录位置 + `[HarmonyPostfix] Pawn_PathFollower.TryEnterNextPathCell`

执行条件（全部满足才跳）：本调用确实进入了新格（`Position != 调用前位置`）、`pather.Moving`、`curPath != null && Found`、`nextCell != Position`（排除到达/关门/绳索/重算分支）、`NodesLeftCount >= 2`（`nextCell` 不是终点）、`BlinkRule` 通过。

跳跃动作：
1. `pawn.Position = pather.nextCell`（`nextCell` 已由原版 `SetupMoveIntoNextCell` 消费并配好成本，所以这是“路线的下一格”，不是另算的格子）。
2. `pawn.Drawer.tweener.Notify_Teleported()`：只重置绘制插值（该字段 public），使 2 格位移读作“跳”，不复用 `Pawn.Notify_Teleported`（那会清路径）。
3. `AccessTools.Method(typeof(Pawn_PathFollower), "SetupMoveIntoNextCell").Invoke(pather, null)`：让原版继续消费**再下一格**、算成本、检查可走性、通知门。方法缺失时只记一次 `Log.ErrorOnce` 并跳过跳跃（不会崩）。

不跳的情形与理由：

- 进/出**门格**一律不跳（`GridsUtility.GetDoor`），门开、关门、`FreePassage`、`StartManualCloseBy` 全部留给原版，门状态不会被跳过。
- 终点剩一格 → 正常走过，绝不越界（`NodesLeftCount < 2` 直接返回；`SetupMoveIntoNextCell` 的 `<=1` 守卫也因此不会被触发）。
- 目标格 `!WalkableBy` → 记 `not-walkable` 并交给原版重算。
- 斜角：两侧正交格都不可走 → `diagonal-corner`；两侧都是门格 → `diagonal-door-squeeze`（与原版“两个 pass-through-only 邻格不能斜穿”一致）。
- 目标格有阻挡的 pawn → `pawn-occupied`。
- 状态无关：不保存任何 per-pawn 跳跃状态/队列，路径取消、重算、脱装都不会留下残留，也不会污染后续 job/预约（预约属于 job，本功能从不改写）。

**每 tick 只发生一次**：`PatherTick` 只在 `nextCellCostLeft <= 0` 时调用一次 `TryEnterNextPathCell`，我们的跳跃发生在该调用的尾部，并在同一次调用内把后续格准备好，因此不会递归、也不会每帧 `SetPosition`。全模块调用扫描显示 `SetupMoveIntoNextCell` 只有两个调用者：`PatherTick`（刚开始使用新路径时）与 `TryEnterNextPathCell`（进入一格之后）。我们的 postfix 只挂在后者，所以**起步第一格一定是走**，不会出现开局瞬移。

诊断/证据（`StrideDiagnostics`，public API，不含假测试）：

- `GetSnapshot(pawn)` → walked / blinked / blocked / terrainCellsFreed / first-last tick。
- `Describe(pawn)` → 一行英文技术摘要（供日志/测试脚本）。
- `DescribeLocalized(pawn)` → Keyed 本地化摘要（UI 可复用）。
- `DescribeTerrainCost(pawn, cell)` → 该格 `pathCost` 与穿着后的有效地形成本（对照证据）。
- 日志经 `ExperimentLog.Record("movement.<category>", pawn, detail)`（Keyed 文本），类别为 `normalStep` / `blink` / `blocked` / `terrain` / `apparel`；每 pawn/category 前 3 条必记，之后每 240 tick 最多一条，硬上限 40 条/类别；异常被吞并计数（`LogFailureCount`，日志故障不能影响 pather）。
- `CompStrideLog`（`Notify_Equipped`/`Notify_Unequipped`）在穿脱瞬间记一条，附当时计数，用于证明“卸下即恢复”。调用链已核对：`Pawn_ApparelTracker.Notify_ApparelAdded` → `Apparel.Notify_Equipped` → `ThingWithComps.Notify_Equipped` → `ThingComp.Notify_Equipped`（移除镜像），所以 `LabActions.Wear/TryDrop` 会真正触发本组件。

## 3. Def / 资源 / 本地化

- `WotF_TerrainStride`（地形轻步披肩）：`ApparelBase` → `Middle` 层 + `Torso` 部位组。
- `WotF_BlinkStride`（闪现腰带）：`Belt` 层 + `Waist` 部位组。
- 选择不同层且不同部位组 → 两件可**同时**穿戴（这也是护盾腰带式 `Belt/Waist` 的常见组合）。代价：披肩占用 Middle/Torso（普通衬衫槽），与普通衬衫互斥。
- 两者都是 `Apparel`，无 recipe / 无 research：由实验 Lab 发放（`LabActions.EquipTerrainStride` / `EquipBlinkStride`），不影响正常殖民者，只有穿上的 pawn 才改变行为。
- 贴图：`Textures/Experiments/Movement/{TerrainStride,BlinkStride}.png` 是物品本体（地面）图，另有 `{TerrainStride,BlinkStride}_{Male,Female,Thin,Hulk,Fat}.png` 作为**穿戴图**（vanilla 的 `BodyTypeDef` 全集共 5 个，精灵种族 `bodyTypes` 只有 `Thin`）。全部为 64×64 原创占位，五个体型复用同一张图；`apparel.wornGraphicPath` 仍指向无后缀路径，由游戏按体型拼接后缀查找（见 §8 的 IL 依据）。
- 本地化：`1.7/Languages/<lang>/Keyed/WotF_Movement.xml` 提供状态行与穿脱提示（被 `StrideDiagnostics.DescribeLocalized`、`CompStrideLog` 实际使用）；`1.7/Languages/<lang>/DefInjected/ThingDef/WotF_Movement.xml` 提供中文/英文 label 与 description。放在 `1.7/` 下而不是仓库根，是因为 `LoadFolders.xml` 的 `<v1.6>` 只加载 `/`：根目录的 DefInjected 会在 1.6 指向不存在的 Def 而报 “def not found”，移入 `1.7/` 后 1.6 完全看不到实验 Def 与实验翻译。

## 4. 已执行的验证

1. 独立编译（不触碰集成的 csproj/obj/bin）：

   ```
   $env:DOTNET_CLI_HOME='E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/dotnet-home'
   $env:DOTNET_MULTILEVEL_LOOKUP='0'
   E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/dotnet/dotnet.exe build \
     E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/check-movement/check-movement.csproj -t:Rebuild
   ```

   结果：`EXIT=0`，0 error / 0 warning（日志 `Moddev/tools/check-movement/build.log`）。引用 `Assembly-CSharp.dll`、`UnityStubs.dll`、`Waylith.dll`、`0Harmony.dll`，全部 `Private=false`，不带游戏 DLL。`ExperimentLog` 用 `tools/check-movement/Stubs/ExperimentLogStub.cs` 占位（contract：`static void Record(string, Pawn, string)`）。
2. 补丁目标静态核对（`tools/check-movement/verify-patches.ps1`）：
   - `GetPawnCellBaseCostOverride` → `static=True public=True params=pawn:Pawn,c:IntVec3`（postfix 参数名与类型匹配）。
   - `TryEnterNextPathCell` → `static=False public=False params=0`（prefix/postfix 无方法参数，正确）。
   - `SetupMoveIntoNextCell` → 实例私有 0 参数（反射调用合法）。
   - `pawn` 字段为 **private**，因此用 Harmony 字段注入 `___pawn`（不能写 `__instance.pawn`，编译期已证实 CS0122）。
   - 构建产物中两个补丁类带 `HarmonyPatch`，三个方法带 `HarmonyPrefix`/`HarmonyPostfix`（可被 `PatchAll` 发现）。
3. 与集成代码的联合编译（只读，不写集成 obj/bin/Assemblies）：
   `tools/check-movement/check-integration.csproj` 用真实 `Source/WhisperOfTheForest/**/*.cs`（含集成方的 `ExperimentLog`、`LabActions`、UI、飞弹）编译，`EXIT=0`、0 warning、0 error，输出 `tools/check-movement/bin/Debug/net10.0/check-integration-movement.dll`。说明 `ExperimentLog.Record(string, Pawn, string)` 契约一致、命名空间与类型无冲突。
4. 集成方实际部署的 `1.7/Assemblies/WhisperOfTheForest.dll`（17:42:46 首次含本功能；17:46:44 的当前版本）经 Cecil 检查已包含 `Movement.*` 全部类型，`Patch_GetPawnCellBaseCostOverride`、`Patch_TryEnterNextPathCell` 带 `HarmonyPatch`，`NoteWalked` 已是 3 参数版本（即 `movement.normalStep` 日志也在内），与当前源文件一致。
5. 仓库结构核对（只读）：`LoadFolders.xml` 的 `<v1.7>` 同时加载 `/` 与 `1.7`，因此根目录的 `Textures/`、`Languages/` 与 `1.7/Defs/` 都会在 1.7 载入，本功能的路径划分与之一致；`Bootstrap.cs` 只做一次 `new Harmony("NekoYukari.WhisperOfTheForest.Experiments").PatchAll()`。集成侧已提供入口：`LabActions.EquipTerrainStride/EquipBlinkStride`、`LabCommands` 的 `equip-terrain-stride`/`equip-blink-stride` 与窗口按钮、`status` 中的 Def 存在性检查。
6. 目标身体部位可用性：原仓库 `Defs/Bodies/Bodies_Elf.xml` 自带 `Torso` 与 `Waist` 部位组，因此两件装备在精灵/HAR 身体上同样可穿。

## 5. 未测项（需实机）

- Def 是否能正常加载（`ApparelBase` 继承、`Waist`/`Torso` 部位组、`CompProperties_StrideLog`、`wornGraphicPath` 在 1.7 Waylith 渲染管线下的表现；这两件只是占位外观）。
- 真实行走耗时对照：泥土 `pathCost=14`、沙 4、土 2（vanilla `Terrain_Natural.xml`），基础 13 tick/格时，泥地从 27 tick/格降到 13 tick/格，可用 `StrideDiagnostics.Describe` 与 `DescribeTerrainCost` 记录。
- 走一格跳一格的实际观感（`tweener.Notify_Teleported` 是否读作“跳”而非滑动）。
- 墙、关闭的门、非法斜角、被占据格、`NodesLeftCount` 末端与到达时是否零报错（预期不触发 `SetupMoveIntoNextCell` 的 `Log.Error`）。
- 路径中途取消/重算、卸装、换装、多人同时穿、caravan/世界地图、被击倒（`doesntMove`/`FullBodyBusy`）等边界。
- 连续长时间运行的日志上限行为（应只在穿脱与少量采样时写入）。

## 6. 给执行实机验收的人

1. 用 Lab 给同一个人类实验角色穿 `WotF_BlinkStride`（腰带槽）与 `WotF_TerrainStride`（披肩槽），确认两件可同时装备且外观可分辨。
2. 选一段 8–10 格直线泥土/沼泽（`pathCost=14`），记录 `ExperimentLog` 的 `movement.terrain`、`movement.blink`、`movement.blocked` 行与 `StrideDiagnostics.GetSnapshot`，与未穿时同一路线对比 tick 数与 `walked/blinked` 计数（预期 walked≈blinked，终点最后一格只走不跳）。
3. 建 1 格宽走廊 + 关闭的门、以及一个斜角墙角：确认没有穿墙/穿门/切角，日志出现 `door-cell` / `not-walkable` / `diagonal-corner` 之一，位置不越过阻挡。
4. 走到一半移除腰带：下一格开始不再瞬移，`blinked` 计数停止增长，无报错；再穿回后恢复。
5. 任何 `[WhisperOfTheForest]` 开头的 error/`Log.ErrorOnce` 都需要记录，特别是 `SetupMoveIntoNextCell not found`。

## 7. 配置错误修正与本地化分层（2026-10-25 实机加载后）

实机 `Harmony.PatchAll` 成功，但 `ThingDef.ConfigErrors` 对两件装备各报一条：

```
Config error in WotF_TerrainStride: is smeltable but does not give anything for smelting.
Config error in WotF_BlinkStride: is smeltable but does not give anything for smelting.
```

**IL 依据**（`Verse.ThingDef/<ConfigErrors>d__401::MoveNext`，转储见 `tools/check-movement/il/thingdef-configerrors.il`）条件为：

```
smeltable && smeltProducts.NullOrEmpty() && CostList.NullOrEmpty()
         && !IsStuff() && !MadeFromStuff() && !destroyOnDrop
```

两条 Def 都继承 `ApparelBase → ApparelNoQualityBase`，后者显式设 `<smeltable>true</smeltable>`，而实验物品没有 `costList`/`smeltProducts`（无配方、由 Lab 发放），于是命中该检查。

**决策与最小修正**：在两件 Def 上显式 `<smeltable>false</smeltable>`（就地注释说明原因），而不是补 `costList`/`smeltProducts`。理由：实验物品没有制作材料，任何回收产物或成本都是凭空发明的语义；关闭熔炼既不改变移动行为，也不新增配方与数值。反向错误（“has smeltProducts but has smeltable=false”）不会出现，因为我们没有任何 smeltProducts。

**同轮修正（1.6 隔离）**：把本功能 4 个本地化文件从仓库根 `Languages/` 移到 `1.7/Languages/`（`Keyed` 与 `DefInjected/ThingDef` 各两份）。`LoadFolders.xml` 的 `<v1.6>` 只加载 `/`，根目录的 `ThingDef.WotF_*` 注入会在 1.6 因 Def 不存在而报错；移动后 1.6 既不加载实验 Def 也不加载其翻译，其余实验模块的根级本地化未改动。

本轮检查：5 个 XML（Def + 4 个本地化）全部 `[xml]` 解析通过；`smeltable` 仅出现两处且均为 `false`；根 `Languages/` 已无 `WotF_Movement` 文件（其余功能的 `WotF_UI`/`WotF_ArcaneMissiles` 等保持原状）；本轮未改任何 C#、未自行重建 `1.7/Assemblies`（该 DLL 由集成方构建，当前 17:46:44 版本与源文件一致）。Def 改动需下次启动游戏生效，当前进程仍加载旧 Def。

### 7.1 DefInjected 节点路径修正（translation errors）

实机加载报 4 条翻译错误，全部来自本功能的 `1.7/Languages/{ChineseSimplified,English}/DefInjected/ThingDef/WotF_Movement.xml`：节点写成了带类型前缀的 `ThingDef.WotF_TerrainStride.label`。

**IL 依据**（`Verse.DefInjectionPackage::SetDefFieldAtPath`，转储 `tools/check-movement/il/setdeffield.il`）：方法拿到 path 后立即 `path.Split('.')[0]`，再用 `GenDefDatabase.GetDefSilentFail(defType, firstSegment, false)` 按 **defName** 查 Def；查不到就写 “Found no <defType> named <firstSegment>”。也就是说第一段是 defName，不是类型名；类型由所在目录 `DefInjected/ThingDef/` 提供。

旁证：仓库原有可用的 `Languages/ChineseSimplified/DefInjected/ThingDefs_Trait/Trait_Trance.xml` 用 `<Elf_Trance.degreeDatas.1.label>`；本机 1.7 模组 `1127530465/1.7/Languages/ChineseSimplified/DefInjected/HediffDef/*.xml` 用 `<ADSPain.label>`。两者都不带类型前缀。

**修正**：8 个节点（2 Def × label/description × 中英）由 `ThingDef.WotF_*` 改为 `WotF_*`，并在文件里加注释说明该约定；文本内容一字未改。`label` / `description` 已确认是 `ThingDef` 继承链上的 public 字段（`Def.label`、`BuildableDef.description`），可被 `SetDefFieldAtPath` 解析。

同轮复核：两份 Keyed 文件仍只含 `WotF_Movement_StatusLine` / `_Equipped` / `_Unequipped` 三个键，与 C# 中的三处实际使用完全对应（无缺失、无多余）。

## 8. 穿戴贴图缺失修正（`Failed to find any textures at …_Thin`）

实机日志 `Moddev/evidence/third-run-player.log:579`：

```
ERROR [Verse] Failed to find any textures at Experiments/Movement/TerrainStride_Thin while constructing Multi
  at Verse.Graphic_Multi.Init(GraphicRequest req)
  at Verse.GraphicDatabase.GetInner(GraphicRequest req)
```

**根因（IL 实证）**：

- `RimWorld.ApparelGraphicRecordGetter::TryGetGraphicApparel`：当最外层不是 `Overhead`/`EyeCover`、不是 `RenderAsPack`、且路径不是占位图时，取 `path = apparel.WornGraphicPath + "_" + bodyType.defName`（IL_0093–IL_00a9），再 `GraphicDatabase.Get<Graphic_Multi>(path, Cutout, def.graphicData.drawSize, DrawColor)`（IL_0131）。我们两件装备分别是 `Middle`、`Belt` 层，所以**必然**走这条加后缀分支。
- `Verse.Graphic_Multi::Init`：依次软查找 `path+"_north"`、`"_east"`、`"_south"`、`"_west"`；四者皆空时回退 `Get(path)`；若 north 仍为空则 `Log.Error("Failed to find any textures at " + path + " while constructing Multi")` 并把四个方向都换成 `BadMat`（IL_0139–IL_0187）。其余方向有空缺时按 `south=north`、`east=north`、`west=east`（或镜像相反对）补齐（IL_0188–IL_01f8）。

因此 `Textures/Experiments/Movement/TerrainStride.png` 只够物品本体，穿戴时游戏要的是 `TerrainStride_Thin`（该角色体型 Thin）→ 找不到任何贴图 → 报错且渲染 BadMat。BlinkStride 同一机制，只是那一次没人穿或穿着者体型未触发同一条错误。

**体型清单（实证）**：`Data/Core/Defs/Misc/BodyTypeDefs/BodyTypes.xml` 全集只有 5 个 `BodyTypeDef`：`Male`、`Female`、`Thin`、`Hulk`、`Fat`（Biotech/Odyssey 未新增）。精灵种族 `Defs/ThingDefs_Races/ElfRace.xml` 的 `<bodyTypes Inherit="False"><li>Thin</li>` 只用 `Thin`；两件装备 `developmentalStageFilter=Adult`，没有 Baby/Child 体型 Def 需要覆盖。

**朝向清单（实证）**：vanilla 装备贴图按 `Assets.zip` 中 `Things/Pawn/Humanlike/Apparel/Duster/Duster_<BodyType>_{east,north,south}` 布局，west 由 east 镜像；而 `Graphic_Multi` 的上述回退允许**只放一张无方向后缀的 `<path>.png`**，四个方向都会解析到它（north=plain，其余取自 north/east）。故本实验只需每个体型一张，不需要 4 张方向图。

**修正（仅资源，无 Def/C# 改动）**：在 `Textures/Experiments/Movement/` 新增 10 个文件，均为现有占位图的字节拷贝（不同体型同一张图，不精修美术）：
`TerrainStride_{Male,Female,Thin,Hulk,Fat}.png`、`BlinkStride_{Male,Female,Thin,Hulk,Fat}.png`。`apparel.wornGraphicPath`、`graphicData.texPath` 与两条 Def 均未改。

**静态检查**：`pwsh -NoProfile -File Moddev/tools/check-movement/verify-worn-textures.ps1` → `PASS: all 2 apparel defs have ground + 5 body-type worn textures`（逐项验证 PNG 签名/IHDR 尺寸 64×64、`layers` 是否触发后缀规则、地面图与 5 个体型穿戴图全部存在）。

**未验**：下次启动后的实际渲染效果（四个方向目前是同一张占位图；`graphicData.drawSize` 仍为默认值，各体型不做缩放调整）；本轮未启动游戏、未改 C#、未构建 DLL。
