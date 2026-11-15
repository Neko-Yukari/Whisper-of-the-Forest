# 法术效果具名载荷与多态存档契约

路径约定：
- `S/` = `Source/WhisperOfTheForest/Spells/`
- `E/` = `Source/WhisperOfTheForest/Equipment/`
- `N/` = `../Graphify/native/source/`

## 1. 目标与已定决策

1. C# 运行侧每种效果一个 `sealed` 类型，只声明该效果真正读写的字段，替代 17 种效果共用 22 个可变字段的 `SpellPlanStep`（`S/SpellPlan.cs:96-235`）。
2. 执行走 `SpellEffect` 虚方法，移除 `switch (step.effect)` 分派（`S/SpellExecutor.cs:83-125`）。
3. XML/Def 侧每种效果一个 `SpellGlyphDef` 子类，替代单类挂载全部效果字段的 `SpellGlyphDef`（`S/SpellGlyphs.cs:21,62-120`）。
4. 增幅折叠由各具名效果 Def 自己解释；`SpellCompiler` 只管语法、增幅归属与余链切分，不再按效果种类解释数值（`S/SpellCompiler.cs:414-507`）。
5. 未知效果严禁默认回退为 `Harm`；移除 `EffectOf` 的 `default: return SpellEffectKind.Harm`（`S/SpellCompiler.cs:564-602`），基类 `SpellGlyphDef` 若声明 `kind == SpellGlyphKind.Effect` 则在 `ConfigErrors` 直接报错。
6. 17 种效果、编译器、执行器、4 处持久化载荷与 XML 在同一批一次性改完，不按效果拆小批。
7. 穿刺一次性化：`Harm` 只做一次性伤害，删掉 `Extend`→持续伤口这条路径（`dot` 字段、`ApplySustainedHarm`、`HediffArcaneDot`、Harm 的 `durationTicks`/`pulseIntervalTicks` 与 `Extend`/`Shorten` 兼容项）。DOT 基础设施 `Hediff_SpellDot` + `WotF_Hediff_Scorch` 保留，产出点后续由烈焰龙卷提供。

## 2. 多态存档结论

### 2.1 `LookMode.Deep` 与 `Class` 属性机制

| 阶段 | 原版行为 | 源码位置 |
| --- | --- | --- |
| 列表写盘 | `Scribe_Collections.Look(ref list, label, LookMode.Deep)` 对每个元素调用 `Scribe_Deep.Look(ref target, saveDestroyedThings, "li", ctorArgs)` | `N/Verse/Scribe_Collections.cs:175-179` |
| 写具体类型 | `target.GetType() != typeof(T)` 时，在 `<li ...>` 上写 `Class` 属性，值为 `GenTypes.GetTypeNameWithoutIgnoredNamespaces(target.GetType())`，随后调用 `exposable.ExposeData()` | `N/Verse/Scribe_Deep.cs:47-55` |
| 类名格式 | 模组命名空间不在 `IgnoredNamespaceNames` 内，落盘为完整类型名 `WhisperOfTheForest.Spells.<TypeName>` | `N/Verse/GenTypes.cs:53-57,531-541` |
| 列表读盘 | 逐个子节点调用 `ScribeExtractor.SaveableFromNode<T>(childNode2, ctorArgs)` 并 `list.Add(item)` | `N/Verse/Scribe_Collections.cs:239-246` |
| 还原实例 | 读取节点 `Class` 属性，经 `BackCompatibility.GetBackCompatibleType` 解析具体 `Type`，校验 `!type.IsAbstract`，由 `Activator.CreateInstance`（支持公有/非公有实例构造函数）创建实例后调用 `ExposeData()` | `N/Verse/ScribeExtractor.cs:78-82,105-144` |

每个 `SpellEffect` 子类保留供 Scribe 反射调用的无参构造函数（可为 `private` 或 `public`），业务代码通过完整参数构造函数创建实例。

### 2.2 原版存档多态先例

