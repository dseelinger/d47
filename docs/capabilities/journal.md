---
title: Journal
group: Foundation
nav_order: 102
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and the class decides behaviour, not just
  appearance. HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under
  that class would silently become what the in-app panel draws on this page. The docs site styles
  the two identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to seeing what D47 knows about your game.</p>
<section>
<h2><span class="num">1</span> Nothing. It is already reading.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Journal">
 <rect x="20" y="16" width="840" height="212" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Journal</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Where Elite writes it</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">Saved Games\Frontier Developments</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">What D47 reads</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text-muted)">events, as they are written</text>
 <text x="44" y="222" font-size="15" fill="var(--text-muted)">No setting, no permission, no network. It reads the files the game already writes.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Open the Journal page to watch events arrive.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="The Journal tab">
 <rect x="20" y="16" width="840" height="210" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <rect x="20" y="16" width="840" height="42" rx="8" fill="var(--surface)"/>
 <text x="44" y="44" font-size="16" font-weight="700" fill="var(--accent)">Journal</text>
 <text x="44" y="92" font-size="16" fill="var(--text)">FSDJump — Kuwemaki</text>
 <text x="836" y="92" text-anchor="end" font-size="16" fill="var(--text-muted)">19:31:04</text>
 <text x="44" y="130" font-size="16" fill="var(--text)">Docked — Jameson Memorial</text>
 <text x="836" y="130" text-anchor="end" font-size="16" fill="var(--text-muted)">19:22:47</text>
 <text x="44" y="168" font-size="16" fill="var(--text)">MaterialCollected — Iron</text>
 <text x="836" y="168" text-anchor="end" font-size="16" fill="var(--text-muted)">19:18:02</text>
 <text x="44" y="222" font-size="15" fill="var(--text-muted)">Newest last, in the game's own vocabulary.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="It only knows what the game has written down.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">It only knows what the game has written down.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Elite does not journal everything. If a figure looks stale, the event probably never arrived.</text>
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
<p class="intro">Everything Directive 47 knows about your game, read straight from the files Elite already writes.</p>
<section>
<h2><span class="num">1</span> The answer comes off your own disk.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="Game state is read from the journal on your disk rather than from a website or produced by a model">
 <rect x="20" y="40" width="270" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="155" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOUR OWN DISK</text>
 <text x="155" y="112" text-anchor="middle" font-size="14" fill="var(--text-muted)">the files Elite already writes</text>
 <rect x="305" y="40" width="270" height="110" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2"/>
 <text x="440" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--danger)">NOT A WEBSITE</text>
 <text x="440" y="112" text-anchor="middle" font-size="14" fill="var(--text-muted)">nothing is asked of anybody</text>
 <rect x="590" y="40" width="270" height="110" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2"/>
 <text x="725" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--danger)">NOT A MODEL</text>
 <text x="725" y="112" text-anchor="middle" font-size="14" fill="var(--text-muted)">which would produce a plausible one</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">And what you are flying comes from your actual outfitting, not a table of hull figures —</text>
 <text x="440" y="224" text-anchor="middle" font-size="16" fill="var(--text)">which is what makes your Anaconda different from anyone else’s.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> When it does not know, it says what it is waiting for.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Rather than shrugging, it names the journal event that would supply the missing answer">
 <rect x="20" y="34" width="840" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="46" y="72" text-anchor="start" font-size="16" fill="var(--text)">“No Loadout event has been seen yet, so I do not know what you are</text>
 <text x="46" y="98" text-anchor="start" font-size="16" fill="var(--text)">flying. It is written when you enter the game or change your outfitting.”</text>
 <text x="46" y="138" text-anchor="start" font-size="15" fill="var(--text-muted)">“I have no ship list yet — it is written when you dock at a shipyard.”</text>
 <text x="440" y="204" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">“Started after Elite” and “this is unknowable” have different fixes.</text>
 <text x="440" y="236" text-anchor="middle" font-size="15" fill="var(--text-muted)">Carrier events only ever reach the owner’s journal, so having seen none is genuinely ambiguous — and it says so.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> It already knows where you are.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="A short summary of your situation rides with every question put to the model">
 <rect x="20" y="30" width="840" height="130" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="46" y="64" text-anchor="start" font-size="15" fill="var(--text-muted)">Location: Deciat, docked at Garay Terminal (Orbis)</text>
 <text x="46" y="92" text-anchor="start" font-size="15" fill="var(--text-muted)">Ship: Bold Endeavour, a Anaconda</text>
 <text x="46" y="120" text-anchor="start" font-size="15" fill="var(--text-muted)">Ship metrics: max jump 52.31 ly, fuel 28.5/32 t</text>
 <text x="46" y="148" text-anchor="start" font-size="15" fill="var(--text-muted)">This session: 14 jumps, 2,605,000 cr earned</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">That rides with every question, so “what should I do about this” has something to work with.</text>
 <text x="440" y="226" text-anchor="middle" font-size="15" fill="var(--text-muted)">It states only what the journal reported, and leaves a line out entirely rather than saying “unknown”.</text>
