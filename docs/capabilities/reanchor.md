---
title: Re-anchor
group: Interface
nav_order: 127
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Putting your world-locked headset panels back in front of you.</p>
<section>
<h2><span class="num">1</span> Elite's recenter moves the world, not the panels.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="Elite's in-game recenter moves your cockpit without telling SteamVR, so world-locked panels are left behind">
 <rect x="20" y="44" width="380" height="104" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="210" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">ELITE RECENTRES</text>
 <text x="210" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">your cockpit moves</text>
 <line x1="412" y1="96" x2="428" y2="96" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="442,96 426,88 426,104" fill="var(--accent-muted)"/>
 <rect x="454" y="44" width="406" height="104" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="657" y="82" text-anchor="middle" font-size="16" font-weight="800" fill="var(--danger)">SteamVR IS NEVER TOLD</text>
 <text x="657" y="114" text-anchor="middle" font-size="15" fill="var(--text)">your panels stay exactly where</text>
 <text x="657" y="136" text-anchor="middle" font-size="15" fill="var(--text)">they were</text>
 <text x="440" y="196" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">Nothing can detect that and put it right on your behalf.</text>
 <text x="440" y="226" text-anchor="middle" font-size="15" fill="var(--text-muted)">From SteamVR’s point of view, nothing happened. What can happen is you noticing, and asking.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> They move together, which is not the same as resetting.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="Re-anchoring moves every world-locked panel as a group, preserving their arrangement">
 <rect x="20" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="80" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">RE-ANCHOR</text>
 <text x="220" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">every panel moves together</text>
 <text x="220" y="136" text-anchor="middle" font-size="15" fill="var(--text-muted)">the layout survives</text>
 <rect x="460" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="80" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text-muted)">NOT RESET</text>
 <text x="660" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">which would stack them all</text>
 <text x="660" y="136" text-anchor="middle" font-size="15" fill="var(--text-muted)">in one spot in front of you</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">Only your facing and position are used — looking at your feet is not an instruction.</text>
</svg>
<p class="body">Head-locked panels and the captions are untouched: they follow you already, so they were never anywhere to drift from. And it says how many moved, so “there was nothing to re-anchor” is an answer rather than a silence that looks like a failure.</p>
</section>
<section>
<h2><span class="num">3</span> Never only from the panel.</h2>
<svg viewBox="0 0 880 236" role="img" aria-label="A system-wide hotkey and a model-free spoken phrase both reach this, because the panel may be the thing you cannot aim at">
 <rect x="20" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="84" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">Ctrl+Alt+R</text>
 <text x="220" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">works anywhere, including</text>
 <text x="220" y="138" text-anchor="middle" font-size="15" fill="var(--text-muted)">with Elite in the foreground</text>
 <rect x="460" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="84" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">OR JUST SAY SO</text>
 <text x="660" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">and it needs no model</text>
 <text x="660" y="138" text-anchor="middle" font-size="15" fill="var(--text-muted)">configured at all</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">A drifted panel is exactly the case where you cannot aim at the panel.</text>
 <text x="440" y="224" text-anchor="middle" font-size="16" fill="var(--text-muted)">And the moment you want this is the moment Elite is holding the foreground.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="vr.html"><span class="ct">Headset →</span><span class="cd">Placing a panel in the first place, and what world-locked means.</span></a>
<a class="card" href="focus.html"><span class="ct">Focus the game →</span><span class="cd">The other thing that has to work from outside the window.</span></a>
<a class="card" href="interface.html"><span class="ct">The window →</span><span class="cd">The same panel, on the surface you can point a mouse at.</span></a>
</div>
</div>
</div></div>

## The details

Puts your world-locked headset panels back in front of you.

### Ask for it

> "re-anchor"
> "put the panels back"
> "recentre the panels"

Or press **Ctrl+Alt+R**, which works anywhere — including with Elite in the foreground.

### When you need it

Elite's in-game recenter moves your cockpit without telling SteamVR. Your world-locked panels
stay exactly where they were, in a world that has quietly rotated underneath them.

Nothing can detect that and put it right on your behalf: from SteamVR's point of view, nothing
happened. What can happen is you noticing your panels are in the wrong place and asking for them
back.

### What it does to them

Every world-locked panel moves together, so their arrangement relative to each other survives.
That is the difference between re-anchoring and resetting — putting each one back where it
started would stack them all in the same spot in front of you.

Only your facing and position are used. If you happened to be looking at your feet when you
triggered it, that is not an instruction to tip every panel forward and hang them over your knees.

Head-locked panels and the captions are untouched. They follow you already, so they were never
anywhere to drift from.

It tells you how many moved, so "there was nothing to re-anchor" is an answer rather than silence
that looks like a failure:

```text
Re-anchored 2 surfaces, keeping their layout.
```

### Never only from the panel

The hotkey works system-wide rather than only when Directive 47 has focus, and the spoken route
needs no model configured. Both matter for the same reason: a drifted panel is exactly the case
where you cannot aim at the panel, and the moment you want this is the moment Elite is holding
the foreground.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `reanchor_headset_surfaces`

Snaps every world-locked headset panel back in front of the Commander, preserving their relative
layout. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

This is its own capability rather than part of the headset one because the keyword router reaches
a capability's first argument-free tool. A capability owning both "how is the headset" and "put
the panels back" could only be asked one of them without a model in the path — and this is the
one that has to work with no model at all.

</details>
