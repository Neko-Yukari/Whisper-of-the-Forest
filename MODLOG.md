# Whisper-of-the-Forest 技术实验记录

## 范围与授权

用户要求以私有仓库为基础完成实际模组，允许本机操作和自主实验，全部资料保存在 `E:/SteamLibrary/steamapps/common/RimWorld/Moddev`。所有实验集成现有模组，不提交、不推送、不发布。

## 已核实环境

| 项目 | 结果 |
| --- | --- |
| 仓库 | `Moddev/Whisper-of-the-Forest`，初始 HEAD `8646749`，分支 `main`，初始工作树干净 |
| 包名 | `NekoYukari.WhisperOfTheForest` |
| 游戏 | RimWorld 1.7.4985 rev1269，Steam unstable |
| 引擎 | Waylith / .NET 10 / Vulkan，游戏根目录内直接提供程序集 |
| Harmony | Workshop 2009463077，模组 3.0.1.0，1.7 库 3.0.0.0 |
| RimUnity | Workshop 3819570122，本机模组 0.1.17，程序集位于 `1.7/Assemblies` |
| 原仓库内容 | About、Defs、Languages、Textures；精灵 Def 使用 Humanoid Alien Races |
| HAR | Workshop 839005762 已安装；需验证自带 Harmony 与前置的加载结果 |
| Royalty | 已安装；真正 Psycast 在 Activate 中检查此 DLC |
| 工具链 | gh/git 可用，系统最高 SDK 9.0.319；.NET 10 runtime 存在但 SDK 缺失 |

本地证据来自 `ref:noble-lagoon-quarry`，关键文件已由主代理核实。RimUnity 官方资料见 <https://github.com/chezhou9413/RimUnity>。

## 实现路线

采用 XML Def + C# 组件 + Harmony 定点行为补丁。飞弹逻辑按游戏 tick 前进，粒子由 RimUnity 呈现；路径碰撞由 RimWorld 地图状态判断。使用本地 SDK 和独立实验场景验证，保留原种族内容。

计划、所有权与验收标准见 [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)。

## 调查发现与已知问题

- 公开 RimUnity exporter 版本与本机 runtime 版本不同，开发以本机程序集和匹配源码为准。
- 原版 Thing 不自动成为 RimUnity collider，不能把粒子碰撞等同于游戏命中。
- 原仓库只有 HAR 的 loadAfter，没有硬依赖声明；实现需补齐实际前置关系。
- `Pawn_PathFollower.CostToMoveIntoCell`、路径节点消费和门通行规则需结合方法体及实机验证，不能直接清空地图成本。
- 旧 ilspycmd 8.2 在当前程序集上失败；dotnet-ildasm 0.12.2 附带的 Mono.Cecil 可用于元数据和 IL。
- enhanced_terminal 的反斜杠路径曾被剥离，后续命令统一用正斜杠和分离 argv。

## 验证记录

### 2026-10-28：组合施法与魔术书开工（未验收）

