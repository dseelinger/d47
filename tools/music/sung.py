"""Turn a theme of sung songs on or off by moving its songs in or out of the installed app's music folders.

  sung.py THEME on     move the theme's songs into data\\audio\\music
  sung.py THEME off    move them to sung-off\\THEME\\ in the music source folder
  sung.py all on|off   every theme
  sung.py on|off       the general theme
  sung.py              list the themes, on or off, and where their songs are

The songs are the rows of with-lyrics.tsv; a row's theme column names its theme, general when empty.
sung-on.txt in the music source folder lists the themes that are on; when it does not exist, only general is on.
with-lyrics.py writes a new song where its theme is, so a theme that is off fills sung-off\\THEME\\.
Nothing else in the music folders is touched. A running d47 picks up the change from its audio folder watch, with
no restart. A song that is playing is locked and fails to move; run the same command again once it ends.
"""

import csv
import os
import shutil
import sys

DATA = os.environ.get("D47_MUSIC_SOURCE")
if not DATA:
    sys.exit("D47_MUSIC_SOURCE is not set.")
LEDGER = os.path.join(DATA, "with-lyrics.tsv")
STATE = os.path.join(DATA, "sung-on.txt")
OWN = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", "d47", "data", "audio", "music")
THEMES = ("general", "christmas", "love", "christian")


def theme_of(row):
    return row.get("theme") or "general"


def off_root(theme):
    return os.path.join(DATA, "sung-off", theme)


def themes_on():
    if not os.path.exists(STATE):
        return {"general"}
    with open(STATE, encoding="utf-8") as f:
        return {line.strip() for line in f if line.strip() in THEMES}


def set_on(theme, on):
    now = themes_on() | {theme} if on else themes_on() - {theme}
    with open(STATE, "w", encoding="utf-8") as f:
        f.writelines(f"{t}\n" for t in THEMES if t in now)


def songs(theme):
    if not os.path.exists(LEDGER):
        return
    with open(LEDGER, encoding="utf-8") as f:
        for row in csv.DictReader(f, delimiter="\t"):
            if theme_of(row) == theme:
                yield row["folder"], row["file"]


def count(root, theme):
    return sum(os.path.exists(os.path.join(root, folder, file)) for folder, file in songs(theme))


def move(theme, src_root, dst_root):
    moved, failed = 0, []
    for folder, file in songs(theme):
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
    args = [a.lower() for a in sys.argv[1:]]
    if len(args) == 1 and args[0] in ("on", "off"):
        args = ["general"] + args
    if args and (len(args) != 2 or args[0] not in THEMES + ("all",) or args[1] not in ("on", "off")):
        sys.exit(__doc__ + f"\nThemes: {', '.join(THEMES)}")
    failed = []
    if args:
        on = args[1] == "on"
        for theme in THEMES if args[0] == "all" else (args[0],):
            set_on(theme, on)
            moved, theme_failed = move(theme, off_root(theme), OWN) if on else move(theme, OWN, off_root(theme))
            failed += theme_failed
            print(f"{theme}: {moved} moved, {len(theme_failed)} failed")
        for line in failed:
            print(line)
    state = themes_on()
    for theme in THEMES:
        print(f"{theme}: {'on' if theme in state else 'off'}, {count(OWN, theme)} songs in {OWN}, "
              f"{count(off_root(theme), theme)} in {off_root(theme)}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
