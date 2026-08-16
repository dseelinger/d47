#!/usr/bin/env python3
"""Regenerate d47's blueprint table.

Not part of the build. Its output is committed as
`src/D47.Core/Knowledge/Blueprints.tsv` and shipped as an embedded resource; the app never
runs this and never reaches the network for it. Run it when Frontier changes a recipe:

    python tools/gen-blueprints.py

What it answers
---------------
Phase 14 asks what a blueprint does, what it costs per application, who offers it and to
what grade, and which experimental effects a module can take. The journal carries none of
it: it says what this Commander already rolled, never what a roll would need.

Combined with `EngineeringRules.RollsFor`, the per-application ingredient list becomes an
exact total rather than an estimate — `ingredients × rolls(grade, rank)`, with no hedging.

Two sources, and only one of them can be the authority
------------------------------------------------------
`msarilar/EDEngineer` (MIT) `blueprints.json` is the source. `EDCD/coriolis-data` is the
cross-check, joined on the `CoriolisGuid` EDEngineer records against coriolis's per-grade
`uuid`.

**Coriolis cannot be the authority on experimentals**, and not because it is stale.
Eleven effects have a *different recipe per module type* — "Double Braced" has eight — and
coriolis models one recipe per effect. That is a structural disagreement, so it is printed
rather than resolved: a generator that silently picked a winner would be inventing game data
with a straight face.

What `kind` is for
------------------
EDEngineer keeps six different things in one list, and they are not interchangeable:

- **modification** — a module blueprint. Ingredients are **per application**, so every size
  is 1 and the run fails if that stops being true.
- **experimental** — an ungraded effect. Ingredients are a one-off cost, sizes vary.
- **synthesis**, **tech-broker**, **unlock**, and the Odyssey **suit**/**weapon** rows.

Told apart by the `@`-prefixed pseudo-engineers EDEngineer uses — `@Synthesis`,
`@Technology`, `@Merchant`, `@Bartender` — rather than by a list of type names, so a new
munitions recipe classifies itself. The distinction is load-bearing: multiplying a synthesis
recipe by a roll count is arithmetic on the wrong kind of thing.

EDEngineer's on-foot quantities are pre-patch, and are corrected here
---------------------------------------------------------------------
**Measured 2026-08-16 against the game itself**, and this is the one place a source's numbers
are changed rather than merely reported. Every on-foot size in `blueprints.json` predates the
patch that cut them, by two different factors:

- a **modification** costs `⌈size ÷ 2⌉` — 5/10/15 become 3/5/8
- a **grade upgrade** costs `⌈size ÷ 3⌉` — 1/5/10/15/25/35 become 1/2/4/5/9/12
- and a grade upgrade no longer asks for **Power Regulators** at all

The evidence is in `docs/spikes/journal-corpus-on-foot.md` §3: 78 ingredient comparisons
against the `Resources` list on 16 real `UpgradeSuit`/`UpgradeWeapon` events for the upgrade
ladder, and a zero-remainder cover of 41 material lines across four `ShipLocker` deltas for
the modification ladder, each corroborating the other's leftovers. **Every key in both tables
was observed** — nothing is extrapolated, and the two divisors are a description of the
measurements rather than a rule anything here relies on.

**Why this is a correction and not a refusal.** Everywhere else this generator finds two
sources disagreeing it prints the disagreement and changes nothing, because neither source is
the game. Here one of them *is* the game. Shipping the published figures would quote a
Commander two to three times the real cost of everything on foot, in the half of Phase 20 that
was supposed to need no caveat at all — and it would look right, because it is what every
other tool says.

The remap is printed on every run, and a size the tables do not cover is a hard stop rather
than a pass-through.

Two things the plan for this step got wrong, both worth recording
-----------------------------------------------------------------
**EDEngineer's `Effects` strings are not double-encoded.** The file carries 56 real U+2713
ticks and reads cleanly as UTF-8. `âœ“` is precisely what those correct bytes look like
decoded as cp1252, so the defect was in the reading and not in the file — the same shape of
mistake `docs/spikes/README.md` §"The method" is written about. No repair is applied here,
only the right encoding.

**"Every ingredient size is 1" is true of modifications and of nothing else.** 57 graded ship
rows break it and every one is synthesis — munitions, refills, limpets, SRV repair. Hence
`kind`, and hence the assertion being scoped to modifications rather than dropped.

Note on the data: game data is Frontier's, used under their media usage rules. See `NOTICE`.
"""

import collections
import datetime
import json
import urllib.request
from pathlib import Path

import edengineer_names

