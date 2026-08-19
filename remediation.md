# Remediation 15

Reported from 2026-08-19 against **v0.37.0 and v0.38.0**, one item at a time and mostly with a
picture. Each is checked off as it ships, and **checked only once it has been seen to work** — a
change that compiles is not a fixed item.

**Remediation 14 is finished and its record has moved.** Seven of its nine shipped in
[v0.36.2](CHANGELOG.md), item 4 settled with the persona pack, and item 6 shipped whole as
[Phase 35](list.md) in v0.38.0. Its item 5 is **item 1 below**, carried over rather than closed.
Its item 7 stays where it was written up — [Phase 36](list.md), trade routes d47 computes itself,
unbuilt and asked for as its own session. This file is the current batch and not a growing
archive, which is why 14 is gone from it.

**Nearly all of this batch is the Loadout tab**, and most of it was found by one Commander
planning one Type-10 in one sitting. That concentration is the finding as much as the items are.

## The original asks

**This file is an interpretation, and the difference has already cost something.** The items below
were written up from sixteen requests made one at a time against a running build. The write-up
added framing, and in at least three places the framing was mistaken for the request: item 5 grew a
"sibling to decide" nobody asked for, item 4 closed off a choice that had been left open
(*"a clickable link **or** an up-down clicker"*), and item 12 turned a bug report into a design
coin-flip. So the requests are recorded here in the Commander's own words, and **where an item and
an ask disagree, the ask wins.**

| # | The ask, as made | Item |
|---|---|---|
| 1 | I expected to drag "Medium Hardpoint 3" onto "Small Hardpoint 1" and couldn't. | 1 |
| 2 | I should be able to tell the first two lasers apart by something besides price. The more expensive one should be more powerful, and I should know how. | 2a, 2b |
| 3 | When this page appears it should focus the Search box. I shouldn't have to click it. If what I want is on the list without scrolling, no harm done. | 3 |
| 4 | I should be able to skip "choose an engineering grade". 999 times out of 1000 it's 5. Make the grade a clickable link or an up-down clicker where it says "grade 5". | 4 |
| 5 | Same for a maximum grade of 1 — so it's whatever the maximum grade is, not always 5. | 4 |
| 6 | "Point Defence is not engineered" should be "is not currently engineered". | 5 |
| 7 | These are not the correct engineering options for armour. | 6 |
| 8 | Planning a Core Internal shouldn't make me choose when there's only one choice. It can't be anything else. | 7 |
| 9 | You have all this space — show what each engineering choice does in general. Not per grade; that goes on the grade page. | 8 |
| 10 | What's special about a Guardian Distributor? It should say. | 9 |
| 11 | If something's already engineered: a gear glyph next to its name in the slot list, and the engineering font in a different colour in the details pane. | 10 |
| 12 | Fuel tanks can't be engineered. | 6 |
| 13 | Every choice for what goes into a slot should show what's currently in the slot, if anything. | 11 |
| 14 | Not showing my just-entered engineering in the checklist. The Suggestions button is there, but it only says 1. | 12 |
| 15 | There should be a dropdown with each of my ships in it, so I don't have to switch ships in-game before setting a core. | 13 |
| 16 | `type9_military` where the hull name belongs. | 14 |

Fifteen from the Commander, plus #16 spotted in a screenshot. Sixteen asks, fourteen items: #5 folds
into #4, and #12 into #7.

## Two things run through this batch

Stated once here rather than fourteen times below, because most items are an instance of one or
the other, and fixing them one at a time will not stop the next one arriving.

**A. Joins that miss and fall back silently.** d47 holds four knowledge tables that name the same
things differently, every join between them is by name, and every failure is absorbed rather than
reported: coriolis to FDevIDs on `edID` (item 2a), module to blueprint on name (items 6, 7),
module to tech-broker on name (item 9), and the journal's own blueprint symbols to the blueprint
table (item 10). The last is not even an accident — `ChecklistNaming.Readable` calls its output
*"ugly and true"*, and `CannotConfirm` tells the Commander outright that **"nothing I ship joins
the two spellings"**. A failed join currently produces either *everything* or *nothing*, and both
read exactly like the feature working. **No join should fail quietly**: a generator that cannot
match a row should say so at build time rather than let a panel paper over it at runtime.

**B. Internal identifiers reaching what the Commander reads.** `PowerDistributor PrioritySystems`,
`ship 53`, `LargeHardpoint1`, `type9_military`, `Pulse Laser (subsurfdispmisle turret)`. This
overlaps A but is not the same thing, and is much the cheaper half: for `ship 53` and
`type9_military`, **d47 had the name in hand and printed the id anyway** — the fleet page one tab
over says "Oxen (Type-10 Defender)" correctly. The rule wanted is that no shown or spoken string is
composed from a symbol where a name exists, and that where no name exists the sentence says so
rather than prettifying a symbol into something that looks like one.

## The items

