# Deprecated

Content here is kept for the record only: it is **not compiled and not loaded** (the project globs
`Source/WhisperOfTheForest` only, and nothing under `_Deprecated` sits in a load folder), and it is not
acceptance evidence. Each entry names its source and why it left.

## 2026-11-05 - railgun charge timer

| Item | Source | Reason |
| --- | --- | --- |
| `Source/WhisperOfTheForest/Spells/GameComponent_RailgunCasts.cs` (`RailgunCast`, `GameComponent_RailgunCasts`) | plain move (the repository has no git) | The composite form kept its own charge timer and its own pending list. Casting is now one state machine for every form (`Spells/Casting/`: windup → channel → recovery), so the railgun's charge is the channel of its cast; a second timer would be a second truth. The charge visual is requested by `RailgunCastService.PlayCharge` when that channel starts. |
| `SpellRailgunSpec.chargeTicks` (field, inside `Spells/SpellRailgunSpec.cs`) | removed in place | The duration moved to the rune's `<channelTicks>`; the composite form's numbers no longer keep a copy of the cast timing. |

The type name survives as a state-less compatibility shell in
`Source/WhisperOfTheForest/Spells/Casting/GameComponent_RailgunCasts.cs`: the current L2 fixture
(`a25-fixture.rws`) names the old component, and a missing class logs an error on every load. The
shell reports the in-flight count from `GameComponent_SpellCasts` for the lab's status line and
restores nothing. Delete it once no save or fixture names it (and change `Lab/LabCommands.cs:174-176`
to read `GameComponent_SpellCasts.Current?.Count`).

## 2026-11-05 - trigger rune, elf race, instant maturity, standalone psycasts, tech lab window

Five removals in one pass. Paths under `_Deprecated/` mirror the original relative path. Files that
were git-tracked moved with `git mv`; untracked ones (the whole `Spells/`/`Abilities/` era, the
`Experiments/` defs and the spellbook textures) moved with a plain move.

### 1. Hit-trigger rune `WotF_Trigger_OnHit`

| Item | Source | Reason |
| --- | --- | --- |
| `1.7/Defs/Experiments/Spellbook.Trigger.xml` | extracted from `1.7/Defs/Experiments/Spellbook.xml` | The new spell table expresses timing with 滞留 / 地印 / 延迟 instead of a hit-then-continue rune. Only the entry rune goes; the segment machinery (`SpellSegment`, `spellSegmentIndex`, `SpellTriggerService`, `SpellTriggerGeometry`) stays as the payload layer those three will use. |
| `1.7/Languages/ChineseSimplified/DefInjected/SpellGlyphDef/WotF_Spells.Trigger.xml`, `1.7/Languages/English/...` | extracted from the matching `WotF_Spells.xml` | Same removal; the rune's label and description left with it. |
| `Textures/Experiments/Spellbook/Glyphs/WotF_Trigger_OnHit.png` | same path | The rune's icon. |
| `Textures/Experiments/Spellbook/TriggerRing.png` | same path | Texture of the removed `WotF_Fleck_TriggerRing` (also in the extracted XML above). |

### 2. Root elf race from the first draft

| Item | Reason |
| --- | --- |
| `Defs/Bodies/Bodies_Elf.xml`, `Defs/RulePack/RulePacks_Namers_Elf.xml`, `Defs/ThingDefs_Races/ElfPawnKind.xml`, `Defs/ThingDefs_Races/ElfRace.xml`, `Defs/ThingDefs_Trait/Trait_Trance.xml` | First-draft race made in passing; conflicts with the new setting and is rewritten in phase 4. |
| `Languages/ChineseSimplified/ElfRace.xml`, `Languages/ChineseSimplified/DefInjected/TraitDef/Trait_Trance.xml`, `Languages/{ChineseSimplified,English}/Strings/Names/Elf_Adult_Name_{Female,Male}.txt`, `.../Elf_Family_Name.txt` | Localisation of the same race and its Trance trait. |
| `Textures/Things/Elf/Bodies/Naked_Thin_{east,north,south}.png`, `Textures/Things/Elf/Heads/Average_Normal_{east,north,south}.png` | Only referenced by `Bodies_Elf.xml` / `ElfRace.xml`. |
| Kept on purpose: `Languages/*/Keyed/WotF_Plants.xml` | Its `WotF_MaturePlant_Failed_*` texts are still used by `PlantGrowthEffect`, which the Grow rune calls. |

### 3. Instant maturity

| Item | Reason |
| --- | --- |
| `1.7/Defs/Experiments/Plants.xml` | The new design does not let a cast ripen a crop directly. |
| `Source/WhisperOfTheForest/Abilities/CompAbilityEffect_WotFPlantGrowth.cs`, `CompProperties_AbilityWotFPlantGrowth.cs`, `CompProperties_WotFAbilityEffect.cs` | The ability comp pair of the removed `WotF_MaturePlant`, plus its shared comp-properties base, which lost every consumer. |
| `1.7/Languages/{ChineseSimplified,English}/DefInjected/AbilityDef/WotF_MaturePlant.xml` | Label and description of the removed ability. |
| **Kept on purpose**: `Source/WhisperOfTheForest/Abilities/PlantGrowthEffect.cs` | The spellbook `Grow` rune still executes it through `SpellExecutor`. It moves only when Grow becomes a fertility-multiplier growth effect. |

