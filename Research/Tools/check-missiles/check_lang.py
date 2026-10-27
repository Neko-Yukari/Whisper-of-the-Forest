"""Static check for the missile feature's DefInjected files.

Real loader rule (LoadedLanguage.SetDefFieldAtPath): the folder under DefInjected selects the
def type, so each node key is `<defName>.<field path>` - a type prefix such as `AbilityDef.`
makes the first segment the def name and produces a translation error.
"""
import re
import sys
import xml.etree.ElementTree as ET

BASE = r"E:/SteamLibrary/steamapps/common/RimWorld/Moddev/tools/check-missiles"
MOD = r"E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest"


def fields(path):
    text = open(path, encoding="utf-8").read()
    return set(re.findall(r"field public [\w\.\<\>`\[\], ]+ (\w+)\s*$", text, re.M))


allowed = {
    "AbilityDef": fields(f"{BASE}/abilitydef.txt") | {"label", "description"},
    "DamageDef": fields(f"{BASE}/v-damagedef.txt") | {"label", "description"},
}

defined = set()
for def_node in ET.parse(f"{MOD}/1.7/Defs/Experiments/Missiles.xml").getroot():
    name = def_node.find("defName")
    if name is not None:
        defined.add(def_node.tag + "|" + name.text.strip())

files = [
    ("AbilityDef", "ChineseSimplified"),
    ("DamageDef", "ChineseSimplified"),
    ("AbilityDef", "English"),
    ("DamageDef", "English"),
]

problems = []
checked = 0

for def_type, lang in files:
    path = f"{MOD}/1.7/Languages/{lang}/DefInjected/{def_type}/"
    import os
    for name in os.listdir(path):
        if not name.endswith(".xml"):
            continue
        root = ET.parse(path + name).getroot()
        if root.tag != "LanguageData":
            problems.append(f"{lang}/{name}: root is <{root.tag}>")
        for node in root:
            checked += 1
            key = node.tag
            if not key[0].isalpha():
                problems.append(f"{lang}/{name}: bad key <{key}>")
                continue
            parts = key.split(".")
            if len(parts) < 2:
                problems.append(f"{lang}/{name}: key <{key}> has no field path")
                continue
            def_name, field = parts[0], parts[1]
            if f"{def_type}|{def_name}" not in defined:
                problems.append(f"{lang}/{name}: <{key}> first segment '{def_name}' is not a {def_type} defName in Missiles.xml")
            elif field not in allowed[def_type]:
                problems.append(f"{lang}/{name}: <{key}> field '{field}' does not exist on {def_type}")
            if def_type not in key:
                if def_name in allowed:
                    problems.append(f"{lang}/{name}: <{key}> looks like a type prefix")

# also make sure no experiment localization is left in the shared root Languages folder
import glob
for stale in glob.glob(f"{MOD}/Languages/**/*ArcaneMissile*.xml", recursive=True):
    problems.append("stale root-language file: " + stale)

print(f"checked {checked} translation keys")
print("problems:", len(problems))
for p in problems:
    print("  -", p)
sys.exit(1 if problems else 0)
