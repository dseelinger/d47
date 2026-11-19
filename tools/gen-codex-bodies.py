#!/usr/bin/env python3
"""Regenerate d47's table of star and planet codex entries known per galactic region.

Not part of the build. Its output is committed as `src/D47.Core/Knowledge/CodexBodies.tsv` and
shipped as an embedded resource beside `Codex.tsv`; the app never runs this and never reaches Spansh.

    python tools/gen-codex-bodies.py
    python tools/gen-codex-bodies.py --limit-bytes 400000000 --output <file>   # a trial on the dump's head

Source: Spansh's galaxy dump (downloads.spansh.co.uk/galaxy.json.gz), about 119 GB compressed. It
is streamed once and never saved. The dump is a JSON array with one system per line; each system has
`coords` and `bodies`. Each system is placed in a codex region by a port of `GalacticRegions.Find`
over the committed `Regions.tsv`, and each body is matched to a codex entry by its `subType`,
`luminosity`, `terraformingState` and `atmosphereType`. A region's count for an entry is the number
of systems in it with at least one matching body.

Every EntryID in ENTRIES was confirmed by a journal CodexEntry logged for a scanned body of that
type, and the Spansh fields by Spansh records of bodies of that type. A body that matches no entry
is left out and counted in the report printed at the end.
"""

import argparse
import http.client
import json
import queue
import sys
import threading
import time
import urllib.error
import urllib.request
import zlib
from bisect import bisect_right
from collections import Counter
from email.utils import parsedate_to_datetime
from pathlib import Path

DUMP_URL = "https://downloads.spansh.co.uk/galaxy.json.gz"
AGENT = "d47-gen"

ROOT = Path(__file__).resolve().parent.parent
KNOWLEDGE = ROOT / "src" / "D47.Core" / "Knowledge"
OUTPUT = KNOWLEDGE / "CodexBodies.tsv"
REGIONS = KNOWLEDGE / "Regions.tsv"
CODEX = KNOWLEDGE / "Codex.tsv"
FIXTURE = ROOT / "tests" / "fixtures" / "codex" / "region-positions.tsv"

CATEGORY = "Astronomical Bodies"
STARS = "Stars"
TERRESTRIALS = "Terrestrial planets"
GAS_GIANTS = "Gas giant planets"

# Luminosity classes, written as the class's Roman numeral; "0" is a hypergiant (Ia0, 0 or O).
DWARF = ("IV", "V", "VI")
GIANT = ("III",)
SUPERGIANT = ("I", "II")
HYPERGIANT = ("0",)
DEGENERATE = ("VII",)

