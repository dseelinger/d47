---
name: kokoro
description: Fix how the Kokoro voice says a word by writing its IPA into the installed app's pronunciations.json. Takes a word or name, optionally with how the maintainer says it. Use when the user invokes /kokoro <word>, /kokoro <word> like <respelling>, or says "Kokoro says X wrong", "fix how Kokoro says X", "add IPA for X".
---

# Kokoro

d47 reads the Commander's corrections from `%LOCALAPPDATA%\Programs\d47\data\pronunciations.json`
(`src/D47.Core/Speech/PronunciationOverrides.cs`). Kokoro re-reads it before every line, so an entry
is heard on the next line with no restart. The format is in `docs/pronunciations.md`.

Invoking this skill is the maintainer's go-ahead to write that one file. Do not ask again.

## The argument

- `/kokoro Achenar`: one word or a multi-word name, written the way it appears in game text.
- `/kokoro Achenar like ACK-en-ar`: the maintainer's own pronunciation, as a respelling. When one is
  given it decides the sounds; do not substitute your own.
- No argument: ask for the word.

## Deciding the sounds

- A common English word or a name with one settled pronunciation: use it.
- A system name or invented word with no settled pronunciation and no respelling in the argument:
  propose one reading as a respelling with the stress in capitals, and ask before writing. One
  question, one line.

## Writing the IPA

Match the shipped dictionary, `data\models\kokoro\phoneme_dict.json`, which is US English in the
symbols Kokoro was trained on. Look up a similar real word in it when unsure. Its conventions:

- The stress mark goes directly before the stressed vowel, not at the start of the syllable:
  `dˈeɪ`, `kˈɑːləni`. Secondary stress is `ˌ`.
- The English r is `ɹ`, never `r`. `ɚ` for unstressed "er" (`fˈɑːðɚ`); `ɜː` for stressed (`bˈɜːd`).
- `ɡ` (U+0261), not the keyboard `g`.
- Diphthongs as two symbols: `eɪ aɪ oʊ aʊ ɔɪ`.
- `ɾ` for the flapped t in "water" (`wˈɔːɾɚ`).
- Never `oː`: Kokoro says it as "aw". Write `ɔɹ` for "or" (`stˈɔɹi`).

Then confirm every symbol is in `data\models\kokoro\tokenizer.json` (`model.vocab`). An entry with
a symbol outside it is skipped by d47 and logged once.

```powershell
$v = (Get-Content "$env:LOCALAPPDATA\Programs\d47\data\models\kokoro\tokenizer.json" -Raw | ConvertFrom-Json -AsHashtable).model.vocab
'kˈɑːləni'.ToCharArray() | Where-Object { -not $v.ContainsKey([string]$_) }
```

No output means every symbol is speakable.

## Writing the entry

The file allows comments and trailing commas, so do not round-trip it through a JSON serialiser
that would drop them. Edit it as text:

- The file is missing: create it as `{ "<word>": "ipa:<ipa>" }`.
- The word already has an entry (keys match ignoring case and repeated spaces): replace that
  value, and say what it was.
- Otherwise: add the entry before the closing brace, with a comma after the previous one.

Write it UTF-8 without a BOM, with the Write or Edit tool, not shell redirection. Then parse it
with `ConvertFrom-Json -AsHashtable` to confirm it is still valid JSON; if it is not, fix it before
reporting.

Entries match whole words only: `story` does not cover `stories`. When the word has an inflected
form the game also uses (a plural, a possessive), add that too.

## The report

One line per entry: the word, the IPA, and a respelling of what it should sound like, so the
maintainer knows what to listen for. Say whether an entry was new or replaced one. Nothing to
restart.