- 用户授权 auto drive：分析 Ars Nouveau 的魔法组成及 RimWorld 对应机制，提供能组装、保存并交给有能力小人施放的魔术书，可自由使用原创生图或许可清楚的免费资源。共享契约见 `Experiments/spellbook.md`，验收清单见 `Experiments/acceptance.md` A41–A49。
- 已核实原作基本语法：仅一个 Form 且位于首位；多个 Effect 顺序执行；Augment 作用于左侧最近的兼容 Form/Effect。首轮固定 Self/Touch/Projectile、Harm/Heal/Explosion/Grow、Amplify/AOE/Accelerate/Decelerate/Split，十符文、十法术槽。使用当前存档法术库与已有原生追踪射弹，不将实验零消耗称为完成了原作 Mana 系统。
- DeLM run `run_f012dba2178b4904b0b8ef7d`，研究 `tidy-pebble-acorn`、运行层 `misty-summit-clover`、书页 `crisp-brook-kettle` 并行；集成 `wild-aurora-kettle` 依赖前三项，独占最终 DLL 与隔离实例。当前三项已认领、尚无完成证据，主代理未实现功能或操作 Lab。
- 开工保护基线：分支 `feat/rimworld-native-projectile-lab-a20-20261027` / HEAD `2a44f27c73d5eb3754b60d475ca476a87c97097e`；DLL 仍为已验收 a21 的 `a1a877c60e18fabc678b5b888521a742740940caa666d817df4bd0f0d2372b5`。维护版 Missiles.xml 哈希为 `650b6b5959c54ae9e7cc95188e75e3d53bd5f29dad13752c23747032c33c8549`，继续只读。
- 原最终存档 SHA256：a18 `1f0ee13058e7332833bfa8e878cff016ed4f874c8bea18512e030afd5af5c313`；a19 `c93de88320b6899ed305523f8cedbe856394128c852a0534483a50ecf40d7afa`；a20 `fd3a8021587352f6071c12d7f686e66efd1bbe97055724309ff5a49010dfd3fd`；a21 `da7804abd1dab207eacf442c859ed5988e813a0682ca4012f8818d093ae4d71e`。新验证使用 a22，不覆盖这些文件。
- 开工遇到 `delm_status: expireLeases is not defined`，已报维护工单 `sp_bcc3672609ba4f1da52cb57515204578`。维护修复热重载中间态引用后，主代理真实读取旧 run，结果 seq19/completed/2done/0claimed；未唤醒旧 worker，随后才启动新 run。
- 首轮 a22 集成构建为 185,344 B / SHA256 `9e04ec5a462a10ee20dfbd01efa0fecb40957421a9d7dffd576d0bb194c17d9c`，静态检查 47 PASS；但真实 Player.log 报 `Could not instantiate Verb ... Cannot create abstract class Verse.Verb` 与每 tick `Ability.WarmupTick` NRE，且 Projectile+Harm 命中 Pawn 后无伤口。a22 只能作为失败现场，不能作为最终交付；已保留 `a22-final.rws`、`a22-player.log`、旧 run 审计并停止旧 run，另开 a23 run 修复根因后重建。
- a22 已通过的部分：十槽保存、副本隔离、读档名称/符文顺序、非法组合原因、无 psylink 拒绝、Self Heal、Touch Grow 相邻与远距拒绝、plan 数值（Split/Accel/Decel/Amplify/Explosion）以及书页回调开窗。未通过项按上一条保留。
- 旧 run 的两个 worker 均在持有任务时被宿主结束；维护确认当前 DeLM 无 claim 释放入口，故不编辑 board、不伪造 DONE，改以 `run_e5d55b029eee440b877ae127` 继续。
- 随后维护修复 DeLM 工具注入（`ab7f8505`，`delm_*` 不再经 Code Mode），运行层修复完成：`SpellAbility.AbilityTick` 空实现、`WotF_SpellbookCast` 的 `verbClass=Verb_CastAbility`、`SpellTargets` 以 health 判定 Pawn、`SpellExecutor` 改用该判定，`GameComponent_Spellbook` 用显式 working lists 保存空/部分失效的 pawn 字典。check-spells 273 PASS / 0 FAIL。
- a23 最终构建与部署件均为 185,856B、SHA256 `353773d6f47c4722b44f517826af571412efcf32686d464ff65836e8a61bcbfe`；verify 47 PASS、check-defs 35/0、check-spells ALL、check-ui OK、check-spellbook-ui OK。最终实例 Player.log 0 ERROR/Config error/Verb/NRE/dictionary error，仅保留 1 条旧有简体中文翻译 WARN。
- a23 实机通过：十槽保存/副本隔离/读档顺序/非法组合原因；Self Heal、Touch Grow 相邻与 7 格拒绝、Projectile Harm 真 Pawn 0→17.92 伤、Projectile Heal 17.92→5.84、多效果链 ProjHarm+Heal 单次、AOE Grow 两株、Amplify/AOE/Accel/Decel/Split 数值、飞行存档改槽后按快照一次结算；旧四技能 4/4。主代理补测实际 Projectile+Explosion（日志 explosion=1）与 Split（live3/impact3），实时 frame 见 speed28.8/damage13.5；测试后重载 a23-final。交付档 `../lab-profile/Saves/a23-final.rws` 12,232,266B、tick12940、Paused、Lab Elf40081、activeSlot2(T03-ProjHarm)、窗口关闭、live0；隔离 PID56960。旧 a18–a21 与 `a22-final` 保留，维护 Missiles.xml 哈希未变。
- 未实测：真实鼠标点击 Gizmo/书页、中文像素级排版与多分辨率、原版护盾/拦截、特殊植物、多地图。没有提交、推送或发布。

