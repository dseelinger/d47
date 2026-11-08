"""Sung songs for the maintainer's own music folders: new lyrics from Claude, rendered on ACE-Step, copied in.

  with-lyrics.py                        run until every folder has its quota or stop.cmd is used
  with-lyrics.py --until 05:00          also stop at that local wall-clock time
  with-lyrics.py --count 1 --out DIR    one song into DIR/<folder>/, for a test
  with-lyrics.py --folders a,b          only those folders

Each folder's quota is its count in catalogue.BRIEFS. A song counts while its file is still in the folder, so a
deleted song is written again. The ledger is with-lyrics.tsv in the data folder; with-lyrics.log is the log.
Nothing here touches tracks\\ or generated.txt, so the weekly publish never sees these songs.
Shares generate.lock with the library generator, stops after the current song when STOP exists, and waits while
Elite Dangerous is running. A usage or rate limit from Claude ends the run.
"""

import argparse
import csv
import hashlib
import json
import os
import random
import re
import shutil
import subprocess
import sys
import time
import traceback
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import catalogue  # noqa: E402
import generate  # noqa: E402

DATA = generate.DATA
LEDGER = os.path.join(DATA, "with-lyrics.tsv")
LOG = os.path.join(DATA, "with-lyrics.log")
TMP = os.path.join(DATA, "_with-lyrics_tmp")
EXPERIMENT = os.path.join(DATA, "experiments", "lyrics")
OWN = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", "d47", "data", "audio", "music")
SUNG_OFF = os.path.join(DATA, "sung-off")
FIELDS = ["folder", "file", "title", "chorus", "vocal", "mood", "seed", "lyrics"]
MODEL = "opus"

CUES = {"arrival-from-supercruise", "arrival-from-hyperspace", "interdiction"}
SOLO_NEEDS = {"saxophone": "saxophone", "piano": "piano", "harp": "harp", "violin": "violin", "flute": "flute",
              "guitar": "guitar", "brass": "brass", "cello": "cello"}

VOCALS = ["warm male vocal", "breathy female vocal", "smoky female vocal", "soft male vocal", "airy female vocal",
          "rich female alto vocal", "tender male vocal", "intimate female vocal", "mellow male vocal",
          "clear male vocal", "gentle female vocal", "ethereal female vocal", "weathered male vocal",
          "smooth soulful female vocal", "hearty male vocal", "soft falsetto male vocal", "husky female vocal",
          "bright female vocal", "low gentle male vocal", "distant female vocal"]
COMBAT_VOCALS = ["gritty male baritone vocal", "deep powerful male vocal", "raspy male rock vocal",
                 "urgent gritty male vocal", "aggressive male vocal", "rough male vocal", "hoarse male vocal",
                 "breathless male vocal", "fierce female vocal", "powerful female belt vocal",
                 "dark female alto vocal", "deep ominous male vocal"]

ANGLES = ["a pilot talking to the ship", "a letter home that will arrive late", "a promise made before launch",
          "a station worker watching ships come and go", "an old wingmate remembered", "one small object in the cockpit",
          "the moment just before it happens", "the walk back afterwards", "a song the crew sings to stay awake",
          "a stranger met only over comms", "counting the jumps", "the view no one else will see",
          "a debt that has to be paid", "the first time doing this", "the hundredth time doing this",
          "what the pilot will tell no one", "a lighthouse of a star", "someone waiting at the other end",
          "a superstition kept for luck", "the quiet after the noise"]

