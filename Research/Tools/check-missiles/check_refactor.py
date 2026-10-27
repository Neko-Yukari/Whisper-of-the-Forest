"""Static wiring check for the Def-selected missile visuals / impact strategies.

Verifies that the XML defines exactly the contracted Def names, that every class referenced by the
Defs exists in the compiled assembly, that the ability's settings select them, and that both
selections are scribed. Complements the runtime harness (which executes the real logic).
"""
import re
import sys
import xml.etree.ElementTree as ET

BASE = r"E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/check-missiles"
MOD = r"E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest"

problems = []

# --- Defs -------------------------------------------------------------------------------------
tree = ET.parse(f"{MOD}/1.7/Defs/Experiments/Missiles.xml")
root = tree.getroot()

defs_by_name = {}
for node in root:
    name = node.find("defName")
    if name is not None:
        defs_by_name[name.text.strip()] = (node.tag, node)

required = {
    "WotF_Visual_RimUnity": ("WhisperOfTheForest.Experimental.Missiles.ArcaneMissileVisualDef",
                             "WhisperOfTheForest.Experimental.Missiles.ArcaneMissileVisualRimUnity"),
    "WotF_Visual_Native": ("WhisperOfTheForest.Experimental.Missiles.ArcaneMissileVisualDef",
                           "WhisperOfTheForest.Experimental.Missiles.ArcaneMissileVisualNative"),
    "WotF_Impact_Damage": ("WhisperOfTheForest.Experimental.Missiles.ArcaneMissileImpactDef",
                           "WhisperOfTheForest.Experimental.Missiles.ArcaneMissileImpactWorker_Damage"),
    "WotF_Impact_Heal": ("WhisperOfTheForest.Experimental.Missiles.ArcaneMissileImpactDef",
                         "WhisperOfTheForest.Experimental.Missiles.ArcaneMissileImpactWorker_Heal"),
    "WotF_Impact_Explode": ("WhisperOfTheForest.Experimental.Missiles.ArcaneMissileImpactDef",
                            "WhisperOfTheForest.Experimental.Missiles.ArcaneMissileImpactWorker_Explode"),
}

dll_types = open(f"{BASE}/dll-types.txt", encoding="utf-8").read()

for def_name, (def_type, class_name) in required.items():
    entry = defs_by_name.get(def_name)
    if entry is None:
        problems.append(f"missing Def {def_name}")
        continue
    tag, node = entry
    # Mod-defined Def subclasses must appear under their fully qualified type name; a short tag
    # makes the game log "Type X is not a Def type or could not be found".
    if tag != def_type:
        problems.append(f"{def_name}: root tag <{tag}> must be the fully qualified <{def_type}>")
    field = "visualClass" if def_type.endswith("ArcaneMissileVisualDef") else "workerClass"
    value = node.find(field)
    if value is None or value.text.strip() != class_name:
        problems.append(f"{def_name}: {field} should be {class_name}")
    if f"class {class_name} " not in dll_types and f"class {class_name}:" not in dll_types:
        problems.append(f"{def_name}: {class_name} is not in the compiled missile assembly")

# --- Ability selection ------------------------------------------------------------------------
ability = defs_by_name.get("WotF_ArcaneMissiles", (None, None))[1]
settings_node = None
if ability is not None:
    for li in ability.iter("li"):
        candidate = li.find("settings")
        if candidate is not None:
            settings_node = candidate

if settings_node is None:
    problems.append("ability comp settings block not found")
else:
    for field, target in (("visualDef", "WotF_Visual_RimUnity"), ("impactDef", "WotF_Impact_Damage")):
        node = settings_node.find(field)
        if node is None:
            problems.append(f"settings missing <{field}>")
        elif node.text.strip() not in defs_by_name:
            problems.append(f"settings <{field}> references undefined Def {node.text.strip()}")
        elif node.text.strip() != target:
            problems.append(f"settings <{field}> is {node.text.strip()}, expected {target} by default")

# --- Save wiring ------------------------------------------------------------------------------
settings_source = open(f"{MOD}/Source/WhisperOfTheForest/Missiles/ArcaneMissileSettings.cs", encoding="utf-8").read()
for field in ("visualDef", "impactDef"):
    if f'Scribe_Defs.Look(ref {field}, "{field}")' not in settings_source:
        problems.append(f"ArcaneMissileSettings.ExposeData does not scribe {field}")

