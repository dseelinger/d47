---
title: On foot
group: Knowledge
nav_order: 109
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
<p class="intro">Two steps to knowing what a suit upgrade really costs.</p>
<section>
<h2><span class="num">1</span> Ask about what you are wearing.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what does grade 3 on my Maverick cost</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Suits, weapons and their mods ship with D47 and need no network.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">It answers in materials, not just credits.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Find out where the materials come from.</h2>
<svg viewBox="0 0 880 308" role="img" aria-label="Grade 3 Maverick">
 <rect x="20" y="16" width="840" height="268" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Grade 3 Maverick</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Manganese</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">10 — settlements</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Chemical Superbase</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text)">5 — you have 2</text>
 <rect x="44" y="182" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="210" font-size="16" fill="var(--text)">Credits</text>
 <text x="812" y="210" text-anchor="end" font-size="16" fill="var(--text-muted)">75,000</text>
 <text x="44" y="278" font-size="15" fill="var(--text-muted)">What you are short of lands on the Gap page with everything else.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="On-foot materials are not ship materials.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">On-foot materials are not ship materials.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">They are a separate set with separate sources. Having a full ship inventory buys you nothing here.</text>
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
<p class="intro">What you are wearing, what an on-foot upgrade really costs, and where its materials come from.</p>
<section>
<h2><span class="num">1</span> Two axes, and neither one is the ship model.</h2>
<svg viewBox="0 0 880 262" role="img" aria-label="A grade is bought at Pioneer Supplies and can be raised; a modification is applied by an engineer and is permanent">
 <rect x="20" y="40" width="400" height="150" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="82" text-anchor="middle" font-size="19" font-weight="800" fill="var(--text)">GRADE</text>
 <text x="220" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">1 → 5, at Pioneer Supplies</text>
 <text x="220" y="142" text-anchor="middle" font-size="15" fill="var(--text-muted)">no randomness, fixed price</text>
 <text x="220" y="168" text-anchor="middle" font-size="15" fill="var(--text-muted)">undone by buying a higher one</text>
 <rect x="460" y="40" width="400" height="150" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="82" text-anchor="middle" font-size="19" font-weight="800" fill="var(--text)">MODIFICATION</text>
 <text x="660" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">ungraded — present or absent</text>
 <text x="660" y="142" text-anchor="middle" font-size="15" fill="var(--text-muted)">four per item, at an engineer</text>
 <text x="660" y="168" text-anchor="middle" font-size="15" font-weight="700" fill="var(--danger)">never reversible</text>
 <text x="440" y="226" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">Four slots, permanent, and no way to practise.</text>
 <text x="440" y="256" text-anchor="middle" font-size="15" fill="var(--text-muted)">A wrong one is recoverable only by buying and re-upgrading a fresh item, so it says so once per answer.</text>
</svg>
<p class="body">There is no roll count anywhere on this page, so there is no hedging either: every quantity is what the game charges, exactly.</p>
</section>
<section>
<h2><span class="num">2</span> The published quantities are wrong, and the game settles it.</h2>
<svg viewBox="0 0 880 256" role="img" aria-label="Community sources list five, ten and fifteen ingredients where the game actually charges three, five and eight">
 <rect x="20" y="40" width="400" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text-muted)">PUBLISHED EVERYWHERE</text>
 <text x="220" y="120" text-anchor="middle" font-size="24" font-weight="800" fill="var(--text-muted)">5 / 10 / 15</text>
 <text x="220" y="148" text-anchor="middle" font-size="14" fill="var(--text-muted)">for a modification</text>
 <line x1="432" y1="100" x2="448" y2="100" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="462,100 446,92 446,108" fill="var(--accent-muted)"/>
 <rect x="474" y="40" width="386" height="124" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="667" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">WHAT THE GAME CHARGES</text>
 <text x="667" y="120" text-anchor="middle" font-size="24" font-weight="800" fill="var(--accent)">3 / 5 / 8</text>
 <text x="667" y="148" text-anchor="middle" font-size="14" fill="var(--text-muted)">measured on 16 real upgrades</text>
 <text x="440" y="210" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">The published figures would have quoted you two to three times the real cost.</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">And they would have looked right, because they are what every other tool says.</text>
