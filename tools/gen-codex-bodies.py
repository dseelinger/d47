#!/usr/bin/env python3
"""Regenerate d47's table of star and planet kinds and the systems known to have each, per galactic region.

Not part of the build. Its output is committed as `src/D47.Core/Knowledge/CodexBodies.tsv` and
shipped as an embedded resource beside `Codex.tsv`; the app never runs this and never reaches Spansh.

    python tools/gen-codex-bodies.py                                           # the local mirror when there is one, else the dump
    python tools/gen-codex-bodies.py --stream                                  # stream the dump even when the mirror is there
    python tools/gen-codex-bodies.py --db <file>                               # read the mirror at another path
    python tools/gen-codex-bodies.py --limit-bytes 400000000 --output <file>   # a trial on the dump's head
    python tools/gen-codex-bodies.py --counts <file>                           # also save the counts
    python tools/gen-codex-bodies.py --from-counts <file>                      # write the table from saved counts

Source: Spansh's galaxy dump (downloads.spansh.co.uk/galaxy.json.gz), about 119 GB compressed, either
streamed once and never saved or read from the local SQLite mirror at MIRROR when that file has a full
import. The dump is a JSON array with one system per line; each system has `coords` and `bodies`.
The mirror is written by a tool outside this repository and has three tables: `systems(id64, updated,
built, record)`, where `record` is a system's dump line, zlib-compressed and without its trailing
comma; `applied(kind, lastModified, appliedAt, systems, added, replaced)`, one row per Spansh file
applied, `kind` being `all` for the full dump; and `meta(key, value)`, whose `completeAsOf` row is the
time up to which every system is current. `--limit-bytes` always streams. Each system is placed in a codex region by a port of `GalacticRegions.Find`
over the committed `Regions.tsv`, and each star and planet with a `subType` is put in one kind by
`kind_of`, the rules `CodexKinds.OfSpansh` follows in Core. A region's count for a kind is the number
of systems in it with at least one body of that kind.

Kinds form a tree, one row per node: a star's subType, then its luminosity band; a gas giant's
subType, then its set of ring types; a planet's subType, then terraformable or not, then with or
without atmosphere, then the atmosphere. Only a row that no other row names as its parent has regions.
"""

import argparse
import http.client
import json
import queue
import re
import sqlite3
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
MIRROR = Path(r"C:\d47-data\spansh\galaxy.db")

ROOT = Path(__file__).resolve().parent.parent
KNOWLEDGE = ROOT / "src" / "D47.Core" / "Knowledge"
OUTPUT = KNOWLEDGE / "CodexBodies.tsv"
REGIONS = KNOWLEDGE / "Regions.tsv"
FIXTURE = ROOT / "tests" / "fixtures" / "codex" / "region-positions.tsv"
KINDS_FIXTURE = ROOT / "tests" / "fixtures" / "codex" / "kinds.tsv"

FAMILIES = {"star": "Stars", "gas": "Gas giant planets", "planet": "Terrestrial planets"}

# Luminosity bands by the class's Roman numeral; "0" is a hypergiant (0, O or Ia0). A tie for the usual band goes to
# the band earlier in BAND_ORDER.
BANDS = {"IV": "dwarf", "V": "dwarf", "VI": "dwarf", "III": "giant", "I": "supergiant", "II": "supergiant", "0": "hypergiant", "VII": "white dwarf"}
BAND_ORDER = ("dwarf", "giant", "supergiant", "hypergiant", "white dwarf")
UNBANDED = ("Neutron Star", "Black Hole", "Supermassive Black Hole")

RING_ORDER = ("Icy", "Rocky", "Metal Rich", "Metallic")
NO_ATMOSPHERE = "No atmosphere"

# The band of a star of a banded subType with no luminosity, while the usual bands are not yet known.
PENDING = "?"

KIND_SHAPE = re.compile(r"[a-z][a-z0-9_.]*\Z")

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


def band_of(luminosity: str | None) -> str | None:
    return BANDS.get(luminosity_class(luminosity))