- [x] **1. Copy a plan to another slot by dragging.** *Built.* Carried over from remediation 14 item 5.
  Ctrl and left button held, dragged from one slot row to another, copies the module, the
  engineering and the experimental effect, matching the new slot's largest fitting size. Only
  within one kind: Hardpoints to Hardpoints, Utility Mounts to Utility Mounts, Optional Internal to
  Optional Internal. **Core Internal is neither draggable nor a target.**

  **It is a desktop-only item now.** The rows live at Loadout > Ships > Slot, and Loadout was
  withdrawn from the big VR panel in v0.37.0, so there is no headset half left to design. The VR
  gesture analysis, and the one correction that reversed it, are in
  [v0.37.0's changelog](CHANGELOG.md) — worth reading only if Loadout ever returns to that panel.

  **The reported example is the acceptance test, and it is better than an invented one.** Medium
  Hardpoint 3 carries a plan for a 2F Pulse Laser turreted, grade 5 Long Range, Flow Control. Small
  Hardpoint 1 has no plan and a fitted 1G Pulse Laser turreted. The size rule resolves
  `hpt_pulselaser_turret_medium` to `hpt_pulselaser_turret_small`, which is 1G — **the module the
  target already has**. So the module half of the copy is a no-op and the whole value is the
  engineering. An implementation that skips the copy when the resolved module matches would do
  nothing here and look exactly like the reported bug.

  The size and kind rules belong in Core with tests; `EliteSpecifications.ModulesFor` and
  `ShipSlotKind` already answer both. **One rule the item does not yet state**: what happens when a
  blueprint or an experimental does not exist for the downsized module. Long Range and Flow Control
  both exist on a small pulse laser, so the reported case is clean — but *copy what transfers and
  report what did not* is different work from *refuse the copy*, and it wants deciding rather than
  discovering.

  **Decided 2026-08-19, and the reserved decision turned out not to exist.**

  **There is no partial-copy case.** `Blueprints.tsv` carries `kind, module, name, grade, engineers,
  ingredients, effects, guid` — **no size, no class, no rating**. Blueprints and experimentals are
  keyed on the module *name* alone, and downsizing preserves the name, so the whole set always
  transfers. *Copy what transfers and report the rest* versus *refuse the copy* was a policy about
  something that cannot happen.

  **What replaces it: the module may not come small enough.** Measured over both kinds, and the
  first measurement was scoped to hardpoints only and generalised, which the Commander corrected.

  - **20 of 60 hardpoint names do not come in size 1** — Plasma Accelerator is 2-4, Pacifier
    Frag-Cannon is 3 only, Pack-Hound Missile Rack is 2 only. Dropped on a Small hardpoint they
    resolve to nothing.
  - **10 of 59 non-hardpoint names have an interior hole.** Six limpet controllers are **odd sizes
    only — 1, 3, 5, 7**; Planetary Vehicle Hangar is **2, 4, 6**; Experimental Weapon Stabiliser is
    3 and 5; Corrosion Resistant Cargo Rack is 1, 4, 5, 6.

  **So "the new slot's largest fitting size" must search, not clamp.** `min(slotSize, moduleMax)` is
  wrong: it resolves a size-7 Collector Limpet Controller onto a size-4 slot as size 4, which does
  not exist. The rule is `max(s in sizes where s <= slotSize)`, and where no such size exists there
  is no target.

  **A drop with no valid target is disallowed and shown as disallowed** — greyed, during the drag,
  rather than accepted and then explained. An invalid target that never highlights is the ordinary
  idiom, it is discoverable while the mouse is still down, and it needs no dialog. Same rule as the
  kind constraint already stated, with one more condition: *and the module fits*.

  **A drop overwrites whatever is in the target.** No confirmation, no merge: dragging an engineered
  Multi-Cannon onto a slot planned for a pulse laser replaces it, which is what dragging means.

- [x] **2a. A mining missile is shipping as a Pulse Laser.** *Shipped in v0.38.1.* Reported as
  *"I should be able to differentiate between the first two lasers by something besides the
  price"* — and the honest
  answer is that the expensive one is not a laser. `hpt_mining_subsurfdispmisle_turret_small` is a
  **Sub-surface Displacement Missile**, turreted, and the table carries it as `name: Pulse Laser,
  mount: Fixed, 1B, 38,750 cr`. It reaches the pulse-laser chooser because `AskModule` groups the
  offered modules by `Name`, so a wrong name does not merely mislabel a row — **it files the module
  under a different weapon**.

  The generator joins coriolis-data to FDevIDs on `edID` and trusts the result. Checked against the
  symbols, **nine hardpoints disagree with their own ids**: four have their mounts transposed (both
  `subsurfdispmisle` pairs — `_fixed_` ids reading Turreted and `_turret_` ids reading Fixed), and
  five carry a blank mount where the symbol states one (AX Missile Rack twice, Heat Sink Launcher,
  Caustic Sink Launcher, Point Defence). Separately, **four names carry a raw symbol fragment**:
  `Pulse Laser (fixed seismchrgwarhd)`, `Pulse Laser (subsurfdispmisle turret)`,
  `Frame Shift Drive (mkii overchargebooster)` and `Thrusters (mkiiagileboost)`. **Two of those
  four sit in core sockets**, so every Commander with that ship meets them. The seismic charge one
  is the reason a mount check alone is not enough — its mount is right and only its name is wrong.

  Two assertions catch the lot, and both belong in the generator: **mount against the symbol's own
  `_fixed_`/`_turret_`/`_gimbal_` infix**, and **name against the symbol's family stem**. That is
  the two-resolver shape that already caught a renamed system and an invented one in Phase 23.
  **These rows are in the shipped table**, so this is a candidate for its own patch ahead of the
  rest of the batch.

  **What shipped, and the one correction.** Every measured claim above held: nine hardpoints
  disagreed with their ids, four names carried a raw fragment, two of those in core sockets. The
  cause is upstream — five coriolis `edID`s do not lead where they claim, three of them pointing at
  `hpt_pulselaser_fixed_small`, which is where "Pulse Laser" came from.

  **The mount assertion works and is in.** Flagged 4 rows across all of `outfitting.csv`, every one
  a genuine defect, no false positives — and made two-sided, since a symbol with *no* infix must
  carry no mount, which is what catches `Int_MkIIAgileBoost_Engine_Size5_Class5` filed under the
  literal mount `"mount"`.

  **The name-against-stem assertion does not, and was replaced.** Measured before wiring: it flags
  **427 of ~1030 rows**, nearly all correct. Frontier's symbols carry no textual rule to the display
  name — `hpt_drunkmissilerack` is a Pack-Hound Missile Rack, `hpt_crimescanner` a Kill Warrant
  Scanner, `hpt_mrascanner` a Pulse Wave Analyser. There is no threshold that separates those from
  `subsurfdispmisle` reading "Pulse Laser".

  What replaced it is exact rather than fuzzy, and serves the same goal better: **both sources carry
  the symbol**, so the id that claims to link two rows is asked whether it landed on the row it says
  it did. Five mis-keys, no false positives, no heuristic. The fallback when it misses is a **lookup
  by symbol**, which named 33 modules that were previously named from their source file — including
  both Mk II core modules, whose real names Frontier had all along.

  **Three things the report had not reached**, all the same defect class:
  1. **35 symbols are declared twice by coriolis**, one of each pair a husk with no cost. The winner
     was whichever `sorted()` put last, and it had already landed badly — the large AX Missile Rack
     shipped priced at **zero**, in the chooser whose complaint is that price is the only
     discriminator. The keyed entry now wins.
  2. `int_expmodulestabiliser_size5_class3` read **"Experemental Weapon Stabilizer"** — two typos,
     beside a correctly spelled size 3.
  3. `hpt_missing_hardpoint` and `hpt_missing_utility` were in the table, both named "Missing
     Hardpoint". `PLACEHOLDERS` matched `int_missing_` only.

  Five assertions now run **in CI against the shipped table**, not only in the generator, which is
  not part of the build. All five were confirmed to fail against the old table before the fix was
  kept. `disambiguate` now alters no name at all, and that is asserted too: every module in the
  table is named by the naming authority, and a qualifier reappearing means a name went missing
  upstream.

- [x] **2b. Nothing in the table says what a module does.** *Built.* The reported ask: *"the more expensive
  one should be more powerful, and I should know how"*. `EliteSpecifications.tsv` carries mass,
  power, integrity and cost, and **no damage, no DPS, no rate of fire, no range** — so no chooser
  can say why one weapon beats another, and price is the only discriminator on the row.

  **The shape already exists**, which is what makes this ordinary work: the table already carries
  type-specific columns beside the generic four — `optimal_mass, max_fuel, fuel_power,
  fuel_multiplier` for drives, and `hull_boost, kinetic_res, thermal_res, explosive_res,
  caustic_res` for armour. So this extends a pattern rather than inventing one. **This and item 9
  are one job**: the same generator pass, different columns.

  Worth deciding when it is specced: which figures earn a place on a chooser row that already
  carries four. DPS does the work of several and is the number people compare; damage per shot and
  rate of fire may belong on the slot page instead. And DPS is **not a lookup** — it is damage
  times rate of fire, and burst weapons carry a burst interval and shots per burst that do not fit
  that formula. If the source data turns out thin or inconsistent, this stops being a generator
  change and becomes a question about what d47 can honestly claim, which is the same shape as
  Phase 36's saturation figure: measure it, or say plainly that it is not modelled.

  **Decided 2026-08-19: carry the components, and compute DPS with coriolis's own formula.**

  **The reported pair is answered by 2a rather than by figures, and the premise was wrong.** Named
  correctly, the small fixed Pulse Laser does 2.05 damage every 0.26s and costs 2,200; the
  Sub-Surface Displacement Missile does 5 every 2.0s and costs 38,750. The expensive one is *less*
  powerful — about 2.5 damage per second against 7.9 — because it is a mining tool. "The more
  expensive one should be more powerful" does not hold, and it stopped needing to the moment the
  thing stopped calling itself a laser.

  **The source is rich, not thin.** Of 215 hardpoints, **178 carry damage**; the other 37 are
  scanners, chaff, heat sinks and limpet controllers, correctly carrying none. Also present:
  `range` (74%), `thermload`, `distdraw`, `piercing`, `clip`, `reload`, `ammo`, and `damagedist` —
  the damage *type* split, which is thermal 59, kinetic 49, explosive 28, absolute 8 and 34 mixed.
  **`damagedist` is probably the best discriminator on the page and nobody asked for it**: why one
  weapon beats another is usually thermal-versus-kinetic against what you are shooting, not a bigger
  number.

  **The note above missed a case.** It warned that burst weapons break `damage x rate`. True, but
  **18 weapons carry no `fireint` at all — every beam laser and every mining laser** — because they
  are *continuous*: their `damage` already is damage per second. Dividing by a missing interval
  would not merely be wrong, it would be arithmetic on nothing.

  **The formula is lifted rather than derived**, which is the whole point:

  ```js
  RoF = burst / (((burst - 1) / burstRoF) + 1/intRoF + charge)
  DPS = damage * roundspershot * RoF
  ```

  from `EDCD/coriolis`, `src/app/shipyard/Module.js`. Its `LICENSE.md` says outright that **the code
  is MIT and the JSON data is Frontier's**, so taking the formula takes the MIT half — the same
  distinction the game-data invariant already draws, applied in the direction that is allowed. There
  is also `getSustainedFactor()`, folding clip and reload into a sustained figure.

  One formula covers all three models, and the continuous case falls out of `getDps`'s own
  `|| 1`: a beam has no `rof`, so the expression is `NaN`, `NaN || 1` is 1, and `DPS = damage`.
  **Verify that reading against the running site before relying on it** rather than trusting this
  paragraph.

  **Where it is shown is item 8's layout**, which the Commander drew on the *module* chooser. Row
  keeps its name and size; the space to the right carries the full specification of the highlighted
  module. One layout pattern, both choosers.

  **The formula is validated against an independent reference.** The Commander supplied EDOMH's
  panel for a 1G turreted Pulse Laser. Every figure coriolis carries matches it — damage 1.19, cost
  26,000, mass 2, power 0.38, distributor draw and thermal load 0.19, piercing 20, range 3,000,
  falloff 500 — and so do **both computed values**: RoF `1/0.3` = 3.333 against EDOMH's 3.33/s, and
  DPS `1.19 x 3.333` = 3.967 against its 3.97HP/s. Only `breachdmg` differs, 1 against 1.01, which is
  below anything worth showing. **EDOMH calls `fireint` "Burst Interval" and shows rate of fire
  derived from it**, so d47 says *rate of fire* too: that is the number people read.

  **Ten figures, which is what "relatively simple" came to.** Damage per second, damage and its
  type, rate of fire, maximum range, thermal load and distributor draw, beside the four the table
  already carries — mass, power draw, integrity, cost. Left out and available later: armour
  piercing, jitter, breach damage and its chances, damage falloff start, boot time, and the seven
  boolean flags. None of them answer *which of these two should I buy*.

  **Damage type is not one value, and engineering moves it.** Raised by the Commander and measured:

  - **156 of 178 distributions sum to exactly 1** — real proportions. A Rail Gun is 67% thermal and
    33% kinetic, a Plasma Accelerator 60% absolute, 20% kinetic, 20% thermal.
  - **22 sum to 2, and they are exactly the 22 carrying `X`.** So `X present` and `sums to 2` are
    the same set, which makes the rule exact rather than a guess: on an anti-xeno weapon `X` is a
    **marker beside a real type**, not a share of it. Rendering it as a percentage would print
    "100% AX, 100% kinetic", which reads as broken.
  - **Six experimentals convert damage type** — Inertial Impact to kinetic, High Yield Shell to
    explosive, Incendiary Rounds and Overload Munitions to thermal.

  **And the source will not say by how much.** Those effects are recorded as
  `Damage partially kinetic|✓|good` — the delta is a **tick, not a number**. So the line says the
  conversion happens and quotes no figure, which is the Phase 36 saturation rule again: say plainly
  what is not modelled rather than print something that looks computed. Three renderings follow —
  proportions as percentages, `X` as *"effective against Thargoids"*, and a converting experimental
  as a statement with no number.

  **One incidental to fix before building on this column**: `effects` separates entries with `;`,
  not `,`. An attribute tally here split on the comma and undercounted.

- [x] **3. A searchable chooser should take the keyboard when it appears.** *Built.* Reported against the
  module chooser: *"I should not have to click it."* The precedent already ships — `PanelPrompts`
  focuses the text-entry prompt on `AttachedToVisualTree` (remediation 10 item 11), and the comment
  there already disposes of both objections: nothing in the headset sends a keystroke, and the
  panel swallows the push-to-talk key before any control sees it, which is what stops holding it
  filling the box with brackets. The searchable chooser wants the same line on its search box.

  **Every searchable chooser, not only the module one** — the box is built from
  `request.Searchable`, and the reporter's own argument (if the thing being looked for is on the
  list without scrolling then fine, focusing has not hurt anything) holds wherever the list is
  short. Down-arrow walking from the box into the list is a nice-to-have and explicitly not
  required.