</svg>
<p class="body">Grade upgrades were restated the same way — 1 / 5 / 10 / 15 / 25 / 35 published against <strong>1 / 2 / 4 / 5 / 9 / 12</strong> charged — and Power Regulators, listed everywhere, are not asked for at all. The generator refuses to run if any on-foot ingredient falls outside the measured tables: one uncorrected number sitting among corrected ones is invisible and wrong by a factor of three.</p>
</section>
<section>
<h2><span class="num">3</span> The suit's own name is not safe to repeat.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="Elite's localisation leaves an unresolved token naming Class1 whatever the real grade is">
 <rect x="20" y="36" width="840" height="94" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="46" y="74" text-anchor="start" font-size="16" font-weight="700" fill="var(--danger)">$UtilitySuit_Class1_Name;</text>
 <text x="46" y="108" text-anchor="start" font-size="15" fill="var(--text)">269 of 768 real events carry that token — and every one of them says Class1.</text>
 <text x="440" y="170" text-anchor="middle" font-size="16" fill="var(--text)">Speaking that string would give you the wrong grade more than a third of the time.</text>
 <text x="440" y="202" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So the name comes from the symbol and a shipped table, never from Elite’s own text.</text>
 <text x="440" y="232" text-anchor="middle" font-size="15" fill="var(--text-muted)">A modification with no known name is said as the symbol Elite wrote, and labelled as one.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> The order of the plan is a routing fact.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="A grade 1 item has no modification slots and an engineer's base has no Pioneer Supplies, so upgrading comes first">
 <rect x="20" y="40" width="250" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="80" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">A GRADE 1 ITEM</text>
 <text x="145" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">has zero mod slots</text>
 <line x1="282" y1="90" x2="306" y2="90" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="320,90 304,82 304,98" fill="var(--accent-muted)"/>
 <rect x="335" y="40" width="270" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="470" y="80" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">AN ENGINEER’S BASE</text>
 <text x="470" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">has no Pioneer Supplies</text>
 <line x1="617" y1="90" x2="641" y2="90" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="655,90 639,82 639,98" fill="var(--accent-muted)"/>
 <rect x="670" y="40" width="190" height="100" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="765" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">UPGRADE FIRST</text>
 <text x="765" y="106" text-anchor="middle" font-size="15" fill="var(--text-muted)">then make the trip</text>
 <text x="440" y="190" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">The upgrade cannot be done when you get there.</text>
 <text x="440" y="220" text-anchor="middle" font-size="15" fill="var(--text-muted)">So the plan is ordered the way the trips have to happen — a step in the order, not a footnote.</text>
</svg>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="engineers.html"><span class="ct">Engineers →</span><span class="cd">Who applies a modification, and how far away they are.</span></a>
<a class="card" href="gap.html"><span class="ct">The gap →</span><span class="cd">The ship locker half of what your plans still need.</span></a>
<a class="card" href="checklists.html"><span class="ct">Checklists →</span><span class="cd">Where a promoted kit plan lands, grade first and modifications after.</span></a>
</div>
</div>
</div></div>

## The details

What you are wearing and carrying, what an on-foot grade or modification costs, and where its
materials come from.

> "what suit am I wearing"
> "what does night vision cost"
> "what would grade 5 on this suit take"
> "where do I find graphene"

Nothing here touches the network.

### On foot is not the ship feature with a different vocabulary

Two axes, and neither one is the ship model.

| | Grade | Modification |
|---|---|---|
| Applied at | **Pioneer Supplies**, in the Concourse | An engineer |
| Range | 1 → 5 | **Ungraded** — present or absent |
| Randomness | **None.** Fixed list, fixed price | None |
| Reversible | Buy a higher grade | **Never** |
| Limit | — | **Four per item**, one slot earned per grade above 1 |