</svg>
<p class="body">Two Commanders on one machine stay separate: location, ship, carrier, fleet, materials and session totals are kept per Commander and never merged. And an event Directive 47 does not recognise is skipped and noted rather than breaking the read — an invented figure in your game state is worse than a missing one.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="specifications.html"><span class="ct">Specifications →</span><span class="cd">The counterpart: what a hull <em>can</em> do, rather than what yours is doing.</span></a>
<a class="card" href="ships.html"><span class="ct">Ships →</span><span class="cd">The fleet this reads, and the builds you plan against it.</span></a>
<a class="card" href="privacy.html"><span class="ct">Privacy →</span><span class="cd">Proof that none of this leaves the machine, computed rather than claimed.</span></a>
</div>
</div>
</div></div>

## The details

Everything Directive 47 knows about your game, read straight from the files Elite Dangerous
already writes.

Ask where your ships are and the answer comes off your own disk — not from a website, and not
from a model that will produce a plausible one.

### Ask for it

> "where am I"
> "what am I flying"
> "where is my carrier"
> "what ships do I own"
> "what materials am I carrying"
> "how have I done this session"

**Every one of those reaches the thing it names.** This capability has six answers and used to be
reached as a whole, with the first of the six taken by default — which is *where you are*. So
"where is my fleet carrier" was answered with where **you** were standing, under your own name,
with your own route attached, and so was every other question here.

**"My jump range" means yours, and only yours.** Ask about a ship — *"what's the Cobra Mk III's
jump range?"* — and this capability does not answer at all; the question goes to the model, which
has the specification tables and knows about hulls you do not own. It used to be caught here and
answered with your docking bay.

### What it can tell you

**Where you are** — system, body, docking state, and what you are doing:

```text
Fixture One is in Fixture Nebula Point, near Fixture Nebula Point A. Currently in supercruise.
Next jump: Fixture Reach (class M). 3 jumps left on the route.
```

**What you are flying**, from your actual outfitting rather than from a table of ship
specifications — which is what makes your Anaconda different from anyone else's:

```text
Flying Bold Endeavour, a Anaconda, ident JM-01.
maximum jump range 52.31 ly, fuel tank 32 t, cargo 12/64 t, unladen mass 1122.6 t, hull 94%.
Rebuy 9,694,497 cr.
38 modules fitted, 12 engineered.
Unpowered: int_cargorack_size4_class1.
```

**Where your carrier is.** Ask *"where is my carrier"* and that is all you get back — the system
and when it was last reported:

```text
Fleet carrier Bootstrap (K7Q-BQL) is in Colonia, as of 2026-09-05 16:36 UTC.
```

**Your fleet** is a separate question, and asking it is what makes Directive 47 read out ship
names. *"What ships do I own"*, *"my fleet"*, *"what ships are on the carrier"*:

```text
Fleet carrier Bootstrap (K7Q-BQL) is in Colonia, as of 2026-09-05 16:36 UTC.
Currently flying Bold Endeavour, a Anaconda.
2 other ships stored, as of 2026-09-05 16:36 UTC:
  Deciat:
    Wanderer (Asp Explorer)
  Shinrarta Dezhra:
    Mule (Python)
```

The system is a heading and each ship is alone on the line under it, because a fleet of a dozen
run together after one colon is a paragraph to read and a single breath to hear.

**What every ship carries**, not just the one you are sitting in. Each `Loadout` Elite writes is
kept, so *"which of my ships with at least 24 tonnes of cargo has the best jump range"* is a
question about figures already on disk:

```text
2 ships, each as it was last seen fitted — refit one and it reads as it was until you board it again. Jump range is the maximum on a full tank with an empty hold, as the game reports it, so a laden run is shorter.
  Bold Endeavour, a Anaconda — you are flying it, cargo 8 t, jump 52.31 ly, unladen mass 1122.6 t, fuel 32 t, worth 219,694,497 cr, rebuy 9,694,497 cr, as of 2026-09-05 16:36 UTC
  Mule, a Python — Shinrarta Dezhra, cargo 128 t, jump 21.44 ly, unladen mass 350.6 t, fuel 32 t, worth 61,204,110 cr, rebuy 3,060,205 cr, as of 2026-08-14 19:02 UTC
No loadout read, so not covered above: Wanderer (Asp Explorer).
```

