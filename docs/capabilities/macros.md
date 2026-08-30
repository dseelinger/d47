---
title: Macros
group: Acting on the game
nav_order: 135
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Your own named sequences of ship actions. Say the name, and they run in order.</p>
<section>
<h2><span class="num">1</span> You write them down, not out loud.</h2>
<svg viewBox="0 0 880 236" role="img" aria-label="Every other capability has a fixed vocabulary, but composing a new sequence does not, so macros are authored in the panel or the file">
 <rect x="20" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text-muted)">EVERYTHING ELSE</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">has a fixed list of words</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">behind it</text>
 <rect x="460" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">A NEW SEQUENCE</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">does not, and never can</text>
 <text x="660" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">so you author it instead</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">Settings → Macros → Edit macros: a drop-down of actions, one of on/off, and a pause.</text>
 <text x="440" y="224" text-anchor="middle" font-size="15" fill="var(--text-muted)">Or the file. Both write the same one, re-read while it runs — a saved macro is sayable a moment later.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> It stops before it starts.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="Every step is checked before any is sent, so a macro with an unreachable step does not run at all">
 <rect x="20" y="40" width="190" height="70" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="115" y="82" text-anchor="middle" font-size="15" fill="var(--text)">gear on</text>
 <rect x="230" y="40" width="190" height="70" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="325" y="82" text-anchor="middle" font-size="15" fill="var(--text)">lights on</text>
 <rect x="440" y="40" width="190" height="70" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="535" y="82" text-anchor="middle" font-size="15" fill="var(--text)">cargo scoop on</text>
 <rect x="650" y="40" width="190" height="70" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="745" y="74" text-anchor="middle" font-size="15" font-weight="700" fill="var(--danger)">hardpoints</text>
 <text x="745" y="98" text-anchor="middle" font-size="14" fill="var(--text-muted)">on your stick</text>
 <text x="440" y="156" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So none of them is sent.</text>
 <text x="440" y="192" text-anchor="middle" font-size="16" fill="var(--text)">Every step is checked before any of them goes.</text>
 <text x="440" y="222" text-anchor="middle" font-size="15" fill="var(--text-muted)">Half a macro leaves the ship in a state you did not ask for and have to work out yourself.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> A macro can only say what you could already say.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="Macros are limited to existing actions, exclude weapons, and cannot take a name that already means something">
 <rect x="20" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="76" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">ONLY WHAT IT ALREADY HAS</text>
 <text x="220" y="106" text-anchor="middle" font-size="14" fill="var(--text-muted)">the same list “gear down” comes from</text>
 <text x="220" y="130" text-anchor="middle" font-size="14" fill="var(--text-muted)">it cannot express a new key</text>
 <rect x="460" y="40" width="190" height="110" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="555" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--danger)">NO WEAPONS</text>
 <text x="555" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">authored text is not</text>
 <text x="555" y="130" text-anchor="middle" font-size="14" fill="var(--text-muted)">a way around that</text>
 <rect x="670" y="40" width="190" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="765" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">NO TAKING</text>
 <text x="765" y="94" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">A USED NAME</text>
 <text x="765" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">“gear down” is refused</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text-muted)">Otherwise two things answer to it and you cannot tell which one ran.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> A bad one is refused by name, with the reason.</h2>
<svg viewBox="0 0 880 214" role="img" aria-label="A macro naming an action that does not exist is reported by name rather than silently dropped">
 <rect x="20" y="36" width="840" height="64" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="46" y="76" text-anchor="start" font-size="16" fill="var(--danger)">Refused: “combat” uses an action D47 does not have: shields_up.</text>
 <text x="440" y="142" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">One bad macro does not cost you the others.</text>
 <text x="440" y="176" text-anchor="middle" font-size="15" fill="var(--text-muted)">The rest of the file still loads — a silent drop would leave you saying a phrase into the dark.</text>
 <text x="440" y="202" text-anchor="middle" font-size="15" fill="var(--text-muted)">Steps already in the state you asked for are skipped, so a macro is safe to say twice.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="flight-controls.html"><span class="ct">Flight and navigation →</span><span class="cd">The actions a macro is built from, and the switch it needs.</span></a>
