# Research/Results — 结构化实验结果

全部是旧机器 `Moddev/evidence/` 与 `Moddev/lab-run/` 的原始副本（未编辑、未裁剪），来源和哈希见 `../SOURCES.md`。文本文件按仓库 `core.autocrlf` 规范为 LF 入库，与旧机原件的差异只在换行符。

## 文件是怎么产生的

游戏带 `-wotflab=<目录>` 启动后，实验设施按行读取 `<目录>/requests.txt`，执行后：

- `results.jsonl`：每条命令一条 JSON，含命令回显与世界状态快照。
- `experiment.jsonl`：事件流（发射、命中、存档、按钮回调等），带 `tick`。
- `lab-status.json`：最近一次启动/状态的快照。
- `observe-base.json`：基线观测样本。

## 怎么读

`results.jsonl` 每行至少包含：

| 字段 | 含义 |
| --- | --- |
| `id` | 命令 ID（`c...`），实验中引用证据就用它 |
| `ok` | 命令是否成功执行 |
| `lab` / `protocol` | 实验设施协议版本 |
| `harmonyPatched` / `gameLoaded` | 模组是否已 patch、Def 是否加载 |
| `defs` | 关心的 Def 是否存在的布尔表 |
| `tick` / `timeSpeed` | 当时的游戏 tick 与速度 |
| `selected` / `selectedId` | 被观察对象的完整快照（位置、job、能力、伤口等） |
| `request` / `dir` | 请求文件与本轮实验目录（旧机器路径） |
| `missiles` | 射弹统计对象：`live/launched/impacts/expired/labFired/frameCount/frames[]`；每帧含 `x/z/headingX/headingZ/targetX/targetZ/ageTicks/targetThingId/targetCell/settings` |
| `results` | 只有少数旧行把命令返回值包在这一层；多数行的字段是**平铺**的，取值前先看该行有哪些 key |

`experiment.jsonl` 每行大致是 `{"ts","tick","category","actor","detail"}`，适合看时间顺序与因果关系。

典型查法（PowerShell，逐行 JSON 更省内存）：

```powershell
# 找某个命令 ID（c073730994927 在 a20-moving，不在 a20）
Select-String -Path Research/Results/a20-moving/results.jsonl -Pattern 'c073730994927'

# 读一次飞行的状态与帧数据
Get-Content Research/Results/a20-moving/results.jsonl |
  ForEach-Object { $_ | ConvertFrom-Json } |
  Where-Object { $_.missiles } |
  Select-Object -First 20 id, tick,
    @{n='live';e={$_.missiles.live}},
    @{n='ageTicks';e={$_.missiles.frames[0].ageTicks}},
    @{n='headingX';e={$_.missiles.frames[0].headingX}}
```

`missiles` 是**对象**不是数组，`age/heading` 在 `missiles.frames[]` 里；`live/impacts/expired` 直接在 `missiles` 上。少数旧行额外套了一层 `results`，遇到时先看 keys 再取值。

## 各目录对应哪一轮

| 目录 | 说明 | 对照验收编号 |
| --- | --- | --- |
| `a18/` | 第一版实现 + 移动/UI 实验，含 `results.jsonl`/`experiment.jsonl` | A01–A19 |
| `a19-final/` | 三轮预设（基础/Heavy/Mender）落地后的最终结果 | A20–A25 |
| `a19-soundfix/` | 爆炸音效修复后的重跑（无 ERROR/Exception/null SoundDef） | A22 补充 |
| `a20/` | 原生射弹迁移后的主结果（含先前累计记录，`c065...` 为本轮命令） | A26–A29 |
| `a20-moving/` | 补验：已推进状态下保存、读档续飞 | A31–A32 |
| `a20-prelaunch/` | a20 开跑前的 `.a19` 快照（其中 `results.jsonl.a19` 与 `a19-soundfix/results.jsonl` 完全相同，未重复上传） | — |
| `lab-run/` | 最后一次实验目录的原样快照。`requests.txt` 是历史命令记录（实体 ID/坐标/存档名都已过期，不能原样重放，仅作写法参考），见 `../HANDOFF.md` §8.4 | — |
| `Logs/` | 三份 **脱敏摘录**（不是完整日志），头部写明源文件字节数与 SHA256 | 上下文 |
| `Media/first-flight.mp4` | 首次飞行效果的录屏（自有素材，546 KB） | 观感参考 |

结论汇总在 `Experiments/acceptance.md`；这里只放原始数据，不重复写结论。

## 没上传的

完整 `Player.log`、存档（`*.rws`，6–22 MB/个）、4.5 MB 的主菜单截图、纹理缓存等留在旧机器；文件名、大小、SHA256 和不传的理由见 `../SOURCES.md` 的"未上传清单"。
