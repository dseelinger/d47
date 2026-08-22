---
title: Engineers
group: Knowledge
nav_order: 107
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text.

  This band is also read by the app and drawn in the panel, which adds two rules the general
  help pages do not have. It must stay well-formed XML — no HTML entities beyond the five XML
  ones, so write → and — as themselves. And no text below font-size 14: the big headset panel
  is 19 pixels per degree, so 14 px is about 31 arcminutes of cap height against a 20 arcminute
  floor for text meant to be read (list.md Phase 39).

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss. In
  the panel those same role names resolve against the Commander's live theme instead.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Who can improve your ship, where they are, and who to go and get next.</p>
<section>
<h2><span class="num">1</span> Two lists.</h2>
<svg viewBox="0 0 880 250" role="img" aria-label="The tab has two roots, the Directory and the Route">
 <rect x="30" y="30" width="390" height="160" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="225" y="90" text-anchor="middle" font-size="24" font-weight="800" fill="var(--text)">DIRECTORY</text>
 <text x="225" y="126" text-anchor="middle" font-size="16" fill="var(--text-muted)">everybody — all 38 of them</text>
 <text x="225" y="156" text-anchor="middle" font-size="16" fill="var(--text-muted)">nearest and reachable first</text>
 <rect x="460" y="30" width="390" height="160" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="655" y="90" text-anchor="middle" font-size="24" font-weight="800" fill="var(--text)">ROUTE</text>
 <text x="655" y="126" text-anchor="middle" font-size="16" fill="var(--text-muted)">who to unlock next</text>
 <text x="655" y="156" text-anchor="middle" font-size="16" fill="var(--text-muted)">worked out for you</text>
 <text x="440" y="226" text-anchor="middle" font-size="16" fill="var(--text-muted)">Press any name, anywhere, to open that engineer.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> The Directory is sorted by what you can do today.</h2>
<svg viewBox="0 0 880 300" role="img" aria-label="Three bands: reachable now, already yours, behind somebody else">
 <rect x="40" y="24" width="800" height="68" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="72" y="66" font-size="21" font-weight="800" fill="var(--accent)">READY FOR UNLOCK</text>
 <text x="808" y="66" text-anchor="end" font-size="16" fill="var(--text-muted)">nothing standing in your way</text>
 <rect x="40" y="110" width="800" height="68" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="72" y="152" font-size="21" font-weight="800" fill="var(--text)">UNLOCKED</text>
 <text x="808" y="152" text-anchor="end" font-size="16" fill="var(--text-muted)">and how far to the next grade</text>
 <rect x="40" y="196" width="800" height="68" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="72" y="238" font-size="21" font-weight="800" fill="var(--text-muted)">NEEDS A REFERRAL</text>
 <text x="808" y="238" text-anchor="end" font-size="16" fill="var(--text-muted)">somebody else has to introduce them</text>
 <text x="440" y="290" text-anchor="middle" font-size="16" fill="var(--text-muted)">Alphabetical order would answer that question for nobody.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Most of them are behind somebody else.</h2>
<svg viewBox="0 0 880 262" role="img" aria-label="Twenty-seven of thirty-eight engineers are reached through another engineer">
 <text x="440" y="56" text-anchor="middle" font-size="32" font-weight="800" fill="var(--accent)">27 of the 38</text>
 <text x="440" y="88" text-anchor="middle" font-size="17" fill="var(--text-muted)">are reached through somebody else</text>
 <rect x="40" y="120" width="230" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="172" text-anchor="middle" font-size="22" font-weight="800" fill="var(--text)">YOU</text>
 <line x1="282" y1="163" x2="302" y2="163" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="316,163 300,155 300,171" fill="var(--accent-muted)"/>
 <rect x="325" y="120" width="230" height="86" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="158" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">SELENE JEAN</text>
 <text x="440" y="186" text-anchor="middle" font-size="16" fill="var(--accent)">reach grade 3 with her</text>
 <line x1="567" y1="163" x2="587" y2="163" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="601,163 585,155 585,171" fill="var(--accent-muted)"/>
 <rect x="610" y="120" width="230" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="725" y="158" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">BILL TURNER</text>
 <text x="725" y="186" text-anchor="middle" font-size="16" fill="var(--text-muted)">then he invites you</text>
 <text x="440" y="248" text-anchor="middle" font-size="16" fill="var(--text-muted)">D47 tells you how far along that path you already are.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> The Route picks the one unlock that helps most.</h2>
