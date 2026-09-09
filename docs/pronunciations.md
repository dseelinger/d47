---
title: Correcting a pronunciation
group: General help
nav_order: 7
---

# Correcting a pronunciation

The local voice works a word out from its spelling when nobody has written the pronunciation
down for it. Most of the time that is right. Sometimes it is not — Elite has four hundred
billion system names, Frontier keep adding to them, and the community argues about half the
ones that already exist.

When you hear one you disagree with, you can fix it yourself. It takes a text file and no
restart.

## The file

Make a file called `pronunciations.json` in the `data` folder beside `d47.exe`. The
**Diagnostics** page says exactly where that is, and whether d47 has found one yet.

```json
{
  "Shinrarta Dezhra": "shin rar tah dezh rah",
  "Deciat": "dessy at",
  "Dezhra": "ipa:ˈdɛʒɹə"
}
```

Save it. Say the word again. That is the whole loop — d47 re-reads the file whenever it
changes, so you never have to leave the game.

Delete the file and everything goes back to how it shipped.

## The two ways to write an entry

**Respell it.** Write the word the way it sounds, in ordinary letters, and d47 says it the way
it would say those letters. `"Deciat": "dessy at"`. This is the one to reach for first — it
needs nothing but your ear.

Capitals are for you, not for d47: it reads `DEZH` and `dezh` the same way. If you want the
emphasis in a particular place, use the other form.

**Write the sounds exactly.** Put `ipa:` in front and the rest is taken as
[IPA](https://en.wikipedia.org/wiki/International_Phonetic_Alphabet), symbol for symbol,
including where the stress mark `ˈ` goes. `"Dezhra": "ipa:ˈdɛʒɹə"`. This is exact control and
it is expert-hostile; it is here for the stubborn ones.

## What the rules are

- **Whole words only.** An entry for `male` will never reach inside `female`, and one for
  `observe` will never reach inside `observed`. A key can be several words — `Shinrarta
  Dezhra` is one name — and the longest matching entry wins.
- **Capitals do not matter** in the word being corrected. `DEZHRA`, `Dezhra` and `dezhra` are
  the same entry.
- **It beats everything.** Your entry outranks the shipped dictionary and the letter-to-sound
  rules both. Nothing overrides it.
- **A broken entry is ignored, not fatal.** An empty pronunciation, or IPA containing a symbol
  the voice has no sound for, is skipped: that word is said the way it would have been anyway,
  and the log names the entry once so you can go and fix it. The rest of your file still works.
- **Comments and a trailing comma are allowed**, because this is a file a person types into.

## What it does not cover

This is the **local voice** only. ElevenLabs, OpenAI, Cartesia and Edge each work out their own
pronunciations inside their own service, where d47 cannot reach — so an entry here does nothing
when one of those is speaking. That is not a bug to report.

It is also **per installation, not per Commander**. How a word sounds is a fact about the
voice, not about who is flying, so every Commander on this machine gets the same corrections.
