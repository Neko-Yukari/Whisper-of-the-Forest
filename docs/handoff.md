# 交接：森林的低语（Whisper of the Forest）

写给下一个接手本项目的 agent。按「当前状态 → 项目是什么 → 代码结构 → 怎么验 → 决策与坑 → 下一步」读，读完能直接开工。

## 1. 当前状态（2026-11-10）

- 仓库：`E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest`，分支 `feat/forest-magic-foundation-20261105`，HEAD `2b4efec`，已推送 origin（`git ls-remote` 已核对同 hash）。
- 该提交含 217 个文件、+12966/−338：把此前分批批准但未提交的功能（魔源电池/网络、仪式全家、自动施法炮塔、缚足、骇入、精灵命名、翠绿林庭派系、饥荒啃草、工作台特性、修电器动画）连同本轮「法术目标白名单重做」一次落库。
- `generated-images/`（17 张精灵外观迭代图，约 23 MB，全仓库零引用）**故意未提交**，仍在本地位未跟踪。要么加进 `.gitignore`，要么提交时显式排除；`git add -A` 会把它带进去。
- 已验收：目标白名单重做（含三项返修）通过独立复核；定向 L2 run `20261110-083225` 15/15；整份 Player.log `errorBlocks=0`；产品 DLL 307712 B、与部署副本一致且不含 Lab 类型；`check-defs` 141 引用 / 137 规则 / 0 failed；Lab 已退出。
- 未验收：真实鼠标选目标与观感（L3）、炮塔实战选敌、无 Anomaly 环境下的人形突变体归类、`git diff --check` 报的 3 个 Lab 文件末尾空行（未清理，不影响构建）。

## 2. 项目是什么

RimWorld 1.7 模组。设定：精灵（翠绿林庭）用一套从「闪耀时代工程站」继承的符文魔法；魔法本质是遗落科技而非神术。第一版范围见路线图，明确不做大地图派系魔法模拟、不改群系生成。

核心系统（全部已实现）：

| 系统 | 一句话 |
| --- | --- |
| 法术语法 | 法术 = 形式 + 效果 + 时机 + 增幅，符文在 Def 里声明兼容表，编译器把一次施法编译成冻结的 `SpellPlan` |
| 施法流程 | 前摇（可移动）/ 引导 / 后摇；施法中可存读档 |
| 目标白名单 | 每个效果符文写 `<targets>`，创建时取并集冻结进 `SpellRecipe` 并随档保存；玩家、法杖 AI、炮塔共用 |
| 射弹引擎 | 基于原版 `Projectile`，支持追踪、分裂、加减速、穿透 |
| 魔源网络 | 发光节点按半径连网；储量只在魔源电池里；燃木熔炉 / 灵木苗圃 / 魔源转化器供能 |
| 仪式 | 古林祭坛 + 三色粉笔环（3×3/5×5/7×7）+ 主持人读条 + 网络扣费 |
| 自动施法炮塔 | 抄写书中槽位副本或新写法术进炮塔，按 `cost.mana × manaCostFactor` 扣网 |
| 种族 | HAR 精灵 + 林庭血脉基因（魔力、经验、心灵熵、等级）；外观暂用米莉拉占位贴图 |

## 3. 代码结构

```
Source/WhisperOfTheForest/         产品代码，csproj 自动编译（EnableDefaultCompileItems）
  Spells/                          SpellGlyphs(符文与ConfigErrors) SpellCompiler SpellRecipe SpellPlan
                                   SpellExecutor SpellService(选敌参数) SpellAbility SpellTargeting
    Casting/                       SpellCastingService SpellCast(含 Scribe 的 SpellTargetKind) Patch_*
    Effects/                       MechHack HediffComp_Ensnare 等
  Missiles/                        ArcaneMissile* Projectile_ArcaneMissiles
  Turrets/                         CompSpellTurret Building_SpellTurret Verb_CastTurretSpell Window_TurretSpell
  Mana/                            CompManaBattery CompManaNode ManaNetwork MapComponent_ManaNetwork
  Rituals/                         RitualDef RitualEffectWorker RitualEffect_* JobDriver_PerformRitual
  Race/                            ForestBloodline* ElfNames FamineGrass ForestRepair ...
  Equipment/                       CompSpellStaff CompShortStaff SpellStaffModifiers Verb_CastSpellStaff
  UI/ Diagnostics/
Source/Lab/  Source/LabTests/      仅 WotfLab=true 时编译，不进产品 DLL
1.7/Defs/ 1.7/Languages/ 1.7/Assemblies/(DLL 是发行产物，故意入库)  Textures/
Tools/                             build.ps1 test.ps1 check-defs.ps1 lab.ps1 launch-lab.ps1
docs/                              本文、rimunity-vfx-workflow.md、audio-candidates.md、adr/
```

`Mods/Whisper-of-the-Forest` 是指向本仓库的 Junction，改仓库即改装载的模组。

## 4. 资料索引

设计文档（Obsidian，权威来源，代码只是实现）：

- `C:/Users/81004/Documents/Obsidian Vault/环世界模组-森林的低语/00-总览.md`
- 设定：`设定/01-世界背景与低语` `02-精灵种族设定` `03-派系与对外关系` `04-魔法体系（遗落科技）` `05-建筑与器物`
- 技术：`技术/01-种族与派系实现` `02-魔法实现`（目标选择、界面布置、施法流程） `03-法术表`（全部符文与数值） `04-仪式实现`（含魔源网络计划） `05-第一版落地路线图`（完成项、未验收项、施工阶段、待细化）

仓库内：`docs/adr/0001-live-spell-slot-and-trigger-segments.md`、`docs/rimunity-vfx-workflow.md`、`AGENTS.md`（项目规则，必读）。

