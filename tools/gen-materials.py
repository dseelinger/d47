#!/usr/bin/env python3
"""Regenerate d47's material and inventory table.

Not part of the build. Its output is committed as
`src/D47.Core/Knowledge/Materials.tsv` and shipped as an embedded resource; the app never
runs this and never reaches the network for it. Run it when Frontier adds a material, a
micro-resource or a commodity:

    python tools/gen-materials.py

What it answers
---------------
Phase 14 asks d47 to price a material trade, total up what a blueprint needs, and say where
to go and get it. None of that works off the journal alone. The journal writes a symbol and
a count; it does not say what grade a material is, which trader line it sits in, whether a
"unit" is a unit or a tonne, or where any of it comes from.

Four columns and each fixes a specific way of being confidently wrong
--------------------------------------------------------------------
**`grade`** — the capacity a holding counts against, and the input to every trade rate.

**`line`** — one column of the material trader's grid, e.g. Carbon → Vanadium → Niobium →
Yttrium. Trading within a line is cheap; crossing to another costs a further 6x. The journal
cannot supply this: its `Category` field names the Raw/Manufactured/Encoded *type*, which no
trade ever crosses, so Iron-for-Vanadium reads as "same category" and prices as free when it
is in fact a 6x cross-line trade. 560 of 1,096 real trades are cross-line — the majority.
See `docs/spikes/material-lines.md`.

**`ledger`** — which inventory a thing lives in. Gold x200 in an engineer's tribute is two
hundred tonnes of *cargo*; filing it under a 300-unit material cap produces a feasibility
verdict that is nonsense, delivered confidently. It falls straight out of which FDevIDs file
the symbol appears in, so the classification is free.

**`origins`** — where the thing is found, from EDEngineer's own sourcing table.

**`settlements`, `buildings`, `containers`, `barter`** — the on-foot sourcing detail, which is
richer than anything a ship material gets. `origins` for an Odyssey ingredient is coarse — 195
of them say only `Planetary Settlement` — but the same file carries `SettlementType`,
`BuildingType` (16 codes, AGRI to STO) and `ContainerType` (22 kinds of locker and data port),
so "which settlement, which building, which locker" is answerable rather than approximated.
`barter` is `value/cost`, present on exactly the 33 Components, and it makes the Bartender's
rate arithmetic — see `OnFootRules.BarterYield`.

Sources, and what each is the authority on
------------------------------------------
`EDCD/FDevIDs` is the naming authority and, it turns out, carries almost everything:
`material.csv` has the symbol the journal writes, the display name, the grade in `rarity`,
the type in `type` — **and the trader line in `category`**. `microresources.csv`,
`commodity.csv` and `rare_commodity.csv` supply the other three ledgers.

`EDDiscovery/EliteDangerousCore` (Apache-2.0) `MCMRType.cs` is **the cross-check on the
line**, not the source of it. Its `MaterialGroupType` maps one-to-one onto FDevIDs
`category` across all 108 tradeable materials, and its nine Guardian/Thargoid groupings are
exactly FDevIDs' 29 `None` rows. Two independently maintained sources agreeing completely is
a far better warrant than either alone, so this script asserts the agreement and fails if it
ever breaks.

`msarilar/EDEngineer` (MIT) `entryData.json` is the authority on **origins** — 365 of its
371 entries carry `OriginDetails`, drawn from a vocabulary of 77 strings — and on the four
on-foot columns above. It is read `utf-8-sig`: the file has a BOM and a plain `json.load`
fails on it.

**One barter figure is overruled.** Graphene is published at `BarterValue` 12 and the game
charges 13. Two independent trades in a 912-journal corpus pin it to exactly that, and with
the correction the published mechanic reproduces **49 of 49** real trades rather than 47 —
`floor(sum(offered x value) / cost)`, no carry-over. The wrong number is the plausible one:
cost runs `2*value - 1` for every component valued 3 to 6, which 12 fits and 13 does not. See
`docs/spikes/journal-corpus-on-foot.md` §2.

The join, and the trap in it
----------------------------
**Key on the FDevIDs `symbol`; join EDEngineer on display name.** EDEngineer's own
`FormattedName` is not Frontier's symbol and differs for 22 materials, every one of them
Encoded and always the same way — the leading adjective kept where Frontier drops it, so
`anomalousbulkscandata` against `bulkscandata`. Keying on it throws no exception anywhere
visible; it just silently loses 22 of the 45 Encoded materials, and d47 reports a shortfall
the Commander does not have.

Display names drift too, so five are aliased below. Each is asserted to be *drift* rather
than *absence*: the name must exist in exactly one source and its counterpart in exactly the
other. Three near-misses fail that test and are deliberately **not** aliased —
`Geographical Data` is not `Geological Data`, and pretending otherwise would hang one item's
origins on a different real item, which is worse than having none.

Note on the data itself: FDevIDs and coriolis-data both state that the game data is
Frontier's, and EDDiscovery's Apache-2.0 grant covers their code rather than the game facts.
That is why this is a *derived index* with its provenance recorded rather than a copy — see
`NOTICE` and `docs/spikes/README.md`.
"""