def is_unbanded(sub_type: str) -> bool:
    """White dwarfs, neutron stars and black holes: kinds by subType alone, whatever their luminosity."""
    return sub_type.startswith("White Dwarf (") or sub_type in UNBANDED


def is_gas_giant(sub_type: str) -> bool:
    lowered = sub_type.lower()

    return "gas giant" in lowered or "water giant" in lowered


def kind_of(body: dict, usual) -> tuple | None:
    """The kind of a Spansh body as a field tuple, or None; `usual` gives a banded star subType's usual band or None.

    ("star", subType) for a star with no band, ("star", subType, band), ("gas", subType, ring types in
    RING_ORDER), ("planet", subType, terraformable, atmosphere).
    """
    kind = body.get("type")
    sub_type = body.get("subType")

    if kind not in ("Star", "Planet") or not sub_type:
        return None

    if kind == "Star":
        if is_unbanded(sub_type):
            return ("star", sub_type)

        band = band_of(body.get("luminosity")) or usual(sub_type)

        return ("star", sub_type, band) if band else None

    if is_gas_giant(sub_type):
        types = {ring.get("type") for ring in body.get("rings") or ()}

        return ("gas", sub_type, tuple(ring for ring in RING_ORDER if ring in types))

    state = body.get("terraformingState")

    return ("planet", sub_type, state is not None and state != "Not terraformable", body.get("atmosphereType") or NO_ATMOSPHERE)


def slug(field: str) -> str:
    """The field lowercased, each run of other characters made "_", with none at either end."""
    return re.sub(r"[^a-z0-9]+", "_", field.lower()).strip("_")


def ring_phrase(rings: tuple) -> str:
    if not rings:
        return "no rings"

    if len(rings) == 1:
        return f"{rings[0]} rings"

    return f"{', '.join(rings[:-1])} and {rings[-1]} rings"


def rows_of(kind: tuple) -> list[dict]:
    """The rows from the family's top group down to the kind, each with its key and parent key."""
    family, sub_type = kind[0], kind[1]
    top = f"{family}.{slug(sub_type)}"
    base = {"family": FAMILIES[family], "subType": sub_type, "band": "", "terraformable": "", "atmosphere": "", "rings": ""}
    rows = [dict(base, key=top, name=sub_type, parent="")]

    if family == "star":
        if len(kind) == 3:
            rows.append(dict(base, key=f"{top}.{slug(kind[2])}", name=f"{sub_type}, {kind[2]}", parent=top, band=kind[2]))
    elif family == "gas":
        rings = kind[2]
        rows.append(dict(
            base,
            key=f"{top}.{'_'.join(slug(ring) for ring in rings) if rings else 'no_rings'}",
            name=f"{sub_type}, {ring_phrase(rings)}",
            parent=top,
            rings=",".join(rings) if rings else "None"))
    else:
        terraformable, atmosphere = kind[2], kind[3]
        word = "terraformable" if terraformable else "not terraformable"
        level = dict(base, terraformable="yes" if terraformable else "no")
        middle = f"{top}.{slug(word)}"
        rows.append(dict(level, key=middle, name=f"{sub_type}, {word}", parent=top))

        if atmosphere == NO_ATMOSPHERE:
            rows.append(dict(level, key=f"{middle}.no_atmosphere", name=f"{sub_type}, {word}, {NO_ATMOSPHERE}", parent=middle, atmosphere=NO_ATMOSPHERE))
        else:
            group = f"{middle}.atmosphere"
            rows.append(dict(level, key=group, name=f"{sub_type}, {word}, with atmosphere", parent=middle))
            rows.append(dict(level, key=f"{group}.{slug(atmosphere)}", name=f"{sub_type}, {word}, {atmosphere}", parent=group, atmosphere=atmosphere))

    return rows


def key_of(kind: tuple) -> str:
    return rows_of(kind)[-1]["key"]