1. `Quest.parts`：字段类型为 `List<QuestPart>`（`N/RimWorld/Quest.cs:15`），基类 `public abstract class QuestPart : IExposable`（`N/RimWorld/QuestPart.cs:9`），通过 `Scribe_Collections.Look(ref parts, "parts", LookMode.Deep)` 存读（`N/RimWorld/Quest.cs:525`），具体子类如 `QuestPart_AddHediff`（`N/RimWorld/QuestPart_AddHediff.cs:8`）。
2. `Game.components`：字段类型为 `List<GameComponent>`（`N/Verse/Game.cs:21`），基类 `public abstract class GameComponent : IExposable`（`N/Verse/GameComponent.cs:5`），通过 `Scribe_Collections.Look(ref components, "components", LookMode.Deep, this)` 存读（`N/Verse/Game.cs:443`），具体子类如 `GameComponent_Bossgroup`（`N/RimWorld/GameComponent_Bossgroup.cs:6`）。
3. `HediffSet.hediffs`：字段类型为 `List<Hediff>`（`N/Verse/HediffSet.cs:13`），通过 `Scribe_Collections.Look(ref hediffs, "hediffs", LookMode.Deep)` 存读（`N/Verse/HediffSet.cs:248-250`），具体子类如 `Hediff_Injury : HediffWithComps`（`N/Verse/Hediff_Injury.cs:7`）。

### 2.3 子类被删除或改名时的行为与读档检查

1. `GetBackCompatibleType` 找不到类型返回 `null`（`N/Verse/ScribeExtractor.cs:107-108`）。
2. `GetBestFallbackType<SpellEffect>` 不命中 `Thing`/`Hediff`/`Ability`/`Thought` 分支，回退为抽象基类 `typeof(SpellEffect)` 并打一条 `Log.Error("Could not find class ...")`（`N/Verse/ScribeExtractor.cs:110-113,155-189`）。
3. `if (type.IsAbstract)` 抛出 `ArgumentException("Can't load abstract class ...")`（`N/Verse/ScribeExtractor.cs:114-117`）。
4. `SaveableFromNode` 捕获异常后打一条 `Log.Error("SaveableFromNode exception: ...")` 并返回 `null`（`N/Verse/ScribeExtractor.cs:146-152`）。
5. `Scribe_Collections` 把 `null` 原样 `list.Add(item7)` 塞进列表（`N/Verse/Scribe_Collections.cs:244-245`）。

存档读取边界规定：
- 不写 17 类型白名单。
- 所有持有 `List<SpellEffect>` 的 `ExposeData()` 在 `Scribe.mode == LoadSaveMode.LoadingVars` 时必须检查并移除 `null` 元素，一旦发现 `null` 立即调用 `SpellDiagnostics.Error` 报错（对标原版 `N/Verse/HediffSet.cs:251-254`、`N/RimWorld/Quest.cs:531-534`）。
- 删除 `SpellExecutor.ExecuteStepList` 中 `if (step == null) continue;` 的静默跳过分支（`S/SpellExecutor.cs:49-52`）。

## 3. 17 种效果的字段表

表中 `Q` 代表经 `CollectTargets` 读取的公共范围选取对 `aoe + radius`（`S/SpellExecutor.cs:451-491`），具名化后统一为 `useArea + targetRadius`。