# Terraforming and atmosphere: "yes", "no", or "" for either.
# (EntryID, name, subcategory, Spansh subType, luminosity classes, terraformable, atmosphere)
ENTRIES = [
    (1100101, "O Type Star", STARS, "O (Blue-White) Star", DWARF, "", ""),
    (1100201, "B Type Star", STARS, "B (Blue-White) Star", DWARF, "", ""),
    (1100202, "B Type Giant", STARS, "B (Blue-White) Star", GIANT, "", ""),
    (1100301, "A Type Star", STARS, "A (Blue-White) Star", DWARF, "", ""),
    (1100302, "A Type Giant", STARS, "A (Blue-White) Star", GIANT, "", ""),
    (1100303, "A Type Supergiant", STARS, "A (Blue-White super giant) Star", SUPERGIANT, "", ""),
    (1100401, "F Type Star", STARS, "F (White) Star", DWARF, "", ""),
    (1100402, "F Type Giant", STARS, "F (White) Star", GIANT, "", ""),
    (1100501, "G Type Star", STARS, "G (White-Yellow) Star", DWARF, "", ""),
    (1100502, "G Type Giant", STARS, "G (White-Yellow) Star", GIANT, "", ""),
    (1100601, "K Type Star", STARS, "K (Yellow-Orange) Star", DWARF, "", ""),
    (1100602, "K Type Giant", STARS, "K (Yellow-Orange giant) Star", GIANT, "", ""),
    (1100701, "M Type Star", STARS, "M (Red dwarf) Star", DWARF, "", ""),
    (1100801, "L Type Star", STARS, "L (Brown dwarf) Star", DWARF, "", ""),
    (1100901, "T Type Star", STARS, "T (Brown dwarf) Star", DWARF, "", ""),
    (1101001, "T Tauri Star", STARS, "T Tauri Star", DWARF, "", ""),
    (1101101, "AeBe Type Star", STARS, "Herbig Ae/Be Star", DWARF, "", ""),
    (1101201, "Y Type Star", STARS, "Y (Brown dwarf) Star", DWARF, "", ""),
    (1101404, "C Type Hypergiant", STARS, "C Star", HYPERGIANT, "", ""),
    (1102002, "S Type Giant", STARS, "S-type Star", GIANT, "", ""),
    (1102101, "W Type Star", STARS, "Wolf-Rayet Star", SUPERGIANT, "", ""),
    (1102201, "D Type Star", STARS, "White Dwarf (D) Star", DEGENERATE, "", ""),
    (1102202, "DA Type Star", STARS, "White Dwarf (DA) Star", DEGENERATE, "", ""),
    (1102203, "DAB Type Star", STARS, "White Dwarf (DAB) Star", DEGENERATE, "", ""),
    (1102206, "DAZ Type Star", STARS, "White Dwarf (DAZ) Star", DEGENERATE, "", ""),
    (1102212, "DQ Type Star", STARS, "White Dwarf (DQ) Star", DEGENERATE, "", ""),
    (1102213, "DC Type Star", STARS, "White Dwarf (DC) Star", DEGENERATE, "", ""),
    (1102300, "Neutron Star", STARS, "Neutron Star", DEGENERATE, "", ""),
    (1102400, "Black Hole", STARS, "Black Hole", DEGENERATE, "", ""),
    (1200101, "Water Giant", GAS_GIANTS, "Water giant", (), "", ""),
    (1200301, "Gas Giant with Water-Based Life", GAS_GIANTS, "Gas giant with water-based life", (), "", ""),
    (1200401, "Gas Giant with Ammonia-Based Life", GAS_GIANTS, "Gas giant with ammonia-based life", (), "", ""),
    (1200501, "Class I Gas Giant", GAS_GIANTS, "Class I gas giant", (), "", ""),
    (1200601, "Class II Gas Giant", GAS_GIANTS, "Class II gas giant", (), "", ""),
    (1200701, "Class III Gas Giant", GAS_GIANTS, "Class III gas giant", (), "", ""),
    (1200801, "Class IV Gas Giant", GAS_GIANTS, "Class IV gas giant", (), "", ""),
    (1200901, "Class V Gas Giant", GAS_GIANTS, "Class V gas giant", (), "", ""),
    (1201001, "Helium-Rich Gas Giant", GAS_GIANTS, "Helium-rich gas giant", (), "", ""),
    (1300100, "Earth-Like Planet", TERRESTRIALS, "Earth-like world", (), "", ""),
    (1300202, "Ammonia World", TERRESTRIALS, "Ammonia world", (), "no", ""),
    (1300301, "Water World", TERRESTRIALS, "Water world", (), "no", ""),
    (1300302, "Water World, Terraformable", TERRESTRIALS, "Water world", (), "yes", ""),
    (1300401, "Metal-Rich Body", TERRESTRIALS, "Metal-rich body", (), "no", "no"),
    (1300501, "High Metal Content Body", TERRESTRIALS, "High metal content world", (), "no", "no"),
    (1300502, "High Metal Content Body, Terraformable", TERRESTRIALS, "High metal content world", (), "yes", "no"),
    (1300601, "Rocky Body", TERRESTRIALS, "Rocky body", (), "no", "no"),
    (1300602, "Rocky Body, Terraformable", TERRESTRIALS, "Rocky body", (), "yes", "no"),
    (1300701, "Rocky Ice Body", TERRESTRIALS, "Rocky Ice world", (), "no", "no"),
    (1300801, "Icy Body", TERRESTRIALS, "Icy body", (), "no", "no"),
    (1301401, "Metal-Rich Body with Atmosphere", TERRESTRIALS, "Metal-rich body", (), "no", "yes"),
    (1301501, "High Metal Content Body with Atmosphere", TERRESTRIALS, "High metal content world", (), "no", "yes"),
    (1301502, "High Metal Content Body with Atmosphere, Terraformable", TERRESTRIALS, "High metal content world", (), "yes", "yes"),
    (1301601, "Rocky Body with Atmosphere", TERRESTRIALS, "Rocky body", (), "no", "yes"),
    (1301602, "Rocky Body with Atmosphere, Terraformable", TERRESTRIALS, "Rocky body", (), "yes", "yes"),
    (1301701, "Rocky Ice Body with Atmosphere", TERRESTRIALS, "Rocky Ice world", (), "no", "yes"),
    (1301801, "Icy Body with Atmosphere", TERRESTRIALS, "Icy body", (), "no", "yes"),
]

