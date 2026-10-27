# 奥术飞弹实验记录

负责人：missiles。范围：`Source/WhisperOfTheForest/Missiles/**`、`1.7/Defs/Experiments/Missiles.xml`、
`Textures/Experiments/Missiles/**`、`1.7/Languages/**` 下本功能的中英文 DefInjected、本文件。
最终 DLL 由集成负责人构建。

## 1. 结果概览

- 能力 `WotF_ArcaneMissiles` 是**真 Psycast**：`abilityClass` 继承 Core 的 `PsycastBase`（`RimWorld.Psycast`），
  Gizmo、预热、视线检查、冷却、灵能/熵机制全部来自原版，只额外挂一个自定义 `CompAbilityEffect`。
- 目标可选生物/建筑/地面坐标（`verbProperties.targetParams.canTargetLocations=true`）。
- 每次施法发射 3 枚追踪飞弹，速度 13 格/秒、最大转向 300°/秒；横向波动**只作用于渲染**。
- 每枚飞弹是真实的 `Verse.Projectile` Thing（`WotF_ArcaneMissileProjectile`）：生成/注册、tick、存档、销毁都由引擎负责，
  模组只提供追踪步进与命中载荷（详见 §9）。
- **视觉后端与命中策略由 Def 选择**：`settings.visualDef` / `settings.impactDef` 是 `Verse.Def` 引用，
  核心不 `switch` 具体后端；新增一种表现 = 一个生命周期类 + 一个 Def，新增一种命中 = 一个 worker 类 + 一个 Def。
- 两套**已实现并在本机 1.7 实机跑过命中验收**的视觉后端：`WotF_Visual_RimUnity`（RimUnity 粒子+拖尾）与
  `WotF_Visual_Native`（原版 Fleck，无 GameObject）。三套命中样例：`WotF_Impact_Damage / Heal / Explode`。
  六组实机结果见 §8；外观本身未作评估（由用户接手）。

## 2. 代码结构与扩展面

| 文件 | 职责 |
| --- | --- |
| `Missiles/ArcaneMissileDefs.cs` | `ArcaneMissileVisualDef`（`visualClass`）、`ArcaneMissileImpactDef`（`workerClass`）、Def 名常量与查找、`ConfigErrors` |
| `Missiles/ArcaneMissileSettings.cs` | 全部参数 + `visualDef` / `impactDef` + Scribe |
| `Missiles/ArcaneMissile.cs` | 单枚飞弹：限速转向、子步扫掠碰撞、到达判定、一次结算、Scribe；`Tick` 返回 `Flying/Impact/Expired` |
| `Missiles/ArcaneMissileImpactWorker.cs` | 命中上下文 `ArcaneMissileImpactContext` + 抽象 worker |
| `Missiles/ArcaneMissileImpactWorkers.cs` | 三个 worker 样例（伤害/治疗/爆炸） |
| `Missiles/ArcaneMissileVisualSource.cs` | 视觉生命周期抽象类、帧/爆点上下文、共享装饰波动 |
| `Missiles/ArcaneMissileVisualRimUnity.cs` | RimUnity 后端（GO + ParticleSystem + TrailRenderer） |
| `Missiles/ArcaneMissileVisualNative.cs` | 原版 Fleck 后端（无持久对象） |
| `Missiles/MapComponent_ArcaneMissiles.cs` | 地图级视觉门控（当前图判定、挂起/回图重建）、Lab 计数与快照；**不持有飞弹列表、不推进逻辑** |
| `Missiles/Projectile_ArcaneMissiles.cs` | `Verse.Projectile` 子类：自驱 `TickInterval`、位置同步、单次结算、视觉生命周期、`ExposeData` / `Destroy` |
| `Missiles/ArcaneMissileLauncher.cs` | 公开 API：`Fire*`、默认选择解析、用 `ThingMaker`+`GenSpawn` 把飞弹交给引擎、`SpawnVisualProbe` |
| `Missiles/CompAbilityEffect_WotFArcaneMissiles.cs` | XML 参数 + 效果 comp |

### 固定公共契约

```csharp
public ArcaneMissileVisualDef visualDef;   // null => 启动时用 WotF_Visual_RimUnity
public ArcaneMissileImpactDef impactDef;   // 非 null 时永远优先于 impactMode 枚举
public ArcaneMissileImpactMode impactMode; // 保留：impactDef 为空时按枚举映射 Def
```

- `visualDef`/`impactDef` 都会被 `Scribe_Defs` 保存（`ArcaneMissileSettings.cs:113-114`）；飞弹自己的 settings 是
  发射时的快照，因此读档后仍按当时选择的 Def 重建。
- 运行期解析：`ArcaneMissileLauncher.ResolveSelections`（`ArcaneMissileLauncher.cs:157`）只补 `null`；
  **显式选择的 Def 即使类型错误也不会被替换**：`ConfigErrors()` 在加载期报错，运行期每个 Def 只写一条
  `Log.Error`，然后该飞弹“没有视觉”或“没有效果”地正常结束（不会回退到另一个后端）。

### 组合调用示例