- [x] **4. Do not ask which grade.** *Built.* *"999 times out of 1000 it will be 5."* Measured: of **160**
  module-and-modification pairs, **155 reach grade 5**. The whole exception set is five, small
  enough for a test to name every one: Chaff Launcher, Heat Sink Launcher and Point Defence each
  stop at grade **1** on Ammo Capacity, and Shield Cell Bank stops at **4** on both Rapid Charge
  and Specialised.

  So **the rule is the highest grade the blueprint offers, not the number five** — `offered` is
  already `OrderDescending()`, so it is `offered.First()`. `AskGrade` already opens with
  `const int Usual = 5` and preselects it; today's fallback when 5 is not offered is `null`, which
  means **"Any grade"**, and silently landing on *any* is worse than landing on the top. The
  reported Ammo Capacity case is exactly that: a two-row page, one real answer, nothing preselected.

  **The grade becomes a stepper on the slot page, not a link.** Changing the grade changes what is
  underneath it — `EngineeringRules.RollsFor` turns grade and engineer rank into a roll count, which
  drives the *What it costs* block — so a stepper answers *what would grade 4 cost me instead* in
  place, which is the actual question somebody has when they touch that number. A link that reopens
  a chooser gets you back to where you started.

  Three things it needs. `SlotPlan.Describe()` is **one string that is both the line shown and the
  line spoken**, so the slot page composes from parts and `Describe()` stays whole. The stepper
  **clamps to `offered`**, so it stops at 3 where the recipe stops at 3. And it wants **a route back
  to "any"** — `SlotPlan.Grade` is `int?` where null is a documented wildcard, and *Long Range, any
  grade* is a real thing to want; stepping below 1 into "any" is the obvious answer, if a wildcard
  is worth a position at all.

  **One open question**: a grade 5 plan needs a grade 5 engineer, and `RollsFor` already knows the
  Commander's rank with each. Once the grade stops being a deliberate choice, a plan can quietly
  assume a roll nobody can do yet. Whether the page says so — *grade 5, and Broo is only grade 3 for
  you* — or stays quiet on a page already carrying a materials list, is a judgement about nagging
  rather than a defect.

  **Decided 2026-08-19, and two of the three questions above were not the Commander's.** Asks #4
  and #5 say only: don't ask for the grade, default to the blueprint's highest, and make it *"a
  clickable link or an up-down clicker"*. The write-up above picked the clicker and argued for it;
  the Commander has now confirmed the **stepper**, so the argument was right and the closing-off
  was still wrong. The engineer-rank question was invented here and is answered below on its own
  merits.

  | Question | Settled |
  |---|---|
  | Control | **Stepper.** Confirmed, not assumed |
  | Default | **The blueprint's highest offered grade**, never the number 5 |
  | "Any grade" | **Deleted. It is not a thing** |
  | `SlotPlan.Grade` | **`int`, not `int?`** |
  | A blueprint offering one grade | **Do not print the grade at all** — it is superfluous |

  **`int` rather than `int?` is the decision that carries the others.** The wildcard cannot come
  back through a nullable field nobody is watching. Two consequences follow and neither needs
  deciding: a stored plan carrying `"Grade": null` from the wildcard era reads as the blueprint's
  highest offered grade, which is where a new plan would land anyway; and a plan naming a module
  with no engineering has no grade to hold, so it holds `0` and never renders it — `Describe()`
  already prints a grade only beside a blueprint, and its grade-without-a-blueprint branch becomes
  dead code to remove.

  **The suppression rule keys on *offers one grade*, not on *the maximum is 1*.** Measured: 3 of
  160 module-and-blueprint pairs offer a single grade — Chaff Launcher, Heat Sink Launcher and
  Point Defence, all on Ammo Capacity, all grade 1 — so the two rules select the same rows today
  and would not if Frontier shipped a single-grade-3 recipe. Those three get no stepper either:
  there is nothing to step. The grade stays in the data, because the checklist and the costing need
  it; only `Describe()` omits it, which is the one string that is both shown and spoken.

  **The rank question, answered.** `EngineeringRules.RollsFor` returns **null** when `rank < grade`,
  so stepping past the Commander's rank does not produce a wrong cost — it produces **no cost**, and
  the *What it costs* block goes blank on its own. So this was never a choice between nagging and
  staying quiet; it is whether an already-blank block explains itself. It should: one line, only
  when the cost is unavailable, and nothing at all when it is not.

