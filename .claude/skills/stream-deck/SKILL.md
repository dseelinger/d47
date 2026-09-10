---
name: stream-deck
description: Edit the maintainer's Stream Deck profiles by writing ProfilesV2 JSON directly, using the verified v2 format — page folder name encoding, the manifest shape, and the exact action entries the app accepts. Use when the user invokes /stream-deck, or asks to add, change or remove Stream Deck buttons, fix a profile that imported with no keys, or build a new profile.
---

# Stream Deck profiles

Everything here was verified on this machine against Stream Deck 7.4 and a MK.2. It is written down
because the same problem has been solved from scratch twice, each time through the same failures.

## Do not import. Write the files.

Importing a hand-built `.streamDeckProfile` fails. Four attempts produced, in order: a page-folder
name error, `RT ERROR: profile looped`, and twice a clean import with zero keys and no log line.
Writing `ProfilesV2` directly works and is verifiable.

The method:

1. **Quit Stream Deck.** It holds profiles in memory and writes them back on exit. Any edit made
   while it runs is overwritten, and any read while it runs may be stale.
2. Create the profile in the app first if it does not exist — that gets a correct skeleton, a UUID
   and a registry entry for free. Then quit again.
3. Write the page manifests.
4. Start Stream Deck.
5. **Verify by quitting again and re-reading the files.** The app rewrites what it parsed, so keys
   that survive a quit were genuinely loaded. Keys sitting unread on disk look identical until then.

Delete any leftover `.streamDeckProfile` file afterwards. One left on the Desktop got double-clicked,
re-imported stale, and destroyed a good profile.

## Where things are

| What | Where |
| --- | --- |
| Profiles | `%APPDATA%\Elgato\StreamDeck\ProfilesV2\<UUID>.sdProfile\` |
| **Log — read this first on any failure** | `%APPDATA%\Elgato\StreamDeck\logs\StreamDeck.log` |
| Backups (zip archives) | `%APPDATA%\Elgato\StreamDeck\Backup\*.streamDeckProfilesBackup` |
| Executable | `C:\Program Files\Elgato\StreamDeck\StreamDeck.exe` |
| Active profile | `HKCU\Software\Elgato Systems GmbH\StreamDeck` → `Devices` |

Every import failure named itself in the log. Read it before theorising.

The active profile lives in an opaque Qt `QByteArray` in that registry value. **A profile cannot be
made live programmatically** — say so and ask the maintainer to pick it in the app.

The device: model `20GBA9901`, UUID `@(1)[4057/128/A00SA5022OQ7Y6]`. Keys are addressed
`"column,row"`, columns 0–4, rows 0–2.

## Page folder names are derived from the page UUID

Not random. A page whose folder name does not match its UUID gives
`ERROR I/O: ios_base::failbit set` and `no pages in umbrellas`. base32hex with `U` removed:

```python
ALPHA = '0123456789ABCDEFGHIJKLMNOPQRSTVW'
def folder_for(page_uuid):
    v = int.from_bytes(uuid.UUID(page_uuid).bytes, 'big') << 2
    return ''.join(ALPHA[(v >> (5 * i)) & 31] for i in range(26))[::-1] + 'Z'
```

Verified 10/10 against an app-authored profile.

## Top-level manifest.json

```json
{"Device": {"Model": "20GBA9901", "UUID": "@(1)[4057/128/A00SA5022OQ7Y6]"},
 "Name": "Directive 47 Development",
 "Pages": {"Current": "<visible-1>", "Default": "<a third UUID, NOT in Pages>",
           "Pages": ["<visible-1>", "<visible-2>"]},
 "Version": "2.0"}
```

**`Default` must be a page that is not in `Pages`.** Pointing it at a visible page gives
`RT ERROR: profile looped`: the profile imports, and every action is silently discarded.

Folder count = visible pages + 1 for `Default` + 1 per `profile.openchild` child profile.

## Page manifest.json

Keys `Controllers`, `Icon`, `Name`. `Controllers` is a list of one `{"Actions": {...}}`, keyed by
coordinate.

## Action entries

Copied from entries Stream Deck wrote itself. Match this shape exactly.

```json
{"ActionID": "<fresh uuid4>", "LinkedTitle": false, "Name": "Open",
 "Plugin": {"Name": "Open", "UUID": "com.elgato.streamdeck.system.open", "Version": "1.0"},
 "Settings": {"path": "C:\dev\d47\tools\deck\triage.cmd"},
 "State": 0, "States": [{"Title": "Triage"}], "UUID": "com.elgato.streamdeck.system.open"}
```

Types in use:

| Purpose | `UUID` | `Plugin.UUID` | `Settings` |
| --- | --- | --- | --- |
| Run a file | `com.elgato.streamdeck.system.open` | same | `{"path": "..."}` |
| Type into the focused window | `com.elgato.streamdeck.system.text` | same | `{"Hotkey": {"KeyModifiers": 0, "QTKeyCode": 33554431, "VKeyCode": -1}, "isSendingEnter": true, "isTypingMode": false, "pastedText": "/desktop"}` |
| Switch page | `com.elgato.streamdeck.page.goto` | **`com.elgato.streamdeck.page`** | `{"PageIndex": 2}` |
| Open a child profile | `com.elgato.streamdeck.profile.openchild` | | `{"ProfileUUID": "<uuid>"}` |
| Back to parent | `com.elgato.streamdeck.profile.backtoparent` | | `{}` |
| Open a URL | `com.elgato.streamdeck.system.website` | | `{"browser": "", "openInBrowser": true, "path": "https://..."}` |

Three things that cost attempts:

- **`Plugin.UUID` is the plugin id, not the action id.** They differ for `page.goto`.
- **Keep `States` minimal** — `[{"Title": "..."}]`, or `[{}]`. The font block (`FontFamily`,
  `FontSize`, `ShowTitle`, `TitleColor`, `OutlineThickness`, `TitleAlignment` and the rest) appears
  only on keys carrying a custom image.
- **Omit `Image` entirely.** Do not set it to `""`.
- `LinkedTitle: false` with a `Title` gives a custom label; `true` takes the action's own name.

## The d47 profile

`Directive 47 Development`, profile UUID `2CFD100A-59FE-4ADF-82B0-A12855B1A0B2`.

Page 1 runs the launchers in `tools/deck/*.cmd`. Each opens a `claude` session in the repo with a
name, model, effort and an initial slash command; the **Desktop** key types `/desktop` into the
focused terminal to hand that session to the desktop app. Page 2 holds the release keys.

Adding a button is two edits: a `.cmd` in `tools/deck/`, and one action entry in the page manifest.
