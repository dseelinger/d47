---
title: Exobiology
group: Knowledge
nav_order: 117
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and that is load-bearing rather than cosmetic.
  HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under that class
  would silently become what the in-app panel draws on this page. The docs site styles the two
  identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. That separation is the
  whole point of there being two, and it is the thing that will erode first.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to knowing what a plant is worth before you land.</p>
<section>
<h2><span class="num">1</span> Scan something, or ask about a genus by name.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what is bacterium aurasus worth</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The value tables ship with D47 and need no network.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Ask about the genus and it answers for the whole family.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Read which half the answer came from.</h2>
<svg viewBox="0 0 880 308" role="img" aria-label="Bacterium Aurasus">
 <rect x="20" y="16" width="840" height="268" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Bacterium Aurasus</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Value</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">1,000,000 cr</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Where it grows</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text-muted)">from the shipped tables</text>
 <rect x="44" y="182" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="210" font-size="16" fill="var(--text)">Whether anybody has sold one here</text>
 <text x="812" y="210" text-anchor="end" font-size="16" fill="var(--text-muted)">needs the network</text>
 <text x="44" y="278" font-size="15" fill="var(--text-muted)">The two halves come from two places, and the answer says which.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="First-footfall bonuses are not in the shipped figure.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">First-footfall bonuses are not in the shipped figure.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">The number is the base sale. Being first multiplies it, and D47 says so rather than folding it in.</text>
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
<p class="intro">Two halves, from two sources, answering two questions — and only one of them may quote money.</p>
<section>
<h2><span class="num">1</span> Keeping the halves apart is the whole design.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="The scan half names genera and cannot price them; the route half names species and can">
 <rect x="20" y="40" width="400" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">AFTER THE SCAN</text>
 <text x="220" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">the game’s own answer</text>
 <text x="220" y="130" text-anchor="middle" font-size="15" fill="var(--text-muted)">names the genus</text>
 <text x="220" y="152" text-anchor="middle" font-size="14" font-weight="700" fill="var(--danger)">so it cannot quote money</text>
 <rect x="460" y="40" width="400" height="124" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">BEFORE YOU GO</text>
 <text x="660" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">an index of what others found</text>
 <text x="660" y="130" text-anchor="middle" font-size="15" fill="var(--text-muted)">names the species</text>
 <text x="660" y="152" text-anchor="middle" font-size="14" font-weight="700" fill="var(--accent)">so it may</text>
 <text x="440" y="210" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">The species is what sets the price.</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">Mixing the halves would hide which figures rest on a survey and which rest on nothing.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Elite names the genus and stops there.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="A surface scan reports a genus such as Brain Trees, never the species that would set its value">
 <rect x="20" y="36" width="840" height="94" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="46" y="74" text-anchor="start" font-size="16" fill="var(--text)">1 biological signal: Brain Trees.</text>
 <text x="46" y="108" text-anchor="start" font-size="15" fill="var(--text-muted)">Also down there: 3 Geological.</text>
 <text x="440" y="170" text-anchor="middle" font-size="16" fill="var(--text)">Every one of the 792 events measured names a genus, and never a species.</text>
 <text x="440" y="200" text-anchor="middle" font-size="16" fill="var(--text)">Bacterium Alcyoneum and Bacterium Acies are very different money.</text>
 <text x="440" y="230" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So this half lists what is there and refuses to price it.</text>
</svg>
<p class="body">A scan that found nothing is a real answer, and the one that saves you a landing — <em>no biological signals, nothing here to sample</em>. That is different from “I have not looked”, and the two are worded differently on purpose.</p>
</section>
<section>
<h2><span class="num">3</span> It will not tell you whether you have gone far enough.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Sampling progress reports an upper bound learned from your own play, never the required distance">
 <rect x="20" y="36" width="840" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="46" y="72" text-anchor="start" font-size="16" fill="var(--text)">Stratum Paleas — 2 of 3, 1 to go.  341 metres from your last specimen.</text>
 <text x="46" y="102" text-anchor="start" font-size="15" fill="var(--text)">The closest I have seen Stratum accepted is 502 metres, over 4 samples.</text>
 <text x="46" y="130" text-anchor="start" font-size="14" fill="var(--text-muted)">That is an upper bound on what it needs, not the figure — the Codex entry has that.</text>
 <text x="440" y="192" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">A sourcing decision rather than a gap.</text>
 <text x="440" y="222" text-anchor="middle" font-size="16" fill="var(--text)">The real figure is published by the game itself, in the species’ own Codex entry.</text>
 <text x="440" y="248" text-anchor="middle" font-size="15" fill="var(--text-muted)">A community wiki copied into a shipped table is Directive 47 laundering somebody’s forum post.</text>
