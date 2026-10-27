"""Static check: every XML element written for the missile feature must map to a public field
on the corresponding C# type, and Class attributes must resolve to known classes."""
import re
import sys
import xml.etree.ElementTree as ET

BASE = r"E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/check-missiles"
MOD = r"E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest"

GENERIC = {"defName", "label", "description", "ParentName", "Abstract", "Name", "MayRequire",
           "MayRequireAnyOf", "MayRequireAll", "Class", "Inherit"}


def fields(path):
    text = open(path, encoding="utf-8").read()
    return set(re.findall(r"field public [\w\.\<\>`\[\], ]+ (\w+)\s*$", text, re.M))


ability = fields(f"{BASE}/abilitydef.txt") | GENERIC
verb = fields(f"{BASE}/v-target.txt") | GENERIC
targeting = verb
comp = fields(f"{BASE}/compabilityeffect.txt") | GENERIC | {"compClass", "settings"}
damage = fields(f"{BASE}/v-damagedef.txt") | GENERIC
settings = fields(f"{BASE}/myfields.txt") | GENERIC

problems = []
notes = []


def expect(node, allowed, where):
    for child in node:
        if child.tag not in allowed:
            problems.append(f"{where}: unknown element <{child.tag}>")
        elif len(child):
            notes.append(f"{where}/{child.tag}: nested <{child[0].tag}>")


tree = ET.parse(f"{MOD}/1.7/Defs/Experiments/Missiles.xml")
root = tree.getroot()

defined = set()
for def_node in root:
    name = def_node.find("defName")
    if name is not None:
        defined.add(name.text.strip())

for def_node in root:
    tag = def_node.tag
    if tag == "DamageDef":
        expect(def_node, damage, "DamageDef")
    elif tag == "AbilityDef":
        expect(def_node, ability, "AbilityDef")
        vp = def_node.find("verbProperties")
        if vp is None:
            problems.append("AbilityDef: missing verbProperties")
        else:
            expect(vp, verb, "verbProperties")
            tp = vp.find("targetParams")
            if tp is None:
                problems.append("AbilityDef: missing targetParams")
            else:
                expect(tp, targeting, "targetParams")
        comps = def_node.find("comps")
        if comps is None or not len(comps):
            problems.append("AbilityDef: missing comps")
        else:
            for li in comps:
                cls = li.get("Class", "")
                if not cls.startswith("WhisperOfTheForest.Experimental.Missiles.") and not cls.startswith("CompProperties_"):
                    problems.append(f"comps: unexpected class {cls}")
                expect(li, comp, "comps/li")
                s = li.find("settings")
                if s is not None:
                    expect(s, settings, "settings")
        # damageDef reference must resolve to a def in this file or a known vanilla def
        dmg_ref = def_node.find(".//damageDef")
        if dmg_ref is not None and dmg_ref.text.strip() not in defined:
            problems.append(f"damageDef {dmg_ref.text.strip()} is not defined in Missiles.xml")

# MayRequire must be a real packageId
for def_node in root:
    may = def_node.get("MayRequire")
    if may and may != "Ludeon.RimWorld.Royalty":
        problems.append(f"MayRequire '{may}' is not the local Royalty packageId")

# every settings value must be present so XML defaults are explicit
default_settings = set()
for li in root.iter("li"):
    s = li.find("settings")
    if s is not None:
        default_settings = {child.tag for child in s}
missing = settings - GENERIC - default_settings
if missing:
    notes.append("settings fields not written in XML: " + ", ".join(sorted(missing)))

print("defined defs:", sorted(defined))
print("problems:", len(problems))
for p in problems:
    print("  -", p)
for n in notes:
    print("note:", n)
sys.exit(1 if problems else 0)