# Earth-like worlds are logged as one entry whether or not they were terraformed.
EARTH_LIKE_STATES = {None, "Not terraformable", "Terraformed"}

X0 = -49985
Z0 = -24105
SCALE = 83
GRIDS_PER_LY = 4096

BATCH_BYTES = 32 << 20
READ_BYTES = 4 << 20
QUEUED_CHUNKS = 64


class RegionMap:
    """A port of GalacticRegions.Find over Regions.tsv."""

    def __init__(self, path: Path):
        self.names: dict[int, str] = {}
        rows: dict[int, tuple[list[int], list[int]]] = {}

        for line in path.read_text(encoding="utf-8-sig").splitlines():
            if not line or line.startswith("#"):
                continue

            cells = line.split("\t")

            if len(cells) < 3:
                continue

            if cells[0] == "N":
                self.names[int(cells[1])] = cells[2]
            elif cells[0] == "M":
                ends, ids, x = [], [], 0

                for run in cells[2].split(","):
                    span, region = run.split(":")
                    x += int(span)
                    ends.append(x)
                    ids.append(int(region))

                rows[int(cells[1])] = (ends, ids)

        self.rows = [rows.get(z, ([], [])) for z in range(max(rows) + 1)] if rows else []

    def find(self, x: float, z: float) -> int | None:
        """The region id holding the position, or None; truncates toward zero as C#'s (int) cast does."""
        px = int((x - X0) * SCALE / GRIDS_PER_LY)
        pz = int((z - Z0) * SCALE / GRIDS_PER_LY)

        if px < 0 or pz < 0 or pz >= len(self.rows):
            return None

        ends, ids = self.rows[pz]
        i = bisect_right(ends, px)

        if i >= len(ends) or ids[i] == 0:
            return None

        return ids[i]


def luminosity_class(value: str | None) -> str | None:
    """The Roman numeral of a luminosity such as "Vab", "IIIa" or "Ia0"; "0" for a hypergiant."""
    if value is None:
        return None

    if value in ("0", "O") or value.startswith("Ia0"):
        return "0"

    numeral = ""

    for character in value:
        if character not in "IV":
            break

        numeral += character

    return numeral or None


def terraformable(state: str | None) -> str | None:
    if state is None or state == "Not terraformable":
        return "no"

    if state == "Terraformable":
        return "yes"

    return None


def has_atmosphere(body: dict) -> str:
    """Whether a planet has an atmosphere; a missing atmosphereType counts as none."""
    kind = body.get("atmosphereType")

    return "no" if kind is None or kind == "No atmosphere" else "yes"


def build_index() -> dict[str, list[tuple]]:
    index: dict[str, list[tuple]] = {}

    for entry in ENTRIES:
        index.setdefault(entry[3], []).append(entry)

    return index


INDEX = build_index()


def entry_of(body: dict) -> int | None:
    sub_type = body.get("subType")
    candidates = INDEX.get(sub_type)

    if not candidates:
        return None

    if sub_type == "Earth-like world":
        return candidates[0][0] if body.get("terraformingState") in EARTH_LIKE_STATES else None

    luminosity = luminosity_class(body.get("luminosity")) if body.get("type") == "Star" else None
    trf = terraformable(body.get("terraformingState"))
    atmosphere = has_atmosphere(body)

    for entry_id, _, _, _, classes, wants_trf, wants_atmosphere in candidates:
        if classes and luminosity not in classes:
            continue

        if wants_trf and wants_trf != trf:
            continue

        if wants_atmosphere and wants_atmosphere != atmosphere:
            continue

        return entry_id

    return None


