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
  the two identically (main.scss extends one from the other); the app draws this band first and the How it works band after it.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to seeing what D47 knows about your game.</p>
<section>
<h2><span class="num">1</span> Nothing. It is already reading.</h2>
<p class="say">Do nothing; D47 is already reading the journal.</p>
<p class="expect">It reads the files Elite writes in Saved Games, with no setting, permission or network.</p>
<svg viewBox="0 0 880 252" role="img" aria-label="Journal">
 <rect x="20" y="16" width="840" height="212" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Journal</text>
 <rect x="44" y="70" width="792" height="42" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Where Elite writes it</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">Saved Games\Frontier Developments</text>
 <rect x="44" y="126" width="792" height="42" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">What D47 reads</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text-muted)">events, as they are written</text>
 <text x="44" y="222" font-size="15" fill="var(--text-muted)">No setting, no permission, no network. It reads the files the game already writes.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Step the Transcript readings to Journal File.</h2>
<p class="say">Open the Transcript tab and step the readings to Journal File.</p>
<p class="expect">Each event the game writes appears as a sentence, newest first. The Raw switch shows the event names, such as FSDJump and Docked.</p>
<svg viewBox="0 0 880 246" role="img" aria-label="The Journal File reading">
 <rect x="20" y="16" width="840" height="210" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <rect x="20" y="16" width="840" height="42" fill="var(--surface)"/>
 <text x="44" y="44" font-size="16" font-weight="700" fill="var(--accent)">Journal File</text>
 <text x="44" y="92" font-size="16" fill="var(--text)">Jumped to Kuwemaki</text>
 <text x="836" y="92" text-anchor="end" font-size="16" fill="var(--text-muted)">19:31:04</text>
 <text x="44" y="130" font-size="16" fill="var(--text)">Docked at Jameson Memorial</text>
 <text x="836" y="130" text-anchor="end" font-size="16" fill="var(--text-muted)">19:22:47</text>
 <text x="44" y="168" font-size="16" fill="var(--text)">Collected Iron</text>
 <text x="836" y="168" text-anchor="end" font-size="16" fill="var(--text-muted)">19:18:02</text>
 <text x="44" y="222" font-size="15" fill="var(--text-muted)">Newest first. The Raw switch shows the game's own event names.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Check Journal File when a figure looks out of date.</p>
<p class="expect">If the event is not listed, Elite never wrote it, which is why the figure is stale.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="It only knows what the game has written down.">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
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
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Everything Directive 47 knows about your game, read straight from the files Elite already writes.</p>
<section>
<h2><span class="num">1</span> The answer comes off your own disk.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="Game state is read from the journal on your disk rather than from a website or produced by a model">
 <rect x="20" y="40" width="270" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="155" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOUR OWN DISK</text>
 <text x="155" y="112" text-anchor="middle" font-size="14" fill="var(--text-muted)">the files Elite already writes</text>
 <rect x="305" y="40" width="270" height="110" fill="var(--surface)" stroke="var(--danger)" stroke-width="2"/>
 <text x="440" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--danger)">NOT A WEBSITE</text>
 <text x="440" y="112" text-anchor="middle" font-size="14" fill="var(--text-muted)">nothing is asked of anybody</text>
 <rect x="590" y="40" width="270" height="110" fill="var(--surface)" stroke="var(--danger)" stroke-width="2"/>
 <text x="725" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--danger)">NOT A MODEL</text>
 <text x="725" y="112" text-anchor="middle" font-size="14" fill="var(--text-muted)">which would produce a plausible one</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">And what you are flying comes from your actual outfitting, not a table of hull figures —</text>
 <text x="440" y="224" text-anchor="middle" font-size="16" fill="var(--text)">which is what makes your Anaconda different from anyone else’s.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> When it does not know, it says what it is waiting for.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Rather than shrugging, it names the journal event that would supply the missing answer">
 <rect x="20" y="34" width="840" height="124" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
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
 <rect x="20" y="30" width="840" height="130" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="46" y="64" text-anchor="start" font-size="15" fill="var(--text-muted)">Location: Deciat, docked at Garay Terminal (Orbis)</text>
 <text x="46" y="92" text-anchor="start" font-size="15" fill="var(--text-muted)">Ship: Bold Endeavour, a Anaconda</text>
 <text x="46" y="120" text-anchor="start" font-size="15" fill="var(--text-muted)">Ship metrics: max jump 52.31 ly, fuel 28.5/32 t</text>
 <text x="46" y="148" text-anchor="start" font-size="15" fill="var(--text-muted)">This session: 14 jumps, 2,605,000 cr earned</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">That rides with every question, so “what should I do about this” has something to work with.</text>
 <text x="440" y="226" text-anchor="middle" font-size="15" fill="var(--text-muted)">It states only what the journal reported, and leaves a line out entirely rather than saying “unknown”.</text>
