# RimUnity 高级视效工作流

本文记录已经在真实游戏里走通的制作链：Unity 编辑器制作 → 导出明文资源 → 模组加载 → 地图粒子 → Bloom → ScreenEfx。轨道炮等高级视效沿用这条链，不再各自摸索工具。

## 环境与目录

- Unity：`2022.3.53f1c1`，安装在 `C:\Program Files\Unity\Hub\Editor\2022.3.53f1c1\Editor\Unity.exe`。
- RimUnity：游戏侧工坊 `3819570122`，制作端版本必须同为 `0.1.22`。
- Unity 工程：`Moddev/UnityVfx/WotF-Vfx`，不进模组仓库，不碰其他 Unity 工程。
- 导出器：`Moddev/UnityVfx/packages/com.chezhou.rimunity-0.1.22.tgz`。
- 工程入口：`Assets/Editor/WotfVfxBuild.cs`。现有 `BuildProbe` 和 `BuildDarken` 是可运行的最小样例。
- 导出产物：模组 `1.7/Data/RimUnity/`。加载器只读这里；不要手改 JSON/GLSL，改 Unity 源资产后重新导出。

## 一次性安装

创建工程：

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.53f1c1\Editor\Unity.exe' `
  -batchmode -nographics -quit -createProject 'E:\SteamLibrary\steamapps\common\RimWorld\Moddev\UnityVfx\WotF-Vfx' `
  -logFile 'E:\SteamLibrary\steamapps\common\RimWorld\Moddev\UnityVfx\create.log'
```

在 `Packages/manifest.json` 的 `dependencies` 中加入：

```json
"com.chezhou.rimunity": "file:../../packages/com.chezhou.rimunity-0.1.22.tgz"
```

再让 Unity 打开一次工程完成导入。不要从其他工程复制 `Library` 或锁文件。

## 制作与导出

1. 用 Unity 原生 `ParticleSystem`、材质和贴图制作 Prefab。最小粒子材质可用 `Legacy Shaders/Particles/Additive`。
2. 需要泛光的每个对象及子对象都设 Layer **29**；修改根对象不会自动修改子级。
3. 用 `RimUnity.Editor.PrefabExporter.Export` 导出。`BuildProbe` 展示了 Prefab、材质和程序化贴图的完整写法。
4. 自定义 Shader 用显式顶点/片元程序。ScreenEfx 必须按 **world** 导出（`ExportRequest.ui = false`），顶点入口用 `vert_img`。
5. 导出贴图时保留图形设备，使用 `-force-d3d11`，不要加 `-nographics`。

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.53f1c1\Editor\Unity.exe' `
  -batchmode -quit -force-d3d11 `
  -projectPath 'E:\SteamLibrary\steamapps\common\RimWorld\Moddev\UnityVfx\WotF-Vfx' `
  -executeMethod WotfVfxBuild.BuildProbe `
  -logFile 'E:\SteamLibrary\steamapps\common\RimWorld\Moddev\UnityVfx\export-probe.log'
```

把方法名换成 `WotfVfxBuild.BuildDarken` 可导出压暗材质。不能只看进程退出码：还要检查日志里的导出异常、降级警告，以及模组 `catalog.json` 是否包含新入口。

**修改导出资源后必须重启游戏。RimUnity 不支持这套明文资源的热重载。**

## 游戏内加载

产品侧用 `PrefabVfxDef` 声明导出入口：

```xml
<WhisperOfTheForest.Vfx.PrefabVfxDef>
  <defName>WotF_Vfx_PipelineProbe</defName>
  <workerClass>WhisperOfTheForest.Vfx.PrefabVfxWorker</workerClass>
  <prefabPath>Effects/Probe</prefabPath>
  <height>1.2</height>
  <durationTicks>300</durationTicks>
</WhisperOfTheForest.Vfx.PrefabVfxDef>
```

法术仍然只发 `VfxRequest`。`PrefabVfxWorker` 内部顺序固定为：