def count_batch(batch: bytes, regions: RegionMap) -> tuple[Counter, Counter, int, int]:
    found: Counter = Counter()
    unmatched: Counter = Counter()
    systems = outside = 0

    for line in batch.split(b"\n"):
        line = line.strip()

        if not line.startswith(b"{"):
            continue

        if line.endswith(b","):
            line = line[:-1]

        system = json.loads(line)
        systems += 1
        coords = system.get("coords")
        region = regions.find(coords["x"], coords["z"]) if coords else None

        if region is None:
            outside += 1
            continue

        entries = set()

        for body in system.get("bodies") or ():
            if body.get("type") not in ("Star", "Planet"):
                continue

            entry = entry_of(body)

            if entry is None:
                unmatched[(
                    body.get("type"),
                    body.get("subType"),
                    luminosity_class(body.get("luminosity")) if body.get("type") == "Star" else "",
                    body.get("terraformingState"),
                    has_atmosphere(body) if body.get("type") == "Planet" else "",
                )] += 1
            else:
                entries.add(entry)

        for entry in entries:
            found[(entry, region)] += 1

    return found, unmatched, systems, outside


class DumpError(RuntimeError):
    """The download cannot go on."""


def describe(limit: int | None) -> dict:
    """The dump's size, ETag and date, and how many compressed bytes this run reads."""
    request = urllib.request.Request(DUMP_URL, method="HEAD", headers={"User-Agent": AGENT})

    with urllib.request.urlopen(request, timeout=120) as head:
        total = int(head.headers["Content-Length"])

        return {
            "end": min(total, limit) if limit else total,
            "etag": head.headers["ETag"],
            "modified": parsedate_to_datetime(head.headers["Last-Modified"]).date().isoformat(),
        }


def download(dump: dict, chunks: queue.Queue) -> None:
    """Puts the compressed dump on the queue in chunks, then None, resuming a dropped download where it stopped.

    A failure is put on the queue in place of None.
    """
    end = dump["end"]
    offset = 0
    failures = 0
    started = shown = time.time()

    try:
        while offset < end:
            headers = {"User-Agent": AGENT, "Range": f"bytes={offset}-{end - 1}", "If-Range": dump["etag"]}

            try:
                with urllib.request.urlopen(urllib.request.Request(DUMP_URL, headers=headers), timeout=120) as response:
                    if response.status != 206:
                        raise DumpError(f"the dump changed during the run (HTTP {response.status} at byte {offset}); run it again")

                    while offset < end:
                        chunk = response.read(READ_BYTES)

                        if not chunk:
                            break

                        offset += len(chunk)
                        failures = 0
                        chunks.put(chunk)

                        if time.time() - shown >= 60:
                            shown = time.time()
                            rate = offset / (shown - started)
                            print(
                                f"{offset / 1e9:.1f} of {end / 1e9:.1f} GB, {rate / 1e6:.1f} MB/s, "
                                f"{(end - offset) / rate / 60:.0f} min left",
                                flush=True,
                            )
            except (OSError, urllib.error.URLError, http.client.HTTPException) as error:
                failures += 1

                if failures > 20:
                    raise DumpError(f"download failed at byte {offset} after 20 retries: {error}") from error

                print(f"download dropped at byte {offset} ({error}); resuming", flush=True)
                time.sleep(min(60, 5 * failures))

        chunks.put(None)
    except DumpError as error:
        chunks.put(error)


def batches(chunks: queue.Queue, whole: bool):
    """Yields whole lines of the decompressed dump in batches as the download thread delivers it."""
    decompressor = zlib.decompressobj(wbits=31)
    pending = b""

    while (chunk := chunks.get()) is not None:
        if isinstance(chunk, Exception):
            raise chunk

        pending += decompressor.decompress(chunk)

        if len(pending) >= BATCH_BYTES:
            cut = pending.rfind(b"\n") + 1
            yield pending[:cut]
            pending = pending[cut:]

    if whole:
        pending += decompressor.flush()

        if not decompressor.eof:
            raise DumpError("the dump ended before its gzip stream did")
    else:
        pending = pending[: pending.rfind(b"\n") + 1]

    if pending:
        yield pending