Both caveats are in the answer because both are real. The jump range is the right figure for
ranking ships against each other and the wrong one for a run with the hold full, and a ship
refitted since you last boarded it is remembered as it was — which is what the timestamp on each
line is for, and what **Rescan my journals** on the [Ships](ships.md) page repairs. A ship you
own that no `Loadout` has been read for is named rather than left out, so a ranking is never
mistaken for the whole fleet.

**What is in module storage**, grouped by where it is, because the question underneath is nearly
always "can I fit it here, or do I have to fetch it":

```text
3 modules in storage, as of 3311-01-01 00:01 UTC.

Deciat:
  int powerplant size6 class5 (PowerPlant Boosted, grade 5), hot — 58,000 cr to transfer, 60 minutes

Shinrarta Dezhra (where you are):
  Beam Laser

In transit: Prismatic Shield Generator.
```

Ask for one thing and it narrows to it — `get_stored_modules` takes a fragment, so "have I got a
shield anywhere" works without the full catalogue name.

This is your own storage read off disk, and it is a different question from where a module can be
*bought*: that one is [galaxy search](galaxy.md) and it leaves the machine. This one works with
the galaxy search switched off, which is what a fresh install is.

Where Elite writes no readable name, Directive 47 keeps the symbol's words rather than inventing a
friendly one — `int powerplant size6 class5` above is ugly and true, and a guessed "6A Power
Plant" would be indistinguishable from the feature working.

**What you are carrying**, by category, with the top holdings rather than all of them — a full
list is over a hundred lines and no answer to a spoken question:

```text
1,204 units across 87 materials.
Raw: Carbon 47, Iron 36, Nickel 22, and 12 more.
Encoded: Distorted Shield Cycle Recordings 3.
On foot: 14 items in the backpack, 212 in the ship locker.
  Backpack consumables: Energy Cell 3, Medkit 2.
```

**How the session has gone**, counted from when you entered the game rather than from when the
journal file happens to have started:

```text
Session running 2.4 hours.
Earned 2,605,000 cr — 250,000 bounties, 1,400,000 trade, 880,000 exploration, 75,000 missions.
14 jumps, 384.2 ly travelled.
22 bodies scanned.
Balance at session start: 1,000,000 cr.
```

Every figure is a sum of amounts Elite reported. Nothing comes from a price table or a market
lookup, so the numbers are the ones you would recognise.

### When it does not know

It says which event it is waiting for rather than shrugging — "Directive 47 started after Elite"
and "this is unknowable" have different fixes:

```text
No Loadout event has been seen yet, so I do not know what you are flying. It is written when
you enter the game or change your outfitting.
```

```text
I have no ship list yet — it is written when you dock at a station with a shipyard.
```

```text
I have no module storage list yet — it is written when you dock at a station with outfitting.
```

**Where the carrier is comes from your journals, not from this session.** Directive 47 reads the
whole journal folder for carrier events at startup — `CarrierBuy`, `CarrierLocation`,
`CarrierJump` and `CarrierStats` — so the answer survives a restart, and it says when the carrier
was last reported so you can judge how stale that is. Elite writes those events when the carrier
jumps and when you open its management panel, and never in between; a carrier that has sat in one
system for a fortnight was last reported a fortnight ago, and the date says so rather than the
answer pretending it is fresh. Nothing is inferred from where your ships happen to be stored.

Carrier events only ever reach the owner's journal, so having seen none is genuinely ambiguous
and the answer says so rather than claiming you have no carrier:

```text
No fleet carrier appears in any journal read, so I do not know where one is.
```

And before any journal has been found at all — a fresh install, or Elite has never run:

```text
No Elite Dangerous journal has been detected yet.
```

### It already knows where you are

You do not have to say where you are before asking something else. A short summary of your
situation goes with every question you put to the model, so "what should I do about this" has
something to work with:

```text
Location: Deciat, docked at Garay Terminal (Orbis)
Status: docked
Ship: Bold Endeavour, a Anaconda
Ship metrics: max jump 52.31 ly, fuel 28.5/32 t, cargo 64/64 t
Cargo hold: Tritium 40t, Gold 16t, Beryllium 8t
This session: 14 jumps, 2,605,000 cr earned
```

It states only what the journal reported, and leaves a line out entirely rather than saying
"unknown".