| 效果／运行类型 | 当前真正读取的 step 字段 | 冻结后的具名字段与行为约束 | 依据 |
| --- | --- | --- | --- |
| Harm／`HarmEffect` | `Q, amount, penetration` | `damage, armorPenetration, useArea, targetRadius`；**一次性伤害，不再有持续伤口**：`Extend`/`Shorten` 不可接（用户已定：穿刺接延时变成持续伤害是错误设计） | `S/SpellExecutor.cs:127-146` |
| Heal／`HealEffect` | `Q, factor, magnitudeFactor, durationTicks` | `tendQuality, clottingDurationTicks, useArea, targetRadius`；编译期计算 `tendQuality = Mathf.Clamp01(factor * magnitudeFactor)`，不读 `amount` | `S/SpellExecutor.cs:178-209` |
| Explosion／`ExplosionEffect` | `amount, radius, penetration, sensitive` | `blastDamage, blastRadius, armorPenetration, ignoreBuildings`；不读 `aoe`，始终触发单次原版爆炸 | `S/SpellExecutor.cs:211-242` |
| Grow／`GrowEffect` | `aoe, radius, factor, magnitudeFactor, durationTicks` | `useArea, soilRadius, growthFactor, fertilityDurationTicks`；编译期计算 `growthFactor = factor > 0f ? factor * Mathf.Max(1f, magnitudeFactor) : 0f` | `S/SpellExecutor.cs:262-290` |
| Lightning／`LightningEffect` | `Q, amount, penetration, empAmount, perStackFactor, durationTicks` | `burnDamage, burnArmorPenetration, empDamage, bonusPerChargeStack, chargeDurationTicks, useArea, targetRadius`；`empDamage` 不随 `Amplify`/`Dampen` 或法杖伤害倍率缩放 | `S/SpellExecutor.cs:300-335`、`E/SpellStaffModifiers.cs:99-102` |
| Freeze／`FreezeEffect` | `Q, durationTicks` | `freezeDurationTicks, useArea, targetRadius` | `S/SpellExecutor.cs:98-99,338-351` |
| ColdSnap／`ColdSnapEffect` | `Q, amount, penetration, vulnerableFactor` | `damage, armorPenetration, frozenTargetMultiplier, useArea, targetRadius`；`vulnerableFactor` 只属 `ColdSnap` | `S/SpellExecutor.cs:354-368` |
| Knockback／`KnockbackEffect` | `Q, amount` | `pushDistance, useArea, targetRadius` | `S/SpellExecutor.cs:108-109,370-382` |
| Pull／`PullEffect` | `Q, amount` | `pullDistance, useArea, targetRadius` | `S/SpellExecutor.cs:110-111,370-382` |
| Snare／`SnareEffect` | `Q, durationTicks` | `rootDurationTicks, useArea, targetRadius` | `S/SpellExecutor.cs:100-101,338-351` |
| Hex／`HexEffect` | `Q, durationTicks` | `hexDurationTicks, useArea, targetRadius` | `S/SpellExecutor.cs:104-105,338-351` |
| Magelight／`MagelightEffect` | `glowRadius, magnitudeFactor, glowColor, durationTicks` | `lightRadius, lightColor, lifetimeTicks`；编译期计算 `lightRadius = glowRadius * magnitudeFactor` | `S/Effects/SpellEffectRuntime.cs:69-104` |
| Blink／`BlinkEffect` | `amount` | `selfForwardDistance`；`primary == caster` 时向前跳 `Mathf.RoundToInt(selfForwardDistance)` 格，否则跳到目标 `cell` | `S/SpellExecutor.cs:388-406` |
| Linger／`LingerEffect` | `payload, radius, pulseIntervalTicks, durationTicks` | `payload, fieldRadius, pulseIntervalTicks, lifetimeTicks` | `S/SpellExecutor.cs:408-426`、`S/Effects/Thing_SpellField.cs:35-44,79-103` |
| Rune／`RuneEffect` | `payload, durationTicks` | `payload, lifetimeTicks`；触发只看自身格子的第一个合格 `Pawn`，不存半径与节拍 | `S/SpellExecutor.cs:408-426`、`S/Effects/Thing_SpellField.cs:35-44,61-73,105-120` |
| Delay／`DelayEffect` | `payload, durationTicks` | `payload, delayTicks` | `S/SpellExecutor.cs:428-443`、`S/Effects/MapComponent_SpellDelays.cs:70-84` |
| Hack／`HackEffect` | `aoe, radius, amount` | `useArea, targetRadius, hackStrength, baseTicks, minTicks, maxTicks, controlTicks` | `S/Effects/MechHack.cs:14-32,56-101` |

## 4. 编译期消化字段与增幅顺序约束

### 4.1 严禁进入运行载荷的旧字段

