---
title: Journal reminders
group: Interface
nav_order: 150
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md. The class is d47-howto rather than d47-eli5: HelpLibrary.Band takes
  the first d47-eli5 div in the file, and the app draws only that one.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Say "remind me to" and a moment in the game. No AI needed.</p>
<section>
<h2><span class="num">1</span> Say what, then when.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a reminder typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">remind me to buy limpets when I next dock</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It reads the reminder back: "I'll remind you to buy limpets when you next dock."</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">"what reminders do I have" — "cancel the reminder to buy limpets"</text>
</svg>
</section>
</div></div>
</details>

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Reminders in your own words, said when the game reaches a moment.</p>
<section>
<h2><span class="num">1</span> The game decides when, not the clock.</h2>
<svg viewBox="0 0 880 200" role="img" aria-label="A reminder waits for a journal moment such as docking, then is said in the Commander's words">
 <rect x="20" y="30" width="400" height="100" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="72" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">when I next dock</text>
 <text x="220" y="104" text-anchor="middle" font-size="16" fill="var(--text-muted)">waits for Docked in the journal</text>
 <rect x="460" y="30" width="400" height="100" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="72" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">buy limpets</text>
 <text x="660" y="104" text-anchor="middle" font-size="16" fill="var(--text-muted)">said once, as you said it</text>
 <text x="440" y="176" text-anchor="middle" font-size="16" fill="var(--text-muted)">Only you can set or cancel one. The AI is refused both.</text>
</svg>
</section>
</div></div>
</details>

## The details

Reminders in your own words, said when the game reaches a moment.

### Ask for it

> "remind me to buy limpets when I next dock"
> "remind me to sell data when I arrive in Sol"
> "when I'm back at my carrier, remind me to fit the new shields"
> "what reminders do I have"
> "cancel the reminder to buy limpets"

Setting and cancelling need no AI configured at all. The words are read by a fixed grammar, and
the reply reads the reminder back with any system, station or material name exactly as it was
stored, so a misheard name shows up straight away.

### The moments

| Say | Fires |
| --- | --- |
| "when I next dock", "next time I dock" | At your next docking anywhere. |
| "when I dock at Jameson Memorial" | At a docking at that station. |
| "when I get to Sol", "when I arrive in Sol" | When you arrive in that system. |
| "when I'm back at my carrier" | When you dock at your own carrier. |
| "when my hold is empty", "when my hold is full" | When the ship's hold becomes empty or full. |
| "when arsenic is full" | When that material reaches its storage limit. |
| "next session", "next time I play" | At the start of your next session. |

"Remind me to" with any other moment is declined, and the reply names these. A time — "in twenty
minutes", "at seven" — is declined the same way, because these reminders follow the game. In a run
started with timers and alarms, a time goes to the [timer and alarm tools](utilities.html)
instead.

What it says when one fires is set out on the [Callouts](callouts.html) page.

### Only you set them

Setting and cancelling are protected: the AI is not offered either and is refused if it asks. A
message from another player in your comms panel cannot plant a reminder.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `set_journal_reminder`

Store a reminder in the Commander's own words, said once when the journal reaches the trigger.
Reached only by saying "remind me to".

```json
{"type":"object","properties":{"argument":{"type":"string","description":"The station, system or material the trigger names."},"sentence":{"type":"string","description":"What to say when it fires, as the Commander said it."},"trigger":{"type":"string","description":"The moment in the game it waits for.","enum":["next_docking","docking_at","arrival_in","own_carrier","hold_empty","hold_full","material_full","next_session"]}},"required":["sentence","trigger"],"additionalProperties":false}
```

#### `get_journal_reminders`

List the journal reminders the Commander has armed, each with the moment it waits for.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `cancel_journal_reminder`

Cancel an armed journal reminder by words from its sentence.

```json
{"type":"object","properties":{"words":{"type":"string","description":"Words from the sentence of the reminder to cancel."}},"required":["words"],"additionalProperties":false}
```

</details>
