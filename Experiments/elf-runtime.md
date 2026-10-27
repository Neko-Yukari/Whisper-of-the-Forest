# 原精灵 Def 运行期错误修复（1.7.4985）

范围：仅修两个阻断 `ElfColonist` 实验的确定错误。未改 `ParentName`、年龄阶段、年龄相关数值、体形/美术、语言名字内容。

## 1. 命名语法不可解析

**日志证据**（`Player.log` 行 670，2026-10-25 18:05:12）：

```
ERROR [Verse] Grammar unresolvable. Root 'r_name'
1  r_name → [Adult_Name] [Family_Name]
   Adult_Name → UNRESOLVABLE
2  Family_Name->(10 strings from file: Names/Elf_Family_Name)
```

**根因**：`Defs/RulePack/RulePacks_Namers_Elf.xml` 的 `rulesStrings` 引用 `[Adult_Name]`，但两个 `RulePackDef` 的 `rulesFiles` 只定义了
`Elf_Adult_Name_Female`（男性包甚至映射到 `Names/Elf_Adult_Name_Male`）。RimWorld 的 GrammarResolver 只按符号名解析，
符号未定义即整条 `r_name` 不可解析，HAR 的 `HarmonyPatches.GeneratePawnNamePrefix`（日志调用栈）拿不到名字。

**修复**：`rulesFiles` 的符号改名为 `Adult_Name`，并保持每个包指向本性别名字文件：

- `NamerPerson_Elf_Male`：`Adult_Name->Names/Elf_Adult_Name_Male`
- `NamerPerson_Elf_Female`：`Adult_Name->Names/Elf_Adult_Name_Female`

名字文件本身（英/中）未改，`r_name` 规则文本未改，命名风格不变。

## 2. 精灵没有可用近战

**日志证据**（行 740，18:05:36）：`Lab Subject 23471 has no available melee attack, spawned=True dead=False downed=False curJob=null verbList= bodyVerbs=`

**根因**（1.7 程序集只读 IL 核实，`Assembly-CSharp.dll`）：

- `RimWorld.Pawn_MeleeVerbs.GetUpdatedAvailableVerbsList` 从 `pawn.verbTracker.AllVerbs` 收集近战动词；
- `Verse.Pawn.get_Tools` 直接返回 `Thing.def.tools`；
- `Verse.VerbTracker.InitVerbs` 先按 `IVerbOwner.VerbProperties`、再遍历 `IVerbOwner.Tools` 的 `Tool.Maneuvers` 生成动词。

`ElfRace.xml` 用 `ParentName="BasePawn"`，而 `BasePawn`（`Data/Core/Defs/ThingDefs_Races/Races_Animal_Base.xml:4`）不含 `tools`；
原版 `Human`（`Data/Core/Defs/ThingDefs_Races/Races_Humanlike.xml:17-77`）的拳/牙/头近战全部来自自身 `<tools>`。
因此该精灵 `verbTracker.AllVerbs` 为空，`bodyVerbs=` 与 `verbList=` 同时为空。

**修复**：在 `ElfRace.xml` 增加 `<tools>`，内容与 Human 的基础徒手工具一致（left/right fist、teeth、head；
Blunt/Bite，power 8.2/8.2/8.2/5，cooldown 2）。`Bodies_Elf.xml` 已含 `LeftHand`、`RightHand`、`Teeth`、`HeadAttackTool`
四个部位组，链接可用。未改 `ParentName`，也未改体形/健康/年龄。

## 复验命令（不启动游戏）

```powershell
pwsh -NoProfile -File Moddev/tools/check-elf/check_elf.ps1
```

检查项：两个命名包的 `[符号]` 是否都有定义、`rulesFiles` 指向的名字文件在每个语言下是否存在、
`Elf` 是否有 `<tools>`、每个 `linkedBodyPartsGroup` 是否存在于 `Bodies_Elf.xml`。当前输出 `PASS`。

只读 IL 复核（Mono.Cecil，工具在 `Moddev/tools/check-elf`，不含游戏 DLL/源码副本）：