<svg viewBox="0 0 880 290" role="img" aria-label="The solver takes your planned modifications and names the unlock covering the most of them">
 <text x="30" y="40" font-size="15" font-weight="700" fill="var(--text-muted)">WHAT YOU PLANNED</text>
 <rect x="30" y="56" width="250" height="52" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="88" text-anchor="middle" font-size="16" fill="var(--text)">Dirty Drive Tuning</text>
 <rect x="30" y="118" width="250" height="52" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="150" text-anchor="middle" font-size="16" fill="var(--text)">Increased FSD Range</text>
 <rect x="30" y="180" width="250" height="52" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="212" text-anchor="middle" font-size="16" fill="var(--text)">Long Range Sensors</text>
 <line x1="292" y1="144" x2="308" y2="144" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="322,144 306,136 306,152" fill="var(--accent-muted)"/>
 <rect x="332" y="86" width="228" height="116" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="446" y="130" text-anchor="middle" font-size="20" font-weight="800" fill="var(--accent)">ROUTE</text>
 <text x="446" y="160" text-anchor="middle" font-size="16" fill="var(--text-muted)">which single unlock</text>
 <text x="446" y="184" text-anchor="middle" font-size="16" fill="var(--text-muted)">covers the most of these?</text>
 <line x1="572" y1="144" x2="588" y2="144" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="602,144 586,136 586,152" fill="var(--accent-muted)"/>
 <rect x="612" y="86" width="238" height="116" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="731" y="126" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">PROFESSOR PALIN</text>
 <text x="731" y="154" text-anchor="middle" font-size="16" fill="var(--text-muted)">3 steps, about 18 jumps</text>
 <text x="731" y="182" text-anchor="middle" font-size="16" fill="var(--accent)">covers 2 of the 3</text>
 <text x="440" y="252" text-anchor="middle" font-size="16" fill="var(--text-muted)">Not the shortest chain — the one that covers the most of what you planned.</text>
 <text x="440" y="278" text-anchor="middle" font-size="16" fill="var(--text-muted)">Counted in jumps, at your ship's range, from where you are standing.</text>
</svg>
<p class="body">Every step of the working is on the page on purpose. A ranking you cannot inspect is an oracle — and when it is wrong, or when you would simply rather go somewhere else, you could not tell a bad answer from a bug.</p>
</section>
</div></div>

## The details

Where each engineer is, what they grade, how far along you are with them, and who to go and get
next.

> "which engineers have I unlocked"
> "who grades frame shift drives"
> "where is Felicity Farseer"
> "who should I unlock next"

Nothing here touches the network. Two halves that only mean something together: a shipped
**directory** — who works where and modifies what — and your **standing**, folded from your own
journal.

"Who grades frame shift drives" has one answer for everybody. "Who can grade *mine*" has one
answer for you, and it is the question people are actually asking, so both answers arrive
together.

### Where you stand

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

### Who grades what

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

### One engineer

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

### The chain of unlocks

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

### The chain has a source, and it did not always

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

### The Engineers tab

Two roots. The **Directory** is everybody, ordered by what you can act on today; the **Route** is
the solver. Drilling a directory row opens the one engineer behind it. The search box filters on
the name, the system and what they grade.

#### Who can I go and get

```text
1 of 38 unlocked, 12 within reach. 2 planned things are waiting on somebody you have not unlocked.

You can go and get these now
  * Felicity Farseer - 1 planned thing wants them     131 ly, about 5 jumps
  * Elvira Martuuk - 1 planned thing wants them       182 ly, about 7 jumps
    The Dweller                                        34 ly, about 2 jumps
    Jude Navarro                                       67 ly, about 3 jumps
```

