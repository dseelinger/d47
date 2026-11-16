#!/usr/bin/env python3
"""Regenerate d47's table of the ring types each mined commodity comes from.

Not part of the build. Its output is committed as `src/D47.Core/Knowledge/RingMaterials.tsv`
and shipped as an embedded resource. Run it by hand over journal folders:

    python tools/gen-ring-materials.py <journal folder> [<journal folder> ...]
    python tools/gen-ring-materials.py --check <fixture folder>

What it reads
-------------
`Journal.*.log` under each folder, in name order within a folder. A `Scan` gives each ring's
`RingClass` by `Rings[].Name`. A `SupercruiseExit` or `Location` whose `BodyType` is `PlanetaryRing`
or `AsteroidCluster` puts the ship in the ring named by `Body`; a belt cluster `X A Belt Cluster 3`
is in the ring `X A Belt`. Any other `SupercruiseExit` or `Location`, and `SupercruiseEntry`,
`FSDJump`, `Docked`, `Died`, `Shutdown` and `LoadGame`, take the ship out of it. A
`ProspectedAsteroid` while in a ring is a prospect of that ring. `SAASignalsFound` on a body whose
name ends in `Ring` lists that ring's hotspots. A ring with no `Scan` has no class, and its
prospects and hotspots are left out. `Materials[].Name`, `MotherlodeMaterial` and `Signals[].Type`
are folded to lower case, and only cargo symbols in `Materials.tsv` are kept.

The rules
---------
A ring type is listed for a material seen in at least two distinct rings of that type: in a
prospect's `Materials`, as its `MotherlodeMaterial`, or as a ring hotspot.

A material never seen in a prospect's `Materials` but seen as a `MotherlodeMaterial` is `core`, and
its types share rank 1, in `BodyCatalogue.RingTypes` order.

Any other material is `laser`. Its types are ranked by the share of a type's prospects whose
`Materials` contain it; motherlodes do not count towards the share. Prospects in a ring with a
hotspot of that material are left out; a type left with fewer than 30 prospects uses all of them.
Types are taken by share, highest first, ties in `BodyCatalogue.RingTypes` order. The first type
has rank 1. A later type starts the next rank when the upper bound of its Wilson 95% interval is
below the lower bound of the first type in the current rank; otherwise it shares that rank.

Output
------
`RingMaterials.tsv`: a header, then `symbol, name, ring_type, rank, method`, one row per material
and ring type, by symbol and then in rank order. The measured shares and counts are printed to the
console and never written to the table. `--check` runs a fixture folder's journals and compares
the rows, header comments aside, with its `expected.tsv`.
"""
import argparse
import datetime
import json
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TABLE = os.path.join(ROOT, "src", "D47.Core", "Knowledge", "RingMaterials.tsv")
MATERIALS = os.path.join(ROOT, "src", "D47.Core", "Knowledge", "Materials.tsv")

RING_TYPES = ("Icy", "Metal Rich", "Metallic", "Rocky")
RING_CLASSES = {
    "eringclass_icy": "Icy",
    "eringclass_metalrich": "Metal Rich",
    "eringclass_metalic": "Metallic",
    "eringclass_rocky": "Rocky",
}
RING_BODY_TYPES = ("PlanetaryRing", "AsteroidCluster")
LEAVES_RING = {"SupercruiseEntry", "FSDJump", "Docked", "Died", "Shutdown", "LoadGame"}
EVENTS = LEAVES_RING | {"SupercruiseExit", "Location", "Scan", "SAASignalsFound", "ProspectedAsteroid"}
BELT_CLUSTER = re.compile(r"^(.* Belt) Cluster \d+$", re.IGNORECASE)
LEAST_RINGS = 2
LEAST_PROSPECTS = 30
Z = 1.96
COLUMNS = "symbol\tname\tring_type\trank\tmethod"


