#!/usr/bin/env python3
"""The format rules for the hidden layer of d47's stock stories, used by tools/publish-story.py.

Not part of the build. `faults` checks one hidden entry and its card; the hidden layer is published as
`<id>.sealed` (raw-deflated JSON, then base64) by `publish-story.py`.

Each entry: {"id", "secret", "end", "beats", "opening", "scan", "clues", "finale", "options", "cast"}, paced by
the "length" on its card, one of the keys of LENGTHS. The card's "goals" and "beaconTrip", where present, are true or false.
  opening  optional, a story of any length: one or more lines said word for word, in order, at the pick, before
           the scan line and chapter one. Left out when the story has none; never an empty list.
           An opening line may carry {commander} ("Commander" and the Commander's name), {their} (his, her or
           their) and {they} (he, she or they), resolved as the line is spoken, and {name:<cast-id>}; no other token.
  scan     a story of NARRATED lengths only: one line, the Commander's beacon scan before the story opens,
           narrated word for word at the pick. A longer story has none and flies to a real beacon.
  beats    one non-empty line for each beat of the length's sheet, keyed as in BEATS, and no other.
  clues    exactly one line for each of the length's clue days.
  finale   exactly one line for each of the length's finale chapters.
  options  1 to 4 endings, each {"id", "label", "after", "add"}; "add" is a list of persona ids.
  cast     0 to 4 speakers, each {"id", "name", "who", "role", "provider", "voice"}; provider is a free speaking id in
           PROVIDERS (edge, kokoro or chatterbox), and voice "own" is chatterbox only. A billed provider
           (elevenlabs, openai, cartesia) is refused.
           "role" is who the speaker is to the Commander: a lower-case noun phrase of at most 160 characters,
           no closing full stop, naming neither version's name and holding no {name:} token.
           A speaker may instead carry "versions": {"forMan": {...}, "forWoman": {...}}, each
           {"name", "voice", "provider"?}, and then no "name" or "voice" of its own. forMan is the
           version a Commander who is a man meets; forWoman, one who is a woman. Hidden text names
           such a speaker as {name:<cast-id>}, and the card names neither the token nor either name.
           A speaker may carry "link", a number from 0 to 1: the signal strength of the comms link it is heard
           through. It may carry "effects", a list of {"id", "level"}: Guardian effect ids (GuardianVoice.Table),
           each at a level from 1 to 20, applied in order before the link. An id may be listed once.
           A speaker may carry "aboard": {"boards", "leaves"}, when it rides in the Commander's ship and is heard
           in the cockpit rather than through its link. Each is a beat of the length's sheet, or null for the
           story's start or end; boards comes before leaves. The member is aboard for every chapter whose stage
           hands the writer a beat from boards up to, not including, leaves. A speaker that rides more than once
           carries a list of two or more such spans in story order: each boards after the beat the one before it
           leaves at, only the first boards at null and only the last leaves at null. One span is an object.
           A span may carry "place": "ship" (the default) or "carrier", for a member who lives aboard the
           Commander's own fleet carrier rather than their ship; a member's spans share one place.
           A speaker may carry "recorded": true: it is heard only in recordings or messages and never speaks
           live. A recorded speaker never carries "aboard".
           A speaker may carry "pinned": true: it speaks in its provider and voice, not the ship's provider.
           Voice "own" is pinned whether or not it says so. A version never carries "pinned"; it covers both.
           A speaker may carry "primary": true, a recurring character the Commander knows from the card;
           a primary speaker must have a picture when the story is published, is named in the card's blurb
           or inYourWords unless it has versions, and never speaks in the voice "own".
  black    optional: a list of spans {"from", "to"} of the story that take place in the black, where chapters are held
           to neither the Sol band nor the home leg. Each is a beat of the length's sheet, or null for the story's start
           or end; from is included and to is not, so from comes before to. Spans do not overlap.
Every opening line, the scan line and every clue and finale line is {"speaker", "text"}, the speaker "ship", "narrator" or a cast id.
The gate (EveryStoryKeepsTheFormatOfItsLengthGateTests) checks the same rules, and the persona and voice ids.
"""
import pathlib
import re

REPO = pathlib.Path(__file__).resolve().parent.parent
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
# Speaking ids in TtsProviderCatalog whose Billed is false.
PROVIDERS = ("edge", "kokoro", "chatterbox")
BILLED = ("elevenlabs", "openai", "cartesia")
SPEAKERS = ("ship", "narrator")
VERSIONS = ("forMan", "forWoman")
CARD_FIELDS = ("blurb", "inYourWords", "beacon")
NAME_TOKEN = re.compile(r"\{name:([^{}\s]+)\}")
ANY_TOKEN = re.compile(r"\{[^{}]*\}")
OPENING_TOKEN = re.compile(r"\{(?:commander|their|they|name:[^{}\s]+)\}")


def text(value) -> bool:
    return isinstance(value, str) and bool(value.strip())


def guardian_ids() -> set:
    source = (REPO / "src" / "D47.Core" / "Audio" / "GuardianVoice.cs").read_text(encoding="utf-8")
    return set(re.findall(r'Id = "([^"]+)"', source))