```csharp
using WhisperOfTheForest.Experimental.Missiles;
using Verse;

// 1) 默认：RimUnity 视觉 + 伤害
ArcaneMissileLauncher.FireAtCell(pawn, cell);

// 2) 换视觉：原版 Fleck
ArcaneMissileSettings s = ArcaneMissileLauncher.CreateDefaultSettings();
s.visualDef = ArcaneMissileDefs.VisualByName(ArcaneMissileDefs.NativeVisualDefName);
ArcaneMissileLauncher.FireAtThing(pawn, target, s);

// 3) 换事件：治疗（impactDef 优先于 impactMode；必须把同一个 s 传进 Fire）
s.impactDef = DefDatabase<ArcaneMissileImpactDef>.GetNamedSilentFail(ArcaneMissileDefs.HealImpactDefName);
ArcaneMissileLauncher.Fire(pawn, new LocalTargetInfo(target), s);

// 4) 只测某个视觉后端（无战斗效果）
ArcaneMissileLauncher.SpawnVisualProbe(map, cell, 3f, null,
    ArcaneMissileDefs.VisualByName(ArcaneMissileDefs.NativeVisualDefName));
```

**人类只换表现/事件**：改 XML 里 `<settings><visualDef>…</visualDef><impactDef>…</impactDef>`，或在 Lab/代码里改这两个字段；
`impactMode` 保留为无 `impactDef` 时的回退映射，二者不需要同时改。

### 新增后端（不改核心）

- 新视觉：写一个 `ArcaneMissileVisualSource` 子类（实现 `Spawn/Update/Impact/Probe/Dispose`），
  在 XML 按下面这种完整写法注册。**根标签必须是完整类型名**（`WhisperOfTheForest.Experimental.Missiles.*`），
  短标签只对 `Verse`/`RimWorld` 里的类型有效；写成短名会被游戏报
  `Type ... is not a Def type or could not be found` 并跳过该 Def。

```xml
<WhisperOfTheForest.Experimental.Missiles.ArcaneMissileVisualDef>
  <defName>WotF_Visual_MyBackend</defName>
  <label>arcane missiles (my backend)</label>
  <visualClass>WhisperOfTheForest.Experimental.Missiles.ArcaneMissileVisualRimUnity</visualClass>
</WhisperOfTheForest.Experimental.Missiles.ArcaneMissileVisualDef>
```

- 新事件：写一个 `ArcaneMissileImpactWorker` 子类（`Resolve` 必须无状态），注册方式（同样是完整根标签）：

```xml
<WhisperOfTheForest.Experimental.Missiles.ArcaneMissileImpactDef>
  <defName>WotF_Impact_MyEvent</defName>
  <label>arcane impact: my event</label>
  <workerClass>WhisperOfTheForest.Experimental.Missiles.ArcaneMissileImpactWorker_Damage</workerClass>
</WhisperOfTheForest.Experimental.Missiles.ArcaneMissileImpactDef>
```

  现有三个样例对应的完整类型：`WhisperOfTheForest.Experimental.Missiles.ArcaneMissileImpactWorker_Damage/_Heal/_Explode`。

## 3. XML 参数

`1.7/Defs/Experiments/Missiles.xml` 中 `<settings>` 的全部字段即 `ArcaneMissileSettings` 的公开字段。
该文件里 5 个 Def 的根标签是**完整类型名**（`WhisperOfTheForest.Experimental.Missiles.ArcaneMissileVisualDef` /
`…ArcaneMissileImpactDef`）：1.7 的 Def 加载器只对 `Verse`/`RimWorld` 类型解析短名，mod 命名空间里的 Def
必须写全名，否则实机日志会报 `Type ArcaneMissileVisualDef is not a Def type or could not be found` 并跳过该 Def。

| 字段 | 默认 | 说明 |
| --- | --- | --- |
| `missileCount` | 3 | 每次施法弹数（1–12） |
| `speedCellsPerSecond` | 13 | 飞行速度，格/秒 |
| `maxTurnDegreesPerSecond` | 300 | 最大转向角速度（13 格/秒时转弯半径约 2.5 格） |
| `wobbleAmplitude` / `wobbleFrequency` | 0.14 / 2.2 | **仅渲染**的横向摆动幅度（格）与频率（Hz）；默认最大视觉偏移 0.14 格 |
| `launchSpreadDegrees` | 8 | 一轮飞弹的扇形展开角 |
| `flightHeight` | 1.2 | 飞行高度（格） |
| `hitRadius` | 0.45 | 命中半径 |
| `lifetimeSeconds` / `maxRangeCells` | 10 / 34 | 超时与超距清理 |
| `damageAmount` / `armorPenetration` / `damageDef` | 7 / 0.5 / `WotF_ArcaneMissileDamage` | 命中策略使用的伤害参数 |
| `impactMode` | `Damage` | 无 `impactDef` 时的回退映射 |
| `impactDef` | `WotF_Impact_Damage` | 命中策略 Def（非 null 优先） |
| `visualDef` | `WotF_Visual_RimUnity` | 视觉后端 Def |
| `ignoreFlightLimits` | false | 仅跳过 `lifetimeSeconds`/`maxRangeCells`；出界、碰撞、`MapRemoved` 照旧；旧数值与 `Clamp` 不变 |
| `expireOnTargetLost` | false | 为 true 时必须有存活 Thing 目标；目标死亡/未生成/换图/读档丢引用时立即 `Expired`（`reason=targetLost`），不结算命中 |
| `explosionRadius` | 1.7 | 爆炸半径（Explode worker） |
| `healAmount` | 10 | 治疗量（Heal worker） |
| `missileSize` / `trailTime` | 0.55 / 0.35 | 粒子/拖尾尺寸与拖尾时长 |
| `missileColorHex` / `trailColorHex` | `#9BD8FF` / `#3F7DFF` | `#RRGGBB` 或 `#RRGGBBAA` |