### 4. Three standalone missile psycasts

| Item | Reason |
| --- | --- |
| `1.7/Defs/Experiments/Missiles.Abilities.xml` | Extracted from `1.7/Defs/Experiments/Missiles.xml`: the `WotF_ArcaneMissiles`, `_Heavy`, `_Mender` AbilityDefs. Casting is unified through the spellbook. The file's damage Def, projectile ThingDef, two visual Defs and three impact Defs stay in `Missiles.xml`. |
| `Source/WhisperOfTheForest/Missiles/CompAbilityEffect_WotFArcaneMissiles.cs` | `CompProperties_AbilityWotFArcaneMissiles` + `CompAbilityEffect_WotFArcaneMissiles`, which only those three Defs used. |
| `1.7/Languages/{ChineseSimplified,English}/DefInjected/AbilityDef/WotF_ArcaneMissiles.xml` | Their labels and descriptions. |

### 5. Tech lab window and bottom-right entry

| Item | Reason |
| --- | --- |
| `Source/WhisperOfTheForest/UI/Window_TechLab.cs`, `TechLabUi.cs`, `TechLabEntryOverlay.cs`, `TechLabStyle.cs`, `Patch_PlaySettingsRowEntry.cs` | The experiment window and its play-settings row icon are removed. The mod keeps a settings page: `TechLabMod`/`TechLabSettings` became `WotfMod`/`WotfSettings` (rename, not a copy) and now only carry the debug-log switch. |
| **Kept on purpose**: `Source/WhisperOfTheForest/UI/TechLabDrawScope.cs` | It is a generic IMGUI state scope that `UI/Spellbook/` uses; the name is a leftover, the code is not lab UI. |

## 2026-11-05 - one visual system (Vfx)

The missile visual backend abstraction was merged into the Vfx system: `VfxWorker` /
`VfxService` / `MapComponent_Vfx` are now the only visual framework, and the missile trail is a
`FollowThing` Vfx effect like the railgun's shot is a `FixedDuration` one. The missile's backends
kept their work (rewritten in place as `VfxWorker`s); only the old contract they implemented is
deprecated. Def names (`WotF_Visual_RimUnity`, `WotF_Visual_Native`) are unchanged.

| Deprecated file | Classes | Reason |
| --- | --- | --- |
| `Source/WhisperOfTheForest/Missiles/ArcaneMissileVisualSource.cs` | `ArcaneMissileVisualSource`, `ArcaneMissileVisualFrame`, `ArcaneMissileImpactVisualContext` | Replaced by `VfxWorker` + `VfxRequest` (the missile sends a typed `ArcaneMissileVfxRequest`). Its map-frame/map-hidden static hooks are gone: the Vfx component drives every effect once per frame for every map, and a follow effect ends when its Thing does. The shared wobble helper moved to `Missiles/ArcaneMissileVfx.cs`. |

## 2026-11-05 - L2 test narrowing

Scope change: L2 covers system handoffs only. Single rune/form/effect parameter checks moved here;
the kept suite is `spellbook.active-slot-cast`, `spellbook.slots-survive-reload`,
`form.projectile-harm`, `reload.projectile-midflight`, `movement.terrain-stride`,
`movement.blink-stride`.

Original paths: `Source/WhisperOfTheForest/LabTests/`. Moved with a plain move: the folder was
never committed, so `git mv` refused it as "not under version control".

| Deprecated file | Classes | Reason |
| --- | --- | --- |
| `Source/WhisperOfTheForest/LabTests/AugmentTests.cs` | `Augment_Amplify`, `Augment_Area`, `Augment_Accelerate`, `Augment_Decelerate`, `Augment_Split` | Asserts design parameters: amplify multiplier, AOE radius, missile speed factor, split volley count. They change with the design and only produce false failures. |
| `Source/WhisperOfTheForest/LabTests/CompileTests.cs` | `Compile_RejectsIncompatibleChain` | Asserts the glyph compatibility table, a design parameter. |
| `Source/WhisperOfTheForest/LabTests/EffectTests.cs` | `Effect_HealTouch`, `Effect_Explosion`, `Effect_Grow` | Single-effect behaviour (how much heal, explosion damage, plant growth) - system internals, checked by logging instead. |
| `Source/WhisperOfTheForest/LabTests/FormTests.cs` | `Form_SelfHarm`, `Form_TouchHarm`, `Form_TouchOutOfReach` (`Form_ProjectileHarm` was split out and kept) | Asserts which form reaches how far and who it may wound: design parameters. |
| `Source/WhisperOfTheForest/LabTests/MovementTests.cs` | `Movement_TerrainStrideKeepsWalls` (`Movement_TerrainStride` and `Movement_BlinkStride` were split out and kept) | Movement implementation detail rather than a system handoff; the kept stride tests cover apparel -> pathing. |
| `Source/WhisperOfTheForest/LabTests/RailgunTests.cs` | `Railgun_HitsBehindWall`, `Railgun_DamagesWall`, `Railgun_AimBeyondCarrierRange` | The railgun composite rune is being reworked (penetration design changes), so its current pass/fail is not a basis for anything. Also asserts damage numbers and aim range. |
| `Source/WhisperOfTheForest/LabTests/TriggerTests.cs` | `Trigger_OnHitChain` | Trigger rune behaviour is being reworked together with the railgun; the assertion is a single-rune effect chain, not a system handoff. |