Sorted by what you can act on today — **within reach, then already yours, then behind somebody
else** — rather than alphabetically or by speciality, because the question is nearly always *who
can I go and get*. Alphabetical order answers that for nobody.

The line at the top carries the count that belongs to the Loadout tab: **how many of your plans are
waiting on somebody you have not unlocked.** A plan blocked on a person is not a plan blocked on
materials, and the gap analysis cannot tell you which it is.

One pane rather than two: a row already holds the name, where they are and what wants them, so a
second column beside it would only repeat them.

#### How do I get everything my plans need

```text
Felicity Farseer — 1 step, 131 ly, about 5 jumps, and 1 planned thing covered.
  Felicity Farseer at Farseer Inc in Deciat — 131 ly, about 5 jumps
      first: Gain exploration rank Scout or higher.
      hand over: Meta-alloys ×1
      grade 3 with them takes 2,000,000 cr of profit sold there
      covers: Long Way (Krait MkII) · FrameShiftDrive — grade 3 Increased FSD Range

Professor Palin — 3 steps, 466 ly, about 18 jumps, and 2 planned things covered.
  Elvira Martuuk at Long Sight Base in Khun — 182 ly, about 7 jumps
      first: Attain a maximum distance from your career start location of at least 300 light years.
      hand over: Soontill Relics ×3
  Marco Qwent at Qwent Research Base in Sirius — 186 ly, about 7 jumps
      first: Gain invitation from Sirius Corporation.
      hand over: Modular Terminals ×25
  Professor Palin at Abel Laboratory in Arque — 98 ly, about 4 jumps
      first: Attain a maximum distance from your career start location of at least 5,000 light years.
      hand over: Sensor Fragment ×25
      covers: Bad Idea (Python) · MainEngines — grade 5 Dirty Drive Tuning
      covers: Long Way (Krait MkII) · FrameShiftDrive — grade 3 Increased FSD Range
```

**A solver rather than a display.** Walking one engineer's referral chain is exact and cheap, and it
answers the wrong question — a blueprint usually lists several engineers, and one unlock covers many
plans. Marsha Hicks rolls multi-cannons, cannons, fragment cannons, fuel scoops, refineries and four
limpet controllers, all at grade 5. So the best next unlock is the one that satisfies the most of
what you have planned, not the shortest chain.

**One unit, and it is jumps.** Distance converts at the range of the ship you are actually flying,
and each stop carries its own leg, so a long chain of short hops and one long haul are compared on
the same scale instead of being balanced by a tuning constant. **Colonia needs no rule of its own**:
22,000 light years is hundreds of jumps and swamps any step count — and because the distance is
measured from where you are standing, being in Colonia flips it automatically.

**What is not a trip is not turned into one.** A tribute of fifty units is a shopping run to a system
you know, and it is already inside the leg that reaches it. A combat rank is not a trip at all, so it
is printed in Frontier's own words, breaks ties, and never becomes a number. That is also why the
whole working is on the page: a ranking nobody can inspect is an oracle, and when it is wrong — or
when you would simply rather go somewhere else — you cannot tell a bad answer from a bug.

Distance stays the primary key deliberately. Making "you can just go and do it" a class above it
would put an already-invited Colonia engineer ahead of one in the Bubble, and undo the thing Colonia
needed no rule for.

#### The route reaches the checklist as a chain

Pressing **Put this route on my checklist** proposes one item per stop, in flying order, each
carrying the grade that stop actually needs:

```text
Rank 3 with Liz Ryder
Rank 3 with Hera Tani
Rank 5 with Broo Tarquin
```

A single line reading "unlock Broo Tarquin" hides two engineers and two rank climbs behind a tick you
can never make progress on. It is a proposal, like every other plan promotion: accepting it is your
own act.

### Distance is arithmetic, and that is the whole point

The coordinates ship in `Engineers.tsv`, generated rather than hand-written. `get_distance` computes
the same figure correctly and is a network call — so ranking thirty-eight people through one every
time a plan changes is a tab that is useless in flight and unusable offline.