能力本身：`level 1`，`Ability_EntropyGain 0`、`Ability_PsyfocusCost 0`（实验可重复施放），冷却 `90~120` tick，
`range 24.9`，`requireLineOfSight` 继承 true，`aiCanUse false`，`displayGizmoWhileUndrafted true`。

### 预设 A/B（已完成实机验证）

| 预设 | Def | 弹数 | 速度 | 转向 | 命中策略 | 结束规则 | 可选目标 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A 重弹 | `WotF_ArcaneMissiles_Heavy` | 1（spread 0） | 13 | 10°/s | `WotF_Impact_Explode` | `ignoreFlightLimits=true`（飞出地图/撞到物体才结束）；`expireOnTargetLost=false`，目标失效仍飞向最后已知格 | 生物/建筑/地面 |
| B 愈合弹 | `WotF_ArcaneMissiles_Mender` | 1（spread 0） | 4 | 720°/s | `WotF_Impact_Heal` | `ignoreFlightLimits=true` + `expireOnTargetLost=true`（目标失效直接消散，不治疗） | 仅 Pawn/Animal（`thingCategory=None` + 只开 pawn/animal/human） |

两者沿用 level 1 / range 24.9 / warmup 0.5 / cooldown 90~120 / 零消耗 / 同一图标 / RimUnity 视觉；未设 hotKey 以免冲突。
原 `WotF_ArcaneMissiles` 与旧存档行为未改（300°/s、34 格、10 秒、目标失效飞向 last cell）。

配置流程：复制一个 AbilityDef，修改 defName、标签及 comp 的 settings，选择已有的 `impactDef` 和 `visualDef`，重启游戏加载 XML。授予按钮和 `grant` 自动发现所有带飞弹 comp 的预设；`fire preset=<defName>`、`cast preset=<defName>` 读取同一份 settings，不需增加 C# 名单。只有全新的命中逻辑或视觉后端才需新增对应 worker。爆炸声音由 DamageDef 的 `soundExplosion` 配置，当前使用原版 `Explosion_Bomb`。

20 格 / 13 格每秒的几何仿真（直接运行生产 `ArcaneMissile`，使用 250×250 无墙地图替身；目标 DrawPos 每 tick 移动，不包含真实寻路与插值）：

- 静止目标：5/8/10/12/15°/s 在 10/15/20/25 格全部命中。
- 目标以 3.99（土壤）或 4.6（地板）格/秒全速横移，起步延迟 0/0.25/0.5 秒：**所有候选 0 命中**；
  5/8/10°/s **6/6 飞出 250 图**；12/15°/s 留在场内绕圈，20 秒内不再命中。
- 错过瞬间让目标停下：5–10°/s 仍出界；12/15°/s 留场但 60 秒内仍不命中（转弯半径 50–62 格，形成环绕）。
- 非规避移动：直线逃跑 3.99/4.6 全部命中；以 1.5 格/秒横走时 12/15°/s 命中，10°/s 在 20 格 miss、15 格 hit。
- 转弯半径（13 格/秒）：5°=149 格、8°=93.1、10°=74.5、12°=62.1、15°=49.7；半圈分别为 468/292/234/195/156 格。

**A 取 10°/s**：上述仿真中可横向规避，测试轨迹均飞出地图。12°/s 在部分场景形成长时间环绕，未采纳。该结果只适用于指定几何，不能保证任意起点、距离和地图中的出界行为。

B（720°/s，4 格/秒，转弯半径 0.32 格）：
- 闭合条件：同向直线逃跑时闭合率 = `vm - vt`；≤0 永远追不上，>0 时 10 格闭合时间 = `10/(vm - vt)`。
- 仿真：4 格/秒命中静止（136 tick ≈ 2.3 秒）与 2 格/秒（262 tick ≈ 4.4 秒）；在仿真时段内未追上土壤约 3.99 格/秒的目标（按未舍入速度计算，10 格理论追及约 865 秒），也无法追上同向持续以 4.6 格/秒奔跑的目标。
- 3 格/秒更慢（静止 180 tick、2 格/秒 529 tick），同样追不上健康奔跑的小人。
- 结论：B 面向站桩/慢速目标，不是追击弹；不提升到 8–10 格/秒。

`expireOnTargetLost` 的真实边界：目标 `Dead=true` 但仍 `Spawned`、未 `Destroyed` 时也立即失效（不假设死亡一定 despawn）；
`Spawned=false`、`thing.Map != map`、读档后 `targetThing == null` 同样 `Expired`，都不调用 worker。`Fire` 在该 flag 为 true 时
拒绝坐标目标与已死亡目标（返回 0 + `Log.Error`），不会先丢引用再落地面治疗。

## 4. 权威逻辑（与视觉完全解耦）

- **权威 tick**：由原生 `Projectile_ArcaneMissiles.TickInterval(delta)` 驱动（`Projectile_ArcaneMissiles.cs:66`），
  引擎把飞弹当普通 Thing 登记、逐个 tick、随存档序列化并在结束时 `Destroy`。飞弹不再由 `MapComponentTick` 推进：
  组件只剩地图级视觉门控与 Lab 计数（见 §5、§9）。`TickInterval` 里的 `delta` 是自上次调用以来的游戏 tick 数
  （原版 `Projectile.UpdateRateTicks`：屏内 1、屏外 15），因此循环 `delta` 次逐 tick 模拟，转向与碰撞采样不会因为
  屏外或倍率被跳过；harness 的 `native/delta-batch-equals-single-ticks` 断言 15 tick 批量与 15 次单 tick 结果完全一致。