- [x] **5. "Point Defence is not engineered" should be "is not currently engineered".** *Built.*
  `ChecklistEvaluator` line 217. Right for a reason beyond taste: that verdict is a **reading taken
  at a moment**, not a property of the module — list.md Phase 26 has a plan carrying the journal's
  verdict with its date, standing as of when it was taken, and the state it ships with is
  `ChecklistState.Open`, meaning still to do. *Is not engineered* reads as a fact about Point
  Defence; *is not currently engineered* reads as a fact about right now, which is the only thing
  d47 knows. It is in Core, so it lands on the spoken path too, and reads well aloud.

  **One sibling to decide**: `EngineeringCapability` appends a compact `not engineered` to a report
  line. Match it, or leave it terse because a dash-separated report already reads as a snapshot.

  **The sibling was never asked for, and is left terse.** Ask #6 is one string in one place, and it
  shipped. The "one sibling to decide" above was added by this write-up rather than by the
  Commander, and carrying it forward as a decision awaiting them cost a round of discussion for
  nothing — the first of the three cases that put `## The original asks` at the top of this file.

  Left alone on its merits as well as on scope. `EngineeringCapability.Line` has two callers, and
  the `— not engineered` branch is **dead** on the one that iterates `engineered`; the only place it
  renders is the disambiguation list — *"3 fitted modules match 'shield'"* — where it is a
  **discriminator in a picker**, not a verdict, and sits beside `Reinforced, grade 3, complete`,
  which is equally a reading-at-a-moment stated flat. If *not engineered* needs "currently", so does
  *complete*, and that ends in hedging every row.

  One thing noticed in passing and not fixed: that dead branch means `Line` serves two callers with
  different contracts. Worth a note rather than a change.