EDENGINEER = ("https://raw.githubusercontent.com/msarilar/EDEngineer/master/"
              "EDEngineer/Resources/Data/blueprints.json")

CORIOLIS = "https://raw.githubusercontent.com/EDCD/coriolis-data/master/modifications/"

ROOT = Path(__file__).resolve().parent.parent

MATERIALS = ROOT / "src" / "D47.Core" / "Knowledge" / "Materials.tsv"

OUTPUT = ROOT / "src" / "D47.Core" / "Knowledge" / "Blueprints.tsv"

COLUMNS = ["kind", "module", "name", "grade", "engineers", "ingredients", "effects", "guid"]

# EDEngineer's pseudo-engineers. Everything they front is a recipe of some other kind, and
# telling them apart this way means a new munitions row classifies itself rather than waiting
# for somebody to add its type name to a list.
PSEUDO = {
    "@Synthesis": "synthesis",
    "@Technology": "tech-broker",
    "@Merchant": "merchant",
    "@Bartender": "bartender",
}

ON_FOOT = {"Suit": "suit", "Weapon": "weapon"}

# What the game charges, against what EDEngineer publishes. See the module docstring: measured,
# complete over the sizes that occur, and a stop rather than a guess for anything else.
MODIFICATION_SIZES = {5: 3, 10: 5, 15: 8}

UPGRADE_SIZES = {1: 1, 5: 2, 10: 4, 15: 5, 25: 9, 35: 12}

# Removed from every grade-upgrade recipe by Frontier, in their own words, and still listed by
# EDEngineer in all four suit rows. Absent from all four UpgradeSuit events in the corpus.
WITHDRAWN = {"Power Regulator"}

# The kinds the two remaps apply to. Everything else — ship modifications, experimentals,
# synthesis, tech broker, unlocks — is left exactly as published, because nothing measured
# says otherwise about any of them.
MODIFICATION_KINDS = {"suit", "weapon"}

UPGRADE_KINDS = {"merchant"}

# EDEngineer spells one weapon two ways — "Oppressor" at grade 2 and "Opressor" at 3, 4 and 5.
# Left as a typo it splits that weapon's upgrade ladder in half on any name join, so a
# Commander asks what grade 5 costs and is told there is no such recipe. Corrected here rather
# than at every call site, and asserted below so it cannot be silently fixed upstream and
# leave a rewrite behind that no longer matches anything.
MISSPELLED = {"Manticore Opressor": "Manticore Oppressor"}

# The one kind with no pseudo-engineer to give it away. EDEngineer models an engineer's
# invitation task as an ungraded blueprint offered by the engineer being unlocked, so without
# this it classifies as an experimental effect called "Bill Turner".
UNLOCK = "Unlock"


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "d47-gen-blueprints"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def load_json(url: str):
    # utf-8-sig, and no mojibake repair. See the module docstring: the tick marks are fine and
    # it was the reading that was broken.
    return json.loads(fetch(url).decode("utf-8-sig"))


def materials() -> tuple[dict[str, str], set[str]]:
    """Display name -> symbol, from the table Step 1 already generated and committed.

    Read from disk rather than refetched, so a blueprint ingredient is checked against exactly
    the rows d47 ships. One display name, "Wreckage Components", is two things — a Thargoid
    material and a salvage commodity — and the material is what a blueprint means, so the
    material ledger wins and the collision is reported rather than being resolved by file order.
    """
    if not MATERIALS.exists():
        raise SystemExit(f"{MATERIALS} is missing — run tools/gen-materials.py first")

    by_name: dict[str, tuple[str, str]] = {}
    ambiguous: set[str] = set()

    for line in MATERIALS.read_text(encoding="utf-8").splitlines():
        # Everything after a section marker is the known-but-unkeyed list: names with no
        # symbol, and so nothing a blueprint ingredient can resolve to.
        if line.startswith("["):
            break

        if line.startswith(("#", "symbol\t")) or not line.strip():
            continue

        cells = line.split("\t")
        symbol, name, ledger = cells[0], cells[1], cells[2]
        key = name.casefold()

        if key in by_name:
            ambiguous.add(name)
            # "material" sorts before the other ledgers and is what a recipe means.
            if by_name[key][1] == "material":
                continue

        by_name[key] = (symbol, ledger)

    return {name: symbol for name, (symbol, _ledger) in by_name.items()}, ambiguous