### 2026-10-27：植物催熟技能接入与 a21 实机交付

- 用户要求把当前功能一起放进游戏查看，交付含三种飞弹与新催熟技能的演示档。本轮只做 Lab 接入与隔离验证：不改飞弹参数、不改视觉、不扩展符文/魔力系统。
- 功能侧（前序任务）：`Abilities/PlantGrowthEffect` 提供 `TryMature(Plant, out string failureReasonKey)` 与 `FailureReasonKey(Plant)`，只改原生 `Plant.Growth` 并 `MapMeshDirty(Position, MapMeshFlagDefOf.Things)`；`CompAbilityEffect_WotFPlantGrowth` 是薄适配，`CompProperties_WotFAbilityEffect` 为无行为公共基类，旧飞弹 props 仅换基类；Def `WotF_MaturePlant` 复用 PsycastBase 与现有图标，双语 label/description 与三条失败提示。
- 集成侧：`LabActions` 授予改为按共享基类发现（排序/去重/原 psylink 流程不变，无硬编码 Def 名单）；`LabCommands.cast` 对任意已授予的实验技能走真实 `Ability.Activate`，只有飞弹 Def 才附带既有 settings，`fire` 仍飞弹专用；新增有界 `plants` 命令（最多 16 株列表、单目标状态、`confirm=1` 在已登记矩形造一株 fixture），只读 growth。
- 构建与检查：隔离重建与部署件同为 127,488 字节、SHA256 `a1a877c60e18fabc678b5b888521a742740940caa666d817df4bd0f0d2372b5`，0 warning/0 error；`verify.ps1` 31 PASS、`check-defs.ps1` 21 引用 0 missing；未重跑旧 52 项 harness、UI 与移动场景。
- 现场与重启：旧现场先存 `a21-before`（13,301,179 字节），旧会话（14:53 启动、只含旧 Def）与 a20 日志归档到 `../evidence/a21-prelaunch/`；安全退出旧隔离实例后部署新 DLL，再用同一组参数（`-savedatafolder` + `-wotflab` + `-quicktest`，无 `-mod`）启动 PID33332，`status` 显示 11 个特性 Def 全 true。
- 实机（quicktest 地图，真实 Ability 入口）：作物 45297 与橡树 45298 均从 `growth 0.15/Growing` 变为 `1/Mature`、`harvestableNow=true`，thingId 与 HP（85/200）不变；两次植物施法后 `launched=0/impacts=0`；对门与已成熟植物的施法不改变任何状态；第二次 `grant` 后技能仍为四项；旧三发飞弹仍正常 `launched=3/impacts=3`；`save/load a21-final` 后成熟状态与四项技能保留，存档内两株为 `<growth>1</growth>`、`<age>2000</age>`（未强改年龄）。最终 `a21-final.rws` 17,446,723 字节，tick 5019、Paused、选中 Elf45296、窗口关闭、live=0，`a21-player.log` 0 条 ERROR/Exception。
- 未实测项：玩家点击选植物与 targetParams 高亮、中文 UI 文本显示、`Valid` 返回 false 的点击路径（仅编译与反编译调用链证据）、特殊植物、实际收获动作。旧 a18/a19/a20 存档与证据保留，本轮不提交、推送或发布。
- 流程：原集成 worker 中断后由主代理报维护工单 `sp_0430e3b38e5b40aebebb57aeba40dd3a`；本轮由 worker-2 承接同一集成任务，只补完验证与文档，未重做功能、未另起委派。

### 2026-10-27：原生射弹迁移交付

