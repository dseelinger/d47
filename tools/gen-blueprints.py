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
           "symbol",

           # The module types this recipe belongs to. A shared blueprint has one row per kind and
           # they are not interchangeable — "Double Braced" has eight recipes — so a module needs
           # the row that is *its own*, not merely one carrying the right name.
           "mtypes"]


# Which blueprints each module type can take. See `edsy_offers`.
MTYPE_COLUMNS = ["mtype", "blueprints"]


# A module type EDSY splits and EDEngineer does not, mapped to the type whose recipes are its own.
#
# **One entry, and it is measured rather than reasoned.** EDSY files the Supercharged (SCO) drive
# under `cfsdo` and the ordinary one under `cfsd`; EDEngineer has a single module kind called
# "Frame Shift Drive", so every drive recipe landed on `cfsd` and an SCO drive — which is the drive
# essentially every Commander now flies — was offered eight blueprints d47 held no recipe for. The
# Loadout tab said so honestly and could do nothing about it.
#
# What settles it is the corpus and not the naming: across the 919-journal corpus, SCO drives
# carrying `FSD_LongRange` report `Modifiers` that the `cfsd` grade rows reproduce to 0.000%. The
# recipe is not merely *similar*, it is the same recipe filed under one name by a source that does
# not make the distinction. See docs/spikes/build-gauges.md.
#
# **Only where EDSY offers the variant that blueprint anyway**, applied below — so this widens what
# a type can be costed with and never what it is offered. The obvious generalisation was tried and
# rejected: filing any unanimous sibling recipe under an uncosted type puts *Sturdy Mount* on a
# cargo rack, because EDEngineer's weapon rows carry `CargoRack_IncreasedCapacity` as a second
# symbol. A rule that cannot tell those apart is a rule that invents game data.
MTYPE_ALIASES = {"cfsdo": "cfsd"}

# Engineering two trackers describe differently, removed from what d47 offers at all
# (<https://github.com/dseelinger/d47/issues/127>).
#
# **The Commander's rule, given 2026-09-01:** *"If the two trackers don't agree on an engineering
# item, remove that from d47's offered engineering."* It replaces a looser one from the same day
# — take what they agree on — which shipped this blueprint costed at the intersection of the two
# and left the disagreement out of the ingredient list. That is a worse failure than silence: a
# Commander gathers exactly what d47 asks for, flies to the workshop and cannot roll, and nothing
# on the page ever suggested the list might be short.
#
# **`GuardianModule_Sturdy` — Anti-Guardian Zone Resistance.** EDEngineer has no Guardian
# blueprint symbol at all and coriolis has none either, so the only two sources that describe it
# are EDSY and ED Odyssey Materials Helper. They agree that it exists, that it is Ram Tah's, that
# it has one grade, and on two of its ingredients — and they disagree about a third, Tactical Core
# Chip, which EDSY lists and EDOMH does not. EDSY is also malformed in exactly that spot:
# `maxgrade:1` against three `mats` groups, the only entry of the 65 carrying both fields where
# the counts disagree.
#
# So d47 says nothing about it. The offer is dropped as well as the recipe, because an offer with
# no recipe is still a claim — *"Frontier engineers this and I cannot cost it"* — and d47 is not
# in a position to make even that one about a blueprint whose two describers disagree.
#
# **This is not the same as the honest-gap machinery below**, which is for engineering d47 knows
# is real and cannot price. This is for engineering d47 cannot describe consistently, and the
# difference is whether the Commander is told something.
DISPUTED_OFFERS = {"GuardianModule_Sturdy"}


