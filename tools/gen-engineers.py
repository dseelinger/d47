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

What is deliberately missing
----------------------------
**The unlock chain.** list.md asks for "who in the chain of unlocks", and there is no
permissive machine-readable source for it: FDevIDs has no unlock column, coriolis-data
carries blueprints and not requirements, and EDEngineer's data files are blueprints,
entries, equipment, localisation and release notes — none of them the referral graph. It is
wiki knowledge, and writing it out from memory is exactly the invented game data every other
table in this repo exists to avoid. A wrong unlock requirement costs a Commander a trip.

What d47 says instead is what the journal actually knows: which engineers are unlocked and
at what rank, which have invited you, and which you have not met. "Invited but not unlocked"
*is* the chain, observed rather than asserted.
"""

import csv
import io
import json
import urllib.request
from collections import defaultdict
from pathlib import Path

FDEV_IDS = "https://raw.githubusercontent.com/EDCD/FDevIDs/master/engineers.csv"

BLUEPRINTS = ("https://raw.githubusercontent.com/msarilar/EDEngineer/master/"
              "EDEngineer/Resources/Data/blueprints.json")

SPANSH_SYSTEM = "https://spansh.co.uk/api/system/{id64}"

OUTPUT = Path(__file__).resolve().parent.parent / "src" / "D47.Core" / "Knowledge" / "Engineers.tsv"

COLUMNS = ["id", "name", "system", "station", "unlock", "specialities"]

# Not engineers. EDEngineer uses these to model the Odyssey vendors and synthesis, which sit
# in the same blueprint list and would otherwise appear in the directory as people.
NOT_PEOPLE = "@"

# The blueprint list models an engineer's invitation task as a blueprint of its own, whose
# "ingredients" are the tribute they want. It is not a modification and does not belong in a
# speciality list — but it is the one piece of "who unlocks what" that has a source.
UNLOCK = "Unlock"


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

        for engineer in blueprint.get("Engineers") or []:
            if engineer.startswith(NOT_PEOPLE):
                continue

            spelled.add(engineer)
            key = relax(engineer)

            if kind == UNLOCK:
                tribute[key] = ", ".join(
                    f"{(item.get('Name') or '').strip()} ×{item.get('Size') or 0}"
                    for item in blueprint.get("Ingredients") or []
                    if item.get("Name")
                )
                continue

            # The top grade an engineer reaches on a module type is the number that decides
            # whether they are worth the trip. A list of every blueprint they offer is a
            # different document and not one anybody can hear read out.
            best[key][kind] = max(best[key].get(kind, 0), grade)

    return (
        {key: sorted(kinds.items(), key=lambda pair: (-pair[1], pair[0])) for key, kinds in best.items()},
        tribute,
        spelled,
    )


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

    built, placeless, silent = [], [], []
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

            # "Frame Shift Drive:5" — the type and the top grade, comma separated.
            ",".join(f"{kind}:{grade}" for kind, grade in (kinds or [])),
        ])

    unknown = sorted(name for name in spelled if relax(name) not in joined)

    lines = [
        "# Generated by tools/gen-engineers.py. Do not edit by hand — rerun the tool.",
        "# Identity from EDCD/FDevIDs engineers.csv; system and base names resolved from those",
        "# ids through spansh.co.uk at generation time; specialities from msarilar/EDEngineer",
        "# blueprints.json (MIT). The unlock chain is deliberately absent — see the script.",
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

    if unknown:
        # A name in the blueprint data that the id list has never heard of is a mismatch
        # worth seeing: either a new engineer, or a spelling drift between two sources.
        print(f"In blueprints but not in engineers.csv: {', '.join(unknown)}")


if __name__ == "__main__":
    main()