**Your hold is reported as how full, not just how big.** Capacity on its own is what let a passing
remark call a laden hold half empty: there was a tank size in front of the model and no manifest to
check the claim against. The held figure and the heaviest few commodities now go with it, so a
comment about your cargo is a comment about your actual cargo. Until `Cargo.json` has been read for
the ship — still starting up, or driving the SRV under it — the line falls back to `cargo capacity
64 t`, which claims nothing about how full you are.

### More than one Commander

Two Commanders on one machine stay separate. Location, ship, carrier, fleet, materials and
session totals are kept per Commander and never merged.

### When Elite changes

Frontier adds and changes journal events several times a year. An event Directive 47 does not
recognise is skipped and noted rather than breaking the read, and a field that is missing or has
changed shape reads as absent rather than as zero — an invented figure in your game state is
worse than a missing one.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

Most take no arguments — each reports on the Commander currently being tailed — so they share a
schema:

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

`get_location`, `get_ship`, `get_materials` and `get_session_summary`.

`get_fleet` answers about the carrier, and lists the ships only when it was asked to:

```json
{"type":"object","properties":{"ships":{"type":"boolean","description":"List the ships the Commander owns and which system each one is stored in. Set it only when they asked what ships they have or what ships are in a system. Asking where the carrier is is not that question."}},"required":[],"additionalProperties":false}
```

`get_fleet_loadouts` reads the remembered loadouts rather than the flown one, and filters and ranks
in the tool so the model is comparing figures rather than deriving them:

```json
{"type":"object","properties":{"min_cargo":{"type":"integer","description":"List only ships with at least this many tonnes of cargo capacity."},"order_by":{"type":"string","description":"Rank the ships by this figure, largest first. Listed by name otherwise.","enum":["jump_range","cargo"]}},"required":[],"additionalProperties":false}
```

`get_stored_modules` takes an optional fragment to narrow the list:

```json
{"type":"object","properties":{"module":{"type":"string","description":"Narrow the list to stored modules whose name contains this \u2014 for example \u0022shield\u0022 or \u0022Frame Shift Drive\u0022. Leave it out for the whole store."}},"required":[],"additionalProperties":false}
```

`StoredModules`, like `StoredShips`, is a complete snapshot rather than a delta, so the store is
replaced wholesale on each one. Merging would keep modules that have since been sold or fitted,
and the failure that causes is a Commander flying somewhere to collect something that is not
there.

D47 watches the journal folder Elite writes to
(`%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous` by default; overridable for
development with `D47_JOURNAL_DIR`) and always tails the newest file by filename rather than by
modification time — the filename encodes the session start, and that is what survives being
copied.

`Backpack.json` and `ShipLocker.json` are state rather than journal: Elite rewrites them in
place, so they are re-read only when their last-write time moves, and a file caught mid-write is
retried on the next tick rather than skipped.

Reading is pull-based; nothing here owns a thread or a timer. The tick loop calls `Poll()` at
roughly 10 Hz and a test calls it directly, which is what makes journal behaviour testable
against a recorded session with no game, headset or hardware in the room.

The game-state block sits at prompt position 7, below the cache breakpoint, so it changes every
turn without invalidating anything above it. Being re-billed every turn is what shapes it: terse,
facts only, and absent rather than empty when nothing is known — a model told nothing is known
will say so unprompted. It carries a header naming it as untrusted data describing the world
rather than instructions, because that is the point at which untrusted text actually arrives.

The credit balance in that block is the spendable figure from `Status.json` — what the game's own
panel shows, not total wealth including ships and holdings — refreshed every tick with the rest of
the block. It is absent rather than zero when Elite is sitting at its main menu, and absent once
the read is more than ten minutes old: `Status.json` outlives the game exiting, still saying "in
the ship", so without an age on the read a game shut last night would report its balance as
today's. Spoken, it is banded the way a Commander says a credit total ("8.9 billion");
the exact digits are there for when you ask how much exactly.

The block also says what the frame shift drive is doing, when it is doing something worth saying:
mass locked, cooling down after a jump, or charging. Mass lock is the one that matters, because a
jump key pressed against it succeeds as a keypress and moves nothing — so the line names the way
out as well as the state, and says outright that a jump must not be reported as done while the lock
holds. It is silent the rest of the time, and carries the same age limit as the balance.

State is keyed by Frontier ID from the journal's own `Commander` event. Ship metrics come from
the `Loadout` event rather than a specification table: a table is a second source of truth that
goes stale on every balance pass and cannot know what this Commander engineered. Elite has
already done the mass-and-FSD arithmetic and written the answer into `MaxJumpRange`.

"Session" means since the last `LoadGame`. Elite rolls the journal into a continuation file
during a long session without re-emitting it, so resetting on the file would silently restart the
totals mid-flight.

</details>
