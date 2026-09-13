#!/usr/bin/env python3
"""Regenerate d47's exobiology species table.

Not part of the build. Its output is committed as
`src/D47.Core/Knowledge/Exobiology.tsv` and shipped as an embedded resource; the app never
runs this and never reaches the network for it. Run it when Frontier adds a species or the
sampled ranges need refreshing:

    python tools/gen-exobiology.py

What it answers
----------------
`ExobiologyCatalogue.Possible` predicts which species a body could carry before it is
sampled, from the same four conditions the journal's `Scan` event already carries: planet
class, atmosphere, volcanism, gravity, temperature and pressure. Nothing in d47 or the
journal states which combinations of those six a given species accepts, so this generator
learns it from `spansh.co.uk`'s surveyed-body index: species and value from `landmarks`,
conditions from the body each landmark sits on.

One source, and what it is trusted for
---------------------------------------
`spansh.co.uk`'s `api/bodies/field_values/landmark_subtype` lists every distinct landmark
name it has indexed — biological species alongside points of interest (crashed ships,
Guardian ruins, human settlements) that share the same `landmarks` array but carry no sale
value. Filtering to landmarks whose `value` is above zero is what separates the two; every
name it tried and rejected is not a species this generator invented; it is what the survey
data says is not sellable.

`api/bodies/search`, filtered to one species by `landmark_subtype`, returns up to 250
surveyed bodies a page; two pages (500 bodies) is the sample each species' ranges are
learned from.

One normalisation, on both sides of the table
-----------------------------------------------
Spansh's body fields and the journal's `Scan` fields name the same conditions in different
words — capitalised where the journal is not, "High metal content world" where the journal
writes "High metal content body", "Sulphur dioxide" where the journal writes "sulfur
dioxide atmosphere". `normalise_planet_type`, `normalise_atmosphere` and
`normalise_volcanism` below are the one fold applied to both: this generator applies them
to what Spansh reports before a row is written, and `ExobiologyCatalogue.Possible` applies
the same fold to what the journal reports before it looks a row up. Neither side needs to
agree with the other's spelling, only with the same function.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

SEARCH = "https://spansh.co.uk/api/bodies/search"
FIELD_VALUES = "https://spansh.co.uk/api/bodies/field_values/landmark_subtype?q="

OUTPUT = Path(__file__).resolve().parent.parent / "src" / "D47.Core" / "Knowledge" / "Exobiology.tsv"

PAGE_SIZE = 250

# A column stays in a row's categorical list once it is seen on at least this share of the
# bodies sampled for that species — frequent enough to be a real constraint rather than one
# outlying survey.
CATEGORICAL_THRESHOLD = 0.02

# The band each species' gravity, temperature and pressure ranges are learned from.
PERCENTILE_LOW = 1
PERCENTILE_HIGH = 99

PAUSE = 0.3


def fetch(url: str, payload: dict[str, object] | None = None) -> dict[str, object]:
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    headers = {"User-Agent": "d47-gen-exobiology"}

    if data is not None:
        headers["Content-Type"] = "application/json"

    request = urllib.request.Request(url, data=data, headers=headers)

    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                return json.loads(response.read().decode("utf-8"))
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
            if attempt == 2:
                raise
            time.sleep(2 * (attempt + 1))

    raise AssertionError("unreachable")


def candidate_names() -> list[str]:
    """Every landmark subtype spansh has indexed — species and non-biological alike."""
    found = fetch(FIELD_VALUES)
    return sorted(found.get("values", []))


def search(name: str, page: int) -> list[dict[str, object]]:
    body = fetch(SEARCH, {
        "filters": {"landmark_subtype": {"value": [name]}},
        "sort": [],
        "size": PAGE_SIZE,
        "page": page,
    })
    results = body.get("results", [])
    assert isinstance(results, list)
    return results


def sample(name: str) -> tuple[str, int, list[dict[str, object]]] | None:
    """The genus, the value, and up to 500 surveyed bodies — or None where value is zero."""
    first = search(name, 0)

    if not first:
        return None

    landmark = next(
        (row for body in first for row in body.get("landmarks", []) if row.get("subtype") == name),
        None)

    if landmark is None or not landmark.get("value"):
        # Not a species — a point of interest sharing the same landmarks array.
        return None

    bodies = list(first)

    if len(first) == PAGE_SIZE:
        time.sleep(PAUSE)
        bodies += search(name, 1)

    return str(landmark["type"]), int(landmark["value"]), bodies


# ------------------------------------------------------------------ normalisation


def normalise_planet_type(raw: str | None) -> str:
    words = ["body" if word == "world" else word for word in re.findall(r"[a-z]+", (raw or "").lower())]
    return "".join(words)


def normalise_atmosphere(raw: str | None) -> str:
    text = (raw or "").strip().lower()

    if text in ("", "no atmosphere"):
        return "none"

    text = text.replace("sulphur", "sulfur")
    stopwords = {"thin", "thick", "hot", "atmosphere"}

    return "".join(word for word in re.findall(r"[a-z]+", text) if word not in stopwords)


def normalise_volcanism(raw: str | None) -> str:
    text = (raw or "").strip().lower()

    if text in ("", "no volcanism"):
        return "none"

    return "".join(word for word in re.findall(r"[a-z]+", text) if word != "volcanism")


# ------------------------------------------------------------------ aggregation


def percentile(values: list[float], pct: float) -> float:
    ordered = sorted(values)

    if len(ordered) == 1:
        return ordered[0]

    at = (pct / 100) * (len(ordered) - 1)
    low, high = int(at), min(int(at) + 1, len(ordered) - 1)

    return ordered[low] + (ordered[high] - ordered[low]) * (at - low)


def categorical(values: list[str], threshold: float) -> list[str]:
    counts: dict[str, int] = {}

    for value in values:
        counts[value] = counts.get(value, 0) + 1

    kept = [value for value, count in counts.items() if count / len(values) >= threshold]
    return sorted(kept)


def row(name: str, genus: str, value: int, bodies: list[dict[str, object]]) -> list[str]:
    planet_types = categorical([normalise_planet_type(str(b.get("subtype", ""))) for b in bodies], CATEGORICAL_THRESHOLD)
    atmospheres = categorical([normalise_atmosphere(b.get("atmosphere")) for b in bodies], CATEGORICAL_THRESHOLD)
    volcanism = categorical([normalise_volcanism(b.get("volcanism_type")) for b in bodies], CATEGORICAL_THRESHOLD)
    regions = sorted({str(b["system_region"]) for b in bodies if b.get("system_region")})

    gravity = [float(b["gravity"]) for b in bodies if b.get("gravity") is not None]
    temperature = [float(b["surface_temperature"]) for b in bodies if b.get("surface_temperature") is not None]
    pressure = [float(b["surface_pressure"]) for b in bodies if b.get("surface_pressure") is not None]

    return [
        name,
        genus,
        str(value),
        str(len(bodies)),
        ";".join(planet_types),
        ";".join(atmospheres),
        ";".join(volcanism),
        f"{percentile(gravity, PERCENTILE_LOW):.4f}",
        f"{percentile(gravity, PERCENTILE_HIGH):.4f}",
        f"{percentile(temperature, PERCENTILE_LOW):.1f}",
        f"{percentile(temperature, PERCENTILE_HIGH):.1f}",
        f"{percentile(pressure, PERCENTILE_LOW):.6f}",
        f"{percentile(pressure, PERCENTILE_HIGH):.6f}",
        ";".join(regions),
    ]


def main() -> int:
    argparse.ArgumentParser(description=__doc__).parse_args()

    rows: list[list[str]] = []
    skipped = 0

    names = candidate_names()
    print(f"{len(names)} landmark subtypes to check", file=sys.stderr)

    for index, name in enumerate(names, start=1):
        found = sample(name)
        time.sleep(PAUSE)

        if found is None:
            skipped += 1
            continue

        genus, value, bodies = found
        rows.append(row(name, genus, value, bodies))
        print(f"  [{index}/{len(names)}] {name} — {len(bodies)} bodies", file=sys.stderr)

    rows.sort(key=lambda cells: cells[0])

    header = [
        "# Generated by tools/gen-exobiology.py. Do not edit by hand — rerun the tool.",
        f"# Source: spansh.co.uk api/bodies/search, {time.strftime('%Y-%m-%d', time.gmtime())}. Species and",
        "# values from surveyed landmarks whose value is above zero; up to 500 surveyed bodies sampled per",
        "# species for the condition ranges. Game data is Frontier's, used under their media usage rules;",
        "# see NOTICE. Categorical columns keep a value seen on at least 2% of the sampled bodies; numeric",
        "# ranges are the 1st-99th percentile. Atmosphere, planet type and volcanism are normalised — see",
        "# normalise_planet_type, normalise_atmosphere and normalise_volcanism in this file, mirrored in",
        "# ExobiologyCatalogue's private NormalisePlanetType, NormaliseAtmosphere and NormaliseVolcanism,",
        "# which apply the same fold to a live journal Scan.",
        "species\tgenus\tvalue\tbodies_sampled\tplanet_types\tatmospheres\tvolcanism\tgravity_low\t"
        "gravity_high\ttemperature_low\ttemperature_high\tpressure_low\tpressure_high\tregions",
    ]

    OUTPUT.write_text(
        "\n".join(header + ["\t".join(cells) for cells in rows]) + "\n",
        encoding="utf-8",
        newline="\n")

    print(f"\nWrote {len(rows)} species to {OUTPUT} ({skipped} landmark names had no sale value)",
          file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
