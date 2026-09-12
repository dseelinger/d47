---
name: stream-deck
description: Edit the maintainer's Stream Deck profiles by writing ProfilesV2 JSON directly, using the verified v2 format — the Keypad controller type, page folder name encoding, key images and the action entries the app accepts. Use when the user invokes /stream-deck, or asks to add, change or remove Stream Deck buttons, fix a key that does the wrong thing, fix a profile that shows no keys, or build a new profile.
---

# Stream Deck profiles

Verified on this machine against Stream Deck 7.4 and a MK.2. Written down because the same problem
has been solved from scratch three times, each time through the same failures.

## Changing the d47 profile

It is generated, not hand-edited. Two scripts in `tools/deck/`, with Stream Deck **closed**:

```bash
python tools/deck/apply_profile.py --tiles
```

`gen_tiles.py` draws the key images; `apply_profile.py` writes the page manifests and then checks
every launcher path and every image file before it finishes. Start Stream Deck afterwards.

Adding a button is three edits: a `.cmd` in `tools/deck/`, an entry in `KEYS` in `gen_tiles.py`, and
a line in `PAGE_1` or `PAGE_2` in `apply_profile.py`. Do not edit the profile JSON by hand — the
next run of the script overwrites it.

## The one that costs the most time

**Every controller needs `"Type": "Keypad"`.** Without it the profile loads, the pages load, the log
says nothing, and every action is silently discarded — an empty grid. This single missing field
caused four straight "imported fine, no buttons" failures, and it is invisible unless you diff
against a working profile.

```json
{"Controllers": [{"Type": "Keypad", "Actions": {"0,0": { ... }}}], "Icon": "", "Name": "Page 1"}
```

## Diff against a working profile first

Do this before theorising. `ProfilesV2` holds profiles the app itself authored; open one and compare
field by field. Both the `Type` bug and the key-image path bug were found in a minute this way,
after hours of guessing. If the maintainer offers to export a profile, take it.

## Do not import. Write the files.

Importing a hand-built `.streamDeckProfile` fails. Write `ProfilesV2` directly:

1. **Quit Stream Deck.** It holds profiles in memory; an edit made while it runs is lost.
2. Create the profile in the app first if it does not exist — that gets a correct skeleton, a UUID
   and a registry entry for free. Then quit again.
3. Write the page manifests and key images.
4. Start Stream Deck.

Delete any leftover `.streamDeckProfile` afterwards. One left on the Desktop got double-clicked,
re-imported stale, and destroyed a good profile.

### Verification: only the maintainer's eyes count

The app **does not rewrite a profile on exit** unless it was edited in the UI. So neither of these
proves the profile was parsed, and both were believed here and were wrong:

- keys still present in the file after a quit — an unread file survives untouched too
- the file still being pretty-printed rather than the app's compact style

Ask the maintainer to look at the deck. There is no file-level substitute. To show a design without
making them squint at the hardware, paste the tiles onto a dark contact sheet and send that.

## Where things are

| What | Where |
| --- | --- |
| Profiles | `%APPDATA%\Elgato\StreamDeck\ProfilesV2\<UUID>.sdProfile\` |
| Log — read on any failure | `%APPDATA%\Elgato\StreamDeck\logs\StreamDeck.log` |
| Backups (zips) | `%APPDATA%\Elgato\StreamDeck\Backup\*.streamDeckProfilesBackup` |
| Executable | `C:\Program Files\Elgato\StreamDeck\StreamDeck.exe` |
| Active profile | `HKCU\Software\Elgato Systems GmbH\StreamDeck` -> `Devices` |

The log names load-time failures (`ios_base::failbit`, `no pages in umbrellas`, `profile looped`)
but is **silent about discarded actions** — the `Type` bug produces no line at all.

The active profile is an opaque Qt `QByteArray` in that registry value. **A profile cannot be made
live programmatically** — ask the maintainer to pick it from the dropdown.

Device: model `20GBA9901`, UUID `@(1)[4057/128/A00SA5022OQ7Y6]`. Keys are `"column,row"`,
columns 0-4, rows 0-2.

The built-in action ids are readable from the binary, which beats guessing:

```bash
grep -ao "com\.elgato\.streamdeck\.[a-z0-9.]*" "/c/Program Files/Elgato/StreamDeck/StreamDeck.exe" | sort -u
```

Stream Deck 7 has **no `multiactionswitch`**. The only two-state key action is
`com.elgato.streamdeck.system.hotkeyswitch`, which sends keystrokes, not text.

## Page folder names are derived from the page UUID

Not random. A mismatch gives `ERROR I/O: ios_base::failbit set` and `no pages in umbrellas`.
base32hex with `U` removed, verified 10/10:

```python
ALPHA = '0123456789ABCDEFGHIJKLMNOPQRSTVW'
def folder_for(page_uuid):
    v = int.from_bytes(uuid.UUID(page_uuid).bytes, 'big') << 2
    return ''.join(ALPHA[(v >> (5 * i)) & 31] for i in range(26))[::-1] + 'Z'
```

## Top-level manifest.json

```json
{"Device": {"Model": "20GBA9901", "UUID": "@(1)[4057/128/A00SA5022OQ7Y6]"},
 "Name": "Directive 47 Development",
 "Pages": {"Current": "<visible-1>", "Default": "<a third UUID, NOT in Pages>",
           "Pages": ["<visible-1>", "<visible-2>"]},
 "Version": "2.0"}