RULES = """You write song lyrics for a sung track that plays in the game Elite Dangerous.

Rules:
- The song is about the situation given, from the point of view of a pilot or a station crew, in the game's
  setting: Federation, Empire, Alliance, Thargoids, Guardians, carriers, the Bubble and the deep black.
- Invent any person you need. Never name a real player, and never mention any app, tool or website.
- Write every line fresh. Do not quote or adapt lyrics from existing songs.
- One central image for the whole song.
- Section tags on their own line in square brackets, a blank line between sections. Allowed tags: [Intro],
  [Verse 1], [Verse 2], [Chorus], [Bridge], [Outro], and one instrumental break such as [Instrumental] or
  [Guitar Solo]. At most one descriptor per tag, such as [Chorus - anthemic].
- An instrumental break may only name an instrument listed in the arrangement.
- Lines short enough to sing in one breath: 4 to 10 syllables. Lines in matching places of similar length.
- No words in capitals, no parentheses, and never the word "choir".

Reply with JSON only, no other text: {"title": "<song title, taken from the chorus>", "lyrics": "<the lyrics>"}"""


class LimitReached(Exception):
    pass


def log(msg):
    line = f"{datetime.now():%Y-%m-%d %H:%M:%S} {msg}"
    print(line, flush=True)
    with open(LOG, "a", encoding="utf-8") as f:
        f.write(line + "\n")


generate.log = log  # should_stop, wait_for_game and load_models log here, not in generate.log


def read_ledger():
    if not os.path.exists(LEDGER):
        return []
    with open(LEDGER, encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f, delimiter="\t"))


def append_ledger(row):
    new = not os.path.exists(LEDGER)
    with open(LEDGER, "a", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=FIELDS, delimiter="\t")
        if new:
            w.writeheader()
        w.writerow({**row, "lyrics": row["lyrics"].replace("\n", "\\n")})


def chorus_of(lyrics):
    lines = lyrics.replace("\\n", "\n").splitlines()
    for i, line in enumerate(lines):
        if line.startswith("[Chorus"):
            return next((l for l in lines[i + 1:] if l.strip()), "")
    return ""


