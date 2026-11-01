---
title: Correcting a pronunciation
group: General help
nav_order: 9
---

# Correcting a pronunciation

<!--
  The How it works band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A text file in <code>data</code> overrides how the local voices say a word.</p>
<section>
<h2><span class="num">1</span> Write the word as it sounds.</h2>
<svg viewBox="0 0 880 140" role="img" aria-label="A file lists words and how to say them">
 <rect x="20" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="218" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">pronunciations.json</text>
 <text x="218" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">in the data folder</text>
 <line x1="424" y1="70" x2="440" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="454,70 438,62 438,78" fill="var(--accent-muted)"/>
 <rect x="464" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="662" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">"Deciat": "dessy at"</text>
 <text x="662" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">a respelling</text>
</svg>
<p class="body">Each entry maps a word or name to a respelling in ordinary letters, or to exact sounds written after <code>ipa:</code>.</p>
</section>
<section>
<h2><span class="num">2</span> D47 reads the file when it changes.</h2>
<svg viewBox="0 0 880 140" role="img" aria-label="Saving the file is enough">
 <rect x="20" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="218" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">SAVE THE FILE</text>
 <text x="218" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">no restart</text>
 <line x1="424" y1="70" x2="440" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="454,70 438,62 438,78" fill="var(--accent-muted)"/>
 <rect x="464" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="662" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">SAY IT AGAIN</text>
 <text x="662" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">the new sound</text>
</svg>
<p class="body">Delete the file and every word goes back to how it shipped.</p>
</section>
<section>
<h2><span class="num">3</span> Your entry wins over the shipped dictionary.</h2>
<svg viewBox="0 0 880 140" role="img" aria-label="The override is checked first">
 <rect x="20" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="218" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">YOUR ENTRY</text>
 <text x="218" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">checked first</text>
 <line x1="424" y1="70" x2="440" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="454,70 438,62 438,78" fill="var(--accent-muted)"/>
 <rect x="464" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="662" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">SHIPPED RULES</text>
 <text x="662" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">only if nothing matched</text>
</svg>
<p class="body">Whole words only, longest match first. The check comes first in <code>Phonemiser</code>: <code>if (layer?.Match(bodies, at) is { } correction)</code>.</p>
</section>
</div></div>
The local voices work a word out from its spelling when nobody has written the pronunciation
down for it. Most of the time that is right. Sometimes it is not — Elite has a very large
number of system names, Frontier keep adding to them, and players disagree about how many of
them are said.

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
needs nothing but your ear. It works for both local voices, Kokoro and Chatterbox.

**Write the sounds exactly.** Put `ipa:` in front and the rest is taken as
[IPA](https://en.wikipedia.org/wiki/International_Phonetic_Alphabet), symbol for symbol,
including where the stress mark `ˈ` goes. `"Dezhra": "ipa:ˈdɛʒɹə"`. This is exact control and
it is expert-hostile; it is here for the stubborn ones. IPA works only for Kokoro. Chatterbox
skips an IPA entry and says the word as written.

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

This is the **local voices** only, Kokoro and Chatterbox. ElevenLabs, OpenAI, Cartesia and Edge each work out their own
pronunciations inside their own service, where d47 cannot reach — so an entry here does nothing
when one of those is speaking. That is not a bug to report.

It is also **per installation, not per Commander**. How a word sounds is a fact about the
voice, not about who is flying, so every Commander on this machine gets the same corrections.
