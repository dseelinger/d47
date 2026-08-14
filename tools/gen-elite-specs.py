#!/usr/bin/env python3
"""Regenerate d47's ship and module specification table.

This is *not* part of the build. Its output is committed as
`src/D47.Core/Knowledge/EliteSpecifications.tsv` and shipped as an embedded resource; the
app never runs this and never reaches the network for it. Run it when Frontier ships a
new hull or rebalances one:

    python tools/gen-elite-specs.py

Why this table exists at all
----------------------------
Phase 14 asks d47 to answer "how fast is a Python", "does a Type-9 need a large pad" and
"what is a 5A drive's optimal mass". None of that is in the journal: the journal reports
what *this* Commander is flying, not what a hull is capable of before they buy one.

So the choice was a table or no feature, and a hand-written one would be exactly the
confidently-invented game data the guardrails exist to prevent — a wrong top speed is
indistinguishable from the feature working. A *derived* table is checkable, refreshable
and traceable to something that is not this file's author's memory. Same argument as
`gen-material-grades.py`, at a larger scale.

Two sources, joined on Frontier's own ids
-----------------------------------------
`EDCD/FDevIDs` is the naming authority: `shipyard.csv` maps a hull id to the symbol the
journal writes, and `outfitting.csv` maps a module id to its name, mount, class and
rating. It carries no performance figures.

`EDCD/coriolis-data` carries the figures — speed, boost, armour, shields, module mass,
power draw, and the drive numbers a jump range is computed from. Its ship and module
entries each carry an `edID`, which is the same id, so the two join cleanly and the join
is the check: a ship in one and not the other is a gap this script refuses to hide.

Note on the data itself: coriolis-data's LICENSE.md is explicit that the JSON is game
data belonging to Frontier and that its MIT grant covers only the site's own code. That is
the same standing as FDevIDs' CSVs, which d47 already derives a table from, and the reason
this output is a *derived index of facts about a game* rather than a copy of a work.

Why a TSV resource and not a generated .g.cs
--------------------------------------------
`MaterialGrades.g.cs` is 137 short rows and belongs in code. This is around 1,200 module
rows with a dozen columns each, and a static dictionary that size is a large static
constructor that runs whether or not anybody asks a specification question. list.md asks
for a dataset "lazy-queried at runtime", and a resource parsed on first use is literally
that: a Commander who never asks never pays for it.
"""

import csv
import io
import json
import urllib.request
from pathlib import Path

FDEV_IDS = "https://raw.githubusercontent.com/EDCD/FDevIDs/master/"

CORIOLIS_RAW = "https://raw.githubusercontent.com/EDCD/coriolis-data/master/"

CORIOLIS_TREE = "https://api.github.com/repos/EDCD/coriolis-data/git/trees/master?recursive=1"

OUTPUT = Path(__file__).resolve().parent.parent / "src" / "D47.Core" / "Knowledge" / "EliteSpecifications.tsv"

SHIP_COLUMNS = [
    "symbol", "name", "manufacturer", "pad", "speed", "boost", "armour", "shields",
    "hardness", "hull_mass", "fuel", "crew", "masslock", "cost", "hardpoints", "internals",
]

MODULE_COLUMNS = [
    "symbol", "name", "class", "rating", "mount", "mass", "power", "integrity", "cost",
    "optimal_mass", "max_fuel", "fuel_power", "fuel_multiplier",
]

PADS = {1: "small", 2: "medium", 3: "large"}


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "d47-gen-elite-specs"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def rows(name: str) -> list[dict]:
    text = fetch(FDEV_IDS + name).decode("utf-8-sig")
    return list(csv.DictReader(io.StringIO(text)))


def number(value) -> str:
    """A figure, or empty for one the source does not carry.

    Empty is a real answer and zero is not: a cargo rack with no mass entry and a cargo
    rack that weighs nothing are different claims, and only one of them is true.
    """
    if value is None or value == "":
        return ""
    if isinstance(value, float) and value == int(value):
        return str(int(value))
    return str(value)


def coriolis_files() -> tuple[list[str], list[str]]:
    tree = json.loads(fetch(CORIOLIS_TREE))["tree"]
    paths = [entry["path"] for entry in tree if entry["path"].endswith(".json")]

    ships = sorted(p for p in paths if p.startswith("ships/"))
    modules = sorted(p for p in paths if p.startswith("modules/"))

    if not ships or not modules:
        raise SystemExit("coriolis-data has no ships/ or modules/ JSON — the layout moved")

    return ships, modules


def build_ships(paths: list[str]) -> list[list[str]]:
    by_id = {int(row["id"]): row for row in rows("shipyard.csv")}

    built, missing = [], []

    for path in paths:
        for ship in json.loads(fetch(CORIOLIS_RAW + path)).values():
            properties = ship.get("properties") or {}
            slots = ship.get("slots") or {}

            identity = by_id.get(ship.get("edID"))

            if identity is None:
                missing.append(properties.get("name") or path)
                continue

            built.append([
                identity["symbol"].lower(),
                identity["name"],
                properties.get("manufacturer") or "",
                PADS.get(properties.get("class"), ""),
                number(properties.get("speed")),
                number(properties.get("boost")),
                number(properties.get("baseArmour")),
                number(properties.get("baseShieldStrength")),
                number(properties.get("hardness")),
                number(properties.get("hullMass")),
                number(properties.get("reserveFuelCapacity")),
                number(properties.get("crew")),
                number(properties.get("masslock")),
                number(properties.get("hullCost")),

                # Sizes rather than counts. "3 large, 2 medium" is the answer to "what can
                # it carry"; "5 hardpoints" is not.
                ",".join(str(size) for size in slots.get("hardpoints") or [] if size),
                ",".join(str(size) for size in slots.get("internal") or []
                         if isinstance(size, int) and size),
            ])

    # A hull with no id row cannot be keyed to anything the journal writes, so its figures
    # are unreachable — but its *existence* is worth recording. Three of them on
    # 2026-08-14: Caspian Explorer, Corsair and Kestrel Mk II, all newer than the id list.
    # Carried through so d47 can say "that is a ship I know of and have no figures for",
    # which is a better answer than "I have never heard of it" and is the difference
    # between a stale table and a wrong one.
    return sorted(built), sorted(missing)