</svg>
<p class="body">An event Directive 47 does not recognise is skipped and noted, so the read never breaks. Each Commander's state is kept separate.</p>
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
> "what are my career statistics"
> "how many merc coins do I have"
> "what's my reputation with the Empire"
> "how much exploration data am I carrying"
> "reset unsold exploration"

**Every one of those reaches the thing it names.** "Where is my fleet carrier" is answered with
the carrier's system, not with where you are standing.

**"My jump range" means yours, and only yours.** Ask about a ship — *"what's the Cobra Mk III's
jump range?"* — and this capability does not answer; the question goes to the model, which has the
specification tables and knows about hulls you do not own.

### What it can tell you

**What an event changed.** Selecting an event on the Journal File reading (Transcript › Journal File) shows a band titled WHAT THIS
CHANGED, between What this means and Every field, saying what d47 took from it. "Nothing in d47's
picture changed." is a normal answer: most events carry nothing d47 keeps. An event that never
reached the Commander's state has no band.

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
24 modules fitted, 9 engineered.
Hardpoints:
  Huge Hardpoint 1: 4A Plasma Accelerator, fixed — Efficient Weapon, grade 5, Thermal Conduit
  Large Hardpoint 1: 3C Multi-Cannon, gimballed — Overcharged Weapon, grade 5, Auto Loader
  ...
Core Internal:
  Armour: Military Grade Composite — Heavy Duty Armour, grade 5, Deep Plating
  Power Plant: 8A Power Plant — Overcharged Power Plant, grade 5
  Frame Shift Drive: 6A Frame Shift Drive — Increased FSD Range, grade 5, Mass Manager
  ...
Optional Internal:
  Compartment 1 (size 7): 7A Shield Generator — Reinforced Shield Generator, grade 5
  Compartment 4 (size 4): 4E Cargo Rack
  ...
Unpowered: 4E Cargo Rack.
```

Paint jobs, decals, name plates and the other cosmetics are left out of the list and the count: only
what sits in one of the hull's outfitting slots is listed, under the outfitting screen's headings and
in its order.

**Name another ship you own** and the same report comes off `loadouts.json` — every `Loadout`
Elite has ever written for it, kept whether or not you are aboard right now — dated so a ship
refitted since you last boarded it is visibly stale:

```text
Campaigner, a Panther Clipper MkII, as of 2026-08-01 09:00 UTC.
maximum jump range 48.14 ly, fuel tank 128 t, cargo capacity 1200 t, unladen mass 1575.4 t.
27 modules fitted, 6 engineered.
Hardpoints:
  ...
```

A ship you own that no `Loadout` has been read for says so rather than describing a different one:

```text
Campaigner (Panther Clipper MkII) — no loadout has been read for it yet. That is written the
next time you board it.
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
2 ships, each as it was last seen fitted — refit one and it reads as it was until you board it again. Jump range is the maximum with one jump's fuel and an empty hold, as the game reports it, so a full tank or a laden hold is shorter.
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

**The BGS work you have done**, per system and faction, since the last BGS tick — *"what's my BGS
work"*, *"what have I done for the BGS"*. It comes from the last seven days of your journals, so it
survives a restart. With no tick time it covers the last 24 hours and says so, naming the BGS tick
time setting in Galaxy. At most three system and faction pairs are named, the one with the most
entries first, and the rest are counted:

```text
Since the BGS tick at 16:14 UTC, 3 hours ago:
Diamond Frogs in HIP 97704: missions +5 and -1; bounties 117,480 credits; trade 2,400,000 credits sold for 310,000 profit; cartography 506,832 credits; influence 23.1% before the tick, 24.0% when you were there at 18:02 UTC.
and 4 more factions.
```

Name a faction or a system and only the matching pairs come back, all of them. When a tick time is
known and your journals hold a reading from before it and one from after, each line adds the faction's
influence before the tick and at your latest visit, or "unchanged" when the two readings agree. Elite
updates systems for some hours after the tick, so that is what the readings showed, not a result of
your work.

**Your career statistics**, from the journal's `Statistics` event — every section Elite writes, or
one section named, with each figure under a readable name and in its unit — credits, Merc Coins,
light years, or hours and minutes:

```text
Career statistics, as of 2026-09-05 16:36 UTC:

Exploration:
  Total Hyperspace Distance: 4557.72 ly
  Time Played: 54 minutes
```

Name one section and only that one comes back — *"what have I earned from exobiology"*:

```text
Exobiology, as of 2026-09-05 16:36 UTC:
  Organic Data Profits: 4,204,000 cr
```

*"How many merc coins do I have"* reads out the bank account section, where Elite keeps the Merc
Coin ledger beside the credit balance:

```text
Bank Account, as of 2026-09-26 10:00 UTC:
  Current Wealth: 1,234,567 cr
  Merc Coins Current: 46 Merc Coins
  Merc Coins Total Earned: 1,046 Merc Coins
  Merc Coins Total Spent: 1,000 Merc Coins
  Merc Coins Spent On Merc Gear: 750 Merc Coins
  Merc Coins Spent On Engineering: 250 Merc Coins
```

The journal carries no weekly earning allowance and no cap, so d47 does not say how many Merc Coins
are left to earn this week.

`Statistics` is written about 30 seconds after `LoadGame`, once per session:

```text
The game has not reported your career statistics this session.
```

**Your reputation and navy rank** — each superpower as a band and a number out of 100, with the
Imperial and Federal navy ranks beside the Empire and the Federation. Ask for one superpower —
*"what's my reputation with the Empire"*, *"what's my imperial rank"* — and only that one comes
back:

```text
Reputation, as of 2026-09-23 10:00 UTC:
  Empire: Cordial, 28 of 100. Imperial Navy rank: Outsider, 93% into it.
```

The superpower figures come from the `Reputation` event Elite writes when you enter the game.

**A minor faction, by name** — *"what's my reputation with Mother Gaia"*. The reading comes from
the last time you arrived in a system where that faction is present, so the answer always says
when that was:

```text
Mother Gaia: Friendly, 43 of 100, last read on 2026-09-20.
```

Part of a name is enough when only one faction you have met matches it. When several do, they
are listed by name rather than one being picked for you.

**The bodies you have mapped and not sold** — every body you finished mapping with the Detailed
Surface Scanner since you last sold its system's data, each with about what Universal Cartographics
will pay for it:

```text
About 3,277,078 credits of mapped bodies unsold, from 3 bodies. 3 of them were mapped efficiently. This is an estimate, and it leaves out stars and bodies scanned but not mapped, so Universal Cartographics will pay more.
```

Elite never writes what a body is worth; only a sale carries credits, and only for the whole sale.
The value is computed from the body's scan with the formula players have worked out from sales —
a figure per planet class, raised for a terraformable body and for its mass, multiplied for
mapping, for being first to map it, for being first to discover it, and for mapping it within the
probe target. It is spoken as an estimate for that reason. The Detailed scan Elite writes straight
after a mapping can say the body had not been mapped when an earlier scan said it had; a body
counts as already mapped or already discovered if any scan up to the mapping said so.

When you finish mapping a body, the [Mapped body value](callouts.md#mapping) callout says what it is
worth and the new total. The total goes down when you sell: `MultiSellExplorationData` removes every
held body in each system it names, because the sale names systems rather than bodies. It goes to
zero when you die, and when you say "reset unsold exploration", which the model cannot do for you.
It is rebuilt from your journals at every start, so it is right after a restart.

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

### Combat stats {#combat-stats}

The Combat stats ledger (Commander › Ledgers › Combat stats) lists your kills and the credits they were
awarded, counted from the journals on this PC. Choose a period (24 hours, 7 days, 30 days or Everything) and a grouping (Enemy ship,
Own ship, System or Faction). Press a column heading to sort, and the panel search narrows the rows by name.

Kills in conflict zones carry no ship type, so they are grouped as "In conflict zones, ship type not recorded".
Under Faction, a kill counts the faction whose ship you destroyed, and its credits count the faction that paid;
a kill that paid several factions is split among them.

While D47 is still reading your older journals the page says "D47 is reading your journals for past kills.
The counts appear here when it finishes." If that reading failed, **History not finished** gives the time the
counts start at. These counts come from the journals, not from Elite's own career figures, which are on
Commander › Statistics, and count vouchers claimed.

### Codex pictures {#codex-pictures}

Stock pictures of codex entries are published as files on a GitHub release. Directive 47 fetches the list
the first time a codex page wants a picture in a session, and each picture the first time a page shows it,
and keeps them in `data\codex-pictures`. A picture's file name gives its codex entry and region; nothing
else is sent: no key, no Commander name, no position and nothing else from your journal.

Pictures you save yourself are kept in the same folder and never leave this PC.

To make your own, say "take a picture" in game, then open the entry's page in the main window (Commander › Ledgers › Codex, then the
region and the entry) and press **Choose a picture**. The captures are listed newest first,
those taken with this entry ahead of the rest. Press one to crop it: drag inside the square to move it and
drag a corner to resize it. The square stays inside the capture and is never smaller than 384 pixels of it.
**Save** keeps a 768 by 768 picture, the capture it was cut from and the square, in `data\codex-pictures\local\<Frontier ID>`;
**Cancel** keeps nothing.

