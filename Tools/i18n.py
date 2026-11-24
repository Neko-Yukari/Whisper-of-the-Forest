#!/usr/bin/env python3
"""
Whisper of the Forest — Repository i18n Manager

Single source of truth under Tools/i18n/:
  - glossary.json     : canonical trilingual terminology (zh / en / ja)
  - keyed.json        : 1.7/Languages/<Lang>/Keyed/*.xml
  - def_injected.json : 1.7/Languages/<Lang>/DefInjected/<DefType>/*.xml
  - strings.json      : 1.7/Languages/<Lang>/Strings/**/*.txt

Commands:
  python Tools/i18n.py export   # Extract from 1.7/Languages + 1.7/Defs into Tools/i18n/*.json
  python Tools/i18n.py check    # Validate completeness, placeholders, rulesStrings, no CJK in EN
  python Tools/i18n.py apply    # Regenerate 1.7/Languages/{English,ChineseSimplified,Japanese}
  python Tools/i18n.py csv      # Export a flat Tools/i18n/translations.csv snapshot for spreadsheets
"""

import csv
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from xml.sax.saxutils import escape as xml_escape

REPO = Path(__file__).resolve().parent.parent
LANG_ROOT = REPO / "1.7" / "Languages"
DEFS_ROOT = REPO / "1.7" / "Defs"
I18N_DIR = REPO / "Tools" / "i18n"

LANG_MAP = {
    "zh": "ChineseSimplified",
    "en": "English",
    "ja": "Japanese",
}


def load_json(path: Path, default):
    if path.exists():
        return json.loads(path.read_text(encoding="utf-8"))
    return default