def sound_fault(who: str, speaker: dict) -> list:
    found = []
    link = speaker.get("link")
    if "link" in speaker and (isinstance(link, bool) or not isinstance(link, (int, float)) or not 0 <= link <= 1):
        found.append(f"{who} has a link outside 0 to 1")
    effects = speaker.get("effects")
    if "effects" in speaker and not isinstance(effects, list):
        return found + [f"{who} has effects that are not a list"]
    known = guardian_ids()
    seen = set()
    for effect in effects or []:
        eid = effect.get("id") if isinstance(effect, dict) else None
        level = effect.get("level") if isinstance(effect, dict) else None
        if eid not in known:
            found.append(f"{who} has the effect {eid}, which is not a Guardian effect")
        elif eid in seen:
            found.append(f"{who} lists the effect {eid} twice")
        seen.add(eid)
        if isinstance(level, bool) or not isinstance(level, int) or not 1 <= level <= 20:
            found.append(f"{who} has the effect {eid} at a level outside 1 to 20")
    return found


def aboard_fault(who: str, speaker: dict, sheet: tuple, length) -> list:
    if "aboard" not in speaker:
        return []
    aboard = speaker["aboard"]
    if isinstance(aboard, list) and not aboard:
        return [f"{who} has an empty aboard list"]
    if isinstance(aboard, list) and len(aboard) == 1:
        return [f"{who} has an aboard list of one span, which is written as an object"]
    spans = aboard if isinstance(aboard, list) else [aboard]
    if any(not isinstance(span, dict) or set(span) - {"boards", "leaves", "place"} for span in spans):
        return [f"{who} has an aboard that is not {{\"boards\", \"leaves\", \"place\"}} or a list of them"]
    found = []
    previous = None
    for at, span in enumerate(spans):
        name = who if len(spans) == 1 else f"{who} span {at + 1}"
        known = True
        place = span.get("place", "ship")
        if place not in ("ship", "carrier"):
            found.append(f"{name} has aboard place {place!r}, not 'ship' or 'carrier'")
        elif at > 0 and place != spans[0].get("place", "ship"):
            found.append(f"{name} has aboard place {place!r}, not the first span's; a member's spans share one place")
        for field in ("boards", "leaves"):
            beat = span.get(field)
            if beat is not None and beat not in sheet:
                known = False
                found.append(f"{name} has aboard {field} {beat!r}, not a beat of a {length} story")
        boards, leaves = span.get("boards"), span.get("leaves")
        if at > 0 and boards is None:
            found.append(f"{name} boards at null; only the first span boards at the start")
        if at < len(spans) - 1 and leaves is None:
            found.append(f"{name} leaves at null; only the last span leaves at the end")
        if known and boards is not None and leaves is not None and BEATS.index(boards) >= BEATS.index(leaves):
            found.append(f"{name} leaves at or before the beat it boards at")
        if known and previous is not None and boards is not None and BEATS.index(boards) <= BEATS.index(previous):
            found.append(f"{name} boards at or before the beat the span before it leaves at")
        previous = leaves if leaves in sheet else None
    return found


def black_fault(entry: dict, sheet: tuple, length) -> list:
    black = entry.get("black")
    if black is None:
        return []
    if not isinstance(black, list) or any(not isinstance(span, dict) or set(span) - {"from", "to"} for span in black):
        return ["black is not a list of {\"from\", \"to\"} spans"]
    found = []
    known = []
    for at, span in enumerate(black):
        name = f"black span {at + 1}"
        in_sheet = True
        for field in ("from", "to"):
            beat = span.get(field)
            if beat is not None and beat not in sheet:
                in_sheet = False
                found.append(f"{name} has {field} {beat!r}, not a beat of a {length} story")
        if not in_sheet:
            continue
        start = 0 if span.get("from") is None else BEATS.index(span["from"])
        end = len(BEATS) if span.get("to") is None else BEATS.index(span["to"])
        if start >= end:
            found.append(f"{name} ends at or before the beat it starts at")
            continue
        found += [f"{name} overlaps {other}" for other_start, other_end, other in known if start < other_end and other_start < end]
        known.append((start, end, name))
    return found


def role_fault(sid, speaker: dict) -> list:
    role = speaker.get("role")
    if not text(role):
        return [f"cast member {sid} has no role"]
    found = []
    if role[0].isupper():
        found.append(f"cast member {sid} has a role that starts with a capital letter")
    if role.rstrip().endswith("."):
        found.append(f"cast member {sid} has a role that ends with a full stop")
    if len(role) > 160:
        found.append(f"cast member {sid} has a role of {len(role)} characters, more than 160")
    versions = speaker.get("versions") if isinstance(speaker.get("versions"), dict) else {}
    names = [speaker.get("name")] + [(v or {}).get("name") for v in versions.values() if isinstance(v, dict)]
    if "{name:" in role or any(text(n) and re.search(rf"\b{re.escape(n)}\b", role) for n in names):
        found.append(f"cast member {sid} has a role that holds a name or a {{name:}} token")
    return found


