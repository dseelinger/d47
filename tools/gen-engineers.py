#!/usr/bin/env python3
"""Regenerate d47's engineer directory.

Not part of the build. Its output is committed as
`src/D47.Core/Knowledge/Engineers.tsv` and shipped as an embedded resource; the app never
runs this and never reaches the network for it. Run it when Frontier adds an engineer or
moves one:

    python tools/gen-engineers.py

What it answers
---------------
Phase 14 asks where each engineer is and what they grade. Neither is in the journal: the
journal reports this Commander's *progress* with an engineer, and says nothing about where
they live or what they offer until you have already been there.

Three sources, and each is the authority on exactly one thing
------------------------------------------------------------
`EDCD/FDevIDs` `engineers.csv` is the identity: the engineer id the journal writes, their
name, the id64 of their system and the market id of their base. It carries no names for
either place.

`spansh.co.uk` turns those two ids into names. `api/system/<id64>` answers with the system
and its stations, and the station whose `market_id` matches is the base. This is the one
network call the generator makes per engineer, and it is made *here* rather than at runtime
precisely so a Commander asking "where is Farseer" gets an answer with no network at all.

`msarilar/EDEngineer` (MIT) `blueprints.json` is what they grade: every blueprint carries
the engineers who offer it and the grade it goes to, so grouping by engineer gives their
speciality list and the top grade they reach in each.

The referral chain, and the one place a source is overruled
-----------------------------------------------------------
list.md asks for "who in the chain of unlocks", and the reason this script once had no answer
was recorded as a verdict when it was only a fact about three files: FDevIDs has no unlock
column, coriolis carries blueprints and not requirements, and EDEngineer's ship rows name only
the engineer being unlocked. All true, and none of it about the world.

`EDDiscovery/EliteDangerousCore` `Items/Engineers.cs` has the graph — **C# source rather than
data**, so it is a regex over `new EngineeringInfo(...)` initialisers with a hard assertion of
38 rows. Without that assertion the failure mode is a table that half-populates and reads as
correct: d47 saying an engineer has no referrer when the parse simply missed the line.

**Bill Turner is overruled.** EDDiscovery calls him "Common knowledge"; the Fandom engineer
table says Selene Jean, and a journal trace decides it in the wiki's favour — see
`docs/spikes/journal-corpus-engineering.md` §4. The two sources agree on the other 37, so the
one disagreement is stated in the code rather than quietly patched.

**The on-foot chain states no grade, and is not given one.** Ship referrals read "From Hera Tani
(grade 3-4)"; Odyssey referrals read "From Domino Green" and nothing more, because those unlock
on a count of modifications rather than on a grade. Defaulting them to 3 would invent a
requirement the game does not have. One engineer, Yi Shen, names three referrers and any will do.
"""

import csv
import io
import json
import re
import urllib.request
from collections import defaultdict
from pathlib import Path

FDEV_IDS = "https://raw.githubusercontent.com/EDCD/FDevIDs/master/engineers.csv"

BLUEPRINTS = ("https://raw.githubusercontent.com/msarilar/EDEngineer/master/"
              "EDEngineer/Resources/Data/blueprints.json")

SPANSH_SYSTEM = "https://spansh.co.uk/api/system/{id64}"

# Pinned, because this one is C# source rather than a data file with a schema: the referral
# graph is parsed out of `new EngineeringInfo(...)` initialisers, and a restructure upstream
# would change what the regex means rather than break it. Bump deliberately.
EDDISCOVERY_COMMIT = "459d01dc2bccf688019c2f8e45c3909277a4e316"

EDDISCOVERY = (
    f"https://raw.githubusercontent.com/EDDiscovery/EliteDangerousCore/{EDDISCOVERY_COMMIT}"
    "/EliteDangerous/FrontierData/Items/Engineers.cs"
)

# Every engineer in the game. A hard assertion rather than a sanity check: this is a regex over
# somebody's source code, and the failure mode without it is a table that half-populates and
# looks fine — d47 saying an engineer has no referrer when it simply failed to read the line.
EXPECTED_ENGINEERS = 38

OUTPUT = Path(__file__).resolve().parent.parent / "src" / "D47.Core" / "Knowledge" / "Engineers.tsv"

