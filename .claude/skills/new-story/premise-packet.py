"""Encode, decode and tally the surprise-story premise packets.

    python premise-packet.py encode <premise-dir> --round N --out <packet.txt> [--changes changes.json] [--settled ids.json]
    python premise-packet.py decode <packet.txt> --out <packet.json>
    python premise-packet.py lint <premise-dir>
    python premise-packet.py tally <reply.txt>... --round N --out <reviews.json>

encode reads every *.json list of premises in <premise-dir>, adds the published cards and used titles,
and writes base64 of the packet. tally decodes each reviewer's reply line by line (one line per id, then BATCH)
and prints ids, verdicts, scores and the ids that did not decode, never premise text; the decoded reviews go to --out.
"""

import argparse
import base64
import binascii
import json
import pathlib
import re
import subprocess
import sys
import unicodedata

SKILL = pathlib.Path(__file__).with_name("SKILL.md")
LEVEL_ORDER = {"N": 0, "M": 1, "E": 2}
LENGTH_ORDER = {"3D": 0, "1W": 1, "2W": 2, "1M": 3, "3M": 4, "6M": 5, "1Y": 6}


def ascii_text(text):
    for a, b in (("’", "'"), ("‘", "'"), ("“", '"'), ("”", '"'), ("—", ", "), ("–", "-"), ("…", "...")):
        text = text.replace(a, b)
    return unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode()


def sort_key(premise_id):
    level, length, slot = premise_id.split("-")
    return LEVEL_ORDER[level], LENGTH_ORDER[length], slot


def published_index():
    raw = subprocess.run(
        ["gh", "release", "download", "stories-1", "--repo", "dseelinger/d47", "--pattern", "index.json", "--output", "-"],
        capture_output=True, check=True).stdout
    return json.loads(raw.decode("utf-8"))


def used_titles(cards):
    section = re.search(r"## Titles already used(.*?)## The pitch", SKILL.read_text(encoding="utf-8"), re.S).group(1)
    items = " ".join(line.strip().removeprefix("- ") for line in section.splitlines() if line.startswith(("- ", "  ")))
    return sorted({t.strip().rstrip(".") for t in items.replace(". ", ", ").split(",") if t.strip()} | {c["title"] for c in cards})


def encode(args):
    premises = []
    for path in sorted(pathlib.Path(args.premise_dir).glob("*.json")):
        premises += json.loads(path.read_text(encoding="utf-8"))
    premises.sort(key=lambda p: sort_key(p["id"]))
    cards = published_index()
    packet = {
        "packet": "d47-premises",
        "round": args.round,
        "premiseCount": len(premises),
        "ids": [p["id"] for p in premises],
        "publishedCards": [
            {k: ascii_text(str(c.get(k, ""))) for k in ("title", "level", "length", "genre", "core", "tone", "blurb")} for c in cards],
        "titlesAlreadyUsed": used_titles(cards),
    }
    if args.changes:
        packet["changes"] = json.loads(pathlib.Path(args.changes).read_text(encoding="utf-8"))
    if args.settled:
        packet["settled"] = json.loads(pathlib.Path(args.settled).read_text(encoding="utf-8"))
    packet["premises"] = premises
    raw = json.dumps(packet, separators=(",", ":"), ensure_ascii=True).encode("ascii")
    encoded = base64.encodebytes(raw).decode("ascii")
    if json.loads(base64.b64decode(encoded)) != packet:
        sys.exit("round trip failed")
    pathlib.Path(args.out).write_text(encoded, encoding="ascii")
    print(f"round {args.round}: {len(premises)} premises, {len(encoded)} characters, {args.out}")


GENRES = ["Monster in the House", "Golden Fleece", "Dude with a Problem", "Whydunit", "Buddy Love", "Rites of Passage",
          "Fool Triumphant", "Institutionalized", "Out of the Bottle", "Superhero"]