```csharp
var template = RimUnity.Assets.PrefabLoader.Load(def.modContentPack, def.prefabPath);
var instance = RimUnity.Object.Instantiate(template);
instance.transform.position = new RimUnity.Vector3(x, height, z);
instance.SetActive(true);
instance.GetComponent<RimUnity.ParticleSystem>().Play(true);
```

所有加载、实例化、组件操作都在游戏主线程。模板是共享且失活的，不改模板、不销毁模板；销毁的是自己创建的实例。

### 时间、暂停与生命周期

RimUnity 粒子按真实帧时间运行，不自动跟随游戏倍速。`PrefabVfxWorker` 每帧把每个粒子系统的 `main.simulationSpeed` 设为 `Find.TickManager.TickRateMultiplier`，暂停时为 0，因此与法术 tick 同步。

固定时长效果按 `TicksGame` 到期；`Release` 销毁实例。离开当前地图时实例撤下，回到地图时可重建；`MapComponent_Vfx.MapRemoved` 清空该图效果。整套读档会丢弃旧 MapComponent，不应只依赖 `MapRemoved` 日志判断清理，需在每次改生命周期后做读档残留检查。

## Bloom

- Bloom 只覆盖地图主通道的粒子、拖尾和模型，不覆盖 GUI 贴片。
- RimUnity 默认关闭 Bloom；阈值、强度和扩散是用户全局设置，内容模组不能按单个特效另设。
- 内容模组用 `RimUnity.RimUnityMod.Bloom.SetLayerEnabled(29, true)` 接管 Layer 29，生命周期结束时 `ReleaseLayer(29)`。
- Lab 可用 `vfx bloom=on` / `vfx bloom=off` 做同场景对照。已实测：开启时探针有光晕，关闭后只剩粒子本体。
- 颜色超过 1 的 HDR 保留还受 RimUnity 的“地图 HDR 接管”用户开关影响；不要把“颜色夹白”和“没有 Bloom”混为一谈。

## ScreenEfx：只压暗地图，保留高光与 UI

通道顺序是：**地图 → Bloom 合成 → ScreenEfx → 游戏 UI**。所以 ScreenEfx 不会压暗 UI，但它拿到的已经是合成画面，不能再单独识别我们的光束。

本模组用 `Assets/Effects/SelectiveDarken.shader` 做亮度选择性压暗：

```hlsl
float luminance = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
float keep = smoothstep(_KeepLow, _KeepHigh, luminance);
float multiplier = 1.0 - _Strength * (1.0 - keep);
```

暗部按 `_Strength` 压暗，超过亮度阈值的高光保留。代价是其他足够亮的物体也会保留；阈值由具体 Vfx Def 调整。

游戏侧参考 `ScreenDarkenVfxWorker` / `SelectiveDarkenClip`：

- `AssetLoader.Load<Material>` 取共享模板，Clip 内 `new Material(template)` 建私有副本。
- `EfxPlayer.Play(clip)` 后播放器拥有 Clip；结束只调用 `handle.Stop()`。
- 私有材质只在 `OnRelease` 里销毁，不能在 `Stop` 后立即销毁。
- `_MainTex` 是上一阶段画面。全屏三角形已提供与导出器配套的 UV，**内容 Shader 不要再做 Y 翻转**。
- 屏幕效果不参与存档；暂停/时长由所属 worker 按 tick 控制。

已实测：地图变暗、UI 保持原亮度、粒子高光与 Bloom 仍亮，画面方向正常。

## 世界几何：LineRenderer 而不是 Prefab

轨道炮的光柱、电弧和冲击环用运行时创建的 `RimUnity.LineRenderer`，只从导出目录取一个材质（`Materials/RailgunGlow`）。下面的约束来自导出日志与反编译核对，渲染观感本身尚未实机验收：