Your own position comes out of the journal: `Location`, `FSDJump` and `CarrierJump` each carry a
`StarPos`, and across a 912-journal corpus all three carried one every time, 9,332 of 9,332. Nothing
else does — so docking, which names your system and states no position, deliberately leaves it alone
rather than making it unknown the moment you land.

Where either end is unknown the answer is "distance unknown" rather than zero. Zero would sort an
engineer nobody can place to the top of a list whose entire subject is who is nearest.

### Tools

`get_engineer_route` and `promote_engineer_route` are `Protected`: reachable from the panel and from
a phrase, never from the model. Cost rather than safety is the reason — the advertised tool surface
is re-billed on every turn and the largest profile sat at 39,639 bytes against a 40,000 ceiling.
Nothing is lost by it: "who should I unlock next" is a fixed question with no free-text argument in
it, which is exactly the shape the keyword router handles with no round trip at all.

#### `get_engineer_progress`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `find_engineer`

```json
{"type":"object","properties":{"engineer":{"type":"string","description":"An engineer by name \u2014 for example \u0022Farseer\u0022 or \u0022Hera Tani\u0022."},"grades":{"type":"string","description":"A kind of module to find engineers for \u2014 for example \u0022Frame Shift Drive\u0022, \u0022Thrusters\u0022 or \u0022Shield Generator\u0022."}},"required":[],"additionalProperties":false}
```

#### `get_engineer_route`

Say *"who should I unlock next"*, *"what is the fastest way in"* or *"which engineer next"*.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `promote_engineer_route`

Say *"put that route on my checklist"* or *"promote this unlock"*.

```json
{"type":"object","properties":{"engineer":{"type":"string","description":"Which engineer, by name. Omit for the best next unlock."}},"required":[],"additionalProperties":false}
```

### Notes for anyone reading the code

`tools/gen-engineers.py` builds the directory from four sources, each the authority on exactly
one thing:

- **EDCD/FDevIDs** `engineers.csv` is the identity — the engineer id the journal writes, the name,
  the id64 of the system and the market id of the base. It names neither place.
- **spansh.co.uk** turns those two ids into names, at *generation* time. That one network call per
  engineer is made in the tool precisely so that asking "where is Farseer" at the controls reaches
  nothing at all. The same record carries the system's coordinates, so the ranking costs no call
  the tool was not already making.
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

**Coordinates are agreed by two sources and checked against a third.** spansh states them and
EDDiscovery states them, both exactly, on Elite's 1/32 ly grid — so a difference wider than one grid
step is not rounding, and the generator refuses to write the table at all rather than ship a distance
nobody can check. Run it with `--corpus` and Frontier's own `StarPos` becomes a third opinion
wherever you have been there; the shipped table had all 38 placed and 31 of them confirmed that way.

**Several referrers mean any of them, and one row argues with that.** Yi Shen's own meeting text
reads "Complete referral tasks for Baltanos, Eleanor Bresa and Rosa Dayette" — three requirements
rather than a choice of three. The directory has read it as a choice since the chain arrived and the
solver keeps that reading; what it does about it is name the referrer it picked and print the meeting
text underneath, so you read Frontier's sentence rather than only Directive 47's count of it. That is
the whole reason a ranking has to show its work.

**Who can roll a thing is read per grade**, because the blueprint table states it per grade: six
engineers offer Increased FSD Range at grade 3 and three of them at grade 5. Deriving the list from
an engineer's top grade instead would send you to Professor Palin for a grade 5 drive he does not
roll. The directory's own speciality lists are unioned in beside it, and that half is not
redundant — the four Colonia on-foot engineers have no rows in the blueprint table at all, so reading
it alone tells a Colonia Commander that nobody within twenty-two thousand light years can do the
thing three people next door do.

Four engineers — Baltanos, Eleanor Bresa, Rosa Dayette and Yi Shen — have a location and no
blueprint data. They are kept, and the answer says "I have no record of what they modify" rather
than implying they do nothing. Dropping them would have Directive 47 deny that a real person
exists.
