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
<h2><span class="num">2</span> Or say the phrase alone, then the entry.</h2>
<p class="say">Say "captain's log", wait for "Go ahead.", then say the entry.</p>
<p class="expect">The next thing you say is kept word for word, even a command. "Cancel" or "never mind" saves nothing and D47 says "Not logged."; two minutes of silence drops the opening.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The opening phrase alone holds the next utterance">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">captain's log</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The next utterance is the entry.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
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

### Reading them back

These phrases are read without the model, so the words of an entry never leave this PC for a
provider. Matches are read newest first, at most three, each opened by its date in the game's
calendar and its system. More than three ends with "and four more on the Captain's log page."

- "read my last log entry"
- "read today's log entries", "read yesterday's log entries", "read this week's log entries",
  "read this session's log entries"
- "read log entries from Sol", "read log entries in Sol"
- "read log entries about Colonia", where every word has to appear in the entry
- "how many log entries"
- "delete my last log entry", which says which one went by its date and first words

"Captain's log entries from Sol" is a search, not an entry. The model cannot read entries, so
"what did I note about Colonia" does not work; "read log entries about Colonia" does.

### Exporting them

"Export my log entries" writes every entry for the Commander flying, oldest first, to
`data\captains-log\<Frontier id>\captains-log-<yyyy-MM-dd>.md`. A second export the same day is
written as `-2`, then `-3`, and never over the first. Each entry is a heading with its date in the
game's calendar, a line with whichever of system, body, station, ship and position were known, and
the text. D47 says how many entries and where the file is. With no entries nothing is written and
D47 says so.

### Where the words go

D47 sends entries nowhere. The words pass through the speech recognition provider you have
selected as you speak them, and through the text-to-speech provider you have selected when D47
speaks "Logged.". The **Privacy and egress** section names both, and says whether each runs on
this PC.

### Only you write them

The tools are protected: the AI is not offered them and is refused if it asks, so it cannot write,
read or delete an entry in your name.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `add_log_entry`

Save a log entry in the Commander's own words, with the system, body, station, ship and position
known now. Reached only by saying "captain's log".

```json
{"type":"object","properties":{"text":{"type":"string","description":"The entry, as the Commander said it."}},"required":["text"],"additionalProperties":false}
```

#### `find_log_entries`

Read the Commander's captain's log entries back, newest first, at most three in full. Reached only
by the read phrases above.

```json
{"type":"object","properties":{"count_only":{"type":"boolean","description":"Say how many entries match instead of reading them."},"span":{"type":"string","description":"Which entries by time; all when omitted.","enum":["all","last","today","yesterday","week","session"]},"system":{"type":"string","description":"Only entries made in this star system."},"words":{"type":"string","description":"Only entries whose text holds every one of these words."}},"required":[],"additionalProperties":false}
```

#### `delete_log_entry`

Delete the Commander's newest captain's log entry. Reached only by saying "delete my last log
entry".

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `export_log_entries`

Write every captain's log entry of the Commander to a Markdown file, oldest first, and say how many
and where. Reached only by saying "export my log entries".

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

</details>