反编译原版源码：`Moddev/Graphify/native/`（1.7）。Graphify 图谱：`Moddev/Graphify/graphify-out/graph.json`，快照 2026-11-10 06:53 UTC，69,647 节点 / 234,690 边；**只含产品代码，不含 LabTests**，测试入口仍需 grep 补齐。查询用 `graphify_hybrid.local__*` 并显式传 `project_path`。

实机证据：`Moddev/lab-run/test-runs/<run>.json`（结果）与 `<run>.disklog.json`（整份 Player.log 扫描）。交接用的临时说明在 `C:/Users/81004/AppData/Local/Temp/opencode/`，会过期，别当长期资料。

## 5. 怎么验

验收分四层，缺一层就不能说完成：

- **L0 编译**：`Tools/build.ps1`（产品）或 `Tools/build.ps1 -Lab`（含测试宿主），要求 0 警告 0 错误。
- **L1 加载**：Lab 启动后 L1 检查 `ownErrors run/load=0`、Harmony patch 数、Def 数，并扫描整份磁盘 Player.log 的 ERROR 块；日志缺失或属于别的进程一律记「未验证」。
- **L2 实机行为**：`Tools/test.ps1 -Filter 'a,b'`（逗号合跑，`-StopAfter` 首错即停）。测试走真实游戏对象与公共入口，不走 shim。
- **L3 观感**：只能由用户看。给可运行实例、存档名、观察路径，不要用截图或文字替用户下结论。

常用命令：

```powershell
Tools/build.ps1                 # 产品构建，输出到 1.7/Assemblies
Tools/build.ps1 -Lab            # 含 Lab/LabTests
Tools/test.ps1 -Filter 'spell.,turret.' -StopAfter
Tools/build.ps1 -Verify         # 构建并跑 check-defs
Tools/check-defs.ps1 -Repo .
```

硬规则：同一时间只允许一个 agent 构建或开游戏；开测前确认 `RimWorld` 进程为 0；测试跑完关 Lab 并释放使用权。产品 DLL 必须不含 `LabTestRunner` / `LabHost` / `WOTF_LAB` 标识。

## 6. 决策与已知坑

已定决策（改之前先问用户）：

- 不兼容旧版本与旧存档，不做迁移、不留兼容层（`AGENTS.md`）。
- 数值只写在 Def，C# 不复制默认值；Def 保证的字段不在运行时加空判断；错误只在真实输入边界（存档加载、Lab 命令解析、第三方回调）处理。
- 产品代码不得引用 `Lab/`、`LabTests/`。
- 目标白名单：每效果 XML 声明、创建时并集冻结、不按派系筛、效果命中时自己检查语义条件（如骇入只收敌对机械）；范围增幅加「格子」，延迟/滞留/符印只加「格子」不继承载荷类别；自身/环绕免选；轨道炮九类。
- 目标类别按种族判定：异象用 `RaceProps.IsAnomalyEntity`，机械用 `IsMechanoid`，**不要用 `Pawn.IsEntity`**（它看 `Faction.OfEntities`，会随派系变化）。人形突变体按自身种族算人形，不单列类别。
- 炮塔：类别开关 × 关系开关只能收窄法术白名单；非敌对建筑/植物/岩石/物品一律不选；抄写保留冻结白名单，只有新草稿才推导。
- 魔源储量只在电池；节点只负责连网；`ManaNetwork` 的 `Capacity/Stored/Add/TryDraw` 签名不变。
- 精灵只生成女性；名 + 氏族名，名单混合 D&D 5e / 日式奇幻 / 四元素；存档只存拉丁拼写，显示层按设置替换；`Naivara Amastacia` 是用户原创角色，随机生成必须跳过。
- 世界观：林庭与帝国原版永久敌对（不补白名单）；精灵故乡是独有森林，不绑原版发光森林；遗迹不基于 Odyssey，以后可单向兼容进太空；林庭血脉基因保持非 archite 以便基因提取。

坑（踩过的）：

- 把 `Moving` 能力上限设 0 的 Hediff 会让人倒地（`ShouldBeDowned`），定身要用 `IsRooted` 拦寻路。
- 战斗 AI 会给被定身者重派找掩体的移动任务，见 `Patch_EnsnareCombat.cs`。
- 炮塔炮管挂自定义 verb 时，`PlaceWorker_ShowTurretRadius` 找不到 `Verb_Shoot` 会空引用。
- 原版选敌开关分不清无派系机械与动物，只能粗筛，最终由 `SpellTargeting` 裁决。
- `Pawn.IsEntity`、`IsSubhuman` 这类属性语义容易误读，用前读反编译源码。
- 施法目标身份必须显式存（`SpellTargetKind.Cell/Thing`），不能用「Thing 引用为 null」推断选了格子。
- 改完不要靠反复重跑构建碰运气，先回读源码定位原因。
- 回归范围按依赖选，不默认跑全量：先用图谱找改动函数的调用方，再用 grep 找经过这些入口的 LabTests。

## 7. 下一步（按路线图）

1. 阶段 2 收尾：逐项实机检查法术表，补滞留/地印/环绕/落雷的视觉，标定数值与手感。
2. 阶段 5 收尾：精灵外观替换占位贴图（**对外可见，需用户先批准**），实机外观检查。
3. 阶段 6：远征遗迹（世界地点、地图、一次性传送节点、超位符文拓片、回程法阵）。
4. 待用户拍板：人形突变体是否单列类别、法杖对「自身/环绕」法术的交战距离、心灵敏感度是否对齐原版、普通符文是否也给大范围/分裂加熵。

开工前先读 `AGENTS.md` 和 `技术/05-第一版落地路线图.md`，它们比本文更新。
