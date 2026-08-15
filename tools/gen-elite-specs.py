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

Bulkheads are modules, and they are not under modules/
------------------------------------------------------
Armour is per-hull. A Mandalay's Lightweight Alloy and an Adder's are different objects
with different mass and different cost, so coriolis-data does not file them under
`modules/` with the generic outfitting at all — each one lives inside its own ship's JSON
under `bulkheads`, and FDevIDs marks them by being the only `outfitting.csv` rows with the
`ship` column filled in. Reading only `modules/` therefore dropped every one of them,
which is not a small corner: 1,725 of 20,526 engineered modules across 912 real journals
are bulkheads, and each one d47 could not name was read out as its raw symbol.

So they are built here from the same two sources and the same id join, iterating the
naming authority rather than the figures: every `outfitting.csv` row that names a hull is
a row in the output, and a missing counterpart in coriolis-data costs it its figures and
not its existence — the symbol is still what the journal writes, so a named bulkhead with
no numbers still answers "that is your Reactive Surface Composite" instead of
"panthermkii_armour_reactive".

Their names carry the hull. Frontier calls forty-eight different objects "Lightweight
Alloy" because the outfitting screen already knows which ship you are standing in and this
table does not, so the hull is prefixed — from `outfitting.csv`'s own `ship` column,
spelled as the ships section above spells it. That makes each name unique, and it makes
`get_module_specification` behave: a bare "Lightweight Alloy" is ambiguous across
forty-eight hulls, and Catalogue answers an ambiguous fragment by offering the candidates
back rather than picking one.

Class and rating are deliberately dropped. `outfitting.csv` files every bulkhead as class
1, and rates the pre-2024 hulls I while rating the newer ones A, B or C for the same five
grades of the same armour — a placeholder that distinguishes nothing and would be spoken
as "1I Lightweight Alloy", which is a claim about the game that is not true. What does
distinguish them is `hull_boost` and the four resistances, so those are carried.