```

**`Default` must be a page not in `Pages`.** Pointing it at a visible page gives
`RT ERROR: profile looped` and every action is discarded.

Folder count = visible pages + 1 for `Default` + 1 per `profile.openchild` child.

## Action entries

```json
{"ActionID": "<fresh uuid4>", "LinkedTitle": false, "Name": "Open",
 "Plugin": {"Name": "Open", "UUID": "com.elgato.streamdeck.system.open", "Version": "1.0"},
 "Settings": {"path": "C:\\dev\\d47\\tools\\deck\\triage.cmd"},
 "State": 0, "States": [{ ... }], "UUID": "com.elgato.streamdeck.system.open"}
```

| Purpose | `UUID` | `Plugin.UUID` | `Settings` |
| --- | --- | --- | --- |
| Run a file | `com.elgato.streamdeck.system.open` | same | `{"path": "..."}` |
| Type into the focused window | `com.elgato.streamdeck.system.text` | same | `{"Hotkey": {"KeyModifiers": 0, "QTKeyCode": 33554431, "VKeyCode": -1}, "isSendingEnter": true, "isTypingMode": false, "pastedText": "/desktop"}` |
| Switch page | `com.elgato.streamdeck.page.goto` | **`com.elgato.streamdeck.page`** | `{"PageIndex": 2}` |
| Open a child profile | `com.elgato.streamdeck.profile.openchild` | | `{"ProfileUUID": "<uuid>"}` |
| Back to parent | `com.elgato.streamdeck.profile.backtoparent` | | `{}` |
| Open a URL | `com.elgato.streamdeck.system.website` | | `{"browser": "", "openInBrowser": true, "path": "https://..."}` |

`Plugin.UUID` is the plugin id, not the action id — they differ for `page.goto`.

## Key appearance

`States[n]` carries the whole style. A styled key needs the full block; a bare `{"Title": "x"}`
renders the plugin's stock icon with a small title under it.

```json
{"FontFamily": "Arial", "FontSize": 12, "FontStyle": "Bold", "FontUnderline": false,
 "Image": "Images/triage.png", "OutlineThickness": 2, "ShowTitle": false,
 "Title": "", "TitleAlignment": "bottom", "TitleColor": "#ffffff"}
```

**`Image` is relative to the page folder** — `Profiles/<page folder>/Images/x.png`, not the profile
root. The root `Images/` folder exists but is empty and is not where the app looks. Each page needs
its own copy of the files it references.

**Bake the label into the image and set `ShowTitle: false`.** Stream Deck's own title rendering
clips anything past about ten characters at `FontSize` 11, wraps nothing, and cannot be tracked or
positioned. `d47 Capture` does the same, which is why it reads better than titles over flat colour.

Tiles are 144x144: near-black ground, an 11px accent bar along the top naming the category, a large
glyph in the accent colour, and the label across the bottom. Draw at 4x and downsample with LANCZOS
or thin strokes crawl. Colour-coding: violet opens a Claude session, cyan types into the focused
session, green runs a script, blue runs the app, amber is release.

## The d47 profile

`Directive 47 Development`, UUID `2CFD100A-59FE-4ADF-82B0-A12855B1A0B2`.

One page, 12 keys. Row 0 opens a session, row 1 types into the session that has focus, row 2 runs
the app and cuts releases.

| | 0 | 1 | 2 | 3 | 4 |
| --- | --- | --- | --- | --- | --- |
| **0** | Triage | Coord | Architect | Issue | Review |
| **1** | Desktop | Push | Wrap up | | |
| **2** | Test drive | Restart | | Patch | Minor |

The launchers open `claude` in the repo with a name, model, effort and an opening slash command.
**Issue** is the one that asks a question first: it reads an issue number from the terminal and
takes that issue's model and effort from `.claude/triage-state.json`, which `/triage` writes. A
number triage does not name starts on `sonnet`/`medium` and says so. The session cannot correct
itself afterwards — the desktop app refuses `set_session_model` and `set_session_effort` for the
session making the call — so the launcher is the only place this can be set.
**Desktop** types `/desktop` into the focused terminal to hand that session to the desktop app, so
work starts on a keypress and continues by clicking. **Review** reviews against `origin/main`: the
issue worker commits and does not push, so the working diff is empty by the time anyone reviews.
The release keys ask Y/N in the terminal first, and `release.ps1` follows the run to the end on its
own — a separate watch key duplicates it. **Major is deliberately not on the deck**: cut one with
`tools\release.ps1 -Major`.

The profile keeps its second page in the manifest, empty, with nothing navigating to it.

A neural-voice key was tried and dropped. If one is asked for again: the state lives in the session
and nowhere on disk, and sessions run in parallel with different settings, so **a key cannot detect
whether it is on** and a toggle is impossible. Two keys or none. Do not fake it with a flag file,
which would make one session's voice global.

## When a key misbehaves

Separate the two layers before debugging:

- Wrong thing happens, or nothing happens, and the terminal opens -> the fault is in
  `tools/deck/*.cmd`, not the profile.
- No terminal at all, or the wrong key art -> the fault is the profile; re-run `apply_profile.py`.

## Writing the script

The Bash heredoc on this machine **collapses a doubled backslash to a single one**, so a Windows
path written as a doubled escape arrives with real tab and formfeed characters in it. Build paths
with `os.path.join` and `os.sep` and use no literal backslash anywhere in the script — or write the
file with the Write tool, which does not mangle. Long heredocs also fail outright with
`unexpected EOF`. Verify every `Settings.path` with `os.path.isfile` and every `Image` against the
page folder before restarting; both classes of breakage are silent.