| 旧字段 | 去向 | 依据 |
| --- | --- | --- |
| `amplifyStacks, dampenStacks, aoeStacks` | 从运行载荷移除；仅在编译诊断元数据 `compile.stacks` 输出 | `S/SpellCompiler.cs:420,432,441` |
| `magnitudeFactor` | 编译期折入 `Heal.tendQuality`、`Grow.growthFactor`、`Magelight.lightRadius` | `S/SpellCompiler.cs:422,434`、`S/SpellExecutor.cs:180,269`、`S/Effects/SpellEffectRuntime.cs:87` |
| `baseDurationTicks` | 仅用于编译期响应 `Extend` 时提供基础时长 | `S/SpellCompiler.cs:394,494-507` |
| `factor` | 各具名 Def 直接声明具名字段（如 `tendQuality`、`growthFactor`） | `1.7/Defs/Spells/Spellbook.xml:196,252` |
| `dot` | 删除。穿刺不再生成持续伤口；DOT 基础设施（`Hediff_SpellDot` + `WotF_Hediff_Scorch`）保留，由后续效果（烈焰龙卷）单独提供产出点 | `S/SpellCompiler.cs:451`、`S/SpellExecutor.cs:133-169`、`S/SpellPlan.cs:133` |
| `effect` | 由 `SpellEffect` 具体子类表示 | `S/SpellPlan.cs:101,199` |
| `requiresAmount` | 删除；各具名 Def 子类在各自的 `ConfigErrors` 校验自身必填字段 | `S/SpellGlyphs.cs:390-400`、`S/SpellCompiler.cs:663-666` |

### 4.2 增幅顺序与取整不可合并为总乘积

各具名效果 Def 在编译时必须按增幅符文的实际顺序逐枚折叠，不得先汇总总倍率再一次性相乘：

1. `Extend` 与 `Shorten` 每遇到一枚都立即执行 `Mathf.Max(1, Mathf.RoundToInt(durationTicks * factor))`（`S/SpellCompiler.cs:445-459,494-507`）；逐步取整与先乘后取整结果不同。
2. `Harm` 不再拥有时长：`Extend`/`Shorten` 不可接，其兼容项已删。其余有时长的效果，`Extend`/`Shorten` 逐枚改 `durationTicks`（`S/SpellCompiler.cs:494-507`），取整沿用现有 `System.Math.Max(1, (int)System.Math.Round(ticks * factor))`（`S/SpellCompiler.cs:507`），不要换成 `Mathf.RoundToInt`。
3. `Amplify` 对 `Explosion` 额外累加 `augment.radius`，而 `Dampen` 只缩放伤害，不扣减该半径增量（`S/SpellCompiler.cs:424-436`）。

## 5. 两个容易误改的现有事实

### 5.1 `Lightning` 无 `Extend` 时 step 时长必须保持 `0`

- `1.7/Defs/Spells/Spellbook.xml:290` 在 `WotF_Effect_Lightning` 上写了 `<durationTicks>600</durationTicks>`。
- 但 `SpellCompiler.BuildStep` 只在 `HasOwnDuration(kind)` 为 `true` 时将 `glyph.durationTicks` 写入 `step.durationTicks`，而 `HasOwnDuration` 不含 `Lightning`（`S/SpellCompiler.cs:395,405-407,519-535`）。
- 因此未带 `Extend` 时 `Lightning` 传入 `AddTimedHediff` 的 `durationTicks` 为 `0`，`SpellEffectRuntime.MakeTimedHediff` 跳过 `disappears.SetDuration`，使用 `HediffDef` 自身的默认消失时长；带 `Extend` 时才从 `baseDurationTicks`（600）起算（`S/Effects/SpellEffectRuntime.cs:33-35`、`S/SpellCompiler.cs:494-507`）。
- `LightningGlyphDef` 必须把该字段命名为 `extendBaseChargeDurationTicks`（或同义具名字段），仅在存在 `Extend` 时产正的 `chargeDurationTicks`，无 `Extend` 时保持 `0`。

### 5.2 `Hack` 必须断开运行期对唯一 Def 的反查

