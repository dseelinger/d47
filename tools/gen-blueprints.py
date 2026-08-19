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
import re
import urllib.request
from pathlib import Path

import edengineer_names

EDENGINEER = ("https://raw.githubusercontent.com/msarilar/EDEngineer/master/"
              "EDEngineer/Resources/Data/blueprints.json")

CORIOLIS = "https://raw.githubusercontent.com/EDCD/coriolis-data/master/modifications/"

# The third source, and the only one that says which blueprints a *module kind* can take. See
# `edsy_offers`. Same standing on the data as the other two: EDSY's code is CC BY-NC and the game
# data in it remains Frontier's, used under their media usage rules. See NOTICE.
EDSY_DB = "https://raw.githubusercontent.com/taleden/EDSY/master/eddb.js"

ROOT = Path(__file__).resolve().parent.parent

MATERIALS = ROOT / "src" / "D47.Core" / "Knowledge" / "Materials.tsv"

SPECIFICATIONS = ROOT / "src" / "D47.Core" / "Knowledge" / "EliteSpecifications.tsv"

OUTPUT = ROOT / "src" / "D47.Core" / "Knowledge" / "Blueprints.tsv"

COLUMNS = ["kind", "module", "name", "grade", "engineers", "ingredients", "effects", "guid",

           # Frontier's own name for the blueprint, which is what the journal writes into
           # `Engineering.BlueprintName` and what coriolis keys its recipes on. See `symbols`.
           "symbol"]


# Which blueprints each module type can take. See `edsy_offers`.
MTYPE_COLUMNS = ["mtype", "blueprints"]

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


def symbols() -> dict[str, set]:
    """Every coriolis blueprint uuid, mapped to Frontier's own name for that blueprint.

    **This is the join the whole Loadout tab was missing** (remediation 15, items 6 and 10). The
    key of `modifications/blueprints.json` is an `fdname` — `Engine_Dirty`,
    `PowerDistributor_PrioritySystems`, `Armour_Advanced` — and that is *the same string the
    journal writes* into `Engineering.BlueprintName`. So carrying it turns two spellings that
    nothing joined into one id.

    What it fixes is not cosmetic. `ChecklistNaming.Readable` called its own output "ugly and
    true" and `CannotConfirm` told the Commander outright that nothing d47 shipped joined the two
    spellings, so a grade 5 System Focused roll was read back as
    "grade 5 PowerDistributor PrioritySystems". Both statements were accurate when written; this
    column is what makes them false.

    Specials carry a uuid each rather than a grade ladder, so they are folded in here too — an
    experimental effect is a blueprint the journal names the same way.

    **A uuid can belong to more than one name**, so this keeps every one rather than the last read.
    coriolis files the same recipe under `Armour_Advanced` and `Misc_LightWeight`, and taking one
    left the other reachable from nothing.
    """
    found: dict[str, set] = {}

    for fdname, record in load_json(CORIOLIS + "blueprints.json").items():
        for grade in (record.get("grades") or {}).values():
            if grade.get("uuid"):
                found.setdefault(grade["uuid"], set()).add(fdname)

    for fdname, record in load_json(CORIOLIS + "specials.json").items():
        if record.get("uuid"):
            found.setdefault(record["uuid"], set()).add(fdname)

    return found


def relax(text: str) -> str:
    """Letters and digits, lower case. `Catalogue.Relax` in Core, for the same reason."""
    return "".join(character.lower() for character in text if character.isalnum())


def specification_modules() -> dict[str, set]:
    """Every module name each EDSY module type covers, read from the sibling table.

    Not a fetch. `gen-elite-specs.py` already resolved every module's `mtype` from the same EDSY
    file, so reading its output is cheaper than doing it twice and cannot disagree with it.

    Used to settle a label that belongs to several blueprints: `Stripped Down` is an experimental
    on six different module types, and the row saying "Thrusters / Stripped Down" wants the one
    whose type covers a module called Thrusters.
    """
    found: dict[str, set] = {}
    section, columns = None, None

    for line in SPECIFICATIONS.read_text(encoding="utf-8").splitlines():
        if line.startswith("#"):
            continue

        if line.startswith("["):
            section, columns = line.strip("[]"), None
            continue

        if columns is None:
            columns = line.split("	")
            continue

        if section != "modules":
            continue

        cells = dict(zip(columns, line.split("	")))

        if cells.get("mtype") and cells.get("name"):
            found.setdefault(cells["mtype"], set()).add(relax(cells["name"]))

    return found