- 用户确认主要复用 `Verse.Projectile` 的对象管理，已有追踪和粒子逻辑继续使用。先建立 `../Graphify/` 本地图谱，再用 DeLM 共享队列交给 worker。主代理只负责说明、监督与最终验收；流程故障先向维护窗口报告，不绕过调度自行实现。
- 方案：新增原生射弹子类承载现有三种飞弹，由游戏管理生成、更新、销毁及存档。保留既有转向、几何碰撞、XML 预设、命中 worker 和视觉后端，不改全局 Projectile 基类。
- 保留 a18/a19 文件，新验收使用 a20；只验证本次迁移的连接处。原生 API 已核实：ExactPosition、ExactRotation、Launch、TickInterval、Impact 和 ExposeData 可重写；默认飞行依赖固定 origin/destination 和 ticksToImpact，沿途拦截方法为 private，需要按实际接口处理。
- 主代理核对最终 DLL 与部署一致：122,880 字节，SHA256 `c08464fdfb2a51bf77951623d0a7c35b33ff91d18b2bc9df86c45876f51eb0c1`。原组件已取消模拟列表；三个技能的真实 cast、原生对象注册、命中计数和新格式加载有本轮证据。主代理独立检查为 29 PASS、19 引用无缺失；a20-player.log 无 ERROR/Exception。
- 首轮图谱早于迁移，且飞行存档 age=0、命中目标静止。主代理通过同一 DeLM 队列补充图谱刷新与一个移动追踪/已推进存档场景，没有自行修改实现或接管实机。验收细节见 acceptance.md A26–A29。
- 工作流故障已优先通过 peer_special 送达维护窗口，工单 `sp_cf14b3c00d624c499fd375f6bf8f4b5e`。诊断排除无效依赖和持久化丢失，确认租约到期无续租、非 owner DONE 静默接受及重复完成通知。维护方报告修复已推送至 `7441514a`；completed 后追加任务不唤醒的问题又经工单 `sp_b2e792f2952d44c8a8a1591991474400` 修复至 `d89e85d0`。主代理使用新 delm_reopen 恢复原 run 的两项补验，返回 active / 2 open / 0 claimed，未绕过流程或重做已完成任务。
- 维护方独立处理用户的进度条要求：三个技能的 `showCastingProgressBar` 均已改为 false。主代理仅核对 XML；此项待游戏重载 Def 才生效，DLL 哈希不受影响，当前隔离实例的实测不覆盖该显示变更。
- 两项补验完成并经主代理核对：图谱刷新后为 794 节点/2038 边，35 个模组 C# 哈希一致；移动目标真实位移、飞弹航向跟随，age152 的原生射弹保存后按 age153 恢复，随后命中一次。原始命令和事件分别为 `a20-moving-results.jsonl`、`a20-moving-experiment.jsonl`，详细证据见 acceptance.md A30–A33。
- 最终试玩档为 `../lab-profile/Saves/a20-final.rws`（12,756,887 字节，未被补验覆盖），补验后加载并暂停在 tick29060，选中 Elf37175、窗口关闭、入口可见、无 live 弹；隔离实例 PID13364。原 a18/a19 文件保留。没有修改已交付 DLL，部署哈希仍一致；本轮不提交、推送或发布。
- 图谱只覆盖当前模组 C# 与五个原生类，反编译材料留在模组仓库外。护盾/拦截链未接入、旧飞行中对象不转换、多地图及目标失效实机未重做；粒子外观和画面残留没有自动验收。
- 补验完成通知再次重复送达，已把新的两条通知证据通过 peer_special 送达维护工单 `sp_b106e808d30c47d5b1a85034a275e5e1`。这是待维护核查的通知问题，不触发重复施工；原 run 最终为 5 done / 0 open / 0 claimed。

### 2026-10-25：开发与隔离准备

- 本地 .NET 10 SDK 已由集成负责人下载并核验官方 SHA-512：`Moddev/tools/dotnet/dotnet.exe --version` 为 `10.0.402`，runtime 为 `10.0.13`。未改全局 PATH 或注册表。`ref:ivory-cliff-pepper`
- 已创建目录联接：`RimWorld/Mods/Whisper-of-the-Forest` → `RimWorld/Moddev/Whisper-of-the-Forest`。游戏读取同一份源码产物，避免复制后测试旧 DLL。
- 首次启动前已确认没有正在运行的 `RimWorld` 进程。
- 已通过 `um backup create` 备份原配置与存档，均返回 0：
  - 配置：`C:/Users/81004/.universal-modder/backups/rimworld-wotf-config/20261025-173259.zip`，91 个文件。
  - 存档：`C:/Users/81004/.universal-modder/backups/rimworld-wotf-saves/20261025-173300.zip`，24 个文件。
  - 恢复入口：`um backup restore rimworld-wotf-config` / `um backup restore rimworld-wotf-saves`；本次实验只使用独立测试存档。