import csv
import datetime
import io
import json
import re
import urllib.request
from pathlib import Path

import edengineer_names

FDEV_IDS = "https://raw.githubusercontent.com/EDCD/FDevIDs/master/"

EDENGINEER = ("https://raw.githubusercontent.com/msarilar/EDEngineer/master/"
              "EDEngineer/Resources/Data/entryData.json")

# Pinned, unlike the others: this one is C# source rather than a data file with a schema,
# so it can be restructured under us in a way a CSV cannot. Bump it deliberately.
EDDISCOVERY_COMMIT = "459d01dc2bccf688019c2f8e45c3909277a4e316"

EDDISCOVERY = (
    f"https://raw.githubusercontent.com/EDDiscovery/EliteDangerousCore/{EDDISCOVERY_COMMIT}"
    "/EliteDangerous/FrontierData/Items/MCMRType.cs"
)

OUTPUT = Path(__file__).resolve().parent.parent / "src" / "D47.Core" / "Knowledge" / "Materials.tsv"

COLUMNS = ["symbol", "name", "ledger", "category", "grade", "line", "origins",
           "settlements", "buildings", "containers", "barter"]

# Measured against the game, which the published figure disagrees with. See the module
# docstring; asserted below so a fix upstream retires this rather than double-counting.
BARTER_CORRECTIONS = {"Graphene": 13}

BLANK = ["", "", "", ""]

# Which FDevIDs file a symbol appears in *is* its inventory. The four are disjoint — no
# symbol appears in two — so this is a classification rather than a guess.
LEDGERS = [
    ("material.csv", "name", "material"),
    ("microresources.csv", "English name", "ship-locker"),
    ("commodity.csv", "name", "cargo"),
    ("rare_commodity.csv", "name", "rare-cargo"),
]

# EDEngineer's `Kind`, mapped to the ledger it must land in. Used as a tiebreaker rather
# than a filter: exactly one display name, "Wreckage Components", is ambiguous across
# ledgers — it is both a Thargoid material and a salvage commodity — and Kind decides it.
KINDS = {
    "Material": "material",
    "Data": "material",
    "OdysseyIngredient": "ship-locker",
    "Commodity": ("cargo", "rare-cargo"),
}

# The five drifting display names live in one place, because gen-blueprints.py joins on the
# same names and a drift repaired in one generator and not the other loses rows in silence.
ALIASES = edengineer_names.ALIASES


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "d47-gen-materials"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def rows(name: str) -> list[dict]:
    return list(csv.DictReader(io.StringIO(fetch(FDEV_IDS + name).decode("utf-8-sig"))))


def slug(text: str) -> str:
    return re.sub(r"-+", "-", re.sub(r"[^a-z0-9]+", "-", text.lower())).strip("-")


