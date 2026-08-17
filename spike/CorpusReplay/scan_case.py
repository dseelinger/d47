"""Which journal fields does Elite spell more than one way, differing only by case?

The ColonisationContribution / MainEngines family, measured rather than guessed. Walks every
event in the corpus, records distinct string values per (event, dotted field path), and reports
every path where case-folding collapses more than one spelling.

Usage: python scan_case.py [journal-directory]
"""
import collections
import glob
import json
import os
import sys

HOME = os.path.expanduser("~")
DEFAULT = os.path.join(HOME, "Saved Games", "Frontier Developments", "Elite Dangerous")

# Values that are prose or identifiers rather than symbols: a system name legitimately varies,
# and a localised string is meant to. Reported separately rather than dropped.
PROSE = {"Message", "Message_Localised", "From", "To", "Name_Localised", "Body",
         "StarSystem", "SystemName", "Station", "StationName", "Title", "Description"}

MAX_VALUES = 4000  # per path, so one unbounded field cannot eat the run


def walk(prefix, node, out, depth=0):
    if depth > 3:
        return
    if isinstance(node, dict):
        for k, v in node.items():
            if isinstance(v, str):
                path = prefix + "." + k if prefix else k
                bucket = out[path]
                if len(bucket) < MAX_VALUES:
                    bucket.add(v)
            elif isinstance(v, (dict, list)):
                walk(prefix + "." + k if prefix else k, v, out, depth + 1)
    elif isinstance(node, list):
        for v in node[:50]:
            if isinstance(v, (dict, list)):
                walk(prefix + "[]", v, out, depth + 1)


def main():
    directory = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    files = sorted(glob.glob(os.path.join(directory, "Journal.*.log")))
    print(f"{len(files)} journals in {directory}")

    per_event = collections.defaultdict(lambda: collections.defaultdict(set))
    lines = 0
    bad = 0

    for f in files:
        with open(f, encoding="utf-8", errors="replace") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                lines += 1
                try:
                    e = json.loads(line)
                except Exception:
                    bad += 1
                    continue
                kind = e.get("event")
                if not kind:
                    continue
                walk("", e, per_event[kind])

    print(f"{lines:,} lines, {bad} unparseable, {len(per_event)} event kinds\n")

    findings = []
    for kind in sorted(per_event):
        for path, values in per_event[kind].items():
            if path in ("event", "timestamp"):
                continue
            folded = collections.defaultdict(set)
            for v in values:
                folded[v.lower()].add(v)
            clashes = {k: v for k, v in folded.items() if len(v) > 1}
            if clashes:
                leaf = path.split(".")[-1].replace("[]", "")
                findings.append((kind, path, leaf in PROSE, clashes))

    symbols = [f for f in findings if not f[2]]
    prose = [f for f in findings if f[2]]

    print(f"=== {len(symbols)} SYMBOL paths spelled more than one way ===\n")
    for kind, path, _, clashes in sorted(symbols, key=lambda f: -len(f[3])):
        print(f"{kind}.{path}  — {len(clashes)} value(s) with a case clash")
        for low, spellings in sorted(clashes.items())[:6]:
            print(f"    {sorted(spellings)}")
        if len(clashes) > 6:
            print(f"    … and {len(clashes) - 6} more")
        print()

    print(f"=== {len(prose)} PROSE paths (expected to vary; listed for completeness) ===")
    for kind, path, _, clashes in sorted(prose, key=lambda f: -len(f[3]))[:15]:
        print(f"  {kind}.{path} — {len(clashes)}")


if __name__ == "__main__":
    main()
