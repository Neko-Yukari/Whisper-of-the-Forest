# 魔术书与组合施法

本轮目标是让玩家在游戏内选择符文、排列法术、命名并保存，再由有灵能施法能力的小人使用。现有原生追踪射弹与独立催熟效果继续作为执行机制。此文件在新 DeLM run 启动前固定共享契约；研究负责人补充来源与映射，集成负责人随后填写实际验证。

## 1. 语法与已核实来源

- [Ars.Guide：Glyphs](https://ars.guide/book/getting_started/introduction_to_glyphs/)：仅一个 Form，且必须位于首位；多个 Effect 按排列顺序执行；Augment 修改左侧最近的 Form 或 Effect，可连续叠加，不修改其他 Augment。
- [Ars.Guide：Spellcrafting](https://ars.guide/book/getting_started/introduction_to_spellcrafting/)：点击目录添加 glyph，点击已编排 glyph 删除，命名后 Create 保存；书签切换，书中最多十个法术。
- [Ars.Guide：Mana](https://ars.guide/book/getting_started/spell_mana/)：原作消耗各 glyph 费用之和并随时间回复。本轮仍为实验零 psyfocus 消耗，通过施放间隔控制连续施法，不宣称已实现 Mana 系统。
- [Ars.Guide：1.21.1 Glyphs](https://ars.guide/1.21.1/docs/spell_theory/glyphs)：该目录包含附属模组，映射时必须核对 ID 的 namespace，不将全部条目当作 Ars Nouveau 本体。

## 2. 首轮产品范围

支持三种形式、四种效果和五种增幅，法术最多十个符文，存档中保存十个法术槽位。允许多个效果组成有序链。编辑草稿和保存内容分离，未保存编辑不得改变已保存法术或已发出的射弹。

| 类别 | 符文 | RimWorld 行为 |
| --- | --- | --- |
| Form | Self / 自身 | 在施法者自身及所在格执行 |
| Form | Touch / 接触 | 只对相邻可接触目标执行，不隔墙、不开远距离瞬发捷径 |
| Form | Projectile / 射弹 | 复用现有原生追踪 Projectile，沿途真实碰撞，在实际命中对象与位置执行 |
| Effect | Harm / 伤害 | 沿用现有伤害类型及真实 TakeDamage |
| Effect | Heal / 治疗 | 治疗既有非永久伤口，不复活、不补回肢体 |
| Effect | Explosion / 爆炸 | 原生爆炸，每次命中只执行一次；范围增幅只扩大这一次爆炸 |
| Effect | Grow / 催熟 | 调用 PlantGrowthEffect，保持植物实例、HP、年龄与收获规则 |
| Augment | Amplify / 强化 | 修改前一伤害、治疗或爆炸的实际强度；对已瞬间成熟的 Grow 不假装有强化效果 |
| Augment | AOE / 范围 | 扩大前一效果的作用范围；每个目标最多执行一次，爆炸不得对圈内每个对象重复生成爆炸 |
| Augment | Accelerate / 加速 | 只修改前一 Projectile 的飞行速度 |
| Augment | Decelerate / 减速 | 只修改前一 Projectile 的飞行速度 |
| Augment | Split / 分裂 | 只修改前一 Projectile 的弹数，使用现有发散与单弹结算机制 |

最少支持以上集合。研究发现其他合理对应机制时可以给出映射与后续建议，不因“可对应”就扩大本轮实现。配方或目标错误必须显示清晰原因，不能静默无效果或用日志 ok 代替实际结果。

## 3. 共享代码契约

命名空间为 `WhisperOfTheForest.Experimental.Spells`，UI 为 `WhisperOfTheForest.Experimental`。运行层任务独占 `Source/WhisperOfTheForest/Spells/`，UI 任务独占 `Source/WhisperOfTheForest/UI/Spellbook/`。双方直接使用以下接口，不各自复制配方或校验逻辑。

- `enum SpellGlyphKind { Form, Effect, Augment }`。
- `SpellGlyphDef : Verse.Def`，至少公开 `SpellGlyphKind kind`、`string iconPath`。目录来自 `DefDatabase<SpellGlyphDef>.AllDefsListForReading`，规则和参数尽量放在本轮自己的 XML；新行为仍需实现，不能将数据驱动夸大为任意行为免编码。
- Def 名称固定：`WotF_Form_Self`、`WotF_Form_Touch`、`WotF_Form_Projectile`；`WotF_Effect_Harm`、`WotF_Effect_Heal`、`WotF_Effect_Explosion`、`WotF_Effect_Grow`；`WotF_Augment_Amplify`、`WotF_Augment_AOE`、`WotF_Augment_Accelerate`、`WotF_Augment_Decelerate`、`WotF_Augment_Split`。
- `SpellRecipe : IExposable`：`public string name`、`public List<string> glyphs`，glyphs 保存 defName 顺序；`SpellRecipe Copy()` 返回独立列表。
- `GameComponent_Spellbook : GameComponent`：`static GameComponent_Spellbook Current`、`const int SlotCount = 10`、`SpellRecipe GetSlot(int slot)`（返回副本）、`bool SaveSlot(int slot, SpellRecipe recipe, out string reason)`、`void ClearSlot(int slot)`、`int GetActiveSlot(Pawn pawn)`、`void SetActiveSlot(Pawn pawn, int slot)`。槽位与小人当前选择可存档，空槽返回 null，槽位从 0 开始。
- `SpellService.Validate(SpellRecipe recipe, out string reason)` 返回 bool；reason 为可直接显示的本地化文本。校验包括语法、最多十符文、兼容增幅、至少一个有效效果。纯编辑操作可以增删形成暂时无效草稿，保存与施放必须校验。
- `SpellService.CanCast(Pawn pawn, SpellRecipe recipe, out string reason)`；`SpellService.TryCast(Pawn pawn, SpellRecipe recipe, LocalTargetInfo target, out string reason)`；`SpellService.BeginTargeting(Pawn pawn, SpellRecipe recipe)`。最后一个是书页与 Gizmo 共用的玩家施法入口，Self 不要求玩家额外点自己。
- 有能力定义为玩家控制的存活、已生成、具 Royalty 灵能链接等级至少 1 的小人；不能施法的原因需要可见。施法服务在真实执行前仍校验，不依赖 UI 单侧限制。是否使用原生 Ability/Verb 或原生 Command_Target，由运行负责人核实真实 API 后选择最小完整实现，必须具备原生目标选择、距离/阻挡/状态限制及冷却，不仅是 Lab 直接改数值。
- UI 提供 `public class Window_Spellbook : Window` 和 `public Window_Spellbook(Pawn caster)`。运行层用小人原生 Gizmo 打开该窗口，用户通过书签为该小人选择当前法术，再使用施法 Gizmo 或书页按钮开始目标选择。
- 射弹载荷沿用 `ArcaneMissileSettings` 的 Copy / ExposeData 路径，仅新增可深复制、可保存的法术配方或已编译效果列表；通过新的 `ArcaneMissileImpactWorker` 适配执行链。旧 impactDef 优先级、Damage/Heal/Explode 行为、原生 Projectile 生命周期不改。配方必须在发射时冻结；编辑、换槽或读档不能改写已飞出的效果。
- 目标类型应根据能实际执行的效果选择：Grow 需要 Plant，Heal 需要 Pawn，Explosion 可选格；范围效果可围绕指定格查找适用对象。多个效果不要求作用于同一种对象，但不能因为链中某效果不适用就对其他对象冒充成功，也不应用全链交集把所有混合配方禁止。

规则数值采用有界合理默认，由运行负责人记录：强化倍率、范围增长、加减速倍率、分裂弹数及上限、射程、冷却。旧三飞弹与催熟技能不改参数。本轮不引入另一套魔力条、法术解锁、跨存档同步或任意脚本执行。

## 4. 视觉与文件所有权

书页为原生 Verse Window，约 1080×700，并适配较小屏幕。左页为可搜索、按类别切换的符文目录；右页是名字、符文序列、选中符文说明、组合校验、保存与施放；页侧十个书签。可点击添加/移除，并提供顺序调整；不强制拖放。保存覆盖已有槽位须可辨识，切换书签不应悄悄丢弃草稿。

视觉采用深绿皮革、暖纸页、黄铜刻线和克制的青绿魔法亮光；文本由游戏原生字体绘制以保留中文清晰度。图片只画底材、边框或符文，不把关键文字烘焙进图片。不得复制原作 UI 素材；原创生图、许可清楚的免费资源或自绘均可。图片为辅助，交互区域需要清楚可读；保留 IMGUI 状态（复用 `TechLabDrawScope`），不泄漏 WordWrap / Anchor / GUI.color，不改全局字体。

| 负责人 | 唯一可写范围 |
| --- | --- |
| 研究 | 本文件的研究映射/来源/边界章节；`Moddev/Graphify/` 的项目图谱重建与原生接口参考（反编译内容不进仓库） |
| 运行层 | `Spells/`；`Missiles/ArcaneMissileSettings.cs`；自己的 `1.7/Defs/Experiments/Spellbook.xml`、中英 `WotF_Spells` Keyed/DefInjected；`Moddev/tools/check-spells/` 与必要的原 harness 编译引用调整 |
| 书页界面 | `UI/Spellbook/`；`Textures/Experiments/Spellbook/`；自己的中英 `WotF_Spellbook` Keyed；可给 `Window_TechLab.cs` 添加打开书页按钮，其余旧 UI 只读 |
| 最终集成（依赖前三项） | 本轮上述文件的必要集成修复；`Lab/`、`Tools/`、最终 DLL、`Moddev/lab-run` 与隔离实例、a22 存档和证据；更新本文件实测章节 |
| 主代理 | `IMPLEMENTATION_PLAN.md`、`MODLOG.md`、`Experiments/acceptance.md`，最终只读审查与验证 |

所有任务遵循全局 AGENTS.md 和 `coding-guidelines`。最终 DLL 与游戏/命令队列只有集成任务的 owner 可写、可操作。前期仅输出到各自隔离检查目录，不能先覆盖部署 DLL。维护版 Missiles.xml 逐字只读，旧四技能及 a18–a21 存档受保护。不提交、推送、发布，不操纵桌面输入。若任务超时/worker 中断，先停并上报，不因租约过期自行接管旧 owner 的外部进程。

## 5. 验收契约

1. 编译真实游戏引用，检查中英文本和资源引用。关键行为经公共API/真实游戏验证，不靠源码字面量计数充当功能测试。
2. 十槽编辑至少证明：添加/删除/排序、非法增幅拒绝保存、有效链保存、编辑副本不污染保存值，读档后配方名称和顺序不变。
3. 隔离 a22 中，具能力角色能打开书页并进入玩家目标选择，无能力角色拒绝；观察原生 Gizmo 与实际书页，清楚区分 UI 回调与鼠标点击证据。
4. 用少量场景覆盖三种 Form、四种 Effect 和五种 Augment 的实际作用，至少一条多效果链、一条范围催熟、一条射弹治疗/伤害；记录实际 target/hp/growth/live/impact 变化。无需穷举所有配方。
5. 保存已在飞行中的新配方法术，读档续飞，效果按发射快照结算一次；保存后重新编辑书页不能改写在途法术。
6. 旧四技能存在且保持参数；维护 XML 哈希一致，日志无本轮 Error/Exception；最终 a22-final 暂停、有能力小人已选中、书中有可试玩样例、live=0，旧存档保留。
7. 不为额外边界堆建通用编辑器、脚本调度或新测试平台。未运行项目单列；多地图、原版护盾/拦截和特殊植物边界继续如实保留。

## 6. 研究映射（研究任务填写）

填写者 worker-1（run `run_f012dba2178b4904b0b8ef7d`）。本节只给来源、映射与边界，不改 §2–§5；本轮施工仍是 3 Form + 4 Effect + 5 Augment（§2/§3），下面的“后续”项都不属于本轮范围。

### 6.1 来源、取样方法与两条核对结论

来源（均为 ars.guide 公开页，2026-10-2x 抓取）：[Glyphs 三分类](https://ars.guide/book/getting_started/introduction_to_glyphs/)（Form 唯一且在首位、Effect 按排列顺序执行、Augment 只改左侧最近 Form/Effect 且可叠加）、[Spellcrafting](https://ars.guide/book/getting_started/introduction_to_spellcrafting/)（点选添加/删除、命名后创建、书签、书中十个法术）、[Mana](https://ars.guide/book/getting_started/spell_mana/)（消耗为各 glyph 费用之和、随时间回复）、[1.21.1 Glyphs 结构数据](https://ars.guide/1.21.1/docs/spell_theory/glyphs/)（每条含 `Type`/`Tier`/`Cost`/`ID: <namespace>:glyph_*`/Java `Classes`）、Tier 索引 [一](https://ars.guide/book/glyphs_tier_one/)/[二](https://ars.guide/book/glyphs_tier_two/)/[三](https://ars.guide/book/glyphs_tier_three/)。

namespace 过滤（§1 第 4 条的要求）：结构页 229 条中 `ars_nouveau:` 87 条（5 Form、13 Augment、69 Effect），其余落在 14 个附属 namespace：`ars_additions, ars_astrologique, ars_controle, ars_elemental, ars_hex, ars_scalaes, ars_technica, ars_trinkets, ars_zero, arsomega, not_enough_glyphs, reliquified_ars_nouveau, starbunclemania, toomanyglyphs`。Tier 索引页把附属条目与本体混排（如 `ars_technica:glyph_apply`、`not_enough_glyphs:glyph_plow`、`ars_controle:glyph_filter_*`），所以“出现在索引或拼写表里”不能当作本体；本节只映射 `ars_nouveau:`。

结论 A（契约核对，不需扩大施工）：§3 固定的 12 个 defName 恰好是本体最小子集——Form `self`(T1,10)/`touch`(T1,5)/`projectile`(T1,10)；Effect `harm`(T1,15)/`heal`(T2,50)/`explosion`(T2,200)/`grow`(T2,70)；Augment `amplify`(T1,20)/`aoe`(T2,35)/`accelerate`(T2,10)/`decelerate`(T2,5)/`split`(T3,20)。本体没有“契约缺失因而必须补”的项。

结论 B（兼容性差异，如实保留）：本体的增幅兼容是逐 glyph 声明的：`harm`/`heal`/`light`/`blink` 只有 Amplify；`grow`/`harvest`/`pickup`/`orbit` 只有 AOE；`explosion`/`break`/`fell`/`cut`/`intangible` 才有 Amplify+AOE。§2 表允许 AOE 修饰“前一效果”、Amplify 修饰治疗，比原作宽松；按契约实现即可，但 UI 校验与验收文案不应宣称“与原作一致”。

RimWorld 侧证据分两级：**已编译现役代码**（模组自身，编译即证明签名存在）与**反编译原生切片**（`Moddev/Graphify/native/source/<Type>.cs`，见 6.8）。两者都不会进仓库。

### 6.2 语法与执行语义对照

| 原作规则（§1） | 本轮对应 | 证据 |
| --- | --- | --- |
| 唯一 Form 且在首位 | `SpellService.Validate` 语法校验 + `SpellGlyphDef.kind` | §3 第 42 行 |
| Effect 按排列顺序执行，可多个 | `SpellRecipe.glyphs` 保序（`List<string>`），执行层按序结算 | §3 第 40 行 |
| Augment 只改左侧最近的 Form/Effect，可连续叠加、不改其他 Augment | 契约把“左侧最近”固化为解析规则 | §2 第 25–29 行 |
| 命名 + 保存，书签切换，书中十个法术 | `GameComponent_Spellbook.SlotCount = 10`、`GetSlot/SaveSlot/ClearSlot`、`Get/SetActiveSlot(Pawn)` | §3 第 41 行 |
| 消耗 = 各 glyph 费用之和，随时间回复 | 本轮零 psyfocus、用施放间隔限制连发；**不实现 Mana 条** | §1 第 9 行 |
| 施法由“有灵能能力的小人”执行，原生目标选择与冷却 | 原生 `Ability`/`CompAbilityEffect` 路线在本模组已现役 | `Missiles/CompAbilityEffect_WotFArcaneMissiles.cs:26-42`、`Abilities/CompAbilityEffect_WotFPlantGrowth.cs:11` |

### 6.3 Form 族（本体 5 个）

| 本体 Form | 原作要点 | RimWorld 对应与状态 | 真实类/API（file:line） |
| --- | --- | --- | --- |
| `glyph_self` | 以施法者为目标 | **当前**：施法者自身及所在格，不给玩家额外点选 | `LocalTargetInfo` 值类型（`native/source/Verse.LocalTargetInfo.cs:12`，`Thing`(:32)/`Cell`(:74)） |
| `glyph_touch` | 以被点选方块/实体为目标 | **当前**：只对相邻可接触目标，不隔墙、不做远距离瞬发 | `Verse.LocalTargetInfo`；相邻/阻挡判定属运行层，`GenAdj`/`GenSight` 未入切片，**未确认** |
| `glyph_projectile` | 生成投射物，命中实体或方块时结算 | **当前**：复用原生追踪射弹，沿途真实碰撞 | `Verse.Projectile.Launch`（`native/source/Verse.Projectile.cs:178`）、`CheckForFreeInterceptBetween`（:276）、`CheckForFreeIntercept`（:322）；现役 `Missiles/Projectile_ArcaneMissiles.cs` |
| `glyph_underfoot` | 以施法者脚下格为目标 | 后续：`Pawn.Position` 常量偏移即可 | `Verse.Pawn` 继承 `Verse.Thing.Position`（`native/source/Verse.Thing.cs:266`） |
| `glyph_pantomime` | 作用于视线内最近方块 | 后续：需要视线射线；RimWorld 有 `GenSight`/`Verb` 目标解析但未入切片，**未确认** | — |

注：结构页把 `glyph_rune`（放置触发符）与 `glyph_orbit`（环绕投射物）标成 `Type: Effect`，与其“位于首位、充当时序容器”的用法不符，疑为文档归类问题；本体实际分类**未确认**，本节按 Effect 记录，不据此改契约。

### 6.4 Effect 族（按研究要求分组；本体为 `ars_nouveau:glyph_*`）

状态列：**当前** = 契约内本轮实现；**后续** = RimWorld 有真实 API、可后续做；**无合理对应** = RimWorld 无此机制或需新子系统。

**战斗（伤害/状态/击退）**

| 本体 glyph（Tier/Cost/本体兼容） | 原作要点 | RimWorld 对应与状态 | 真实类/API（file:line） |
| --- | --- | --- | --- |
| `harm`(T1/15/Amplify) | 直接伤害，文档默认 5 点、每层 Amplify +2 | **当前**：沿用现有伤害类型与真实 `TakeDamage` | `Thing.TakeDamage(DamageInfo)`（`native/source/Verse.Thing.cs:1482`）；现役 `Missiles/ArcaneMissileImpactWorkers.cs:19-27` |
| `explosion`(T2/200/Amplify+AOE) | 在落点爆炸，Amplify 同时加伤害与尺寸 | **当前**：原生爆炸，每次命中只一次，AOE 只扩大这一次 | `GenExplosion.DoExplosion(... float radius, DamageDef, Thing instigator, int damAmount, float armorPenetration, … bool damageFalloff …)`（`native/source/Verse.GenExplosion.cs:19`）；现役 `ArcaneMissileImpactWorkers.cs:70-79` |
| `ignite`(T1/15/AOE) | 点燃实体或方块 | 后续（有 API） | `FireUtility.TryAttachFire(this Thing, float fireSize, Thing instigator)`（`native/source/RimWorld.FireUtility.cs:116`） |
| `snare`(T1/100/—) | 禁止移动/跳跃 | 后续（有 API） | `StunHandler.StunFor(int ticks, Thing instigator, …)`（`native/source/RimWorld.StunHandler.cs:181`） |
| `wither`(T3/100/Amplify)、`hex`(T3/100/Amplify)、`freeze`/`cold_snap`/`crush`/`wind_shear`/`fangs`/`lightning`/`flare`/`firework` | 各类 debuff、地形转化、定点伤害 | 无合理对应（debuff/地形转化在本体无对应 Def；`StunFor`+`TakeDamage` 只能近似）；`lightning` 的落雷 API 未核实 | — |
| `gust`(T1/15)、`launch`(T1/30)、`leap`(T1/25)、`pull`(T1/15)、`gravity`(T2/15)、`wind_burst`(T1/30) | 击退/抛起/拉扯/坠落 | 后续：可用原生飞行器搬运 pawn | `PawnFlyer.MakeFlyer(ThingDef flyingDef, Pawn, IntVec3 destCell, EffecterDef, SoundDef, …)`（`native/source/RimWorld.PawnFlyer.cs:288`） |
| `bounce`/`bubble`/`glide`/`slowfall`/`invisibility`/`intangible`/`rewind`/`wall`/`burst`/`linger`/`orbit`/`rune`/`summon_decoy` | 持续/定时/环境改写类 | 无合理对应（需要持续场、定时链、假体方块等新子系统；本轮不建；`delay`/`reset`/`redstone_signal` 见下方“感知/杂项与自动化”） | — |

**治疗**

| 本体 glyph | 原作要点 | RimWorld 对应与状态 | 真实类/API |
| --- | --- | --- | --- |
| `heal`(T2/50/Amplify) | 治疗少量生命并消耗施法者饥饿；对亡灵反成伤害 | **当前**：只治既有非永久伤口，不复活、不补肢；饥饿消耗按 §1 零消耗边界不实现 | `Hediff_Injury.Heal(float)`（`native/source/Verse.Hediff_Injury.cs:206`）+ `HediffUtility.IsPermanent(this Hediff)`（`native/source/Verse.HediffUtility.cs:46`）+ `Pawn.health.hediffSet.hediffs`（`native/source/Verse.Pawn.cs:29`）；现役 `ArcaneMissileImpactWorkers.cs:41-56` |

**农业**

| 本体 glyph | 原作要点 | RimWorld 对应与状态 | 真实类/API |
| --- | --- | --- | --- |
| `grow`(T2/70/**仅 AOE**) | 相当于骨粉催长 | **当前**：`PlantGrowthEffect`，只改 growth，保留实例/HP/年龄/收获规则；Amplify 无意义（与原作只允许 AOE 一致） | `Plant.Growth` setter（Clamp01 + 清缓存，`native/source/RimWorld.Plant.cs:88-94`）；现役 `Abilities/PlantGrowthEffect.cs:52`、`MapMeshDirty`(:56) |
| `harvest`(T1/10/仅 AOE) | 取走成熟产物但不毁植株 | 后续：交给原版收获 Job，不手写掉落 | `Plant.HarvestableNow`（`Plant.cs:114`）、`DesignationDefOf.HarvestPlant`（`native/source/RimWorld.DesignationDefOf.cs:25`）、`DesignationManager.AddDesignation`（`native/source/Verse.DesignationManager.cs:172`）、`new Designation(LocalTargetInfo, DesignationDef, ColorDef)`（`native/source/Verse.Designation.cs:54`） |
| `fell`(T1/150/Amplify+AOE) | 整棵伐倒 | 后续：原版砍伐指示 | `DesignationDefOf.CutPlant`（`DesignationDefOf.cs:23`） |
| `crush`(T2/30) | 石→砾→沙 | 无合理对应（RimWorld 无方块材质链） | — |

**建筑/采集**

| 本体 glyph | 原作要点 | RimWorld 对应与状态 | 真实类/API |
| --- | --- | --- | --- |
| `break`(T1/10/Amplify+AOE)、`cut`(T1/0) | 破坏方块 / 模拟剪刀 | 后续：原版挖掘指示或真实伤害 | `DesignationDefOf.Mine`（`DesignationDefOf.cs:15`）、`Mineable.DestroyMined(Pawn)`（`native/source/RimWorld.Mineable.cs:44`）、`Thing.TakeDamage`（`Verse.Thing.cs:1482`） |
| `place_block`(T1/10/AOE)、`exchange`(T2/50) | 用物品/快捷栏方块放置或交换 | 后续：走原版建造流程（蓝图+材料），不凭空生成成品 | `GenConstruct.PlaceBlueprintForBuild(BuildableDef, IntVec3, Map, Rot4, Faction, ThingDef, …)`（`native/source/RimWorld.GenConstruct.cs:172`）、`GenSpawn.Spawn(ThingDef, IntVec3, Map, WipeMode)`（`native/source/Verse.GenSpawn.cs:17`） |
| `phantom_block`(T1/5)、`prestidigitation`(T1/0) | 临时方块，Amplify 变永久 | 后续：生成 + 生命期 comp | `GenSpawn.Spawn(...)`（`Verse.GenSpawn.cs:17`）、`Thing.Destroy(DestroyMode)`（`Verse.Thing.cs:1048`） |
| `light`(T1/25) | 永久光源 | 后续：发光 comp 现成 | `CompGlower.GlowColor`（可写覆盖，`native/source/Verse.CompGlower.cs:44`）、`CompGlower.UpdateLit(Map)`(:103) |
| `smelt`(T2/100)、`conjure_water`(T2/80)、`evaporate`(T1/50) | 即时冶炼 / 生成或清除流体 | 无合理对应（原版冶炼是 RecipeDef+Bill；无流体系统） | — |
| `rotate`(T1/10)、`interact`(T1/10) | 旋转方块 / 代玩家交互 | 部分对应（`interact` 后续）：开关类可用转换 comp；旋转未核实 | `CompFlickable.SwitchIsOn`（`native/source/RimWorld.CompFlickable.cs:43`）、`DoFlick()`(:98) |
| `craft`(T1/50) | 打开合成界面 | 无合理对应（不做即时合成菜单） | — |

**物品与过滤**

| 本体 glyph | 原作要点 | RimWorld 对应与状态 | 真实类/API |
| --- | --- | --- | --- |
| `pickup`(T1/10/仅 AOE) | 拾取周围物品 | 后续：入施法者背包 | `GenPlace.TryPlaceThing(Thing, IntVec3, Map, ThingPlaceMode, …)`（`native/source/Verse.GenPlace.cs:32/38`）、`Pawn.inventory.innerContainer`（`native/source/Verse.Pawn.cs:33`、`native/source/Verse.Pawn_InventoryTracker.cs:20`）、`ThingOwner.TryAdd(Thing, bool)`（`native/source/Verse.ThingOwner.cs:207`） |
| `toss`(T1/10/Amplify) | 把物品丢到目标位置 | 后续 | `ThingOwner.TryDrop(Thing, IntVec3, Map, ThingPlaceMode, …)`（`Verse.ThingOwner.cs:438`） |
| `ender_inventory`(T2/50)、`infuse`(T2/30) | 远程末影箱 / 灌注药水 | 无合理对应（无跨图背包、无药水） | — |
| **过滤族** | 条件/逻辑过滤 glyph | **全部为附属模组**，不是本体：`ars_controle:glyph_filter_{above,below,level,not,or,random,xnor,xor}`、`ars_elemental:glyph_{aerial,aquatic,fiery,insect,undead,summon,not_*}_filter`、`not_enough_glyphs:glyph_filter_{dark,light}`。本轮**不做**；后续若要“过滤”语义，RimWorld 的对应物是 `ThingFilter`/`StorageSettings`，不在契约内。 | 无（附属模组） |

**感知/杂项与自动化**

| 本体 glyph | 原作要点 | RimWorld 对应与状态 | 真实类/API |
| --- | --- | --- | --- |
| `sense_magic`(T2/50)、`name`(T2/25)、`dispel`(T1/30)、`wololo`(T1/30) | 探测/命名/驱散/改色 | 无合理对应 | — |
| `delay`(T1/0)、`reset`(T1/0)、`redstone_signal`(T1/0) | 延迟结算 / 重置目标 / 红石 | 无合理对应（需要本模组自建调度与信号层；`CompFlickable` 只能近似开关电源，不代表红石时序） | — |
| `summon_steed`/`summon_wolves`/`summon_undead`/`summon_vex`/`animate_block`(T2/200) | 临时召唤物 | 后续（有生成 API，临时生命期需自建）：`GenSpawn.Spawn`、`PawnGenerator`（未入切片，**未确认**签名） | `Verse.GenSpawn.cs:17` |

### 6.5 Augment 族（本体 13 个）

| 本体 Augment（Tier/Cost） | 原作要点 | 本轮状态 | RimWorld 对应与真实 API |
| --- | --- | --- | --- |
| `amplify`(T1/20) | 加算提升多数效果强度；对 Break 提升采集等级 | **当前**：只改前一伤害/治疗/爆炸强度；对瞬间成熟 Grow 不假称生效 | 数值来自 `ArcaneMissileSettings.damageAmount/healAmount`（`Missiles/ArcaneMissileSettings.cs:34,39`），真实结算见 6.4 三处现役 API |
| `aoe`(T2/35) | 扩大作用范围 | **当前**：每目标最多一次，爆炸不重复生成 | 爆炸半径即 `DoExplosion` 的 `radius` 参数（`Verse.GenExplosion.cs:19`）；半径枚举（`GenRadial`）未入切片，**未确认** |
| `accelerate`(T2/10)/`decelerate`(T2/5) | 只改投射物速度 | **当前**：只改飞行速度 | `ArcaneMissileSettings.speedCellsPerSecond`（`ArcaneMissileSettings.cs:24`，clamp 1–60 见 :71） |
| `split`(T3/20) | 多弹齐发，每弹各自结算 | **当前**：复用现有发散与单弹结算 | `ArcaneMissileSettings.missileCount`（`ArcaneMissileSettings.cs:23`，clamp 1–12 见 :70） |
| `dampen`(T2/0) | 减弱强度（Amplify 的反向） | 后续（与 amplify 同一倍率位） | 同上 |
| `extend_time`(T2/10)/`duration_down`(T2/15) | 延长/缩短持续时间 | 无合理对应：本轮效果全是瞬时，没有持续时间概念 | — |
| `pierce`(T2/40) | 投射物穿过后继续飞行 | 后续：需改 `HitFlags`/命中后继续；`Verse.Projectile` 拦截逻辑已入切片（`Verse.Projectile.cs:276/322`），具体改法**未确认** | — |
| `sensitive`(T1/10) | 让 Form 命中草/流体等非阻挡物 | 无合理对应（本轮不做方块级目标语义） | — |
| `extract`(T2/30)/`fortune`(T2/80) | 精准采集 / 掉落加成 | 无合理对应（掉落由 ThingDef/`PlantProperties` 决定） | — |
| `randomize`(T1/0) | 随机化部分 glyph 行为 | 无合理对应（本轮无脚本化行为） | — |

### 6.6 一句话汇总

本体 87 个 glyph 里，与 RimWorld 现有机制**稳妥对接**的只有：伤害、爆炸、治疗、催熟、点燃、眩晕、点位移动、方块生成/发光、物品移入移出、挖掘/砍伐/收获指示、传送位移——其中 12 项已是本轮契约，其余属“有 API 但不在本轮”。持续效果、方块材质与流体、红石/定时链、召唤与假身、随机化与掉落改写，都属于**新子系统**，本轮明确不做，也不在文档里假称已支持。

### 6.7 未确认接口（不得当作已实现）

1. 目标合法性与相邻/阻挡判定所用 `GenAdj`/`GenSight` 系列签名——未反编译，需运行负责人按真实 API 选定。
2. `pierce` 的实现路径（改 `ProjectileHitFlags` 还是命中后重发）——`Verse.Projectile` 已入切片但该改法未验证。
3. `lightning` 落雷、`rotate` 旋转方块、召唤物临时寿命（`PawnGenerator`/租赁类 comp）——未入切片。
4. 结构页把 `rune`/`orbit` 归为 Effect，与用法不符；本体真实分类未核实。
5. `heal` 的“消耗饥饿”在 RimWorld 无等价物，本轮按零消耗边界处理，不宣称对齐。

### 6.8 Graphify 边界（本次未重建图谱）

快照 `Moddev/Graphify/graphify-out/metadata.json:3` 仍是 `indexed_at_utc 2026-10-27T07:35:02Z`、794 节点、`head_commit 8646749`；研究时工作树 HEAD 已是 `2a44f27`、`Source/WhisperOfTheForest` 下 39 个 `.cs`（快照时 35），且 `Spells/`、`UI/Spellbook/` 正由其它 worker 写入。**源不稳定，故未执行 `refresh-graph.ps1` 的图谱抽取**，只补做原生参考切片：新增 `pwsh -File refresh-graph.ps1 -NativeOnly`（不 staging、不抽取、不发布 metadata），重反编译 25 个类型到 `Moddev/Graphify/native/source/`，清单与用途见 `Moddev/Graphify/native/NATIVE-INDEX.md`。因此：本节 file:line 一律指 `native/source/<Type>.cs` 或模组源文件，不引用图谱节点；图谱节点/边不覆盖本轮 a22 代码，读数时按此过期边界处理。反编译内容仅作参考，不进仓库、不进 DLL。

## 7. 实际交付与验证（集成任务填写）

### 7.1 构建与静态检查（2026-10-28 01:17–01:37 本地）

本轮集成共三次构建，前两次都因**序列化日志缺陷**不能作为交付件（细节见 7.3）：

| 序 | sha256 | size | 状态 |
| --- | --- | --- | --- |
| 1 | `b04c9f9935b22b2b118f01d36a172f1866b1ecde83e6811adf9f1803c1fbe9aa` | 185856 | 加载 a22/a23 存档即喷 `Cannot fill dictionary … activeSlots/lastCastTicks` |
| 2 | `756bfb1907356b9f88295995809ea3d61d0bfeec7dcc3106fb499f11e5d3e0fb` | 185856 | 空字典守卫对 Saving 生效，但存档缺节点，加载仍喷 `You need to provide working lists …` |
| **3（交付）** | **`353773d6f47c4722b44f517826af571412efcf32686d464ff65836e8a61bcbfe`** | **185856** | `activeSlots`/`lastCastTicks` 改显式 working lists（Reference/Value，`logNullErrors=false`） |

- `Tools/build.ps1 -Verify`：**47 PASS / 0 FAIL**（含 15 个 a22 Def）；`Tools/check-defs.ps1`：35 引用 **0 missing**；
  `Moddev/tools/check-spells/check.ps1`：**ALL CHECKS PASSED**（含 targeting harness、glyph config harness）。
- 部署件与仓库件一致：`Mods/Whisper-of-the-Forest` 为指向本仓库的 junction，部署 DLL 实测 `185856` /
  `353773d6f47c4722b44f517826af571412efcf32686d464ff65836e8a61bcbfe`（`Moddev/evidence/a23-before/build.txt`）。
- 旧四技能、`Missiles.xml` 与 a18–a22 存档均未被本轮改动（存档 mtime 逐一核对未变）。

### 7.2 隔离实例与日志

- 命令：`-savedatafolder=Moddev/lab-profile -wotflab=Moddev/lab-run -quicktest`（**无 `-mod`**，沿用 profile 内
  ModsConfig，含 CharacterEditor/Royalty/DLC），日志取 `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by
  Ludeon Studios\Logs\Player.log`。
- 最终构建加载交付档 `a23-final` 的新实例：**0 条 `ERROR`、0 条 `Config error`、0 条 `Could not instantiate Verb`、
  0 条 `WarmupTick` NRE、0 条 `Cannot fill dictionary`/`working lists`**。遗留 1 条本轮之前即存在的
  `WARN Translation data for language Simplified Chinese has 1 errors`（非本模组 Keyed，未处理，如实保留）。
- 场景夹具：`setup confirm=1` 在 quicktest 地图生成 origin=(101,135)/(101,113) 两轮干净泳道（concrete 101–108、
  mud 109–112、**x=113 关闭的门 + x=113/114 墙**、旁听殖民者已在测试前征召并移出弹道）。

### 7.3 本轮修掉的两个缺陷（都先 FAIL 后复验）

1. **存档字典 ERROR**（7.1 表 1、2 行）：`GameComponent_Spellbook.ExposeData` 对空字典调用
   `Scribe_Collections.Look`，加载时喷 `Cannot fill dictionary because there are no values`。空值守卫版（表 2）把问题
   从“空列表”变成“缺节点”，加载改为喷 `You need to provide working lists`——**同一症状换了个字符串**。最终版用显式
   working lists + `logNullErrors=false` 才清零。证据：`Moddev/evidence/a23-before/Player.log.oldbuild-legacy-load`
   （35 条）与 `Player.log.session-b`（37 条）对照最终构建 0 条。
2. **a22 遗留的三条运行层缺陷已在实机复验消失**：能力 verb 可实例化（无 `Could not instantiate Verb`、无
   `WarmupTick` NRE，`SpellAbility.AbilityTick` 空实现生效）；**Projectile+Harm / Heal 对真实 Pawn 生效**
   （见 7.4，a22 的 `useHitPoints` 误判已修）。

### 7.4 已实测通过（最终构建 353773d6 上重跑关键项）

- **十槽编辑与读档**：`slot=0..9` 全 ok（含 `Projectile,Split,Harm` 这种增幅在效果之前的顺序）；`copyprobe`
  `countBefore=countAfter=2, isolated=true`；非法组合被拒并给出可见原因——缺 Form（`每一个法术必须以形式开头…`）、
  两个 Form、`Amplify` 绑 `Grow`（拒绝保存 slot=9 且**未覆盖**原 T10）。存档后读档：10 槽名称与符文顺序**逐字一致**
  （`a23-book-after-load.txt`）。
- **psylink 门槛**：新建 0 级小人 40078 → `can`/`cast` 均 `ok=false`，原因`这个小人的灵能链接等级需要达到 1 级才能施法。`，
  `begin` 的 `isTargeting=false`（未进入目标选择）；`grant` 后 `psylink=1`，**300 tick 内自动获得** `WotF_SpellbookCast`
  （未手工加能力），40081 同；随后 `can=true`。书页 `open`/`close` 状态正确（`window.open` 真开真关，`casterId` 正确）。
  观察：Lab 的 `open` 动作对 0 级小人也开窗（书页可读可编辑），**施法仍在 `SpellService` 侧被拒**；原生 Gizmo 只对合格
  小人出现。
- **Self Heal（slot0，40078）**：`hurt 2` 后伤口 **1 条 / severity 2.0**（hediffCount 3）→ 施放后 **0 / 0**（hediffCount 1）。
- **Touch Grow（slot4，40081 → 野生草 14244@(42,148)）**：growth **0.266 → 1.0**、Mature、hp 85 不变、同实例。
- **Touch 7 格拒绝**：`cast=false`，原因`接触法术只能作用于相邻格。`，目标 14246 growth 0.671 未变。
  （注：首次尝试时被 2.0 s 冷却挡下并由 `仍在凝聚灵能…` 说明，冷却后再测才得到距离拒绝——**冷却本身也是真实生效的**。）
- **Projectile Harm 打真 Pawn（slot2，40078@(101,115) → 40081@(112,115)，impact age=38）**：目标 Pawn 伤口
  **0 → Crack 9.0 + Bruise 8.92**（一次 impact，`live=0/impact=1`）。这正是 a22 未通过的那条。
- **Projectile Heal 治真 Pawn（slot3）**：severity **17.92 → 5.84**，Bruise 被移除、Crack 9→5.84，伤口数 2→1。
- **多效果链（slot5 = Projectile+Harm+Heal）**：一次 impact 内按序结算——新增伤 18 被 Heal 12 抵消，余 **6.0**
  （severity 5.84→11.84）。
- **AOE Grow 两株干净前置（slot6 = Self+Grow+AOE，caster@(101,114)）**：40178 Potato 与 40179 Oak 均 **0.15 → 1.0**、
  Mature、harvestable、hp 不变（85/200），一次施法两株。
- **plan 数值**：Harm 9 → **13.5**（Amplify×1）、弹速 16 → **28.8**（Accelerate）/ **8.8**（Decelerate）、
  Split **1→3** 发、AOE 半径 **1.9→3.1**、Explosion 12 → **18** 且半径 3.1。
- **飞行中存档 → 改槽 → 读档**：slot9 临时改成 `Projectile+Decelerate+Harm`（8.8 速度），`cast` 后 `step 15` 让 age 前进
  → `save a23-flight`（`live=1`）→ 把 slot9 改成 `Projectile+Heal` → `load`：**`live=1`、目标伤口未变**；继续 step 后
  **一次** impact（`impacts=1`），日志 `age=68`（大于读档后经过的 tick，证明 age 延续而非归零），目标伤口 4→5（+9 新
  Crack，即发射快照里的 Harm），**改槽没有改写已在途的法术**。同一结论在 slot2 版本上复现（+9.0 一次结算）。
- **旧四技能简检**（同一实例、真实 Ability）：`WotF_ArcaneMissiles`（目标 severity +21）、`_Mender`（−10.08）、
  `_Heavy`（+7）、`WotF_MaturePlant`（14241 growth 0.766 → 1.0 / Mature）。
- **交付现场**：`a23-final.rws` **12,232,266 B**、tick **12940**、Paused、选中 **Lab Elf 40081**（psylink 1、
  abilities 含 `WotF_SpellbookCast`）、该书 `activeSlot=2`（`T03-ProjHarm`）、书页关闭、`live=0`；另留 `a23-flight.rws`
  作在途存档样例。a18–a22 未被覆盖。
- **主代理补测（最终实例，不写入交付档）**：`spellbook action=cast` 的 Projectile+Explosion 日志为
  `impact ... hit=ground explosion=1`；Projectile+Split+Harm 实发 `live=3/impact=3`；Accelerate+Amplify 的实时
  frame 为 `speed=28.8/damage=13.5`。测试后重新 `load a23-final`，最终仍 Paused/选中 40081/activeSlot2/窗口关闭/live0。### 7.5 过程中的两次“假阴性”（记录以免后人重踩）

两次“弹道落地但 Pawn 无伤”都不是结算缺陷：① 目标放在 x=127 时弹道穿过 lab 泳道 **x=113 关闭的门**，日志
`target=门 (83%) cell=(113,0,115) age=42`；② 旁听殖民者 Abbie 恰好站在 **(102,115)**，日志
`target=Abbie cell=(102,0,115) age=1`。清空泳道并征召路人后，全部命中真实目标。结论：本 lab 泳道**不能跨越 x≥113**，
且弹道前后 11 格内不得有第三方 Pawn。

### 7.6 未测（如实保留）

原生 Gizmo 与书页内的**真实鼠标点击**（仍只覆盖 action 回调与只读窗口状态）；中文像素级排版与观感、多分辨率实机；
多地图、原版护盾/投射物拦截、特殊植物（异种植物/不可催熟）边界；书页在“无 psylink 小人”下的 UI 文案本身（本轮只验证
窗口可打开 + `can`/`cast` 拒绝与可见原因）。

## 8. 2026-10-29 改版契约（与上文冲突时以本节为准）

用户要求复刻新生魔艺的书页体验，并新增 Noita/新生魔艺扩展式的命中触发。实现范围仍是技术实验，不追求数值平衡；素材本轮可参考原作，发布前换原创。

### 8.1 书页交互

- **当前槽就是编辑槽**。点击右侧任意法术槽（书签）立即把它设为当前槽，并同步人物下方施法按钮；空槽也切换，施法按钮显示空槽并禁用。
- **去掉草稿状态和确认弹窗**。点左侧符文直接追加到法术栏；点法术栏里的符文直接移除；点“创建”把法术栏写入当前槽。切槽或关书时，未创建的改动直接丢弃，与新生魔艺一致。
- **“清空”只清空法术栏**，不删除已保存法术。另设一个“删除此槽”动作，单击即清空当前槽，不再确认。覆盖已有槽也直接覆盖。
- 左页：分类标签（全部/形式/效果/增幅/触发）+ 搜索框 + 符文网格。右页：法术名输入、法术栏、实时校验、创建/清空/删除、右侧十个书签。
- 不能接的符文半透明显示；悬停用红字说明“该增幅不能接在此符文上”；存在非法组合时“创建”不可用。
- 书页约 1080×700，适配较小屏幕；继续使用原生 Verse UI 与 `TechLabDrawScope`，不引入新的 UI 框架。

### 8.2 动效

- 点符文：图标从目录飞入法术栏，约 0.15 秒缓出。
- 移除：缩小并淡出，约 0.12 秒。
- 切书签：书页横向轻翻，约 0.18 秒。
- 非法操作：法术栏轻微抖动并闪一次红光，约 0.12 秒。
- 创建成功：一道柔光从左到右扫过法术栏，约 0.35 秒。
- 游戏内触发：命中点闪现一圈符文光环，约 0.5 秒，帮助看清“这里接上了下一段”。
- 所有动效由 IMGUI 时间驱动，不新增动画库；观感最终由用户试玩微调。

### 8.3 增幅兼容（按 Ars Nouveau 5.13.3 源码）

每个符文在 XML 用 `<compatibleAugments><li>defName</li>…</compatibleAugments>` 声明可接的增幅；代码只读这份列表，不再写死规则。当前集合：

| 符文 | 可接增幅 |
| --- | --- |
| 自身 / 接触 | 无 |
| 射弹 | 加速、减速、分裂 |
| 伤害 | 强化 |
| 治疗 | 强化 |
| 爆炸 | 强化、范围 |
| 催熟 | 范围 |
| 触发 | 无 |

因此：范围不能接伤害/治疗，加速/减速/分裂只属于射弹，强化不能接催熟。`SpellService.Validate` 与书页使用同一份列表。

### 8.4 命中触发

- 新符文 `WotF_Trigger_OnHit`，`kind=Trigger`，中文“命中触发”，英文 “on hit”。
- **语法**：触发符文不能位于首位或末位；它前面是当前段，后面必须紧跟一个形式，开始下一段。增幅仍只绑定左侧最近的非增幅符文，不能跨过触发符文。
- **编译**：`SpellCastPlan` 由按顺序排列的 `SpellSegment` 列表组成。每段包含一个形式、该段的效果与已应用的增幅；触发符文标记该段“命中后继续”。一条法术可有多个触发，形成多段链。
- **发射**：每发射弹携带完整计划快照和“下一段”索引；索引随存档保存。飞行中读档继续按快照执行，不读取改后的书页。
- **命中后**：
  - 下一段是自身/接触：作用于被命中的 Thing；没有 Thing 时作用于命中格。
  - 下一段是射弹：**“目标后方”指沿入射方向穿过目标**——方向 = `normalize(hit.DrawPos − impact)`，生成点 = `hit.DrawPos + 方向 × 0.75`，再沿同方向在 20 格内取最远合法格作为目标格；因此新弹从目标另一侧继续穿透，不回头、不追踪已被命中的 Thing（`targetThing = null`）。命中物为空或已销毁时沿用原飞行 heading、锚点取命中点 `impact`；两点重合（无可用方向）时同样退回 heading。生成格不在图内或前方没有合法格时本段不触发并写日志。未来可加自动追踪和目标过滤。
- **不触发**：出界、过期、目标失效、被清理，都不执行后续段；只有真实碰撞/到达触发。
- **数量**：本轮不设总射弹上限，由 10 个符文槽自然限制；日志记录每段实际发射数。分裂可造成有限扇出，但指针只向前，不存在递归回环。
- **技术报告**：交付时说明实际采用的段列表、指针、存档字段和命中后生成方式。

### 8.5 验收

1. 点击任意书签立即切换当前槽，人物施法按钮同步；空槽禁用并显示原因。
2. 编辑流程无草稿/确认；创建、覆盖、清空、删除、切槽丢弃均按 8.1 工作。
3. 非法增幅半透明 + 红字原因，创建被阻止；合法组合可保存。
4. `[射弹][伤害][触发][射弹][爆炸]` 实机：第一发命中造成伤害，第二发从目标后方射出并爆炸；出界不触发。
5. 触发链飞行中保存/读档，指针延续且只结算一次；书页改动不改写在途计划。
6. 旧四技能、旧存档、维护版 `Missiles.xml` 不变；构建与静态检查通过；最终隔离存档暂停、选中具能力小人、书页关闭、`live=0`。
7. 未实测项如实列出；不提交、推送或发布。

### 8.6 书页 Phase 2 实现记录（书页界面 owner）

只覆盖书页侧；运行层的段列表、指针、存档字段由运行负责人记录。

- **第五个分类标签**：左页标签行改为 `全部 / 形式 / 效果 / 增幅 / 触发`，宽度统一由
  `CatalogLayout.CategoryTabWidth` 计算（`(宽 - 4×4) / 5`），`CatalogLayout.CategoryTab` 给出每格
  矩形。实测标签宽：1080×700 为 **78.4 px**，820×560 为 **56.0 px**（布局自检下限 44 px）。
- **追加合法性只有一个来源**：目录每行调用 `SpellCompiler.CanAppend(chain.glyphs, glyph, out reason)`
  （`SpellCompiler.cs:41`），页面不再有 `CanAddMore` 之类的本地上限或语法判断。
- **不可追加的显示与点击**：图标和两行文字按 `SpellbookStyle.BlockedAlpha = 0.4` 绘制；悬停时在
  目录区域内画一个深底红字小框显示 `CanAppend` 给出的 reason（原版 tooltip 文字颜色固定，无法直接
  染红）；点击不追加，只播法术栏抖动/红闪，并把同一句 reason 写进页脚状态行。
- **创建按钮**：`canCreate = SpellService.Validate(bar, out reason)`；不合法（含空法术栏、缺效果、
  增幅不兼容、触发符文悬空等）即禁用，原因由校验框用同一句 reason 显示，页面不另写规则。
- **文案**：中英 `WotF_Spellbook` 各 63 个 key，新增 `CategoryTrigger`，删除不再使用的
  `ChainFull`（满栏与其它拒绝理由统一来自 `WotF_Spells.Reason.*`）；`KindTrigger` 在 Phase 1 已加。
- **未实测**：真实鼠标悬停/点击的观感、红字小框在实际字号与中文换行下的排版、触发链在书页里的
  半透明判别是否符合直觉；这些留给隔离实例试玩与用户微调。
- **实机验收（2026-10-30）**：a24 已在隔离实例通过书签同步、兼容拒绝、触发链、越界不触发、两段飞行存档和旧四技能；结果与未测边界见 `acceptance.md` A51–A57。