def line_of(kind: str, category: str) -> str:
    """The trader line, as a stable id derived from what FDevIDs already says.

    `category` is the line: 1..7 for Raw, and a named group for the other two. `None` means
    the material has no line, which is true of every Guardian and Thargoid material — those
    are grouped for display and the trader does not deal in them at all.

    The type is prefixed because Raw's categories are bare digits, and skipped when the name
    already carries it, so "Encoded Firmware" does not become `encoded-encoded-firmware`.
    """
    if category in ("", "None"):
        return ""

    name = slug(category)
    prefix = slug(kind)

    return name if name.startswith(prefix + "-") else f"{prefix}-{name}"


def eddiscovery_lines() -> dict[str, str]:
    """`MaterialGroupType` per journal symbol, for cross-checking the line.

    A regex over C# rather than a parse of data, which is exactly why this is the check and
    not the source. `spike/MaterialLineProbe/` is where this was proven out.
    """
    text = fetch(EDDISCOVERY).decode("utf-8-sig")

    call = re.compile(
        r"""Add\(\s*CatType\.(?P<cat>\w+)\s*,
            \s*ItemType\.\w+\s*,
            \s*MaterialGroupType\.(?P<group>\w+)\s*,
            \s*MCMR\.(?P<id>\w+)\s*,
            \s*(?P<strings>"(?:[^"\\]|\\.)*"(?:\s*,\s*"(?:[^"\\]|\\.)*")*)""",
        re.VERBOSE,
    )

    found = {}

    for match in call.finditer(text):
        if match.group("cat") not in ("Raw", "Encoded", "Manufactured"):
            continue

        # Two overloads reach here; the longer one carries an explicit FDName first.
        strings = re.findall(r'"((?:[^"\\]|\\.)*)"', match.group("strings"))
        symbol = strings[0] if len(strings) >= 3 else match.group("id")

        found[symbol.lower()] = match.group("group")

    if len(found) < 100:
        raise SystemExit(
            f"only {len(found)} materials parsed out of MCMRType.cs — the source has moved, "
            "and the line column has lost its cross-check"
        )

    return found


def check_lines(built: list[list[str]]) -> int:
    """Assert FDevIDs and EDDiscovery agree about every line, or stop.

    The two are maintained by different people from different starting points, so a
    one-to-one mapping across every material both know is a strong warrant. A break in it
    means one of them has reclassified something, and a silently mispriced trade is the
    thing this whole column exists to prevent.
    """
    theirs = eddiscovery_lines()

    mapping: dict[str, set[str]] = {}
    reverse: dict[str, set[str]] = {}
    unlined = []

    for symbol, _name, ledger, _category, _grade, line, *_rest in built:
        if ledger != "material" or symbol not in theirs:
            continue

        if line:
            mapping.setdefault(line, set()).add(theirs[symbol])
            reverse.setdefault(theirs[symbol], set()).add(line)
        else:
            unlined.append((symbol, theirs[symbol]))

    split = {ours: sorted(t) for ours, t in mapping.items() if len(t) != 1}
    merged = {t: sorted(ours) for t, ours in reverse.items() if len(ours) != 1}

    if split or merged:
        raise SystemExit(
            "FDevIDs and EDDiscovery disagree about the trader lines, so one of them has "
            f"reclassified a material.\n  one line, several groups: {split}\n"
            f"  one group, several lines: {merged}"
        )

    # The materials FDevIDs gives no line must be exactly the ones EDDiscovery files outside
    # the trader's grid. A material with a real line here and none there is a mispriced trade.
    leaked = sorted(symbol for symbol, group in unlined if group in reverse)

    if leaked:
        raise SystemExit(
            f"FDevIDs gives no line to {leaked}, but EDDiscovery puts them in a group the "
            "trader deals in"
        )

    return len(mapping)