def count_batch(batch: bytes, regions: RegionMap) -> dict:
    """Systems per (kind, region); bodies per (banded subType, band); systems with a star of no luminosity."""
    found: Counter = Counter()
    bodies: Counter = Counter()
    pending: Counter = Counter()
    left_out: Counter = Counter()
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

        kinds = set()
        waiting = set()

        for body in system.get("bodies") or ():
            kind = kind_of(body, lambda _: PENDING)

            if kind is None:
                if body.get("type") in ("Star", "Planet"):
                    left_out[(body.get("type"), body.get("subType"))] += 1

                continue

            if kind[0] == "star" and len(kind) == 3:
                if kind[2] == PENDING:
                    waiting.add(kind[1])
                    continue

                bodies[(kind[1], kind[2])] += 1

            kinds.add(kind)

        for kind in kinds:
            found[(kind, region)] += 1

        for sub_type in waiting:
            bands = tuple(sorted(kind[2] for kind in kinds if kind[0] == "star" and len(kind) == 3 and kind[1] == sub_type))
            pending[(sub_type, region, bands)] += 1

    return {"found": found, "bodies": bodies, "pending": pending, "left_out": left_out, "systems": systems, "outside": outside}


def merge(total: dict, part: dict) -> None:
    for name in ("found", "bodies", "pending", "left_out"):
        total[name].update(part[name])

    total["systems"] += part["systems"]
    total["outside"] += part["outside"]

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


def open_mirror(path: Path) -> sqlite3.Connection:
    return sqlite3.connect(f"{path.resolve().as_uri()}?mode=ro", uri=True)


def mirror_date(path: Path) -> str | None:
    """The date the mirror is complete up to, or None when it has no full import."""
    db = open_mirror(path)

    try:
        modified = [row[0] for row in db.execute("SELECT lastModified FROM applied WHERE kind = 'all'")]

        if not modified:
            return None

        row = db.execute("SELECT value FROM meta WHERE key = 'completeAsOf'").fetchone()
    finally:
        db.close()

    return (row[0] if row and row[0] else max(modified))[:10]


def mirror_batches(path: Path):
    """Yields the mirror's system lines in id64 order in batches of about BATCH_BYTES."""
    db = open_mirror(path)

    try:
        lines: list[bytes] = []
        size = 0

        for (record,) in db.execute("SELECT record FROM systems ORDER BY id64"):
            line = zlib.decompress(record)
            lines.append(line)
            size += len(line) + 1

            if size >= BATCH_BYTES:
                yield b"\n".join(lines)
                lines, size = [], 0

        if lines:
            yield b"\n".join(lines)
    finally:
        db.close()


def usable_mirror(path: Path) -> str | None:
    """The mirror's date when the file is a readable mirror with a full import, else None."""
    if not path.is_file():
        return None

    try:
        return mirror_date(path)
    except sqlite3.Error:
        return None


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

def check_kinds() -> None:
    """Stops on a fixture row the classifier gives another key for."""
    rows = 0

    for line in KINDS_FIXTURE.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue

        cells = line.split("\t")
        names = ("type", "subType", "luminosity", "terraformingState", "atmosphereType")
        body = {name: cell for name, cell in zip(names, cells) if cell}

        if cells[5]:
            body["rings"] = [{"type": ring} for ring in cells[5].split(",")]

        kind = kind_of(body, lambda _: cells[6] or None)
        found = key_of(kind) if kind else None
        rows += 1

        if found != cells[7]:
            raise SystemExit(f"the classifier disagrees with {KINDS_FIXTURE.name} at {line!r}: found {found}")

    if rows == 0:
        raise SystemExit(f"{KINDS_FIXTURE} has no rows")

    print(f"Classifier agrees with all {rows} fixture rows", flush=True)


def usual_bands(bodies: Counter) -> dict[str, str]:
    """Each banded star subType's band with the most bodies."""
    return {
        sub_type: max(BAND_ORDER, key=lambda band: (bodies[(sub_type, band)], -BAND_ORDER.index(band)))
        for sub_type in sorted({sub_type for sub_type, _ in bodies})
    }


