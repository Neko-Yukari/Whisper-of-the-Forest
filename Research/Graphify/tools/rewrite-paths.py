"""Rewrite staged graphify paths back to the real source locations.

The index step copies sources into ``runs/<stamp>/source/{mod,native}`` so a
snapshot is reproducible. Graphify records paths relative to that staging root.
This pass maps each staging prefix to its real absolute directory so queries
cite the file a reader can actually open, not the copy.

Usage:
  python rewrite-paths.py --graph-in run/graphify-out/graph.json \
      --graph-out graphify-out/graph.next.json \
      --map "mod=E:/.../Source/WhisperOfTheForest" \
      --map "native=E:/.../Graphify/native/source" \
      [--graph-meta graph-meta.json]
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def rewrite(value: str | None, mapping: dict[str, str]) -> str | None:
    if not value:
        return value
    normalised = str(value).replace("\\", "/")
    head, _, rest = normalised.partition("/")
    root = mapping.get(head)
    if root is None:
        return value
    if not rest:
        return root
    return f"{root.rstrip('/')}/{rest}"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--graph-in", required=True)
    parser.add_argument("--graph-out", required=True)
    parser.add_argument("--map", action="append", default=[], metavar="PREFIX=DIR")
    parser.add_argument(
        "--graph-meta",
        default=None,
        help="JSON object merged into the graph's top-level 'graph' block",
    )
    args = parser.parse_args()

    mapping: dict[str, str] = {}
    for item in args.map:
        prefix, sep, target = item.partition("=")
        if not sep:
            raise SystemExit(f"--map expects PREFIX=DIR, got {item!r}")
        mapping[prefix] = Path(target).resolve().as_posix()

    graph = json.loads(Path(args.graph_in).read_text(encoding="utf-8"))
    rewritten = 0
    for collection in ("nodes", "edges"):
        for record in graph.get(collection, []):
            current = record.get("source_file")
            updated = rewrite(current, mapping)
            if updated != current:
                record["source_file"] = updated
                rewritten += 1

    if args.graph_meta:
        graph.setdefault("graph", {}).update(
            json.loads(Path(args.graph_meta).read_text(encoding="utf-8"))
        )

    out = Path(args.graph_out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(graph, ensure_ascii=False), encoding="utf-8")

    # Anything still relative means a staging prefix had no mapping entry.
    remaining = sorted(
        {
            str(record.get("source_file", "")).split("/", 1)[0]
            for collection in ("nodes", "edges")
            for record in graph.get(collection, [])
            if record.get("source_file")
            and not str(record["source_file"]).startswith(("/", "\\"))
            and ":" not in str(record["source_file"]).split("/", 1)[0]
        }
    )
    print(
        json.dumps(
            {
                "rewritten_records": rewritten,
                "nodes": len(graph.get("nodes", [])),
                "edges": len(graph.get("edges", [])),
                "unmapped_staging_prefixes": remaining,
            }
        )
    )


if __name__ == "__main__":
    main()
