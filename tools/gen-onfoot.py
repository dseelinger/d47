#!/usr/bin/env python3
"""Regenerate d47's on-foot equipment table.

Not part of the build. Its output is committed as
`src/D47.Core/Knowledge/OnFoot.tsv` and shipped as an embedded resource; the app never runs
this and never reaches the network for it. Run it when Frontier adds a suit or a weapon:

    python tools/gen-onfoot.py

What it answers
---------------
Phase 20 asks what the Commander is wearing and what grade it is. The journal writes a symbol
— `explorationsuit_class3`, `wpn_m_sniper_plasma_charged` — and the id lists this repo already
uses cannot name either: **FDevIDs has no suit list and no hand-weapon list**, and
`outfitting.csv` is ship modules only. That inverts the ship arrangement, where the id list
named things and the journal merely agreed.

`EDDiscovery/EliteDangerousCore` `Items/Suits.cs` and `Items/HandItems.cs` (Apache-2.0) close
it. Both are keyed on exactly the symbol the journal writes, and a 912-journal corpus
exercises 9 of the 16 wearable suit symbols and 7 of the 11 weapons, and agrees on every
one.

Names join. Numbers do not.
--------------------------
EDDiscovery also carries per-grade `SuitStats` and `WeaponStats`, and they are a lead rather
than a table: the provenance varies per figure (`rob checked 20/8/21 for all suits to class 3
in game, class 4/5 according to wiki`), one weapon row is annotated `TBD Guess`, and
`SuitStats` has a transcription bug that assigns the health multipliers to the shield fields.
So this takes the names, the classes and the tools, and leaves every number alone.

Prices are measured rather than sourced
---------------------------------------
No permissive source found carries the purchase price of a suit or a weapon. The nine below
are read off `BuySuit` / `BuyWeapon` events in the corpus, which is the game itself; the rest
are left blank rather than guessed, because the credit cost of an upgrade is the base price
times a fixed multiplier and a wrong base is a wrong answer at every grade. See
`docs/spikes/journal-corpus-on-foot.md` §4.

Only the grade 1 row of a suit carries a price, because the other four are separate symbols
and repeating the base across them would read as a class 5 Artemis costing 150,000.

The modification alias table, and why it is authored
----------------------------------------------------
The journal names a fitted modification `weapon_clipsize`; every recipe source names it
"Magazine Size". **Nothing checked carries both spellings** — not EDEngineer, not FDevIDs, not
EDDiscovery's `Items` tables — and a relaxed match joins only 5 of the 13 symbols the corpus
contains. That is the `Engine_Dirty` problem again, and the ship side answered it by refusing
to guess, because 786 blueprints is not a table anybody can author.

**25 modifications is.** So the map below is written out, and it is a spelling reconciliation
rather than game data — the same category as the `Ballistic Data` alias in `gen-engineers.py`.
It names the modification **family**: three of them exist once per weapon manufacturer, and the
recipe table spells those "Greater range (Takada laser)". The journal has one symbol for all
three, so the family is what a symbol can honestly resolve to and the manufacturer comes from
the weapon the mod is fitted to.
Every row marked `corpus` was observed in a journal. Every row marked `derived` follows
Frontier's own naming convention and has **not** been seen in a journal, so it is emitted with
that provenance and a symbol d47 has never had to read is never silently trusted.

Note on the data: game data is Frontier's, used under their media usage rules. See `NOTICE`.
"""

import datetime
import re
import urllib.request
from pathlib import Path

# Pinned, because these are C# source rather than data files with a schema: the tables are
# parsed out of dictionary initialisers, and a restructure upstream would change what the
# regexes mean rather than break them. Bump deliberately.
EDDISCOVERY_COMMIT = "459d01dc2bccf688019c2f8e45c3909277a4e316"

EDDISCOVERY = (
    f"https://raw.githubusercontent.com/EDDiscovery/EliteDangerousCore/{EDDISCOVERY_COMMIT}"
    "/EliteDangerous/FrontierData/Items/{file}"
)

ROOT = Path(__file__).resolve().parent.parent

OUTPUT = ROOT / "src" / "D47.Core" / "Knowledge" / "OnFoot.tsv"

COLUMNS = ["kind", "symbol", "name", "grade", "detail", "price", "seen"]

# Hard assertions rather than sanity checks. These are regexes over somebody's source code and
# the failure mode without them is a table that half-populates and reads as correct — d47
# naming four suits and silently having no idea what the fifth is.
#
# Both suit counts are asserted although only one is emitted. The NPC suits are two thirds of
# that table and share its shape, so a change upstream that moved rows between the two groups
# would otherwise show up as a wearable-suit count that happens to still be right.
EXPECTED_SUITS = 16
EXPECTED_NPC_SUITS = 29
EXPECTED_WEAPONS = 11
EXPECTED_TOOLS = 5

# What the Commander can wear is everything that is not somebody else's. Frontier marks every
# NPC suit the same way, down to the AI's own flight suit.
NPC = "suitai"

