#!/usr/bin/env python3
"""Build the daily index of FSS signals that d47 searches.

Not part of the build. Run by .github/workflows/signal-index.yml; writes only the --out file.

    python tools/signal-index.py --out <signals.tsv.gz> [--index <previous index>] [--days N] [--end yyyy-MM-dd]
                                 [--archive-dir <folder>]
    python tools/signal-index.py --check <fixture folder>

Reads the EDDN archive of Journal.FSSSignalDiscovered messages, one file per UTC day, for the --days days ending
on --end (default: yesterday UTC), merges them into the previous index and writes the new one. The previous
index is gzipped or plain TSV and may be missing. A signal not seen for 90 days is dropped. A day whose file
cannot be fetched fails the run before --out is written.

--archive-dir reads `Journal.FSSSignalDiscovered-<date>.jsonl.bz2` (or `.jsonl`) from a folder instead of the
network. --check runs the fixture folder's `Journal.FSSSignalDiscovered-<date>.jsonl` over its optional
`previous.tsv` and compares the rows with `expected.tsv`.

Index: UTF-8 TSV with a header line, ordered by system_address, one row per (system_address, signal_type,
signal_name). A Megaship has one row per name, in the system it was last seen.
"""
import argparse
import bz2
import datetime
import gzip
import io
import json
import os
import sys
import tempfile
import urllib.error
import urllib.request

ARCHIVE_URL = "https://edgalaxydata.space/EDDN/{month}/Journal.FSSSignalDiscovered-{day}.jsonl.bz2"
USER_AGENT = "d47-signal-index (+https://github.com/dseelinger/d47)"
HEADER = "system_address\tsystem_name\tx\ty\tz\tsignal_type\tsignal_name\tlast_seen"
EXPIRY_DAYS = 90

DROPPED_TYPES = {"fleetcarrier", "squadroncarrier", "outpost", "combat", "uss"}
DROPPED_BY_TYPE_AND_PREFIX = {
    "generic": ("$fixed_event_", "$multiplayer_scenario81_title"),
    "installation": ("planetary construction site:",),
}
DROPPED_PREFIXES = ("$operations_", "test signal")


def is_dropped(signal_type, name):
    t = signal_type.lower()
    n = name.lower()
    if t in DROPPED_TYPES or t.startswith("station"):
        return True
    if n.startswith(DROPPED_PREFIXES):
        return True
    return n.startswith(DROPPED_BY_TYPE_AND_PREFIX.get(t, ()))


def clean(text):
    return " ".join(str(text).split())


def number(value):
    text = format(float(value), ".5f").rstrip("0").rstrip(".")
    return "0" if text in ("", "-0") else text


class Index:
    """Rows keyed by (system_address, type, name); a Megaship is keyed by name alone."""

    def __init__(self):
        self.rows = {}

    @staticmethod
    def key(address, signal_type, name):
        if signal_type == "Megaship":
            return ("Megaship", name)
        return (address, signal_type, name)

    def add(self, address, system, x, y, z, signal_type, name, seen):
        k = self.key(address, signal_type, name)
        row = (address, system, x, y, z, signal_type, name, seen)
        old = self.rows.get(k)
        if old is None or (seen, address) > (old[7], old[0]):
            self.rows[k] = row

    def expire(self, reference):
        cutoff = (reference - datetime.timedelta(days=EXPIRY_DAYS)).isoformat()
        self.rows = {k: r for k, r in self.rows.items() if r[7] >= cutoff}

    def lines(self):
        ordered = sorted(self.rows.values(), key=lambda r: (r[0], r[5], r[6]))
        return [HEADER] + ["\t".join(str(c) for c in r) for r in ordered]


def open_text(path):
    if path.endswith(".gz"):
        return io.TextIOWrapper(gzip.open(path, "rb"), encoding="utf-8")
    if path.endswith(".bz2"):
        return io.TextIOWrapper(bz2.open(path, "rb"), encoding="utf-8")
    return open(path, encoding="utf-8", newline="")