With a picture of your own for the region, the entry's page also offers **Re-crop**, which opens the kept
capture with the saved square, and **Replace**. **Use the stock picture**, or **Remove my picture** when there
is no stock one, deletes your picture and its capture. These are in the main window only; the big panel shows
the picture and none of them. An entry in a region D47 cannot name has none of them.

Pressing the picture on an entry's page shows it full size over the window, as a speaker picture does.
**Close** or Esc puts it away. The family placeholder opens nothing.

**Codex pictures** in the settings turns it off. Nothing is fetched and a codex page shows only the
pictures you saved yourself, even for stock pictures already on disk, which are kept.

### Share codex pictures {#share-codex-pictures}

Pictures you save are kept for the active Commander, in `data\codex-pictures\local\<Frontier ID>`, and a
codex page shows only that Commander's. Pictures saved before this setting existed moved into the folder of the
first Commander whose journal was read afterwards. With no Commander active, no picture of your own is shown
and **Choose a picture** is not offered.

**Share my codex pictures with my other Commanders** in the settings is off by default. Ticked, every
Commander's pictures are one pool: a page shows the active Commander's picture where there is one and another
Commander's where there is not. **Re-crop**, **Replace**, **Remove my picture** and **Use the stock picture**
act on the active Commander's picture only. Stock pictures are shared either way.

### Open new codex entries {#open-new-codex-entries}

When you log a codex entry for the first time, the panel opens that entry's page, in the window and on the
headset panel, under Commander › Ledgers › Codex, in the region and then the entry. It is on by default. A surface showing a
dialog page or a keyboard entry stays where it is, and the others still move. When several entries are
logged at once, the last one's page is shown. Entries replayed from the journal when D47 starts open nothing.

**Open new codex entries** in the settings turns it off.

### Codex publish token {#codex-publish-token}

This is for the maintainer of the stock codex pictures. The stock pictures are files on releases of the
public repository `dseelinger/d47-codex-pictures`, listed by `index.json` on the release `codex-pictures-1`.
With a token, a codex entry's page in the main window can put a picture on that list for every Commander, or
take one off it.

To make the token, create a fine-grained personal access token on GitHub with access to the repository
`dseelinger/d47-codex-pictures` only and the permission **Contents: Read and write**. Paste it into **Codex
publish token** in the advanced settings, where it is stored encrypted for this Windows account. Without a
stored token, Directive 47 reads the environment variable `D47_CODEX_PUBLISH_TOKEN`. The repository needs at
least one commit, such as a README, before the first publish, because GitHub cannot tag an empty repository.

**Publish** shows when your own picture for the entry and region is the one on show. It uploads the picture
to the highest-numbered `codex-pictures-N` release with fewer than 1000 files, creating the next one when
none has room, then adds the picture to `index.json`, and deletes the stock picture it replaced. Your picture
is then deleted from this PC, so the page shows the stock picture every install now sees; the kept capture and
square stay, so **Re-crop** still works.

**Unpublish** shows when there is a stock picture for the entry and region. It asks first, then takes the
picture off `index.json` and deletes it from the release. Pictures Commanders saved themselves are kept.

One publish or unpublish runs at a time. A failure stops at the step it reached, shows GitHub's message,
and changes no file on this PC. Neither button shows on the big panel or without a token.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

Most take no arguments — each reports on the Commander currently being tailed — so they share a
schema:

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

`get_location`, `get_materials` and `get_session_summary`, and the pair for the mapped bodies not
yet sold: `get_unsold_exploration` answers the total, and `clear_unsold_exploration` sets it to zero.
The clear is Protected: the keyword phrase, the panel and a hotkey reach it, and the model is
refused.

`get_bgs_work` takes an optional faction and system, each compared without regard to case:

