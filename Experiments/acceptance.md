# 实机验收场景

运行场景的“通过”必须来自真实游戏结果；扩展接口另作代码检查。编译、自检、游戏状态和外观观察分别记录。按用户最新要求，粒子、拖尾和动画观感交用户验收，自动检查使用状态、日志和存档，不追加常规截图或录屏。

| 编号 | 场景与操作 | 预期 | 证据与结果 |
| --- | --- | --- | --- |
| A01 | 独立模组列表启动到主菜单并创建测试地图 | 无本模组 Def/类型/资源异常，原精灵 Def 可加载 | 通过：c132715872208 显示 Harmony 已 patch、8 个运行时状态表中的实验 Def 存在（静态验证另含 UI，共 9 个）。最终归档 a18-acceptance-player.log 无 ERROR/Exception/WordWrap；自定义 Def 标签和 Thin 穿戴缺图已修复。其他体型仅做资源静态检查 |
| A02 | 新游戏与加载测试存档 | 实验窗口默认关闭；入口状态按设置恢复 | 通过：新实例 c130657422448 为 windowOpen=false；打开窗口后保存/加载 a18-ui，c133302408151 再次为 false，showFloatingEntry=true 和自定义位置保留。入口外观由用户验收 |
| A03 | 授予技能并通过原版 Ability/Job 链施法 | 正常完成施法和三弹发射 | 通过：grant c134825861990、cast c134826412825、step c134826937223；experiment 记录 launched=3/3 和三条命中。该版未追加鼠标点击 Gizmo/瞄准外观检查 |
| A04 | 目标在飞弹飞行途中沿路线移动 | 状态证明追踪移动目标并结算；转弯与拖尾观感由用户验收 | 通过：c134655260706 发射时目标在 (158,137)，c134656856429 弹仍 live=1、目标已到 (160,137)；最终命中 Lab Human 于 (163,135)，c134711018745 为 live=0/impacts=1/expired=0。旧的“命中后目标移动”记录不作为证据 |
| A05 | 对地图坐标发射 | 飞到目标区域后结算一次并销毁 | 通过：c132841864453 / c132843351296 向 (153,135) 发射，两后端均记录一次 ground impact，并使邻墙受爆炸伤害 |
| A06 | 高速飞弹命中墙体 | 子步检测能捕获墙体命中 | 通过：speed=60 的 c132933398534 使墙 HP 173→166；c132934365873 为 live=0/impacts=1/expired=0。途中墙优先于到达的顺序另由生产代码 harness 覆盖 |
| A07 | 伤害、治疗和爆炸命中策略 | 分别造成伤害、减少真实伤口、调用范围爆炸；不重复结算 | 通过：两后端 Damage 各扣墙 7 HP；Heal 各使 Torso 伤口 3→0；Explode 各使邻墙损失 4 HP。命中日志每组一次，完整 ID 对照见 missiles.md 第 8 节及 integration.md 第 9 节 |
| A08 | 飞行中保存加载，目标消失，地图切换 | 状态可恢复或明确终止；生命周期有明确所有者 | 飞行中保存/加载实机通过：a18-flight 保存两 Def、位置、目标和速度；c133216759828 恢复 live=1，c133225876465 为 live=0/impacts=1。目标丢失、切图、MapRemoved 仅有生产代码 harness/IL 证据，未做真实多地图场景；外观重建由用户检查 |
| A09 | 同一小人在普通地面/泥地分别走相同格数 | 装备后忽略 terrain 额外成本；脱下恢复地形差异 | 第三实例通过：同一精灵一步，普通地面 25 tick、泥地 40 tick、穿装备泥地 25 tick、卸下泥地 35 tick；5 tick 间隔采样，含起步延迟，不作为精确最短耗时。结果 c105318749755/c105323783376/c105327608361/c105332123585，泥地成本 14→0→14 |
| A10 | 地形装备遇墙、关闭门及其他障碍 | 保留合法路径和原阻挡规则 | 通过：墙格不可达，闭门 NoClosedDoors 路径不可达；专用 a18-door 中，c135857403199 起经 (159,137)→160→161→162→163，门由 closed 变 open（c135859099440），瞬移计数保持不变。仅调整隔离夹具，没有改生产装备逻辑 |
| A11 | 瞬移装备沿直线及转角走到终点 | 交替正常步行与下一格瞬移，不越过终点 | 第三实例直线与重新下令转向通过：101→103→105→107→108，3 次瞬移，终点再等 40 tick 未越过；结果 c105516145390。转向 103,113 后正常结束，结果 c105602168482；非法斜角另见 A12 |
| A12 | 瞬移路线含关闭门和墙角 | 不越过障碍或切墙角；保留原版开门过程 | 实机门与绕墙通过：c135924507449 在门前记录 door-cell、blink=1；c135928330408 在门格记录 leaving-door-cell、blink仍1；c135929848779 已走出门，c135937736442 离门后才增至2。门 c135926344810 已打开。墙外采样 c134948813348 / c134949848373 / c134950857511 为 (161,135)→(163,135)→(163,136)，未穿墙。非法斜角拒绝规则经源码核对，未单独注入非法路径节点 |
| A13 | 行进中卸下装备、取消路线后重新下令 | 不残留瞬移或错位路径状态 | 第三实例通过：103,115 处卸装后继续 100 tick，瞬移计数保持 4；再改变目的地并装备，抵达 103,113，无挂起。结果 c105559846216/c105600651694/c105602168482 |
| A14 | 打开/关闭窗口、拖动悬浮按钮 | 点击打开，拖动不误开，关闭后可再次打开 | 通过：第二实例点击/关闭/拖动；WordWrap 泄漏已修，第四实例打开窗口后换行与原版界面正常，日志未再出现 WordWrap 断言 |
| A15 | 右下角开关隐藏入口、固定入口恢复窗口 | 隐藏设置有效；不会把窗口入口永久锁死 | a18 历史验证通过：入口小叉隐藏后，旧底部固定按钮仍可打开实验台。该底栏入口现已按用户标注移除，新原生行入口见 A24 |
| A16 | 保存入口位置/可见性后重启 | 设置与位置保留，窗口仍默认关闭，坐标在屏内 | 设置持久化通过：False/1599/746 在第三至第五实例保留，c130657422448 返回 custom=true、windowOpen=false。恢复显示后的外观和位置交用户验收 |
| A17 | 实验小人种地、移动、开枪 | 能在有界日志观察实际行为，普通角色不受实验替换影响 | 通过：第四实例 c130447010859 下达 Sow，c130508717438 记录 sowStarts=1、sowCompletions=1 及真实土豆植株；c130527368891 记录真实 Projectile.Launch 产生 shots=2。移动证据见 A09/A11。观测仅登记实验角色，不替换普通角色行为 |
| A18 | 两种视觉 Def 与三种命中事件 Def 交叉选择 | 只换配置即可共用飞行/碰撞/存档；各事件只结算一次 | 六组功能通过：两种 visualDef 与三种 impactDef 均由同一 Fire 入口运行，实际状态与命中结果见 A07；选择 Def 随弹保存见 A08。外观不在自动验收范围 |
| A19 | 新增独立视觉或命中实现的接入检查 | 不需修改追踪、碰撞、保存和清理核心；文档列出必需实现 | 结构检查通过：两视觉和三 worker 通过 Def 注册；核心持有抽象生命周期/事件接口，两个选择字段使用 Scribe_Defs。生产代码 harness 31 项通过；接入步骤见 missiles.md 第 2 节。实际组合与存档验收分别见 A18/A08 |

