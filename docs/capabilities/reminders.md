---
title: Journal reminders
group: Interface
nav_order: 152
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md. The class is d47-howto rather than d47-eli5: HelpLibrary.Band takes
  the first d47-eli5 div in the file, and the app draws only that one.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to a reminder that waits for the game. No AI needed.</p>
<section>
<h2><span class="num">1</span> Say what, then when.</h2>
<p class="say">Say "remind me to buy limpets when I next dock".</p>
<p class="expect">D47 reads it back: "I'll remind you to buy limpets when you next dock."</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a reminder typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">remind me to buy limpets when I next dock</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It reads the reminder back: "I'll remind you to buy limpets when you next dock."</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">"what reminders do I have" — "cancel the reminder to buy limpets"</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Answer it when it goes off.</h2>
<p class="say">Say "noted", or "remind me next time".</p>
<p class="expect">Noted removes the reminder; remind me next time arms it again for the same moment.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with an answer to a reminder typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">noted</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"remind me next time" arms it again on the same moment.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">"remind me tomorrow" arms it for your next session.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> A moment in the game, never a time.</h2>
<p class="say">Say a moment such as "when I next dock" or "next session", not "in twenty minutes".</p>
<p class="expect">A time is declined, and the reply names the moments D47 does take.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="A time of day is declined">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">remind me to sell data in twenty minutes</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Reminders follow the game, not the clock.</text>
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

### Answering one that went off

For as long as a reminder has gone off and not been answered, three more phrases are heard. They
answer the most recent one.

| Say | Does |
| --- | --- |
| "noted", "got it", "thanks" | Removes it. |
| "remind me next time" | Arms it again on the same moment. |
| "remind me tomorrow", "remind me next session" | Arms it again for the start of your next session. |

"Tomorrow" means your next session, not a time of day, because these reminders follow the game.
With nothing gone off, none of these is heard. A reminder you do not answer is removed at the
start of your next session, as before.

The carrier captain's fuel and upkeep warnings answer to the same phrases while one has just been
spoken, whichever fired last. "Noted" keeps it quiet until its condition clears and returns.
"Remind me next time" lets it speak at the next dock or jump request on the same reading, and the
tomorrow and next-session phrases keep it quiet until you next load the game. A restart forgets all
of this, and a warning's switch still turns it off for good.

### Only you set them

Setting, cancelling and answering are protected: the AI is not offered any of them and is refused if it asks. A
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

#### `acknowledge_journal_reminder`

Remove the journal reminder that has just gone off. Reached only by saying "noted", "got it" or "thanks".

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `snooze_journal_reminder`

Arm the journal reminder that has just gone off again, on its own trigger or at the next session.

```json
{"type":"object","properties":{"until":{"type":"string","description":"When it goes off again: the same moment, or the next session.","enum":["same_trigger","next_session"]}},"required":[],"additionalProperties":false}
```

</details>