def table(counts: dict) -> tuple[list[dict], dict[str, Counter], Counter]:
    """The rows sorted by key, the systems per region of each kind, and the bodies left out."""
    usual = usual_bands(counts["bodies"])
    found: Counter = Counter(counts["found"])
    left_out: Counter = Counter(counts["left_out"])

    for (sub_type, region, bands), n in counts["pending"].items():
        band = usual.get(sub_type)

        if band is None:
            left_out[("Star", f"{sub_type}, no luminosity")] += n
        elif band not in bands:
            found[(("star", sub_type, band), region)] += n

    rows: dict[str, dict] = {}
    kinds: dict[str, tuple] = {}
    regions: dict[str, Counter] = {}

    for (kind, region), n in found.items():
        key = key_of(kind)

        if kinds.setdefault(key, kind) != kind:
            raise SystemExit(f"{kinds[key]} and {kind} both give the key {key}")

        regions.setdefault(key, Counter())[region] += n

        for row in rows_of(kind):
            if rows.setdefault(row["key"], row) != row:
                raise SystemExit(f"two rows have the key {row['key']}: {rows[row['key']]} and {row}")

    parents = {row["parent"] for row in rows.values()}

    for key, row in rows.items():
        if not KIND_SHAPE.match(key) or ".." in key:
            raise SystemExit(f"{key!r} is not a kind key")

        if row["parent"] and row["parent"] not in rows:
            raise SystemExit(f"{key} names {row['parent']} as its parent, which has no row")

        if (key in regions) == (key in parents):
            raise SystemExit(f"{key} is {'both a kind and a group' if key in regions else 'neither a kind nor a group'}")

    for row in rows.values():
        banded_top = row["family"] == FAMILIES["star"] and not row["parent"] and row["key"] in parents
        row["usual"] = usual.get(row["subType"], "") if banded_top else ""

    return [rows[key] for key in sorted(rows)], regions, left_out


def save_counts(counts: dict, dump: dict, path: Path) -> None:
    path.write_text(json.dumps({
        "modified": dump["modified"],
        "systems": counts["systems"],
        "outside": counts["outside"],
        "found": [[list(kind), region, n] for (kind, region), n in counts["found"].items()],
        "bodies": [[sub_type, band, n] for (sub_type, band), n in counts["bodies"].items()],
        "pending": [[sub_type, region, list(bands), n] for (sub_type, region, bands), n in counts["pending"].items()],
        "left_out": [[kind, sub_type, n] for (kind, sub_type), n in counts["left_out"].items()],
    }), encoding="utf-8")


