# Engineers

Where each engineer is, what they grade, and how far along you are with them.

> "which engineers have I unlocked"
> "who grades frame shift drives"
> "where is Felicity Farseer"

Nothing here touches the network. Two halves that only mean something together: a shipped
**directory** — who works where and modifies what — and your **standing**, folded from your own
journal.

"Who grades frame shift drives" has one answer for everybody. "Who can grade *mine*" has one
answer for you, and it is the question people are actually asking, so both answers arrive
together.

## Where you stand

```text
1 engineer unlocked of 38 that exist.

  Felicity Farseer — grade 5, at Farseer Inc in Deciat

Invited and not yet unlocked: Elvira Martuuk.

Heard of, no invitation yet: The Dweller.

Not met at all: Baltanos, Bill Turner, Broo Tarquin, ...
```

Four buckets that account for everybody. Unlocked ones are listed highest grade first, because the
grade is what decides who is worth flying to; an alphabetical list makes you work that out
yourself.

## Who grades what

```text
5 engineers grade Thrusters, best first:
  Chloe Sedesi — to grade 5, at Cinder Dock in Shenve; not met
  Mel Brandon — to grade 5, at The Brig in Luchtaine; not met
  Professor Palin — to grade 5, at Abel Laboratory in Arque; not met
  Felicity Farseer — to grade 3, at Farseer Inc in Deciat; unlocked at grade 5, 40% to the next
  Elvira Martuuk — to grade 2, at Long Sight Base in Khun; invited, not yet unlocked
```

Best grade first, with your own standing beside each one. The best engineer for a job and the best
one *you can reach today* are frequently not the same person, and an answer that only gave you the
first would send you somewhere you cannot go.

## One engineer

```text
Felicity Farseer works out of Farseer Inc in Deciat.
Grades: Frame Shift Drive to 5, Sensors to 3, Surface Scanner to 3, Thrusters to 3, ...
Their invitation asks for Meta-alloys ×1.
The Commander has them unlocked at grade 5, 40% to the next.
```

## What is missing, and why

list.md asks for "who in the chain of unlocks", and **Directive 47 does not carry the referral
graph**. There is no permissive machine-readable source for it — not EDCD/FDevIDs, not
EDCD/coriolis-data, not EDEngineer, whose data files are blueprints, entries, equipment,
localisation and release notes and none of them the chain. It is wiki knowledge, and writing it out
from memory is exactly the invented game data every table in this project exists to avoid. A wrong
unlock requirement costs you a trip.

Two things are here in its place, and both have sources.

**The tribute**, for the 26 engineers whose invitation is a delivery: "their invitation asks for
Meta-alloys ×1" comes straight out of the blueprint data, which models each invitation task as a
blueprint whose ingredients are what they want.

**The chain as observed.** An engineer who has invited you *is* a referral that has already
happened, and the journal says so. "Invited, not yet unlocked" is a fact about your game rather
than a claim about the rules — and it is the same information, arriving from the one source that
cannot be wrong about it.

## Tools

### `get_engineer_progress`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `find_engineer`

```json
{"type":"object","properties":{"engineer":{"type":"string","description":"An engineer by name \u2014 for example \u0022Farseer\u0022 or \u0022Hera Tani\u0022."},"grades":{"type":"string","description":"A kind of module to find engineers for \u2014 for example \u0022Frame Shift Drive\u0022, \u0022Thrusters\u0022 or \u0022Shield Generator\u0022."}},"required":[],"additionalProperties":false}
```

## Notes for anyone reading the code

`tools/gen-engineers.py` builds the directory from three sources, each the authority on exactly
one thing:

- **EDCD/FDevIDs** `engineers.csv` is the identity — the engineer id the journal writes, the name,
  the id64 of the system and the market id of the base. It names neither place.
- **spansh.co.uk** turns those two ids into names, at *generation* time. That one network call per
  engineer is made in the tool precisely so that asking "where is Farseer" at the controls reaches
  nothing at all.
- **msarilar/EDEngineer** (MIT) `blueprints.json` is what they grade: every blueprint names the
  engineers who offer it and the grade it reaches.

The join needs a name that two sources agree on. The id list writes `Tod 'The Blaster' McQuinn` and
the blueprint list writes `Tod McQuinn`; without normalising the quoted nickname away his entire
speciality list is silently lost, which reads as an engineer who grades nothing. The same rule
works at runtime, so asking for "Tod McQuinn" finds him.

`EngineerProgress` **comes in two shapes and they mean different things.** At startup Elite writes
one carrying an `Engineers` array — a complete snapshot — and during play it writes one naming a
single engineer whose standing just changed. Treating the second as a snapshot would wipe the other
thirty-seven the first time somebody ranked up; treating the first as a delta would keep an
engineer you no longer have. So the array replaces and the single merges, which is the same
distinction the materials inventory draws.

Standings are keyed by engineer id rather than by name. A name is a string two sources can spell
differently; an id is not.

Four engineers — Baltanos, Eleanor Bresa, Rosa Dayette and Yi Shen — have a location and no
blueprint data. They are kept, and the answer says "I have no record of what they modify" rather
than implying they do nothing. Dropping them would have Directive 47 deny that a real person
exists.