- **转向**：每 tick 把朝向按 `maxTurnDegreesPerSecond/60` 向目标方向旋转，速度恒定；波动不参与这一步。
- **扫掠碰撞**：每 tick 位移按 ≤0.18 格切分子步；每子步先测 Pawn（`DrawPos` 距离 ≤ `hitRadius+0.3`），
  再测 `Fillage == Full` 的建筑/墙/门，最后测被指定的非 Pawn 目标；13 格/秒时每 tick 2 个子步。
- **到达判定也在子步内**：地面坐标目标检查每个子步到目标点的距离 ≤ `max(0.35, hitRadius)`。60 格/秒时
  tick 边界可能分别落在目标两侧 0.5 格（> 0.45），只在 tick 开始判定会永远错过并绕圈；墙体判定在到达之前，
  所以不会穿墙。
- **所有地图都在 tick**：`TickManager.DoSingleTick` 先对每张地图调 `MapPreTick`，末尾再调 `MapPostTick`，
  Thing 的 tick 走引擎 tick 链，所以非当前图的飞弹也照常推进；`MapComponentUpdate` 每帧对所有地图调用（含暂停），
  只用来做可见性与重建（§5）。
- **一次结算**：`ArcaneMissile.Tick` 在命中/过期时返回 `Impact`/`Expired`，`TickInterval` 立刻转给 `Settle`
  （`Projectile_ArcaneMissiles.cs:100`）：置 `settled`、通知组件计数、播放命中视觉、然后 `Destroy`。worker 抛异常只
  记录一次日志，仍然 `Impact` 并销毁——不会在后续 tick 重复伤害；`settled` 也保证同一枚弹不会二次结算。
- **施法者保护**：出生点前移 0.55 格，前 12 tick 跳过施法者。
- **目标丢失**：目标死亡/离图后不再追踪，仍飞向最后已知格子并在地面结算；原目标与原目标格保留在
  `ArcaneMissileImpactContext.OriginalTarget/OriginalTargetCell` 里交给事件。
- **清理**：出界、超时、超距由 `Tick` 返回 `Expired` 后 `Destroy`；地图移除时引擎自己 despawn/destroy 这些 Thing，
  我们的 `Destroy`/`DeSpawn` 覆写先释放视觉再走原版链路；存活上限 96 枚在发射时按 `listerThings` 计数检查。

### 命中上下文（worker 能读到的全部）

`Map`、`Caster`、`Hit`（实际命中的 Thing，可空=地面，可为建筑/墙）、`OriginalTarget`、`OriginalTargetCell`、
`ImpactCell`、`ImpactX/ImpactZ`（实际命中点）、`HeadingX/HeadingZ`、`Settings`、`DamageDef`。

三个 worker 均为真实样例：`Damage` 用 `DamageInfo` 打一次；`Heal` 只对 `Hediff_Injury` 逐条 `Heal`；
`Explode` 调用 `GenExplosion.DoExplosion`（`doVisualEffects:false`，视觉由视觉层负责）。

`Heal` 的 1.7 事实：`Hediff_Injury.Heal` 的 IL 只做 `Severity -= amount`、通知 comps、再调
`Pawn_HealthTracker.Notify_HediffChanged`（`DirtyCache` + `CheckForStateChange`），**不会**在调用内移除 hediff；
`Hediff.ShouldRemove` 由 hediff 自己的 tick 检查。也就是说原正向遍历在当前版本不会跳伤，但为了不被“同步移除”
（未来或其它 hediff 子类）影响，循环改为倒序；多伤口样例验证一个 10 点治疗在 3 处伤（8/4/4）上恰好治疗 10 点总量。

## 5. 视觉层与生命周期

`ArcaneMissileVisualSource` 是一枚飞弹一个实例：`Spawn` → 每游戏 tick `Update` → 命中时 `Impact` → 结束时 `Dispose`。
视觉只读帧数据（位置、朝向、age、wobblePhase、settings），从不改逻辑或存档。

**RimUnity 后端**（`WotF_Visual_RimUnity`）
- 对象结构：`RimUnity.GameObject("WotF_ArcaneMissileFx")` = `ParticleSystem` + `ParticleSystemRenderer`
  （Billboard、sortMode None、additive `RimUnity_StandardParticle4`、64×64 程序化 soft-dot 纹理）
  + `TrailRenderer`（time=trailTime、minVertexDistance 0.08、宽 0.34×size→0.02、alpha 渐隐）。
- 亮点：`startSize` 0.22–0.5×size、`startLifetime` 0.22–0.42s、emission 45/s、Sphere r=0.05、World 模拟、max 64；
  发射器移动而粒子留在世界空间 → 彗尾，再叠加 Trail 光带。
