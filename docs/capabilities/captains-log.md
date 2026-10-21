---
title: Captain's log
group: Conversation
nav_order: 159
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to a log entry in your own words. No AI needed.</p>
<section>
<h2><span class="num">1</span> Say "captain's log", then the entry.</h2>
<p class="say">Say "captain's log, the Guardian site is two kilometres north of the landing pad".</p>
<p class="expect">D47 answers "Logged." and keeps every word after "captain's log" exactly as it heard them.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a log entry typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">captain's log, the Guardian site is two kilometres north</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">D47 answers "Logged." and the model is not asked.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">"log entry, fuel is low" does the same.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The entry needs words after the phrase.</h2>
<p class="say">Say the entry in the same breath as "captain's log".</p>
<p class="expect">"Captain's log" alone saves nothing; D47 says "Say the entry after 'captain's log'."</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The opening phrase alone is not saved">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">captain's log</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Nothing is saved without an entry.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Your own words, kept as you said them, with where you were.</p>
<section>
<h2><span class="num">1</span> The words are kept, not rewritten.</h2>
<svg viewBox="0 0 880 200" role="img" aria-label="A spoken entry is saved with the system, body and ship, and never goes to the AI">
 <rect x="20" y="30" width="400" height="100" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="72" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">what you said</text>
 <text x="220" y="104" text-anchor="middle" font-size="16" fill="var(--text-muted)">word for word</text>
 <rect x="460" y="30" width="400" height="100" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="72" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">captains-log.json</text>
 <text x="660" y="104" text-anchor="middle" font-size="16" fill="var(--text-muted)">with system, body and ship</text>
 <text x="440" y="176" text-anchor="middle" font-size="16" fill="var(--text-muted)">The AI never sees an entry, and is refused if it tries to write one.</text>
</svg>
</section>
</div></div>
</details>

## The details

Say "captain's log" or "log entry", optionally followed by a comma or colon, and then the entry.
D47 saves the rest of the utterance exactly as it heard it and answers "Logged.". No model is
asked and nothing costs money.

### What is saved with it

Each entry keeps the time (UTC) and whichever of these the game state knows at that moment: the
system, the body, the station when you are docked, the ship's type and name, and your latitude and
longitude when you are on a body. Entries are kept per Commander in `data\captains-log.json`.

With no Commander flying, nothing is saved and D47 says "Nobody is flying, so there is nobody to
log for."

"Captain's log entries" is not an entry; it is reserved for reading entries back.

### Where the words go

D47 sends entries nowhere. The words pass through the speech recognition provider you have
selected as you speak them, and through the text-to-speech provider you have selected when D47
speaks "Logged.". The **Privacy and egress** section names both, and says whether each runs on
this PC.

### Only you write them

The tool is protected: the AI is not offered it and is refused if it asks, so it cannot write an
entry in your name.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `add_log_entry`

Save a log entry in the Commander's own words, with the system, body, station, ship and position
known now. Reached only by saying "captain's log".

```json
{"type":"object","properties":{"text":{"type":"string","description":"The entry, as the Commander said it."}},"required":["text"],"additionalProperties":false}
```

</details>