补充检查只针对实际失败或新改动。每项若未运行必须保留“未运行”，不得以实现存在替换结果。

最终证据位于 `../evidence/a18-results.jsonl`、`a18-experiment.jsonl`、`a18-acceptance-player.log`。主代理已核对上述关键结果、飞行存档 XML、移动中目标状态及门口采样。第五实例的独立 Player.log 未成功保留，已被后续启动覆盖；该轮不作为最终日志无错的唯一依据。

实际发生并修复的失败包括 Def 类型标签、装备贴图、窗口状态泄漏，以及临时穿门夹具编辑造成的 XML 解析错误；最后一项从备份重建后成功加载，没有修改生产逻辑。当前保留的未实测范围为真实多地图/地图移除/目标丢失场景、Unity Editor/exporter 工作流，以及由用户负责的外观检查。

## a19 增量验收（2026-10-26）

| 编号 | 场景 | 证据与结果 |
| --- | --- | --- |
| A20 | 两个 XML 预设及自动授予 | 最终 DLL 121,344 字节，SHA256 `ce991a866e9160f69f0cd7d72c522ceb9d4e01b55c75a60d372acc017cce3074`，与部署一致。源码从 AbilityDef 的飞弹 comp 自动发现预设；重复 grant c180307587244 后仍为三个飞弹技能。构建检查 29 PASS、0 WARN、0 FAIL；17 个引用无缺失；生产代码 harness 46 PASS、0 FAIL |
| A21 | 20 格布置，健康 Human 持续横移 | Human 840：MoveSpeed=4.6、无伤、土壤成本 2，发射前已开始 Goto。正常时间下 DrawPos 持续变化；tick 19905 发射 13/10 Heavy，tick 20004 日志为 outOfBounds。a17592095420：live=0、impact=0、expired=1、伤口=0。采样最小间距 2.647 格，只代表请求边界采样；未测精确反应延迟 |
| A22 | Heavy 真实 Ability 命中及爆炸声修复 | cast c181519081870 读取 Heavy 的单发、13/10、Explode 设置；tick 24729 命中一次，实验 Human 伤势 19.696→26.696（+7），等待后计数不变。DamageDef 配置 Explosion_Bomb；新 a19-soundfix-player.log 无 ERROR/Exception/null SoundDef。未做声音听感判断 |
| A23 | Mender 追踪、治疗与飞行存档 | 4/720 Heal 飞行 720 tick（12 秒）后命中一次；另一次飞行中方向变化且持续存活，a19-mender-flight 保存两 flag 和 Heal Def。c180144103268 读档后 live=1；tick 24610 命中，c180154581195 为 live=0、impact=1，伤势 29.768→19.696，等待后不重复。伤势差含自然愈合；治疗上限 10 另由代码和 harness 验证 |
| A24 | 用户标注的原生小图标区 | 最终 DLL 的 check-ui 通过，Harmony 参数名与类型匹配；harmonyPatched=true、原生行绘制计数增长。c175848741995 隐藏后，c175849320529 调用真实恢复回调，窗口打开且入口恢复；加载 a19-ui 后窗口默认关闭。主代理一次只读截图确认位置在右侧原生小图标区、底栏旧入口消失。未追加鼠标点击或 tooltip 实测 |
| A25 | 最终试玩状态 | a19-final.rws 为 12,760,089 字节；c181611256186：tick 24937、Paused、十个观测 Def 均加载。选中已授予三技能的 Elf 37175，live=0；c181603975660：窗口关闭、悬浮入口可见。核对时隔离 PID 25772 运行，旧 a18-final 保留 |

