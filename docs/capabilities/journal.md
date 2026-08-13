---
title: Journal
---

Everything Directive 47 knows about your game, read straight from the files Elite Dangerous
already writes.

Ask where your ships are and the answer comes off your own disk — not from a website, and not
from a model that will produce a plausible one.

## Ask for it

> "where am I"
> "what am I flying"
> "where is my carrier"
> "what ships do I own"
> "what materials am I carrying"
> "how have I done this session"

## What it can tell you

**Where you are** — system, body, docking state, and what you are doing:

```text
Fixture One is in Fixture Nebula Point, near Fixture Nebula Point A. Currently in supercruise.
Next jump: Fixture Reach (class M). 3 jumps left on the route.
```

**What you are flying**, from your actual outfitting rather than from a table of ship
specifications — which is what makes your Anaconda different from anyone else's:

```text
Flying Bold Endeavour, a Anaconda, ident JM-01.
maximum jump range 52.31 ly, fuel tank 32 t, cargo capacity 64 t, unladen mass 1122.6 t, hull 94%.
Rebuy 9,694,497 cr.
38 modules fitted, 12 engineered.
Unpowered: int_cargorack_size4_class1.
```

**Your fleet**, and your carrier if you have one:

```text
Fleet carrier Bootstrap (K7Q-BQL) is in Colonia.
Currently flying Bold Endeavour, a Anaconda.
2 other ships stored:
  Deciat: Wanderer (asp)
  Shinrarta Dezhra: Mule (python)
```

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

## When it does not know

It says which event it is waiting for rather than shrugging — "Directive 47 started after Elite"
and "this is unknowable" have different fixes:

```text
No Loadout event has been seen yet, so I do not know what you are flying. It is written when
you enter the game or change your outfitting.
```

```text
I have no ship list yet — it is written when you dock at a station with a shipyard.
```

Carrier events only ever reach the owner's journal, so having seen none is genuinely ambiguous
and the answer says so rather than claiming you have no carrier:

```text
No fleet carrier events have been seen this session.
```

And before any journal has been found at all — a fresh install, or Elite has never run:

```text
No Elite Dangerous journal has been detected yet.
```

## It already knows where you are

You do not have to say where you are before asking something else. A short summary of your
situation goes with every question you put to the model, so "what should I do about this" has
something to work with:

```text
Location: Deciat, docked at Garay Terminal (Orbis)
Status: docked
Ship: Bold Endeavour, a Anaconda
Ship metrics: max jump 52.31 ly, fuel 28.5/32 t, cargo capacity 64 t
This session: 14 jumps, 2,605,000 cr earned
```

It states only what the journal reported, and leaves a line out entirely rather than saying
"unknown".

## More than one Commander

Two Commanders on one machine stay separate. Location, ship, carrier, fleet, materials and
session totals are kept per Commander and never merged.

## When Elite changes

Frontier adds and changes journal events several times a year. An event Directive 47 does not
recognise is skipped and noted rather than breaking the read, and a field that is missing or has
changed shape reads as absent rather than as zero — an invented figure in your game state is
worse than a missing one.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

Every tool here takes no arguments — each reports on the Commander currently being tailed — so
they share a schema:

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

`get_location`, `get_ship`, `get_fleet`, `get_materials` and `get_session_summary`.

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

State is keyed by Frontier ID from the journal's own `Commander` event. Ship metrics come from
the `Loadout` event rather than a specification table: a table is a second source of truth that
goes stale on every balance pass and cannot know what this Commander engineered. Elite has
already done the mass-and-FSD arithmetic and written the answer into `MaxJumpRange`.

"Session" means since the last `LoadGame`. Elite rolls the journal into a continuation file
during a long session without re-emitting it, so resetting on the file would silently restart the
totals mid-flight.

</details>