- 现状：`MechHackService.Tuning` 硬编码 `SpellDefs.Glyph("WotF_Effect_Hack").GetModExtension<MechHackExtension>()`，在 `Apply` 开始同化（`:96`）与 `HediffComp_MechAssimilation.CompPostTick` 完成同化（`:171`）时实时反查 Def（`S/Effects/MechHack.cs:14-41,96,169-171`）。
- 改造要求：
  1. 删除 `MechHackExtension`，把 `baseTicks`、`minTicks`、`maxTicks`、`controlTicks` 直接作为 `HackGlyphDef` 的字段并在 `HackGlyphDef.ConfigErrors` 校验（原校验见 `S/Effects/MechHack.cs:25-32`）。
  2. 编译时把这 4 个值与折叠后的 `hackStrength` 一起冻结进 `HackEffect`。
  3. `HackEffect.Execute` 在启动同化时把算出的同化 ticks 与 `controlTicks` 一并传给 `HediffComp_MechAssimilation.Begin(caster, channel.loadID, assimilationTicks, controlTicks)`，由 `HediffComp_MechAssimilation.CompExposeData` 持久化 `controlTicks`（`S/Effects/MechHack.cs:150-189`）。

## 6. 执行接口与冻结边界

### 6.1 `SpellEffect` 基类接口

```csharp
public abstract class SpellEffect : IExposable
{
    public abstract string DiagnosticName { get; }
    public abstract int Execute(Pawn caster, Thing primary, IntVec3 cell, Map map);
    public abstract void ExposeData();
}
```

- 不加第 3 个选敌/射程钩子：法术射程由形式决定（`S/SpellCompiler.cs:245-246`、`E/Verb_CastSpellStaff.cs:27-48`），目标类别白名单在 `plan.targets` 冻结（`S/SpellCompiler.cs:207-211,366-379`、`S/SpellService.cs:318-350`）。
- `DiagnosticName` 返回小写效果名（如 `"harm"`、`"heal"`），供 `SpellExecutor.ExecuteStepList` 拼报告（`S/SpellExecutor.cs:60-62`）。

### 6.2 执行器与目标收集职责

- `SpellExecutor.ExecuteStepList` 签名改为接收 `IReadOnlyList<SpellEffect> steps`，返回同样的空格分隔报告字符串；单步返回值语义保持不变（`Explosion`/`Linger`/`Rune`/`Delay` 返回 `1`，`Grow` 返回格数，其余返回受影响目标数，`S/SpellExecutor.cs:241,277-287,425,442`）。
- 目标收集下放到各效果的 `Execute` 内部；`SpellExecutor` 暴露共享静态方法 `CollectTargets(bool useArea, float targetRadius, Thing primary, IntVec3 cell, Map map, Predicate<Thing> accept)`（原逻辑见 `S/SpellExecutor.cs:451-491`）。每个效果先物化目标 `List<Thing>`，再逐个结算。

### 6.3 冻结发生在法杖修正之后

- 现状缺陷：`SpellService.TryCast` 先调 `SpellCompiler.Compile(castRecipe, out reason)`，再调 `staff.ApplyEffects(plan)` 原地修改 `step.amount`、`step.durationTicks`、`step.baseDurationTicks`（`S/SpellService.cs:226-237`、`E/SpellStaffModifiers.cs:69-136`）。
- 改造要求：
  1. `SpellCompiler.Compile` 签名改为接收可选的 `SpellStaffModifiers staff`（`SpellService.BeginTargeting` 等无材料场景传 `null`）。
  2. 每个效果 Def 在编译方法内先按顺序折叠增幅符文，再应用 `staff` 倍率，最后构造字段只读公开（私有字段供 Scribe `ref` 读写）的 `sealed SpellEffect`。
  3. 保持法杖倍率原作用范围：`staff.damageFactor` 只乘 `Harm.damage`、`Explosion.blastDamage`、`Lightning.burnDamage`、`ColdSnap.damage`（以及 `plan.railgun.damage`）；`staff.durationFactor` 只乘 `Heal.clottingDurationTicks` 与 `Grow.fertilityDurationTicks`（`E/SpellStaffModifiers.cs:78-81,99-136`）。
  4. `SpellEffect` 实例冻结后不可变；`CopySteps` 只需复制外层 `List<SpellEffect>` 容器，无需在 17 个子类上写 `Copy()` 虚方法（`SpellEffect` 不实现 `ILoadReferenceable`，多处 Deep 保存同一不可变实例不会触发 `RegisterDeepSaved` 冲突，见 `N/Verse/DebugLoadIDsSavingErrorsChecker.cs:89-104`）。