# Grade 1 purchase prices, read off BuySuit / BuyWeapon in the journal corpus. Blank is the
# honest answer for anything not bought there; see the module docstring.
PRICES = {
    "flightsuit": 0,
    "tacticalsuit": 150000,
    "explorationsuit": 150000,
    "utilitysuit": 150000,
    "wpn_m_assaultrifle_laser_fauto": 125000,
    "wpn_m_assaultrifle_kinetic_fauto": 125000,
    "wpn_m_launcher_rocket_sauto": 175000,
    "wpn_s_pistol_plasma_charged": 50000,
    "wpn_m_sniper_plasma_charged": 175000,
}

# The join nothing publishes. "corpus" means the symbol was read out of a real SuitLoadout;
# "derived" means it follows the convention and has never been seen, which is a different
# quality of claim and is carried as one.
MODIFICATIONS = [
    # symbol,                              blueprint name,                  kind,      provenance
    ("suit_improvedjumpassist",            "Improved jump assist",          "suitmod",   "corpus"),
    ("suit_quieterfootsteps",              "Quieter footsteps",             "suitmod",   "corpus"),
    ("suit_nightvision",                   "Night vision",                  "suitmod",   "corpus"),
    ("suit_increasedsprintduration",       "Increased sprint duration",     "suitmod",   "corpus"),
    ("suit_increasedbatterycapacity",      "Improved battery capacity",     "suitmod",   "corpus"),
    ("suit_increasedammoreserves",         "Extra ammo capacity",           "suitmod",   "corpus"),
    ("suit_increasedshieldregen",          "Faster shield regen",           "suitmod",   "corpus"),
    ("suit_improvedarmourrating",          "Damage resistance",             "suitmod",   "corpus"),
    ("suit_increasedmeleedamage",          "Added melee damage",            "suitmod",   "derived"),
    ("suit_adsmovementspeed",              "Combat Movement Speed",         "suitmod",   "derived"),
    ("suit_improvedradar",                 "Enhanced tracking",             "suitmod",   "derived"),
    ("suit_backpackcapacity",              "Extra backpack capacity",       "suitmod",   "derived"),
    ("suit_increasedo2capacity",           "Increased air reserves",        "suitmod",   "derived"),
    ("suit_reducedtoolbatteryconsumption", "Reduced tool battery consumption", "suitmod", "derived"),
    ("weapon_stability",                   "Stability",                     "weaponmod", "corpus"),
    ("weapon_handling",                    "Faster handling",               "weaponmod", "corpus"),
    ("weapon_clipsize",                    "Magazine size",                 "weaponmod", "corpus"),
    ("weapon_backpackreloading",           "Stowed reloading",              "weaponmod", "corpus"),
    ("weapon_suppression_unpressurised",   "Noise suppressor",              "weaponmod", "corpus"),
    ("weapon_audiomasking",                "Audio Masking",                 "weaponmod", "derived"),
    ("weapon_range",                       "Greater range",                 "weaponmod", "derived"),
    ("weapon_headshotdamage",              "Headshot damage",               "weaponmod", "derived"),
    ("weapon_accuracy",                    "Improved Hip Fire Accuracy",    "weaponmod", "derived"),
    ("weapon_reloadspeed",                 "Reload speed",                  "weaponmod", "derived"),
    ("weapon_scope",                       "Scope",                         "weaponmod", "derived"),
]

# The game names 25, and this table has to name all of them or a Commander's fitted mod comes
# back as a raw symbol. Read from the Fandom category page, 2026-08-16.
EXPECTED_MODIFICATIONS = 25

SUIT = re.compile(
    r'new SuitFDName\("(?P<symbol>[a-z0-9_]+)"\)\s*,\s*new Suit\(\s*'
    r'"(?P<name>[^"]+)"\s*,\s*(?P<cls>\d+)\s*,\s*(?P<primary>\d+)\s*,\s*(?P<secondary>\d+)\s*,'
    r'\s*"(?P<u1>[^"]*)"\s*,\s*"(?P<u2>[^"]*)"\s*,\s*"(?P<u3>[^"]*)"'
)

WEAPON = re.compile(
    r'new HandItemFDName\("(?P<symbol>[a-z0-9_]+)"\)\s*,\s*new Weapon\(\s*'
    r'"(?P<name>[^"]+)"\s*,\s*(?P<primary>true|false)\s*,\s*'
    r'Weapon\.WeaponDamageType\.(?P<damage>\w+)\s*,\s*'
    r'Weapon\.HandItemClass\.(?P<shape>\w+)'
)

TOOL = re.compile(
    r'new HandItemFDName\("(?P<symbol>[a-z0-9_]+)"\)\s*,\s*new HandItem\(\s*'
    r'"(?P<name>[^"]+)"\s*,\s*HandItem\.HandItemClass\.(?P<shape>\w+)'
)