def origins() -> tuple[dict[tuple[str, str], str], list[str], set[str], int]:
    """Where each thing is found, keyed by (display name, ledger).

    Keyed on the *name* rather than EDEngineer's `FormattedName`, which is not Frontier's
    symbol and silently loses 22 Encoded materials — see the module docstring.

    Two names are duplicated in EDEngineer's own file. `Virology Data` is a straight repeat,
    and `Biological Sample` appears twice under different `FormattedName`s, only one of
    which FDevIDs carries. Both pairs currently agree on their origins, so collapsing them
    loses nothing — but a future pair that *disagreed* would quietly hand one item the
    other's sourcing, so a disagreement stops the run instead.
    """
    entries = json.loads(fetch(EDENGINEER).decode("utf-8-sig"))

    corrected: list[str] = []

    found: dict[tuple[str, str], str] = {}
    onfoot: dict[str, list[str]] = {}
    spelled: set[str] = set()
    carrying: set[str] = set()
    collapsed = 0

    for entry in entries:
        name = (entry.get("Name") or "").strip()
        detail = "; ".join(entry.get("OriginDetails") or [])

        if not name:
            continue

        # The four on-foot columns, keyed on the name alone because they exist only for the
        # Odyssey ingredients and those sit in one ledger.
        value = entry.get("BarterValue")
        cost = entry.get("BarterCost")

        if value is not None and name in BARTER_CORRECTIONS:
            corrected.append(f"{name}: BarterValue {value} -> {BARTER_CORRECTIONS[name]}")
            value = BARTER_CORRECTIONS[name]

        onfoot[name.casefold()] = [
            "; ".join(entry.get("SettlementType") or []),
            "; ".join(entry.get("BuildingType") or []),
            "; ".join(entry.get("ContainerType") or []),
            f"{value}/{cost}" if value is not None and cost is not None else "",
        ]

        spelled.add(name)

        if detail:
            carrying.add(name)

        ledgers = KINDS.get(entry.get("Kind") or "")

        for ledger in (ledgers,) if isinstance(ledgers, str) else (ledgers or ()):
            key = (ALIASES.get(name, name).casefold(), ledger)

            if key in found:
                if found[key] != detail:
                    raise SystemExit(
                        f"EDEngineer has two {name!r} entries in the {ledger} ledger with "
                        f"different origins:\n  {found[key]!r}\n  {detail!r}\n"
                        "Joining on display name would give one of them the other's sourcing."
                    )
                collapsed += 1
                continue

            found[key] = detail

    if not corrected:
        raise SystemExit(
            "no barter figure needed correcting. If EDEngineer has been fixed upstream, delete "
            "BARTER_CORRECTIONS — a correction that no longer matches anything is a rule nobody "
            "can see is dead."
        )

    return (found, onfoot, sorted(spelled), {e.get("Kind") or "" for e in entries},
            collapsed, len(carrying), corrected)