主代理核对了 `../evidence/a19-final-{results,experiment}.jsonl`、`a19-soundfix-{results,experiment}.jsonl`、`a19-soundfix-player.log` 和飞行存档。首次 Harmony 失败及修复前的 null SoundDef 日志保留，不能将旧 `a19-final-player.log` 标成无错日志。

暂停单步时 DrawPos 冻结的试验不计入规避结论。目标死亡、消失或离图仅有生产代码 harness 证据；Mender 的新预设 `cast` 入口未单独实测，运行结果来自同一 Def 的 `fire`。这些项目按已确认的精简范围保留为未实测项，不新增测试设施。

## a20 原生射弹迁移验收（2026-10-27）

| 编号 | 场景 | 主代理核对的证据与结果 |
| --- | --- | --- |
| A26 | 射弹所有权 | `Projectile_ArcaneMissiles : Projectile` 自身保存状态，原生 TickInterval 调用既有运动逻辑；Launcher 用 ThingMaker/GenSpawn，MapComponent 无模拟列表、不驱动飞行或逐弹视觉。原版直线推进和概率命中未启用，保留既有几何碰撞 |
| A27 | 最终产物及检查 | DLL 122,880 字节，SHA256 `c08464fdfb2a51bf77951623d0a7c35b33ff91d18b2bc9df86c45876f51eb0c1`，部署副本一致。主代理独立运行 verify：29 PASS / 0 WARN / 0 FAIL；check-defs：19 引用 / 0 missing。worker 的 52 项 harness 对应同哈希的隔离构建，未机械重跑 |
| A28 | 三种真实 Ability | `c065432664213` 施放 Mender；`c065433626743` 中实体 126852 为 WotF_ArcaneMissileProjectile，随后 live=0、impact=1。`c065458589351` 原三发产生三个实体且 impact=3；`c065513178980` Heavy 为 13/10 Explode，随后 impact=1。伤势前后和时间边界见 integration.md §11；伤势总差含自然愈合，不当作精确载荷量 |
| A29 | 首次原生存档及试玩状态 | a20-flight.rws 包含原生 Thing、arcaneMissile 状态和两 flag，恢复后命中一次；该次保存 age=0，仅证明发射后对象可加载，已推进飞行续存见 A32。a20-final.rws 为 12,756,887 字节，XML tick=29059、原生飞弹数=0。旧 a19-final 文件大小和时间保持不变 |
| A30 | 当前代码图谱 | `../Graphify/graphify-out/metadata.json` 时间为 2026-10-27T07:35:02.2249724Z，35 个模组源码文件与 5 个原生类共 794 节点、2038 边。主代理逐文件哈希核对无差异；local 查询命中 Projectile_ArcaneMissiles:20，图内 EXTRACTED inherits 边指向原生 Projectile。只覆盖所列源码，不包含全游戏和 XML |
| A31 | 原生弹追踪移动目标 | `c073523155123` → `c073524559636` 显示 Human 的真实 DrawPos 移动约 4.23 格，moving=true。Mender 正常时间下 age 61→313 持续 live=1，target 坐标连续变化，heading 从 (0.124,-0.992) 转至 (-0.572,-0.82)；关键帧 `c073613537658`、`c073614730435`、`c073615655944` 已核对，不使用旧 a19 证据替代 |
| A32 | 已推进状态保存与续飞 | `a20-moving-flight.rws` 13,497,066 字节，原生实体146887，age=152、pos(232.710632,229.878464)，目标37171，Heal/4/720/两 flag 保留。`c073730994927` 读档 age=153、位置约推进一 tick；`c073737058377` 为 live=0、impact=1、expired=0。目标伤口0.4→0，治疗上限被小伤口截断，不将总差值当成精确10点验证 |
| A33 | 补验后恢复与显示边界 | `c073748075200` 重新加载未覆盖的 a20-final；最终 `c073808175495` 为 tick29060、Paused、selectedId37175，`c073808639348` 为 live=0；`c073809112432` 窗口关闭、悬浮入口可见。隔离 PID13364，a18/a19保留。维护方关闭施法进度条的 XML 尚未在该实例重载，不计为已生效 |

