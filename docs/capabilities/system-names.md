---
title: System names
group: Knowledge
nav_order: 115
---

What a system's own name says about it — its sector, its boxel, and the mass code that sizes it.
Computed from the string, with no network.

> "read this system name"
> "what does this system name mean"
> "what is the mass code here"
> "what does Dryafea PO-X d2-0 mean"

This is the only capability D47 ships that needs **nothing at all**: no key, no account, no network,
and not even a journal if you say the name out loud. That is the point of it rather than a nice
property — first footfall pays a multiple, and by definition it happens in systems nobody has
scanned and uploaded, so a crowd-fed index is the wrong tool for exactly the half of exploring worth
the most. A name read off the Galaxy Map is the right one, and it works four thousand light years
from anything.

```text
Dryafea PO-X d2-0
  Sector: Dryafea — a 1,280 light year cube.
  Boxel: PO-X d2, system 0 within it.
  Mass code d: fourth of eight, a to h, least to most massive. Its boxel is 80 light years
    across (measured against real coordinates).
  Main star: class M.

The letter orders systems by mass and sizes the box. It is not established to predict what a
system pays — I measured that against real scan history and the sample could not settle it, so
I will not pretend otherwise.
```

## How a procedural name is built

```text
Dryafea      PO-X       d        2      -0
└ sector     └ boxel    └ mass   └ boxel  └ system
                          code     number   number
```

- The **sector** is a cube 1,280 light years on a side.
- The **letters** pick a boxel out of that sector.
- The **mass code** is the lone letter before the digits, `a` to `h`, least to most massive. It also
  sizes the boxel — a bigger box holds more mass.
- The **boxel number** appears only once a sector's letters have been used up, so `Synuefe AA-A a0`
  is boxel 0 rather than a broken name.
- The **system number** says which system inside the boxel. It means nothing else.

## The ladder, and where it comes from

D47 does not recite this from memory. The name encodes a boxel index in its letters and boxel
number, so regressing that index against real `StarPos` coordinates — with each sector's own origin
cancelled, so no sector grid has to be assumed — recovers the box size as the slope of the fit.
Across 2,854 procedurally-named systems:

| Mass code | Box size | Measured | Systems |
|---|---|---|---|
| `a` | 10 ly | 9.99 | 298 |
| `b` | 20 ly | 20.02 | 1,400 |
| `c` | 40 ly | 39.51 | 638 |
| `d` | 80 ly | 78.23 | 489 |
| `e` | 160 ly | 165.32 | 26 — thin |
| `f` | 320 ly | not measured | 2 |
| `g` | 640 ly | not measured | 1 |
| `h` | 1,280 ly | not measured | 0 |

The top three are not measured here and the answer says so out loud when you ask about one. They
rest on the doubling that the five rungs above establish, closed at the far end by the published
rule that `h` is the sector itself — and 10 × 2⁷ is exactly 1,280, so the ladder has nowhere else to
land. Community documentation agrees at both ends: mass code `a` within a 10 ly cube, `h`
"somewhere in this 1280 ly cube", halving down through `g` at 640 and `f` at 320.

## What this will not tell you

**Whether a heavier boxel pays better.** This is the thing the feature is most likely to be asked to
confirm, and D47 declines every time.

The claim is real folklore — more system mass, more of the big-payout genera — and Phase 16's spike
set out to turn it into a number using one Commander's own 632 `ScanOrganic` events. It could not,
for three reasons that no amount of further care fixes at this sample size:

- Only three mass codes carry a usable sample, and `b` — the one the claim leans hardest on — has
  **five scans**.
- Stratum is 80% of `b`, 16% of `c`, absent from `d`'s top three and 25% of `e`. No trend, on
  numbers too small to have one.
- **The data is not a sample of the galaxy — it is a record of where somebody chose to fly.** A
  Commander who already believes the folklore goes hunting where it says to, and their history then
  agrees with it for that reason alone.

So D47 says what the letter means and stops. Shipping a heuristic here would send you a very long
way in the wrong direction with D47 sounding certain. See
[the exobiology spike](https://github.com/dseelinger/d47/blob/main/docs/spikes/exobiology-sources.md).

## The star

Where D47 watched you arrive, it names the main star's class too — a variant's colour follows the
star, and the variant is what sets an organic's price.

That comes from the `FSDTarget` the game writes **before** the jump, not from the arrival scan.
Measured over 7,412 jumps: `FSDTarget` named the system being entered on **99.7%** of them, while a
main-star `Scan` followed on only **28.6%**. The obvious source is the one that is usually not
there.

Two cases deliberately produce no star at all rather than a plausible one:

- **A jump that ended somewhere other than the plotted target.** Carrying the target's class over
  would name a star you are not looking at.
- **A name you merely read aloud.** D47 knows the class of the system you are standing in; that says
  nothing about the one on the Galaxy Map, so it stays quiet.

## Hand-named systems

Sol, Colonia and `HIP 12685` carry no mass code, and being told so is a real answer rather than a
failure — 1,892 of the 4,746 names surveyed were hand-named, which makes it the common case.

```text
Colonia is a hand-named system, so there is no mass code in it to read. Only
procedurally-generated names carry one.
```

The grammar was checked against all 4,746 of those names, and **no name that looks procedural failed
to parse**. That count is the one worth keeping: a grammar that silently rejects a real name reports
"this one has no mass code", which is a wrong answer wearing the shape of a right one.

## Tools

### `read_system_name`

Decodes a name — the one you are in, or any you supply.

```json
{"type":"object","properties":{"name":{"type":"string","description":"The system name to read. Leave out for the system the Commander is in."}},"required":[],"additionalProperties":false}
```
