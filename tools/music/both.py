"""Alternates the two generators: one library track, then one sung song for the folder furthest from its quota.

  both.py                         run until both are done or stop.cmd is used
  both.py --until 05:00           also stop at that local wall-clock time
  both.py --count 1               stop after that many library tracks
  both.py --folders a,b           only those folders
  both.py --out DIR               put the sung songs under DIR instead of the installed app's music folders

The library track goes to tracks\\ and generated.txt for the weekly publish, as generate.py does. The sung song
is drawn from its folder's brief with its own arrangement and goes to the own-music folder, as with-lyrics.py
does; its lyrics are written while the library track renders. Sung songs follow with-lyrics.py's plan, not the
library queue, so every folder short of its quota gets them. Once the catalogue is done the run carries on with
sung songs only. A usage or rate limit from Claude turns the sung songs off for the rest of the run.
"""

import argparse
import importlib.util
import os
import shutil
import sys
import traceback
from concurrent.futures import ThreadPoolExecutor

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import generate  # noqa: E402

library_log = generate.log
_spec = importlib.util.spec_from_file_location("with_lyrics", os.path.join(HERE, "with-lyrics.py"))
lyrics = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(lyrics)
generate.log = library_log  # library tracks, stops and waits log to generate.log; sung songs to with-lyrics.log


def next_song(rows, out, folders, pool):
    """A future for the next written song in the folder furthest from its quota, or None when all are done."""
    order = lyrics.plan(rows, out, folders, 1)
    return song_for(order[0][0], rows, out, pool) if order else None


def song_for(folder, rows, out, pool):
    """A future for the folder's next written song, or None when its sung songs are done."""
    s = lyrics.next_spec(folder, rows, out)
    return (s, pool.submit(lyrics.write, s, rows)) if s else None


def finish_song(dit, pending, rows, out):
    """Places a written song. False when Claude's usage limit was reached."""
    s, future = pending
    try:
        song = future.result()
    except lyrics.LimitReached as e:
        lyrics.log(f"Claude usage limit reached; library tracks only from here: {e}")
        return False
    except Exception as e:
        lyrics.log(f"{s['folder']} lyrics FAILED: {e}")
        return True
    if song is not None:
        lyrics.place(dit, s, song, out, rows)
    return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--until")
    ap.add_argument("--count", type=int, default=0)
    ap.add_argument("--folders", default="")
    ap.add_argument("--out", default=lyrics.OWN)
    args = ap.parse_args()
    if os.path.normcase(os.path.abspath(args.out)) != os.path.normcase(lyrics.OWN):
        lyrics.LEDGER = os.path.join(args.out, "with-lyrics.tsv")

    if not generate.take_lock():
        print("Another generator holds the lock.")
        return
    try:
        if os.path.exists(generate.STOP):
            os.remove(generate.STOP)
        deadline = generate.deadline_of(args.until)
        folders = set(filter(None, args.folders.split(",")))
        added = generate.read_generated()
        lyrics.import_experiment(args.out)
        rows = lyrics.read_ledger()
        todo = list(generate.queue(0, folders))
        if args.count:
            todo = todo[:args.count]
        sung = len(lyrics.plan(rows, args.out, folders, 0))
        generate.log(f"both: {len(todo)} library tracks and {sung} sung songs to make"
                     + (f", until {deadline:%Y-%m-%d %H:%M}" if deadline else ""))
        if not (todo or sung) or generate.should_stop(deadline) or not generate.wait_for_game(deadline):
            return

        dit = generate.load_models()
        if dit is None:
            return

        def go():
            return not generate.should_stop(deadline) and generate.wait_for_game(deadline)

        singing, stopped = True, False
        tracks = songs = 0
        with ThreadPoolExecutor(max_workers=1) as pool:
            for row in todo:
                if not go():
                    stopped = True
                    break
                pending = next_song(rows, args.out, folders, pool) if singing else None
                tracks += generate.make(dit, row, added)
                if pending is None:
                    continue
                if not go():
                    stopped = True
                    break
                before = len(rows)
                singing = finish_song(dit, pending, rows, args.out)
                songs += len(rows) > before

            while singing and not stopped and not args.count:
                order = lyrics.plan(rows, args.out, folders, 1)
                if not order or not go():
                    break
                before = len(rows)
                singing = finish_song(dit, song_for(order[0][0], rows, args.out, pool), rows, args.out)
                if len(rows) == before:
                    # The plan would offer the same folder again; end rather than loop on it.
                    lyrics.log(f"{order[0][0]}: no song made; ending the sung-only part of the run")
                    break
                songs += 1
        generate.log(f"both finished: {tracks} library tracks, {songs} sung songs this run")
    finally:
        shutil.rmtree(generate.TMP, ignore_errors=True)
        shutil.rmtree(lyrics.TMP, ignore_errors=True)
        if os.path.exists(generate.LOCK):
            os.remove(generate.LOCK)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        generate.log("crashed:\n" + traceback.format_exc().rstrip())
        raise