def edsy_database() -> str:
    """EDSY's `eddb.js`, whole. Fetched once; three things below read it."""
    request = urllib.request.Request(EDSY_DB, headers={"User-Agent": "d47-gen-blueprints"})

    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read().decode("utf-8-sig")


def edsy_section(database: str, name: str) -> str:
    """One top-level block of `eddb.js`, by the comment it closes with."""
    opened = database.index("\n\t" + name)

    return database[opened: database.index("}, // eddb." + name, opened)]


def edsy_named(block: str) -> dict[str, tuple[str, str]]:
    """`id : { name:'...', fdname:'...' }` pairs, as id to (Frontier's name, EDSY's label).

    Commented-out entries are skipped rather than read. EDSY retires a blueprint by commenting it
    out — `misc_lw4` sits directly under `misc_lw` with the same `fdname` — so reading past the
    comments would offer a grade ladder Frontier withdrew.
    """
    found = {}

    for line in block.splitlines():
        text = line.strip()

        if text.startswith("//"):
            continue

        entry = re.match(r"(\w+)\s*:\s*\{", text)
        symbol = re.search(r"fdname\s*:\s*'([^']+)'", text)
        label = re.search(r"\bname\s*:\s*'([^']+)'", text)

        if entry and symbol:
            found[entry.group(1)] = (symbol.group(1), label.group(1) if label else "")

    return found


