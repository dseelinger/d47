# Change requests

Wanted changes that are not defects. **Bugs are not here** — those are in
[bugs.md](../../bugs.md). Everything here behaves as built; the request is that it be built
differently.

An entry leaves this file when it ships, and the line it gets in [CHANGELOG.md](../../CHANGELOG.md)
under the release that carried it is its permanent record.

An entry states what is wanted and where the code is. Where one carries an **open question** that
changes the work materially, it says so — those want an answer before the code does, because the
answer is usually the difference between two different pieces of work rather than a flag.

Where an item contradicts a comment in the source, that is called out. Those comments are the
reasoning being overturned, and leaving one standing beside code that no longer obeys it turns
the file into a liar.

**Numbers are not reused.** Items cite each other by number, and reusing one would leave an old
citation resolving to a live entry about something else, reported by nothing — the trap the
phase-renumbering rule in [CLAUDE.md](../../CLAUDE.md) exists to name. The next batch starts
at 20.

---

## Open

### 22. Say when a system might be holding High Grade Emissions

Asked for 2026-08-21: *"Notifies me when I am in a system that has a chance of having High Grade
Emissions available, and what the material(s) is/are. Skips if I'm already full of that material.
Should support all Manufactured Materials that can [be] harvested from HGE. Should support multiple
material types when a system matches multiple conditions … and not just Core Dynamics Composites
plus the related one that can be found in the same HGE, but when completely different ones are
there, such as Pharmaceutical [Isolators] and something else, if conditions are right."* With an
on/off row in settings, like every other callout.

**Everything except the table is already in hand, and that is worth stating precisely.**

- *The conditions are in the journal.* One `FSDJump` carries `SystemAllegiance`, `SystemEconomy`
  **and** `SystemSecondEconomy`, `SystemGovernment`, `SystemSecurity`, `Population`, and every
  faction with its `FactionState` — which is the whole of what a Commander reads off the system
  map when deciding whether an HGE here is worth waiting for. The second economy and the faction
  list are what make "multiple materials at once" expressible rather than a special case.
- *"Skip if I'm already full" is exact.* `MaterialGrades.CapacityOfGrade` holds the per-grade cap
  and the Commander's holdings are live in game state, so this is a comparison and not an
  estimate.
- *The callout shape exists.* A journal-triggered callout with a settings row is the most
  well-trodden path in the app.

**What is missing is the mapping, and it must not be invented.** Which allegiance, economy and
state combination yields which grade 5 manufactured material is community reverse-engineering.
Frontier publishes none of it, no shipped table carries it, and `CLAUDE.md` is explicit that game
data is derived by a generator with its provenance recorded and never hand-written — a rule that
exists because it has already been got wrong in both directions.

**The corpus cannot settle it either, and that was checked rather than assumed.** Across the
920 journals there are **19** `$USS_HighGradeEmissions;` signals and no `USSType` for one at all,
so there is nothing to join a signal to the materials that came out of it. The 14,000-odd grade 5
manufactured `MaterialCollected` events are overwhelmingly from trade and mission rewards and
cannot be attributed to a source.

**Open question, and it changes the work rather than a flag.** Three answers, and each is a
different piece of work:

1. **The Commander writes the table.** Their own game knowledge is not a licensing question, and a
   table stated by the person asking for the feature has a provenance — them — that can be
   recorded honestly beside it. Fastest, and the most likely to be right.
2. **A source is named that carries it.** Then it is a generator like every other table, and the
   licence gate and the attribution rules apply as usual.
3. **Derive it from play.** d47 records the system's conditions when an HGE is scooped and builds
   the mapping from what the Commander actually finds. Honest, needs no source at all, and is
   worth nothing for weeks.

**Not started.** A callout that names the wrong material is worse than no callout, so this waits
on the answer rather than shipping a guess.

---

## Shipped

### 20. Ordering the checklist, by voice and with both ends — shipped 0.45.0

Asked for 2026-08-21 against a transcript in which d47 said *"Ordering I cannot set"* and *"no
selection, no move"*. Three parts: the selected line has to be reorderable by voice, a line just
added has to *be* the selected one, and **Move to Top** and **Move to Bottom** have to exist beside
the two steps that already did — the end glyphs drawn as the step glyphs with a bar on them.

### 21. The carrier's crew speak to each other when you drop in — shipped 0.45.0

Asked for 2026-08-21, with the exchange written out: the tower tells the captain the Commander is
inbound, and the captain answers the tower and then the Commander. Model-written where there is a
model, with the authored lines as the floor — which is what Phase 11 already built for the
carrier's other lines, so this reuses it rather than adding a second path.


**The ten raised hand-testing 0.15.0 on 2026-08-16** all shipped together in 0.21.0. Their record
is that section of the changelog, which keeps them in the order they were built.

Two of them left something worth knowing about:

- **Item 9's headset defaults have never been seen in a headset.** The arithmetic is tested and
  the first-show placement is written down, but whether knee height *reads* as the right place —
  and whether it is wrong for a seated Commander — is a question only somebody wearing one can
  answer.
- **Item 6 turned up a defect on the way past**, in the log-level rows rather than in anything it
  was asked to change: three of them named namespaces that do not exist and so controlled
  nothing. Fixed in 0.21.1, and `TechnicalLogBridge` now reads the one list rather than keeping
  a copy.

**Items 11 to 14, raised on 2026-08-17**, shipped in 0.22.0: the NPC preamble, comms on the
Technical page, the radio treatment for everybody who is not aboard, and an NPC's voice being
theirs for as long as the Commander is in the system. Their record is that section of the
changelog.

Two of them also turned up something on the way past. The empty-sender case was being read aloud
as " says: …" — 8821 events in the corpus have an empty `From` rather than a missing one. And the
crew's voice assignments shared the per-system table with the NPCs, so a hired gunner changed
voice on every hyperspace jump; they are aboard, so they now last the session.

**The five raised hand-testing 0.21.x on 2026-08-17** — items 15 to 19, all of them about the
settings surface — shipped together in 0.23.0. Their record is that section of the changelog: the
search matching a section's own name, **Verify Key** shut until a key is typed, the ElevenLabs key
row moved up beside the provider that needs it, and the voice picker's audition becoming a play
glyph on each row now that a click highlights rather than chooses.

Item 18's open question was answered **both ways**: the price is a line above the list *and* the
pointer text on every glyph. A tooltip alone would have made a cost you have to hover to discover,
which is what Phase 11 put the number on the button to prevent.

Two of them turned something up on the way past. Item 18 was only possible because of 19, which is
why they shipped together — and building the picker's rows per keystroke, as the first cut did,
cost it the highlight on the value the Commander arrived with: a list holds its selection by
object, and a text box raises `TextChanged` as its template applies, so the filter re-ran and
handed the list a different row for the same voice before the window had finished opening.