<a class="card" href="switches.html"><span class="ct">HOTAS switches →</span><span class="cd">Running one from a physical switch instead of by saying its name.</span></a>
<a class="card" href="ship-systems.html"><span class="ct">Ship systems →</span><span class="cd">Pips and heat sinks, the steps a docking macro usually wants.</span></a>
</div>
</div>
</div></div>

## The details

Your own named sequences of ship actions. Say the name, and Directive 47 runs them in order.

### Ask for it

> "docking prep"
> "run docking prep"
> "what macros do I have"

The name is whatever you called it. Both "the name" and "run the name" work, and neither needs
the AI to be configured — a macro is the most closed vocabulary there is, because you wrote it.

### Writing one

Macros are the one thing here you cannot set up by voice, and that is deliberate. Everything else
Directive 47 does has a fixed list of words behind it; composing a *new* sequence does not, and it
never can. So authoring happens in the panel or in the file.

**In the panel:** Settings → Macros → **Edit macros**. Each step is a drop-down of actions, a
drop-down of on / off / toggle, and a pause in milliseconds. There is nothing to type but the
name, so nothing you build there can be rejected.

**In the file:** `data/macros.json`, beside the executable like everything else Directive 47
writes.

```json
{
  "macros": [
    {
      "name": "docking prep",
      "steps": [
        { "action": "landing_gear", "state": "on", "pauseMs": 200 },
        { "action": "lights", "state": "on" }
      ]
    }
  ]
}
```

Both routes write the same file, and it is re-read while Directive 47 is running — a macro saved
in a text editor is sayable a moment later, with no restart.

### The pauses matter

Elite's panels animate. Without a pause, the second keystroke of "open the right panel, then go
down twice" arrives before the panel is there. 250 ms is the default and is usually enough.

### What a macro is allowed to do

Only actions Directive 47 already has — the same list that "gear down" comes from. It cannot
express a key that is not already reachable by asking out loud.

Two limits worth knowing:

- **No weapons.** The fire groups exist internally for the arrival honk, and macros cannot reach
  them. A macro is authored text, and authored text must not be a way around the rule that the
  AI never gets to open fire.
- **No taking a name that already means something.** A macro called "gear down" is refused,
  because otherwise two things answer to it and you cannot tell which one ran.

### When something is wrong with one

A bad macro is never silently dropped — that would leave you saying a phrase into the dark for a
week. It is refused by name, with the reason, in the editor and in "what macros do I have":

```text
- Refused: "combat" uses an action D47 does not have: shields_up.
```

One bad macro does not cost you the others. The rest of the file still loads.

### It stops before it starts

Every step is checked before any of them is sent. If the fourth action is on your joystick, the
macro does not run at all rather than pressing the first three and leaving the ship half
configured in a state you did not ask for and have to work out for yourself.

Steps already in the state you asked for are skipped, so a macro that puts the gear down does
nothing about the gear when it is already down.

Macros need **Let Directive 47 press keys in Elite** switched on — see
[Flight and navigation](flight-controls.md).

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `list_macros`

Report the Commander's macros, their steps, and any that were refused. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `run_macro`

Run one of the Commander's own named macros. Only names listed in the current game state exist;
anything else comes back saying so.

```json
{"type":"object","properties":{"name":{"type":"string","description":"The macro\u0027s name, as the Commander wrote it."}},"required":["name"],"additionalProperties":false}
```

The name is a free string rather than an enum, and that is not laziness. Macro names change
whenever the file is edited, and a schema that changed with them would invalidate the entire
cached prompt prefix. The names available now ride in the live game-state block below the cache
breakpoint, and the handler enforces the list regardless of what the model asked for.

The model-free route needs the same trick from the other end: descriptors are registered once and
never mutated, so macro phrases reach the keyword router through a function it consults rather
than through the descriptor. A macro name never enters a schema, so nothing about caching is
affected by letting that source change.

</details>