- 命中爆点：独立 GO，一次性 22 粒（径向 0.4–2.2 格/s、带上抛、0.3–0.6s）。它登记在本后端自己的
  「地图 → 爆发对象」表里（含到期时间），由 MapComponent 的逐帧钩子清理；**不再用延迟 Destroy**，
  所以暂停、切图、切游戏都不会留下无主对象。
  跨 `Game.Load` 的核对：RimUnity 的 `HostFrame.Update` 每帧调用 `NativeRuntime.ClearScene(sceneOwner)`，
  对 `SceneOwner` 不匹配的根对象执行 `DestroyImmediate`，而 `DestroyImmediate` 在 finally 里置 `Destroyed=true`；
  `RimUnity.Object.op_Equality` 把 `Destroyed` 视为 null。`PruneBursts` 的 null 判断在 map 过滤之前、且每帧对当前
  有组件的图执行，因此旧 game 的 record 会在第一帧被释放（旧 `Map` 引用一并释放）。残留条件：若新 game 里从未
  出现过本功能的 MapComponent（既没读入在飞飞弹也没再发射），就没有帧钩子来剪枝，旧 record 会留到下次使用；
  这是有界且自愈的，未为此加额外兜底。
- 材质/纹理静态共享缓存，按 `Current.Game` 身份重建；shader 缺失只报一次错并停用该后端。

**原版 Fleck 后端**（`WotF_Visual_Native`）
- 没有任何持久 GameObject：`Spawn/Update` 每 3 个游戏 tick 调一次 `FleckMaker.ThrowLightningGlow`，
  `Impact` 调 `FleckMaker.Static(ExplosionFlash)` + `ThrowLightningGlow`。
- `Dispose` 只置停止标志；fleck 由原版挂在地图上并自己过期，不需要我们清理，也不需要额外地图可见性控制。
- 调试差异：RimUnity 侧问题看 shader/材质/GO 是否创建；原生侧只需看 fleck 是否出现（无材质、无 shader、无 GO 泄漏）。
  两者外观不要求一致。

### 失败处理（不重试、不影响命中）

- 每枚飞弹自己持一个 `visualBroken` 布尔（`EnsureVisual`，`Projectile_ArcaneMissiles.cs:238`）：`Spawn` 抛异常或
  `visualDef` 无效 → 记录一次错误并永久放弃该弹的视觉，后续 tick 不再创建；`Update` 抛异常 → 记录、销毁该视觉、
  同样不再重建。
- `Dispose` 抛异常只记录错误，`Destroy` 照常走完，飞弹不会被视觉拖住。
- 失败状态跟着飞弹对象走：切图/回图的挂起与重建只动 `visual` 引用，**不清** `visualBroken`，所以隐藏再回来不会
  重试同一个已失败的视觉；飞弹被销毁时标记随对象消失，不需要额外的集合清理。
- 命中/过期/出界与视觉完全无关；上述失败都不会改变命中次数或伤害。

### 调度事实与暂停 / 倍率 / 切图 / 读档

本机 1.7 源码（Cecil/IL）证据：

- `TickManager.DoSingleTick` 先遍历 `Find.Maps` 对**每一张地图**调 `MapPreTick`，末尾再遍历一次调 `MapPostTick`；
  因此 **所有地图都在 tick**，不是只有当前地图。
- `Root_Play.Update` 每帧无条件调用 `Current.Game.UpdatePlay()`（暂停也不例外）；`Game.UpdatePlay` 遍历
  **所有地图**调 `Map.MapUpdate()`，后者调 `MapComponentUtility.MapComponentUpdate` → `MapComponentUpdate()`。
  所以每帧、每张地图、暂停中也会收到 `MapComponentUpdate`。

基于这三点，`MapComponent` 的实现是：

- **可见性**：`MapComponentUpdate` 比较 `Find.CurrentMap == map`。不是当前图 → `SuspendVisuals()` 并通知视觉基类
  `NotifyMapHidden(map)`（RimUnity 立即销毁该图的命中爆点/probe），逻辑 tick 继续跑；回到该图 → 置重建标志，
  同一帧重建视觉（不依赖 tick）。tick 在挂起期间不创建/更新任何视觉。
- **视觉 tick 只有一个驱动**：每枚飞弹在自己的 `TickInterval` 末尾 `SyncPosition()` 后调一次 `TickVisual()`；
  组件不再遍历飞弹 tick 视觉，所以同一 tick 不会更新两次（`check_refactor.py` 对此有断言）。
- **暂停**：tick 停止（飞弹位置冻结），但帧钩子仍在跑，所以暂停时读档/回图也能立刻重建视觉；
  RimUnity 粒子是否随暂停冻结取决于其内部时钟（0.1.17 只说明播放独立于**游戏速度**），我们不假设同步；
  爆发对象用宿主墙钟到期 + 逐帧清理，不依赖 RimUnity 的延迟队列。
- **倍率**：tick 随倍率加快，RimUnity 粒子按自己的时钟推进；高倍率下拖尾相对距离更短更密。
  原生 fleck 按游戏 tick 生成，密度与路径长度成比例。这是表现差异，不是逻辑差异。
- **读档**：视觉从不入存档。读入的 settings 直接携带当时选择的 Def，`EnsureVisual` 不会用默认值顶替；
  Def 缺失时该弹报错且无视觉（命中照常）。`FinalizeInit` 只置标志，真正的重建在下一帧的
  `MapComponentUpdate` 完成——暂停中加载也能立刻看到，不必等用户走一个 tick。
- **切游戏**：RimUnity 清自己的对象，材质缓存按 `Current.Game` 重建；我们只持有逻辑状态。
- 预制体（Prefab）不是必需路径：程序化创建已在实机跑过命中验收（§8）；外观未单独评估。
  若以后改用导出的 prefab，只需在 RimUnity 后端的 `Spawn` 里换实现，Def/核心无需改动。