- [x] **6. A chooser offers blueprints that cannot exist.** *Built.* Two reports, needing **opposite**
  answers, which is what proves the defect is the fallback rather than the data.

  **Armour** offers every blueprint in the game — Dirty Drive Tuning, Ammo Capacity, Efficient
  Weapon — on a Type-10's Lightweight Alloy. Bulkhead names carry the hull (the generated table
  says so outright: forty-eight hulls have a Lightweight Alloy), so the module is
  `Type-10 Defender Lightweight Alloy` while `Blueprints.tsv` keys armour recipes under plain
  **`Armour`**. The join misses. What should be offered is five: Blast Resistant, Heavy Duty,
  Kinetic Resistant, Lightweight, Thermal Resistant, grades 1 to 5.

  **Fuel tanks** offer the same forty and **cannot be engineered at all** — verified, zero blueprint
  rows of any kind mention Fuel Tank, and the twenty `Fuel*` rows are Fuel Scoop and Fuel Transfer
  Limpet Controller, both genuinely engineerable. Here d47's data is right and the panel is wrong.

  So the fallback in `AskBlueprint` is hiding **three** states behind one condition:

  | | Module | Right behaviour | Today |
  |---|---|---|---|
  | 1 | none chosen yet | show everything | correct |
  | 2 | chosen, blueprints exist under another name | show its five | shows all forty |
  | 3 | chosen, genuinely has none | skip the step and say so | shows all forty |

  **Cases 2 and 3 are indistinguishable to the code** — both are `recipes.Count == 0` — which is
  exactly why one fallback covers both and is wrong in both. A fuel tank slot is still plannable,
  just not engineerable: *Plan this slot* should ask which tank and then stop.

  **First step is a harness, and its job is classification rather than counting.** Push all 125
  module names through `ForModule`, print which return empty, then sort those into case 2 and case 3
  by hand.

  **The harness ran, and the classification does not have to be done by hand after all.**
  119 non-bulkhead module names reach the Loadout tab. **38 find blueprints and 81 find none** — so
  the fallback is not a corner case, it is what two thirds of the modules in the game hit.

  `Blueprints.tsv` has only **63 module keys**, and a third of them are not modules: `Limpets`,
  `SRV Refuel`, `AFM Refill`, `FSD Injection`, `Suit`, `Weapon`, `Unlock` and the munitions rows are
  Elite's *synthesis* and tech-broker recipes, which is what `kind` already separates. Of the 25
  keys nothing reaches, only four are real case 2: **`Armour`**, **`Surface Scanner`**,
  **`Wake Scanner`** and **`Manifest Scanner`**.

  **Do not hand-write the alias table.** The `module` column is EDEngineer's `Type` field verbatim
  — a free-text vocabulary of its own — and EDEngineer carries no FDev symbol, so name-to-name is
  the only join available *from that side*. A hand-authored map from "Detailed Surface Scanner" to
  "Surface Scanner" is precisely the game data the invariant forbids, and it would need re-checking
  every time Frontier ships a module.

  **It is derivable, exactly, and the loop closes in coriolis.** `modifications/modules.json` maps a
  coriolis **group code** to the blueprints that group accepts, by `fdname` — `bh` to
  `Armour_Advanced` and the rest, `ss` to `Sensor_Expanded`, `ws` and `cs` to `Sensor_FastScan`. The
  `modules/*.json` files are keyed by those same group codes and carry each module's **symbol**, and
  the specification table is keyed on symbol. So:

      blueprint guid -> coriolis blueprint fdname -> group code -> module symbols -> the spec table

  Every step is an id join in data d47 already fetches, and `gen-blueprints.py` already joins the
  guid. That makes case 2 and case 3 a **property of the data rather than a judgement**: a module's
  group either appears in `modules.json` with blueprints or it does not, and the panel can be told
  which without anybody deciding it.

  Two consequences worth stating. The join stops going through `Catalogue.Match` at all, so the
  fuzziness the report worried about becomes moot rather than tuned. And it wants a column: the
  module rows need their coriolis group, which `gen-elite-specs.py` iterates already and throws away.
  **Measured, and it is two thirds of the way there rather than all of it.** The chain above is
  right about the group codes and wrong about the guid. `CoriolisGuid` is **not** a per-module key:
  coriolis models `Lightweight` as *one* blueprint that 23 module types share, and EDEngineer
  splits it into 23 entries that all carry the same guid. So group → fdname → guid → Type fans out
  to all 23 and identifies nothing. Fifty guids are shared this way, of 488.

  Matching on **guid-set equality** instead — a group's whole set of blueprint guids against a
  Type's whole set — is much better, and settles the cases the item leads with:

  - **25 of 87 groups resolve to exactly one Type.** `bh` → `Armour` is one, which is the reported
    armour case. So are all three shield generators — `sg`, `bsg` and `psg` all → `Shield Generator`
    — so **Bi-Weave and Prismatic are case 2 as well**, which the report had not listed.
  - **43 groups carry no blueprints at all**, which is case 3 stated by the data rather than
    inferred. `ft` is among them: fuel tanks genuinely cannot be engineered, exactly as reported.
  - **19 groups do not resolve**, and two are case-2s from the list above — `cs` (Cargo Scanner /
    `Manifest Scanner`) and `ws` (Frame Shift Wake Scanner / `Wake Scanner`). Their sets differ
    from any Type's by the shared `Lightweight` guids, so equality is too strict and containment is
    too loose.

  **Stopped here deliberately.** The remaining 19 want either a further derivation — scoring on the
  guids a group does *not* share is the obvious next try — or a decision, and hand-writing the
  mapping for them is the thing the game-data invariant exists to prevent. Everything above is
  reproducible from the skipped harness in `BlueprintJoinHarness`. None of the fix is built, and
  the `group` column the chain needs was written, verified against all 1,164 modules and then
  **backed out again**, so the shipped table carries nothing inert while the fix is unbuilt.


  **One thing v0.38.1 changed here.** `int_detailedsurfacescanner_tiny` used to be named
  *Surface Scanner*, which joined to the blueprint key of that name by accident; item 2a corrects it
  to **Detailed Surface Scanner**, which does not. Nothing regressed that was not already broken for
  the other 80 — but it means the case-2 list above is measured against v0.38.1 and not against
  v0.38.0. 35 of them near-miss a blueprint key on a substring test — `Advanced Multi-Cannon`,
  `Bi-Weave Shield Generator`, `Retributor Beam Laser`, `Frame Shift Drive (SCO)`,
  `Pack-Hound Missile Rack` and so on — but `ForModule` goes through `Catalogue.Match`, which is
  fuzzy, and `Same` is already case-insensitive, so an unknown share of those already resolve.
  **Reading the matcher will not settle it and running it will.** That classification is the fix's
  input, and nothing else produces it.

  **Decided 2026-08-19: re-key the blueprint table, and no classification by hand is needed after
  all.** The whole difficulty above came from solving the wrong join.

  **The rarity-weighted scoring the note suggests as "the obvious next try" is worse than useless.**
  It resolves 27 groups instead of 25 and gets some of them **wrong** — `hs` (Heat Sink Launcher),
  `ec` (Electronic Countermeasure) and `po` (Point Defence) all come out as *Chaff Launcher*. Those
  four take an **identical blueprint set**, so from the guid side they carry no information that
  tells them apart, and scoring simply picks one with confidence. A confident wrong answer is thread
  A's own failure mode, so that path is closed permanently rather than under-tuned.

  **`modifications/modules.json` maps group to blueprint *fdname* exactly** — `bh` to
  `Armour_Advanced`, `ss` to `Sensor_Expanded`. That answers *which blueprints can this module take*
  with no ambiguity for all 87 groups. The ambiguity was only ever in mapping a group to
  **EDEngineer's `Type`**, which was needed only because `Blueprints.tsv` happens to be keyed on
  `Type` — an accident of how the table was built, not something the data forces.

  So: **key the blueprint table on coriolis's fdname and carry the group.** The offer becomes exact,
  the 19 unresolved groups stop existing, and nothing is hand-written. EDEngineer stays the source of
  ingredients, engineers and effects, joined on the guid, which it is reliable for.

  **What the disagreements actually were.** Every module name in the specification table, checked
  for whether its group carries blueprints against whether the running `ForModule` finds any — 275
  disagreements, and they are three causes plus a mistake of mine:

  | Cause | Count | |
  |---|---|---|
  | **Bulkheads** | **241** | The name carries the hull, the table keys on `Armour`. **This is ask #7, and it is one fix** |
  | **Variant modules** | ~30 | Bi-Weave and Prismatic Shield Generator, Frame Shift Drive (SCO), Detailed Surface Scanner, Cargo Scanner, Pack-Hound Missile Rack, Retributor Beam Laser, the Mk II thrusters, Guardian Hybrid Power Plant and Distributor |
  | **A measurement error** | 4 | Guardian Gauss, Plasma and Shard Cannon, and Shock Cannon. I tested only the `blueprints` key and ignored `specials`, which 22 groups carry. Not defects |
  | **Agreeing** | 85 | 34 engineerable, 51 correctly with none — `ft` among them, which is **ask #12** answered by the data |

  **Not to be built as a lookup from names.** The group answers *whether*, exactly; the fdname
  answers *which*, exactly; and a module whose group has blueprints but whose name resolves to none
  is a **generator-time report**, never a silent fallback. That is thread A's rule applied to its own
  worst case.

  One residue, and it is cosmetic: where a single fdname is shared by 23 module types, EDEngineer
  gives it two display names — `Lightweight` and `Lightweight Mount`. That is the same ambiguity in
  miniature, but it now decides only **what a row is called** and never **whether it is offered**.
  Report it and take the majority spelling.