def persona_ids() -> set:
    return set(re.findall(r'Id: "([^"]+)"', PERSONAS.read_text(encoding="utf-8")))


def hidden_texts(entry: dict):
    """Every hidden text a name token may appear in."""
    for field in ("secret", "end"):
        yield field, entry.get(field)
    for beat, line in (entry.get("beats") or {}).items():
        yield f"beat {beat}", line
    for at, line in enumerate(entry.get("opening") or []):
        if isinstance(line, dict):
            yield f"opening line {at + 1}", line.get("text")
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
    if provider in BILLED:
        return [f"{name} has the provider {provider}, which is billed; a story may pin only a free one ({', '.join(PROVIDERS)})"]
    if provider not in PROVIDERS:
        return [f"{name} has the provider {provider}, not one of {', '.join(PROVIDERS)}"]
    if not text(voice):
        return [f"{name} has no voice"]
    if voice == "own" and provider != "chatterbox":
        return [f"{name} has the voice own with a provider other than chatterbox"]
    return []


def faults(entry: dict, personas: set, card: dict | None = None) -> list:
    found = [f"no {field}" for field in FIELDS if not text(entry.get(field))]

    length = (card or {}).get("length")
    if length not in LENGTHS:
        found.append(f"the card's length is {length}, not one of {', '.join(LENGTHS)}")
    clue_count, finale_count, sheet = LENGTHS.get(length, LENGTHS["1-year"])
    if "goals" in (card or {}) and not isinstance(card["goals"], bool):
        found.append(f"the card's goals is {card['goals']!r}, not true or false")
    if "beaconTrip" in (card or {}):
        if not isinstance(card["beaconTrip"], bool):
            found.append(f"the card's beaconTrip is {card['beaconTrip']!r}, not true or false")
        elif card["beaconTrip"] and length in NARRATED:
            found.append(f"the card's beaconTrip is set on a {length} story, shorter than 1 month")

    beats = entry.get("beats") or {}
    found += [f"no beat {beat}" for beat in sheet if not text(beats.get(beat))]
    found += [f"beat {beat} is not a beat of a {length} story" for beat in BEATS if beat not in sheet and text(beats.get(beat))]

    found += black_fault(entry, sheet, length)

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
        found += sound_fault(f"cast {at + 1} ({sid})", speaker)
        found += aboard_fault(f"cast {at + 1} ({sid})", speaker, sheet, length)
        found += role_fault(sid, speaker)
        if "primary" in speaker and not isinstance(speaker["primary"], bool):
            found.append(f"cast {at + 1} ({sid}) has a primary that is not true or false")
        if "recorded" in speaker and not isinstance(speaker["recorded"], bool):
            found.append(f"cast {at + 1} ({sid}) has a recorded that is not true or false")
        if speaker.get("recorded") is True and "aboard" in speaker:
            found.append(f"cast {at + 1} ({sid}) is recorded and also aboard")
        if "pinned" in speaker and not isinstance(speaker["pinned"], bool):
            found.append(f"cast {at + 1} ({sid}) has a pinned that is not true or false")
        if isinstance(speaker.get("versions"), dict) and any(isinstance(version, dict) and "pinned" in version for version in speaker["versions"].values()):
            found.append(f"cast member {sid} has pinned on a version; put it on the member")
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

    for speaker in cast:
        if speaker.get("primary") is not True:
            continue
        sid = speaker.get("id")
        versions = speaker.get("versions") if isinstance(speaker.get("versions"), dict) else {}
        if speaker.get("voice") == "own" or any((v or {}).get("voice") == "own" for v in versions.values()):
            found.append(f"cast member {sid} is primary and speaks in the Commander's own voice")
        name = speaker.get("name")
        if speaker.get("versions") is None and text(name) and not any(
            re.search(rf"\b{re.escape(name)}\b", (card or {}).get(field) or "") for field in ("blurb", "inYourWords")
        ):
            found.append(f"cast member {sid} is primary, and neither blurb nor inYourWords names them")

    opening = entry.get("opening")
    if "opening" in entry and (not isinstance(opening, list) or not opening):
        found.append("an opening that is not a list of one or more lines; leave it out when the story has none")
        opening = []
    opening = [line if isinstance(line, dict) else {} for line in opening or []]
    for at, line in enumerate(opening):
        unknown = sorted({token for token in ANY_TOKEN.findall(line.get("text") or "") if not OPENING_TOKEN.fullmatch(token)})
        if unknown:
            found.append(f"opening line {at + 1} has the token {', '.join(unknown)}, not {{commander}}, {{their}}, {{they}} or {{name:<cast-id>}}")

    scanned = [scan] if isinstance(scan, dict) else []
    for name, lines in (("opening line", opening), ("scan line", scanned), ("clue", clues), ("finale line", finale)):
        for at, line in enumerate(lines):
            if not text(line.get("text")):
                found.append(f"{name} {at + 1} has no text")
            if not text(line.get("speaker")):
                found.append(f"{name} {at + 1} has no speaker")
            elif line["speaker"] not in speakers:
                found.append(f"{name} {at + 1} is spoken by {line['speaker']}, not the ship, the narrator or the cast")

    return found