def build_modules(paths: list[str]) -> tuple[list[list[str]], int]:
    by_id = {int(row["id"]): row for row in rows("outfitting.csv")}

    built, unnamed = [], 0

    for path in paths:
        for group in json.loads(fetch(CORIOLIS_RAW + path)).values():
            if not isinstance(group, list):
                continue

            for module in group:
                symbol = module.get("symbol")

                if not symbol:
                    continue

                identity = by_id.get(module.get("edID"))

                if identity is None:
                    # Unlike a ship, a module with no id row is still worth having: it is
                    # keyed by the same symbol the journal writes, so it can be looked up.
                    # Only its Frontier name is missing, and the file name carries one.
                    unnamed += 1
                    name = Path(path).stem.replace("_", " ").title()
                    mount = ""
                else:
                    name = identity["name"]
                    mount = identity["mount"]

                built.append([
                    symbol.lower(),
                    name,
                    number(module.get("class")),
                    module.get("rating") or "",
                    mount,
                    number(module.get("mass")),
                    number(module.get("power")),
                    number(module.get("integrity")),
                    number(module.get("cost")),

                    # Only the drive carries these, and they are the four numbers a jump
                    # range is computed from.
                    number(module.get("optmass")),
                    number(module.get("maxfuel")),
                    number(module.get("fuelpower")),
                    number(module.get("fuelmul")),
                ])

    # One symbol can appear in more than one coriolis file. Last wins would be arbitrary;
    # sorting and de-duplicating on the key keeps the output stable between runs.
    unique = {row[0]: row for row in sorted(built)}

    return disambiguate(sorted(unique.values())), unnamed


# Frontier's own placeholder rows. Not modules anybody can fit, and a "0Z Frame Shift Drive"
# in a list of the sizes a drive comes in is a lie about the game.
PLACEHOLDERS = ("int_missing_",)

# Tokens every module symbol carries, so they never distinguish one from another.
NOISE = {"int", "hpt", "size", "class"}


def disambiguate(built: list[list[str]]) -> list[list[str]]:
    """Give colliding modules names that tell them apart.

    `outfitting.csv` calls both `int_hyperdrive_size8_class5` and
    `int_hyperdrive_overcharge_size8_class5` a "Frame Shift Drive", so without this the table
    holds three different 8A drives under one name — three lines that look identical and
    carry different numbers, which is precisely the confidently-wrong answer this whole
    table is built the way it is to avoid.

    The qualifier is *derived from the symbol*, never invented: whatever token one member of
    a colliding group has and the others do not. That yields "Frame Shift Drive
    (overcharge)" rather than a guess at what Frontier calls it in the outfitting screen.
    """
    kept = [row for row in built if not row[0].startswith(PLACEHOLDERS)]

    groups: dict[tuple, list[list[str]]] = {}
    for row in kept:
        groups.setdefault((row[1], row[2], row[3], row[4]), []).append(row)

    for group in groups.values():
        if len(group) < 2:
            continue

        tokens = [
            {t for t in row[0].split("_") if t and t not in NOISE and not t.isdigit()}
            for row in group
        ]
        shared = set.intersection(*tokens)

        for row, own in zip(group, tokens):
            distinct = sorted(own - shared)

            if distinct:
                row[1] = f"{row[1]} ({' '.join(distinct)})"

    return kept


def main() -> None:
    ship_paths, module_paths = coriolis_files()

    ships, unkeyed = build_ships(ship_paths)
    modules, unnamed = build_modules(module_paths)

    lines = [
        "# Generated by tools/gen-elite-specs.py. Do not edit by hand — rerun the tool.",
        "# Derived from EDCD/FDevIDs (shipyard.csv, outfitting.csv) for names and the symbols",
        "# the journal writes, joined on Frontier's own ids with EDCD/coriolis-data for the",
        "# figures. See that script for why this table is derived rather than written.",
        f"# Ships: {len(ships)}. Modules: {len(modules)}. Known but unmeasured: {len(unkeyed)}.",
        "[ships]",
        "\t".join(SHIP_COLUMNS),
    ]

    lines += ["\t".join(row) for row in ships]
    lines += ["[modules]", "\t".join(MODULE_COLUMNS)]
    lines += ["\t".join(row) for row in modules]

    # Named and nothing else. A ship in this section exists and d47 has no figures for it,
    # which is a different answer from never having heard of it.
    lines += ["[known-but-unmeasured]", *unkeyed]

    OUTPUT.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")

    print(f"Wrote {len(ships)} ships and {len(modules)} modules to {OUTPUT}")

    if unkeyed:
        print(f"No shipyard.csv row, recorded as known but unmeasured: {', '.join(unkeyed)}")

    if unnamed:
        print(f"{unnamed} modules had no outfitting.csv row and were named from their file")


if __name__ == "__main__":
    main()