首轮原始结果为 `../evidence/a20-results.jsonl` 与 `a20-experiment.jsonl`，其中包含先前累计记录；本轮命令以 `c065...` 标识，事件以 2026-10-27 UTC 时间为边界。补验单独归档为 `a20-moving-results.jsonl`（73条）与 `a20-moving-experiment.jsonl`（31条，最早 2026-10-27T07:35:11 UTC）。主代理核对原始结果、保存实体、源码哈希及最终状态，`a20-player.log` 与补验后的 Player.log 均未检出 ERROR/Exception。粒子仅有生命周期代码、RimUnity设置路径执行及无报错证据，未通过画面检查粒子或残留，观感由用户判断。护盾/拦截链没有接入，旧飞行中格式不转换，多地图及目标失效未在本轮实机重做。

## a21 植物催熟验收（2026-10-27）

| 编号 | 场景 | 主代理核对的证据与结果 |
| --- | --- | --- |
| A34 | 效果与触发分离 | 催熟效果是 `PlantGrowthEffect.TryMature(Plant, out string)` 与 `FailureReasonKey(Plant)`，只依赖 `Verse`/`RimWorld`，不依赖 Ability、飞弹或 Lab；技能侧只有 `CompAbilityEffect_WotFPlantGrowth` 这个薄适配（`Valid`/`CanApplyOn`/`Apply`）。任何持有已生成 `Plant` 的触发源都能直接调用，无需假命中 context |
| A35 | 授予发现与去重 | 授予改为按共享基类 `CompProperties_WotFAbilityEffect` 发现，排序与去重逻辑不变。`c1a21grant` 后 abilities 为 Mender/ArcaneMissiles/Heavy/MaturePlant 四项；`c2a21grant2` 再授予一次后 `c2a21obs2` 仍为四项，未重复、未丢失旧三技能。没有硬编码 WotF_MaturePlant 名单 |
| A36 | 未成熟作物催熟 | `c1a21fix1` 的 `Plant_Potato` 45297 为 `growth 0.15/Growing/hp85/harvestableNow=false`；`c1a21cast1` 真实 `Ability.Activate` 后 `c1a21after1` 为 `growth 1/Mature/harvestableNow=true/hp85`，thingId、def、位置不变 |
| A37 | 未成熟树催熟 | `c1a21fix2` 的 `Plant_TreeOak` 45298 同为 `0.15→1`，`isTree=true`、`Mature`、`harvestableNow=true`、`hp200` 不变（`c1a21cast2`、`c1a21after2`）。两项都只改 growth，未改 Age、HP、病害或堆叠 |
| A38 | 拒绝与不变式 | 对门 45292 施法（`c1a21door`/`c1a21castdoor`）与对已成熟 45297 再施法（`c1a21castmature`/`c1a21after3`）都返回 ok 但状态不变；门报告 `isPlant=false`，已成熟植物报告 `failureReasonKey=WotF_MaturePlant_Failed_AlreadyMature`。直调 Activate 的 ok 不代表通过了玩家的目标校验。两次植物施法后 `launched=0/impacts=0/live=0`（`c1a21miss1`、`c1a21miss2`），没有生成飞弹或多余结算 |
| A39 | 存档保留成熟状态 | `c2a21save1` 存 a21-final，`c2a21load1` 读档后 `c2a21p5`/`c2a21p6` 仍为 `growth 1/Mature/harvestableNow=true`、hp 不变，`c2a21obs3` 仍四项技能；`a21-final.rws` 内两株原生字段为 `<health>85/200</health>`、`<growth>1</growth>`、`<age>2000</age>`，Elf45296 在同一存档 |
| A40 | 构建、检查与最终态 | 隔离重建与部署件同为 127,488 字节、SHA256 `a1a877c60e18fabc678b5b888521a742740940caa666d817df4bd0f0d2372b5`，0 warning/0 error；`verify.ps1` 31 PASS/0 WARN/0 FAIL，`check-defs.ps1` 21 引用/0 missing。最终 `c2a21st5` 为 tick 5019、Paused、selectedId 45296、四项技能，`c2a21ui4` 窗口关闭、入口可见，`c2a21miss-d` live=0；旧飞弹一次真实 cast 结算 `launched=3/impacts=3`（`c1a21castmiss`、`c2a21miss-a`）说明旧链路未受影响 |

