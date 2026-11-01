---
title: Switch Commander
group: Interface
nav_order: 155
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to a different Commander. It works once the journals list two or more Commanders.</p>
<section>
<h2><span class="num">1</span> Say who to switch to.</h2>
<p class="say">Say "switch to Kestrel Vane".</p>
<p class="expect">D47 says "Switched to CMDR Kestrel Vane" and stays with that Commander until you switch again or another Commander logs in.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a switch command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">switch to commander Kestrel Vane</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"switch to Kestrel Vane" works too.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">The Commander you are showing is not offered.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Or switch from the title bar or the Commanders page.</h2>
<p class="say">Pick another Commander from the title bar menu or the Commanders page.</p>
<p class="expect">D47 shows the Commander you picked; the one already showing is not offered.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The title bar and the Commanders page switch too">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">Commanders</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The same Commanders the journals list appear in both places.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> Only you can switch.</h2>
<p class="say">Say the phrase or use the title bar yourself.</p>
<p class="expect">A switch asked of the model is refused, so nothing changes.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The model is refused this tool">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">switch to Kestrel Vane</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">A switch the model asks for is refused.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">The phrase, the title bar and the page all work.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Saying a Commander's name changes which Commander D47 shows, without the mouse.</p>
<section>
<h2><span class="num">1</span> The journals list the Commanders you can switch to.</h2>
<svg viewBox="0 0 880 162" role="img" aria-label="Each Commander but the one showing gets two phrases">
 <rect x="20" y="16" width="840" height="130" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">Journals list CMDR Kestrel Vane.</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">You say "switch to Kestrel Vane".</text>
 <text x="440" y="124" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">D47 shows that Commander.</text>
</svg>
<p class="body">D47 builds "switch to" phrases from the Commanders the journals list, leaving out the one showing. With only one Commander listed there is nothing to switch to.</p>
</section>
<section>
<h2><span class="num">2</span> Only you can switch.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="A model call to switch Commanders is refused">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">A misheard sentence could switch Commanders.</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">So the model is refused this tool.</text>
</svg>
<p class="body">Spoken phrases, the title bar and the Commanders page switch Commanders. The model is refused, so a misheard sentence cannot change who D47 shows mid-conversation.</p>
</section>
<section>
<h2><span class="num">3</span> The reply confirms who you are now.</h2>
<svg viewBox="0 0 880 94" role="img" aria-label="The confirmation is one fixed line">
 <rect x="20" y="16" width="840" height="62" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">"Switched to CMDR Kestrel Vane. I'll stay with this commander until you switch again or another commander logs in."</text>
</svg>
<p class="body"><code>CommandersCapability.Switched</code> builds this line. Picking the Commander already showing answers "You are already CMDR" and the name.</p>
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
Switched to CMDR Kestrel Vane. I'll stay with this commander until you switch again or another commander logs in.
```

Every Commander except the one d47 is showing has these two phrases. Until the journals list two or
more Commanders there is nothing to switch to.

### Who d47 shows

D47 starts with the Commander who last logged in, even while Elite is still at its menu. A different
Commander logging in to the game is shown in place of the one you picked.

### The conversation follows the switch

Each Commander has their own conversation with d47 until d47 closes. After a switch, d47 answers from
the picked Commander's conversation, which is empty if they have not spoken since d47 started.
Switching back resumes the first Commander's conversation. The transcript on screen keeps its lines
and adds "Showing Commander Kestrel Vane" where the switch happened. A Commander logging in to the
game starts every conversation from nothing.

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