- [x] **7. Never ask a question with one answer — or with none.** *Built, with one exception below.* Reported against Life Support:
  *"there is only one choice, it can't be anything else."* `AskModule` early-outs when
  `offered.Count == 0` and not when it is 1, so one module name still draws a two-row page —
  "Anything — I only want the engineering" plus the only answer. Skipping to `AskVariant` lands on
  the 5A-5E question, which is a real choice, so nothing useful is lost.

  Taking the single option records `Module = "Life Support"` where choosing *Anything* would leave
  it null. **That is better rather than merely different**: for a socket that accepts one type,
  *anything* and *the one thing it takes* are the same want, and the plan line reads properly
  instead of opening with a bare grade.

  **Do not build this as "Core Internal does not ask."** The Frame Shift Drive socket is a core
  internal offering **three** module names — `Frame Shift Drive`, `Frame Shift Drive (SCO)` and
  `Frame Shift Drive (mkii overchargebooster)` — and SCO is a real decision. A core-internals rule
  would suppress a question that needs asking. The rule is *one option, take it*, wherever it
  occurs, and `AskVariant`, `AskEffect` and `AskGrade` want the same check while the fix is open.
  Item 6's case 3 is the same rule one notch further: never ask a question with **no** answers.

  **Built in `AskModule` and `AskGrade`. `AskVariant` already had the check** — it early-outs at
  `offered.Count <= 1` and says why. So the sweep found one of the three already done, which is
  worth knowing before the next item generalises from the same list.

  **`AskEffect` is deliberately excluded, and the report's grouping is wrong on it.** The rule
  rests on the item's own argument — that *anything* and *the one thing it takes* are the same
  want — and that is true of a module socket and false of an experimental. The decline on the
  effect page is **"No effect"**, which the single effect does not satisfy; they are opposite
  wants, not the same one. Auto-taking it would put an experimental on the plan nobody asked for,
  which is a worse defect than the extra press. The exclusion is commented at the site so the next
  sweep does not "fix" it.

  One consequence worth noting for item 4: `AskGrade` now takes a lone offered grade without
  asking, which covers the reported Ammo Capacity case (grade 1 only) on its own. What item 4 asks
  for beyond that — the highest offered grade rather than the number five, and the stepper — is
  untouched and still open.

- [x] **8. A blueprint row should say what the blueprint does.** *Built.* *"Since you have all this space,
  show what each of the engineering choices do in general — not each specific grade."*

  The data supports it. `Blueprints.tsv` carries an `effects` column shaped as attribute, delta and
  a good-or-bad flag, so `Lightweight` becomes *less mass, at the cost of integrity* and
  `Short Range Blaster` becomes *more damage, at the cost of range and heat* — and that flag hands
  over the gains-then-costs ordering for free, so d47 never has to be taught which way is better.

  **It must be derived, not written.** A hand-authored blurb per blueprint is exactly what the
  game-data invariant forbids, and it is the call Phase 34 already made in refusing to hand-write
  Frontier's rank ladders. The generator has the column; the panel composes the sentence.

  Two things for whoever builds it. **34 of 160 blueprints change their attribute set across
  grades** — always by *adding* at higher grades in the two inspected, with each attribute's
  direction constant — so the general line is built from the union, or from the top grade where that
  is the superset; settle which by checking rather than assuming. And the attribute names are
  **Frontier's** — `Optimal Multiplier`, `Optimal Mass` — opaque out of context but matching the
  outfitting screen; keeping their words is the same argument the slot headings already won.

  **This collides usefully with item 4.** The per-grade numbers were to live on the grade choice
  page, and item 4 deletes that page. Their home becomes the **stepper on the slot page**: move the
  grade, watch the numbers move. That is a better place for them than a page passed through once,
  and it is the second independent reason the grade should be a stepper.

  **The layout, drawn by the Commander on the running build 2026-08-19.** Ask #9 said the per-grade
  numbers *"go on the grade page"*, and item 4 deletes that page — so this settles where both halves
  live, and it is cheaper than the note above assumed.

  **The blueprint page**, which is the wide chooser the ask was pointing at. Per row:

  - **On the button**: the blueprint's name, and under it the *general* line — *"less mass, at the
    cost of integrity"*. That is `ChoiceOption.Detail`, which the chooser already renders, so the
    half of this item the ask actually asked for needs no new mechanism at all.
  - **In the empty space to the right**: the *exact* figures, for the grade currently selected.
  - **At the far right**: the grade stepper — **beside the row and deliberately not part of it**.

  That last detail is the whole design. A stepper *on* the button would have to modify a selection
  that has not been made yet, which means the blueprint step stops being a `ChoiceRequest` and
  becomes a bespoke page. A stepper *beside* the button does not: the row stays commit-on-click, the
  Commander sets the grade and then picks the blueprint, and it commits at that grade. **The generic
  chooser survives untouched**, and only the module, variant and effect steps go on using it
  unchanged.

  **One stepper for the page rather than one per row.** Not the Commander's call and recorded as
  mine, so it is cheap to reverse: the point of the right-hand column is comparing blueprints, and
  comparing them at different grades is not a comparison. Each row shows its effect at the page's
  grade, clamped to its own maximum.

  **The Fitted pane** — what the Commander calls the details pane, on the right of Loadout > Ships >
  Slot — carries the same two things:

  - the grade inside the *Planned* line becomes a **stepper**, so it can be nudged without reopening
    the chooser, and the *What it costs* block below re-costs as it moves;
  - an **Effect** block describing what the chosen engineering does, shown only where something has
    been chosen.

  **Still to settle by measurement rather than by asking**: 34 of 160 blueprints change their
  attribute set across grades, so the general line is built either from the union or from the top
  grade where that is a superset. Check, do not assume.