def check_fixture(regions: RegionMap) -> None:
    rows = 0

    for line in FIXTURE.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue

        cells = line.split("\t")
        expected = int(cells[4] if len(cells) > 4 else cells[3])
        found = regions.find(float(cells[0]), float(cells[2]))
        rows += 1

        if found != expected:
            raise SystemExit(f"region port disagrees with GalacticRegions.Find at {line!r}: found {found}")

    if rows == 0:
        raise SystemExit(f"{FIXTURE} has no positions")

    print(f"Region port agrees with all {rows} fixture positions", flush=True)


def check_entries() -> None:
    ids = [entry[0] for entry in ENTRIES]
    names = [entry[1].lower() for entry in ENTRIES]

    if len(set(ids)) != len(ids) or len(set(names)) != len(names):
        raise SystemExit("ENTRIES repeats an EntryID or a name")

    for line in CODEX.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue

        cells = line.split("\t")

        if int(cells[0]) in ids:
            raise SystemExit(f"EntryID {cells[0]} is in both Codex.tsv and ENTRIES")

        if cells[1].lower() in names:
            raise SystemExit(f"the name {cells[1]!r} is in both Codex.tsv and ENTRIES")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--limit-bytes", type=int, help="read only this many compressed bytes from the start")
    parser.add_argument("--output", type=Path, default=OUTPUT)
    arguments = parser.parse_args()

    started = time.time()
    regions = RegionMap(REGIONS)
    check_fixture(regions)
    check_entries()

    found: Counter = Counter()
    unmatched: Counter = Counter()
    systems = outside = 0
    dump = describe(arguments.limit_bytes)
    chunks: queue.Queue = queue.Queue(maxsize=QUEUED_CHUNKS)
    threading.Thread(target=download, args=(dump, chunks), daemon=True).start()

    for batch in batches(chunks, whole=not arguments.limit_bytes):
        counts, missed, seen, lost = count_batch(batch, regions)
        found.update(counts)
        unmatched.update(missed)
        systems += seen
        outside += lost

    lines = [
        "# Generated by tools/gen-codex-bodies.py. Do not edit by hand — rerun the tool.",
        f"# Source: Spansh's galaxy dump (downloads.spansh.co.uk/galaxy.json.gz) of {dump['modified']}.",
        "# Columns: EntryID, English name, category, subcategory, then the Spansh body fields that select the entry —",
        "# subType, luminosity classes, terraformable (yes, no, or blank for either), atmosphere (yes, no, or blank) —",
        "# then regions as id:systems pairs. systems is the number of known systems in the region with at least one such body.",
        "# Names and categories are Frontier's own game data; see NOTICE.",
        f"# Entries: {len(ENTRIES)}.",
    ]

    for entry_id, name, subcategory, sub_type, classes, trf, atmosphere in ENTRIES:
        counts = sorted((region, n) for (entry, region), n in found.items() if entry == entry_id)
        regions_cell = ",".join(f"{region}:{n}" for region, n in counts)
        lines.append(
            f"{entry_id}\t{name}\t{CATEGORY}\t{subcategory}\t{sub_type}\t{','.join(classes)}\t{trf}\t{atmosphere}\t{regions_cell}"
        )

    arguments.output.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")

    print(f"Read {systems} systems; {outside} outside the region map.")
    print("Bodies matching no entry, most first:")

    for (kind, sub_type, luminosity, state, atmosphere), n in unmatched.most_common():
        print(f"  {n}\t{kind}\t{sub_type}\t{luminosity}\t{state}\t{atmosphere}")

    print(f"Wrote {len(ENTRIES)} entries to {arguments.output} in {(time.time() - started) / 60:.0f} min")
    return 0


if __name__ == "__main__":
    sys.exit(main())