def digest(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def import_experiment(out):
    """Adds the experiment's songs to a new ledger, matched to their copies in the own-music folders by content."""
    if os.path.exists(LEDGER) or not os.path.isdir(EXPERIMENT):
        return
    for folder in catalogue.BRIEFS:
        mp3 = os.path.join(EXPERIMENT, f"{folder}.mp3")
        lyrics_path = os.path.join(EXPERIMENT, "lyrics", f"{folder}.txt")
        target = os.path.join(out, folder)
        if not (os.path.exists(mp3) and os.path.exists(lyrics_path) and os.path.isdir(target)):
            continue
        size, hashed = os.path.getsize(mp3), digest(mp3)
        match = next((n for n in os.listdir(target) if os.path.getsize(os.path.join(target, n)) == size
                      and digest(os.path.join(target, n)) == hashed), None)
        if not match:
            continue
        lyrics = open(lyrics_path, encoding="utf-8").read().strip()
        caption = open(os.path.join(EXPERIMENT, f"{folder}.txt"), encoding="utf-8").readline()
        vocal = next((p.strip() for p in caption.split(",") if "vocal" in p), "")
        append_ledger({"folder": folder, "file": match, "title": os.path.splitext(match)[0],
                       "chorus": chorus_of(lyrics), "vocal": vocal, "mood": "", "seed": "", "lyrics": lyrics})
    log(f"ledger started with {len(read_ledger())} experiment songs")


def present(rows, out):
    """The ledger rows whose file is still in the folder."""
    return [r for r in rows if os.path.exists(os.path.join(out, r["folder"], r["file"]))]


def plan(rows, out, folders, count):
    """(folder, attempt) pairs to make, round-robin across the folders furthest from their quota."""
    have = {f: 0 for f in catalogue.BRIEFS}
    for r in present(rows, out):
        have[r["folder"]] = have.get(r["folder"], 0) + 1
    made = {f: sum(1 for r in rows if r["folder"] == f) for f in catalogue.BRIEFS}
    need = {f: max(catalogue.BRIEFS[f][0] - have[f], 0) for f in catalogue.BRIEFS if not folders or f in folders}
    order = []
    for i in range(max(need.values(), default=0)):
        for f, n in need.items():
            if i < n:
                order.append((f, made[f] + i))
    return order[:count] if count else order


def spec(folder, attempt, rows):
    """The song's arrangement, drawn from the folder's brief with a seed of its own."""
    count, dur, bpm_range, style, moods, voicings, shapes = catalogue.BRIEFS[folder]
    seed = catalogue.seed_of(f"song-{folder}-{attempt}")
    rng = random.Random(seed)
    combat = folder in catalogue.COMBAT
    used = [r["vocal"] for r in rows if r["folder"] == folder][-3:]
    pool = [v for v in (COMBAT_VOCALS if combat else VOCALS) if v not in used]
    lead, second, accompaniment = rng.choice(voicings)
    vocal = rng.choice(pool)
    mood = rng.choice(moods)
    production = catalogue.COMBAT_PRODUCTION if combat else catalogue.PRODUCTION
    return {"folder": folder, "seed": seed, "mood": mood, "vocal": vocal, "angle": rng.choice(ANGLES),
            "caption": f"{style}, {vocal}, {lead}, {second}, {accompaniment}, {mood}, {production}",
            "arrangement": f"{lead}, {second}, {accompaniment}",
            "bpm": rng.randint(*bpm_range), "key": rng.choice(catalogue.MINOR if combat else catalogue.KEYS),
            "duration": 75 if folder in CUES else rng.randrange(150, 181, 5)}


def prompt(s, rows, problems=None):
    style = catalogue.BRIEFS[s["folder"]][3]
    shape = ("A short cue of about 75 seconds: [Intro], one verse, one chorus, [Outro]." if s["folder"] in CUES else
             "About 2 to 3 minutes: [Intro], two verses, a chorus sung two or three times, a bridge, "
             "one instrumental break, [Outro].")
    earlier = [f'"{r["title"]}" (chorus: {r["chorus"]})' for r in rows if r["folder"] == s["folder"]]
    text = (f"Situation: the music folder \"{s['folder']}\" ({style}).\n"
            f"Mood: {s['mood']}.\nSinger: {s['vocal']}.\nArrangement: {s['arrangement']}.\n"
            f"Angle for this song: {s['angle']}.\nShape: {shape}\n")
    if earlier:
        text += ("\nSongs already written for this folder. Use a different title, central image and chorus from "
                 "every one of them:\n- " + "\n- ".join(earlier) + "\n")
    if problems:
        text += "\nYour last attempt broke these rules; fix them:\n- " + "\n- ".join(problems) + "\n"
    return text


def ask_claude(text):
    """Claude's reply text, from a headless call that loads no project, user settings, tools or MCP servers."""
    env = {k: v for k, v in os.environ.items() if k != "CLAUDECODE" and not k.startswith("CLAUDE_CODE_")}
    cmd = [shutil.which("claude") or "claude", "-p", "--model", MODEL, "--output-format", "json", "--no-session-persistence",
           "--disable-slash-commands", "--strict-mcp-config", "--tools", "", "--setting-sources", "",
           "--append-system-prompt", RULES]
    done = subprocess.run(cmd, input=text, capture_output=True, text=True, encoding="utf-8", env=env,
                          cwd=os.environ.get("TEMP", DATA), timeout=300)
    try:
        reply = json.loads(done.stdout)
    except json.JSONDecodeError:
        raise RuntimeError(f"claude exited {done.returncode}: {(done.stderr or done.stdout).strip()[:300]}")
    if reply.get("is_error"):
        message = str(reply.get("result", ""))
        if re.search(r"limit|quota|credit", message, re.I):
            raise LimitReached(message[:300])
        raise RuntimeError(message[:300])
    return reply.get("result", "")


def syllables(line):
    n = 0
    for w in re.findall(r"[a-z']+", line.lower()):
        w = w.replace("'", "")
        groups = len(re.findall(r"[aeiouy]+", w))
        if w.endswith("e") and not w.endswith(("le", "ee")) and groups > 1:
            groups -= 1
        n += max(groups, 1)
    return n


def lint(s, lyrics):
    """The rules a song breaks, as render.py checked them for the experiment."""
    problems = []
    caption = s["caption"].lower()
    if "choir" in lyrics.lower():
        problems.append("the word 'choir' is used")
    if "(" in lyrics:
        problems.append("parentheses are used")
    for tag in re.findall(r"^\[([^\]]+)\]", lyrics, flags=re.M):
        if tag.count(" - ") > 1:
            problems.append(f"tag [{tag}] has more than one descriptor")
        for word, need in SOLO_NEEDS.items():
            if word in tag.lower() and need not in caption:
                problems.append(f"[{tag}] names an instrument the arrangement lacks")
    section = ""
    for line in lyrics.splitlines():
        if line.startswith("["):
            section = line
        elif line.strip():
            n = syllables(line)
            if not 3 <= n <= 11 and not section.startswith("[Outro"):
                problems.append(f"{n} syllables, too {'long' if n > 11 else 'short'}: {line}")
            if re.search(r"\b[A-Z]{2,}\b", line):
                problems.append(f"capitals: {line}")
    sections = re.findall(r"^\[(Verse|Chorus|Bridge)", lyrics, flags=re.M)
    if s["folder"] in CUES:
        if sections.count("Verse") != 1 or sections.count("Chorus") != 1:
            problems.append("a short cue takes one verse and one chorus")
    elif sections.count("Verse") != 2 or sections.count("Chorus") not in (2, 3) or "Bridge" not in sections:
        problems.append(f"sections are {sections}; the song needs two verses, two or three choruses and a bridge")
    return problems


def write(s, rows):
    """{"title", "lyrics"} that pass the lint, or None after two tries."""
    problems = None
    for _ in range(2):
        reply = ask_claude(prompt(s, rows, problems))
        match = re.search(r"\{.*\}", reply, re.S)
        try:
            song = json.loads(match.group(0)) if match else None
        except json.JSONDecodeError:
            song = None
        if not song or not song.get("title") or not song.get("lyrics"):
            problems = ["the reply was not the JSON object asked for"]
            continue
        song["lyrics"] = song["lyrics"].strip()
        problems = lint(s, song["lyrics"])
        if not problems:
            return song
    log(f"{s['folder']} lyrics rejected: {'; '.join(problems)}")
    return None


def file_name(title, folder_path):
    base = re.sub(r'[<>:"/\\|?*\x00-\x1f]', "", title).strip(" .") or "Untitled"
    name, n = f"{base}.mp3", 2
    while os.path.exists(os.path.join(folder_path, name)):
        name, n = f"{base} {n}.mp3", n + 1
    return name


def render(dit, s, lyrics):
    """The rendered mp3's path in TMP, or None."""
    from acestep.inference import GenerationParams, GenerationConfig, generate_music

    params = GenerationParams(
        task_type="text2music", thinking=False, caption=s["caption"], lyrics=lyrics, instrumental=False,
        vocal_language="en", bpm=s["bpm"], keyscale=s["key"], duration=s["duration"],
        use_cot_caption=False, use_cot_metas=False, use_cot_language=False,
        inference_steps=50, guidance_scale=7.0, shift=3.0, seed=s["seed"])
    config = GenerationConfig(batch_size=1, audio_format="mp3", use_random_seed=False, seeds=[s["seed"]])
    shutil.rmtree(TMP, ignore_errors=True)
    os.makedirs(TMP)
    try:
        result = generate_music(dit, None, params=params, config=config, save_dir=TMP)
    except Exception as e:  # one bad song must not end the night
        log(f"{s['folder']} render FAILED: {e!r}")
        return None
    if not result.success or not result.audios:
        log(f"{s['folder']} render FAILED: {result.status_message}")
        return None
    return result.audios[0]["path"]


def place(dit, s, song, out, rows):
    """Renders a written song, moves it into its folder under out and adds it to the ledger. True on success."""
    t0 = time.time()
    path = render(dit, s, song["lyrics"])
    if path is None:
        return False
    target = os.path.join(out, s["folder"])
    os.makedirs(target, exist_ok=True)
    name = file_name(song["title"], target)
    shutil.move(path, os.path.join(TMP, name))
    os.replace(os.path.join(TMP, name), os.path.join(target, name))
    row = {"folder": s["folder"], "file": name, "title": song["title"].strip(),
           "chorus": chorus_of(song["lyrics"]), "vocal": s["vocal"], "mood": s["mood"],
           "seed": s["seed"], "lyrics": song["lyrics"]}
    append_ledger(row)
    rows.append(row)
    log(f"{s['folder']}: {name} {s['duration']}s in {time.time() - t0:.0f}s")
    return True


def next_spec(folder, rows, out):
    """The next song's arrangement for a folder, or None when the folder has its quota."""
    have = sum(1 for r in present(rows, out) if r["folder"] == folder)
    if have >= catalogue.BRIEFS[folder][0]:
        return None
    return spec(folder, sum(1 for r in rows if r["folder"] == folder), rows)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--until")
    ap.add_argument("--count", type=int, default=0)
    ap.add_argument("--folders", default="")
    ap.add_argument("--out", default=OWN, help="the music folders to fill; the installed app's by default")
    ap.add_argument("--after-library", action="store_true",
                    help="run.cmd's call: keep a STOP the library generator's run left, rather than clearing it")
    args, _ = ap.parse_known_args()
    if os.path.normcase(os.path.abspath(args.out)) != os.path.normcase(OWN):
        global LEDGER
        LEDGER = os.path.join(args.out, "with-lyrics.tsv")
    elif os.path.isdir(SUNG_OFF) and any(f.endswith(".mp3") for _, _, files in os.walk(SUNG_OFF) for f in files):
        print(f"The sung songs are off, in {SUNG_OFF}; run sung.py on first, or every one would be written again.")
        return

    if not generate.take_lock():
        print("The library generator or another song run holds the lock.")
        return
    try:
        if not args.after_library and os.path.exists(generate.STOP):
            os.remove(generate.STOP)
        deadline = generate.deadline_of(args.until)
        import_experiment(args.out)
        rows = read_ledger()
        todo = plan(rows, args.out, set(filter(None, args.folders.split(","))), args.count)
        log(f"start: {len(todo)} songs to make" + (f", until {deadline:%Y-%m-%d %H:%M}" if deadline else ""))
        if not todo or generate.should_stop(deadline) or not generate.wait_for_game(deadline):
            return

        dit = generate.load_models()
        if dit is None:
            return

        done = 0
        with ThreadPoolExecutor(max_workers=1) as pool:
            specs = [spec(f, a, rows) for f, a in todo]
            pending = pool.submit(write, specs[0], rows)
            for i, s in enumerate(specs):
                try:
                    song = pending.result()
                except LimitReached as e:
                    log(f"Claude usage limit reached; stopping: {e}")
                    break
                except Exception as e:
                    log(f"{s['folder']} lyrics FAILED: {e}")
                    song = None
                if i + 1 < len(specs):
                    pending = pool.submit(write, specs[i + 1], rows)
                if song is None:
                    continue
                if generate.should_stop(deadline) or not generate.wait_for_game(deadline):
                    break
                done += place(dit, s, song, args.out, rows)
            pending.cancel()
        log(f"finished: {done} songs this run")
    finally:
        shutil.rmtree(TMP, ignore_errors=True)
        if os.path.exists(generate.LOCK):
            os.remove(generate.LOCK)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        log("crashed:\n" + traceback.format_exc().rstrip())
        raise