COLUMNS = [
    "id", "name", "system", "station", "tribute", "specialities",
    "referred_by", "referral_grade", "body", "discovery", "meeting", "unlock", "reputation",
]

# Not engineers. EDEngineer uses these to model the Odyssey vendors and synthesis, which sit
# in the same blueprint list and would otherwise appear in the directory as people.
#
# The plan for this step asked for the `@Merchant` rows to be emitted here rather than dropped,
# on the grounds that they are Phase 20's entire on-foot vocabulary. They are — and `Blueprints.tsv`
# already carries all 56 of them as `kind=merchant`, with full recipes, which did not exist when
# that was written. Emitting them a second time would put four pseudo-people into a directory of
# people to duplicate a table that already has them, so the filter stays.
NOT_PEOPLE = "@"

# The blueprint list models an engineer's invitation task as a blueprint of its own, whose
# "ingredients" are the tribute they want. It is not a modification and does not belong in a
# speciality list — but it is the one piece of "who unlocks what" that has a source.
UNLOCK = "Unlock"

# EDEngineer's two on-foot blueprint types.
ON_FOOT = {"Suit", "Weapon"}

# The Odyssey unlock quantities are stale, and they are **stated rather than patched**.
#
# Frontier's 4.0.18.08 notes cut four of them and EDEngineer still carries the pre-patch numbers.
# Correcting them per ingredient was tried and abandoned, because the two sources are not counting
# the same thing: EDEngineer's Odyssey `Unlock` rows are **cumulative along the referral chain** —
# the row that unlocks Kit Fowler carries Domino Green's Push ×5 as well as the Opinion Polls, and
# the row that unlocks Wellington Beck carries Hero Ferrari's Settlement Defence Plans. Frontier's
# notes are per engineer. So EDEngineer's 40 Opinion Polls is not a stale version of Frontier's 5;
# it is a different quantity with a different meaning, and quietly replacing one with the other
# would produce a number belonging to neither.
#
# This is the column whose failure wastes a Commander's trip rather than merely their materials, so
# it says so out loud instead. Keyed the way `relax` leaves a name — lower case, no spaces.
TRIBUTE_NOTES = {
    "kitfowler":
        "Frontier's update 4.0.18.08 cut Opinion Polls to 5; this list is EDEngineer's and is "
        "cumulative along the chain, so check it before you fly",
    "yardenbond":
        "Frontier's update 4.0.18.08 cut Smear Campaign Plans to 5",
    "wellingtonbeck":
        "Frontier's update 4.0.18.08 cut Settlement Defence Plans to 5 and the three "
        "entertainment kinds to 15 in total rather than 15 of each",
    "odengeiger":
        "EDEngineer's quantities here predate Frontier's update 4.0.18.08 and no per-engineer "
        "figure was found for them",
}

# The four Colonia engineers are absent from EDEngineer entirely, and they are not data-less: they
# carry the widest modification lists of the thirteen, so a Colonia Commander reaches nearly
# everything through three of them where a Bubble Commander needs nine. Read from the Fandom
# Engineers page at source on 2026-08-15 and recorded in
# docs/spikes/on-foot-engineering-sources.md §6a, because that page answers 402 to an automated
# fetch and so cannot be a source this script reads.
#
# Kept small and kept here rather than being folded into a table pretending EDEngineer supplied it.
# The `colonia` provenance marker is emitted with the row so d47 can say where it came from.
COLONIA = {
    "baltanos": [
        "Combat Movement Speed", "Improved Jump Assist", "Increased Air Reserves",
        "Increased Sprint Duration", "Faster Handling", "Improved Hip Fire Accuracy",
        "Noise Suppressor",
    ],
    "eleanorbresa": [
        "Added Melee Damage", "Damage Resistance", "Extra Ammo Capacity", "Faster Shield Regen",
        "Magazine Size", "Reload Speed", "Stowed Reloading",
    ],
    "rosadayette": [
        "Enhanced Tracking", "Extra Backpack Capacity", "Improved Battery Capacity",
        "Reduced Tool Battery Consumption", "Greater Range", "Scope", "Stability",
    ],
    "yishen": ["Night Vision", "Quieter Footsteps", "Audio Masking", "Headshot Damage"],
}


