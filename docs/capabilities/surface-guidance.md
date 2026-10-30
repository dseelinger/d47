---
title: Surface guidance
group: Acting on the game
nav_order: 158
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to finding your way back on a surface.</p>
<section>
<h2><span class="num">1</span> Mark where you stand.</h2>
<p class="say">Say "mark this spot".</p>
<p class="expect">D47 says "Marked as mark 1." Name one by saying, for example, "mark this spot as the crash site".</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a mark command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">mark this spot</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text)">"Marked as mark 1."</text>
 <text x="20" y="152" font-size="15" fill="var(--text-muted)">You need to be on or near the surface for D47 to have your position.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Ask which way.</h2>
<p class="say">Say "where is my ship", "which way to my last sample" or "which way to mark 1".</p>
<p class="expect">D47 gives the distance and compass bearing, such as "Your ship is 1.2 kilometres away, bearing 140, south-east." When Elite reports which way you face it adds where the point lies, such as "It is ahead on your right."</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a where-is-my-ship command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">where is my ship</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text)">"Your ship is 1.2 kilometres away, bearing 140, south-east."</text>
 <text x="20" y="152" font-size="15" fill="var(--text-muted)">"It is ahead on your right." follows when Elite reports your heading.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Marks end when you leave the body.</p>
<p class="expect">D47 forgets them on leaving the body, on a new visit and on restart, and says "Nothing is saved on this body to guide you to."</p>
<svg viewBox="0 0 880 152" role="img" aria-label="Marks last for one visit to one body">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">A mark belongs to one visit.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Mark it again when you come back.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">D47 answers when asked, from points it already holds in memory.</p>
<section>
<h2><span class="num">1</span> Three kinds of point.</h2>
<svg viewBox="0 0 880 200" role="img" aria-label="Guidance uses the ship, your marks and your samples">
 <rect x="20" y="40" width="250" height="100" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="145" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOUR SHIP</text>
 <text x="145" y="114" text-anchor="middle" font-size="15" fill="var(--text-muted)">where it landed</text>
 <rect x="315" y="40" width="250" height="100" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOUR {"type":"object","properties":{"name":{"type":"string","description":"What to call the mark. Optional; omit to number it."}},"required":[],"additionalProperties":false}S</text>
 <text x="440" y="114" text-anchor="middle" font-size="15" fill="var(--text-muted)">saved by you</text>
 <rect x="610" y="40" width="250" height="100" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="735" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOUR SAMPLES</text>
 <text x="735" y="114" text-anchor="middle" font-size="15" fill="var(--text-muted)">of an open set</text>
 <text x="440" y="182" text-anchor="middle" font-size="16" fill="var(--text)">Without a target it picks the ship, then the newest mark, then the last sample.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Nothing leaves this machine.</h2>
<svg viewBox="0 0 880 170" role="img" aria-label="Marks are kept in memory and nothing is sent">
 <rect x="20" y="30" width="840" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="68" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">Marks are held in memory, not saved to disk.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text-muted)">Neither tool sends anything, and the model may use either.</text>
</svg>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="exobiology.html"><span class="ct">Exobiology →</span><span class="cd">The samples a bearing can lead back to.</span></a>
</div>
</div>
</div></div>

## The details

Saves points on the body you are at and tells you the bearing and distance to them. It answers when
asked and does not repeat itself while you walk.

### Mark a spot

> "mark this spot"
> "mark this location"
> "mark my position"

Saves your live position as "mark 1", "mark 2" and so on, using the lowest number not in use.
"mark this spot as the crash site" names it and goes through the model. With no position from
Elite, D47 says:

```text
I have no position for you. Marking a spot needs you on or near the surface.
```

A mark lasts while you stay at that body. It is dropped when you leave the body, on your next visit
and on restart. It is not a bookmark.

### Ask which way

> "where is my ship"
> "which way to my ship"
> "guide me to my ship"
> "which way to my last sample"
> "which way to my mark"
> "which way to crash site"

With no target D47 uses the ship, then the newest mark, then the last specimen of the newest open
set. The compass bearing is always given. The wording relative to the way you face is added only
when Status.json has a heading. It uses eight 45-degree sectors centred on straight ahead: ahead,
ahead on your right, on your right, behind you on your right, behind you, and the three on the left.
A point on another body is never used.

### Ask for the nearest earlier find

> "which way to the nearest earlier find"
> "guide me to the nearest earlier find"
> "which way to the nearest Aurasus"

With Earlier biology finds from Canonn on and the finds for this system already fetched, D47 picks
the find on this body nearest your position and gives it in the same wording, such as "The nearest
earlier Bacterium Aurasus - Green find is 2.4 kilometres away, bearing 210, south-west." A name
after "nearest" must appear in the variant name and goes through the model. Without a match D47
says "No Commander has logged an earlier Bacterium find on this body." The spot is where another Commander logged
the entry, not a specimen. This tool sends no request. A plain genus without "nearest" still means
your own last sample. A spoken list of points names at most three.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `mark_surface_point`

Saves the live `Status.json` position as a mark in memory, keyed by the body in `SurfacePoints`.
Optional `name`; refuses without a position. Not protected.

```json
{"type":"object","properties":{"name":{"type":"string","description":"What to call the mark. Optional; omit to number it."}},"required":[],"additionalProperties":false}
```

#### `get_surface_bearing`

Gives the bearing and distance to the ship, a mark, or a genus or species being sampled. Optional
`to`; `nearest <name>` picks the nearest earlier find from the `EarlierFinds` store. Not protected.

```json
{"type":"object","properties":{"to":{"type":"string","description":"ship, the name of a mark, a genus or species being sampled, or \u0022nearest \u003Cname\u003E\u0022 for the nearest earlier find. Optional."}},"required":[],"additionalProperties":false}
```

</details>