def edsy_offers(database: str) -> tuple[list[list[str]], dict[str, str]]:
    """What each module type can be engineered with, and every blueprint's name.

    **This is the join the Loadout tab was missing** (remediation 15 item 6), and EDSY is the only
    one of the three sources that carries it. Its `mtype` section lists, per module type, the
    `blueprints` and `expeffects` that type accepts — and `EliteSpecifications.tsv` already carries
    that same `mtype` against every module, from the same file. So a slot's module resolves to a
    type, and the type says exactly what it takes.

    Every hop is an id. That matters because the two obvious routes are both dead ends, and both
    fail *confidently*, which is worse than failing:

    - coriolis groups its recipes by its own code and EDEngineer names them by module kind, and
      the two cannot be joined. Scoring the overlap ties every scanner with the Chaff Launcher,
      because they share the generic Lightweight and Reinforced rolls; scoring the similarity
      instead still put a Cargo Rack under Torpedo Pylon and a Pulse Laser under Burst Laser.
    - Matching on names is what the panel already did, and it is why a Type-10's armour was
      offered Dirty Drive Tuning.

    Under EDSY the same cases come out right: a Cargo Scanner takes Fast Scan, Long Range and Wide
    Angle rather than a Chaff Launcher's set, a Cargo Rack takes Expanded Cargo Rack, and a
    **Fuel Tank takes nothing at all** — which is the second half of the report, stated by the data
    instead of inferred from an empty list.

    Returns the per-type offer, and Frontier's name for every blueprint keyed by EDSY's label, which
    is what names the rows that carry no id of their own.
    """
    blueprints = edsy_named(edsy_section(database, "blueprint"))
    effects = edsy_named(edsy_section(database, "expeffect"))

    known = {**blueprints, **effects}

    # EDSY's label to every name that carries it, for the second pass in `main`. **A list and not
    # a value**: `Stripped Down` is the label of six different blueprints — one each for thrusters,
    # the drive, the distributor, the power plant, shields and cell banks — and `Double Braced` of
    # eight. Keeping one stranded the rest, which is how a common experimental went missing.
    by_label: dict[str, list[str]] = {}

    for symbol, label in known.values():
        if label:
            by_label.setdefault(relax(label), []).append(symbol)

    types = edsy_section(database, "mtype")
    built = []

    for found in re.finditer(r"\n\t\t(\w+)\s*:\s*\{", types):
        depth, at = 1, found.end()

        while depth > 0 and at < len(types):
            depth += 1 if types[at] == "{" else -1 if types[at] == "}" else 0
            at += 1

        body = types[found.end(): at - 1]

        def listed(key: str, body=body) -> list[str]:
            cell = re.search(r"%s\s*:\s*\[([^\]]*)\]" % key, body)

            if not cell:
                return []

            return [
                item.strip().strip("'")
                for item in cell.group(1).replace("\n", " ").split(",")
                if item.strip()
            ]

        # Paint is not engineering. EDSY carries the four Decorative blueprints because it models
        # what can be applied to a module; a Commander planning a build is not choosing a colour.
        offered = sorted({
            known[item][0]
            for item in listed("blueprints") + listed("expeffects")
            if item in known and not known[item][0].startswith("Decorative_")
        })

        built.append([found.group(1), ",".join(offered)])

    return sorted(built), by_label


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
    fdnames = symbols()
    offers, by_label = edsy_offers(edsy_database())

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

            # Frontier's own name for the blueprint, via the guid — every name the guid belongs
            # to, because coriolis files one recipe under more than one. Filled from EDSY below for
            # the rows carrying no guid at all.
            ",".join(sorted(fdnames.get(entry.get("CoriolisGuid") or "", set()))),
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

    # Second pass, for the rows the guid could not name. EDEngineer leaves `CoriolisGuid` off 193
    # of its 940 module recipes — most of the experimentals among them — so without this every
    # `special_*` effect is a blueprint EDSY offers and nothing here can supply.
    #
    # Matched on the label both sources publish, which is evidence rather than a guess: EDSY says
    # `special_corrosive_shell` is called "Corrosive Shell" and EDEngineer has a row of that name.
    # The vocabulary is closed at 151 entries and every miss is named at the end of the run.
    # Which module names each type covers, from the sibling table — so a label shared by six
    # blueprints resolves to the one belonging to *this* row's module rather than to whichever was
    # read last. Exact wherever the two sources spell a module the same way, which is most of them.
    modules_of = specification_modules()
    types_of: dict[str, set] = {}

    for code, names in modules_of.items():
        for name in names:
            types_of.setdefault(name, set()).add(code)

    offering: dict[str, set] = {}

    for code, listed in offers:
        for name in listed.split(","):
            if name:
                offering.setdefault(name, set()).add(code)

    unnamed = []

    for row in built:
        if row[8] or row[0] not in ("modification", "experimental"):
            continue

        label = relax(row[2])

        candidates = by_label.get(label, [])

        # The two sources spell a few labels differently — "Fast Scanner" against "Fast Scan",
        # "Expanded Probe Scanning Radius" against "Expanded Radius". Where one name contains the
        # other they are taken as the same blueprint, which is a weaker claim than it looks: it is
        # only ever consulted after the exact match fails, and it still has to agree on the module
        # type below.
        if not candidates:
            candidates = [
                name
                for known_label, names in by_label.items()
                if known_label.startswith(label) or label.startswith(known_label)
                for name in names
            ]

        # The one whose module type covers a module of this row's kind. A single candidate needs no
        # deciding; several with no agreeing type is a miss, and it is reported rather than guessed.
        fitting = [
            name for name in candidates
            if offering.get(name, set()) & types_of.get(relax(row[1]), set())
        ]

        if len(candidates) == 1:
            row[8] = candidates[0]
        elif len(fitting) == 1:
            row[8] = fitting[0]
        else:
            unnamed.append(f"{row[1]} / {row[2]}")

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

    # What each module type can be engineered with. Keyed by EDSY's `mtype`, which
    # EliteSpecifications.tsv carries against every module — see edsy_offers.
    text += ["[mtypes]", "\t".join(MTYPE_COLUMNS)]
    text += ["\t".join(row) for row in offers]

    OUTPUT.write_text("\n".join(text) + "\n", encoding="utf-8", newline="\n")

    named = sum(1 for row in built if row[8])

    print(f"Wrote {len(built)} rows to {OUTPUT}")
    print(f"Blueprints carrying Frontier's own name: {named} of {len(built)} "
          f"({len(built) - named} carry no guid, so nothing to key them by)")

    engineerable = sum(1 for row in offers if row[1])
    print(f"Module types: {len(offers)}, of which {engineerable} can be engineered and "
          f"{len(offers) - engineerable} genuinely cannot")

    # The one direction that is not survivable quietly. A blueprint EDSY offers that nothing here
    # supplies is a row a Commander can be shown and d47 cannot cost.
    supplied = {name for row in built for name in row[8].split(",") if name}
    wanted = {name for row in offers for name in row[1].split(",") if name}
    absent = sorted(wanted - supplied)

    if absent:
        print(f"{len(absent)} blueprints are offered by a module type and carry no recipe here:")
        for name in absent:
            print(f"  {name}")

    if unnamed:
        print(f"{len(unnamed)} recipes carry neither an id nor a name EDSY knows, so no module "
              f"type can reach them:")
        for line in sorted(set(unnamed))[:20]:
            print(f"  {line}")
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