def main() -> None:
    built: list[list[str]] = []
    by_name: dict[str, list[str]] = {}
    counts: dict[str, int] = {}

    sourced, onfoot, spelled, kinds, collapsed, carrying, corrected = origins()

    unknown_kinds = kinds - set(KINDS)
    if unknown_kinds:
        raise SystemExit(
            f"EDEngineer has a Kind this script cannot place in a ledger: {sorted(unknown_kinds)}"
        )

    for filename, name_column, ledger in LEDGERS:
        table = rows(filename)
        counts[ledger] = len(table)

        for row in table:
            symbol = (row.get("symbol") or "").strip()
            name = (row.get(name_column) or "").strip()

            if not symbol or not name:
                raise SystemExit(f"{filename} row has no symbol or name: {row}")

            category = (row.get("category") or "").strip()
            kind = (row.get("type") or "").strip()

            built.append([
                symbol.lower(),
                name,
                ledger,

                # For a material this is Raw/Manufactured/Encoded, the type no trade ever
                # crosses. For everything else it is the market or locker grouping.
                kind or category,
                (row.get("rarity") or "").strip(),
                line_of(kind, category) if ledger == "material" else "",
                sourced.get((name.casefold(), ledger), ""),

                # Blank for everything that is not an Odyssey ingredient, which is most of the
                # table. Only the ship-locker ledger has any of this.
                *(onfoot.get(name.casefold(), BLANK) if ledger == "ship-locker" else BLANK),
            ])

            by_name.setdefault(name, []).append(ledger)

    edengineer_names.check(by_name, spelled)
    lines = check_lines(built)

    # The ledger is only a classification if the four files are disjoint. They are today,
    # and a symbol turning up in two of them would make "is this a tonne or a unit" a guess.
    seen: dict[str, str] = {}
    for symbol, _name, ledger, *_rest in built:
        if symbol in seen:
            raise SystemExit(
                f"{symbol!r} is in both the {seen[symbol]} and {ledger} files, so its ledger "
                "is no longer decidable from where it appears"
            )
        seen[symbol] = ledger

    # EDEngineer names with no FDevIDs row. Not aliased onto a near neighbour and not
    # dropped: recorded, so "d47 has no symbol for that" is sayable and distinguishable from
    # never having heard of it. Same treatment EliteSpecifications gives an unkeyed hull.
    keyed = {name.casefold() for _symbol, name, *_rest in built}
    unkeyed = sorted(n for n in spelled if ALIASES.get(n, n).casefold() not in keyed)

    built.sort(key=lambda row: (row[2], row[0]))

    stamp = datetime.date.today().isoformat()

    text = [
        "# Generated by tools/gen-materials.py. Do not edit by hand — rerun the tool.",
        "# Identity, ledger, grade and line from EDCD/FDevIDs (material, microresources,",
        "# commodity, rare_commodity .csv). The line is cross-checked against EDDiscovery/",
        f"# EliteDangerousCore MCMRType.cs at {EDDISCOVERY_COMMIT[:12]} and the run fails if they",
        "# disagree. Origins from msarilar/EDEngineer entryData.json (MIT), joined on display",
        "# name. The four on-foot columns come from the same file; barter is value/cost and sits",
        "# on exactly the 33 Components. One barter figure is overruled against the game — see",
        "# docs/spikes/journal-corpus-on-foot.md §2. Game data is Frontier's, used under their",
        "# media usage rules — see NOTICE.",
        f"# Rows: {len(built)} ("
        + ", ".join(f"{ledger} {count}" for ledger, count in counts.items())
        + f"). Trader lines: {lines}. Built: {stamp}.",
        "\t".join(COLUMNS),
    ]

    text += ["\t".join(row) for row in built]
    text += ["[known-but-unkeyed]", *unkeyed]

    OUTPUT.write_text("\n".join(text) + "\n", encoding="utf-8", newline="\n")

    priced = sum(1 for row in built if row[5])
    described = sum(1 for row in built if row[6])

    print(f"Wrote {len(built)} rows to {OUTPUT}")
    print("Per ledger: " + ", ".join(f"{ledger}={count}" for ledger, count in counts.items()))
    print(f"Trader lines: {lines}, covering {priced} materials")
    # Reconciled rather than just reported, so a row going missing has nowhere to hide.
    print(f"Origins: {len(spelled)} EDEngineer names, {carrying} carrying detail, of which "
          f"{len(unkeyed)} have no FDevIDs symbol -> {described} rows described "
          f"({collapsed} duplicate entries collapsed after checking they agree)")
    print(f"Aliases applied: {len(ALIASES)} — " + ", ".join(sorted(ALIASES)))

    bartered = sum(1 for row in built if row[10])
    detailed = sum(1 for row in built if row[8])
    print(f"On foot: {detailed} rows carrying a building type, {bartered} carrying a barter rate")
    print("Barter figures overruled against the game: " + ", ".join(corrected))

    if unkeyed:
        # Loud on purpose. Each of these is something EDEngineer models and FDevIDs has no
        # symbol for, so d47 cannot key it to anything the journal writes.
        print(f"No FDevIDs symbol, recorded as known but unkeyed: {', '.join(unkeyed)}")


if __name__ == "__main__":
    main()