Three things follow, and they run through every answer on this page.

**Nothing is estimated.** There is no roll count, so no hedging: every quantity below is what the
game charges, exactly.

**Planning matters more here than for ships.** Four slots, permanent, no way to practise. A wrong
modification is unrecoverable except by buying and re-upgrading a fresh item, so d47 says so once
per answer rather than never.

**Ordering is a routing fact.** A grade 1 item has *zero* modification slots, and an engineer's base
has no Pioneer Supplies — only an Apex desk. So "upgrade first" is a step in the order, not a
footnote: the upgrade cannot be done when you get there.

### What you are wearing

#### `get_on_foot_loadout`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

```text
Wearing: Maverick Suit, grade 5 — loadout "Sneaky-Snipy".
  Suit: Improved jump assist, Quieter footsteps, 2 of 4 slots free.
PrimaryWeapon1: Manticore Executioner, grade 5.
  PrimaryWeapon1: Noise suppressor, Magazine size, Stability, 1 of 4 slots free.
SecondaryWeapon: Manticore Tormentor, grade 5.
  SecondaryWeapon: Stowed reloading, Magazine size, Stability, 1 of 4 slots free.
Modifications are permanent — they cannot be removed or replaced, and a wrong one is recoverable
only by buying and re-upgrading a fresh item.
```

**The suit's name never comes from Elite's own localisation.** Frontier's localisation is broken for
every suit above grade 1, and it is not an edge case: of 768 `SuitLoadout` events in a 912-journal
corpus, **269 carry an unresolved `$UtilitySuit_Class1_Name;` token, and every one says Class1**
whatever the real class is. Speaking that string would give you the wrong grade more than a third of
the time. The symbol plus a shipped table is the only honest route.

A modification d47 has no name for is said as the symbol Elite wrote, and labelled as one, rather
than dropped from the list as though the slot were empty.

### What it costs

#### `get_on_foot_engineering`

```json
{"type":"object","properties":{"equipment":{"type":"string","description":"A suit or weapon by name \u2014 for example \u0022Maverick\u0022, \u0022Dominator\u0022 or \u0022Karma AR-50\u0022. Defaults to what the Commander is wearing."},"grade":{"type":"integer","description":"The grade to reach, 2 to 5."},"modification":{"type":"string","description":"A modification by name \u2014 for example \u0022Night Vision\u0022, \u0022Magazine Size\u0022 or \u0022Quieter Footsteps\u0022."}},"required":[],"additionalProperties":false}
```

Name a modification for its recipe and who offers it:

```text
Quieter footsteps — a suit modification.
  Costs: 3 × Settlement Assault Plans (have 0, short 3), 5 × Tactical Plans (have 12),
         5 × Patrol Routes (have 2, short 3), 3 × Micro Hydraulics (have 9),
         8 × Viscoelastic Polymer (have 21)
  From: Yarden Bond.
It is ungraded and permanent, and it needs the item at grade 2 or better before it can be applied
at all.
```

Or name a suit or weapon and a grade for what reaching it takes:

```text
Maverick Suit to grade 4, at Pioneer Supplies.
  Costs: 4 × Suit Schematic (have 1, short 3), 4 × Health Monitor (have 6),
         4 × Manufacturing Instructions (have 4), 9 × Carbon Fibre Plating (have 30),
         9 × Graphene (have 12)
  Credits from grade 1: 6,150,000.
  Grade 4 carries 3 modification slots. An engineer's base has no Pioneer Supplies, so this has to
  happen before the trip.
```

#### The quantities are not the published ones

**This is the part most worth knowing about.** Every on-foot quantity in the community sources
predates the patch that cut them. `tools/gen-blueprints.py` restates them to what the game actually
charges:

