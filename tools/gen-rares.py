#!/usr/bin/env python3
"""Regenerate d47's rare goods table.

Not part of the build. Its output is committed as
`src/D47.Core/Knowledge/Rares.tsv` and shipped as an embedded resource; the app never
runs this and never reaches the network for it. Run it when Frontier adds a rare good:

    python tools/gen-rares.py

What it answers
----------------
`Materials.tsv` carries all 142 rare goods under the `rare-cargo` ledger, but only 6 of
its 142 `origins` cells name a market. Nothing in the tree can answer "where is Lavian
Brandy sold" from data. This table is that answer: one station per rare.

Two sources, one for identity and one for the place
-----------------------------------------------------
`EDCD/FDevIDs` `rare_commodity.csv` — the same file `gen-materials.py` reads for the
rare-cargo ledger — carries the market id: Frontier's own identifier for the station
that sells each rare. It names no system or station.

EDSM resolves a market id to where it is:
`https://www.edsm.net/api-system-v1/stations/market?marketId=<id>` answers with the
system name and the station name. Called once per rare at generation time, so a
Commander asking where a rare is sold costs no network at runtime — the app never
fetches, so this table adds no egress and needs no disclosure entry.

No allocation column. The per-visit quantity is set live by the station's economic
state — a boom can raise Soontil Relics to hundreds per trip — and is read from the
market index at answer time, not a static figure that would be a confident wrong
answer.

Symbols are lower-cased to match `Materials.tsv`'s key, the same transform
`gen-materials.py` already applies to this ledger. The run fails, rather than writing a
partial table, when a symbol has no `rare-cargo` row in `Materials.tsv`, or when EDSM
resolves a market id to no station.
"""

import csv
import datetime
import io
import json
import time
import urllib.error
import urllib.request
from pathlib import Path

FDEV_IDS = "https://raw.githubusercontent.com/EDCD/FDevIDs/master/rare_commodity.csv"

EDSM_MARKET = "https://www.edsm.net/api-system-v1/stations/market?marketId={market_id}"

REPO_ROOT = Path(__file__).resolve().parent.parent
MATERIALS = REPO_ROOT / "src" / "D47.Core" / "Knowledge" / "Materials.tsv"
OUTPUT = REPO_ROOT / "src" / "D47.Core" / "Knowledge" / "Rares.tsv"

COLUMNS = ["symbol", "name", "system", "station", "market_id"]

# Paced rather than fired all at once — 142 sequential lookups against a service this
# script does not own.
PAUSE = 1.0


def fetch(url: str, timeout: int = 30) -> bytes:
    """A GET, retried with backoff on a 429 — EDSM throttles a run of 142 of these."""
    request = urllib.request.Request(url, headers={"User-Agent": "d47-gen-rares"})

    for attempt in range(6):
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            if error.code != 429 or attempt == 5:
                raise

            wait = float(error.headers.get("Retry-After", 0)) or 3 * (attempt + 1)
            time.sleep(wait)

    raise AssertionError("unreachable")


def rare_cargo_symbols() -> set[str]:
    """Every symbol `Materials.tsv` already carries under the rare-cargo ledger.

    The cross-check this script exists to make: a rare FDevIDs names and Materials.tsv
    does not is two generators' sources having drifted apart.
    """
    symbols = set()

    with MATERIALS.open(encoding="utf-8") as handle:
        for line in handle:
            cells = line.rstrip("\n").split("\t")

            if len(cells) > 2 and cells[2] == "rare-cargo":
                symbols.add(cells[0])

    return symbols


def resolve(market_id: str) -> tuple[str, str]:
    """The system and station a market id resolves to, through EDSM."""
    record = json.loads(fetch(EDSM_MARKET.format(market_id=market_id)))
    return (record.get("name") or "", record.get("sName") or "")


def main() -> None:
    rows = list(csv.DictReader(io.StringIO(fetch(FDEV_IDS).decode("utf-8-sig"))))
    known = rare_cargo_symbols()

    built: list[list[str]] = []
    unkeyed: list[str] = []
    unplaced: list[str] = []

    for row in rows:
        symbol = (row.get("symbol") or "").strip().lower()
        name = (row.get("name") or "").strip()
        market_id = (row.get("market_id") or "").strip()

        if not symbol or not name or not market_id:
            raise SystemExit(f"rare_commodity.csv row is missing a field: {row}")

        if symbol not in known:
            unkeyed.append(symbol)
            continue

        system, station = resolve(market_id)

        if not system or not station:
            unplaced.append(f"{name} ({market_id})")
        else:
            built.append([symbol, name, system, station, market_id])

        time.sleep(PAUSE)

    if unkeyed:
        raise SystemExit(
            "Materials.tsv has no rare-cargo row for: " + ", ".join(sorted(unkeyed))
            + " — rerun gen-materials.py first, or FDevIDs and Materials.tsv have "
            "drifted apart"
        )

    if unplaced:
        raise SystemExit(
            "EDSM resolved no station for: " + ", ".join(unplaced)
            + " — writing a partial table would answer some rares and silently drop "
            "the rest"
        )

    built.sort(key=lambda built_row: built_row[0])

    stamp = datetime.date.today().isoformat()

    text = [
        "# Generated by tools/gen-rares.py. Do not edit by hand — rerun the tool.",
        "# Identity and market id from EDCD/FDevIDs rare_commodity.csv, the same file",
        "# gen-materials.py reads for the rare-cargo ledger. Each market id is resolved",
        "# to its system and station through EDSM's stations/market API at generation",
        "# time, so answering where a rare is sold costs no network at runtime. No",
        "# allocation column: the per-visit quantity is set live by the station's",
        "# economic state and is read from the market index at answer time. Game data",
        "# is Frontier's, used under their media usage rules — see NOTICE.",
        f"# Rows: {len(built)}. Built: {stamp}.",
        "\t".join(COLUMNS),
    ]

    text += ["\t".join(built_row) for built_row in built]

    OUTPUT.write_text("\n".join(text) + "\n", encoding="utf-8", newline="\n")

    print(f"Wrote {len(built)} rows to {OUTPUT}")


if __name__ == "__main__":
    main()
