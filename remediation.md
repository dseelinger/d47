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

- [ ] **1. Copy a plan to another slot by dragging.** Carried over from remediation 14 item 5.
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

- [ ] **2b. Nothing in the table says what a module does.** The reported ask: *"the more expensive
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

- [ ] **4. Do not ask which grade.** *"999 times out of 1000 it will be 5."* Measured: of **160**
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

- [x] **5. "Point Defence is not engineered" should be "is not currently engineered".** *Built.*
  `ChecklistEvaluator` line 217. Right for a reason beyond taste: that verdict is a **reading taken
  at a moment**, not a property of the module — list.md Phase 26 has a plan carrying the journal's
  verdict with its date, standing as of when it was taken, and the state it ships with is
  `ChecklistState.Open`, meaning still to do. *Is not engineered* reads as a fact about Point
  Defence; *is not currently engineered* reads as a fact about right now, which is the only thing
  d47 knows. It is in Core, so it lands on the spoken path too, and reads well aloud.

  **One sibling to decide**: `EngineeringCapability` appends a compact `not engineered` to a report
  line. Match it, or leave it terse because a dash-separated report already reads as a snapshot.

- [ ] **6. A chooser offers blueprints that cannot exist.** Two reports, needing **opposite**
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

- [ ] **8. A blueprint row should say what the blueprint does.** *"Since you have all this space,
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

- [ ] **9. A module row should say what is special about that module.** *"What's special about a
  Guardian Distributor? It should say."* Two answers, and they are different.

  **The numbers are not there**, and that is item 2b — distributor capacity and recharge are the
  same generator pass as weapon damage.

  **The provenance is there and will not join**, and it is arguably the better sentence anyway: d47
  already holds 51 `tech-broker` rows including the Guardian family, and *you unlock this at a tech
  broker* explains why you do not already have one, which no capacity figure does. Except the module
  table says **`Guardian Hybrid Power Distributor`** and the tech-broker table says
  **`Guardian Power Distributor`**. Thread A again.

- [ ] **10. An engineered module should look engineered.** Two asks, and the mechanisms exist:
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

- [ ] **11. Every chooser should show what is fitted.** *"Every choice for what goes into the slot
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

- [ ] **12. "Put this build on my checklist" does not put it on the checklist.** Reported as *"not
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

- [ ] **13. Bind a core to any ship, from a dropdown.** Against Phase 35 as shipped in v0.38.0. The
  row binds only the ship being flown, so setting a core means boarding that ship in-game first. It
  should list every ship in the fleet and bind the one chosen; journal state already carries the
  fleet, and the Ships page lists it.

  **This does not weaken the phase's own rule.** Phase 35 requires binding to be *at the Commander's
  command, never by watching*, and to be a protected panel row. Nothing in it requires the Commander
  to be **flying** the ship — that constraint arrived from the row being scoped to *this* ship. A
  dropdown removes it while leaving the protection untouched: still the panel, still deliberate,
  still one act.

- [x] **14. `type9_military` shown where a hull name belongs.** *Built.* *"Oxen, a type9_military is not
  bound to a core, so whoever is aboard stays aboard."* `EliteSpecifications` resolves that symbol —
  `type9_military` is **Type-10 Defender**, by Lakon — and the fleet page one tab over already
  prints "Oxen (Type-10 Defender)". Thread B, and the cheap half of it: the name was in hand and the
  id was printed anyway.