def load_counts(path: Path) -> tuple[dict, dict]:
    saved = json.loads(path.read_text(encoding="utf-8"))

    def kind(fields: list) -> tuple:
        return tuple(tuple(field) if isinstance(field, list) else field for field in fields)

    counts = {
        "systems": saved["systems"],
        "outside": saved["outside"],
        "found": Counter({(kind(fields), region): n for fields, region, n in saved["found"]}),
        "bodies": Counter({(sub_type, band): n for sub_type, band, n in saved["bodies"]}),
        "pending": Counter({(sub_type, region, tuple(bands)): n for sub_type, region, bands, n in saved["pending"]}),
        "left_out": Counter({(body_type, sub_type): n for body_type, sub_type, n in saved["left_out"]}),
    }

    return counts, {"modified": saved["modified"]}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--limit-bytes", type=int, help="read only this many compressed bytes from the start")
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--counts", type=Path, help="also save the counts to this file")
    parser.add_argument("--from-counts", type=Path, help="write the table from counts saved by --counts; read no dump")
    parser.add_argument("--stream", action="store_true", help="stream the dump even when the local mirror is there")
    parser.add_argument("--db", type=Path, help="read the mirror at this path; it must have a full import")
    arguments = parser.parse_args()

    if arguments.db and arguments.stream:
        parser.error("--db and --stream cannot be used together")

    if (arguments.db or arguments.stream) and (arguments.limit_bytes or arguments.from_counts):
        parser.error("--db and --stream cannot be used with --limit-bytes or --from-counts")

    mirror, mirrored = None, None

    if arguments.db:
        if not arguments.db.is_file():
            parser.error(f"no mirror at {arguments.db}")

        try:
            mirrored = mirror_date(arguments.db)
        except sqlite3.Error as error:
            parser.error(f"{arguments.db} is not a readable mirror: {error}")

        if mirrored is None:
            parser.error(f"{arguments.db} has no full import (no applied row with kind 'all')")

        mirror = arguments.db
    elif not (arguments.stream or arguments.limit_bytes or arguments.from_counts):
        mirrored = usable_mirror(MIRROR)
        mirror = MIRROR if mirrored else None

    if not arguments.from_counts:
        print(f"Source: local mirror {mirror} of {mirrored}" if mirror else f"Source: {DUMP_URL}", flush=True)

    started = time.time()
    check_kinds()

    if arguments.from_counts:
        counts, dump = load_counts(arguments.from_counts)
    else:
        regions = RegionMap(REGIONS)
        check_fixture(regions)

        counts = {"found": Counter(), "bodies": Counter(), "pending": Counter(), "left_out": Counter(), "systems": 0, "outside": 0}

        if mirror:
            dump = {"modified": mirrored}
            source = mirror_batches(mirror)
        else:
            dump = describe(arguments.limit_bytes)
            chunks: queue.Queue = queue.Queue(maxsize=QUEUED_CHUNKS)
            threading.Thread(target=download, args=(dump, chunks), daemon=True).start()
            source = batches(chunks, whole=not arguments.limit_bytes)

        for batch in source:
            merge(counts, count_batch(batch, regions))

        if arguments.counts:
            save_counts(counts, dump, arguments.counts)

    rows, regions_of, left_out = table(counts)
    kinds = [row for row in rows if row["key"] in regions_of]
    per_family = Counter(row["family"] for row in kinds)
    families = ", ".join(f"{family} {per_family[family]}" for family in FAMILIES.values())

    lines = [
        "# Generated by tools/gen-codex-bodies.py. Do not edit by hand — rerun the tool.",
        f"# Source: Spansh's galaxy dump (downloads.spansh.co.uk/galaxy.json.gz) of {dump['modified']}.",
        "# Columns: key, name, family, parent key, then the Spansh body fields of the row — subType, luminosity band,",
        "# terraformable (yes or no), atmosphere, ring types (comma-separated, or None) — then the usual band of a banded",
        "# star's subType, then regions as id:systems pairs. systems is the number of known systems in the region with at",
        "# least one body of the kind. A row with no regions is a group of the rows that name it as their parent.",
        "# The body type words are Frontier's own game data; see NOTICE.",
        f"# Kinds: {len(kinds)} — {families}.",
    ]

    for row in rows:
        counts_cell = ",".join(f"{region}:{n}" for region, n in sorted(regions_of.get(row["key"], Counter()).items()))
        lines.append("\t".join((
            row["key"], row["name"], row["family"], row["parent"], row["subType"], row["band"],
            row["terraformable"], row["atmosphere"], row["rings"], row["usual"], counts_cell)))

    arguments.output.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")

    print(f"Read {counts['systems']} systems; {counts['outside']} outside the region map.")
    print(f"Kinds per family: {families}")
    print("Ten smallest kinds by systems:")

    for row in sorted(kinds, key=lambda row: (sum(regions_of[row["key"]].values()), row["key"]))[:10]:
        print(f"  {sum(regions_of[row['key']].values())}\t{row['key']}")

    print("Bodies left out, most first:")

    for (body_type, sub_type), n in left_out.most_common():
        print(f"  {n}\t{body_type}\t{sub_type}")

    print(f"Wrote {len(rows)} rows, {len(kinds)} kinds, to {arguments.output} in {(time.time() - started) / 60:.0f} min")
    return 0


if __name__ == "__main__":
    sys.exit(main())
