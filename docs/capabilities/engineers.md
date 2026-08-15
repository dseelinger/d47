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
  Chloe Sedesi — to grade 5, at Cinder Dock in Shenve; not met, reached through Marco Qwent
  Mel Brandon — to grade 5, at The Brig in Luchtaine; not met, reached through Elvira Martuuk
  Professor Palin — to grade 5, at Abel Laboratory in Arque; not met, reached through Marco Qwent
  Felicity Farseer — to grade 3, at Farseer Inc in Deciat; unlocked at grade 5, 40% to the next
  Elvira Martuuk — to grade 2, at Long Sight Base in Khun; invited, not yet unlocked
```

Best grade first, with your own standing beside each one. The best engineer for a job and the best
one *you can reach today* are frequently not the same person, and an answer that only gave you the
first would send you somewhere you cannot go.

The ones you have not met carry the next step rather than a dead end, which turns a list of people
you cannot use into a list of paths you can start.

## One engineer

```text
Felicity Farseer works out of Farseer Inc in Deciat, on 6 A.
Grades: Frame Shift Drive to 5, Sensors to 3, Surface Scanner to 3, Thrusters to 3, ...
Nobody has to recommend them — public data sources.
Earning the invitation: Gain exploration rank Scout or higher.
Their invitation asks for Meta-alloys ×1.
Reputation with them rises fastest by: Craft modules for a major increase. Sell exploration data at Farseer Inc.
The Commander has them unlocked at grade 5, 40% to the next.
```

"Nobody has to recommend them" is said out loud rather than left silent. Eleven engineers need
nobody, and for those eleven that *is* the answer — silence reads as a missing referral rather than
as an absent one.

## The chain of unlocks

```text
Bill Turner works out of Turner Metallics Inc in Alioth, on 4 A.
Grades: Plasma Accelerator to 5, Sensors to 5, Surface Scanner to 5, Auto Field-Maintenance Unit to 3, ...
Reached through Selene Jean at grade 3.
The Commander is grade 2 with Selene Jean, and the referral needs grade 3 — 2,000,000 cr of profit sold at their workshop, plus roughly half the bar to the grade after it.
Earning the invitation: Gain Friendly status with Alliance. You will also need Allied status with Alioth Independents to get a permit to access the Alioth starsystem.
Their invitation asks for Bromellite ×50.
Reputation with them rises fastest by: Craft modules for a major increase. Sell commodities to Turner Metallics Inc.
The Commander has not met them.
```

**27 of the 38 engineers are reached through somebody else**, and the middle two lines are the point
of the whole feature: who introduces them, and how far along that path you already are. The price is
the published cost of that rank; the "roughly half the bar" is stated in words because that is how
both sources state it and nobody published it as a number.

Several referrers mean **any** of them, not all — reading three names as three requirements would
describe a wall where there is a door:

```text
Yi Shen works out of Eidolon Hold in Einheriar, on 1 A.
I have no record of what they modify.
Reached through any of Baltanos, Eleanor Bresa or Rosa Dayette.
No grade is stated for that referral — the on-foot engineers unlock on a count of modifications rather than on a grade.
The Commander has not met Baltanos, Eleanor Bresa or Rosa Dayette.
```

And a referral with no grade says so. The whole Odyssey chain states one nowhere, because those
engineers unlock on a count of modifications instead; filling in the ship chain's grade 3 there would
be a requirement Directive 47 invented.

## The chain has a source, and it did not always

This page used to say the referral graph was unobtainable — that no permissive machine-readable
source carried it, and that writing it from memory would be the invented game data every table here
exists to avoid. **The first half was a correct measurement of three files and a wrong claim about
the world.** FDevIDs, coriolis-data and EDEngineer's ship rows genuinely do not carry it; EDDiscovery's
`EliteDangerousCore` (Apache-2.0) does, and names both the referring engineer and the grade needed
with them.

The two sources that have it agree on 37 of 38. The conflict is Bill Turner, whom EDDiscovery files
as common knowledge and the wiki as learned from Selene Jean, and one Commander's journal settles it
in the wiki's favour: the invitation lands eleven seconds after Selene Jean reaches rank 3 and before
she reaches rank 4, which is precisely the documented threshold. That override is written into the
generator and asserted by a test, so it cannot quietly stop applying.

**The observed half still comes first where the two meet.** An engineer who has invited you *is* a
referral that has already happened, and no table can be more right about that than your own journal.

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

`tools/gen-engineers.py` builds the directory from four sources, each the authority on exactly
one thing:

- **EDCD/FDevIDs** `engineers.csv` is the identity — the engineer id the journal writes, the name,
  the id64 of the system and the market id of the base. It names neither place.
- **spansh.co.uk** turns those two ids into names, at *generation* time. That one network call per
  engineer is made in the tool precisely so that asking "where is Farseer" at the controls reaches
  nothing at all.
- **msarilar/EDEngineer** (MIT) `blueprints.json` is what they grade: every blueprint names the
  engineers who offer it and the grade it reaches.
- **EDDiscovery/EliteDangerousCore** (Apache-2.0) is the chain and the prose around it — who refers
  whom and at what grade, the body the base orbits, how each is discovered and met, and how
  reputation with them rises. Parsed out of C# source rather than read from data, so the generator
  asserts a hard count of 38 rows and fails the run outright if that shape changes.

A speciality's grade is **0 where the source states none**, which is every Odyssey suit and weapon
row — those blueprints are ungraded in the game rather than missing a figure. Zero is not a grade
and is never spoken as one.

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