def relax(name: str) -> str:
    """A name reduced to what two sources can be expected to agree on.

    The id list writes "Tod 'The Blaster' McQuinn" and the blueprint list writes "Tod
    McQuinn". Dropping the quoted nickname and the punctuation joins them; without this his
    entire speciality list is silently lost, which reads as an engineer who grades nothing.
    """
    stripped, quoted = [], False

    for character in name:
        if character in "'‘’\"":
            quoted = not quoted
        elif not quoted:
            stripped.append(character)

    return "".join(c.lower() for c in "".join(stripped) if c.isalnum())


def fetch(url: str, timeout: int = 60) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "d47-gen-engineers"})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read()


def blueprints() -> tuple[dict[str, list[tuple[str, int]]], dict[str, str], set[str]]:
    """What each engineer grades, and what they want to be persuaded.

    Returns both keyed by the relaxed name, plus the names as the blueprint list spells them
    so a mismatch against the id list can be reported rather than swallowed.
    """
    entries = json.loads(fetch(BLUEPRINTS))

    best: dict[str, dict[str, int]] = defaultdict(dict)
    tribute: dict[str, str] = {}
    spelled: set[str] = set()

    for blueprint in entries:
        kind = (blueprint.get("Type") or "").strip()
        grade = blueprint.get("Grade") or 0

        if not kind:
            continue

        if kind == UNLOCK:
            # The row's *name* is the engineer being unlocked, on all 24 rows. The `Engineers`
            # array is not, on six of them.
            unlocked = relax((blueprint.get("Name") or "").strip())

            tribute[unlocked] = ", ".join(
                f"{(item.get('Name') or '').strip()} ×{item.get('Size') or 0}"
                for item in blueprint.get("Ingredients") or []
                if item.get("Name")
            )

            if unlocked in TRIBUTE_NOTES:
                tribute[unlocked] += f" ({TRIBUTE_NOTES[unlocked]})"

            continue

        for engineer in blueprint.get("Engineers") or []:
            if engineer.startswith(NOT_PEOPLE):
                continue

            spelled.add(engineer)
            key = relax(engineer)

            if kind == UNLOCK:
                # Deliberately NOT filed under `engineer`. See the loop below: an Unlock row is
                # named for the engineer being unlocked, and its `Engineers` array names the
                # engineer being unlocked on the 18 ship rows and the *referring* engineer on all
                # six Odyssey ones. Filing on the array put every on-foot tribute one link down
                # the chain — Kit Fowler asking for what Yarden Bond wants — and it read as
                # correct because both are real engineers with real tributes.
                continue

            # The top grade an engineer reaches on a module type is the number that decides
            # whether they are worth the trip. A list of every blueprint they offer is a
            # different document and not one anybody can hear read out.
            #
            # **On foot that reasoning inverts**, so the speciality is the modification's name.
            # There is no grade to reach — a modification is present or absent — so "Suit:0,
            # Weapon:0" was the whole of what the thirteen Odyssey engineers said about
            # themselves, which is nothing. The names are what a Commander asks for by, and they
            # are what the four Colonia engineers below carry too, so all thirteen read alike.
            speciality = blueprint["Name"] if kind in ON_FOOT else kind
            best[key][speciality] = max(best[key].get(speciality, 0), grade)

    for unlocked in TRIBUTE_NOTES:
        if unlocked not in tribute:
            # A note attached to nobody is a warning that will never be read, which is worse
            # than no warning at all.
            raise SystemExit(f"a tribute note names {unlocked!r}, who has no unlock row")

    print(f"Unlock tributes carrying a staleness note: {len(TRIBUTE_NOTES)}")
    for unlocked in sorted(TRIBUTE_NOTES):
        print(f"  {unlocked}")

    return (
        {key: sorted(kinds.items(), key=lambda pair: (-pair[1], pair[0])) for key, kinds in best.items()},
        tribute,
        spelled,
    )


# `new EngineeringInfo(name, system, base, x, y, z, planet, discovery, meeting, unlock, rep,
#                      permit, odyssey)`. Positional, so every field is read by position and the
# count of them is asserted rather than assumed.
_STRING = r'"((?:[^"\\]|\\.)*)"'
_NUMBER = r"(-?[\d.]+)"

