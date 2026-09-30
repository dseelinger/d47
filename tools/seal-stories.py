#!/usr/bin/env python3
"""Decode and re-encode the hidden layer of d47's stock stories.

Not part of the build. `src/D47.Core/Stories/StoryCatalog.sealed` holds the hidden layer as JSON,
raw-deflated and then base64, so its text is not read by accident. It is not encryption.

    python tools/seal-stories.py decode <path outside the repository>
    python tools/seal-stories.py encode <path of the decoded file>

Decode writes the hidden layer as indented JSON and refuses a path inside the repository, so the
decoded text is never committed. Edit it there, then encode it back. Adding a story is one entry
here and one in `StoryCatalog.json`, sharing an `id`.

Each entry: {"id", "secret", "weeks", "months", "year", "end"}, every field a non-empty string.
"""
import base64
import json
import pathlib
import sys
import zlib

REPO = pathlib.Path(__file__).resolve().parent.parent
SEALED = REPO / "src" / "D47.Core" / "Stories" / "StoryCatalog.sealed"
PUBLIC = REPO / "src" / "D47.Core" / "Stories" / "StoryCatalog.json"
FIELDS = ("id", "secret", "weeks", "months", "year", "end")


def decode(target: pathlib.Path) -> None:
    target = target.resolve()
    if target == REPO or REPO in target.parents:
        sys.exit(f"Refusing to write the hidden layer inside the repository: {target}")

    data = zlib.decompress(base64.b64decode("".join(SEALED.read_text(encoding="ascii").split())), -15)
    entries = json.loads(data.decode("utf-8"))
    target.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Decoded {len(entries)} entries to {target}")


def encode(source: pathlib.Path) -> None:
    entries = json.loads(source.read_text(encoding="utf-8"))
    public = {entry["id"] for entry in json.loads(PUBLIC.read_text(encoding="utf-8"))}

    for index, entry in enumerate(entries):
        missing = [field for field in FIELDS if not str(entry.get(field, "")).strip()]
        if missing:
            sys.exit(f"Entry {index + 1} has no {', '.join(missing)}")
        if entry["id"] not in public:
            sys.exit(f"Entry {entry['id']} has no public entry in {PUBLIC.name}")

    packer = zlib.compressobj(9, zlib.DEFLATED, -15)
    packed = packer.compress(json.dumps(entries, ensure_ascii=False).encode("utf-8")) + packer.flush()
    text = base64.b64encode(packed).decode("ascii")
    lines = [text[at:at + 100] for at in range(0, len(text), 100)]
    SEALED.write_text("\n".join(lines) + "\n", encoding="ascii", newline="\n")
    print(f"Sealed {len(entries)} entries into {SEALED.relative_to(REPO)}")


if __name__ == "__main__":
    if len(sys.argv) != 3 or sys.argv[1] not in ("decode", "encode"):
        sys.exit(__doc__)

    (decode if sys.argv[1] == "decode" else encode)(pathlib.Path(sys.argv[2]))