## 6. 已执行的验证

| 检查 | 命令/方法 | 结果 |
| --- | --- | --- |
| 独立编译 | `Moddev/tools/dotnet/dotnet.exe build Moddev/tools/check-missiles/WotF_MissilesCheck.csproj -t:Rebuild` | 0 警告 0 错误（net10.0，未引用最终工程） |
| 真实工程隔离编译 | `dotnet build Source/WhisperOfTheForest/WhisperOfTheForest.csproj -c Release -p:OutputPath=Moddev/tools/check-missiles/out-a20/` | 0 错误；产物只写 `out-a20/`（122,880 B，与后来集成任务的最终 DLL 同 hash），迁移阶段未替换 `1.7/Assemblies` 的 a19 DLL |
| 运动/命中/生命周期（真实代码） | `Moddev/tools/check-missiles/motion-harness`：直接编译生产 `ArcaneMissile.cs`、`ArcaneMissileSettings.cs`、`ArcaneMissileDefs.cs`、三个 worker、视觉基类、`Projectile_ArcaneMissiles.cs`、`MapComponent_ArcaneMissiles.cs`、`ArcaneMissileLauncher.cs`、`ArcaneMissileLabFrame.cs`，仅对 Verse 引擎面加最小 shim | **52 项断言 ALL PASS**（46 + 6 项原生归属：`native/*`、`component/no-missile-collection`），见下 |
| Def 接线 | `check_refactor.py`：XML 5 个 Def 名/类型/字段、类是否在编译产物、settings 选择、Scribe 字段、核心不引用具体后端；新增原生归属断言（`Projectile` 子类覆写、`ThingMaker`+`GenSpawn`、组件无第二套列表/不推进逻辑/不重复 tick 视觉） | `problems: 0` |
| XML 字段 | `check_defs.py`（XML 元素 ↔ Cecil 字段集） | `problems: 0` |
| 本地化 | `check_lang.py`（10 个键、目录决定类型、无类型前缀） | `problems: 0` |
| 调度事实 | Mono.Cecil 读 `TickManager.DoSingleTick` / `Game.UpdatePlay` / `Root_Play.Update` / `Map.MapUpdate` / `MapComponentUtility.MapComponentUpdate` | 所有地图都 tick；每帧对所有地图调 `MapComponentUpdate`，暂停不拦截 |
| API 签名 | Mono.Cecil 读 `Assembly-CSharp.dll` / `RimUnity*.dll` | `Psycast→Ability.Activate→ApplyEffects→CompAbilityEffect.Apply`、cell 目标 `yield return target`、`GenExplosion` 可选参数、`FleckMaker.Static/ThrowLightningGlow`、`Hediff_Injury.Heal`/`Hediff.ShouldRemove` 均已核对 |

### harness 输出（生产代码，非镜像）

- 默认参数：91 tick 命中坐标目标（≈20 格 / 13 格/秒）。
- 同 seed 两次运行 91 tick 逐 tick 位置完全相同。
- 60 格/秒：tick 边界正好落在目标两侧 0.5 格时，tick 开始与走完整 1.0 格后的距离都是 0.500 > 到达半径 0.45，
  仍在第 21 tick 子步内命中（旧规则必然错过）。
- 60 格/秒撞 10 格处 full-fill 墙：墙受伤 7、停在 `x=10.000`，不穿墙。
- 每 tick 最大转角 `0.034907 rad` = 120°/s÷60；每 tick 位移误差 `1e-6` 格。
- 事件选择：Damage 恰好 7 伤害一次；Heal 在 3 处伤（8/4/4）上总计恰好治疗 10；Explode 调用 `GenExplosion` 一次。
- 失败隔离：worker 抛异常时飞弹仍以 `Impact` 结束、日志 +1、worker 只被调用一次。
- 上下文：目标中途死亡后仍结算，`OriginalTarget`/`OriginalTargetCell` 保留，`Hit == null`（地面命中）。
- 生命周期（真实 `MapComponent` + 真实 `Projectile_ArcaneMissiles` + 可控失败后端）：
  - `Spawn` 抛异常：只尝试 1 次、后续 0 次 `Update`，飞弹仍正常命中并造成 7 伤害；
  - `Update` 抛异常：只更新 1 次即丢弃视觉，`Spawn` 不会重建（仍为 1 次），命中照常；
  - `Dispose` 抛异常：只记录错误，飞弹仍被 `Destroy`（`live=0`）；
  - 失败记录：飞弹结束后存活对象里的失败标记为 0（对象本身已销毁）；切图隐藏并回图后仍为 1 且 `Spawn` 不重试
    （返回时 `spawns` 不变），结束后再次归 0；
  - 切图：离开时 `Dispose` 1 次、挂起期间不新建/不更新，但飞弹照常前进（x 0.50→0.93）；
    回到该图只调 `MapComponentUpdate`（无 tick）就重建视觉（`spawns=2, updates=0`），即暂停中读档也能立即显示。