</svg>
<p class="body">What it does instead is learn. A specimen the game accepted is proof the distance you travelled was sufficient, so the smallest accepted gap is an upper bound — measured from your own play, carried with its sample size, and never presented as the figure itself. It needs no source and gets better the more you sample.</p>
</section>
<section>
<h2><span class="num">4</span> A plotted route cannot contain a first footfall.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="An index only holds visited systems while a first footfall only happens where nobody has been">
 <rect x="20" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">A PLOTTED ROUTE</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">only holds what somebody</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">has already visited</text>
 <line x1="432" y1="86" x2="448" y2="104" stroke="var(--danger)" stroke-width="3" stroke-linecap="round"/>
 <line x1="448" y1="86" x2="432" y2="104" stroke="var(--danger)" stroke-width="3" stroke-linecap="round"/>
 <rect x="460" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">A FIRST FOOTFALL</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">only happens where</text>
 <text x="660" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">nobody has been</text>
 <text x="440" y="196" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So every plot says so in its own last line.</text>
</svg>
<p class="body">The bonus is five times the value, and it is the trade-off nobody should discover after the flight. If undiscovered systems are what you are after, read a system name instead — that works with no network at all, which is the point of it.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="system-names.html"><span class="ct">System names →</span><span class="cd">The half of exploring an index cannot help with, read off the name alone.</span></a>
<a class="card" href="routes.html"><span class="ct">Routes →</span><span class="cd">The other plots that ride the same job-and-poll protocol.</span></a>
<a class="card" href="journal.html"><span class="ct">Journal →</span><span class="cd">Where the scan and the sampling run are read from.</span></a>
</div>
</div>
</div></div>

## The details

Plot a circuit through known biology, and read back what your own surface scan found on the body you
are at.

> "plot me an exobiology route"
> "what biology is on this body"
> "is this body worth landing on"
> "what did the scan find here"

Two halves, from two different sources, answering two different questions. Keeping them apart is the
whole design — because one of them can quote money and the other cannot.

### After the scan: what the game already told you

Once you have mapped a body with the detailed surface scanner, Elite writes `SAASignalsFound` and it
says what is down there outright. No prediction, no inference from `Scan` properties — the game's own
answer, which outranks anything D47 could work out.

```text
HR 3230 3 a a, surface-scanned 2026-08-16 10:00 game time.
  1 biological signal: Brain Trees.
  Also down there: 3 Geological.

Elite names the genus and not the species, and the species is what sets the price — so I cannot
tell you what this is worth until you sample it.
```

**That last paragraph is the ceiling on this half, and it is not a limitation D47 can engineer
around.** Every one of the 792 events measured names a *genus* — `Bacterium`, `Stratum`,
`Brain Trees` — and never a species. *Bacterium Alcyoneum* and *Bacterium Acies* are very different
money, and the game does not say which you are looking at until you take the sample. So this half
lists what is there and refuses to price it.

A scan that found nothing is a real answer and the one that saves you a landing:

```text
Fixture 1, surface-scanned 2026-08-16 10:00 game time.
  No biological signals. Nothing here to sample.
```

That is different from "I have not looked", and the two are worded differently on purpose.

### Before you go: a circuit through known biology

```text
1 system from Sol, 3 jumps, 6,904,100 credits of biology in all:

Opet — 3 jumps, 1 body, 6,904,100 cr.
  Opet 7 b (Rocky body, 2,536 ls out) — 6,904,100 cr
    Frutexa Flabellum — 1,808,900 cr
    Tussock Cultro — 1,766,600 cr
    Fungoida Setisis — 1,670,100 cr
    Bacterium Alcyoneum — 1,658,500 cr

Every system here has already been surveyed by somebody, so none of it is a first footfall —
that pays five times as much and only happens where nobody has been.
```

**Here the species *are* named, and that is why this half may quote figures.** The plotter is reading
an index of bodies people have already scanned and uploaded, and that index knows down to the
species. Values come from the response, never computed — a Commander's own sale history cannot price
a species, because 30 of the 31 species sold in the measured corpus were sold exactly once and the
row total covers an unstated number of specimens.

### On the surface: how many, and how far

Once you take the first specimen, Directive 47 tracks the run and speaks each one as it lands:

```text
Stratum Paleas, 2 of 3. 556 metres from the last one. 1 to go.
Stratum Paleas analysed. That run is complete.
```

Ask at any point and it will answer from where you are standing right now, which is the question you
actually have while driving away from the last specimen:

```text
Stratum Paleas — 2 of 3, 1 to go.
  341 metres from your last specimen.
  The closest I have seen Stratum accepted is 502 metres, over 4 samples. That is an upper
  bound on what it needs, not the figure — the Codex entry has that.

Finished here: Bacterium Cerbrus.
```

**Directive 47 will not tell you whether you have gone far enough, and that is a sourcing decision
rather than a gap.** The required spacing is published by the game in the species' own Codex entry.
Every machine-readable copy of it outside the game is a community wiki, and this project's rule is
that what a web search finds stays a sentence — the same sentence copied into a shipped table is
Directive 47 laundering somebody's forum post into its own voice, and it cannot be corrected without
a new release. The Codex is two clicks away and is authoritative; a table here would be neither.

**What it does instead is learn.** A specimen the game accepted is proof that the distance you
travelled was sufficient, so the smallest accepted gap is an upper bound on the requirement —
measured from your own play, carried with its sample size, and never presented as the figure itself.
It needs no source and gets better the more you sample.

Three things this rests on, all measured:

- **`ScanOrganic` carries no position at all** — not on any of the 632 events measured. Directive 47
  stamps one from `Status.json` as the event lands, so the distance is only as good as how closely
  those two writes track each other. A specimen taken with no position still counts; it simply
  carries no distance, because reporting zero would read as "you have not moved".
- **The run is `Log`, `Sample`, `Sample`, `Analyse`** on 94 of 101 runs. That is three specimens and
  a fourth event that banks them, so `Analyse` completes rather than counting as a fourth.
- **The body radius comes from `Status.json`**, not from a `Scan`. That is a correction to the
  original plan, which would have made the distance uncomputable on any body you had not scanned.
  Distances are great-circle, so the same angle is a shorter drive on a small moon than on a large
  planet.

### The trade-off nobody should discover after the flight

**A plotted route structurally cannot contain a first footfall.** An index only holds what has been
visited and uploaded; the 5× first-footfall bonus only happens where nobody has been. Those two facts
cannot both be satisfied, so every plot says so in its own last line rather than leaving you to work
it out somewhere expensive.

If undiscovered systems are what you are after, [read a system name](system-names.md) instead — that
works with no network at all, which is the point of it.

### Wire notes

The plotter is spansh's `api/exobiology/route`, submitted as a job and polled — the same protocol as
the neutron, Road to Riches and trade plots. Two traps here that the others do not have, both
established against the live service rather than guessed:

- **`from` is required, works, and comes home as `source`.** The endpoint echoes back the parameters
  it understood, and re-emits `from` under a different name. A caller checking its own parameters
  against that echo would conclude the origin was ignored and "fix" it into something that really is
  ignored.
- **`use_mapping_value` is silently dropped**, even though the Road to Riches plotter honours it. A
  dropped parameter is not an error — the plot just runs with the default.

The origin comes back as a stop with **no bodies**, and a loop adds the return leg the same way.
Dropping bodyless stops is what keeps "three systems worth landing on" from reading as five.

### Tools

#### `get_body_biology`

What your own surface scan found. Names genera; never quotes a value.

```json
{"type":"object","properties":{"body":{"type":"string","description":"The body name, or its short form such as \u00227 b\u0022. Leave out for the most recently scanned body that has biology on it."}},"required":[],"additionalProperties":false}
```

#### `get_sampling_progress`

How many specimens you have taken on this body, how far you have moved since the last one, and what
is already finished here. Never says whether the distance was enough.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `plot_exobiology_route`

A circuit through surveyed biology, with species and values.

```json
{"type":"object","properties":{"from":{"type":"string","description":"System to start from. Leave out to start where the Commander is."},"jump_range":{"type":"number","description":"Laden jump range in light years. Defaults to 50."},"loop":{"type":"boolean","description":"Whether the circuit returns to the start. Defaults to true."},"max_results":{"type":"integer","description":"How many systems to visit. Defaults to 10."},"min_value":{"type":"integer","description":"The least a body\u0027s biology must be worth to be a stop, in credits. Defaults to 1,000,000."},"radius":{"type":"number","description":"How far from the origin to look, in light years. Defaults to 200."}},"required":[],"additionalProperties":false}
```
