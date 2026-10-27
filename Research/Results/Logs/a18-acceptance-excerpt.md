# Player.log excerpt - a18-acceptance

Source (machine-local, not shipped): `Moddev/evidence/a18-acceptance-player.log`

| field | value |
| --- | --- |
| source bytes | 53545 |
| source sha256 | `8DAB34512EA28FC83EDD5C13F33914AFFF01A78AF1EC741957129838A6EFDF11` |
| total lines | 594 |
| mod-attributable lines | 11 |
| ERROR/WARN/Exception lines | 16 |
| of those mentioning this mod | 0 |

Redaction: local user profile paths replaced with `C:\Users\<user>`. Line numbers refer to the source log.

## Mod-attributable lines (first 40)

```
[8] Command:   E:\SteamLibrary\steamapps\common\RimWorld\RimWorld.dll -savedatafolder=E:/SteamLibrary/steamapps/common/RimWorld/Moddev/lab-profile -mod=NekoYukari.WhisperOfTheForest;brrainz.harmony;erdelf.HumanoidAlienRaces;Chezhou.RimUnity -wotflab=E:/SteamLibrary/steamapps/common/RimWorld/Moddev/lab-run -quicktest
[224] 2026-10-25 21:56:56.753  INFO [Verse] Command line arguments: -savedatafolder=E:/SteamLibrary/steamapps/common/RimWorld/Moddev/lab-profile -mod=NekoYukari.WhisperOfTheForest;brrainz.harmony;erdelf.HumanoidAlienRaces;Chezhou.RimUnity -wotflab=E:/SteamLibrary/steamapps/common/RimWorld/Moddev/lab-run -quicktest
[395] 2026-10-25 21:56:57.984  INFO [Verse] Running with command line mod list: brrainz.harmony, chezhou.rimunity, ludeon.rimworld, ludeon.rimworld.royalty, ludeon.rimworld.ideology, ludeon.rimworld.biotech, ludeon.rimworld.anomaly, ludeon.rimworld.odyssey, erdelf.humanoidalienraces, nekoyukari.whisperoftheforest
[491] 2026-10-25 21:57:07.536  INFO [Verse] [WotF-Lab] File lab enabled: E:\SteamLibrary\steamapps\common\RimWorld\Moddev\lab-run
[494]     at WhisperOfTheForest.Experimental.LabHost.ConfigureFromCommandLine() [0x111]
[495]     at WhisperOfTheForest.Experimental.Bootstrap..cctor() [0xE]
[515] 2026-10-25 21:57:07.554  INFO [Verse] [WotF-Lab] Harmony patched with id NekoYukari.WhisperOfTheForest.Experiments
[518]     at WhisperOfTheForest.Experimental.Bootstrap..cctor() [0x27]
[548]   - NekoYukari.WhisperOfTheForest
[567]   - NekoYukari.WhisperOfTheForest
[586]   - NekoYukari.WhisperOfTheForest
```

## First 25 ERROR/WARN/Exception lines (context, may be other mods)

```
[10] 2026-10-25 21:56:55.433  WARN [VulkanStartup] Vulkan fallback stage 3: a previous launch only booted at this stage, -ResetVulkanFallback clears it.
[17] 2026-10-25 21:56:55.434  WARN [VulkanStartup] Implicit Vulkan layers are disabled for this run (overlays and capture tools will not hook the game).
[36] 2026-10-25 21:56:55.897  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_NV_optimus" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[50] 2026-10-25 21:56:55.897  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_NV_present" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[64] 2026-10-25 21:56:55.897  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_EOS_Overlay" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[78] 2026-10-25 21:56:55.898  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_OW_OVERLAY" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[92] 2026-10-25 21:56:55.898  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_OW_OBS_HOOK" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[106] 2026-10-25 21:56:55.898  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_RTSS" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[120] 2026-10-25 21:56:55.898  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_VALVE_steam_overlay" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[134] 2026-10-25 21:56:55.898  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_VALVE_steam_fossilize" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[148] 2026-10-25 21:56:55.898  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_OBS_HOOK" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[275] 2026-10-25 21:56:57.606  WARN [Verse] Mod 绮罗边缘战争适配Rim War Kiiro Patch dependency (Ancot.KiiroRace) needs to have <downloadUrl> and/or <steamWorkshopUrl> specified.
[299] 2026-10-25 21:56:57.606  WARN [Verse] Mod 绮罗边缘战争适配Rim War Kiiro Patch dependency (torann.rimwar) needs to have <downloadUrl> and/or <steamWorkshopUrl> specified.
[323] 2026-10-25 21:56:57.883  WARN [Verse] Mod Milira SunLightGatheringTower Patch has a dependency (Ancot.MiliraRace) with empty display name.
[347] 2026-10-25 21:56:57.883  WARN [Verse] Mod Milira SunLightGatheringTower Patch dependency (Ancot.MiliraRace) needs to have <downloadUrl> and/or <steamWorkshopUrl> specified.
[371] 2026-10-25 21:56:57.926  WARN [Verse] Mod Ratkin Gene Expanded dependency (EoralMilk.PawnTextureOffsetAdjuster) needs to have <downloadUrl> and/or <steamWorkshopUrl> specified.
```

Attribution: lines attributed to other mods (CharacterEditor, AlienRaces) reflect the test modlist, not this mod.