```json
{"type":"object","properties":{"faction":{"type":"string","description":"Report only the work done for this faction. Omit for every faction."},"system":{"type":"string","description":"Report only the work done in this system. Omit for every system."}},"required":[],"additionalProperties":false}
```

`get_combat_stats` answers how many kills the Commander has made and the credits awarded for them, from
every journal on this PC. `by` groups by `enemy`, `ship`, `system` or `faction`, `period` limits it to the
last `day`, `week` or `month`, and `name` narrows the grouping, without regard to case. With no `by` it
gives the top entry of each grouping; with one it names the top three and counts the rest. Credits are
the bounties and combat bonds awarded at the kill, not what was redeemed.

```json
{"type":"object","properties":{"by":{"type":"string","description":"The grouping to report. Omit for the top entry of each.","enum":["enemy","ship","system","faction"]},"name":{"type":"string","description":"Report only the entries of the grouping whose name contains this, without regard to case."},"period":{"type":"string","description":"The last day, week (7 days) or month (30 days) before now, or all of the record. Default all.","enum":["day","week","month","all"]}},"required":[],"additionalProperties":false}
```

`get_credit_ledger` answers where the Commander's credits came from and went, from every journal on this
PC. `period` is the last `day`, `week` or `month`, or `all`, and defaults to `week`; `kind` limits it to one
kind of transaction. The first line gives the totals in and out and the balance, then the top three kinds
each way with a count of the rest. Bounties and combat bonds count when redeemed, and are spoken as
"bounties and bonds redeemed". Carrier bank transfers are spoken as moved, not spent. A gap between the
journal and the game's own balance is spoken only from a million credits.

```json
{"type":"object","properties":{"kind":{"type":"string","description":"Report only this kind of transaction.","enum":["trade","exploration","missions","combat","vouchers","rescue","community_goals","colonisation","powerplay","ships","modules","upkeep","rebuy","fines","crew","on_foot","engineers","carrier"]},"period":{"type":"string","description":"The last day, week (7 days) or month (30 days) before now, or all of the record. Default week.","enum":["day","week","month","all"]}},"required":[],"additionalProperties":false}
```

`get_codex_ledger` answers how many codex entries the Commander logged, and how many were new to their
codex, from every journal on this PC. `period` is `trip` (since the last sale of exploration data, the
default), `session` or `all`; `region` limits it to a codex region by name, or `here` for the region the
Commander is in. The first line gives the period and the counts, then the top three categories by new
entries and, without `region`, the top three regions, each with a count of the rest. It says nothing about
codex vouchers, which the journal values only when they are redeemed.

```json
{"type":"object","properties":{"period":{"type":"string","description":"Since the last sale of exploration data (trip), since this session began, or all of the record. Default trip.","enum":["trip","session","all"]},"region":{"type":"string","description":"Report only this codex region, by name, or here for the region the Commander is in."}},"required":[],"additionalProperties":false}
```

`get_codex_discoveries` answers which codex entries are known in a region and which the Commander has not
logged. `region` is a codex region by name, or `here` (the default) for the region the Commander is in. The
first line gives how many entries are known, logged and missing; then the missing count by subcategory and the
three most-reported missing entries by name, with a count of the rest. Logged means the entry was logged in
that region anywhere in the record, not in a period. The answer says when the record starts, because an entry
logged before the oldest journal reads as missing, and ends with the coverage line:
"Stars, Thargoid sites and planets other than the green gas giants are not covered."

```json
{"type":"object","properties":{"region":{"type":"string","description":"The codex region, by name, or here for the region the Commander is in. Default here."}},"required":[],"additionalProperties":false}
```

`get_body_geology` answers which geological features a body likely has, predicted from its class and
volcanism, and whether each is in the Commander's codex for the current region. `body` is a body name or its
short form; left out, it is the most recently seen body with geological signals. It says when the body has no
geological signals, when the geology table has no feature for its class and volcanism, and when there is no
scan of the body. When the region is known, each feature is marked logged or not logged and the answer says
when the record starts, because an entry logged before then reads as not logged. It is a prediction: the codex
entry on the surface decides.

```json
{"type":"object","properties":{"body":{"type":"string","description":"The body name, or its short form such as \u00227 b\u0022. Leave out for the most recently seen body that has geological signals."}},"required":[],"additionalProperties":false}
```