def fetch(file: str) -> str:
    request = urllib.request.Request(
        EDDISCOVERY.format(file=file), headers={"User-Agent": "d47-gen-onfoot"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read().decode("utf-8-sig")


def price_of(symbol: str, grade: str) -> str:
    """The grade 1 price, and only on the grade 1 row.

    Every higher grade of a suit is a separate symbol, so repeating the family's base price
    across all five would read as "a class 5 Artemis costs 150,000", which is wrong by a
    factor of thirty. A blank says the row's own price is not known, which is true, and the
    upgrade arithmetic reads the family's grade 1 row instead.
    """
    if grade not in ("", "1"):
        return ""

    family = symbol.split("_class")[0]

    for key in (symbol, family):
        if key in PRICES:
            return str(PRICES[key])

    return ""


def main() -> None:
    suits_source = fetch("Suits.cs")
    hand_source = fetch("HandItems.cs")

    built: list[list[str]] = []

    parsed = list(SUIT.finditer(suits_source))
    suits = [match for match in parsed if NPC not in match["symbol"]]
    npc = [match for match in parsed if NPC in match["symbol"]]

    for match in suits:
        # The tool a suit carries beyond the two everything has. Blank for the combat suits,
        # which is the answer rather than a gap.
        tool = match["u3"] or ""

        built.append([
            "suit",
            match["symbol"],
            match["name"],
            match["cls"] if match["cls"] != "0" else "",
            f'{match["primary"]}+{match["secondary"]} weapons' + (f", {tool}" if tool else ""),
            price_of(match["symbol"], match["cls"] if match["cls"] != "0" else ""),
            "",
        ])

    weapons = list(WEAPON.finditer(hand_source))

    for match in weapons:
        built.append([
            "weapon",
            match["symbol"],
            match["name"],
            "",
            f'{match["damage"].lower()} {match["shape"].lower()}'
            + (", primary" if match["primary"] == "true" else ", sidearm"),
            price_of(match["symbol"], ""),
            "",
        ])

    tools = list(TOOL.finditer(hand_source))

    for match in tools:
        built.append(["tool", match["symbol"], match["name"], "", match["shape"].lower(), "", ""])

    for symbol, name, kind, seen in MODIFICATIONS:
        built.append([kind, symbol, name, "", "", "", seen])

    check(len(suits), EXPECTED_SUITS, "wearable suits")
    check(len(npc), EXPECTED_NPC_SUITS, "NPC suits")
    check(len(weapons), EXPECTED_WEAPONS, "weapons")
    check(len(tools), EXPECTED_TOOLS, "hand tools")
    check(len(MODIFICATIONS), EXPECTED_MODIFICATIONS, "modifications")

    duplicates = [s for s in {row[1] for row in built} if [r[1] for r in built].count(s) > 1]

    if duplicates:
        raise SystemExit(f"symbols must be unique and these are not: {sorted(duplicates)}")

    built.sort(key=lambda row: (row[0], row[1]))

    stamp = datetime.date.today().isoformat()
    corpus = sum(1 for _s, _n, _k, seen in MODIFICATIONS if seen == "corpus")

    text = [
        "# Generated by tools/gen-onfoot.py. Do not edit by hand — rerun the tool.",
        "# Suit, hand weapon and hand tool identity from EDDiscovery/EliteDangerousCore",
        "# Items/Suits.cs and Items/HandItems.cs (Apache-2.0), keyed on the symbol the journal",
        "# writes. No stat is taken from there: its per-figure provenance varies, one row is",
        "# annotated as a guess, and SuitStats assigns health multipliers to the shield fields.",
        "# Prices are grade 1 and were read off BuySuit/BuyWeapon in a journal corpus; blank",
        "# means not measured rather than free. The suitmod/weaponmod rows are a spelling",
        "# reconciliation, not game data: nothing published carries both the journal's symbol",
        f"# and the recipe's name. seen=corpus means observed in a real journal ({corpus} of",
        f"# {len(MODIFICATIONS)}); seen=derived means it follows the convention and has not been.",
        "# Game data is Frontier's, used under their media usage rules; see NOTICE.",
        f"# Rows: {len(built)} (suits {len(suits)}, weapons {len(weapons)}, tools {len(tools)}, "
        f"modifications {len(MODIFICATIONS)}). Built: {stamp}.",
        "\t".join(COLUMNS),
    ]

    text += ["\t".join(row) for row in built]

    OUTPUT.write_text("\n".join(text) + "\n", encoding="utf-8", newline="\n")

    print(f"Wrote {len(built)} rows to {OUTPUT}")
    print(f"Suits {len(suits)}, weapons {len(weapons)}, tools {len(tools)}, "
          f"modifications {len(MODIFICATIONS)} ({corpus} seen in a journal, "
          f"{len(MODIFICATIONS) - corpus} derived from the naming convention)")
    print(f"Prices known for {sum(1 for row in built if row[5])} of "
          f"{len(suits) + len(weapons)} purchasable items")


def check(found: int, expected: int, what: str) -> None:
    if found != expected:
        raise SystemExit(
            f"expected {expected} {what} and parsed {found}. A partial parse reads as a correct "
            "table, so this is a stop rather than a warning — check whether the source moved."
        )


if __name__ == "__main__":
    main()