# Where coriolis and EDSY spell the same roll differently, and EDSY's is the spelling a module
# type actually offers.
#
# The pattern is already documented below for `Scanner_WideAngle` against `Sensor_WideAngle`;
# these are three more of it, and each one cost a whole module its engineering. EDSY files the
# Shielded roll on every one of these internals under a single generic name, and coriolis gives
# each module its own — so the row named a blueprint nothing offered, was set aside as stranded,
# and its module ended up belonging to no type at all. Reported 2026-08-20 against an Auto
# Field-Maintenance Unit whose row read "engineering is not available for this module".
#
# Applied only where the coriolis spelling is offered by nobody and EDSY's is offered by
# somebody, so this heals itself the day either side renames and never overrides a live name.
SPELLINGS = {
    "AFM_Shielded": "Misc_Shielded",
    "FuelScoop_Shielded": "Misc_Shielded",
    "Refineries_Shielded": "Misc_Shielded",
}

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

            # Engineering two trackers describe differently is not offered at all (#127). Here,
            # at the one place the offer table is built, so nothing downstream has to remember:
            # a symbol dropped here is invisible to the costing, to the uncosted report and to
            # every surface that reads either.
            and known[item][0] not in DISPUTED_OFFERS
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

            # The module types this recipe belongs to, filled below once the passes have worked
            # out which type each kind is.
            "",
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

    def candidates_for(name: str) -> list[str]:
        """Every blueprint EDSY publishes under this label, exactly then by containment.

        De-duplicated, which is not tidying: a label reached both exactly and by containment
        appeared twice, so "one candidate" counted as two and the row went unnamed. Kill Warrant
        Scanner's Wide Angle and every Multi-cannon experimental were lost to that alone.
        """
        label = relax(name)
        found = list(by_label.get(label, []))

        if not found:
            # The two sources spell a few labels differently — "Fast Scanner" against "Fast Scan".
            # Consulted only after the exact match fails, and still narrowed by module type below.
            found = [
                symbol
                for known_label, symbols in by_label.items()
                if known_label.startswith(label) or label.startswith(known_label)
                for symbol in symbols
            ]

        return sorted(set(found))

    def assign(row: list[str], scope: set) -> bool:
        found = candidates_for(row[2])

        if len(found) == 1:
            row[8] = found[0]
            return True

        fitting = [name for name in found if offering.get(name, set()) & scope]

        if len(fitting) == 1:
            row[8] = fitting[0]
            return True

        return False

    def scope_for(module: str) -> set:
        """The module types a recipe's own kind covers, by name then by containment.

        EDEngineer names three modules something d47 does not, and two of the three it names with
        a *part* of d47's name: its "Surface Scanner" is a Detailed Surface Scanner and its "Wake
        Scanner" a Frame Shift Wake Scanner. Containment reaches those without anybody writing the
        pair down. Its "Manifest Scanner" is a Cargo Scanner and shares no word at all, which is
        what the revealed pass below is for.
        """
        name = relax(module)

        if name in types_of:
            return types_of[name]

        return {
            code
            for known, codes in types_of.items()
            if known.startswith(name) or name.startswith(known) or name in known
            for code in codes
        }

    recipes = [row for row in built if row[0] in ("modification", "experimental")]

    # First pass: the rows a label settles on its own, or that the module type d47 already knows
    # settles. Everything Frontier and d47 spell the same way lands here.
    left = [row for row in recipes if not row[8]
            and not assign(row, scope_for(row[1]))]

    # What the first pass revealed. EDEngineer calls three modules something d47 does not — its
    # "Manifest Scanner" is a Cargo Scanner, its "Wake Scanner" a Frame Shift Wake Scanner — so
    # nothing above could scope them. But a kind whose *other* rows resolved has named its own
    # module type in doing so, and that is the scope its remaining rows want. Derived from the
    # data rather than from a list of three aliases somebody has to maintain.
    revealed: dict[str, set] = {}

    for row in recipes:
        if row[8]:
            for name in row[8].split(","):
                revealed.setdefault(relax(row[1]), set()).update(offering.get(name, set()))

    unnamed = []

    for row in left:
        if not assign(row, revealed.get(relax(row[1]), set())):
            unnamed.append(row)

    # Last resort, and the narrowest one there is. A module type offering exactly one blueprint
    # nothing has claimed, whose kind has exactly one recipe still unnamed, has only one pairing
    # available — which is how "Expanded Probe Scanning Radius" meets `Sensor_Expanded`, two names
    # for one thing that share no word.
    claimed = {row[8] for row in recipes if row[8]}

    for row in list(unnamed):
        # The revealed scope where a sibling row named one, and the module's own name otherwise —
        # a kind with a single recipe has no sibling to reveal anything, which is exactly the
        # Detailed Surface Scanner's position.
        scope = revealed.get(relax(row[1])) or scope_for(row[1])
        spare = {name for name, codes in offering.items()
                 if codes & scope and name not in claimed}
        siblings = {other[2] for other in unnamed if other[1] == row[1]}

        if len(spare) == 1 and len(siblings) == 1:
            # Every grade of it, not the first one met. A blueprint is one thing with five rows,
            # and claiming the name on row one left the other four looking unnamed.
            found = next(iter(spare))

            for grade in [other for other in unnamed
                          if other[1] == row[1] and other[2] == row[2]]:
                grade[8] = found
                unnamed.remove(grade)

            claimed.add(found)

    unnamed = [f"{row[1]} / {row[2]}" for row in unnamed]

    # Narrow the names the guid supplied to the ones this row's own module type actually offers.
    # coriolis files a single recipe under several names — the armour Lightweight roll answers to
    # seven, six of them belonging to limpet controllers and weapons — so an unnarrowed row matches
    # every module type that offers any of them. A Collector Limpet Controller would be offered the
    # armour recipe, which is the defect this whole item is about arriving by a side door.
    #
    # Last, because it needs the scope the passes above revealed: EDEngineer calls a hull's armour
    # "Armour" and d47 calls it "Type-10 Defender Lightweight Alloy", so nothing but those passes
    # knows the two are the same thing.
    for row in recipes:
        if "," not in row[8]:
            continue

        scope = revealed.get(relax(row[1])) or scope_for(row[1])
        kept = [name for name in row[8].split(",") if offering.get(name, set()) & scope]

        if kept:
            row[8] = ",".join(kept)

    # Which module type each recipe kind *is*, and so which recipes a module may take.
    #
    # Matched on the blueprint names themselves, which are now ids on both sides: a kind's whole
    # set of blueprints should sit inside the offer of the module type it belongs to, and the
    # right type is the **smallest** offer that contains it. A Manifest Scanner's six names are
    # exactly `ucs`'s six; a Chaff Launcher's four sit inside `ucl`'s four and inside nothing
    # smaller. Every earlier attempt scored overlap instead and tied the scanners with the Chaff
    # Launcher, because overlap rewards a big offer and containment does not.
    # One entry per recipe, holding the names that recipe answers to — **alternatives, not a
    # requirement**. A row carrying three names needs any one of them offered, because the three
    # are coriolis's aliases for a single roll and only one belongs to this module.
    wanted: dict[str, list[set]] = {}

    offered_by = {code: set(listed.split(",")) - {""} for code, listed in offers}

    anywhere = set().union(*offered_by.values()) if offered_by else set()

    # coriolis's spelling for EDSY's, where only EDSY's is offered. See SPELLINGS.
    respelt = []

    for row in recipes:
        names = [name for name in row[8].split(",") if name]

        if not names or set(names) & anywhere:
            continue

        swapped = [SPELLINGS.get(name, name) for name in names]

        if set(swapped) & anywhere:
            respelt.append(f"{row[1]} / {row[2]}: {row[8]} -> {','.join(swapped)}")
            row[8] = ",".join(swapped)

    stranded = []

    for row in recipes:
        names = {name for name in row[8].split(",") if name}

        if not names:
            continue

        # A recipe no module type offers at all cannot say anything about which type its kind is,
        # so it is set aside rather than allowed to veto every candidate. coriolis and EDSY spell
        # one blueprint differently — `Scanner_WideAngle` against `Sensor_WideAngle` — and that one
        # row was enough to leave the Manifest Scanner, the Kill Warrant Scanner and three limpet
        # controllers belonging to nothing.
        if not names & anywhere:
            stranded.append(f"{row[1]} / {row[2]} ({row[8]})")
            continue

        wanted.setdefault(relax(row[1]), []).append(names)

    fits = {
        kind: [code for code, offer in sorted(offered_by.items())
               if offer and all(names & offer for names in rows)]
        for kind, rows in wanted.items()
    }

    # **One mis-keyed row must not veto a whole module.**
    #
    # Containment above asks that *every* row of a kind sit inside the type's offer, and the pass
    # before it already sets aside a row naming a blueprint nobody offers — on the grounds that
    # such a row cannot say anything about which type its kind is. A row naming a blueprint some
    # *other* type offers is just as uninformative and was not covered: it survived to veto.
    #
    # Measured cost, 2026-08-20: the Plasma Accelerator's "Plasma Slug" carries coriolis's
    # `special_plasma_slug_cooled`, which is the Railgun's cooled variant and a real blueprint of
    # its own — so aliasing it would be wrong. `hpa` offers plain `special_plasma_slug` and covers
    # the other fifty rows exactly. That single row left all fifty-one belonging to nothing, and
    # no Plasma Accelerator in the game could be engineered.
    #
    # **At most one dissenter, and it is named.** Two would mean something else is wrong and the
    # answer is to look rather than to widen the tolerance until it passes.
    outvoted = []

    for kind, rows in sorted(wanted.items()):
        if fits.get(kind) or len(rows) < 3:
            continue

        scored = [
            (sum(1 for names in rows if names & offer), -len(offer), code)
            for code, offer in sorted(offered_by.items()) if offer
        ]

        if not scored:
            continue

        covered, _, code = max(scored)

        if covered != len(rows) - 1:
            continue

        fits[kind] = [code]
        missed = sorted({name for names in rows if not names & offered_by[code] for name in names})
        outvoted.append(f"{kind} -> {code}, outvoting one row naming {','.join(missed)}")

    belongs, contested = {}, []

    # The module's own name first, wherever it picks out exactly one of the types that fit.
    # EDEngineer's "Wake Scanner" sits inside d47's "Frame Shift Wake Scanner"; its "Life Support"
    # is a Life Support. Size cannot settle those — Life Support's three generic rolls fit inside
    # the AFM Unit's three just as well — and a name can.
    for kind, codes in sorted(fits.items()):
        named = [code for code in codes if code in scope_for(kind)]

        if len(named) == 1:
            belongs[kind] = named[0]

    # Then the rest, as a **maximum matching rather than first-come-first-served**.
    #
    # One kind to one type either way — two kinds on one type would leave the other type with no
    # recipes, which reads as a module that cannot be engineered, the very defect this is about.
    # What changed is that taking a type is no longer final: a kind that finds its only candidate
    # already taken now asks the holder to move, and the holder moves if it has anywhere else to
    # go. Greedy alphabetical order could not do that, and the Plasma Accelerator was the cost —
    # every weapon type that fits it had been claimed by a laser earlier in the alphabet, so all
    # fifty-one of its rows belonged to nothing and no Plasma Accelerator could be engineered.
    #
    # Kuhn's algorithm, which is the textbook one for this and is a dozen lines. Deterministic:
    # kinds are taken in sorted order and each one's candidates in a fixed order — smallest offer
    # first, which keeps the preference the greedy pass was reaching for, and the code as a
    # tie-break so a rerun cannot shuffle two equal candidates.
    pinned = set(belongs.values())

    order = {
        kind: sorted(
            (code for code in codes if code not in pinned),
            key=lambda code: (len(offered_by[code]), code))
        for kind, codes in sorted(fits.items())
        if kind not in belongs
    }

    owner: dict[str, str] = {}

    def settle(kind: str, seen: set) -> bool:
        """Give `kind` a type, moving whoever holds it if that one has somewhere else to go."""
        for code in order.get(kind, []):
            if code in seen:
                continue

            seen.add(code)

            if code not in owner or settle(owner[code], seen):
                owner[code] = kind
                return True

        return False

    for kind in order:
        settle(kind, set())

    for code, kind in owner.items():
        belongs[kind] = code

    # Said out loud, because a kind with no type is a module whose engineering nobody can reach —
    # and this is the number that was silently 4 for as long as the greedy pass ran.
    for kind, codes in order.items():
        if kind not in belongs and codes:
            contested.append(f"{kind}: fits {', '.join(codes)} and every one is taken")

    for row in recipes:
        row[9] = belongs.get(relax(row[1]), "")

    # One more naming pass, now that each kind's module type is settled. It is the scope the
    # earlier passes wanted and could not have: "Manifest Scanner / Long Range Scanner" is
    # `Sensor_LongRange` and not `Weapon_LongRange`, and only the type says which.
    for row in recipes:
        if not row[9]:
            continue

        # Also re-named where the row's own module type disowns the name it carries. coriolis and
        # EDSY spell one blueprint differently — `Scanner_WideAngle` against `Sensor_WideAngle` —
        # and the guid handed over coriolis's, which no module type offers, so a Cargo Scanner lost
        # a blueprint it really has.
        if row[8] and not ({name for name in row[8].split(",") if name}
                           & offered_by.get(row[9], set())):
            was = row[8]
            row[8] = ""

            # Put it back where nothing better is available. A name its own type does not offer is
            # still the name coriolis has for it, and is better than none.
            if not assign(row, {row[9]}):
                row[8] = was

        if not row[8]:
            assign(row, {row[9]})

    unnamed = [line for line in unnamed
               if line not in {f"{row[1]} / {row[2]}" for row in recipes if row[8]}]


    # A variant type inherits the recipes of the type it is a variant of, but only for the
    # blueprints EDSY says it takes. See MTYPE_ALIASES for why there is exactly one of these and
    # why the general rule was rejected.
    offered_to = {row[0]: {name for name in row[1].split(",") if name} for row in offers}
    inherited = 0

    for row in recipes:
        if not row[9] or not row[8]:
            continue

        names = {name for name in row[8].split(",") if name}

        also = [
            variant for variant, base in MTYPE_ALIASES.items()
            if base == row[9] and names & offered_to.get(variant, set())
        ]

        if also:
            row[9] = ",".join([row[9], *sorted(also)])
            inherited += 1

    # Every offer still without a recipe, named rather than counted, because this is the gap
    # Phase 38 item 10 is about and a list can be checked against the outfitting screen.
    # The remainder is genuine absence: EDEngineer carries no Guardian weapon recipes at all, so
    # Anti-Guardian Zone Resistance has no ingredients here. EDSY has an entry and it cannot be
    # used: it marks the blueprint's own symbol `// TODO`, its three materials are asserted by
    # nothing else — FDevIDs has no row for any of them, by name or by symbol, re-checked
    # 2026-09-01 — and its `maxgrade` disagrees with its `mats` count, which is true of no other
    # entry in that file. A lead is not a recipe. See docs/spikes/build-gauges.md §4.
    costed: dict[str, set] = {}

    for row in recipes:
        for code in row[9].split(","):
            if code:
                costed.setdefault(code, set()).update(
                    name for name in row[8].split(",") if name)

    uncosted = sorted(
        (code, name)
        for code, names in offered_to.items()
        for name in names
        if name not in costed.get(code, set()))

    unplaced = sorted({row[1] for row in recipes if not row[9]})
    stranded = sorted(set(stranded))

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

    if respelt:
        print(f"{len(respelt)} recipes carried a spelling no module type offers and were "
              f"respelt to EDSY's — see SPELLINGS:")
        for line in respelt:
            print(f"  {line}")

    if outvoted:
        print(f"{len(outvoted)} recipe kinds were placed over one dissenting row — check each, a "
              f"dissenter is usually another module's blueprint mis-keyed onto this one:")
        for line in outvoted:
            print(f"  {line}")

    if contested:
        print(f"{len(contested)} recipe kinds could not be given a module type of their own:")
        for line in contested:
            print(f"  {line}")

    if stranded:
        print(f"{len(stranded)} recipes name a blueprint no module type offers, so they were set "
              f"aside when working out what their kind is:")
        for line in stranded:
            print(f"  {line}")

    if unplaced:
        print(f"{len(unplaced)} recipe kinds sit inside no module type's offer, so no module "
              f"reaches them: {', '.join(unplaced)}")

    if unnamed:
        print(f"{len(unnamed)} recipes carry neither an id nor a name EDSY knows, so no module "
              f"type can reach them:")
        for line in sorted(set(unnamed))[:20]:
            print(f"  {line}")
    if inherited:
        print(f"{inherited} recipes were also filed under a variant module type EDSY splits and "
              f"EDEngineer does not: {', '.join(sorted(MTYPE_ALIASES))}")

    # The state Phase 38 item 10 describes, reported every run rather than pinned to a
    # known list: a run that stops naming one of these is the signal that a source has filled it in.
    if uncosted:
        print(f"{len(uncosted)} offers have no recipe anywhere, so the surface says so rather than "
              f"drawing one:")
        for code, name in uncosted:
            print(f"    {code} is offered {name}")

    print("Engineering withheld because two trackers describe it differently (#127): "
          + (", ".join(sorted(DISPUTED_OFFERS)) if DISPUTED_OFFERS else "none"))

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
