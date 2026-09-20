#!/usr/bin/env python3
"""Regenerate d47's table of permit-locked star systems.

Not part of the build. Its output is committed as `src/D47.Core/Knowledge/PermitSystems.tsv`
and shipped as an embedded resource; the app never runs this and never reaches the network
for it. Run it when Frontier locks or unlocks a system:

    python tools/gen-permits.py

Why a table rather than a request
---------------------------------
The station index carries no permit field. Measured 2026-09-20 against a station search
anchored on Alioth, a permit-locked system: no `needs_permit` on any result, and `permit`,
`system_permit`, `requires_permit` and `permit_name` all answer `field_values` with a 500.
The flag lives on the *system* index instead, where it is both a result field and a working
filter.

So the permit rule cannot be applied server-side to the sweep that a trade plan actually
makes. It has to be applied locally, against the merged markets — which include the
Commander's own `MarketBook` entries that no request ever returned. A shipped table answers
for both without a second request, and answers identically with no network at all, which is
what keeps the headless replay harness able to exercise the rule.

What it asks for
----------------
`api/systems/search`, filtered to `needs_permit`, sorted by distance from Sol so paging is
stable. 500 rows a page is the measured cap — asking for 1,000 returns 25. Sol sits about
26,000 light years from the galactic centre, so a 100,000 light year radius reaches every
system in the galaxy from there.

Most of the table is not the famous systems. Permit-locked *regions* near the core hold
hundreds of procedurally-named systems each, and those are exactly the ones a Commander
would otherwise be routed into without recognising the name.
"""

from __future__ import annotations

import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

SEARCH = "https://spansh.co.uk/api/systems/search"

OUTPUT = Path(__file__).resolve().parent.parent / "src" / "D47.Core" / "Knowledge" / "PermitSystems.tsv"

# The measured cap. 1,000 returns 25 rows rather than an error, which is this index's usual
# way of refusing.
PAGE_SIZE = 500

# Sol to the far rim, with room to spare.
RADIUS = 100_000

PAUSE = 0.3

# A run that cannot see these has not found the permit systems, whatever else it returned.
EXPECTED = ["Achenar", "Alioth", "Shinrarta Dezhra", "Sol"]

# Far below the 2,702 measured on 2026-09-20, but high enough that a truncated run fails here
# rather than shipping a table with holes in it.
FLOOR = 1_000


def fetch(payload: dict[str, object]) -> dict[str, object]:
    data = json.dumps(payload).encode("utf-8")
    headers = {"User-Agent": "d47-gen-permits", "Content-Type": "application/json"}
    request = urllib.request.Request(SEARCH, data=data, headers=headers)

    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                return json.loads(response.read().decode("utf-8"))
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
            if attempt == 2:
                raise
            time.sleep(2 * (attempt + 1))

    raise AssertionError("unreachable")


def page(index: int) -> tuple[list[dict[str, object]], int]:
    """One page of permit systems, and what the index says the whole set counts."""
    body = fetch({
        "filters": {
            "distance": {"min": "0", "max": str(RADIUS)},
            "needs_permit": {"value": ["true"]},
        },
        "sort": [{"distance": {"direction": "asc"}}],
        "size": PAGE_SIZE,
        "page": index,
        "reference_system": "Sol",
    })

    results = body.get("results", [])
    assert isinstance(results, list)

    count = body.get("count", 0)
    assert isinstance(count, int)

    return results, count


def names() -> set[str]:
    kept: set[str] = set()
    index = 0
    total = None

    while True:
        results, count = page(index)

        if total is None:
            total = count
            print(f"The index reports {total} permit systems", file=sys.stderr)

        for system in results:
            # Read rather than assumed: the filter is trusted to narrow, not to be the only
            # thing that decides what lands in the table.
            if system.get("needs_permit") is not True:
                continue

            name = str(system.get("name", "")).strip()

            if not name:
                continue

            assert "\t" not in name and "\n" not in name, name
            kept.add(name)

        print(f"  page {index}: {len(results)} rows, {len(kept)} kept", file=sys.stderr)

        # A short page is the last page.
        if len(results) < PAGE_SIZE:
            break

        index += 1
        time.sleep(PAUSE)

    return kept


def main() -> int:
    kept = names()

    missing = [name for name in EXPECTED if name not in kept]
    assert not missing, f"permit systems the run never saw: {', '.join(missing)}"
    assert len(kept) >= FLOOR, f"only {len(kept)} permit systems; the run is truncated"

    rows = sorted(kept)

    header = [
        "# Generated by tools/gen-permits.py. Do not edit by hand — rerun the tool.",
        f"# Source: {SEARCH}, filtered on needs_permit within {RADIUS} ly of Sol.",
        "# System names only, which are Frontier's game data; see NOTICE.",
    ]

    OUTPUT.write_text("\n".join(header + rows) + "\n", encoding="utf-8", newline="\n")

    print(f"Wrote {len(rows)} rows to {OUTPUT}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