def cargo_names():
    """Cargo symbol to display name, from Materials.tsv."""
    names = {}
    with open(MATERIALS, encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("#") or line.startswith("symbol\t"):
                continue
            cells = line.rstrip("\n").split("\t")
            if len(cells) > 2 and cells[2] == "cargo":
                names[cells[0]] = cells[1]
    return names


def ring_of(body):
    match = BELT_CLUSTER.match(body)
    return (match.group(1) if match else body).lower()


def journals(folder):
    for dirpath, dirnames, files in os.walk(folder):
        dirnames.sort()
        for file in sorted(files):
            if file.startswith("Journal.") and file.endswith(".log"):
                yield os.path.join(dirpath, file)


def entries(path):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            if '"event"' not in line:
                continue
            try:
                entry = json.loads(line)
            except ValueError:
                continue
            if isinstance(entry, dict) and entry.get("event") in EVENTS:
                yield entry


class Evidence:
    """Ring classes, ring hotspots and prospects, keyed by lower-cased ring name."""

    def __init__(self, symbols):
        self.symbols = symbols
        self.classes = {}
        self.hotspots = {}
        self.prospects = []

    def symbol(self, value):
        text = str(value or "").lower()
        return text if text in self.symbols else None

    def read(self, path):
        ring = None
        for entry in entries(path):
            event = entry["event"]
            if event == "Scan":
                for item in entry.get("Rings") or []:
                    kind = RING_CLASSES.get(str(item.get("RingClass", "")).lower())
                    if kind and item.get("Name"):
                        self.classes[str(item["Name"]).lower()] = kind
            elif event == "SAASignalsFound":
                body = str(entry.get("BodyName") or "")
                if body.endswith("Ring"):
                    found = {self.symbol(s.get("Type")) for s in entry.get("Signals") or []}
                    self.hotspots.setdefault(body.lower(), set()).update(found - {None})
            elif event in ("SupercruiseExit", "Location"):
                body = entry.get("Body")
                ring = ring_of(str(body)) if body and entry.get("BodyType") in RING_BODY_TYPES else None
            elif event in LEAVES_RING:
                ring = None
            elif event == "ProspectedAsteroid" and ring is not None:
                materials = {self.symbol(m.get("Name")) for m in entry.get("Materials") or []} - {None}
                self.prospects.append((ring, materials, self.symbol(entry.get("MotherlodeMaterial"))))


def wilson(hits, total):
    if total == 0:
        return 0.0, 1.0
    p = hits / total
    denominator = 1 + Z * Z / total
    centre = (p + Z * Z / (2 * total)) / denominator
    half = Z * math.sqrt(p * (1 - p) / total + Z * Z / (4 * total * total)) / denominator
    return centre - half, centre + half


def build(folders, symbols, report):
    evidence = Evidence(symbols)
    for folder in folders:
        for path in journals(folder):
            evidence.read(path)

    classes = evidence.classes
    prospects = [(r, m, l, classes[r]) for r, m, l in evidence.prospects if r in classes]
    hotspots = {r: s for r, s in evidence.hotspots.items() if r in classes}
    report(f"rings with a class {len(classes)}, prospects {len(evidence.prospects)}, "
           f"prospects in a classed ring {len(prospects)}, hotspot rings with a class {len(hotspots)}")

    seen = {}
    lasered = set()
    cored = set()
    for ring, materials, lode, kind in prospects:
        for symbol in materials:
            seen.setdefault(symbol, {}).setdefault(kind, set()).add(ring)
            lasered.add(symbol)
        if lode:
            seen.setdefault(lode, {}).setdefault(kind, set()).add(ring)
            cored.add(lode)
    for ring, found in hotspots.items():
        for symbol in found:
            seen.setdefault(symbol, {}).setdefault(classes[ring], set()).add(ring)

    by_type = {kind: [p for p in prospects if p[3] == kind] for kind in RING_TYPES}
    rows = []
    for symbol in sorted(seen):
        kinds = [k for k in RING_TYPES if len(seen[symbol].get(k, ())) >= LEAST_RINGS]
        if not kinds:
            report(f"{symbol}: no ring type in {LEAST_RINGS} rings")
            continue
        if symbol not in lasered:
            if symbol not in cored:
                report(f"{symbol}: hotspots only, no prospect")
                continue
            report(f"{symbol}: core, {', '.join(kinds)}")
            rows.extend((symbol, kind, 1, "core") for kind in kinds)
            continue
        measured = []
        for kind in kinds:
            pool = [p for p in by_type[kind] if symbol not in hotspots.get(p[0], ())]
            if len(pool) < LEAST_PROSPECTS:
                pool = by_type[kind]
            hits = sum(1 for p in pool if symbol in p[1])
            low, high = wilson(hits, len(pool))
            measured.append((hits / len(pool) if pool else 0.0, RING_TYPES.index(kind), kind, hits, len(pool), low, high))
        measured.sort(key=lambda m: (-m[0], m[1]))
        rank = 1
        first = measured[0]
        for m in measured:
            if m is not first and m[6] < first[5]:
                rank += 1
                first = m
            report(f"{symbol}: {m[2]} rank {rank}, {m[3]}/{m[4]} = {m[0]:.3f}, Wilson {m[5]:.3f}-{m[6]:.3f}")
            rows.append((symbol, m[2], rank, "laser"))
    return rows


def lines(rows, symbols):
    return [COLUMNS] + [f"{s}\t{symbols[s]}\t{k}\t{r}\t{m}" for s, k, r, m in rows]


def header(count):
    return (
        "# Generated by tools/gen-ring-materials.py. Do not edit by hand — rerun the tool.\n"
        "# Source: Scan, SupercruiseExit, Location, SAASignalsFound and ProspectedAsteroid in the journal\n"
        "# folders given on the command line. A ring type is listed for a material seen in at least two\n"
        "# rings of that type; rank 1 is best, from how often a prospected rock of that type holds it, with\n"
        "# rings that have a hotspot of it left out. core: seen only as a motherlode, every type rank 1.\n"
        "# Game data is Frontier's, used under their media usage rules; see NOTICE.\n"
        f"# Rows: {count}. Built: {datetime.date.today().isoformat()}.\n"
    )


def data_lines(text):
    return [line for line in text.splitlines() if line and not line.startswith("#")]


def check(folder, symbols):
    actual = lines(build([folder], symbols, lambda _: None), symbols)
    with open(os.path.join(folder, "expected.tsv"), encoding="utf-8", newline="") as f:
        expected = data_lines(f.read())
    if actual != expected:
        print("ring-materials check failed", file=sys.stderr)
        for line in actual:
            print("  got      " + line, file=sys.stderr)
        for line in expected:
            print("  expected " + line, file=sys.stderr)
        return 1
    print(f"ring-materials check passed: {len(actual) - 1} rows")
    return 0


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("folders", nargs="*")
    p.add_argument("--check")
    a = p.parse_args()
    symbols = cargo_names()
    if a.check:
        return check(a.check, symbols)
    if not a.folders:
        p.error("give at least one journal folder")
    rows = build(a.folders, symbols, print)
    with open(TABLE, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(header(len(rows)))
        handle.write("\n".join(lines(rows, symbols)) + "\n")
    print(f"wrote {len(rows)} rows to {os.path.relpath(TABLE, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
