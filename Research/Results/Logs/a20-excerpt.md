# Player.log excerpt - a20

Source (machine-local, not shipped): `Moddev/evidence/a20-player.log`

| field | value |
| --- | --- |
| source bytes | 112812 |
| source sha256 | `CC55B088F2D85BBF5B8E34C10AEAEA763E93E571922B1F259FC99A38996C71A6` |
| total lines | 1159 |
| mod-attributable lines | 8 |
| ERROR/WARN/Exception lines | 17 |
| of those mentioning this mod | 0 |

Redaction: local user profile paths replaced with `C:\Users\<user>`. Line numbers refer to the source log.

## Mod-attributable lines (first 40)

```
[482] 2026-10-27 14:53:14.727  INFO [Verse] [WotF-Lab] File lab enabled: E:\SteamLibrary\steamapps\common\RimWorld\Moddev\lab-run
[485]     at WhisperOfTheForest.Experimental.LabHost.ConfigureFromCommandLine() [0x111]
[486]     at WhisperOfTheForest.Experimental.Bootstrap..cctor() [0xE]
[506] 2026-10-27 14:53:14.737  INFO [Verse] [WotF-Lab] Harmony patched with id NekoYukari.WhisperOfTheForest.Experiments
[509]     at WhisperOfTheForest.Experimental.Bootstrap..cctor() [0x27]
[1009]   - NekoYukari.WhisperOfTheForest
[1059]   - NekoYukari.WhisperOfTheForest
[1121]   - NekoYukari.WhisperOfTheForest
```

## First 25 ERROR/WARN/Exception lines (context, may be other mods)

```
[10] 2026-10-27 14:53:02.036  WARN [VulkanStartup] Vulkan fallback stage 3: a previous launch only booted at this stage, -ResetVulkanFallback clears it.
[17] 2026-10-27 14:53:02.037  WARN [VulkanStartup] Implicit Vulkan layers are disabled for this run (overlays and capture tools will not hook the game).
[36] 2026-10-27 14:53:02.420  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_NV_optimus" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[50] 2026-10-27 14:53:02.420  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_NV_present" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[64] 2026-10-27 14:53:02.420  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_EOS_Overlay" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[78] 2026-10-27 14:53:02.420  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_OW_OVERLAY" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[92] 2026-10-27 14:53:02.420  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_OW_OBS_HOOK" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[106] 2026-10-27 14:53:02.420  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_RTSS" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[120] 2026-10-27 14:53:02.420  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_VALVE_steam_overlay" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[134] 2026-10-27 14:53:02.420  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_VALVE_steam_fossilize" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[148] 2026-10-27 14:53:02.420  WARN [Vulkan] [General] [Loader Message] Layer "VK_LAYER_OBS_HOOK" forced disabled because name matches filter of env var 'VK_LOADER_LAYERS_DISABLE'.
[283] 2026-10-27 14:53:04.187  WARN [Verse] Mod 绮罗边缘战争适配Rim War Kiiro Patch dependency (Ancot.KiiroRace) needs to have <downloadUrl> and/or <steamWorkshopUrl> specified.
[307] 2026-10-27 14:53:04.187  WARN [Verse] Mod 绮罗边缘战争适配Rim War Kiiro Patch dependency (torann.rimwar) needs to have <downloadUrl> and/or <steamWorkshopUrl> specified.
[331] 2026-10-27 14:53:04.302  WARN [Verse] Mod Milira SunLightGatheringTower Patch has a dependency (Ancot.MiliraRace) with empty display name.
[355] 2026-10-27 14:53:04.302  WARN [Verse] Mod Milira SunLightGatheringTower Patch dependency (Ancot.MiliraRace) needs to have <downloadUrl> and/or <steamWorkshopUrl> specified.
[379] 2026-10-27 14:53:04.320  WARN [Verse] Mod Ratkin Gene Expanded dependency (EoralMilk.PawnTextureOffsetAdjuster) needs to have <downloadUrl> and/or <steamWorkshopUrl> specified.
[1070] 2026-10-27 14:53:37.627  WARN [Verse] Loaded file (Map) is from version 1.7.4985 rev1269, we are running version 1.7.4990 rev1069.
```

Attribution: lines attributed to other mods (CharacterEditor, AlienRaces) reflect the test modlist, not this mod.
