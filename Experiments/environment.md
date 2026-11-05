# 环境与接口调查

本文记录环境、接口与两种粒子方案的开发成本。静态接口、文档说明和真实游戏结果分别标明；逐项验收见 `acceptance.md`。

## 本机版本

游戏目录为 `E:/SteamLibrary/steamapps/common/RimWorld`，`Version.txt` 内容为 `1.7.4985 rev1269`。Steam 的 `appmanifest_294100.acf` 指定 `unstable`。截至本次调查日期 2026-10-25，官方 1.7 仍属测试分支。

`RimWorld.runtimeconfig.json` 指向 .NET 10 和 Waylith CLR；Unity 相关类型通过 UnityStubs 提供。主要引用位于游戏根目录，不是旧版 `RimWorldWin64_Data/Managed`。不要用“仍有 UnityEngine 类型”推断游戏仍运行于旧 Unity 引擎。

公开版本说明：<https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub>。

## 已安装前置

| 前置 | Workshop ID | 本地证据与作用 |
| --- | --- | --- |
| Harmony | 2009463077 | 模组 3.0.1.0；1.7 的 `0Harmony.dll` 为 3.0.0.0 |
| RimUnity | 3819570122 | 本机 0.1.17，`1.7/Assemblies/RimUnity.Core.dll` 和 `RimUnity.dll` |
| Humanoid Alien Races | 839005762 | 原精灵 Def 使用 `AlienRace.ThingDef_AlienRace`，因此是原内容实际依赖 |
| Royalty | 游戏 DLC | `Psycast.Activate` 首先调用 `ModLister.CheckRoyalty("Psycast")`，未启用时直接失败 |

依赖目录统一在 `E:/SteamLibrary/steamapps/workshop/content/294100`。原始仓库只声明 HAR 的 `loadAfter`，没有完整硬依赖。原 ModsConfig 实际启用了 Harmony 和全部 DLC，与用户对初始状态的记忆不同；实验启动仍须指定完整模组集合。

## RimUnity 与弹道的边界

官方源码：<https://github.com/chezhou9413/RimUnity>。重点资料为 `Docs/zh-CN/ApiQuickStart.md`、`Status.md`、`ParticleExpansion.md`。

公开 Workshop 描述仍指向 exporter 0.1.5，不能据此判断已安装 runtime 版本。本机 0.1.17 包含 0.1.16 对游戏 1.7.4984 渲染接口变更的修复。

- 可以通过 `new RimUnity.GameObject(...)` 和 `AddComponent<T>()` 程序化创建粒子、拖尾及材质。此路线在源码中成立，最终可见性必须进游戏检查。
- `RimUnity.ParticleSystem` 的 `main`、`emission`、`shape`、`noise`、`trails` 模块是引用字段，可直接修改。不要套用 Unity 中所有模块均为 struct 的写法。
- 内置 `RimUnity_StandardParticle4` Shader 使用加色混合。`ParticleSystemRenderer.sharedMaterial` 可共用材质；`material` getter 可能克隆材质。
- RimUnity 与 UnityStubs 的 `Vector3`、`Color` 等类型不同，需要明确转换。
- `Object.Instantiate` 针对可克隆的 Prefab 模板；普通程序化对象不能假设可以克隆。
- 对象创建、组件添加、资源加载必须在游戏主线程。游戏行为也应在 tick 中结算，不应在视觉 `Update` 中造成伤害或消耗。
- RimUnity 0.1.17 文档说明粒子播放独立于游戏速度；这不能证明暂停时所有粒子都会冻结。本实验以 RimWorld tick 推进飞弹位置和命中，视觉跟随逻辑状态。暂停与倍率下的观感由用户验收。
- 原版墙、地形和角色不会自动成为 RimUnity collider。路径交互必须查询 RimWorld 地图，检查整段位移，不能只检测本帧终点。
- RimUnity 对象归属当前 Game，切换地图并不自动清理所有对象。地图级视觉需要显式隐藏或销毁。
- 本次使用原创程序化特效，不需要重新分发 Unity 资源包，也不需要用户打开 Unity Editor。

这些结论来自只读调查 `ref:vivid-forest-tower`；当前本机程序集核对来自 `ref:noble-lagoon-quarry`。

## 行为、路径和窗口入口

