---
name: singing
description: Turn the sung songs in the maintainer's installed d47 on or off by moving them in or out of its music folders with tools/music/sung.py. Plain /singing means on. Use when the user invokes /singing, /singing on or /singing off, or says "turn the sung songs on", "disable the sung songs", "no singing", "bring the singing back".
---

# Singing

The sung songs are the files `tools/music/with-lyrics.py` wrote into the installed app's music
folders, `%LOCALAPPDATA%\Programs\d47\data\audio\music\<folder>\`, one row each in
`%D47_MUSIC_SOURCE%\with-lyrics.tsv`. They have no setting in the app. Off means the files are moved
to `%D47_MUSIC_SOURCE%\sung-off\<folder>\`; on means they are moved back. Nothing else in the music
folders and nothing in `data\music-pack` is touched.

Invoking this skill is the maintainer's go-ahead to write to the installed `data\` folder. Do not ask
again.

## The argument

- `on`, or no argument: turn them on.
- `off`: turn them off.
- Anything else: ask which one he means.

## The run

One command, `on` or `off`:

```bash
python -I tools/music/sung.py on
```

It prints how many it moved, any that failed, and how many are now on and off.

d47 does not need a restart, running or not: it watches `data\audio` and reloads the library about
three seconds after the folder goes quiet.

A song that is playing is locked and fails to move. When some fail, say how many, and run the same
command once more after a minute; if they still fail, report them and stop.

While the songs are off, `with-lyrics.py` refuses to run, because it would write every one again.

## The report

One or two lines: on or off, how many songs moved, and any that are still in the wrong place.
