# Research/Graphify — 代码图谱重建

旧机器上有一份本地 graphify 快照（`Moddev/Graphify/graphify-out/`），本次**只上传重建所需脚本与清单，不上传缓存**：`graph.json`、AST cache、`runs/`、虚拟环境、反编译源码全部留在旧机。

## 内容

| 文件 | 说明 |
| --- | --- |
| `refresh-graph.ps1` | 重建入口（旧机原件副本，未改）。把模组 `Source/WhisperOfTheForest/**/*.cs` 与一小片原生类型编译/抽取成 code-only 图，再重写成真实绝对路径。 |
| `tools/rewrite-paths.py` | `refresh-graph.ps1` 依赖的路径重写脚本，原件副本。 |
| `native-metadata.json` | 旧机 `Graphify/native/metadata.json` 的副本：记录被抽取的原生类型、字节数、SHA256、行数，以及 `Assembly-CSharp.dll` 的版本/哈希和反编译器版本。**不含反编译源码本体**。 |

## 为什么不上传反编译源码

`Graphify/native/source/Verse.Projectile.cs` 等 5 个文件是 ilspycmd 从本机 `Assembly-CSharp.dll` 反编译出来的游戏代码（文件夹头也写着"decompiled, READ-ONLY, never shipped"）。它们属于第三方代码，不入库；需要时用下面的命令按 `native-metadata.json` 里的哈希复现。

## 复现步骤

1. 准备 Python 3（旧机用的是项目内 venv）与 graphify Python 包，能执行 `python -m graphify extract`。
2. 装反编译器到脚本期望的位置：

   ```powershell
   dotnet tool install ilspycmd --tool-path <Graphify>/tools/decompiler --version 9.1.0.7988
   ```

3. 运行（参数默认值指向旧机器，需覆盖）：

   ```powershell
   pwsh -File Research/Graphify/refresh-graph.ps1 `
     -ModRepo <repo> `
     -GameAssembly <game>/Assembly-CSharp.dll `
     -Python <python.exe> `
     -OutDir <Graphify>/graphify-out `
     -RefreshNative -Verify
   ```

4. `-RefreshNative` 会按 `native-types` 列表重新反编译；校验方式是比对 `native-metadata.json` 里的 `assembly_sha256`、各类型 `sha256`/`lineCount`。游戏版本不同则哈希必然不同，此时只作为"换了版本的记录"，不要当成漂移。

## a20 时这份图覆盖了什么

- 时间戳 `2026-10-27T07:35:02.2249724Z`，35 个模组源码文件 + 5 个原生类，794 节点 / 2038 边。
- 只覆盖列出的源码，**不含全游戏与 XML**；查询结果里 `Projectile_ArcaneMissiles:20` 带有 `EXTRACTED inherits` 边指向原生 `Projectile`（对应验收 A30）。
- 换机器后图是"按当前源码重新生成"的，与旧快照的一致性只靠上述哈希核对，不要直接比对节点 ID。

## 边界

- 不改全局 graphify 配置、不写云后端；脚本里设了 `GRAPHIFY_BACKEND=none`、`GRAPHIFY_NO_AUTO_REFRESH=1`、`GRAPHIFY_QUERY_LOG_DISABLE=1`。
- 生成的 `graphify-out/` 属于缓存，建议加进本地忽略而不是提交。