## 7. XML 映射

### 7.1 根 Def 与 `Class` 属性规则

1. `SpellGlyphDef : Def` 保持非抽象（`S/SpellGlyphs.cs:21`）。`GenDefDatabase.AllDefTypesWithDatabases` 只为无非抽象 Def 祖先的类型建库，`DefsToGoInDatabase<T>` 通过 `mod.AllDefs.OfType<T>()` 收集全部子类（`N/Verse/GenDefDatabase.cs:58-87`），保持非抽象根可使 `DefDatabase<SpellGlyphDef>` 与 `DefInjected/WhisperOfTheForest.Spells.SpellGlyphDef` 继续覆盖全部子类。
2. Def 根标签统一保持 `<WhisperOfTheForest.Spells.SpellGlyphDef>`（`N/Verse/DirectXmlLoader.cs:181-187`），通过 `Class="WhisperOfTheForest.Spells.<Name>GlyphDef"` 指定具体 C# 子类（`N/Verse/DirectXmlToObject.cs:166-176,243-256`）。
3. **每个具体效果 Def 必须自己写 `Class="..."`，不得把 `Class` 只写在 `Abstract="True"` 的父模板上。** 原版 `XmlInheritance.RecursiveNodeCopyOverwriteElements` 在合并非 `Inherit="False"` 子节点时，会先执行 `current.Attributes.RemoveAll()` 清空父节点全部 XML 属性，再只拷贝子节点的属性（`N/Verse/XmlInheritance.cs:312-317`）。写在父模板上的 `Class` 属性会被抹掉，导致子 Def 退回实例化基类 `SpellGlyphDef`。

### 7.2 最小 XML 形状示例

```xml
<Defs>
  <WhisperOfTheForest.Spells.SpellGlyphDef Name="WotF_EffectGlyphBase" Abstract="True">
    <kind>Effect</kind>
    <windupTicks>5</windupTicks>
    <recoveryTicks>5</recoveryTicks>
    <entropyCost>0</entropyCost>
  </WhisperOfTheForest.Spells.SpellGlyphDef>

  <WhisperOfTheForest.Spells.SpellGlyphDef
      ParentName="WotF_EffectGlyphBase"
      Class="WhisperOfTheForest.Spells.HarmGlyphDef">
    <defName>WotF_Effect_Harm</defName>
    <label>harm</label>
    <description>Deals arcane force damage to what the spell reaches, through the same damage type the arcane missiles already use.</description>
    <targets>
      <li>Humanlike</li>
      <li>Animal</li>
      <li>Mechanoid</li>
      <li>Entity</li>
      <li>Building</li>
      <li>Rock</li>
      <li>Plant</li>
    </targets>
    <damage>9</damage>
    <armorPenetration>0.5</armorPenetration>
    <compatibleAugments>
      <li>WotF_Augment_Amplify</li>
      <li>WotF_Augment_Dampen</li>
      <li>WotF_Augment_BreakArmor</li>
    </compatibleAugments>
    <iconPath>Spellbook/Glyphs/WotF_Effect_Harm</iconPath>
    <manaCost>10</manaCost>
    <experience>3</experience>
  </WhisperOfTheForest.Spells.SpellGlyphDef>
</Defs>
```

## 8. 删除项清单

