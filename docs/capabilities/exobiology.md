# Exobiology

Plot a circuit through known biology, and read back what your own surface scan found on the body you
are at.

> "plot me an exobiology route"
> "what biology is on this body"
> "is this body worth landing on"
> "what did the scan find here"

Two halves, from two different sources, answering two different questions. Keeping them apart is the
whole design — because one of them can quote money and the other cannot.

## After the scan: what the game already told you

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

## Before you go: a circuit through known biology

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

## On the surface: how many, and how far

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

## The trade-off nobody should discover after the flight

**A plotted route structurally cannot contain a first footfall.** An index only holds what has been
visited and uploaded; the 5× first-footfall bonus only happens where nobody has been. Those two facts
cannot both be satisfied, so every plot says so in its own last line rather than leaving you to work
it out somewhere expensive.

If undiscovered systems are what you are after, [read a system name](system-names.md) instead — that
works with no network at all, which is the point of it.

## Wire notes

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

## Tools

### `get_body_biology`

What your own surface scan found. Names genera; never quotes a value.

```json
{"type":"object","properties":{"body":{"type":"string","description":"The body name, or its short form such as \u00227 b\u0022. Leave out for the most recently scanned body that has biology on it."}},"required":[],"additionalProperties":false}
```

### `get_sampling_progress`

How many specimens you have taken on this body, how far you have moved since the last one, and what
is already finished here. Never says whether the distance was enough.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `plot_exobiology_route`

A circuit through surveyed biology, with species and values.

```json
{"type":"object","properties":{"from":{"type":"string","description":"System to start from. Leave out to start where the Commander is."},"jump_range":{"type":"number","description":"Laden jump range in light years. Defaults to 50."},"loop":{"type":"boolean","description":"Whether the circuit returns to the start. Defaults to true."},"max_results":{"type":"integer","description":"How many systems to visit. Defaults to 10."},"min_value":{"type":"integer","description":"The least a body\u0027s biology must be worth to be a stop, in credits. Defaults to 1,000,000."},"radius":{"type":"number","description":"How far from the origin to look, in light years. Defaults to 200."}},"required":[],"additionalProperties":false}
```