原始记录为 `../evidence/a21-results.jsonl`、`a21-experiment.jsonl` 与 `a21-player.log`（0 条 ERROR/Exception），旧会话与 a20 日志在 `../evidence/a21-prelaunch/`。存档：`a21-before.rws` 13,301,179 字节（旧 DLL/旧 Def 现场），`a21-final.rws` 17,446,723 字节（新 DLL quicktest 地图）。未实测项：玩家点击选植物与 `targetParams` 高亮、中文 description/错误提示显示、`Valid` 返回 false 的点击路径（仅代码与编译证据）、特殊植物、实际收获动作，以及旧飞弹/UI/移动的既有场景未重做。隔离实例 PID33332（无 `-mod`，`-savedatafolder`/`-wotflab` 与 a20 相同）。

主代理最终核对：独立运行 verify 为 31 PASS/0 WARN/0 FAIL、check-defs 为 21 引用/0 missing，git diff --check 无空白错误；部署通过 Mods 目录联接读取同一 DLL，产物哈希与上述一致，维护 Missiles.xml 哈希仍为 `650B6B5959C54AE9E7CC95188E75E3D53BD5F29DAD13752C23747032C33C8549`。核对原始 JSON 与存档 XML 中两株的 growth=1、HP=85/200，以及 Elf45296 的四个技能。只读截取窗口 HWND 1508022，确认游戏暂停、Lab Elf 已选中、三个飞弹 Gizmo 与中文“瞬间催熟植物”Gizmo 同时显示；这不等于鼠标施法、目标高亮或成熟贴图对比验收。未重跑 worker 已完成的实机场景。