def read_index(path, index):
    with open_text(path) as f:
        first = f.readline().rstrip("\r\n")
        if first != HEADER:
            raise SystemExit(f"{path}: not a signal index (header is '{first}')")
        for line in f:
            cells = line.rstrip("\r\n").split("\t")
            if len(cells) != 8:
                raise SystemExit(f"{path}: row with {len(cells)} columns")
            index.add(int(cells[0]), cells[1], cells[2], cells[3], cells[4], cells[5], cells[6], cells[7])


def read_day(path, day, index):
    with open_text(path) as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                message = json.loads(line)
            except ValueError:
                continue
            message = message.get("message", message)
            try:
                address = int(message["SystemAddress"])
                system = clean(message["StarSystem"])
                x, y, z = (number(v) for v in message["StarPos"])
                signals = message["signals"]
            except (KeyError, TypeError, ValueError):
                continue
            seen = str(message.get("timestamp", day))[:10]
            if len(seen) != 10 or seen[4] != "-":
                seen = day
            for s in signals:
                signal_type = clean(s.get("SignalType", ""))
                name = clean(s.get("SignalName", ""))
                if signal_type and name and not is_dropped(signal_type, name):
                    index.add(address, system, x, y, z, signal_type, name, seen)


def archive_file(day, archive_dir, scratch):
    if archive_dir:
        stem = os.path.join(archive_dir, f"Journal.FSSSignalDiscovered-{day}.jsonl")
        for candidate in (stem + ".bz2", stem):
            if os.path.exists(candidate):
                return candidate
        raise SystemExit(f"{day}: no archive file in {archive_dir}")
    url = ARCHIVE_URL.format(month=day[:7], day=day)
    target = os.path.join(scratch, f"{day}.jsonl.bz2")
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    try:
        with urllib.request.urlopen(request, timeout=120) as response, open(target, "wb") as out:
            while chunk := response.read(1 << 20):
                out.write(chunk)
    except (urllib.error.URLError, OSError) as e:
        raise SystemExit(f"{day}: could not fetch {url}: {e}")
    return target


def write_gzip(path, lines):
    data = ("\n".join(lines) + "\n").encode("utf-8")
    with open(path, "wb") as raw, gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0, compresslevel=9) as gz:
        gz.write(data)


def days_ending(end, count):
    return [(end - datetime.timedelta(days=i)).isoformat() for i in range(count - 1, -1, -1)]


def build(index_path, days, archive_dir):
    index = Index()
    if index_path and os.path.exists(index_path):
        read_index(index_path, index)
    with tempfile.TemporaryDirectory() as scratch:
        for day in days:
            path = archive_file(day, archive_dir, scratch)
            read_day(path, day, index)
            if path.startswith(scratch):
                os.remove(path)
    index.expire(datetime.date.fromisoformat(days[-1]))
    return index


def check(folder):
    prefix = "Journal.FSSSignalDiscovered-"
    days = sorted(n[len(prefix):-len(".jsonl")] for n in os.listdir(folder)
                  if n.startswith(prefix) and n.endswith(".jsonl"))
    index = build(os.path.join(folder, "previous.tsv"), days, folder)
    with open(os.path.join(folder, "expected.tsv"), encoding="utf-8", newline="") as f:
        expected = f.read().splitlines()
    actual = index.lines()
    if actual != expected:
        print("signal-index check failed", file=sys.stderr)
        for line in actual:
            print("  got      " + line, file=sys.stderr)
        for line in expected:
            print("  expected " + line, file=sys.stderr)
        return 1
    print(f"signal-index check passed: {len(actual) - 1} rows")
    return 0


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--out")
    p.add_argument("--index")
    p.add_argument("--days", type=int, default=1)
    p.add_argument("--end")
    p.add_argument("--archive-dir")
    p.add_argument("--check")
    a = p.parse_args()
    if a.check:
        return check(a.check)
    if not a.out or a.days < 1:
        p.error("--out and a positive --days are required")
    if a.end:
        end = datetime.date.fromisoformat(a.end)
    else:
        end = datetime.datetime.now(datetime.timezone.utc).date() - datetime.timedelta(days=1)
    index = build(a.index, days_ending(end, a.days), a.archive_dir)
    write_gzip(a.out, index.lines())
    print(f"rows {len(index.rows)}, size {os.path.getsize(a.out)} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