- 线渲染器是**相机朝向的 ribbon**：两点连线从任何角度都读作一条光柱，不需要交叉面片。宽度是世界单位（`widthMultiplier`），正好用格子数表达。
- 颜色走 `startColor` / `endColor`（顶点色）。所以**同一份共享材质可以给所有层上色**，不必为每个特效复制材质，也不必每帧写材质属性。Shader 里必须乘 `COLOR` 语义。
- `loop` 没有接通，闭合会抛 `NotSupportedException`：画圆环时把首点复制到末尾即可。`textureMode` 只支持 `Stretch` / `Tile` / `RepeatPerSegment`，`alignment` 默认 `View`。
- 共享材质是模板，不要改它的属性；每层的亮度、颜色、透明度都在线的渐变里。
- 世界材质遵守地图深度（`ZTest LEqual`、`ZWrite Off`）：光束会被地形和墙体裁掉，高度必须高于常见墙体才好看。
- 一条 ribbon 的实际覆盖面积是 **线段长度 × 宽度**。两点几乎重合时（例如用一条线画光球），面积趋近 0，屏幕上看不见——这跟颜色和透明度无关。画圆点要用等长的方形线段（长度 = 宽度）配径向贴图，或者干脆用粒子。

导出单项材质（贴图、Shader 作为依赖自动收集）：

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.53f1c1\Editor\Unity.exe' `
  -batchmode -quit -force-d3d11 `
  -projectPath 'E:\SteamLibrary\steamapps\common\RimWorld\Moddev\UnityVfx\WotF-Vfx' `
  -executeMethod WotfVfxBuild.BuildRailgunGlow `
  -logFile 'E:\SteamLibrary\steamapps\common\RimWorld\Moddev\UnityVfx\export-railgun.log'
```

不要直接用 `& Unity.exe` 判断完成：PowerShell 不等 GUI 进程，退出码会是空。用 `Start-Process -Wait -PassThru` 再读 `ExitCode`，并核对日志里的 `[RimUnity] 已提交 N 个资源入口` 与 catalog 新条目。导出后的 `shader.json` 里可核对 `srcColor/dstColor/depthCompare/depthWrite` 和 `approximations`（应为空）。

Bloom 层接管是**位**而不是计数：`SetLayerEnabled` 可重复调用，`ReleaseLayer` 一次就把整层关掉。多个特效同屏时必须自己引用计数，否则先结束的那个会把还亮着的特效的泛光一起关掉。

## 调试与验收

- L0：`Tools/build.ps1 -Lab`，引用真实程序集，0 警告 0 错误。
- L1/L2：`Tools/test.ps1`；磁盘日志门禁必须另外通过。
- Lab 探针：选择小人后用 `vfx` 播放粒子，`vfx def=WotF_Vfx_DarkenProbe` 播放压暗。轨道炮用 `vfx def=WotF_Vfx_Railgun target=<x>,<z>`（省略 `length` 时取 origin 到 target 的距离），对照版是 `vfx def=WotF_Vfx_Railgun_WorldRing`。
- 截图必须读真实游戏画面。`um win shot` 在当前机器会因 ffmpeg 缺少 `gfxcapture` 失败；可用 Windows MCP 的只读 `ScreenshotWindow`，不要为此切换焦点或发送输入。
- 至少核对：资源首次加载无错误、暂停冻结、倍速同步、到期销毁、切图/读档无残留、Bloom 开关对照、压暗不压 UI、不翻转、不动相机。

## 尚未证明的能力

- 轨道炮用运行时 `LineRenderer` 搭出的光柱、电弧与冲击环，只在导出侧验证过（材质/Shader 转换、catalog 入口、Def 校验、编译）；实机观感、深度遮挡与 Bloom 强度仍需在游戏内验收。
- Unity 编辑器导出的复杂多层 Prefab 在轨道炮上的最终观感，需要单独实机验收。
- 本流程没有提供镜头震动、曝光、色调映射或真正的室内反弹光。
- 世界层材质遵守地图深度；光束高度不足会被地形和墙裁掉。
- 首次实例化和首次 ScreenEfx 显示可能有资源编译/分配尖峰，大特效要实测。