本节的「拒绝」结论按证据分级：运行期只证实直调 `Activate` 后状态不变（原版行为），`CompAbilityEffect_WotFPlantGrowth.Valid` 阻止玩家点击的那条路径由本机程序集编译检查与 `Verb_CastAbility.ValidateTarget` 反编译调用链支撑，未做鼠标实测。

## a22 魔术书验收清单（最终 a23 验收）

本轮按用户的 auto drive 请求验收：研究新生魔艺的组成与 RimWorld 对应机制，提供能自由组装、保存并交给有施法能力小人使用的魔术书。以下项目以 a23 最终构建和真实游戏结果为准，不以队列完成、编译通过或存在某个方法替代功能结果。

| 编号 | 用户要求对应的交付 | 验收所需证据 | 当前结果 |
| --- | --- | --- | --- |
| A41 | 分析原作组成并寻找 RimWorld 对应 | `spellbook.md` 的来源与映射；分清本体和附属模组、已实现与仅有接口可能性 | 通过：§6 已按 229 条中 87 个 `ars_nouveau:` 本体条目、14 个附属 namespace 分类，给 Form/Effect/Augment 映射、真实 API 与未确认项 |
| A42 | 类似魔术书的游戏内页面 | 原生 Window 与实际书页画面；符文目录、说明、配方区、十个书签、可读中文和明确入口 | 通过：原生双页 Window；主代理只读窗口截图确认左页符文目录/分类、右页编排/保存、十个书签、中文按钮；书页 open/close 状态由真实回调确认 |
| A43 | 自由组装受支持的法术 | 添加、删除、调序与命名；一个 Form 开头、多个 Effect 有序、Augment 只修改左侧最近的兼容项；非法组合显示原因 | 通过：十槽保存/读取；缺 Form、两个 Form、Amplify 绑 Grow、射弹增幅错位均拒绝并显示原因；有效链按顺序执行 |
| A44 | 保存多个法术并读档保留 | 十槽保存、改名、切换；草稿不污染已存内容；保存再加载后名称、顺序与小人选中槽位一致 | 通过：10 槽保存、副本隔离、读档名称/顺序逐字一致；`a23-final` 的 activeSlot=2；草稿切换有覆盖确认 |
| A45 | 有能力的小人能施放 | 有灵能链接小人的原生入口与玩家目标选择；无能力、距离、阻挡、状态、冷却拒绝；说明哪些只测回调、哪些真经玩家输入 | 通过：psylink0 的 can/cast 拒绝且原因可见，grant 后自动获得 `WotF_SpellbookCast`；原生 Command_Target、书页 open/close 和 Self/Touch/Projectile 施放链实测。真实鼠标点击未单独测，见边界 |
| A46 | 三种形式与四种效果可实际组合 | 少量场景覆盖 Self、Touch、Projectile 与 Harm、Heal、Explosion、Grow，至少一条多效果链；前后真实 HP、伤口、growth 与计数 | 通过：Self Heal 2.0→0；Touch Grow 0.266→1、Mature；Projectile Harm 0→Crack9+Bruise8.92；Projectile Heal 17.92→5.84；ProjHarm+Heal 余 6.0；AOE Grow 两株 0.15→1.0；Projectile+Explosion 日志 `impact ... explosion=1`（主代理在最终实例补测） |
| A47 | 增幅真实改变法术 | Amplify、AOE、Accelerate、Decelerate、Split 的实际数值、范围或弹数；范围效果去重，爆炸只生成一次 | 通过：Harm 9→13.5、速度16→28.8/8.8、Split1→3、AOE半径1.9→3.1、Explosion12→18且只一次；AOE Grow 两株各一次。主代理另在最终实例的实时 frame 看到 `speed=28.8/damage=13.5`，Split 实发 `live=3/impact=3`，Projectile+Explosion 日志 `explosion=1`；测试后重载 a23-final |
| A48 | 保存法术可稳定交给射弹执行 | 已推进飞行存档；发射后改槽不改载荷；加载后按发射快照执行一次，无重复命中 | 通过：飞行中保存/改槽/load 后 live=1、age 延续、一次 impact，日志按发射快照；`a23-flight.rws` 留档 |
| A49 | 交付可试玩结果且保留既有成果 | 最终 DLL 与部署哈希一致、相关检查通过、a23-final 暂停/live=0/书中有样例；旧四技能、a18–a21 与维护 XML 不受覆盖 | 通过：DLL 185,856B / `353773d6…`；a23-final 12,232,266B / `a3d0ecb1…`，tick12940、Paused、选中 Elf40081、activeSlot2、窗口关闭、live0；a18–a21 哈希未变，Missiles.xml `650B6B59…` |
| A50 | 配置式运行与旧功能回归 | 构建检查、旧四技能、日志和实际入口 | 通过：verify 47/0、check-defs 35/0、check-spells ALL、check-ui OK、spellbook UI OK；旧四技能 4/4 生效 |

