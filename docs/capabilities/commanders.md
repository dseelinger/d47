---
title: Switch Commander
group: Interface
nav_order: 153
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">One step. It works once the journals list two or more Commanders.</p>
<section>
<h2><span class="num">1</span> Say who to switch to.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a switch command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">switch to commander Kestrel Vane</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"switch to Kestrel Vane" works too.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">The Commander you are showing is not offered.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Changing the Commander d47 shows, without the mouse.</p>
<section>
<h2><span class="num">1</span> Only you can switch.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="A misheard sentence could switch Commanders, so the model is never allowed to">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--text)">The model cannot switch Commanders.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Your own words, the panel and the title bar can.</text>
</svg>
</section>
</div></div>
</details>

## The details

Changes which Commander d47 shows, by name. It reads the Commanders the journals list, the same ones
the title bar menu and the Commanders page show.

### Ask for it

> "switch to commander Kestrel Vane"
> "switch to Kestrel Vane"

```text
Switched to CMDR Kestrel Vane. I'll stay with this commander until you switch again.
```

Every Commander except the one d47 is showing has these two phrases. Until the journals list two or
more Commanders there is nothing to switch to.

### The model cannot do this

Only the spoken phrases, the title bar and the Commanders page switch Commanders. `switch_commander`
is **protected**: when the model calls it, the call is refused. A misheard sentence passed to the
model would otherwise switch Commanders mid-conversation.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `switch_commander`

Switch d47 to another Commander found in the journals. Reachable by spoken phrase, the panel or a
hotkey, never by the model.

```json
{"type":"object","properties":{"frontier_id":{"type":"string","description":"The Frontier ID of the Commander to switch to."}},"required":["frontier_id"],"additionalProperties":false}
```

</details>
