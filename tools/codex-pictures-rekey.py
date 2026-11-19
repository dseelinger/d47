"""Adds a `kind` row for each stock codex picture listed under an old star or planet EntryID.

Downloads index.json from the codex pictures release (or reads --index), adds for each `entryId` row that
src/D47.Core/Knowledge/CodexEntryKinds.cs maps a `kind` row in the same region pointing at the same asset, writes
the new index to --out and prints each added row. The `entryId` rows stay. A kind row already in the index for that
region is not added again. Nothing is uploaded.

    python tools/codex-pictures-rekey.py --out <file>
    python tools/codex-pictures-rekey.py --index <copy of index.json> --out <file>
"""

import argparse
import json
import os
import re
import sys
import urllib.request

REPOSITORY = "dseelinger/d47-codex-pictures"
TAG = "codex-pictures-1"
INDEX_URL = f"https://github.com/{REPOSITORY}/releases/download/{TAG}/index.json"
MAP_SOURCE = os.path.join(os.path.dirname(os.path.abspath(__file__)), os.pardir, "src", "D47.Core", "Knowledge", "CodexEntryKinds.cs")
MAP_ROW = re.compile(r'^\s*\[(\d+)\]\s*=\s*"([a-z][a-z0-9_.]*)",', re.MULTILINE)
FIELDS = ("regionId", "tag", "asset", "sha256", "bytes")


def entry_kinds(source=MAP_SOURCE):
    """The EntryID to kind key map in CodexEntryKinds.cs."""
    with open(source, encoding="utf-8") as handle:
        rows = MAP_ROW.findall(handle.read())

    if not rows:
        raise SystemExit(f"No map rows found in {source}.")

    return {int(entry_id): kind for entry_id, kind in rows}


def rekey(rows, kinds):
    """The index rows with a kind row added for each mapped entryId row, and the added rows."""
    present = {(row["kind"], row["regionId"]) for row in rows if "kind" in row}
    added = []

    for row in rows:
        kind = kinds.get(row.get("entryId"))

        if kind is None or (kind, row["regionId"]) in present:
            continue

        present.add((kind, row["regionId"]))
        added.append({"kind": kind, **{field: row[field] for field in FIELDS}})

    return rows + added, added


def ordered(rows):
    """Rows as CodexPictureIndex.Write sorts them: entryId rows by EntryID, then kind rows by key, each by region."""
    return sorted(
        rows,
        key=lambda row: (0, row["entryId"], "", row["regionId"]) if "entryId" in row else (1, 0, row["kind"], row["regionId"]),
    )


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--index", help="read this copy of index.json instead of downloading it")
    parser.add_argument("--out", required=True, help="where to write the new index")
    args = parser.parse_args(argv)

    if args.index:
        with open(args.index, encoding="utf-8-sig") as handle:
            text = handle.read()
    else:
        with urllib.request.urlopen(INDEX_URL, timeout=60) as response:
            text = response.read().decode("utf-8-sig")

    rows = json.loads(text)
    result, added = rekey(rows, entry_kinds())

    with open(args.out, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(ordered(result), handle, indent=2, ensure_ascii=False)

    for row in added:
        print(f"+ {row['kind']} region {row['regionId']} -> {row['asset']}")

    print(f"{len(added)} kind rows added, {len(rows)} rows kept; written to {args.out}.")
    print(f"Nothing was uploaded. To publish it, upload it as index.json to the {TAG} release of {REPOSITORY}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
