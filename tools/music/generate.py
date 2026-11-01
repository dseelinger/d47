"""Generate the tracks in catalogue.tsv that are not on disk yet, round-robin across folders.

  generate.py                     run until the catalogue is done or stop.cmd is used
  generate.py --until 05:00       also stop at that local wall-clock time
  generate.py --per-folder 3      only fill each folder up to that many tracks (the trial batch)
  generate.py --folders a,b       only those folders
  generate.py --smoke DIR         one 10-second test track into DIR, to check an ACE-Step update

Writes the folders it added tracks to, one per line, to generated.txt in the data folder.
Stops after the current track when STOP exists. Waits while Elite Dangerous is running.
Only one copy runs at a time; a second start exits.
"""

import argparse
import csv
import os
import shutil
import subprocess
import sys
import time
from datetime import datetime, timedelta

DATA = os.environ.get("D47_MUSIC_SOURCE")
ACE = os.environ.get("D47_ACE_STEP")
for _name, _value in (("D47_MUSIC_SOURCE", DATA), ("D47_ACE_STEP", ACE)):
    if not _value:
        sys.exit(f"{_name} is not set.")
OUT = os.path.join(DATA, "tracks")
TMP = os.path.join(DATA, "_tmp")
STOP = os.path.join(DATA, "STOP")
LOCK = os.path.join(DATA, "generate.lock")
LOG = os.path.join(DATA, "generate.log")
GENERATED = os.path.join(DATA, "generated.txt")
GAME = "EliteDangerous64.exe"


def log(msg):
    line = f"{datetime.now():%Y-%m-%d %H:%M:%S} {msg}"
    print(line, flush=True)
    with open(LOG, "a", encoding="utf-8") as f:
        f.write(line + "\n")


def pid_alive(pid):
    out = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/NH"], capture_output=True, text=True).stdout
    return str(pid) in out


def game_running():
    out = subprocess.run(["tasklist", "/FI", f"IMAGENAME eq {GAME}", "/NH"], capture_output=True, text=True).stdout
    return GAME.lower() in out.lower()


def take_lock():
    if os.path.exists(LOCK):
        try:
            pid = int(open(LOCK).read().strip())
        except ValueError:
            pid = 0
        if pid and pid_alive(pid):
            return False
    with open(LOCK, "w") as f:
        f.write(str(os.getpid()))
    return True


def deadline_of(until):
    if not until:
        return None
    h, m = map(int, until.split(":"))
    now = datetime.now()
    d = now.replace(hour=h, minute=m, second=0, microsecond=0)
    return d if d > now else d + timedelta(days=1)


def write_generated(folders):
    with open(GENERATED, "w", encoding="utf-8") as f:
        f.write("".join(name + "\n" for name in sorted(folders)))


def path_of(row):
    return os.path.join(OUT, row["folder"], f"{row['id']}.mp3")


def queue(per_folder, folders):
    with open(os.path.join(DATA, "catalogue.tsv"), encoding="utf-8") as f:
        rows = list(csv.DictReader(f, delimiter="\t"))
    by_folder = {}
    for r in rows:
        if per_folder and int(r["n"]) > per_folder:
            continue
        if folders and r["folder"] not in folders:
            continue
        by_folder.setdefault(r["folder"], []).append(r)
    # Round-robin: track 1 of every folder, then track 2 of every folder, and so on.
    longest = max((len(v) for v in by_folder.values()), default=0)
    for i in range(longest):
        for tracks in by_folder.values():
            if i < len(tracks) and not os.path.exists(path_of(tracks[i])):
                yield tracks[i]


def should_stop(deadline):
    if os.path.exists(STOP):
        log("STOP file found")
        return True
    if deadline and datetime.now() >= deadline:
        log(f"deadline {deadline:%H:%M} reached")
        return True
    return False


def wait_for_game(deadline):
    """Waits while the game runs. False if told to stop meanwhile."""
    announced = False
    while game_running():
        if not announced:
            log("Elite Dangerous is running; waiting")
            announced = True
        if should_stop(deadline):
            return False
        time.sleep(60)
    if announced:
        log("Elite Dangerous closed; resuming")
    return True


