#!/usr/bin/env python3
"""Regenerate d47's table of systems worth remarking on.

Not part of the build. Its output is committed as `src/D47.Core/Knowledge/Lore.tsv` and
shipped as an embedded resource; the app never runs this and never reaches the network for
it. Run it when a row is added, corrected or dropped:

    python tools/gen-lore.py

What it answers
---------------
Phase 23 asks d47 to say something on arriving in a system that means more than its
astrography. The journal cannot answer that: `FSDJump` reports where you are, `CodexEntry`
reports astronomy ("G Type Giant", "Terraformable") and `ApproachSettlement` names 229
settlements in the corpus without saying which of them matter.

Where the rows come from, and why this generator is not like the others
-----------------------------------------------------------------------
`gen-engineers.py` and `gen-blueprints.py` transcribe a closed set the game defines. **This
one does not, and that is the whole difficulty.** The set of engineers is decided by
Frontier; *which systems are worth remarking on* is a judgement somebody made, so lifting a
community point-of-interest list would copy their compilation even though every row in it is
a fact. EDSM's Galactic Mapping names, INARA's logbooks and Canonn's site catalogue are all
somebody's selection.

So **the selection below is the maintainer's**, drawn from what each source is the authority
on and written here rather than imported:

* Frontier's own GalNet and in-game fiction for the human history — the capitals, the
  Pilots Federation's home, Jaques Station's misjump.
* `elite-dangerous.fandom.com` and `canonn.science` for the discovery record: who found the
  first Guardian ruins and when, where the first Thargoid barnacle was, which moon carries
  John Jameson's Cobra.
* `spansh.co.uk` and `edsm.net` for identity only — a name in, an id64 out. Neither
  contributes a row.

The note on each row is a **fact about Elite Dangerous**, in the same category as which
system an engineer sits in, and it is Frontier's; see `NOTICE`, which carries the
attribution in their own words. Nothing here is a distance or a status that moves. A row
that would need re-checking every update does not belong in a shipped table — it belongs to
the web lookup that speaks after this one, whose results stay sentences and never become
rows.

Two resolvers, and they have to agree
-------------------------------------
Every row is keyed on **system address**, not on name: `Ceeckia ZQ-L c24-0` is now `Beagle
Point` and the address did not change. The address is resolved twice, through spansh and
through EDSM, and a disagreement or a miss is a hard failure — the same shape of assertion
`gen-engineers.py` puts on its 38 parsed rows, and for the same reason. A name typed with
one letter wrong resolves to nothing and would otherwise ship as a row that can never fire,
reported by nobody.

Both misses this run would have shipped: `Ceeckia ZQ-L c24-0` (renamed) and `Arumclaw` (a
system that does not exist — a search result said Commander Salomé was killed there, and the
row was dropped rather than guessed at).

Cross-checked against Frontier's own writing
--------------------------------------------
Eleven of the twenty rows also appear in the 913-journal corpus on the development machine,
where Frontier themselves wrote the name and the address into an `FSDJump`. Every one agreed
with both resolvers. That check is not run here — a corpus is not something another machine
has — but `--corpus <dir>` repeats it where one exists.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

SPANSH = "https://spansh.co.uk/api/systems/field_values/system_names?q={q}"
EDSM = "https://www.edsm.net/api-v1/system?systemName={q}&showId=1"

OUTPUT = Path(__file__).resolve().parent.parent / "src" / "D47.Core" / "Knowledge" / "Lore.tsv"

# The compilation. One clause per row, present tense, and nothing in it that an update can
# falsify — no distances that get re-surveyed, no station that can be destroyed, no war.
#
# Ordered by how far out it is rather than by subject, because that is the order a Commander
# meets them in and it makes the shape of the list arguable: eight in the bubble, six in the
# Pleiades and the Guardian space beyond it, six past the point where nobody arrives by
# accident.
SEED: list[tuple[str, str]] = [
    ("Sol", "Earth is here. Everywhere else on this list is somewhere humanity got to afterwards."),
    ("Alpha Centauri",
     "Hutton Orbital is out here, 0.22 light years from the arrival star — about ninety minutes "
     "at full throttle, and the longest supercruise anybody makes on purpose."),
    ("Shinrarta Dezhra",
     "Founders World, and Jameson Memorial above it. The Pilots Federation's home, and the permit "
     "arrives with an Elite rank."),
    ("Sirius", "Sirius Corporation was founded here, and still keeps its headquarters in the system."),
    ("Achenar", "The Empire is governed from here."),
    ("Alioth", "The Alliance is governed from here."),
    ("Lave",
     "One of the Old Worlds — Lave, Leesti, Diso and Zaonce were the first systems humanity "
     "settled beyond Sol, colonised in the 25th century."),
    ("Riedquat",
     "Settled from the Old Worlds in the wave after Lave's, and the one with the reputation. "
     "Commanders have been warning each other about Riedquat for a very long time."),
    ("HIP 12099",
     "John Jameson's Cobra Mark Three came down on the first planet's moon B and is still there — "
     "the wreck every Commander eventually visits."),
    ("Merope", "The first Thargoid barnacle anybody found was on Merope 5 C, in 3302."),
    ("Maia", "Darnielle's Progress, on Maia A 2 A, is where meta-alloys can be bought rather than harvested."),
    ("HIP 22460", "Thargoid surface sites, and the system Azimuth fired the Proteus Wave in."),
    ("Synuefe XR-H d11-102", "The first Guardian ruins ever found, on the first planet's moon B, in October 3302."),
    ("Syreadiae JX-F c0",
     "The megaship Zurara is derelict above the first planet — the far end of the Formidine Rift "
     "mystery, and its crew's logs are still aboard."),
    ("Colonia", "Jaques Station misjumped to here in 3302 and never left. The whole region grew around it."),
    ("Great Annihilator", "Two black holes, and the more massive of them is the heaviest stellar black hole known."),
    ("Eta Carinae",
     "One of the most massive stars known, and it is shedding itself into the nebula around it."),
    ("Sagittarius A*", "The supermassive black hole at the centre of the galaxy."),
    ("Stuemeae FG-Y d7561", "Explorer's Anchorage — the starport furthest from Sol, built where the expeditions stop."),
    ("Beagle Point",
     "Sixty-five thousand light years out, near the far rim. It was Ceeckia ZQ-L c24-0 until "
     "Frontier renamed it for the Commanders who got here first."),
]


def fetch(url: str) -> object:
    request = urllib.request.Request(url, headers={"User-Agent": "d47-gen-lore"})

    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                return json.loads(response.read().decode("utf-8"))
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
            if attempt == 2:
                raise
            time.sleep(2 * (attempt + 1))

    raise AssertionError("unreachable")


def by_spansh(name: str) -> int | None:
    """spansh's name field values. Prefix-matched, so the exact name still has to be picked out."""
    found = fetch(SPANSH.format(q=urllib.parse.quote(name)))
    assert isinstance(found, dict)

    for candidate in found.get("min_max", []):
        if candidate.get("name", "").casefold() == name.casefold():
            return int(candidate["id64"])

    return None