| 能力 | 已核实接口 | 实现时需注意 |
| --- | --- | --- |
| 灵能技能 | `PsycastBase`、`CompAbilityEffect.Apply`、`Pawn_AbilityTracker.GainAbility` | Royalty、psylink、psyfocus 和 entropy 是真实施法条件 |
| 移动成本 | `Pawn_PathFollower.CostToMoveIntoCell`、`PathGrid.CalculatedCostAt` | PathGrid 成本缓存共享；不能为一件装备全局清除地图成本 |
| 路径消费 | `PawnPath.Peek`、`ConsumeNextNode`、`NodesLeftCount` | 必须保留终点、门、斜角、路径重算及 job 语义 |
| 门通行 | `Building_Door.FreePassage`、`BlocksPawn`、`CanPhysicallyPass` | 不把瞬移当作绕过关闭门的授权 |
| 交互窗口 | `Window.DoWindowContents`、`PostOpen`、`windowRect` | 本机可 `protected override SetInitialSizeAndPosition`；实际窗口已编译并运行，早期“private、不可 override”的判断已撤销 |
| 界面绘制 | `MapComponentOnGUI` / 游戏组件及 PlaySettings 入口 | 开关不能遮挡原控件或吞掉地图正常操作 |
| 持久化 | `Scribe`、`IExposable`、`ModSettings` | 游戏状态与纯视觉对象分开保存；窗口开闭不持久化 |

## 调查工具与失败记录

- 系统最高 .NET SDK 为 9.0.319；已有 .NET 10 runtime 不能替代 SDK。本实验使用 `Moddev/tools/dotnet` 内便携 SDK 10.0.402。
- 旧 `ilspycmd 8.2.0.7535` 读取当前程序集会出现 `ArgumentException`；没有据此判断游戏 DLL 损坏。
- `dotnet-ildasm 0.12.2` 附带的 `Mono.Cecil.dll` 可以读取当前元数据与 IL。工具路径：`C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll`。
- Context7 没有 RimUnity 对应文档；不使用名称相近但无关的 GameJoy/GameWiki 结果。Universal Modder 知识索引无本游戏条目。
- enhanced_terminal 调用中的反斜杠路径曾被剥离。当前命令使用正斜杠与分离 argv，避免 shell 字符串二次解释。

## 两种粒子方案的实际成本

对本次追踪飞弹，建议以原生 Fleck 作为简单效果的起点；需要连续拖尾、多层粒子或 Unity 编辑器工作流时，再选 RimUnity。两者已经共用同一套追踪、碰撞、命中事件和存档逻辑，换表现不必重写这些部分。

下面的本地实现比较基于当前源码和 2026-10-25 的实际集成。编辑器流程来自官方文档，本次没有运行 Unity Editor 或 exporter。

| 比较项 | 本次原生 Fleck 样例 | 本次 RimUnity 样例 |
| --- | --- | --- |
| 接入 | 每 3 个游戏 tick 调用 `FleckMaker.ThrowLightningGlow`，命中时调用 `FleckMaker.Static` | 创建 GameObject、ParticleSystem、TrailRenderer，设置材质、纹理和粒子参数 |
| 资源 | 复用原版 LightningGlow、ExplosionFlash；新外观可增加贴图和 FleckDef | 本次用程序化 soft-dot 纹理及框架 Shader；也可改为导出的 Prefab |
| 生命周期 | fleck 归地图管理并自行过期；后端停止继续发射即可 | 需要显式销毁飞行对象、管理爆点到期，并处理切地图、读档和缓存所属 Game |
| 调试重点 | Def、贴图路径、生成位置、尺寸和生成频率 | 另需核对 Shader、材质、组件配置、坐标类型转换及对象生命周期 |
| 本次后端文件规模 | 85 行 | 360 行 |
| 简单效果迭代 | 改 XML、贴图或少量 C#，重新加载游戏后检查；Lab 可直接选择现有 Def 和数值 | 程序化路线改 C# 后重建、重启；Prefab 路线增加编辑器导出、复制资源和重启步骤 |
| 对人类开发者的适用性 | 熟悉 RimWorld XML/C#、做闪光、烟尘、简单尾迹时更直接 | 已熟悉 Unity 粒子编辑器、需要复杂组合表现时更有价值，但须检查框架支持范围 |

行数包含注释和空行，两种样例的外观复杂度也不同。这个数字只说明本次需要维护的后端代码量，不是效率倍率或性能测试。未测量开发工时、FPS 或同等画质下的开销。

原生样例的资源工作较少，是因为复用了游戏效果。它仍使用游戏的材质与渲染系统，不能理解为原生粒子“没有材质”。RimUnity 样例增加的工作主要落在资源与生命周期上；本次已将这些工作封装在视觉后端，普通的事件切换不需要接触它们。

当前实验 DLL 同时引用两套后端，并声明 RimUnity 为依赖。选择 `WotF_Visual_Native` 可以使用原生表现，**不会**自动移除整个模组对 RimUnity 的依赖。

## 人类开发与后续扩展

已有组合只需选择两个独立字段：

```xml
<visualDef>WotF_Visual_Native</visualDef>
<impactDef>WotF_Impact_Heal</impactDef>
```