`get_ship` answers about the one being flown by default, and about any other ship the Commander
owns when named — from the loadout last seen for it, dated, rather than refusing (#108):

```json
{"type":"object","properties":{"jump_range":{"type":"boolean","description":"Answer with the unladen jump range alone. Set it only when they asked for the jump range and nothing else about the ship."},"ship":{"type":"string","description":"Which ship, by name or hull. Omit for the one currently flown."}},"required":[],"additionalProperties":false}
```

With `jump_range` set, `get_ship` answers with one line, `Unladen jump range is 52.31 ly.`, for the flown ship or the named one. "Jump range", "my jump range" and "what is my jump range" ask it directly. A remembered ship's line carries its as-of date, and a ship with no recorded jump range says it is not known yet.

Spoken, `get_ship` gives the ship's line and metrics, the module count, and the unpowered modules as
a count with at most three named, then points to Asset Mgmt › Ships for the full loadout. The model
still receives every fitted module. The answer also opens that ship's page under Asset Mgmt › Ships
on the panel and in the headset, unless a chooser is open there.

`get_fleet` answers about the carrier, and lists the ships only when it was asked to:

```json
{"type":"object","properties":{"ships":{"type":"boolean","description":"List the ships the Commander owns and which system each one is stored in. Set it only when they asked what ships they have or what ships are in a system. Asking where the carrier is is not that question."}},"required":[],"additionalProperties":false}
```

`get_fleet_loadouts` reads the remembered loadouts rather than the flown one, and filters and ranks
in the tool so the model is comparing figures rather than deriving them. With `mining` it lists only
ships fitted with a mining laser or Volley Repeater, a collector controller and a refinery, each with
those modules named; a ship whose loadout lists no modules is reported as not seen fitted:

```json
{"type":"object","properties":{"min_cargo":{"type":"integer","description":"List only ships with at least this many tonnes of cargo capacity."},"mining":{"type":"boolean","description":"List only ships fitted for tritium mining \u2014 a mining laser or Volley Repeater, a collector controller and a refinery \u2014 each with the modules that show it."},"order_by":{"type":"string","description":"Rank the ships by this figure, largest first. Listed by name otherwise.","enum":["jump_range","cargo"]}},"required":[],"additionalProperties":false}
```

`get_stored_modules` takes an optional fragment to narrow the list:

```json
{"type":"object","properties":{"module":{"type":"string","description":"Narrow the list to stored modules whose name contains this \u2014 for example \u0022shield\u0022 or \u0022Frame Shift Drive\u0022. Leave it out for the whole store."}},"required":[],"additionalProperties":false}
```

Spoken, `get_stored_modules` gives the count, the number of systems and at most three modules by
name, those in the Commander's system first, then the number in transit. The model still receives
every stored module.

`get_commander_statistics` takes an optional section, one of the sixteen `Statistics` writes:

```json
{"type":"object","properties":{"section":{"type":"string","description":"One section of the career statistics to report. Omit to report every section.","enum":["Bank_Account","Combat","Crime","Smuggling","Trading","Mining","Exploration","Passengers","Search_And_Rescue","Squadron","Crafting","Crew","Multicrew","Material_Trader_Stats","FLEETCARRIER","Exobiology"]}},"required":[],"additionalProperties":false}
```

`get_crime_status` answers *"am I wanted"*, *"my bounties"*, *"my fines"*, *"what do I owe"* and
*"crime status"*. It takes no arguments. It reads three things and says where each comes from:

- The legal state now, as Elite writes it in `Status.json`, and whether the Commander is on foot.
- The unpaid fines and bounties the journal records, for each faction on foot and on the ship being
  flown, largest first, each with the date of its newest crime. Other ships carrying debts are
  counted, not named. A spoken answer names at most three factions and counts the rest.
- Notoriety from the last `Statistics` event, with the time it was read, since Elite writes it only
  at login. With no `Statistics` event yet, notoriety is left out.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Amounts are what the journal records. A fine paid with no `CommitCrime` behind it is not in the
ledger, so the answer says so. Paying a faction's fines or bounties clears its entry, as it does for
the arrival reminder.

`get_standing` takes an optional superpower or minor faction, whole or part of its name:

```json
{"type":"object","properties":{"faction":{"type":"string","description":"A superpower or a minor faction, whole or part of its name. Omit for the four superpowers and both navy ranks."}},"required":[],"additionalProperties":false}
```

A faction's full name is also a phrase the router matches without the model, one set per faction
a reading is held for (`JournalCapability.StandingPhrases`). The bands are
`ReputationBands` in `Knowledge/UnlockTest.cs`, the same thresholds engineer unlocks are tested
against.

`get_ranks` takes no arguments and reports every career and navy rank with its progress and the next rank, whether or not any rank goal is kept:

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
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