- [x] **9. A module row should say what is special about that module.** *Built.* *"What's special about a
  Guardian Distributor? It should say."* Two answers, and they are different.

  **The numbers are not there**, and that is item 2b — distributor capacity and recharge are the
  same generator pass as weapon damage.

  **The provenance is there and will not join**, and it is arguably the better sentence anyway: d47
  already holds 51 `tech-broker` rows including the Guardian family, and *you unlock this at a tech
  broker* explains why you do not already have one, which no capacity figure does. Except the module
  table says **`Guardian Hybrid Power Distributor`** and the tech-broker table says
  **`Guardian Power Distributor`**. Thread A again.

  **Decided 2026-08-19: lead with Frontier's own description, figures underneath.**

  **The answer was in the source and nobody had looked.** coriolis carries **`ukDiscript`** — Frontier's
  own description of the module — on **732 of 970 modules (75%)**, and `ukName` on 76%. For the
  reported example it says outright: *"Enhanced with Guardian technology to speed up capacitor
  recharge rates, at the cost of smaller capacitors and increased heat generation. Also boosts
  overall power output of any power plant it is hooked into."* That is ask #10 answered in one
  sentence, from an id-keyed field, in **Frontier's own words** — which is the shape the game-data
  invariant already prefers and `NOTICE` already practises.

  The figures corroborate it: a 7A Guardian distributor against a standard one is WEP 43 at 8.5/s
  against 61 at 6.1/s, SYS and ENG 31 at 5.2/s against 41 at 4/s, integrity 56 against 144, and
  roughly twice the price. Smaller capacitors, faster recharge, exactly as described.

  **A correction to item 2b's field set, which was mine.** The ten figures listed there are
  **hardpoint-specific** — damage, DPS, rate of fire, range — and *none of them apply to a
  distributor*, which carries `wepcap/weprate`, `syscap/sysrate` and `engcap/engrate`. The table
  already carries drive-specific and armour-specific columns, so per-kind figures are the established
  pattern; the error was proposing a weapons-only set as though it were general.

  **The tech-broker join is worse than reported and matters less.** Only **7 of 51** tech-broker rows
  match a module name. Around 30 are parenthetical variants — *"Guardian Plasma Charger (Fixed,
  Large)"* — which strip mechanically; 3 are ship-launched fighters, correctly not modules; the rest
  are pre-engineered *Modified* / *Engineered V1* / *Sirius Modified* variants. And **`ukName` bridges
  the reported mismatch outright**: coriolis calls it *"Guardian Power Distributor"*, which is the
  tech-broker table's spelling, where FDevIDs says *"Guardian Hybrid Power Distributor"*. Lower
  priority regardless, because the description usually implies the unlock anyway.

- [x] **10. An engineered module should look engineered.** *Built.* Two asks, and the mechanisms exist:
  `LoadoutLine` already carries a `LoadoutTone` (the module name takes `LoadoutTone.Body` and the
  engineering line takes the default), and `LoadoutRow`'s last field is already a mark flag.

  A **gear glyph** beside the name in the slot list, and the **engineering text in a different
  colour** in the details pane. One thing to get right: the orange dot already means *a plan
  exists*, and the two marks are independent — in the reported screenshot Power Distributor is
  engineered with no plan while Power Plant is both — so a row can carry neither, either or both,
  and which is which has to be readable at a glance.

  **The colour would highlight a string that is currently wrong**, so these go together. The Fitted
  pane reads *grade 5 PowerDistributor PrioritySystems, Super Conduits*: the journal's raw symbol
  with the underscore taken out, where the catalogue's own name for that roll is **System Focused**.
  The Planned lines beside it read properly — *grade 5 Dirty Drive Tuning* — because they come from
  the blueprint table. Cause is `ChecklistNaming.Readable`, and this is thread A's acknowledged
  case: the method calls its own output *"ugly and true"*, and `CannotConfirm` says out loud that
  nothing d47 ships joins the two spellings. Making a wrong string more prominent is worse than
  leaving it grey.

  **The blocker is gone, and it was item 6's missing join all along.** Measured 2026-08-19.

  **The journal's `BlueprintName` and coriolis's blueprint `fdname` are the same namespace.**
  `PowerDistributor_PrioritySystems` — the exact string in the reported screenshot — is a key in
  `modifications/blueprints.json`, and so are `Engine_Dirty`, `Weapon_LongRange`,
  `ShieldGenerator_Kinetic` and every other journal symbol probed. Following it through the guid to
  EDEngineer gives the name the Commander should have read:

  | The journal writes | Shown today | Should read |
  |---|---|---|
  | `PowerDistributor_PrioritySystems` | *PowerDistributor PrioritySystems* | **System Focused** |
  | `Engine_Dirty` | — | **Dirty Drive Tuning** |

  **70 of 81 fdnames resolve to exactly one display name.** The other 11 are the shared blueprints,
  where the whole variation is *"Lightweight"* against *"Lightweight Mount"* — both correct, neither
  embarrassing, and the fitted module says which applies.

  So **`CannotConfirm` is about to be lying.** It tells the Commander outright that *"nothing I ship
  joins the two spellings"*, and `ChecklistNaming.Readable` calls its own output *"ugly and true"*.
  Both were accurate when written and both stop being so the moment item 6's re-key lands — that
  same re-key is what creates this join. **Fix them together**: the colour this item asks for would
  otherwise make a wrong string more prominent, which is the one thing worse than leaving it grey.

  The two marks stay independent as the note says — the orange dot means *a plan exists*, the gear
  glyph means *this is engineered*, and a row can carry neither, either or both.

- [x] **11. Every chooser should show what is fitted.** *Built.* *"Every choice for what goes into the slot
  should show what's currently in the slot, if anything."* The design already says so, twice, in
  `ChoiceRequest`'s own documentation: *"The chooser carries what it is choosing for in its header —
  the slot, its size, and what is fitted now. That is the one thing a dropdown cannot do"*, and the
  `Context` parameter is described as *"the slot's size, and what is fitted now"*.

  The loadout call site does neither. `Context(build, slot)` returns ship, slot and the promote
  sentence, and `AskModule` passes `plan?.Module` as `Current` with `CurrentWord = "planned now"` —
  so **the marker the record reserves for *fitted* was repurposed for *planned***, and fitted fell
  out entirely. A documented contract inverted at one call site, rather than a missing feature.

  Phase 26's rule says where each belongs: *fitted and planned are two blocks and never one merged
  line*. So **`Context` carries fitted** — "Military 1 (size 5), currently a 5D Guardian Hull
  Reinforcement", or "currently empty" — and **the row marker keeps "planned now"**, which is
  genuinely useful. No second marker mechanism, and the two facts stay separate the way the detail
  pane already keeps them. Applies to the blueprint and variant pages too, which share the header
  and the gap.

  **No decision needed — the contract already says this and one call site inverts it.**
  `ChoiceRequest`'s own documentation says `Context` is *"the slot's size, and what is fitted now"*
  and `Current` is *"which option is fitted now, by key"*. What `ShipsMode` passes is
  `"{ship} · {slot}. It does not reach your checklist until you promote the build."` — no size, no
  fitted — and `plan?.Module` as `Current` with `CurrentWord = "planned now"`. Both documented facts,
  neither delivered, and the marker reserved for *fitted* spent on *planned*.

  **This is item 15's root cause.** With the header saying *"currently empty"*, *"Anything — I only
  want the engineering"* would have read as obviously wrong on sight rather than merely odd. Build
  them together.