- Context7 未收录 RimUnity，返回的 GameJoy/GameWiki 不相关；使用 RimUnity 官方源码及本机 0.1.17 程序集。Universal Modder 的公开知识索引没有 RimWorld/Waylith 条目。
- 代码分工：`ref:ivory-cliff-pepper` 集成，`ref:golden-stone-blanket` 飞弹，`ref:ivory-brook-thistle` 移动，`ref:olive-forest-chisel` UI。

### 2026-10-25：两轮实机与修复

- 第一轮完成真实加载、精灵角色生成、授予灵能、三枚坐标飞弹发射和落点结算；装备日志显示正常步行与下一格瞬移交替。证据：`../evidence/first-run-player.log`、`../lab-run/results.jsonl`、`../lab-run/experiment.jsonl`。当轮不能证明粒子可见或完整移动边界。
- 第一轮暴露的装备 smeltable、实验 DefInjected 键、窗口组件构造、灵能链接缺脑部绑定、精灵姓名规则和近战工具定义错误已由各负责人修复。第二版 DLL SHA256：`a46fc918a263aeeda46c1eff1c5314cc352636e1ccf3bcdc880412f50efd9c38`；19 项构建校验通过，12 个类型/贴图引用无缺失。
- `-quicktest` 经真实 IL 与启动验证受支持；先前“无此参数”的判断已撤销。`-loadgame` 未发现解析；后续 Lab 提供有界 `load name=...`，通过游戏自己的存档加载流程排队执行。
- 第二轮：中文 quicktest，PID 19620，`r202` 返回 Harmony 已 patch、三项 Def 存在、地图已加载；游戏暂停在 tick 314。上述首轮加载错误没有复现，中文还有 1 条翻译警告，另行定位原精灵翻译文件。
- UI 实测：默认关窗；点击入口打开，关闭可重开；从桌面坐标 `(2093,1125)` 拖至 `(1940,960)` 未误开；点击入口小叉隐藏后，底部固定实验台按钮仍能打开窗口。设置文件记录 `showFloatingEntry=False`、`entryX=1599`、`entryY=746`。尚待重启确认。
- 第二轮缺陷：窗口绘制泄漏 `Text.WordWrap=false`，触发帧末断言；已用三个绘制入口的状态快照与恢复修正，临时编译通过。`setup confirm=1` 因排除全部植物导致找不到空地，已改为仅在明确确认的 15×5 实验矩形内清除 Plant/Filth，保留其他物体并拒绝占用。
- `Start-Process` 由短生命周期 PTY 启动时，报告 PID 但进程消失且无新日志；改用 WMI detached 后实际持续运行并进入地图。启动脚本已按此证据修正。
- 第二轮日志已保存 `../evidence/second-run-player.log`，PID 19620 已优雅退出并由 `WaitForExit` 确认。第三构建将整合窗口与场地修复，继续定量验收。

### 2026-10-25：第三实例与脚手架优先级

- 第三版 DLL 为 89,600 字节，SHA256 `4b7d656d561b34bf9168913e0ecd3d3f29b336da647392ec7720b0db8bac0107`；真实启动的中文日志未见先前错误。测试场地生成成功，精灵授予灵能正常。
- 同一精灵的一格移动对照：普通地面 25 tick、泥地 40 tick、穿地形装备泥地 25 tick、卸下后泥地 35 tick。以 5 tick 间隔采样，包含起步延迟；实际有效地形成本为 `14 → 0 → 14`。墙格仍不可达，普通门路径可达但 `NoClosedDoors` 路径不可达。
- 瞬移直线为 `101 → 103 → 105 → 107 → 108`，最后一格步行结束，再等待没有越过终点。中途卸下后瞬移计数保持不变；改变目的地并重新穿戴后正常抵达。详见 `Experiments/acceptance.md` 的 A09、A11、A13。
- 用户澄清波动只需发生在粒子表现上，追踪轨迹继续使用固定速度和转角上限。高速坐标目标的子步到达检查已修正并通过生产代码自检，尚待下一构建实机核对。
- 用户要求优先完成可指定粒子形式和命中事件的脚手架，并比较 RimUnity 与原生方式对人类开发者的成本。审计发现原命中 `switch` 和固定 RimUnity 视觉类型仍需解耦；新增 Def 选择的独立视觉与命中实现，两后端共用追踪、碰撞和存档。扩展约定写入实施计划。
- 继续工作时旧 PID 28480 已不存在，Lab 状态请求超时；先整合新构建再重新启动隔离实验。先前通过项保留，新增接口和未完成行为继续验证。
- 第三轮完整日志已保存为 `../evidence/third-run-player.log`。第 579 行在穿戴后报告 `TerrainStride_Thin` 缺少贴图，交移动负责人修复并核对其他体型；此前“未见错误”仅覆盖启动时的日志检查，不能代表完整运行无错误。日志尾部有 Waylith 退出时的原生分配警告，目前没有证据将其归因于本模组。