CORES = ["warden", "cora", "analyst-prime", "llamo", "sentinel", "kex", "mender", "cartographer", "quartermaster", "archivist"]
LENGTHS = ["3-days", "1-week", "2-weeks", "1-month", "3-months", "6-months", "1-year"]
STOP = set("""the a an and or but of to in on at for with from by as is are was were be been it its this that these those
their there they them then than who whom whose what when where why how which not no nor all any each every one two three
your you commander commander's ship ships story days week weeks month months year years has have had will would can could
into out over under after before about only also more most much many some such own same other another just even still""".split())


def distinctive(text):
    return {w for w in re.findall(r"[a-z][a-z'-]{3,}", text.lower()) if w not in STOP}


def lint(args):
    """Check the batch and print ids and check names only."""
    premises = []
    for path in sorted(pathlib.Path(args.premise_dir).glob("*.json")):
        premises += json.loads(path.read_text(encoding="utf-8"))
    cards = published_index()
    used = {t.lower() for t in used_titles(cards)}
    published = {(c.get("genre"), c.get("core"), c.get("length")) for c in cards}
    faults = []
    for p in premises:
        i = p["id"]
        if any(ord(ch) > 127 for ch in json.dumps(p, ensure_ascii=False)):
            faults.append(f"{i}: non-ASCII")
        if p.get("genre") not in GENRES or p.get("core") not in CORES or p.get("length") not in LENGTHS:
            faults.append(f"{i}: genre, core or length not recognised")
        if p.get("title", "").lower() in used:
            faults.append(f"{i}: title already used")
        if re.search(r"\bwant(s|ed)?\b", json.dumps(p), re.I):
            faults.append(f"{i}: says want")
        if (p.get("genre"), p.get("core"), p.get("length")) in published:
            faults.append(f"{i}: same genre, core and length as a published card")
        if len(p.get("guesses", [])) != 3:
            faults.append(f"{i}: guesses is not three")
        shared = distinctive(p.get("stakes", "") + " " + p.get("hook", "")) & distinctive(p.get("secret", "") + " " + p.get("twist", ""))
        names = {w for w in re.findall(r"\b[A-Z][a-z]+\b", p.get("stakes", "") + " " + p.get("hook", ""))}
        shared -= {n.lower() for n in names}
        if len(shared) >= 3:
            faults.append(f"{i}: stakes or hook shares {len(shared)} words with the secret or twist")
    titles = [p.get("title", "").lower() for p in premises]
    if len(set(titles)) != len(titles):
        faults.append("batch: a title repeats")
    slots = {}
    for p in premises:
        slots.setdefault((p["level"], p["length"]), []).append(p)
    for key, pair in slots.items():
        if len(pair) == 2 and (pair[0]["genre"] == pair[1]["genre"] or pair[0]["core"] == pair[1]["core"]):
            faults.append(f"{pair[0]['id']}/{pair[1]['id']}: slot pair shares a genre or core")
    for level in ("new", "midrange", "endgame"):
        group = [p for p in premises if p["level"] == level]
        kinds = [p.get("twistKind") for p in group]
        if group and abs(kinds.count("hidden deception") - kinds.count("reversal of the goal")) > 4:
            faults.append(f"{level}: twist kinds {kinds.count('hidden deception')} deception, {kinds.count('reversal of the goal')} reversal")
    for fault in faults:
        print(fault)
    print(f"{len(premises)} premises, {len(faults)} faults")


def decode_text(text):
    runs = re.findall(r"[A-Za-z0-9+/=\s]{200,}", text)
    for run in sorted(runs, key=len, reverse=True):
        compact = re.sub(r"\s", "", run)
        compact += "=" * (-len(compact) % 4)
        try:
            return json.loads(base64.b64decode(compact).decode("utf-8"))
        except (binascii.Error, UnicodeDecodeError, json.JSONDecodeError):
            continue
    return None