Why a TSV resource and not a generated .g.cs
--------------------------------------------
`MaterialGrades.g.cs` is 137 short rows and belongs in code. This is around 1,200 module
rows with a dozen columns each, and a static dictionary that size is a large static
constructor that runs whether or not anybody asks a specification question. list.md asks
for a dataset "lazy-queried at runtime", and a resource parsed on first use is literally
that: a Commander who never asks never pays for it.
"""

import csv
import datetime
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
    "hull_boost", "kinetic_res", "thermal_res", "explosive_res", "caustic_res",
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


def coriolis_files() -> tuple[str, list[str], list[str]]:
    tree = json.loads(fetch(CORIOLIS_TREE))
    paths = [entry["path"] for entry in tree["tree"] if entry["path"].endswith(".json")]

    ships = sorted(p for p in paths if p.startswith("ships/"))
    modules = sorted(p for p in paths if p.startswith("modules/"))

    if not ships or not modules:
        raise SystemExit("coriolis-data has no ships/ or modules/ JSON — the layout moved")

    return tree["sha"], ships, modules


def ship_documents(paths: list[str]) -> list[dict]:
    """Every coriolis ship entry, fetched once.

    Two passes read these — the hull figures, and the bulkheads sitting inside them — and
    fetching forty-seven files twice to do it would make the second pass look free when it
    is not.
    """
    return [
        ship
        for path in paths
        for ship in json.loads(fetch(CORIOLIS_RAW + path)).values()
    ]


def build_ships(documents: list[dict]) -> tuple[list[list[str]], list[str]]:
    by_id = {int(row["id"]): row for row in rows("shipyard.csv")}

    built, missing = [], []

    for ship in documents:
        properties = ship.get("properties") or {}
        slots = ship.get("slots") or {}

        identity = by_id.get(ship.get("edID"))

        if identity is None:
            missing.append(properties.get("name") or "an unnamed hull")
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


def build_modules(paths: list[str], outfitting: dict[int, dict]) -> tuple[list[list[str]], int]:
    built, unnamed = [], 0

    for path in paths:
        for group in json.loads(fetch(CORIOLIS_RAW + path)).values():
            if not isinstance(group, list):
                continue

            for module in group:
                symbol = module.get("symbol")

                if not symbol:
                    continue

                identity = outfitting.get(module.get("edID"))

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

                    # And only a bulkhead carries these five. See build_bulkheads.
                    "", "", "", "", "",
                ])

    return built, unnamed


def build_bulkheads(
    documents: list[dict], outfitting: dict[int, dict]
) -> tuple[list[list[str]], list[str], list[str]]:
    """Per-hull armour, from the ship files rather than from modules/.

    Iterated from `outfitting.csv` rather than from coriolis, because that is the side that
    decides what exists: a bulkhead is exactly an outfitting row with the `ship` column
    filled in, which is the only place either source says out loud that a module belongs to
    one hull. Both directions of the join come back, so neither can fail quietly — a named
    bulkhead with no figures and a figure with no name are different problems, and only one
    of them is survivable.
    """
    figures = {
        bulkhead.get("edID"): bulkhead
        for ship in documents
        for bulkhead in ship.get("bulkheads") or []
    }

    built, unmeasured = [], []

    for identifier, identity in sorted(outfitting.items()):
        if not identity["ship"]:
            continue

        bulkhead = figures.pop(identifier, None)

        if bulkhead is None:
            unmeasured.append(identity["symbol"].lower())
            bulkhead = {}

        built.append([
            identity["symbol"].lower(),

            # The hull, then Frontier's name for the armour. Forty-eight ships have a
            # "Lightweight Alloy", and the outfitting screen can leave the hull unsaid
            # because the Commander is standing in it. A table cannot.
            f"{identity['ship']} {identity['name']}",

            # Class and rating are placeholders in the id list — every bulkhead is class 1,
            # rated I on the old hulls and A, B or C on the new ones for the same five
            # grades — so they are dropped rather than spoken. Nor is armour mounted.
            "", "", "",

            number(bulkhead.get("mass")),

            # A bulkhead draws no power, and neither source gives it an integrity.
            "", "",

            number(bulkhead.get("cost")),

            # Not a drive.
            "", "", "", "",

            # The fraction added to the hull's own armour, so the ships section's `armour`
            # column times one plus this is the fitted figure. Then the four resistances,
            # signed as coriolis signs them: negative is a hole, not a saving. They are
            # what separates Mirrored from Reactive, which are otherwise the same mass, the
            # same boost and a different price.
            number(bulkhead.get("hullboost")),
            number(bulkhead.get("kinres")),
            number(bulkhead.get("thermres")),
            number(bulkhead.get("explres")),
            number(bulkhead.get("causres")),
        ])

    # Whatever is left in `figures` had figures and no name. Nothing can be keyed to it, so
    # it is reported and not written: a row under a symbol coriolis invented would be a
    # symbol the journal never writes.
    return built, unmeasured, sorted(
        bulkhead.get("name") or "an unnamed bulkhead" for bulkhead in figures.values()
    )


# Frontier's own placeholder rows. Not modules anybody can fit, and a "0Z Frame Shift Drive"
# in a list of the sizes a drive comes in is a lie about the game.
PLACEHOLDERS = ("int_missing_",)

# Tokens every module symbol carries, so they never distinguish one from another.
NOISE = {"int", "hpt", "size", "class"}


def relax(text: str) -> str:
    """Letters and digits, lower case. `Catalogue.Relax` in Core, for the same reason."""
    return "".join(character.lower() for character in text if character.isalnum())


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

    What the name already says is then struck out of the qualifier, because that is the same
    word twice rather than a distinction. `outfitting.csv` calls
    `int_corrosionproofcargorack_size5_class1` a "Cargo Rack", the same as the plain rack, so
    the tokens separating them are `cargorack` and `corrosionproofcargorack` — and "Cargo
    Rack (cargorack)" is harder to say than "Cargo Rack" while telling a listener strictly
    less. Striking `cargorack` leaves nothing on one and "corrosionproof" on the other, which
    is the distinction that was actually there.

    Striking can only ever remove information, so it is kept only while the group still comes
    apart. A group left ambiguous by it keeps its raw tokens and reads badly rather than
    reading wrong.
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

        raw = [sorted(own - shared) for own in tokens]
        said = relax(group[0][1])
        struck = [[t for t in (token.replace(said, "") for token in own) if t] for own in raw]

        qualifiers = struck if len({tuple(own) for own in struck}) == len(group) else raw

        for row, qualifier in zip(group, qualifiers):
            if qualifier:
                row[1] = f"{row[1]} ({' '.join(qualifier)})"

    return kept


def main() -> None:
    sha, ship_paths, module_paths = coriolis_files()

    outfitting = {int(row["id"]): row for row in rows("outfitting.csv")}
    documents = ship_documents(ship_paths)

    ships, unkeyed = build_ships(documents)
    generic, unnamed = build_modules(module_paths, outfitting)
    bulkheads, unmeasured, unkeyed_bulkheads = build_bulkheads(documents, outfitting)

    # One symbol can appear in more than one coriolis file. Last wins would be arbitrary;
    # sorting and de-duplicating on the key keeps the output stable between runs.
    unique = {row[0]: row for row in sorted(generic + bulkheads)}
    modules = disambiguate(sorted(unique.values()))

    lines = [
        "# Generated by tools/gen-elite-specs.py. Do not edit by hand — rerun the tool.",
        "# Derived from EDCD/FDevIDs (shipyard.csv, outfitting.csv) for names and the symbols",
        "# the journal writes, joined on Frontier's own ids with EDCD/coriolis-data for the",
        "# figures. See that script for why this table is derived rather than written.",
        "# Bulkheads are per-hull and live in coriolis-data's ship files rather than its",
        "# modules/, and their names carry the hull because forty-eight hulls have a",
        "# Lightweight Alloy. Class and rating are placeholders for armour and are dropped.",
        "# Game data is Frontier's, used under their media usage rules — see NOTICE.",
        f"# Ships: {len(ships)}. Modules: {len(modules)}, of which bulkheads {len(bulkheads)}. "
        f"Known but unmeasured: {len(unkeyed)}.",
        f"# coriolis-data tree {sha[:12]}. Built: {datetime.date.today().isoformat()}.",
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

    print(f"Wrote {len(ships)} ships and {len(modules)} modules "
          f"({len(bulkheads)} of them bulkheads) to {OUTPUT}")

    if unkeyed:
        print(f"No shipyard.csv row, recorded as known but unmeasured: {', '.join(unkeyed)}")

    if unnamed:
        print(f"{unnamed} modules had no outfitting.csv row and were named from their file")

    if unmeasured:
        print(f"{len(unmeasured)} bulkheads had no coriolis-data figures and carry a name "
              f"only: {', '.join(unmeasured)}")

    # The one direction that is not survivable. A bulkhead nothing can key is a bulkhead the
    # journal can name and this table cannot, which is the failure that started all this.
    if unkeyed_bulkheads:
        raise SystemExit(
            "coriolis-data bulkheads with no outfitting.csv row, so nothing to key them by: "
            + ", ".join(unkeyed_bulkheads)
        )


if __name__ == "__main__":
    main()