### 2026-10-25：脚手架集成与第四、第五实例

- 视觉和事件分别使用 Def 注册，两后端共用运动、命中与存档逻辑。生产代码 harness 31 项通过，含单次结算、异常结束、暂停重建和切图清理的引擎替身检查；这些检查不替代真实游戏。
- 第四版最终 DLL 为 113,664 字节，SHA256 `446990baf101288fd0f29691ffafdf2f1541f32a470eb6b52269f15da8f237f2`。构建及 24 项布局检查通过，15 个类型/贴图引用无缺失。
- 第四实例发现自定义 Def 根标签必须使用完整命名空间；修改 XML 后，第五实例 c130629988751 确认两视觉、三事件及原实验 Def 全部加载。此问题未被先前静态字段检查发现，已补入功能记录。
- 两件装备补齐 Male、Female、Thin、Hulk、Fat 的穿戴图，共 10 张现有占位图副本。Thin 地形装备在第四实例回归无缺图错误；其他体型仅核对资源完整性。窗口 WordWrap 修复也在第四实例回归通过。
- 第四实例实际完成播种一次、射击两次，证据 c130508717438 / c130527368891；日志保存在 `../evidence/fourth-run-player.log`，测试存档为隔离配置下的 `wotf-a17`。
- 第五实例 RimUnity + Damage 发射一次，观察到墙体 HP `195 → 188`、`impacts=1`、`live=0`（c131325009966）。原生 Damage 尚在飞行时实例退出，未算通过。2026-10-25 13:18:47 UTC 日志完成关闭流程，退出原因未知，不能据此断言模组崩溃。
- 用户要求减少视觉工具调用，粒子、拖尾和动画观感由用户验收。后续仅用状态、日志和存档做常规功能检查，取消自动录制；集成负责人独占隔离游戏与 Lab 队列，继续命中组合和读档验证。

### 2026-10-25：最终功能验收与试玩交付

- 两种视觉后端与三种命中事件共六组真实运行通过：直接伤害各扣墙体 7 HP，治疗各清除严重度 3 的伤口，坐标爆炸各使邻墙损失 4 HP。主代理核对了原始返回值与命中日志，选择及数值对照见 `Experiments/acceptance.md`、`Experiments/missiles.md`。
- 移动目标证据已排除“命中后才移动”的旧测试：c134655260706 发射后，c134656856429 显示弹仍存活、目标从 (158,137) 移至 (160,137)，随后在 (163,135) 命中一次。真实 Psycast 施法 c134826412825 产生三发、三次命中。
- 飞行存档 a18-flight 包含 visualDef、impactDef、目标、位置和速度。加载后 live=1，继续 tick 后 live=0、impacts=1；窗口打开后保存/加载 a18-ui，窗口关闭而入口设置保留。
- 原 U 形夹具门后紧贴墙，不能证明穿门规则。专用 a18-door 从已备份的实验存档建立，仅移除 Wall37167。首次编辑产生 XML 解析错误，已从备份重建并校验后加载。Terrain 与 Blink 均等待原版开门；Blink 在门前/门格记录 door-cell / leaving-door-cell，走出门后才恢复瞬移。墙外中途位置采样证明沿外侧绕行。
- 当前源码隔离重建与交付 DLL 比较 349 个方法，IL 无差异。最终 DLL 未覆盖，SHA256 仍为 `446990baf101288fd0f29691ffafdf2f1541f32a470eb6b52269f15da8f237f2`。收尾仅修正文档与 XML 空白，没有改变运行行为。
- 原始验收数据已归档为 `../evidence/a18-results.jsonl`、`a18-experiment.jsonl`、`a18-acceptance-player.log`；主代理检查最终日志未发现 ERROR、Exception 或 WordWrap。第五实例独立 Player.log 没有成功归档且已被覆盖，不重造该记录。
- 最终试玩存档为 `../lab-profile/Saves/a18-final.rws`（12,783,682 字节，2026-10-25 14:00:19 UTC）。交付核对时 PID 62836 运行，游戏暂停在 tick 19871，选中 Lab Elf 37175，位置 (163,136)，已授予奥术飞弹并穿戴 BlinkStride；窗口关闭、入口显示。
- 两种粒子方案的接入、调试、迭代和人类开发成本报告位于 `Experiments/environment.md`。真实多地图/地图移除/目标丢失场景只做代码与 harness 检查；Unity Editor/exporter 未实际运行。粒子和装备外观交用户检查，音效精修后置。未提交、推送或发布。

