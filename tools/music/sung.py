"""Turn the sung songs on or off by moving them in or out of the installed app's music folders.

  sung.py on        move them into data\\audio\\music
  sung.py off       move them to sung-off\\ in the music source folder
  sung.py           say where they are

The songs are the rows of with-lyrics.tsv. Nothing else in the music folders is touched. A running d47 picks up
the change from its audio folder watch, with no restart. A song that is playing is locked and fails to move;
run the same command again once it ends.
"""

import csv
import os
import shutil
import sys

DATA = os.environ.get("D47_MUSIC_SOURCE")
if not DATA:
    sys.exit("D47_MUSIC_SOURCE is not set.")
LEDGER = os.path.join(DATA, "with-lyrics.tsv")
OFF = os.path.join(DATA, "sung-off")
OWN = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", "d47", "data", "audio", "music")


def songs():
    with open(LEDGER, encoding="utf-8") as f:
        for row in csv.DictReader(f, delimiter="\t"):
            yield row["folder"], row["file"]


def count(root):
    return sum(os.path.exists(os.path.join(root, folder, file)) for folder, file in songs())


def move(src_root, dst_root):
    moved, failed = 0, []
    for folder, file in songs():
        src = os.path.join(src_root, folder, file)
        dst = os.path.join(dst_root, folder, file)
        if not os.path.exists(src):
            continue
        if os.path.exists(dst):
            failed.append(f"{dst} already exists")
            continue
        try:
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.move(src, dst)
            moved += 1
        except OSError as e:
            failed.append(f"{src}: {e.strerror}")
    return moved, failed


def main():
    mode = sys.argv[1].lower() if len(sys.argv) > 1 else ""
    if mode not in ("", "on", "off"):
        sys.exit(__doc__)
    if mode:
        moved, failed = move(OFF, OWN) if mode == "on" else move(OWN, OFF)
        print(f"{moved} moved, {len(failed)} failed")
        for line in failed:
            print(line)
    print(f"{count(OWN)} sung songs on (in {OWN}), {count(OFF)} off (in {OFF})")
    return 1 if mode and failed else 0


if __name__ == "__main__":
    sys.exit(main())
