#!/usr/bin/env python3
"""Publish one approved stock story to the GitHub release `stories-1`.

Not part of the build. Writes nothing inside the repository.

    python tools/publish-story.py <folder> [--replace] [--dry-run] [--work <dir outside the repository>]

<folder> holds `card.json`, `hidden.json` and the picked pictures: `<story-id>.<cast-id>.png`, or
`<story-id>.<cast-id>.for-man.png` and `.for-woman.png` for a cast member with `versions`.

The script downloads `index.json` from the release (an empty list if there is none), refuses an id already in
it unless --replace is given, checks the card and hidden entry with `faults` from seal-stories.py and requires a
picture for every primary cast member (any other member with no picture file is published without one), numbers the card,
lists the primary members' picture names in its `castPictures`, and builds in the work directory: `<id>.sealed`, a 1024 px
JPEG `<name>.jpg` and the kept `<name>.original.png` for each picture, and the new `index.json`. It then
uploads them with `gh release upload`, `index.json` last. --dry-run stops before the upload and lists each
file with its size. The release is created with --latest=false when missing: UpdateChecker reads the latest
release to find updates.
"""
import argparse
import base64
import importlib.util
import json
import pathlib
import shutil
import subprocess
import sys
import tempfile
import zlib

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is needed to convert the pictures: python -m pip install pillow")

REPO = pathlib.Path(__file__).resolve().parent.parent
RELEASE = "stories-1"
INDEX = "index.json"
SIZE = 1024
QUALITY = 85


def load_sealer():
    spec = importlib.util.spec_from_file_location("seal_stories", REPO / "tools" / "seal-stories.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def outside_repository(path: pathlib.Path, what: str) -> pathlib.Path:
    path = path.resolve()
    if path == REPO or REPO in path.parents:
        sys.exit(f"Refusing a {what} inside the repository: {path}")
    return path


def gh(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["gh", *args], capture_output=True, text=True, encoding="utf-8")


def download_index(work: pathlib.Path) -> list:
    """The release's story list; empty when the release or its index.json is missing."""
    view = gh("release", "view", RELEASE, "--json", "assets")
    if view.returncode != 0:
        if "not found" in view.stderr.lower():
            return []
        sys.exit(f"Could not read the release {RELEASE}: {view.stderr.strip()}")
    if INDEX not in [asset["name"] for asset in json.loads(view.stdout)["assets"]]:
        return []
    fetched = gh("release", "download", RELEASE, "--pattern", INDEX, "--dir", str(work), "--clobber")
    if fetched.returncode != 0:
        sys.exit(f"Could not download {INDEX}: {fetched.stderr.strip()}")
    return json.loads((work / INDEX).read_text(encoding="utf-8"))


def picture_names(entry: dict) -> list:
    """(name, primary) for every picture the cast can have."""
    names = []
    for speaker in entry.get("cast") or []:
        base = f"{entry['id']}.{speaker['id']}"
        primary = speaker.get("primary") is True
        bases = [f"{base}.for-man", f"{base}.for-woman"] if speaker.get("versions") is not None else [base]
        names += [(name, primary) for name in bases]
    return names


def seal(entry: dict) -> str:
    packer = zlib.compressobj(9, zlib.DEFLATED, -15)
    packed = packer.compress(json.dumps([entry], ensure_ascii=False).encode("utf-8")) + packer.flush()
    text = base64.b64encode(packed).decode("ascii")
    return "\n".join(text[at:at + 100] for at in range(0, len(text), 100)) + "\n"


def convert(source: pathlib.Path, work: pathlib.Path, name: str) -> list:
    shutil.copyfile(source, work / f"{name}.original.png")
    with Image.open(source) as image:
        image.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS).save(work / f"{name}.jpg", "JPEG", quality=QUALITY)
    return [work / f"{name}.jpg", work / f"{name}.original.png"]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("folder", type=pathlib.Path)
    parser.add_argument("--replace", action="store_true", help="replace a story already in the release")
    parser.add_argument("--dry-run", action="store_true", help="check and build, list the files, upload nothing")
    parser.add_argument("--work", type=pathlib.Path, help="working directory outside the repository")
    args = parser.parse_args()

    folder = outside_repository(args.folder, "story folder")
    work = outside_repository(args.work, "working directory") if args.work else None
    if work is None:
        work = pathlib.Path(tempfile.mkdtemp(prefix="d47-publish-story-"))
    work.mkdir(parents=True, exist_ok=True)
    for stale in work.iterdir():
        if stale.is_file():
            stale.unlink()

    card = json.loads((folder / "card.json").read_text(encoding="utf-8"))
    entry = json.loads((folder / "hidden.json").read_text(encoding="utf-8"))
    story = card.get("id")
    if not story or entry.get("id") != story:
        sys.exit(f"card.json has the id {story} and hidden.json has {entry.get('id')}; they must match")

    index = download_index(work)
    known = next((at for at, existing in enumerate(index) if existing.get("id") == story), None)
    if known is not None and not args.replace:
        sys.exit(f"{story} is already in the release; pass --replace to publish it again")

    sealer = load_sealer()
    problems = [f"{story}: {fault}" for fault in sealer.faults(entry, sealer.persona_ids(), card)]
    sources = {}
    for name, primary in picture_names(entry):
        picture = folder / f"{name}.png"
        if picture.is_file():
            sources[name] = picture
        elif primary:
            problems.append(f"{story}: no picture {picture.name}; a primary cast member needs one")
        else:
            print(f"{story}: no picture {picture.name}; published without it")
    if problems:
        print("\n".join(problems), file=sys.stderr)
        sys.exit(1)

    card["castPictures"] = [name for name, primary in picture_names(entry) if primary]
    card["number"] = index[known]["number"] if known is not None else max((c.get("number", 0) for c in index), default=0) + 1
    if known is not None:
        index[known] = card
    else:
        index.append(card)

    (work / f"{story}.sealed").write_text(seal(entry), encoding="ascii", newline="\n")
    files = [work / f"{story}.sealed"]
    for name, source in sources.items():
        files += convert(source, work, name)
    (work / INDEX).write_text(json.dumps(index, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    files.append(work / INDEX)

    for file in files:
        print(f"{file.name}  {file.stat().st_size:,} bytes")
    if args.dry_run:
        print(f"Dry run: nothing uploaded. Files are in {work}")
        return

    if gh("release", "view", RELEASE).returncode != 0:
        created = gh("release", "create", RELEASE, "--latest=false", "--title", "Stock stories",
                     "--notes", "Stock story files downloaded by d47. Not an app version.")
        if created.returncode != 0:
            sys.exit(f"Could not create the release {RELEASE}: {created.stderr.strip()}")
    for batch in (files[:-1], files[-1:]):
        uploaded = gh("release", "upload", RELEASE, *map(str, batch), "--clobber")
        if uploaded.returncode != 0:
            sys.exit(f"Upload failed: {uploaded.stderr.strip()}")
    print(f"Published {story} as number {card['number']} to {RELEASE}.")


if __name__ == "__main__":
    main()
