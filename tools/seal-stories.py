#!/usr/bin/env python3
"""Decode and re-encode the hidden layer of d47's stock stories.

Not part of the build. `src/D47.Core/Stories/StoryCatalog.sealed` holds the hidden layer as JSON,
raw-deflated and then base64, so its text is not read by accident. It is not encryption.

    python tools/seal-stories.py decode <path outside the repository>
    python tools/seal-stories.py encode <path of the decoded file>

Decode writes the hidden layer as indented JSON and refuses a path inside the repository, so the
decoded text is never committed. Edit it there, then encode it back. Adding a story is one entry
here and one in `StoryCatalog.json`, sharing an `id`.

Each entry: {"id", "secret", "end", "beats", "clues", "finale", "options", "cast"}.
  beats    one non-empty line for each of the 15 Save the Cat beats, keyed as in BEATS.
  clues    exactly 14 lines: 4 weekly, then 10 monthly.
  finale   exactly 4 lines, one for each finale chapter.
  options  1 to 4 endings, each {"id", "label", "after", "add"}; "add" is a list of persona ids.
  cast     0 to 4 speakers, each {"id", "name", "who", "provider", "voice"}; provider is kokoro or
           chatterbox, and voice "own" is chatterbox only.
Every clue and finale line is {"speaker", "text"}, the speaker "ship", "narrator" or a cast id.
The gate (EveryStoryKeepsTheYearFormatGateTests) checks the same rules, and the persona and voice ids.
"""
import base64
import json
import pathlib
import re
import sys
import zlib

REPO = pathlib.Path(__file__).resolve().parent.parent
SEALED = REPO / "src" / "D47.Core" / "Stories" / "StoryCatalog.sealed"
PUBLIC = REPO / "src" / "D47.Core" / "Stories" / "StoryCatalog.json"
PERSONAS = REPO / "src" / "D47.Core" / "Persona" / "PersonaCatalog.cs"
FIELDS = ("id", "secret", "end")
BEATS = (
    "openingImage", "themeStated", "setUp", "catalyst", "debate", "breakIntoTwo", "bStory", "funAndGames",
    "midpoint", "badGuysCloseIn", "allIsLost", "darkNightOfTheSoul", "breakIntoThree", "finale", "finalImage",
)
CLUES = 14
FINALE = 4
PROVIDERS = ("kokoro", "chatterbox")
SPEAKERS = ("ship", "narrator")


def text(value) -> bool:
    return isinstance(value, str) and bool(value.strip())


def persona_ids() -> set:
    return set(re.findall(r'Id: "([^"]+)"', PERSONAS.read_text(encoding="utf-8")))


def faults(entry: dict, personas: set) -> list:
    found = [f"no {field}" for field in FIELDS if not text(entry.get(field))]

    beats = entry.get("beats") or {}
    found += [f"no beat {beat}" for beat in BEATS if not text(beats.get(beat))]

    clues = entry.get("clues") or []
    if len(clues) != CLUES:
        found.append(f"{len(clues)} clues, not {CLUES}")

    finale = entry.get("finale") or []
    if len(finale) != FINALE:
        found.append(f"{len(finale)} finale lines, not {FINALE}")

    options = entry.get("options") or []
    if not 1 <= len(options) <= 4:
        found.append(f"{len(options)} options, not 1 to 4")
    for at, option in enumerate(options):
        missing = [field for field in ("id", "label", "after") if not text(option.get(field))]
        if missing:
            found.append(f"option {at + 1} has no {', '.join(missing)}")

    cast = entry.get("cast") or []
    if len(cast) > 4:
        found.append(f"{len(cast)} cast, more than 4")
    speakers = set(SPEAKERS)
    for at, speaker in enumerate(cast):
        sid = speaker.get("id")
        missing = [field for field in ("id", "name", "who", "voice") if not text(speaker.get(field))]
        if missing:
            found.append(f"cast {at + 1} has no {', '.join(missing)}")
        if sid in speakers or sid in personas:
            found.append(f"cast {at + 1} has the id {sid}, taken by the ship, the narrator, a persona or another speaker")
        speakers.add(sid)
        if speaker.get("provider") not in PROVIDERS:
            found.append(f"cast {at + 1} has the provider {speaker.get('provider')}, not kokoro or chatterbox")
        elif speaker.get("voice") == "own" and speaker.get("provider") != "chatterbox":
            found.append(f"cast {at + 1} has the voice own with a provider other than chatterbox")

    for name, lines in (("clue", clues), ("finale line", finale)):
        for at, line in enumerate(lines):
            if not text(line.get("text")):
                found.append(f"{name} {at + 1} has no text")
            if not text(line.get("speaker")):
                found.append(f"{name} {at + 1} has no speaker")
            elif line["speaker"] not in speakers:
                found.append(f"{name} {at + 1} is spoken by {line['speaker']}, not the ship, the narrator or the cast")

    return found


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
    personas = persona_ids()

    for index, entry in enumerate(entries):
        found = faults(entry, personas)
        if found:
            sys.exit(f"Entry {index + 1} ({entry.get('id')}): " + "; ".join(found))
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
