#!/usr/bin/env python3
"""Decode and re-encode the hidden layer of d47's stock stories.

Not part of the build. `src/D47.Core/Stories/StoryCatalog.sealed` holds the hidden layer as JSON,
raw-deflated and then base64, so its text is not read by accident. It is not encryption.

    python tools/seal-stories.py decode <path outside the repository>
    python tools/seal-stories.py encode <path of the decoded file>

Decode writes the hidden layer as indented JSON and refuses a path inside the repository, so the
decoded text is never committed. Edit it there, then encode it back. Adding a story is one entry
here and one in `StoryCatalog.json`, sharing an `id`.

Each entry: {"id", "secret", "end", "beats", "scan", "clues", "finale", "options", "cast"}, paced by the
"length" on its card, one of the keys of LENGTHS.
  scan     a story of NARRATED lengths only: one line, the Commander's beacon scan before the story opens,
           narrated word for word at the pick. A longer story has none and flies to a real beacon.
  beats    one non-empty line for each beat of the length's sheet, keyed as in BEATS, and no other.
  clues    exactly one line for each of the length's clue days.
  finale   exactly one line for each of the length's finale chapters.
  options  1 to 4 endings, each {"id", "label", "after", "add"}; "add" is a list of persona ids.
  cast     0 to 4 speakers, each {"id", "name", "who", "provider", "voice"}; provider is kokoro or
           chatterbox, and voice "own" is chatterbox only.
           A speaker may instead carry "versions": {"forMan": {...}, "forWoman": {...}}, each
           {"name", "voice", "provider"?}, and then no "name" or "voice" of its own. forMan is the
           version a Commander who is a man meets; forWoman, one who is a woman. Hidden text names
           such a speaker as {name:<cast-id>}, and the card names neither the token nor either name.
The scan line and every clue and finale line is {"speaker", "text"}, the speaker "ship", "narrator" or a cast id.
The gate (EveryStoryKeepsTheFormatOfItsLengthGateTests) checks the same rules, and the persona and voice ids.
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
SHORT = ("openingImage", "catalyst", "breakIntoTwo", "midpoint", "finale", "finalImage")
# length: (clues, finale chapters, beats), as StoryPacing in src/D47.Core/Stories/StoryPacing.cs.
LENGTHS = {
    "3-days": (1, 1, SHORT),
    "1-week": (2, 2, SHORT + ("allIsLost",)),
    "2-weeks": (4, 2, SHORT + ("funAndGames", "allIsLost")),
    "1-month": (5, 3, SHORT + ("funAndGames", "allIsLost")),
    "3-months": (7, 3, BEATS),
    "6-months": (8, 4, BEATS),
    "1-year": (14, 4, BEATS),
}
# The lengths whose beacon scan is narrated at the pick, as StoryPacing.NarratedScan.
NARRATED = ("3-days", "1-week", "2-weeks")
PROVIDERS = ("kokoro", "chatterbox")
SPEAKERS = ("ship", "narrator")
VERSIONS = ("forMan", "forWoman")
CARD_FIELDS = ("blurb", "inYourWords", "beacon")
NAME_TOKEN = re.compile(r"\{name:([^{}\s]+)\}")


def text(value) -> bool:
    return isinstance(value, str) and bool(value.strip())


def persona_ids() -> set:
    return set(re.findall(r'Id: "([^"]+)"', PERSONAS.read_text(encoding="utf-8")))


def hidden_texts(entry: dict):
    """Every hidden text a name token may appear in."""
    for field in ("secret", "end"):
        yield field, entry.get(field)
    for beat, line in (entry.get("beats") or {}).items():
        yield f"beat {beat}", line
    if isinstance(entry.get("scan"), dict):
        yield "scan line", entry["scan"].get("text")
    for name, key in (("clue", "clues"), ("finale line", "finale")):
        for at, line in enumerate(entry.get(key) or []):
            yield f"{name} {at + 1}", line.get("text")
    for option in entry.get("options") or []:
        yield f"option {option.get('id')} label", option.get("label")
        yield f"option {option.get('id')} after", option.get("after")
    for speaker in entry.get("cast") or []:
        yield f"cast {speaker.get('id')} who", speaker.get("who")


def voice_fault(name: str, provider, voice) -> list:
    if provider not in PROVIDERS:
        return [f"{name} has the provider {provider}, not kokoro or chatterbox"]
    if voice == "own" and provider != "chatterbox":
        return [f"{name} has the voice own with a provider other than chatterbox"]
    return []


def faults(entry: dict, personas: set, card: dict | None = None) -> list:
    found = [f"no {field}" for field in FIELDS if not text(entry.get(field))]

    length = (card or {}).get("length")
    if length not in LENGTHS:
        found.append(f"the card's length is {length}, not one of {', '.join(LENGTHS)}")
    clue_count, finale_count, sheet = LENGTHS.get(length, LENGTHS["1-year"])

    beats = entry.get("beats") or {}
    found += [f"no beat {beat}" for beat in sheet if not text(beats.get(beat))]
    found += [f"beat {beat} is not a beat of a {length} story" for beat in BEATS if beat not in sheet and text(beats.get(beat))]

    scan = entry.get("scan")
    if length in NARRATED and not isinstance(scan, dict):
        found.append(f"no scan line; a {length} story opens after a narrated beacon scan")
    elif length not in NARRATED and scan is not None:
        found.append(f"a scan line; a {length} story flies to a real beacon")

    clues = entry.get("clues") or []
    if len(clues) != clue_count:
        found.append(f"{len(clues)} clues, not {clue_count}")

    finale = entry.get("finale") or []
    if len(finale) != finale_count:
        found.append(f"{len(finale)} finale lines, not {finale_count}")

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
    versioned = {}
    for at, speaker in enumerate(cast):
        sid = speaker.get("id")
        versions = speaker.get("versions")
        own = ("id", "who") if versions is not None else ("id", "name", "who", "voice")
        missing = [field for field in own if not text(speaker.get(field))]
        if missing:
            found.append(f"cast {at + 1} ({sid}) has no {', '.join(missing)}")
        if sid in speakers or sid in personas:
            found.append(f"cast {at + 1} has the id {sid}, taken by the ship, the narrator, a persona or another speaker")
        speakers.add(sid)
        if versions is None:
            found += voice_fault(f"cast {at + 1} ({sid})", speaker.get("provider"), speaker.get("voice"))
            continue
        versioned[sid] = []
        if not isinstance(versions, dict) or any(key not in versions for key in VERSIONS):
            found.append(f"cast member {sid} has one version, not forMan and forWoman")
            versions = versions if isinstance(versions, dict) else {}
        if "name" in speaker or "voice" in speaker:
            found.append(f"cast member {sid} has versions and also a name or a voice of its own")
        for key in VERSIONS:
            version = versions.get(key)
            if version is None:
                continue
            if not text(version.get("name")) or not text(version.get("voice")):
                found.append(f"cast member {sid} version {key} has no name or no voice")
                continue
            versioned[sid].append(version["name"])
            found += voice_fault(f"cast member {sid} version {key}", version.get("provider", speaker.get("provider")), version["voice"])

    for field, value in hidden_texts(entry):
        for cid in sorted(set(NAME_TOKEN.findall(value or ""))):
            if cid not in versioned:
                found.append(f"{field} has the token {{name:{cid}}}, and cast member {cid} has no versions")

    for field in CARD_FIELDS:
        value = (card or {}).get(field) or ""
        for cid in sorted(set(NAME_TOKEN.findall(value))):
            found.append(f"the card's {field} has the token {{name:{cid}}}; call cast member {cid} by role")
        for sid, names in versioned.items():
            if any(re.search(rf"\b{re.escape(name)}\b", value) for name in names):
                found.append(f"the card's {field} names cast member {sid}; call the member by role")

    scanned = [scan] if isinstance(scan, dict) else []
    for name, lines in (("scan line", scanned), ("clue", clues), ("finale line", finale)):
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
    public = {entry["id"]: entry for entry in json.loads(PUBLIC.read_text(encoding="utf-8"))}
    personas = persona_ids()

    for index, entry in enumerate(entries):
        found = faults(entry, personas, public.get(entry.get("id")))
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