ENGINEERING_INFO = re.compile(
    r"new\s+EngineeringInfo\(\s*"
    + _STRING + r"\s*,\s*" + _STRING + r"\s*,\s*" + _STRING + r"\s*,\s*"
    + _NUMBER + r"\s*,\s*" + _NUMBER + r"\s*,\s*" + _NUMBER + r"\s*,\s*"
    + _STRING + r"\s*,\s*"
    + _STRING + r"\s*,\s*" + _STRING + r"\s*,\s*" + _STRING + r"\s*,\s*" + _STRING
    + r"\s*,\s*(?:true|false)\s*,\s*(true|false)\s*\)",
    re.DOTALL,
)

# "From Hera Tani (grade 3-4)." for a ship engineer, and a bare "From Domino Green" for an
# Odyssey one. The grade is optional because the on-foot chain does not state one — those unlock
# on a count of modifications rather than on a grade, and defaulting them to 3 would invent a
# requirement the game does not have.
REFERRAL = re.compile(r"From\s+(?P<who>[^(.]+?)\s*(?:\(grade\s*(?P<grade>\d)[^)]*\))?\s*\.?\s*$",
                      re.IGNORECASE)

# One engineer names three referrers, and any of them will do.
SPLIT_REFERRERS = re.compile(r",\s*|\s+and\s+", re.IGNORECASE)

# EDDiscovery says Bill Turner is "Common knowledge". The Fandom engineer table says Selene Jean,
# and a journal trace in docs/spikes/journal-corpus-engineering.md §4 decides it in the wiki's
# favour. The one place in this file where a source is overruled, so it is stated rather than
# quietly patched: the sources agree on the other 37 of 38.
BILL_TURNER = ("Bill Turner", ["Selene Jean"], 3)


def resolve(who: str, graph: dict[str, dict]) -> str:
    """A referrer's name as the directory spells it.

    EDDiscovery writes "Tod McQuinn" in a referral and "Tod 'The Blaster' McQuinn" as the entry,
    so a referral left as written would name somebody the directory cannot look up.
    """
    entry = graph.get(relax(who))
    return entry["name"] if entry else who


def chain() -> dict[str, dict]:
    """The referral graph and unlock prose, parsed out of EDDiscovery's C#.

    Keyed by the relaxed name so it joins the id list, which spells one engineer with a nickname.
    """
    text = fetch(EDDISCOVERY).decode("utf-8-sig")
    found = ENGINEERING_INFO.findall(text)

    if len(found) != EXPECTED_ENGINEERS:
        raise SystemExit(
            f"parsed {len(found)} EngineeringInfo initialisers out of Engineers.cs, expected "
            f"{EXPECTED_ENGINEERS} — the source has changed shape and the chain would be "
            "half-populated rather than absent"
        )

    graph = {}

    for name, _system, _base, _x, _y, _z, body, discovery, meeting, unlock, rep, _odyssey in found:
        referrers, grade = [], ""

        if (match := REFERRAL.search(discovery.strip())) is not None:
            referrers = [who.strip() for who in SPLIT_REFERRERS.split(match.group("who")) if who.strip()]
            grade = match.group("grade") or ""

        if relax(name) == relax(BILL_TURNER[0]):
            _who, referrers, level = BILL_TURNER
            grade = str(level)

        graph[relax(name)] = {
            "name": name,
            "referred_by": referrers,
            "referral_grade": grade,
            "body": body,
            "discovery": discovery,
            "meeting": meeting,
            "unlock": unlock,
            "reputation": rep,
        }

    return graph


def place(id64: str, market_id: str) -> tuple[str, str]:
    """The system and base an engineer works out of, from the two ids the id list carries."""
    if not id64:
        return "", ""

    record = json.loads(fetch(SPANSH_SYSTEM.format(id64=id64)))["record"]

    system = record.get("name") or ""
    station = ""

    if market_id:
        for candidate in record.get("stations") or []:
            if str(candidate.get("market_id")) == str(market_id):
                station = candidate.get("name") or ""
                break

    return system, station


