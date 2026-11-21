---
name: singing
description: Turn a theme of sung songs in the maintainer's installed d47 on or off — general, christmas, love, christian, or all — by moving its songs in or out of the music folders with tools/music/sung.py. Plain /singing means general on. Use when the user invokes /singing, /singing on|off, /singing <theme> on|off, or says "turn the sung songs on", "disable the sung songs", "no singing", "bring the singing back", "turn the Christmas songs on", "take the love songs out".
---

# Singing

The sung songs are the files `tools/music/with-lyrics.py` wrote, one row each in
`%D47_MUSIC_SOURCE%\with-lyrics.tsv`. Each row has a theme: `general`, `christmas`, `love` or
`christian`. A theme that is on has its songs in the installed app's music folders,
`%LOCALAPPDATA%\Programs\d47\data\audio\music\<folder>\`; a theme that is off has them in
`%D47_MUSIC_SOURCE%\sung-off\<theme>\<folder>\`. `%D47_MUSIC_SOURCE%\sung-on.txt` lists the themes
that are on. The themes have no setting in the app. Nothing else in the music folders and nothing in
`data\music-pack` is touched.

Invoking this skill is the maintainer's go-ahead to write to the installed `data\` folder. Do not ask
again.

## The argument

- No argument, or `on`: `general on`.
- `off`, or "no singing": `all off`.
- `<theme> on` or `<theme> off`, where the theme is `general`, `christmas`, `love`, `christian` or
  `all`. Take the theme from his words: "the Christmas songs" is `christmas`, "the hymns" or "the
  Christian songs" is `christian`, "the Valentine's songs" is `love`.
- A theme that is turned on does not turn another off. "Only Christmas" is `all off`, then
  `christmas on`.
- Anything else: ask which theme and which way.

## The run

```bash
python -I tools/music/sung.py christmas on
```

It prints how many songs of each theme it moved, any that failed, and then every theme's state with
how many songs are in the music folders and in sung-off.

d47 does not need a restart, running or not: it watches `data\audio` and reloads the library about
three seconds after the folder goes quiet.

A song that is playing is locked and fails to move. When some fail, say how many, and run the same
command once more after a minute; if they still fail, report them and stop.

`with-lyrics.py` counts a song in sung-off as made, and writes a new song where its theme is, so a
theme can be on or off while its set is being generated.

## The report

One line per theme that changed: on or off and how many songs moved, plus any still in the wrong
place.