- 过期：超时/超距/出界都返回 `Expired`；极端参数 900 tick 内无 NaN/Infinity。
- 新 flag（真实代码）：
  - 旧设置仍受 34 格/10 秒限制（`reason=range`、`ticks=61`）；
  - `ignoreFlightLimits=true` 时 35 秒、超过 34 格仍在飞，并最终命中；
  - 同样配置下真实出界仍 `Expired` 且 worker 调用 +0；
  - `expireOnTargetLost=true`：`Dead=true` 仍 `Spawned`、`Spawned=false`、换图、读档 `targetThing=null` 四种情况都立即
    `Expired`（`reason=targetLost`）且 worker +0；
  - B 配置（4 格/秒、旧 lifetime 1 秒/旧 range 10）追踪 40 格外的目标 593 tick 后命中并治疗 10（27→… 单次）；
  - `Fire` 在 flag 下拒绝坐标目标与死亡目标（返回 0 + 错误日志），接受存活目标（1 枚）；
  - `GetLabFrames` 只返回 live（1 条）、`TargetThingId` 与目标一致、`Settings` 是实例副本（改副本不影响飞弹）、
    结束后帧列表为空。
- 原生归属（真实 `Projectile_ArcaneMissiles` + shim 的 `Projectile`，6 项）：
  - `native/spawn-registers-in-map`：`GenSpawn` 后从地图注册表（`listerThings`）读到 `live=1`，权威来源是引擎注册表；
  - `native/tick-syncs-thing-position`：`TickInterval` 后 Thing 的 cell 与模型点一致（`10.72/10.48 → (10,0,10)`）；
  - `native/delta-batch-equals-single-ticks`：15 tick 批量与 15 次单 tick 的位移差 0；
  - `native/overrides-tick-save-destroy`：`TickInterval`/`ExposeData`/`Destroy` 由子类自己声明，不是继承默认实现；
  - `native/destroy-unregisters`：`Destroy` 后引擎注册表里不再有该弹；
  - `component/no-missile-collection`：组件的实例字段里没有任何集合，即没有第二套模拟列表。
- harness 的 52 项断言与实机结果是两套证据：前者覆盖运动/命中/生命周期/新 flag/原生归属（含切图与失败路径的仿真），
  后者只覆盖 §8 上表六组组合与一次读档。

## 7. 集成依赖（集成负责人/Lab）

1. `ExperimentLog.Record(string, Pawn, string)`：分类 `"missiles"`，只在发射/命中/过期写。
2. `LabActions.GrantPsycast(Pawn)` 必须给 **psylink ≥ 1**（`AbilityDef.level=1`）；灵能/熵消耗为 0。
3. Lab 可用的公开字段/方法：`ArcaneMissileSettings.visualDef/impactDef/ignoreFlightLimits/expireOnTargetLost`、
   `ArcaneMissileDefs.VisualByName`、`ArcaneMissileDefs.ImpactForMode`、
   `ArcaneMissileDefs.RimUnityVisualDefName/NativeVisualDefName/Damage|Heal|ExplodeImpactDefName`、
   `ArcaneMissileLauncher.Fire/FireAtCell/FireAtThing/SpawnVisualProbe`、
   `MapComponent_ArcaneMissiles.TotalLaunched/TotalImpacts/TotalExpired/LiveMissileCount/ResetLabCounters`、
   `MapComponent_ArcaneMissiles.GetLabFrames()`（live-only 快照，`Settings` 为实例副本、Def 引用只读）。
4. 两个新能力 Def：`WotF_ArcaneMissiles_Heavy`（A）与 `WotF_ArcaneMissiles_Mender`（B），与原 `WotF_ArcaneMissiles` 并列。
5. `About.xml` 已声明 RimUnity 与 Royalty；RimUnity 未加载时本 DLL 无法加载。
6. 本地化：`1.7/Languages/{ChineseSimplified,English}/DefInjected/{AbilityDef,DamageDef}/`；节点键是
   `<defName>.<字段>`，不能带 `AbilityDef.` 之类类型前缀。新的两个 AbilityDef 已加中英 label/description。
7. 5 个视觉/事件 Def 是配置型 Def，label 只用于开发者查看，未加 DefInjected。

## 8. 实机验收（非视觉）

六组组合均在本机 1.7 实际运行过；下表是墙体 HP / 伤势的前后记录。完整原始结果见
`Experiments/acceptance.md` 与 `Experiments/integration.md` §9。

| 组合 | 结果 | 参考 id |
| --- | --- | --- |
| RimUnity + Damage | 墙体 195→188 | c132820616418 → c132821702619 |
| Native + Damage | 墙体 188→181 | c132833566946 → c132834556955 |
| RimUnity + Explode（格 153,135） | 墙体 181→177 | c132841864453 → c132842787576 |
| Native + Explode | 墙体 177→173 | c132843351296 → c132844289500 |
| Heal（两后端） | Torso 3→0，均 `live=0` / `impact=1` | c132900622919（RimUnity）/ c132912292910（Native） |

- 飞行中存档：保存的 settings 为 visual=RimUnity、impact=Damage，目标 `Thing_Wall37181`，速度 2，并记录了位置；
  读档后 `live=1`（c133216759828），继续推进游戏 tick 后 `live=0` / `impact=1`（c133225876465）。
- 5 个视觉/事件 Def 以完整命名空间在实机加载成功（此前短标签导致的 `Type … is not a Def type` 已消除）。
- 上表六组使用的是原 `WotF_ArcaneMissiles`。a19 增量已验证 Heavy 的正常时间横移规避与 outOfBounds、真实 Ability 单次爆炸，以及 Mender 追踪 12 秒、飞行读档后单次治疗；两 flag 在存档中保留。详见 `acceptance.md` A20–A25 和 `integration.md` §10。
- 这些是命中与状态证据，不是外观评价；粒子、拖尾、颜色与暂停/倍率观感由用户自行判断。