def main() -> None:
    rows = list(csv.DictReader(io.StringIO(fetch(FDEV_IDS).decode("utf-8-sig"))))
    offered, tribute, spelled = blueprints()
    graph = chain()

    built, placeless, silent, chainless = [], [], [], []
    joined = set()

    for row in rows:
        name = (row.get("name") or "").strip()

        if not name:
            continue

        system, station = place((row.get("system_address") or "").strip(),
                                (row.get("market_id") or "").strip())

        if not system:
            placeless.append(name)

        key = relax(name)
        joined.add(key)
        kinds = offered.get(key)
        links = graph.get(key)

        if links is None:
            # In the id list and not in EDDiscovery's. Kept, because where they are is still an
            # answer, but reported: it means one source has an engineer the other has not.
            chainless.append(name)
            links = {}

        if not kinds:
            # An engineer nobody has a blueprint for. Kept, because where they are is still
            # an answer, and dropping them would make d47 deny that a real person exists.
            silent.append(name)

        built.append([
            (row.get("id") or "").strip(),
            name,
            system,
            station,

            # The tribute their invitation task asks for. Empty for the ones whose unlock is
            # a rank, a mission or a permit rather than a delivery — which is most of the
            # chain, and exactly the part that has no source.
            tribute.get(key, ""),

            # "Frame Shift Drive:5" — the type and the top grade, comma separated. On foot the
            # grade is 0, because a modification is ungraded: present or absent, never rolled.
            # The four Colonia engineers have no EDEngineer rows at all, so theirs are the named
            # modifications instead, marked so their provenance travels with them.
            ",".join(f"{name}:0" for name in COLONIA[key]) if key in COLONIA
            else ",".join(f"{kind}:{grade}" for kind, grade in (kinds or [])),

            # Who recommends them, and at what grade with that referrer. More than one name means
            # any of them will do. An empty grade beside a present referrer is the on-foot chain,
            # which unlocks on a count of modifications rather than on a grade — inventing a 3
            # there would state a requirement the game does not have.
            ",".join(resolve(who, graph) for who in links.get("referred_by", [])),
            links.get("referral_grade", ""),

            links.get("body", ""),
            links.get("discovery", ""),
            links.get("meeting", ""),
            links.get("unlock", ""),
            links.get("reputation", ""),
        ])

    unknown = sorted(name for name in spelled if relax(name) not in joined)

    lines = [
        "# Generated by tools/gen-engineers.py. Do not edit by hand — rerun the tool.",
        "# Identity from EDCD/FDevIDs engineers.csv; system and base names resolved from those",
        "# ids through spansh.co.uk at generation time; specialities from msarilar/EDEngineer",
        "# blueprints.json (MIT). The referral chain, body, unlock prose and reputation route come",
        f"# from EDDiscovery/EliteDangerousCore Engineers.cs at {EDDISCOVERY_COMMIT[:12]} — C# source, not",
        "# data, so the run asserts all 38 rows parse rather than half-populating the table. Bill",
        "# Turner's referrer is an explicit override; see the script. An Unlock row is filed under",
        "# the engineer it NAMES, not the ones in its Engineers array — those are the referrer on",
        "# all six Odyssey rows, and filing on them put every on-foot tribute one link down the",
        "# chain. Odyssey tributes are EDEngineer's, are cumulative along the chain, and are stale",
        "# since Frontier update 4.0.18.08 — four carry a note saying so. The four Colonia",
        "# engineers have no EDEngineer rows at all and their modification lists were read from the",
        "# wiki; see tools/gen-engineers.py. Game data is Frontier's, used",
        "# under their media usage rules — see NOTICE.",
        f"# Engineers: {len(built)}.",
        "\t".join(COLUMNS),
    ]

    lines += ["\t".join(row) for row in sorted(built, key=lambda row: row[1])]

    OUTPUT.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")

    print(f"Wrote {len(built)} engineers to {OUTPUT}")

    if placeless:
        print(f"No system resolved for: {', '.join(placeless)}")

    if silent:
        print(f"No blueprints found for: {', '.join(silent)}")

    referred = sum(1 for row in built if row[6])
    print(f"Referral chain: {referred} of {len(built)} engineers are reached through somebody else")

    if chainless:
        print(f"In engineers.csv but not in EDDiscovery's Engineers.cs: {', '.join(chainless)}")

    if unknown:
        # A name in the blueprint data that the id list has never heard of is a mismatch
        # worth seeing: either a new engineer, or a spelling drift between two sources.
        print(f"In blueprints but not in engineers.csv: {', '.join(unknown)}")


if __name__ == "__main__":
    main()