### 2026-10-26：两个预设与入口修正交付

- 保留原三发奥术飞弹，增加单发 Heavy（13 格/秒、10°/秒、爆炸）和 Mender（4 格/秒、720°/秒、治疗）。两者跳过旧飞行限制，治疗弹目标失效时无治疗清理。参数与既有命中策略只在 `1.7/Defs/Experiments/Missiles.xml` 配置；授予入口自动发现带飞弹 comp 的 AbilityDef，新增预设无需补 C# 名单。
- 20 格布置下，健康 Human 正常时间持续横移后未受伤；Heavy 以 outOfBounds 清理且 impact=0。Mender 实际追踪 12 秒、飞行中保存加载后命中一次。真实 Heavy Ability 施放造成一次 +7 伤害。证据与限制见 `Experiments/acceptance.md` A20–A25。
- 删除错误的底部 MainButton 入口，改接右侧原生 PlaySettings 小图标区，保留悬浮拖动和隐藏。主代理只读截图确认位置，Lab 验证隐藏后的同一恢复回调及读档默认关窗。
- 实机发现并修复 Harmony 参数名绑定错误；最终 DLL 的现有检查已覆盖参数名和类型。另以一行 `<soundExplosion>Explosion_Bomb</soundExplosion>` 修复爆炸的 null SoundDef，重启隔离实例后真实施法复验通过，新日志无 ERROR/Exception/null SoundDef。
- 最终 DLL 为 121,344 字节，SHA256 `ce991a866e9160f69f0cd7d72c522ceb9d4e01b55c75a60d372acc017cce3074`。构建检查 29 项通过，17 个引用无缺失，生产代码 harness 46 项通过；旧功能不重复全量验收。
- 当前试玩档为 `../lab-profile/Saves/a19-final.rws`（12,760,089 字节）。核对时 PID 25772 暂停于 tick 24937，选中三技能 Lab Elf，窗口关闭、入口显示、无飞弹。原始证据为 `../evidence/a19-final-*` 和 `a19-soundfix-*`；旧 a18 存档及失败现场保留。目标失效实机、Mender 单独 cast、鼠标点击新入口和声音听感未验证；未提交、推送或发布。

### 2026-10-30：a24 魔术书交互与命中触发交付

- 魔术书改为点击书签即切换当前槽，施法按钮同步；去掉草稿与确认弹窗；新增“触发”分类，不可追加的符文半透明并给出红字原因。实机 `SelectBookmark` 槽 2→3、gizmo 同步；空槽禁用。
- 新增 `WotF_Trigger_OnHit`：法术编译为 `SpellCastPlan.segments`，飞弹携带 `spellPlan` 与 `spellSegmentIndex` 并随存档保存；命中后由 `SpellTriggerService` 在目标远侧生成下一段。`[射弹][伤害][触发][射弹][爆炸]` 第一段造成 +9 伤口，第二段在目标东侧生成并只爆炸一次；出图不触发；两段飞行存档读档各只结算一次。
- 最终 DLL 199,168 B，SHA256 `F3F4924161C780A8A2B641BE7973C748CEB28FE8DED5A6C4028E0EDB104CB941`；`a24-final.rws` 12,228,269 B，tick17596，Paused，activeSlot 9，`live=0`；日志 0 ERROR/Exception；a18–a23 与 `Missiles.xml` 未改。未提交、推送或发布。