```powershell
pwsh -NoProfile -File Moddev/tools/check-elf/il.ps1 `
  -Assembly 'E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll' `
  -Type 'RimWorld.Pawn_MeleeVerbs' -Method 'GetUpdatedAvailableVerbsList'
pwsh -NoProfile -File Moddev/tools/check-elf/il.ps1 `
  -Assembly 'E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll' `
  -Type 'Verse.VerbTracker' -Method 'InitVerbs'
```

## 3. 简体中文翻译注入（第二轮）

**日志证据**：`Translation data for language Simplified Chinese has 1 errors`（重启后仅剩此 1 条）。
计数逻辑（`LoadedLanguage.InjectIntoData_AfterImpliedDefs` IL）：`language.loadErrors.Count + Σ(defInjectionPackage.loadErrors.Count)`。

**根因（同一文件的两个确定错误）**：

1. 目录 `Languages/ChineseSimplified/DefInjected/ThingDefs_Trait/` 不是 Def 类型名。
   `LoadedLanguage.LoadData` 对 `DefInjected` 下每个一级目录调用 `GenTypes.GetTypeInAnyAssembly(目录名)`；
   解析失败时 `loadErrors.Add("Error loading language from ...: dir ThingDefs_Trait doesn't correspond to any def type. Skipping...")`。
   这 1 条即当前唯一的语言加载错误，且该目录内文件根本不会被解析。真实类型是 `RimWorld.TraitDef`（Cecil 核实字段 `List<TraitDegreeData> degreeDatas`）。
2. `Elf_Trance.degreeDatas.1.*` 下标越界。`TraitDef.degreeDatas` 只有 1 项；`DefInjectionPackage.SetDefFieldAtPath` 的 IL 判定
   `index >= list.Count` → `"Trying to translate ... at index N but the list only has M entries (so max index is M-1)"`。
   下标为 0 基：原版 `Data/Core/Languages/English/DefInjected/TraitDef/Example_Traits.xml` 明确写 `listname.n` 且示例为 `Nudist.degreeDatas.0.label`。
   这是修好目录名后立刻会暴露的第二个错误。

**修复**：文件移动到 `Languages/ChineseSimplified/DefInjected/TraitDef/Trait_Trance.xml`，键改为
`Elf_Trance.degreeDatas.0.label` / `.0.description`。译文与占位符未改。

**其余原有注入核查**：本模组 `Languages/**/DefInjected` 仅此 1 个文件、2 个键；`Languages/English/DefInjected/ThingDef/`
为空目录（类型合法，无文件，不产生错误）。`Languages/ChineseSimplified/ElfRace.xml` 位于语言根目录，
加载器只读取 `Keyed`/`DefInjected`/`Strings`，该文件不会被读取，因此不产生错误，本次未动。

**复验**：

```powershell
pwsh -NoProfile -File Moddev/tools/check-elf/check_injections.ps1
```

输出 `PASS: 目录类型/Def/字段/列表下标检查全部通过`（2 个键）。该脚本已用自测证明能抓到两类错误：
仅下标改回 `.1.` → 报列表越界；仅目录改回 `ThingDefs_Trait` → 报目录非 Def 类型名。

## 实机待验（本次未做，主代理重启后执行）

1. 重新启动 1.7.4985，日志不再出现 `Grammar unresolvable. Root 'r_name'`，生成 `ElfColonist` 有非空姓名。
2. 征召精灵不再出现 `has no available melee attack`，可显示近战 gizmo 并对目标执行近战。
3. 顺带确认日志无新增与 `Elf`/`NamerPerson_Elf_*` 相关的 Def 或 grammar 报错。
4. 日志不再出现 `Translation data for language Simplified Chinese has N errors`；`Elf_Trance` 特性显示“出神”及中文描述。

已知未处理：实验装备 `WotF_TerrainStride`/`WotF_BlinkStride` 的 smeltable 告警属其他负责人范围（当前日志已无此告警）。