- [x] **12. "Put this build on my checklist" does not put it on the checklist.** *Built.* Reported as *"not
  showing my just-entered engineering in the checklist"*, and diagnosed from the installed build's
  own data rather than from reading code. `checklist.json` holds **one** item, the Commander's
  custom note. `checklist-proposals.json` holds **one proposal, `p-1`**, scope `ship/53`, source
  `engineeringPlan`, **carrying all 40 items** — *Grade 5 Long Range Weapon on LargeHardpoint1* and
  the rest. Nothing failed to save. Three discoverability defects stacked:

  1. **The button's name promises the wrong thing.** It says *checklist* and it makes a proposal.
     Either it says what it does, or it does what it says. Phase 25's *suggestions are a page rather
     than an interruption, and accepting stays the Commander's act* argues for renaming it and
     saying where the items went — but going straight onto the list is a defensible reading of the
     button's own words, and which one wins is a decision rather than a bug.
  2. **"That is already waiting for you" means *a proposal exists*** and reads as *it is already on
     your list*. It is the **second** press's message, so the first press succeeded silently and
     never said where forty items had gone.
  3. **The count is proposals presented as items.** The Suggestions button renders
     `PendingFor(fid).Count` — a count of *proposals*. One proposal carrying forty items renders
     **"Suggestions (1)"**, which beside a checklist holding one custom note reads as *one small
     thing waiting*. Counting items is the number that makes somebody press it; the
     counter-argument is that 1 is the number of decisions, since accepting is per-proposal and the
     page draws one card. Whichever wins, **the card should state its own size** rather than leaving
     it to be inferred from a truncated slot list.

  **And the announcement is in the wrong weight and the wrong place.** The button sits in the
  right-hand cluster with *Add a line*, the full width of a maximised window away from *Showing
  everything / Goals / Import-Export*, in the same weight as everything around it. Forty checklist
  items arriving is an event; the interface reports it by changing a hidden button into one showing
  a 1, at the far edge of the bar. Fixing only the count leaves that.

  **Two smaller things in the same message.** It says *"the ship 53 plan's"* — the raw `ShipID`,
  where it should say **Oxen** (thread B). And it ends *"...TinyHardpoint1, TinyH."* — truncated
  mid-word and closed with a full stop, so it reads as a finished sentence that is not one. It also
  names journal slot symbols where `slot.Describe()` already yields "Large Hardpoint 1", which the
  slot list directly beside it gets right.

  **Decided 2026-08-19: make it do what it says.** The build goes straight onto the checklist.

  > "If I wanted to dither about it more I wouldn't have pressed the button. That's not D47 making a
  > decision, that's me telling D47 about my decision."

  **The framing above was wrong and is corrected here.** Ask #14 is a bug report — the button says
  `Put this build on my checklist`, and the build was not on the checklist — and the write-up turned
  it into an even choice between renaming the button and honouring it. It was never even.

  **Phase 25's rule does not apply, and the reason matters.** *Suggestions are a page rather than an
  interruption, and accepting stays the Commander's act* governs what **d47 raises unbidden**. This
  is not unbidden: the Commander found the build and pressed a button labelled with exactly this
  outcome, and **that press is the act of accepting**. Routing it through a proposal makes d47 ask
  for a decision it has already been given. The proposal machinery keeps doing its real job for
  everything d47 raises on its own.

  Three of the defects listed above dissolve with it: there is no second press to say *"that is
  already waiting for you"*, this build never becomes a proposal to be miscounted as one item, and
  the announcement becomes a real event rather than a hidden button quietly turning into a `1`.

  **Defect 3 survives for other proposal sources** and is still worth fixing: `PendingFor(fid).Count`
  counts proposals where a Commander reads items. Whichever number wins, **the card states its own
  size** rather than leaving it inferred from a truncated slot list.

- [x] **13. Bind a core to any ship, from a dropdown.** *Built.* Against Phase 35 as shipped in v0.38.0. The
  row binds only the ship being flown, so setting a core means boarding that ship in-game first. It
  should list every ship in the fleet and bind the one chosen; journal state already carries the
  fleet, and the Ships page lists it.

  **This does not weaken the phase's own rule.** Phase 35 requires binding to be *at the Commander's
  command, never by watching*, and to be a protected panel row. Nothing in it requires the Commander
  to be **flying** the ship — that constraint arrived from the row being scoped to *this* ship. A
  dropdown removes it while leaving the protection untouched: still the panel, still deliberate,
  still one act.

  **Decided 2026-08-19: two dropdowns — ship, then core — and the ship selection becomes a setting.**

  Ship as the **selector** (which ship am I editing), core as the **value**. That also absorbs the
  separate *Forget this ship's core* button: a **"nobody — whoever is aboard stays aboard"** entry in
  the core dropdown is the unbind, which is one control rather than two and reads as what it does.

  **The wrinkle is in the mechanism, not the design.** `SettingBinding`'s `Read` and `Write` both go
  through `D47Settings`, but a ship-core binding lives in `ship-cores.json`, and the ship dropdown's
  value is not a setting at all. Three ways out were weighed: persist the ship selection as a real
  setting; widen `SettingBinding` to read and write something that is not `D47Settings`; or give
  ship-cores its own panel section outside the declared-row model.

  **Persisting the selection wins.** Smallest change, no contract to widen, and the row stays inside
  the mechanism that gives it keyword-router reachability and its protection. "Which ship am I
  editing" surviving a restart is a convenience rather than a wart. The core dropdown's `Write`
  binds and returns settings unchanged, which is mildly impure and contained to one row.

  **The cost, stated because the rule says so**: the settings file is append-only, so this is one new
  property that can never be removed. Cheap against changing an architecture contract for one row.

  Phase 35's protection is untouched — still the panel, still deliberate, still one act — and nothing
  in the phase ever required the Commander to be *flying* the ship. That constraint came from the row
  being scoped to *this* ship, and the dropdown removes it.

- [x] **14. `type9_military` shown where a hull name belongs.** *Built.* *"Oxen, a type9_military is not
  bound to a core, so whoever is aboard stays aboard."* `EliteSpecifications` resolves that symbol —
  `type9_military` is **Type-10 Defender**, by Lakon — and the fleet page one tab over already
  prints "Oxen (Type-10 Defender)". Thread B, and the cheap half of it: the name was in hand and the
  id was printed anyway.

- [x] **15. "Anything — I only want the engineering" is offered on an empty slot.** *Built.* Reported
  2026-08-19 against v0.38.0, on the module chooser: *"is that supposed to be on the current module?
  If so, fine, but say so. However, I went to an optional slot with nothing in it and it said the
  same thing. You can't engineer an empty slot."*

  `AskModule` adds that row **unconditionally** — it is the first `ChoiceOption` on every module
  page, whatever the slot holds — and taking it leaves `Module` null. On a fitted slot that is a
  real want and merely unsaid. On an empty slot it is a plan to engineer nothing.

  Two fixes for the two cases, and the second is the defect:

  | Slot | Today | Wanted |
  |---|---|---|
  | Something fitted | "Anything — I only want the engineering" | Name it: *"Keep the 7A Thrusters — I only want the engineering"* |
  | Empty | The same row | **Not offered at all** |

  **This is item 11 arriving from the other side.** Had the header carried what is fitted — which
  `ChoiceRequest`'s own documentation says it should, and which item 11 exists to restore — the row
  would have read as obviously wrong on an empty slot rather than merely odd. They share a cause and
  are worth building together, but they are separate defects with separate acceptance tests, so this
  is not folded into 11.

  Not to be confused with item 7, which is about a question with *one* answer. This is about the
  decline being wrong, and it fires on pages that have plenty of answers.