def load_models():
    """Returns (dit, llm), or (None, None) after logging the failure."""
    sys.path.insert(0, ACE)
    os.chdir(ACE)
    from acestep.handler import AceStepHandler
    from acestep.llm_inference import LLMHandler

    dit = AceStepHandler()
    msg, ok = dit.initialize_service(project_root=ACE, config_path="acestep-v15-xl-sft", device="auto", offload_to_cpu=True)
    if not ok:
        log(f"DiT failed: {msg}")
        return None, None
    llm = LLMHandler()
    msg, ok = llm.initialize(checkpoint_dir=os.path.join(ACE, "checkpoints"), lm_model_path="acestep-5Hz-lm-1.7B",
                             backend="pt", device="auto", offload_to_cpu=True, dtype=None)
    if not ok:
        log(f"LM failed: {msg}")
        return None, None
    return dit, llm


def settings(prompt, lyrics, bpm, key, duration, seed):
    from acestep.inference import GenerationParams, GenerationConfig

    params = GenerationParams(
        task_type="text2music", thinking=True, caption=prompt, lyrics=lyrics.replace("\\n", "\n"),
        instrumental=True, bpm=int(bpm), keyscale=key, duration=int(duration),
        use_cot_caption=False, use_cot_metas=False, use_cot_language=False,
        inference_steps=50, guidance_scale=7.0, shift=3.0, seed=int(seed))
    config = GenerationConfig(batch_size=1, audio_format="mp3", use_random_seed=False, seeds=[int(seed)])
    return params, config


def smoke(out_dir):
    """Generates one 10-second track into out_dir with the run's models and settings. Exit code 0 on success."""
    from acestep.inference import generate_music

    dit, llm = load_models()
    if dit is None:
        return 1
    params, config = settings("calm ambient pads, slow", "[Instrumental]", 70, "C minor", 10, 1)
    try:
        result = generate_music(dit, llm, params=params, config=config, save_dir=out_dir)
    except Exception as e:
        log(f"test track FAILED: {e}")
        return 1
    if not result.success or not result.audios or not os.path.getsize(result.audios[0]["path"]):
        log(f"test track FAILED: {result.status_message}")
        return 1
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--until")
    ap.add_argument("--per-folder", type=int, default=0)
    ap.add_argument("--folders", default="", help="comma-separated folders to limit the run to")
    ap.add_argument("--smoke", metavar="DIR", help="generate one 10-second test track into DIR and exit")
    args = ap.parse_args()

    if args.smoke:
        sys.exit(smoke(args.smoke))
    if not take_lock():
        print("Already running.")
        return
    try:
        if os.path.exists(STOP):
            os.remove(STOP)
        added = set()
        write_generated(added)
        deadline = deadline_of(args.until)
        todo = list(queue(args.per_folder, set(filter(None, args.folders.split(",")))))
        log(f"start: {len(todo)} tracks to do" + (f", until {deadline:%Y-%m-%d %H:%M}" if deadline else ""))
        if not todo or not wait_for_game(deadline) or should_stop(deadline):
            return

        dit, llm = load_models()
        if dit is None:
            return

        done = 0
        for row in todo:
            if should_stop(deadline) or not wait_for_game(deadline):
                break
            shutil.rmtree(TMP, ignore_errors=True)
            os.makedirs(TMP)
            params = GenerationParams(
                task_type="text2music", thinking=True, caption=row["prompt"], lyrics=row["lyrics"].replace("\\n", "\n"),
                instrumental=True, bpm=int(row["bpm"]), keyscale=row["key"], duration=int(row["duration"]),
                use_cot_caption=False, use_cot_metas=False, use_cot_language=False,
                inference_steps=50, guidance_scale=7.0, shift=3.0, seed=int(row["seed"]))
            config = GenerationConfig(batch_size=1, audio_format="mp3", use_random_seed=False, seeds=[int(row["seed"])])
            t0 = time.time()
            try:
                result = generate_music(dit, llm, params=params, config=config, save_dir=TMP)
            except Exception as e:  # one bad track must not end the night
                log(f"{row['id']} FAILED: {e}")
                continue
            if not result.success or not result.audios:
                log(f"{row['id']} FAILED: {result.status_message}")
                continue
            dest = path_of(row)
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            os.replace(result.audios[0]["path"], dest)
            done += 1
            added.add(row["folder"])
            write_generated(added)
            log(f"{row['id']} {row['duration']}s in {time.time() - t0:.0f}s")
        log(f"finished: {done} generated this run")
    finally:
        shutil.rmtree(TMP, ignore_errors=True)
        if os.path.exists(LOCK):
            os.remove(LOCK)


if __name__ == "__main__":
    main()