def restate(kind: str, size: int, entry: dict, name: str, unpriced: list[str]) -> int:
    """What the game charges for an on-foot ingredient, where EDEngineer says what it used to.

    Off-foot recipes fall straight through. An on-foot size the measurements do not cover is
    recorded rather than passed along, because passing it along would put one pre-patch number
    into a table of corrected ones with nothing saying which is which.
    """
    table = (MODIFICATION_SIZES if kind in MODIFICATION_KINDS
             else UPGRADE_SIZES if kind in UPGRADE_KINDS
             else None)

    if table is None:
        return size

    if size not in table:
        unpriced.append(f"{entry['Type']} / {entry['Name']}: {name} x{size}")
        return size

    return table[size]


def kind_of(entry: dict) -> str:
    """Which of the six things in EDEngineer's one list this row is."""
    engineers = entry.get("Engineers") or []

    for tag, name in PSEUDO.items():
        if tag in engineers:
            return name

    if entry["Type"] == UNLOCK:
        return "unlock"

    if entry["Type"] in ON_FOOT:
        return ON_FOOT[entry["Type"]]

    return "modification" if entry.get("Grade") else "experimental"


def coriolis_recipes() -> tuple[dict[str, dict[str, int]], dict[str, dict[str, int]]]:
    """Components per uuid, for blueprints and for experimental effects."""
    blueprints = {}
    for record in load_json(CORIOLIS + "blueprints.json").values():
        for grade in (record.get("grades") or {}).values():
            if grade.get("uuid"):
                blueprints[grade["uuid"]] = grade.get("components") or {}

    specials = {
        record["uuid"]: record.get("components") or {}
        for record in load_json(CORIOLIS + "specials.json").values()
        if record.get("uuid")
    }

    return blueprints, specials


def main() -> None:
    entries = load_json(EDENGINEER)
    by_name, ambiguous = materials()
    blueprints, specials = coriolis_recipes()

    built: list[list[str]] = []
    unresolved: list[str] = []
    oversized: list[str] = []
    withdrawn: list[str] = []
    unpriced: list[str] = []
    respelled: list[str] = []
    counts: collections.Counter = collections.Counter()

    for entry in entries:
        kind = kind_of(entry)
        counts[kind] += 1

        ingredients = []
        for item in entry.get("Ingredients") or []:
            name = (item.get("Name") or "").strip()
            size = item.get("Size") or 0
            symbol = by_name.get(edengineer_names.canonical(name).casefold())

            if symbol is None:
                unresolved.append(f"{entry['Type']} / {entry['Name']}: {name!r}")
                continue

            if kind in UPGRADE_KINDS and name in WITHDRAWN:
                withdrawn.append(f"{entry['Type']} / {entry['Name']} G{entry['Grade']}: {name}")
                continue

            size = restate(kind, size, entry, name, unpriced)

            ingredients.append(f"{symbol}*{size}")

            # Per-application is the whole basis of "ingredients x rolls". A modification whose
            # ingredients are not per-application would silently multiply into a wrong total.
            if kind == "modification" and size != 1:
                oversized.append(f"{entry['Type']} / {entry['Name']} G{entry['Grade']}: {name} x{size}")

        effects = ";".join(
            f"{(effect.get('Property') or '').strip()}|{(effect.get('Effect') or '').strip()}"
            f"|{'good' if effect.get('IsGood') else 'bad'}"
            for effect in entry.get("Effects") or []
            if effect.get("Property")
        )

        name = entry["Name"]

        if name in MISSPELLED:
            respelled.append(f"{entry['Type']} / {name} G{entry.get('Grade')}")
            name = MISSPELLED[name]

        built.append([
            kind,
            entry["Type"],
            name,
            str(entry.get("Grade") or ""),
            ",".join(who for who in entry.get("Engineers") or [] if not who.startswith("@")),
            ",".join(ingredients),
            effects,
            entry.get("CoriolisGuid") or "",
        ])

    if unresolved:
        # A blueprint asking for something no id list names cannot be totalled, and reporting a
        # short total is worse than reporting none.
        raise SystemExit(
            f"{len(unresolved)} ingredient references resolve to no material row:\n  "
            + "\n  ".join(unresolved[:20])
        )

    if oversized:
        raise SystemExit(
            "modification ingredients must be per application, and these are not:\n  "
            + "\n  ".join(oversized[:20])
        )

    if unpriced:
        # A stop rather than a warning. One uncorrected size sitting in a table of corrected
        # ones is invisible, and it is wrong by a factor of two or three.
        raise SystemExit(
            f"{len(unpriced)} on-foot ingredient sizes are outside the measured tables, so what "
            "the game charges for them is not known:\n  " + "\n  ".join(unpriced[:20])
        )

    disagreements = compare(entries, blueprints, specials, by_name)

    built.sort(key=lambda row: (row[0], row[1], row[2], row[3]))

    stamp = datetime.date.today().isoformat()

    text = [
        "# Generated by tools/gen-blueprints.py. Do not edit by hand — rerun the tool.",
        "# Recipes, grades, engineers and effects from msarilar/EDEngineer blueprints.json (MIT),",
        "# cross-checked against EDCD/coriolis-data on CoriolisGuid. Coriolis is not the authority:",
        "# eleven experimentals have a different recipe per module type and it models one each, so",
        "# disagreements are printed by the generator rather than resolved here.",
        "# Ingredients are keyed to Materials.tsv symbols and are PER APPLICATION for kind=",
        "# modification — multiply by EngineeringRules.RollsFor for a total. No other kind is",
        "# per application. On-foot quantities (kind=suit, weapon, merchant) are NOT EDEngineer's:",
        "# its figures are pre-patch and are restated here to what the game charges, measured over",
        "# 16 upgrade events and four locker deltas — see docs/spikes/journal-corpus-on-foot.md §3.",
        "# Game data is Frontier's, used under their media usage rules; see NOTICE.",
        f"# Rows: {len(built)} ("
        + ", ".join(f"{kind} {count}" for kind, count in sorted(counts.items()))
        + f"). Built: {stamp}.",
        "\t".join(COLUMNS),
    ]

    text += ["\t".join(row) for row in built]

    OUTPUT.write_text("\n".join(text) + "\n", encoding="utf-8", newline="\n")

    print(f"Wrote {len(built)} rows to {OUTPUT}")
    print("Per kind: " + ", ".join(f"{kind}={count}" for kind, count in sorted(counts.items())))
    print(f"Ingredient references resolved: "
          f"{sum(len(row[5].split(',')) for row in built if row[5])}, unresolved 0")

    if ambiguous:
        print(f"Display names that are more than one material, resolved to the material ledger: "
              f"{', '.join(sorted(ambiguous))}")

    print(f"\nOn-foot quantities restated from EDEngineer's pre-patch figures: "
          f"modifications {MODIFICATION_SIZES}, grade upgrades {UPGRADE_SIZES}")

    print(f"Ingredients dropped as withdrawn by Frontier: {len(withdrawn)}")
    for item in withdrawn:
        print(f"  {item}")

    if not withdrawn:
        raise SystemExit(
            "no Power Regulators were found to drop. Either EDEngineer has been updated — in "
            "which case check whether its quantities moved too, and retire the remap with them — "
            "or the filter has stopped matching."
        )

    print(f"Misspellings corrected: {len(respelled)}")
    for item in respelled:
        print(f"  {item}")

    if not respelled:
        raise SystemExit(
            "the Manticore Opressor typo was not found. If it has been fixed upstream, delete "
            "MISSPELLED — leaving a rewrite in place that no longer matches anything is how a "
            "table starts lying quietly."
        )

    for label, items in disagreements.items():
        print(f"\n{label}: {len(items)}")
        for item in items:
            print(f"  {item}")