最终集成任务独占部署 DLL、游戏实例与 Lab 队列。主代理独立运行 `verify`、`check-defs`、`check-ui`、`check-spells`、`check-spellbook-ui`，核对 DLL/存档/Missiles 哈希与 a18–a21 未覆盖；并用只读窗口截图确认书页实际显示。未覆盖边界：真实鼠标点击 Gizmo/书页、中文像素级排版与多分辨率、原版护盾/拦截、特殊植物、多地图；仅 1 条本轮之前的简体中文翻译 WARN 保留。

## a24 魔术书交互与命中触发验收（2026-10-30）

| 编号 | 场景 | 主代理核对的结果 |
| --- | --- | --- |
| A51 | 书签同步 | `select slot=3` 后 activeSlot 2→3，gizmo T03-ProjHarm→T04-ProjHeal；`select slot=0` 后 T01-SelfHeal；空槽 9 禁用并给出原因 |
| A52 | 增幅兼容 | `[Projectile][Harm][AOE]` 与 `[Touch][Grow][Amplify]` 被拒；`[Projectile][Explosion][AOE]` 通过 |
| A53 | 触发链 | `[Projectile][Harm][Trigger][Projectile][Explosion]`：第一段 +9 伤口；第二段从目标远侧 (241.04,221.01) 朝 (249,226) 发射，只爆炸一次；`live=0/impacts=2` |
| A54 | 不触发 | 目标在图边时 spawn 250.09,220.54 越界，日志 `not triggered`，只有第一段 |
| A55 | 飞行存档 | `a24-flight1` 含 segments/triggerOnHit；`a24-flight2` 含 `spellSegmentIndex=1`；读档各只结算一次 |
| A56 | 旧技能与最终态 | 旧四技能 4/4 生效；`a24-final.rws` 12,228,269B，tick17596，Paused，activeSlot9，live0；日志 0 ERROR/Exception |
| A57 | 构建与产物 | DLL 199,168B / `F3F49241…`；verify 47/0、check-defs 37/0、check-spells 460 PASS、UI/layout OK；a18–a23 与 Missiles.xml 未改 |

未实测：真实鼠标点击/悬停、红字提示排版、a–f 在 199,168B 最终 hash 上重跑（a–f 在 198,656B；最终只多 Lab 截图 hook，产品 Spells/UI/Defs 未变）、爆炸逐格伤害统计、墙体命中后续段落点、护盾/拦截。既有单图 96 发 live 上限仍作为安全阀保留，链本身没有额外段数上限。
