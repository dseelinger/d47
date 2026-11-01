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
<p class="say">When one goes off, say "noted" or "remind me next time".</p>
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
<p class="say">Say "remind me to sell data in twenty minutes" and read the reply.</p>
<p class="expect">A time is declined: "Reminders follow the game rather than the clock. Ask me for one when you next dock, when you arrive in a system, or next session."</p>
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
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A reminder waits for a moment in the game, then D47 says it in your words.</p>
<section>
<h2><span class="num">1</span> The game decides when, not the clock.</h2>
<svg viewBox="0 0 880 162" role="img" aria-label="A reminder waits for a journal moment such as docking">
 <rect x="20" y="16" width="840" height="130" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">You say "remind me to buy limpets when I next dock".</text>
 <text x="440" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">D47 stores "buy limpets" against the next docking.</text>
 <text x="440" y="124" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">At the next docking it says it once.</text>
</svg>
<p class="body">A fixed grammar reads the sentence and the moment, with no AI. The reminder fires when the journal shows that moment.</p>
</section>
<section>
<h2><span class="num">2</span> Only you can set, cancel or answer one.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="The AI is refused every reminder tool that changes anything">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">Setting, cancelling and answering are protected.</text>
 <text x="440" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">A comms message cannot plant a reminder.</text>
</svg>
<p class="body">These tools are protected: the AI is not offered them and is refused if it asks. A message from another player cannot set a reminder.</p>
</section>
<section>
<h2><span class="num">3</span> The reply reads back what was stored.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="D47 reads the reminder back as it will be said">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">"I'll remind you to buy limpets when you next dock."</text>
 <text x="440" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">RemindersCapability.cs</text>
</svg>
<p class="body"><code>RemindersCapability.Set</code> returns this line, built from the sentence and the moment, so a misheard station or system name shows at once.</p>
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
started with `--utilities`, a time goes to the [timer and alarm tools](utilities.html)
instead.

A material must be one with a storage limit, and a reminder holds at most 300 characters. At most 64
reminders are kept at once; the 65th is refused until one is cancelled.

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
start of your next session.

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