def by_edsm(name: str) -> int | None:
    """EDSM's system lookup. Answers `[]` rather than an object for a name it does not know."""
    found = fetch(EDSM.format(q=urllib.parse.quote(name)))

    if not isinstance(found, dict) or found.get("name", "").casefold() != name.casefold():
        return None

    return int(found["id64"])


def from_corpus(directory: Path) -> dict[str, int]:
    """Every system name Frontier themselves wrote an address beside, from local journals."""
    seen: dict[str, int] = {}

    for journal in sorted(directory.glob("Journal.*.log")):
        with journal.open(encoding="utf-8", errors="replace") as handle:
            for line in handle:
                if '"StarSystem"' not in line:
                    continue

                try:
                    event = json.loads(line)
                except json.JSONDecodeError:
                    continue

                name, address = event.get("StarSystem"), event.get("SystemAddress")

                if isinstance(name, str) and isinstance(address, int):
                    seen[name.casefold()] = address

    return seen


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--corpus",
        type=Path,
        help="A journal folder to cross-check the resolved addresses against. Optional.")
    arguments = parser.parse_args()

    corpus = from_corpus(arguments.corpus) if arguments.corpus else {}
    rows: list[tuple[int, str, str]] = []
    problems: list[str] = []
    confirmed = 0

    for name, note in SEED:
        spansh, edsm = by_spansh(name), by_edsm(name)

        if spansh is None or edsm is None:
            problems.append(
                f"{name}: unresolved (spansh={spansh}, edsm={edsm}). "
                "A name no resolver knows would ship as a row that can never fire.")
            continue

        if spansh != edsm:
            problems.append(f"{name}: spansh says {spansh}, EDSM says {edsm}. Resolve by hand before shipping.")
            continue

        if (theirs := corpus.get(name.casefold())) is not None:
            confirmed += 1

            if theirs != spansh:
                problems.append(f"{name}: the journal says {theirs}, both resolvers say {spansh}. Frontier wins.")
                continue

        rows.append((spansh, name, " ".join(note.split())))
        print(f"  {name} → {spansh}", file=sys.stderr)
        time.sleep(0.3)

    if problems:
        print("\nNot written:", file=sys.stderr)

        for problem in problems:
            print(f"  {problem}", file=sys.stderr)

        return 1

    rows.sort(key=lambda row: row[1])

    header = [
        "# Generated by tools/gen-lore.py. Do not edit by hand — rerun the tool.",
        "# The selection is the maintainer's own compilation, not an imported point-of-interest",
        "# list. Each note is a fact about Elite Dangerous and is Frontier's; see NOTICE. Nothing",
        "# here is a distance that gets re-surveyed or a status that can change — anything that",
        "# moves belongs to the web lookup, whose results stay sentences and never become rows.",
        "# Addresses resolved through spansh.co.uk and edsm.net at generation time, which have to",
        f"# agree{f'; {confirmed} of {len(rows)} also matched a local journal' if corpus else ''}.",
        "systemAddress\tname\tnote",
    ]

    OUTPUT.write_text(
        "\n".join(header + [f"{address}\t{name}\t{note}" for address, name, note in rows]) + "\n",
        encoding="utf-8",
        newline="\n")

    print(f"\nWrote {len(rows)} rows to {OUTPUT}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