def decode(args):
    packet = decode_text(pathlib.Path(args.packet).read_text(encoding="ascii"))
    if packet is None:
        sys.exit("no packet found")
    pathlib.Path(args.out).write_text(json.dumps(packet, indent=1), encoding="utf-8")
    print(f"round {packet['round']}: {packet['premiseCount']} premises, {args.out}")


REPLY_LINE = re.compile(r"^\s*([NME]-(?:3D|1W|2W|1M|3M|6M|1Y)-[AB]|BATCH)\s+(\S+)\s*$", re.M)


def decode_line(encoded):
    try:
        return json.loads(base64.b64decode(encoded + "=" * (-len(encoded) % 4)).decode("utf-8"))
    except (binascii.Error, UnicodeDecodeError, json.JSONDecodeError):
        return None


def read_reply(path):
    """One reviewer's reply: its name, the batch entry, the premise entries by id, and the ids that failed."""
    text = pathlib.Path(path).read_text(encoding="utf-8", errors="ignore")
    entries, failed, batch = {}, [], None
    for key, encoded in REPLY_LINE.findall(text):
        value = decode_line(encoded)
        if value is None:
            failed.append(key)
        elif key == "BATCH":
            batch = value
        else:
            entries[key] = value
    names = [e.get("reviewer") for e in entries.values() if e.get("reviewer")] + ([batch.get("reviewer")] if batch else [])
    name = names[0] if names else pathlib.Path(path).stem
    return name, batch, entries, failed


def tally(args):
    reviews = []
    for path in args.replies:
        name, batch, entries, failed = read_reply(path)
        if failed:
            print(f"{name}: did not decode {', '.join(failed)}")
        if not entries:
            print(f"{name}: no decodable premise lines")
        reviews.append({"reviewer": name, "batch": batch, "premises": entries, "failed": failed})
    ids = sorted({i for r in reviews for i in r["premises"]}, key=sort_key)
    settled = []
    print("id        " + "  ".join(r["reviewer"][:8].ljust(14) for r in reviews) + "  settled")
    for premise_id in ids:
        cells, verdicts = [], []
        for review in reviews:
            entry = review["premises"].get(premise_id)
            if entry is None:
                cells.append("-".ljust(14))
                continue
            verdicts.append(entry.get("verdict"))
            rules = ",".join(str(r.get("rule")) for r in entry.get("rules", []))
            cells.append(f"{entry.get('verdict', '?')[:3]} {entry.get('score', '?')}{' r' + rules if rules else ''}".ljust(14))
        entries = [review["premises"][premise_id] for review in reviews if premise_id in review["premises"]]
        flagged = any(e.get("specificMatch") or any(r.get("rule") != 11 for r in e.get("rules", [])) for e in entries)
        done = verdicts.count("keep") >= max(1, len(reviews) - 1) and "replace" not in verdicts and not flagged
        if done:
            settled.append(premise_id)
        print(premise_id.ljust(10) + "  ".join(cells) + ("  yes" if done else ""))
    print(f"{len(settled)} of {len(ids)} settled after round {args.round}")
    pathlib.Path(args.out).write_text(json.dumps({"round": args.round, "settled": settled, "reviews": reviews}, indent=1), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    e = sub.add_parser("encode")
    e.add_argument("premise_dir")
    e.add_argument("--round", type=int, required=True)
    e.add_argument("--out", required=True)
    e.add_argument("--changes")
    e.add_argument("--settled")
    d = sub.add_parser("decode")
    d.add_argument("packet")
    d.add_argument("--out", required=True)
    lt = sub.add_parser("lint")
    lt.add_argument("premise_dir")
    t = sub.add_parser("tally")
    t.add_argument("replies", nargs="+")
    t.add_argument("--round", type=int, required=True)
    t.add_argument("--out", required=True)
    args = parser.parse_args()
    {"encode": encode, "decode": decode, "lint": lint, "tally": tally}[args.command](args)


if __name__ == "__main__":
    main()
