#!/usr/bin/env python3
"""Unpack one published stock story from the GitHub release `stories-1` into a folder.

Not part of the build. Writes nothing inside the repository and uploads nothing.

    python tools/unpack-story.py <story-id> <folder outside the repository>

Downloads `index.json`, `<id>.sealed` and each `<id>.<cast-id>[.for-man|.for-woman].original.png` from the
release, and writes `card.json` (the story's card from the index), `hidden.json` (the unsealed entry, indented)
and each picture as `<name>.png`: the layout `publish-story.py` reads. A picture the release does not have is
skipped with a line saying so.
"""
import argparse
import base64
import json
import pathlib
import shutil
import subprocess
import sys
import tempfile
import zlib

REPO = pathlib.Path(__file__).resolve().parent.parent
RELEASE = "stories-1"
INDEX = "index.json"
ORIGINAL = ".original.png"


def outside_repository(path: pathlib.Path, what: str) -> pathlib.Path:
    path = path.resolve()
    if path == REPO or REPO in path.parents:
        sys.exit(f"Refusing a {what} inside the repository: {path}")
    return path


def gh(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["gh", *args], capture_output=True, text=True, encoding="utf-8")


def download(work: pathlib.Path, *patterns: str) -> None:
    args = ["release", "download", RELEASE, "--dir", str(work), "--clobber"]
    for pattern in patterns:
        args += ["--pattern", pattern]
    fetched = gh(*args)
    if fetched.returncode != 0:
        sys.exit(f"Could not download from {RELEASE}: {fetched.stderr.strip()}")


def unseal(text: str) -> dict:
    packed = base64.b64decode("".join(text.split()))
    entries = json.loads(zlib.decompress(packed, -15).decode("utf-8"))
    if len(entries) != 1:
        sys.exit(f"The sealed file holds {len(entries)} entries; expected one")
    return entries[0]


def picture_names(entry: dict) -> list:
    names = []
    for speaker in entry.get("cast") or []:
        base = f"{entry['id']}.{speaker['id']}"
        names += [f"{base}.for-man", f"{base}.for-woman"] if speaker.get("versions") is not None else [base]
    return names


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("story")
    parser.add_argument("folder", type=pathlib.Path)
    args = parser.parse_args()

    folder = outside_repository(args.folder, "story folder")
    work = pathlib.Path(tempfile.mkdtemp(prefix="d47-unpack-story-"))
    try:
        download(work, INDEX, f"{args.story}.sealed")
        index = json.loads((work / INDEX).read_text(encoding="utf-8"))
        card = next((c for c in index if c.get("id") == args.story), None)
        sealed = work / f"{args.story}.sealed"
        if card is None or not sealed.is_file():
            sys.exit(f"{args.story} is not in the release {RELEASE}")
        entry = unseal(sealed.read_text(encoding="ascii"))
        if entry.get("id") != args.story:
            sys.exit(f"The sealed entry has the id {entry.get('id')}, not {args.story}")

        names = picture_names(entry)
        view = gh("release", "view", RELEASE, "--json", "assets")
        if view.returncode != 0:
            sys.exit(f"Could not read the release {RELEASE}: {view.stderr.strip()}")
        held = {asset["name"] for asset in json.loads(view.stdout)["assets"]}
        have = [name for name in names if name + ORIGINAL in held]
        for name in names:
            if name not in have:
                print(f"{name}{ORIGINAL} is not in the release; skipped")
        if have:
            download(work, *[name + ORIGINAL for name in have])

        folder.mkdir(parents=True, exist_ok=True)
        (folder / "card.json").write_text(json.dumps(card, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        (folder / "hidden.json").write_text(json.dumps(entry, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        for name in have:
            shutil.copyfile(work / (name + ORIGINAL), folder / f"{name}.png")
        print(f"Unpacked {args.story} to {folder}: card.json, hidden.json and {len(have)} pictures")
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