def compare(entries, blueprints, specials, by_name) -> dict[str, list[str]]:
    """Where EDEngineer and coriolis disagree, listed rather than reconciled.

    Printed in full and never used to change a row. The point is that somebody reading the
    output can see which recipes two independent sources tell different stories about — an
    invisible disagreement is the one that ships.
    """
    found: dict[str, list[str]] = {
        "Blueprint recipes where coriolis disagrees": [],
        "Experimental recipes where coriolis disagrees": [],
    }

    joined = collections.Counter()

    for entry in entries:
        guid = entry.get("CoriolisGuid")

        if not guid:
            continue

        graded = bool(entry.get("Grade"))
        theirs = blueprints.get(guid) if graded else specials.get(guid)

        if theirs is None:
            continue

        joined["blueprint" if graded else "experimental"] += 1

        ours = {(item.get("Name") or "").strip(): item.get("Size") or 0
                for item in entry.get("Ingredients") or []}

        # Compared on display name, which is what coriolis keys components by.
        if {k: v for k, v in ours.items()} != {k.strip(): v for k, v in theirs.items()}:
            label = ("Blueprint recipes where coriolis disagrees" if graded
                     else "Experimental recipes where coriolis disagrees")
            found[label].append(
                f"{entry['Type']} / {entry['Name']}"
                + (f" G{entry['Grade']}" if graded else "")
                + f"\n      EDEngineer: {ours}\n      coriolis:   {dict(theirs)}"
            )

    print(f"Joined on CoriolisGuid: {joined['blueprint']} blueprints, "
          f"{joined['experimental']} experimentals")

    return found


if __name__ == "__main__":
    main()