| 删除目标 | 原位置 | 替代方式 |
| --- | --- | --- |
| `SpellEffectKind` 枚举 | `S/SpellPlan.cs:38-56` | 17 个 `sealed SpellEffect` 子类与 17 个 `SpellGlyphDef` 子类 |
| `SpellPlanStep` 及 `effect` 存档字段 | `S/SpellPlan.cs:96-235` | `SpellEffect` 抽象基类 + `LookMode.Deep` 的 `Class` 属性 |
| `SpellCastPlan.HasEffect` / `HasEffectIn` | `S/SpellPlan.cs:278-305` | 无外部调用方，直接删除 |
| `SpellCompiler.EffectOf` 及默认返回 `Harm` | `S/SpellCompiler.cs:564-602` | `EffectGlyphDef` 虚编译方法；非子类的 `Effect` Def 在 `ConfigErrors` 报错 |
| `SpellGlyphDef` 上的效果专属字段与 `requiresAmount` | `S/SpellGlyphs.cs:62-120,234-257,384-413`、`S/SpellCompiler.cs:663-666` | 下放到对应 `EffectGlyphDef` 子类字段与子类 `ConfigErrors` |
| `SpellStaffModifiers.ApplySteps` / `IsTimedBuff` / `IsDamage` | `E/SpellStaffModifiers.cs:76,84-136` | 编译期传入 `SpellStaffModifiers`，由对应效果 Def 折叠 |
| `MechHackExtension` 与 `MechHackService.GlyphName` / `Tuning` | `S/Effects/MechHack.cs:14-41` | 字段迁入 `HackGlyphDef` 并冻结进 `HackEffect` 与 `HediffComp_MechAssimilation` |

## 9. 验收与顺序

1. **完成全部代码与 XML 改造**：17 个效果类型与 Def 子类、编译器、执行器、4 处持久化载荷。
2. **静态校验**：运行 `Tools/build.ps1` 与 `Tools/check-defs.ps1`，0 警告 0 错误，`Summary: 0 failed`。
3. **实机**：同一列表中混合多种 `SpellEffect` 子类及嵌套 `payload`（`Linger`/`Rune`/`Delay`/`Orbit`）的存盘、读盘与读盘后触发结算，由用户在游戏里看。

## 10. 风险清单

1. **行为漂移**：
   - `Extend`/`Shorten` 按符文顺序逐枚折叠，取整沿用 `System.Math.Max(1, (int)System.Math.Round(ticks * factor))`（`S/SpellCompiler.cs:507`）；`Amplify` 单向增加 `Explosion` 半径而 `Dampen` 不回退（`S/SpellCompiler.cs:418-436`）。
   - `Lightning` 未带 `Extend` 时 `chargeDurationTicks` 为 `0`，保留 `HediffDef` 默认时长（`S/SpellCompiler.cs:395,405-407,519-535`、`S/Effects/SpellEffectRuntime.cs:33-35`）。
   - `Grow` 倍率公式为 `factor * Mathf.Max(1f, magnitudeFactor)`，低于 `1` 的 `magnitudeFactor` 被夹到 `1`（`S/SpellExecutor.cs:269`）。
   - `Rune` 只对踏入自身格子的第一个合格 `Pawn` 触发一次，不使用 `radius` 与 `pulseIntervalTicks`（`S/Effects/Thing_SpellField.cs:105-120`）。
2. **漏掉持久载荷**：
   - 共有 4 处独立使用 `LookMode.Deep` 保存步骤列表：`SpellPayload.steps`（`S/SpellPlan.cs:243-271`）、`Thing_SpellField.payload`（`S/Effects/Thing_SpellField.cs:29,127`）、`Thing_SpellOrbit.payload`（`S/Effects/Thing_SpellOrbit.cs:22,121`）、`SpellDelayedPayload.steps`（`S/Effects/MapComponent_SpellDelays.cs:12,19`）。漏改任一处都会在在途射弹、滞留/地印、环绕或延迟法术跨档时暴雷。
3. **假冻结**：
   - 若保留 `SpellStaffModifiers.ApplyEffects` 在编译后改写效果实例（`S/SpellService.cs:232-235`），或保留 `MechHackService` 在执行/同化完成时按固定 `defName` 反查 Def（`S/Effects/MechHack.cs:41,96,171`），则运行载荷仍未真正冻结。