| | Published | What the game charges |
|---|---|---|
| Modification ingredients | 5 / 10 / 15 | **3 / 5 / 8** |
| Grade upgrade ingredients | 1 / 5 / 10 / 15 / 25 / 35 | **1 / 2 / 4 / 5 / 9 / 12** |
| Power Regulators in an upgrade | listed | **not asked for at all** |

Measured against the game itself: 78 ingredient comparisons against the `Resources` list on 16 real
upgrade events, and a zero-remainder cover of 41 material lines across four `ShipLocker` deltas,
each corroborating the other's leftovers. Every quantity in both tables was observed; nothing is
extrapolated. See [the finding](https://github.com/dseelinger/d47/blob/main/docs/spikes/journal-corpus-on-foot.md).

Shipping the published figures would have quoted you two to three times the real cost of everything
on foot — and it would have looked right, because it is what every other tool says.

#### Credits

Reaching a grade costs the item's grade 1 price times a fixed multiplier — **×4, ×15, ×30, ×50** for
grades 2 to 5, per step rather than cumulative, so grade 1 to 5 in full is 99 times the base price.
Exact on all 12 weapon upgrades in the corpus across five weapons and four grades, and on both suit
grades it covers.

Six of the eleven weapons have no published price, and for those d47 gives **no** credit figure
rather than a total missing a step — the two read identically otherwise.

### Where the materials come from

#### `find_micro_resource`

```json
{"type":"object","properties":{"material":{"type":"string","description":"A micro-resource by name \u2014 for example \u0022Graphene\u0022, \u0022Circuit Board\u0022 or \u0022Opinion Polls\u0022."},"wanted":{"type":"integer","description":"How many are wanted, for the trade arithmetic. Defaults to the shortfall."}},"required":["material"],"additionalProperties":false}
```

```text
Graphene — Component. You have 12.
  Found in: Planetary Settlement.
  The Bartender charges 23 barter value for each.
  For 9, you could hand over any of:
    16 × Microelectrode (you have 22)
    23 × Weapon Component (you have 40)
    35 × Chemical Superbase (you have 51)
  Whatever value the last unit does not use is lost, so trade in one go.
```

**The sourcing is more specific than anything a ship material gets.** Every micro-resource carries
the settlement types, the building types (16 codes, AGRI to STO) and the container types (22 kinds
of locker and data port) it turns up in — so "which settlement, which building, which locker" is
answerable rather than approximated, where the best a ship material manages is "Planetary
Settlement".

**The Bartender's rate is arithmetic.** It takes Components for a fixed barter value each and sells
Components for a fixed barter cost, so a trade is
`floor(Σ(offered × value) ÷ cost)` — reproduced exactly on 49 of 49 real trades. This is a better
position than the ship material trader, whose rate took a 1,096-trade corpus to pin down.

Two things that change what you should do:

- **Leftover value is lost**, per trade, with no carry-over. Modelled and refused — crediting the
  remainder to the next trade drops the score from 49 to 30. So trade in one lump, not in ones.
- **Components only.** Items and Data are sold for credits and cannot be exchanged at all, and
  illegal goods are refused outside Anarchy-controlled systems.

#### The locker cap is per category

**1,000 per category, not per item type** — the sources conflicted and the game settles it. Across
28,148 locker snapshots, Components total exactly 1,000 in 7,931 of them and never once more, while
the largest single component ever held is 94. d47 says so when you are within a quarter of the cap,
because gathering past it is wasted time.

Consumables carry a separate cap of **100 per item**.

### The plan, and your checklist

A suit or weapon plan is the on-foot half of the Loadout tab, and it is the Ships page instantiated
against the same drill: an index of what you are wearing and carrying, one item, one slot. **It
stays a second mode rather than a second tab**, because the game separates ship and on-foot hard
and so does its vocabulary — but nothing about the layout is redrawn.

**What differs is the shape of the thing being planned.** A hull's slot is a place a module goes,
and the journal names it. An item on foot has a grade and up to four modification slots, and Elite
names none of them — so the slots here are `Grade`, then `Mod 1` to `Mod 4`, and the numbers are
d47's own. Elite reports what is fitted as a *set* with no positions in it, so those numbers order
your plan and claim nothing about the item.

**The grade is the first slot, and that is a routing fact rather than a preference.** A grade 1 item
has no modification slots at all, and an engineer's base has no Pioneer Supplies — only an Apex
desk. So the plan is ordered the way the trips have to happen.

**The plan owns what and the checklist owns when.** A plan lives in `data/on-foot.json` and nothing
crosses into your checklist unasked; promoting it produces a proposal you accept, with the grade
first and the modifications after it.

**Something you do not own is not absent, it is intended.** It has no `SuitID`, so buying it is the
plan's first step rather than a precondition sitting outside it — and buying one adopts the plan
rather than making you re-point it.

#### `get_on_foot_plans`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `promote_on_foot_plan`

```json
{"type":"object","properties":{"item":{"type":"string","description":"The suit or weapon by name. Omit for the suit being worn, or the one weapon carried."},"weapon":{"type":"boolean","description":"True when this is about a hand weapon rather than the suit."}},"required":[],"additionalProperties":false}
```

#### `drop_on_foot_plan`

```json
{"type":"object","properties":{"item":{"type":"string","description":"The suit or weapon by name. Omit for the suit being worn, or the one weapon carried."},"weapon":{"type":"boolean","description":"True when this is about a hand weapon rather than the suit."}},"required":[],"additionalProperties":false}
```

Dropping a plan keeps whatever it already put on your checklist. You ordered your list around those
lines, and removing them silently is a history that lies.

#### The file

```json
{
  "kit": [
    {
      "id": "kit-1",
      "equipment": "Maverick Suit",
      "kind": "suit",
      "itemId": 1837009111675068,
      "slots": [
        { "slot": "Grade", "grade": 5 },
        { "slot": "Mod 1", "modification": "Night Vision" }
      ]
    }
  ]
}
```

`id` is the plan's own identity and is **independent of `itemId` from the moment it is created** —
that independence is what there is to rebind when you buy one. A plan with no `itemId` is an
intended one. A `slot` that is not `Grade` or `Mod 1` to `Mod 4` is refused and reported, because a
hardpoint on a suit is a line that could be stored and shown and never promoted.

Hand-edited, it takes effect without a restart, and a line the file gets wrong is reported rather
than silently dropped.

### Notes for anyone reading the code

Two generated tables sit under this.

`tools/gen-onfoot.py` builds `OnFoot.tsv` — suits, hand weapons, hand tools and the modification
name map — from **EDDiscovery/EliteDangerousCore** (Apache-2.0) `Items/Suits.cs` and
`Items/HandItems.cs`. That is the only permissive source found keyed on the symbols the journal
writes: **FDevIDs has no suit list and no hand-weapon list**, which inverts the ship arrangement
where the id list named things and the journal merely agreed.

**Names come from there and numbers do not.** Its per-grade stats have provenance that varies per
figure, one row annotated as a guess, and a transcription bug that assigns the health multipliers to
the shield fields. Prices are read off real `BuySuit`/`BuyWeapon` events instead, and are blank
where none was ever seen.

`tools/gen-blueprints.py` builds the recipes, with the restatement described above. It refuses to
run if an on-foot ingredient size falls outside the measured tables, because one uncorrected number
sitting among corrected ones is invisible and is wrong by a factor of two or three.

**The modification name map is authored, and that is a deliberate exception.** Elite writes
`weapon_clipsize`; every recipe source writes "Magazine Size"; **nothing published carries both**,
and a relaxed match joins only 5 of the 13 symbols the corpus contains. The ship side answered the
same problem by refusing to guess, because 786 blueprints is not a table anybody can author. 25
modifications is. Each row records whether its symbol was read out of a real journal or follows the
convention and has never been seen, so a join d47 has watched work and one it merely believes in are
told apart.