# --- Native projectile Def --------------------------------------------------------------------
PROJECTILE_DEF = "WotF_ArcaneMissileProjectile"
PROJECTILE_CLASS = "WhisperOfTheForest.Experimental.Missiles.Projectile_ArcaneMissiles"
entry = defs_by_name.get(PROJECTILE_DEF)
if entry is None:
    problems.append(f"missing projectile ThingDef {PROJECTILE_DEF}")
else:
    tag, node = entry
    if tag != "ThingDef":
        problems.append(f"{PROJECTILE_DEF}: root tag <{tag}> must be <ThingDef>")
    if node.get("ParentName") != "BaseBullet":
        problems.append(f"{PROJECTILE_DEF}: must derive from vanilla BaseBullet (Projectile category)")
    thing_class = node.find("thingClass")
    if thing_class is None or thing_class.text.strip() != PROJECTILE_CLASS:
        problems.append(f"{PROJECTILE_DEF}: thingClass should be {PROJECTILE_CLASS}")
    drawer = node.find("drawerType")
    if drawer is None or drawer.text.strip() != "None":
        problems.append(f"{PROJECTILE_DEF}: drawerType must be None (the visual backend draws it)")
    if node.find("projectile") is None:
        problems.append(f"{PROJECTILE_DEF}: a BaseBullet child needs a <projectile> block")

# --- Contract surface -------------------------------------------------------------------------
launcher = open(f"{MOD}/Source/WhisperOfTheForest/Missiles/ArcaneMissileLauncher.cs", encoding="utf-8").read()
for signature in ("public static int Fire(", "public static int FireAtCell(", "public static int FireAtThing("):
    if signature not in launcher:
        problems.append("launcher missing " + signature)

settings_source_text = settings_source
for field in ("public ArcaneMissileVisualDef visualDef;", "public ArcaneMissileImpactDef impactDef;"):
    if field not in settings_source_text:
        problems.append("settings missing field: " + field)
if "public ArcaneMissileImpactMode impactMode" not in settings_source_text:
    problems.append("settings lost the original impactMode enum field")

# --- Native ownership (Phase 5) ---------------------------------------------------------------
projectile = open(f"{MOD}/Source/WhisperOfTheForest/Missiles/Projectile_ArcaneMissiles.cs", encoding="utf-8").read()
for marker, why in (
    ("public class Projectile_ArcaneMissiles : Projectile", "the carrier must derive from Verse.Projectile"),
    ("protected override void TickInterval(int delta)", "the homing step must run inside the game's own tick"),
    ("public override Vector3 ExactPosition", "the model, not the thing cell, must own the drawn point"),
    ("public override void ExposeData()", "the flight state must be scribed with the projectile"),
    ("public override void Destroy(", "visual cleanup must hook the game's destroy chain"),
):
    if marker not in projectile:
        problems.append(f"Projectile_ArcaneMissiles: {why} (missing `{marker}`)")

# The launcher must hand the projectile to the game instead of keeping the old simulation list.
for marker, why in (
    ("ThingMaker.MakeThing(projectileDef)", "the missile must be created as a ThingDef thing"),
    ("GenSpawn.Spawn(projectile", "the game must spawn/register the projectile"),
):
    if marker not in launcher:
        problems.append(f"launcher: {why} (missing `{marker}`)")

# --- Core independence ------------------------------------------------------------------------
component = open(f"{MOD}/Source/WhisperOfTheForest/Missiles/MapComponent_ArcaneMissiles.cs", encoding="utf-8").read()
for concrete in ("ArcaneMissileVisualRimUnity", "ArcaneMissileVisualNative"):
    if concrete in component:
        problems.append("MapComponent still references concrete backend " + concrete)
for marker, why in (
    ("List<ArcaneMissile>", "MapComponent must not own a second simulation list"),
    ("state.Tick(", "MapComponent must not advance missile gameplay"),
    ("missile.TickVisual()", "the projectile owns its per-tick visual, so the component must not also drive it"),
):
    if marker in component:
        problems.append("MapComponent: " + why)

print(f"defs: {len(defs_by_name)} (required {len(required)})")
print("problems:", len(problems))
for problem in problems:
    print("  -", problem)
sys.exit(1 if problems else 0)