这会把原生表现与治疗事件组合起来，速度、转向、目标追踪、碰撞和保存字段仍由现有核心处理。新增命中事件时，实现一个 `ArcaneMissileImpactWorker` 并注册 Def；新增粒子形式时，实现一个 `ArcaneMissileVisualSource`、注册 Def 并提供所需资源。完整接入示例见 `missiles.md` 第 2 节。

本次已在真实游戏中交叉运行两种视觉后端与伤害、治疗、爆炸三种事件。伤害每次扣除墙体 7 HP，治疗将伤口严重度从 3 降到 0，爆炸以地面坐标为中心使邻近墙体每次损失 4 HP。存档保存了视觉 Def、事件 Def、目标、速度与位置，读档后继续飞行并只结算一次。这里证明的是配置与行为链路；外观是否合适由用户判断。

RimUnity 的编辑器路线要求 Unity 2022.3 LTS、exporter 包及 Shader 工具等配套。官方流程是制作 Prefab、导出资源及目录信息，再由运行时加载；目前没有运行时资源热重载，更新后仍需重启游戏。编辑器能提供直观调整，但不能把 Unity 本身的所有粒子、Shader 和物理功能都视为 RimUnity 已支持。原版地图建筑也不会自动变成 RimUnity collider，因此本项目仍由 RimWorld 地图逻辑判断命中。

这条路线是否比 XML/C# 更省事，取决于开发者已有经验和效果复杂度。对已有 Unity 美术经验的人，编辑器制作复杂效果可能更顺手；对只做简单二维效果的 RimWorld 模组作者，原生路线的工具和依赖更少。本次未实际操作 exporter，也未验证公开 exporter 0.1.5 与本机 runtime 0.1.17 的组合，不据此承诺其兼容性。

## 证据范围与参考资料

- 本地代码：`ArcaneMissileVisualNative.cs`、`ArcaneMissileVisualRimUnity.cs`、`ArcaneMissileImpactWorkers.cs`；功能记录见 `missiles.md`，实机命令 ID 和数值见 `integration.md`、`acceptance.md`。
- [RimUnity ExporterSetup](https://github.com/chezhou9413/RimUnity/blob/main/Docs/zh-CN/ExporterSetup.md)：编辑器环境与工具准备。
- [RimUnity Workflow](https://github.com/chezhou9413/RimUnity/blob/main/Docs/zh-CN/Workflow.md)：制作、导出、加载与迭代流程。
- [RimUnity Status](https://github.com/chezhou9413/RimUnity/blob/main/Docs/zh-CN/Status.md)：受支持功能与限制。
- [RimUnity 0.1.17](https://github.com/chezhou9413/RimUnity/blob/main/Docs/en/Release-0.1.17.md)：粒子时间与本地版本相关说明。
- [RimWorld Modding Tutorials](https://rimworldwiki.com/wiki/Modding_Tutorials)：XML/C# 模组开发入口；[Plague Gun](https://rimworldwiki.com/wiki/Modding_Tutorials/Plague_Gun) 提供命中与原生效果的实例，具体 API 仍以本机 1.7 程序集为准。

上述网页于 2026-10-25 核对。真实多地图切换、地图移除及目标丢失的完整游戏场景尚未运行；对应逻辑已有生产代码 harness 和调度 IL 检查。暂停与倍率下的外观、其他体型装备的视觉适配、Unity Editor/exporter 工作流不计入已完成的实机验证。

## 并发驱动与重载边界（2026-10-27 a21 观察到）

- Def 与 DLL 只在进程启动时读取：本次旧会话 14:53 启动，之后新增的 `Plants.xml` 与新的 `Lab`/`Abilities` 代码都要等重启新实例（PID33332）后才生效，`status.defs` 才从 `WotF_MaturePlant=false` 变为 `true`。改 Def 或重建 DLL 后必须重启隔离实例，`build.ps1` 在检测到 `RimWorld` 进程时也会直接拒绝构建。
- `requests.txt` 是追加式队列，写入后由运行中的游戏按屏幕状态执行；读档是排队的长事件，紧随其后的命令可能返回 `no map loaded`，需要等新图就绪再发（本轮 `c2a21load1` 后的首个 `plants` 如此，等待数秒后恢复）。
- 同一隔离实例不宜由两个 worker 同时驱动：本轮被中断的原集成 worker 与本 worker 的命令在 `results.jsonl` 中交错出现（`c1a21...` 与 `c2a21...`）。结果本身可按 id 区分且都在同一新 DLL 上执行，但排障时应先确认队列归属，避免对同一地图重复执行 `setup confirm=1`。
- 观察只能通过 `status`/`observe`/`plants`/`missiles`/`ui action=status` 等有界命令完成；没有桌面键鼠，因此玩家点击式校验（目标高亮、鼠标提示文本）无法自动化。