**仍未验证**（不要据此扩大结论）：

- 真实多地图：`MapRemoved`、切图隐藏/回图重建、目标中途丢失只有代码与 harness 证据，未做真实多地图操作。
- 暂停/倍率下的实际观感（用户负责）。
- Unity Editor / Prefab exporter 未运行；只验证了程序化创建路径。
- 包仍**硬依赖 RimUnity**（DLL 引用其程序集）；`WotF_Visual_Native` 只是渲染时不经过 RimUnity，
  不能据此声称可以去掉 RimUnity 前置。
- 原生后端复用 `FleckMaker` 标准效果，不需要自建 shader/material；这不等于原版渲染管线没有材质。
- 原生迁移后的 a20 已由集成任务实际发射三种技能，原生 Thing 注册、单次结算和加载证据见 `integration.md` §11、`acceptance.md` A26–A29。
  旧 a19 存档里飞行中的飞弹属于旧对象管理，本轮不提供转换器（按约定），读入后不会被恢复成原生射弹。
- 护盾/拦截：`TickInterval` 不调用 `base`，原版 `CheckForFreeInterceptBetween`、`ImpactSomething`、`ProjectileHitFlags`
  这条链没有接入，因此本模组飞弹不会被护盾腰包/拦截器挡住，也不做友军误伤修正；这是已知差异，不是已兼容。
- harness 的 52 项断言覆盖运动、命中、生命周期、新 flag 和原生归属；实机另有上表六组、a19 两预设，以及 a20 三种真实 cast 和原生存档证据。
  目标失效本轮仍未单独实机；Mender 的 cast 入口已在 a20 运行。
- 转弯半径、不同反应延迟与闭合时间来自计算及 harness；实机规避采用目标提前持续横移，不证明精确反应时限。实机治疗伤势差含自然愈合，不将总差值全归为治疗弹。

## 9. native migration（原生 Projectile 承载）

`Projectile_ArcaneMissiles : Verse.Projectile` 是每枚飞弹的载体，对应 ThingDef `WotF_ArcaneMissileProjectile`
（`1.7/Defs/Experiments/Missiles.xml:26`：`thingClass` 指向该类、`drawerType=None`，画由视觉后端负责）。
这次迁移只改“谁拥有射弹”，不改追踪、碰撞、参数和命中策略。

| 事项 | 现在由谁负责 | 证据 |
| --- | --- | --- |
| 生成/注册 | 引擎：`Fire` 用 `ThingMaker.MakeThing` + `GenSpawn.Spawn` | `ArcaneMissileLauncher.cs:123-125` |
| 更新 | 引擎 tick 链调 `TickInterval(delta)`；屏内 1 tick、屏外 15 tick 批量 | `Projectile_ArcaneMissiles.cs:66`；harness `native/delta-batch-equals-single-ticks` |
| 位置 | 模型点是权威：`ExactPosition` 取 `state.posX/posZ`，并把 Thing 的 cell 同步过去 | `Projectile_ArcaneMissiles.cs:58`、`:124` |
| 存档 | `ExposeData`：`base` 原生字段 + `Scribe_Deep` 的 `ArcaneMissile`（settings 快照、两 flag、目标、位置） | `Projectile_ArcaneMissiles.cs:145` |
| 销毁 | 命中/过期后 `Settle` 调 `Destroy`；`Destroy`/`DeSpawn` 覆写先释放视觉 | `Projectile_ArcaneMissiles.cs:100`、`:133` |
| 第二套列表 | 无：组件从 `listerThings.ThingsOfDef` 读，不保存、不推进 | `MapComponent_ArcaneMissiles.cs:168`；harness `component/no-missile-collection` |
| Lab 计数/快照 | 组件计数；快照由飞弹的 `ToLabFrame()` 生成 | `MapComponent_ArcaneMissiles.cs:50` |
| 地图视觉 | 组件做当前图判定、挂起/回图重建、逐帧通知视觉基类 | `MapComponent_ArcaneMissiles.cs:118` |

保留不动：`Fire/FireAtCell/FireAtThing`、`SpawnVisualProbe`、`GetLabFrames`、`ResetLabCounters`、
`TotalLaunched/TotalImpacts/TotalExpired/LiveMissileCount` 的签名（Lab 调用点无需改），以及三预设、两 flag、
Def 选择的视觉后端和命中 worker。

刻意不做的部分：

- 不覆写 `Impact(Thing, bool)` / `ImpactSomething()`，也不在 `TickInterval` 里调 `base`：原版直线飞行、寿命倒计时和按概率命中
  因此都不会运行，结算只走 `ArcaneMissile.Impact` → worker 一次。
- `Initialize` 把 `origin`/`destination`、`ticksToImpact=0`、`lifetime=0` 填成与模型一致，只为让读这些原生字段的外部代码
  不看到陈旧值；这些字段不参与本模组的飞行。
- 视觉每 tick 只由飞弹自己更新（§5），组件不再遍历；`check_refactor.py` 有对应断言。

验收进度以 `acceptance.md` 的 a20 节为准。护盾/拦截兼容未验，原版沿途拦截链未接入；旧 a19 飞行中存档按约定不提供转换器。
