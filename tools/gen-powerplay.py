#!/usr/bin/env python3
"""Regenerate d47's table of Powerplay rank rewards.

Not part of the build. Its output is committed as `src/D47.Core/Knowledge/PowerplayRanks.tsv`
and shipped as an embedded resource; the app never runs this and never reaches the network
for it. Run it when Frontier changes what a rank grants:

    python tools/gen-powerplay.py

Source: CMDR Eralm_237's Powerplay sheet, one tab per Power, read as CSV. Issue #597.

One row per reward: power, rank, merits, kind, subject, value. `kind` is decal, care_package,
rebuy_own_territory, rebuy_rival, perk or module. A perk's value is the signed cumulative
percent at the rank it changes. A module's subject is its entitlement symbol.
"""

from __future__ import annotations

import csv
import io
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

from curated_powerplay import MODULES, PERKS, POWERS

SHEET = "https://docs.google.com/spreadsheets/d/1yHDRfv5oV38mQwgzEsVa3hYKZH5smDIjoRUgECLIyMw/export"

OUTPUT = Path(__file__).resolve().parent.parent / "src" / "D47.Core" / "Knowledge" / "PowerplayRanks.tsv"

MODULE_RANKS = [34, 39, 44, 50, 57, 63, 70, 76, 83, 88, 91, 97]
SYMBOLS = sorted(set(MODULES.values()))
PAUSE = 0.3

Row = tuple[int, int, str, str, str]


def key(text: str) -> str:
    return " ".join(text.lower().replace("-", " ").split())


def fetch(gid: int) -> list[list[str]]:
    request = urllib.request.Request(f"{SHEET}?format=csv&gid={gid}", headers={"User-Agent": "d47-gen-powerplay"})

    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                return list(csv.reader(io.StringIO(response.read().decode("utf-8"))))
        except (urllib.error.URLError, TimeoutError):
            if attempt == 2:
                raise
            time.sleep(2 * (attempt + 1))

    raise AssertionError("unreachable")


def number(text: str, where: str) -> int:
    cleaned = text.replace(",", "").strip()

    if not re.fullmatch(r"\d+", cleaned):
        sys.exit(f"{where}: expected a number, found {text!r}")

    return int(cleaned)


def percent(text: str, where: str) -> int:
    cleaned = text.strip().strip('"')

    if not re.fullmatch(r"[+-]?\d+%", cleaned):
        sys.exit(f"{where}: expected a percentage, found {text!r}")

    return int(cleaned.rstrip("%"))


def power_rows(power: str, rows: list[list[str]]) -> list[Row]:
    first = next((i for i, r in enumerate(rows) if r and r[0].strip() == "1"), None)
    start = next((i for i, r in enumerate(rows) if r and r[0].strip() == "Rank"), None)

    if first is None or start is None:
        sys.exit(f"{power}: no Rank header or no rank 1 row")

    width = max(len(r) for r in rows[start:])
    headers = [
        key(" ".join((rows[i][c] if c < len(rows[i]) else "") for i in range(start, first)))
        for c in range(width)
    ]

    def column(fragment: str) -> int:
        found = [c for c, h in enumerate(headers) if fragment in h]

        if len(found) != 1:
            sys.exit(f"{power}: expected one header containing {fragment!r}, found {len(found)}")

        return found[0]

    merits_col = column("merits needed")
    care_col = column("care package")
    rival_col = column("another power")
    own_col = column("your power's territory")
    module_col = column("module unlock")

    fixed = {0, 1, 3, merits_col, care_col, rival_col, own_col, module_col}
    perk_cols: dict[int, str] = {}

    for c in range(width):
        if c in fixed:
            continue

        if headers[c] not in PERKS:
            sys.exit(f"{power}: column {c} header {headers[c]!r} has no perk key in curated_powerplay.PERKS")

        perk_cols[c] = PERKS[headers[c]]

    out: list[Row] = []
    seen_full = False

    for line, row in enumerate(rows[first:], start=first + 1):
        if not row or not row[0].strip():
            continue

        label = row[0].strip()
        cells = row + [""] * (width - len(row))
        where = f"{power} row {line}"

        if label == "100+":
            if cells[care_col].strip() != "Full":
                sys.exit(f"{where}: rank 100+ care package is {cells[care_col]!r}, not Full")

            seen_full = True
            continue

        rank = number(label, where)
        merits = number(cells[merits_col], f"{where} merits")
        care = cells[care_col].strip()

        if care == "Rank Decal":
            out.append((rank, merits, "decal", "rank", ""))
        elif care == "mini":
            out.append((rank, merits, "care_package", "mini", ""))
        elif care:
            sys.exit(f"{where} {headers[care_col]!r}: unknown care package {care!r}")

        for col, kind in ((rival_col, "rebuy_rival"), (own_col, "rebuy_own_territory")):
            if cells[col].strip():
                out.append((rank, merits, kind, "", str(percent(cells[col], f"{where} {headers[col]!r}"))))

        for col, perk in perk_cols.items():
            if cells[col].strip():
                out.append((rank, merits, "perk", perk, str(percent(cells[col], f"{where} {headers[col]!r}"))))

        module = cells[module_col].strip()

        if module:
            symbol = MODULES.get(key(module))

            if symbol is None:
                sys.exit(f"{where} {headers[module_col]!r}: module {module!r} has no symbol in curated_powerplay.MODULES")

            out.append((rank, merits, "module", symbol, ""))

        if not any(r[0] == rank for r in out):
            out.append((rank, merits, "", "", ""))

    if not seen_full:
        sys.exit(f"{power}: no 100+ row")

    check(power, out)
    return [r for r in out if r[2]]


def check(power: str, rows: list[Row]) -> None:
    merits: dict[int, int] = {}

    for rank, m, *_ in rows:
        if merits.setdefault(rank, m) != m:
            sys.exit(f"{power}: rank {rank} carries two merit figures")

    missing = [r for r in range(1, 101) if r not in merits]

    if missing:
        sys.exit(f"{power}: ranks with no row: {missing}")

    previous = -1

    for rank in range(1, 101):
        if merits[rank] < previous:
            sys.exit(f"{power}: merits fall at rank {rank}")

        previous = merits[rank]

    modules = sorted((rank, subject) for rank, _, kind, subject, _ in rows if kind == "module")

    if [r for r, _ in modules] != MODULE_RANKS or sorted(s for _, s in modules) != SYMBOLS:
        sys.exit(f"{power}: module rows {modules} are not the 12 symbols at ranks {MODULE_RANKS}")


def main() -> None:
    lines = [
        "# Generated by tools/gen-powerplay.py. Do not edit by hand — rerun the tool.",
        "# Source: CMDR Eralm_237's Powerplay rank sheet, one tab per Power, read as CSV. Issue #597.",
        "# Rank rewards, one row per reward; a perk's value is the cumulative percent at that rank;",
        "# a module's subject is its entitlement symbol. Rank 100+ always gives a full care package",
        "# and is not written. See NOTICE.",
        "power\trank\tmerits\tkind\tsubject\tvalue",
    ]

    for power, gid in POWERS.items():
        print(f"  {power}", file=sys.stderr)

        for rank, merits, kind, subject, value in sorted(power_rows(power, fetch(gid)), key=lambda r: r[0]):
            lines.append(f"{power}\t{rank}\t{merits}\t{kind}\t{subject}\t{value}")

        time.sleep(PAUSE)

    OUTPUT.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"Wrote {len(lines) - 6} rows to {OUTPUT}", file=sys.stderr)


if __name__ == "__main__":
    main()