def save_json(path: Path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def build_defs_index() -> dict:
    """Index all Def XML elements by defName -> Element for English fallback extraction."""
    idx = {}
    for p in sorted(DEFS_ROOT.rglob("*.xml")):
        try:
            root = ET.parse(p).getroot()
        except Exception:
            continue
        for def_el in root:
            dn_el = def_el.find("defName")
            if dn_el is not None and dn_el.text:
                idx[dn_el.text.strip()] = def_el
    return idx


def resolve_def_value(defs_idx: dict, dotted_key: str, want_list: bool):
    """Resolve e.g. 'WotF_ManaNode.description' or 'WotF_RuinSeek_Core.questNameRules.rulesStrings'."""
    parts = dotted_key.split(".")
    if len(parts) < 2 or parts[0] not in defs_idx:
        return None
    cur = defs_idx[parts[0]]
    for seg in parts[1:]:
        if seg.isdigit():
            children = list(cur)
            i = int(seg)
            if 0 <= i < len(children):
                cur = children[i]
            else:
                return None
        else:
            nxt = cur.find(seg)
            if nxt is None:
                return None
            cur = nxt
    if want_list:
        items = [li.text or "" for li in cur.findall("li")]
        return items if items else None
    return cur.text if cur.text is not None else ""


def parse_lang_xml_tree(lang_folder: str, subdir: str) -> dict:
    """Return {(rel_file, tag): {'kind': 'scalar'|'list', 'val': str|list[str]}}."""
    base = LANG_ROOT / lang_folder / subdir
    out = {}
    if not base.exists():
        return out
    for p in sorted(base.rglob("*.xml")):
        rel = p.relative_to(LANG_ROOT / lang_folder).as_posix()
        root = ET.parse(p).getroot()
        for child in root:
            if not isinstance(child.tag, str):
                continue
            children = list(child)
            if children:
                out[(rel, child.tag)] = {
                    "kind": "list",
                    "val": [li.text or "" for li in children],
                }
            else:
                out[(rel, child.tag)] = {
                    "kind": "scalar",
                    "val": child.text or "",
                }
    return out


def parse_lang_txt_tree(lang_folder: str) -> dict:
    base = LANG_ROOT / lang_folder / "Strings"
    out = {}
    if not base.exists():
        return out
    for p in sorted(base.rglob("*.txt")):
        rel = p.relative_to(LANG_ROOT / lang_folder).as_posix()
        lines = [line.strip() for line in p.read_text(encoding="utf-8").splitlines() if line.strip()]
        out[rel] = lines
    return out


def cmd_export():
    I18N_DIR.mkdir(parents=True, exist_ok=True)
    defs_idx = build_defs_index()

    # Load existing JSON to preserve any existing JA (or hand-edited EN) entries
    old_keyed = {(e["file"], e["key"]): e for e in load_json(I18N_DIR / "keyed.json", [])}
    old_def = {(e["file"], e["key"]): e for e in load_json(I18N_DIR / "def_injected.json", [])}
    old_str = {e["file"]: e for e in load_json(I18N_DIR / "strings.json", [])}

    # 1. Keyed
    zh_k = parse_lang_xml_tree("ChineseSimplified", "Keyed")
    en_k = parse_lang_xml_tree("English", "Keyed")
    ja_k = parse_lang_xml_tree("Japanese", "Keyed")
    all_k_keys = sorted(set(zh_k.keys()) | set(en_k.keys()))
    keyed_rows = []
    for rel, tag in all_k_keys:
        prev = old_keyed.get((rel, tag), {})
        zh_v = zh_k.get((rel, tag), {}).get("val", prev.get("zh", ""))
        en_v = en_k.get((rel, tag), {}).get("val", prev.get("en", ""))
        ja_v = ja_k.get((rel, tag), {}).get("val", prev.get("ja", ""))
        keyed_rows.append({
            "file": rel,
            "key": tag,
            "zh": zh_v,
            "en": en_v,
            "ja": ja_v,
        })
    save_json(I18N_DIR / "keyed.json", keyed_rows)

    # 2. DefInjected
    zh_d = parse_lang_xml_tree("ChineseSimplified", "DefInjected")
    en_d = parse_lang_xml_tree("English", "DefInjected")
    ja_d = parse_lang_xml_tree("Japanese", "DefInjected")
    all_d_keys = sorted(set(zh_d.keys()) | set(en_d.keys()))
    def_rows = []
    resolved_from_defs = 0
    missing_en_def = 0
    for rel, tag in all_d_keys:
        prev = old_def.get((rel, tag), {})
        meta = zh_d.get((rel, tag)) or en_d.get((rel, tag))
        kind = meta["kind"]
        zh_v = zh_d.get((rel, tag), {}).get("val", prev.get("zh", [] if kind == "list" else ""))
        en_source = "English/DefInjected"
        if (rel, tag) in en_d:
            en_v = en_d[(rel, tag)]["val"]
        else:
            fallback = resolve_def_value(defs_idx, tag, want_list=(kind == "list"))
            if fallback is not None:
                en_v = fallback
                en_source = "1.7/Defs"
                resolved_from_defs += 1
            else:
                en_v = prev.get("en", [] if kind == "list" else "")
                en_source = "missing"
                missing_en_def += 1
        ja_v = ja_d.get((rel, tag), {}).get("val", prev.get("ja", [] if kind == "list" else ""))
        def_rows.append({
            "file": rel,
            "key": tag,
            "kind": kind,
            "en_source": en_source,
            "zh": zh_v,
            "en": en_v,
            "ja": ja_v,
        })
    save_json(I18N_DIR / "def_injected.json", def_rows)

    # 3. Strings
    zh_s = parse_lang_txt_tree("ChineseSimplified")
    en_s = parse_lang_txt_tree("English")
    ja_s = parse_lang_txt_tree("Japanese")
    str_rows = []
    for rel in sorted(set(zh_s.keys()) | set(en_s.keys())):
        prev = old_str.get(rel, {})
        str_rows.append({
            "file": rel,
            "zh": zh_s.get(rel, prev.get("zh", [])),
            "en": en_s.get(rel, prev.get("en", [])),
            "ja": ja_s.get(rel, prev.get("ja", [])),
        })
    save_json(I18N_DIR / "strings.json", str_rows)

    print(f"EXPORT OK:")
    print(f"  keyed.json        : {len(keyed_rows)} entries")
    print(f"  def_injected.json : {len(def_rows)} entries (from 1.7/Defs fallback={resolved_from_defs}, missing_en={missing_en_def})")
    print(f"  strings.json      : {len(str_rows)} files")


PLACEHOLDER_RE = re.compile(r"\{[0-9A-Za-z_.:+-]+\}")
BRACKET_RE = re.compile(r"\[[A-Za-z0-9_]+\]")
CJK_RE = re.compile(r"[\u4e00-\u9fff\u3040-\u30ff]")


def extract_placeholders(s: str) -> list:
    return sorted(PLACEHOLDER_RE.findall(s))


def check_rules_strings(lang: str, key: str, items: list, ref_items: list, errors: list):
    if not items:
        errors.append(f"[{lang}] {key}: empty rulesStrings list")
        return
    ref_empty_lhs = set()
    if isinstance(ref_items, list):
        for rline in ref_items:
            if "->" in rline:
                rl, rr = rline.split("->", 1)
                if not rr.strip():
                    ref_empty_lhs.add(rl.strip())
    for idx, line in enumerate(items):
        if "->" not in line:
            errors.append(f"[{lang}] {key}[{idx}]: missing '->' in '{line[:60]}'")
            continue
        lhs, rhs = line.split("->", 1)
        if not lhs.strip():
            errors.append(f"[{lang}] {key}[{idx}]: empty keyword before '->'")
        elif not rhs.strip() and lhs.strip() not in ref_empty_lhs:
            errors.append(f"[{lang}] {key}[{idx}]: empty text after '->' for '{lhs.strip()}'")


def cmd_check(langs=("zh", "en", "ja")):
    errors = []
    warnings = []

    keyed_rows = load_json(I18N_DIR / "keyed.json", [])
    def_rows = load_json(I18N_DIR / "def_injected.json", [])
    str_rows = load_json(I18N_DIR / "strings.json", [])

    # 1. Keyed checks
    for row in keyed_rows:
        zh = row.get("zh", "")
        en = row.get("en", "")
        allowed_ph = set(extract_placeholders(zh)) | set(extract_placeholders(en))
        req_ph = set(extract_placeholders(zh)) & set(extract_placeholders(en))
        for lang in langs:
            val = row.get(lang, "")
            if not isinstance(val, str) or not val.strip():
                errors.append(f"[Keyed/{lang}] {row['file']}::{row['key']}: empty")
                continue
            ph = set(extract_placeholders(val))
            extra = ph - allowed_ph
            missing = req_ph - ph
            if extra or missing:
                errors.append(f"[Keyed/{lang}] {row['key']}: placeholders {sorted(ph)} mismatch (missing={sorted(missing)}, extra={sorted(extra)})")
            if lang == "en" and CJK_RE.search(val):
                errors.append(f"[Keyed/en] {row['key']}: contains CJK characters")

    # 2. DefInjected checks
    for row in def_rows:
        kind = row["kind"]
        key = row["key"]
        zh = row.get("zh")
        en = row.get("en")
        is_rules = key.endswith(".rulesStrings") or key.endswith(".rulesFiles")
        for lang in langs:
            val = row.get(lang)
            if kind == "list":
                if not isinstance(val, list) or len(val) == 0:
                    errors.append(f"[Def/{lang}] {row['file']}::{key}: empty list")
                    continue
                if is_rules:
                    check_rules_strings(lang, key, val, zh, errors)
                elif isinstance(zh, list) and len(val) != len(zh):
                    errors.append(f"[Def/{lang}] {key}: list length {len(val)} != zh {len(zh)}")
                for i, item in enumerate(val):
                    if not isinstance(item, str):
                        errors.append(f"[Def/{lang}] {key}[{i}]: non-string item")
                    elif not item.strip() and not is_rules:
                        errors.append(f"[Def/{lang}] {key}[{i}]: empty item")
                    if lang == "en" and isinstance(item, str) and CJK_RE.search(item):
                        errors.append(f"[Def/en] {key}[{i}]: contains CJK characters")
            else:
                if not isinstance(val, str) or not val.strip():
                    errors.append(f"[Def/{lang}] {row['file']}::{key}: empty")
                    continue
                allowed_ph = set(extract_placeholders(zh or "")) | set(extract_placeholders(en or ""))
                req_ph = set(extract_placeholders(zh or "")) & set(extract_placeholders(en or "")) if (en or "").strip() else set(extract_placeholders(zh or ""))
                ph = set(extract_placeholders(val))
                extra = ph - allowed_ph
                missing = req_ph - ph
                if extra or missing:
                    errors.append(f"[Def/{lang}] {key}: placeholders {sorted(ph)} mismatch (missing={sorted(missing)}, extra={sorted(extra)})")
                if lang == "en" and CJK_RE.search(val):
                    errors.append(f"[Def/en] {key}: contains CJK characters")

    # 3. Strings checks
    for row in str_rows:
        zh = row.get("zh", [])
        for lang in langs:
            val = row.get(lang, [])
            if not isinstance(val, list) or len(val) == 0:
                errors.append(f"[Strings/{lang}] {row['file']}: empty list")
            elif len(val) != len(zh):
                warnings.append(f"[Strings/{lang}] {row['file']}: count {len(val)} != zh {len(zh)}")
            if lang == "en":
                for item in val:
                    if CJK_RE.search(item):
                        errors.append(f"[Strings/en] {row['file']}: contains CJK '{item}'")

    for w in warnings[:20]:
        print("WARN:", w)
    for e in errors[:50]:
        print("FAIL:", e)
    print(f"CHECK SUMMARY: langs={','.join(langs)} errors={len(errors)} warnings={len(warnings)}")
    if errors:
        sys.exit(1)


def write_xml_file(path: Path, entries: list):
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = ['<?xml version="1.0" encoding="utf-8"?>', "<LanguageData>"]
    for key, kind, val in entries:
        if kind == "list":
            lines.append(f"  <{key}>")
            for item in val:
                lines.append(f"    <li>{xml_escape(item)}</li>")
            lines.append(f"  </{key}>")
        else:
            lines.append(f"  <{key}>{xml_escape(val)}</{key}>")
    lines.append("</LanguageData>")
    content = "\n".join(lines) + "\n"
    # Verify XML parses cleanly before writing
    ET.fromstring(content)
    path.write_text(content, encoding="utf-8")


def cmd_apply(langs=("zh", "en", "ja")):
    keyed_rows = load_json(I18N_DIR / "keyed.json", [])
    def_rows = load_json(I18N_DIR / "def_injected.json", [])
    str_rows = load_json(I18N_DIR / "strings.json", [])

    for lang in langs:
        folder = LANG_MAP[lang]
        by_file = {}
        for row in keyed_rows:
            val = row.get(lang, "")
            if val != "":
                by_file.setdefault(row["file"], []).append((row["key"], "scalar", val))
        for row in def_rows:
            val = row.get(lang)
            if (row["kind"] == "list" and val) or (row["kind"] == "scalar" and val != ""):
                by_file.setdefault(row["file"], []).append((row["key"], row["kind"], val))

        written = 0
        for rel, entries in sorted(by_file.items()):
            write_xml_file(LANG_ROOT / folder / rel, entries)
            written += 1

        for row in str_rows:
            val = row.get(lang, [])
            if val:
                out_p = LANG_ROOT / folder / row["file"]
                out_p.parent.mkdir(parents=True, exist_ok=True)
                out_p.write_text("\n".join(val) + "\n", encoding="utf-8")
                written += 1

        print(f"APPLY {folder:18s}: wrote {written} files")


def cmd_csv():
    keyed_rows = load_json(I18N_DIR / "keyed.json", [])
    def_rows = load_json(I18N_DIR / "def_injected.json", [])
    out_csv = I18N_DIR / "translations.csv"
    with out_csv.open("w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f)
        w.writerow(["section", "file", "key", "kind", "zh", "en", "ja"])
        for r in keyed_rows:
            w.writerow(["Keyed", r["file"], r["key"], "scalar", r.get("zh", ""), r.get("en", ""), r.get("ja", "")])
        for r in def_rows:
            zh = "\n".join(r["zh"]) if r["kind"] == "list" else r.get("zh", "")
            en = "\n".join(r["en"]) if r["kind"] == "list" else r.get("en", "")
            ja = "\n".join(r["ja"]) if r["kind"] == "list" else r.get("ja", "")
            w.writerow(["DefInjected", r["file"], r["key"], r["kind"], zh, en, ja])
    print(f"CSV OK: {out_csv} ({len(keyed_rows) + len(def_rows)} rows)")


def cmd_merge(shards_dir: Path):
    keyed_rows = load_json(I18N_DIR / "keyed.json", [])
    def_rows = load_json(I18N_DIR / "def_injected.json", [])
    str_rows = load_json(I18N_DIR / "strings.json", [])

    k_map = {(r["file"], r["key"]): r for r in keyed_rows}
    d_map = {(r["file"], r["key"]): r for r in def_rows}
    s_map = {r["file"]: r for r in str_rows}

    merged_k, merged_d, merged_s = 0, 0, 0
    for p in sorted(shards_dir.glob("*.json")):
        payload = json.loads(p.read_text(encoding="utf-8"))
        t = payload.get("type", "")
        if t == "keyed":
            for r in payload.get("rows", []):
                dst = k_map.get((r["file"], r["key"]))
                if dst is not None:
                    dst["en"] = r["en"]
                    dst["ja"] = r["ja"]
                    merged_k += 1
        elif t in ("def_injected", "def_injected_and_strings"):
            for r in payload.get("rows", []):
                dst = d_map.get((r["file"], r["key"]))
                if dst is not None:
                    dst["en"] = r["en"]
                    dst["ja"] = r["ja"]
                    merged_d += 1
            for sr in payload.get("strings", []):
                dst = s_map.get(sr["file"])
                if dst is not None:
                    dst["en"] = sr["en"]
                    dst["ja"] = sr["ja"]
                    merged_s += 1

    save_json(I18N_DIR / "keyed.json", keyed_rows)
    save_json(I18N_DIR / "def_injected.json", def_rows)
    save_json(I18N_DIR / "strings.json", str_rows)
    print(f"MERGE OK: keyed={merged_k}/{len(keyed_rows)}, def_injected={merged_d}/{len(def_rows)}, strings={merged_s}/{len(str_rows)}")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python Tools/i18n.py [export|check|apply|csv|merge <dir>] [--langs=zh,en,ja]")
        sys.exit(2)
    cmd = sys.argv[1]
    langs = ("zh", "en", "ja")
    for a in sys.argv[2:]:
        if a.startswith("--langs="):
            langs = tuple(x.strip() for x in a.split("=", 1)[1].split(",") if x.strip())
    if cmd == "export":
        cmd_export()
    elif cmd == "check":
        cmd_check(langs)
    elif cmd == "apply":
        cmd_apply(langs)
    elif cmd == "csv":
        cmd_csv()
    elif cmd == "merge":
        if len(sys.argv) < 3:
            print("Usage: python Tools/i18n.py merge <shards_dir>")
            sys.exit(2)
        cmd_merge(Path(sys.argv[2]))
    else:
        print(f"Unknown command: {cmd}")
        sys.exit(2)
