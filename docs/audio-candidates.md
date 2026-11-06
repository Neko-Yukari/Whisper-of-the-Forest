# 本地音效候选

用户提供音效库后进行了只读筛选。2026-10-25 使用本机 `ffprobe` 读取下列八个文件均成功；未播放或试听，选型仅依据文件名、用途和音频参数。原文件未修改，未复制进模组。当前优先脚手架，声音接入和听感精调后置。

目录 A：`E:/onedrive/团/音效/魔法游戏技能 - RPG Magic SFX 2/魔法游戏技能 - RPG Magic SFX 2`

目录 B：`E:/onedrive/团/音效/技能魔法 - Epic Stock Media Phantom Magic/技能魔法 - Epic Stock Media Phantom Magic`

| 用途 | 相对路径 | 时长（秒） | 格式 | 大小（字节） |
| --- | --- | ---: | --- | ---: |
| 施法优先候选 | A/Short Spells/S1_Small_Cast_P1.wav | 1.186 | PCM 16-bit，44.1 kHz，双声道 | 210308 |
| 直接命中 | A/Short Spells/S4_Cute_Impact_Generic_1_v1.wav | 1.277 | PCM 16-bit，44.1 kHz，双声道 | 226436 |
| 治疗 | A/Short Spells/S4_Cute_Heal_1.wav | 1.535 | PCM 16-bit，44.1 kHz，双声道 | 271964 |
| 飞行掠过 | A/Basic Whooshes/Subtle_Spell_Whoosh_1.wav | 1.370 | PCM 16-bit，44.1 kHz，双声道 | 245344 |
| 瞬移候选 | A/DucVu_Spells_SFX_[Video_Demo_1]/Teleport_Side_Step.wav | 2.452 | PCM 16-bit，44.1 kHz，双声道 | 433158 |
| 施法备选 | B/_(WAVs)_Magic_Spells_And_Dust/Magic_Game_Spell_Cast_1.wav | 1.698 | PCM 24-bit，96 kHz，双声道；含 MJPEG 封面流 | 1117632 |
| 瞬移备选 | B/_(WAVs)_Magic_Spells_And_Dust/Magic_Game_Poof_Spell.wav | 2.997 | PCM 24-bit，96 kHz，双声道；含 MJPEG 封面流 | 1866068 |
| 小爆点 | B/_(WAVs)_Magic_Hits/Magic_Sparkle_Poof_Hit_2.wav | 2.276 | PCM 24-bit，96 kHz，双声道；含 MJPEG 封面流 | 1450446 |

后续可先试 A 组施法、命中和治疗。瞬移每隔一格触发，两个候选的完整长度都可能叠加过多，需要试听后裁剪或控制触发频率，不能仅凭名称认定合适。B 组如用于游戏资源，应显式选择音频流并转换为统一格式，保留原始文件。

核验命令形式：`ffprobe -v error -show_entries format=duration,size:stream=codec_name,sample_rate,channels,bits_per_sample -of json <文件>`。头部读取成功不代表完整试听或全部数据解码测试。
