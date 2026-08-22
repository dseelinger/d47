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

Nothing open.

---

## Shipped

### 25. The Adventures tab: where it sits, what it shows, and that it is thinking — shipped 0.52.3

Asked for 2026-08-22, in five parts; built the same day, in the next release.

> * Place the Adventures tab after Engineers.
> * Current Adventure on the adventures tab should show "Adventure Only" Triggered Voice text and
>   any "Flavour" text where it responds to questions or comments about the adventure from the
>   commander. Trigger text should be in a highlight color.
> * Next Trigger should show what it expects the commander to do next to advance the story.
> * I've noticed that it can take a while after "triggering" a trigger for me to hear anything.
>   I'm assuming that it's "cogitating". If so, can I get an indication on the Adventure tab that
>   it's thinking? Something animated so I don't think that I haven't done what I'm supposed to do?
>   If it can be accompanied by a set of canned, "That's it!" "You've done it." "Well done." etc.
>   Maybe 10 of them. Short so that you don't have to wait long for the TTS.
> * And the Adventure tab (and the mini-version too) should appear in VR.

**The wait is real and it is deliberate**, which is why the fourth ask has two halves.
`AdventureCallout.Settle` holds a reached beat for twenty seconds so the line is not read out over
the jump that reached it, and `VaryAsync` then spends up to three more having the model say it in
the core's voice. Neither is wrong; what was missing is that the Commander could not tell the wait
from having failed to do the thing. So the confirmation is split off from the telling:
`AdventureAcks` is ten stock lines of four words or fewer, said on the tick the beat fires with no
settle and **no model behind it** — which is why its announcements carry
`AdventureCallout.AckPrefix` rather than `KeyPrefix`, since `FlavourBriefs` routes on the prefix and
would otherwise send the acknowledgement through the very round trip it exists to arrive ahead of.
`AdventureThinking` is the other half, driven off the same 10 Hz tick the clocks are and honest
about whether the frame moved, because the headset only re-rasterises a surface something marked
dirty.

**"Adventure only" needed somewhere to keep what was said.** The reading level was drawing the
*authored* lines, and what a Commander hears is the model's wording — so a story flown over four
evenings had no record of itself anywhere but a session-long transcript carrying everything else
d47 says. `Adventure.Told` is that record, persisted with the story and capped at
`AdventureLimits.MaxTold`. Two decisions the Commander made:

- **The flavour heuristic** is name-, beat- or place-mention over the exchange
  (`AdventureMention`), whole words only and nothing shorter than four letters. Chosen over
  "everything while a story is live" (which stops the page being adventure-only) and over asking
  the model to tag each turn (a round trip in front of every answer, which is the cost the rest of
  this item removes).
- **The feed persists** rather than living for a session.

**"Step X of Y" reverses a rule Phase 47 wrote into the code.** `AdventureStanding` said outright
that *beat 3 of 7 is checklist language and belongs to the Technical transcript*, on the
story-not-a-checklist framing that governs the whole phase. The Commander asked for the count on
both surfaces and it is built; that comment is rewritten rather than deleted, on the same terms the
checklist's withdrawal and return were. What did not change: the beats are still titled dramatic
functions rather than numbered stops, and nothing generated says a number — `Step()` is the one
place a count is spelled.

**Mini follows the tab now.** It was "the transcript's tail and the provenance line" whatever the
panel was on. The instruction is that it show a succinct version of whichever VR tab is selected,
*"but we can keep it to transcript and Adventure for now"* — so `AdventureMini` draws the five
things asked for (the short description, the trigger just fulfilled, the trigger expected, the last
thing the AI said, and the step) and every other tab behaves exactly as it did. Mini still has no
tab strip; which tab it is reading is chosen on the big panel, which is what makes one surface in
two sizes rather than two surfaces with their own state.

**The tab itself reaches the headset by one call**, exactly as Phase 47's own comment predicted it
would — `VrPanelSurface` now passes the window's `AdventureSurface` to `PanelView.EnableAdventures`.
The desktop-only reasoning was that the editor and the ask form want a keyboard; that weighed the
wrong half, since a Commander in a headset is precisely the one who has just arrived somewhere, and
the prompts have taken a spoken value since Phase 25.

A headless capture caught the one defect in this that no test had: the drilled-in reading level
subscribed to the store's change event only, and a beat firing writes nothing to disk — so the card
behind it redrew and the level the Commander was looking at did not.

### 24. No Help glyph on a VR surface — shipped 0.52.2

Asked for 2026-08-22; built the same day on `fixes-3`, in the next release. The panel's help mark
opened the documentation site in a browser — on the desktop, which a Commander in the headset
cannot see. The model's `HelpRequested` event was the wrong seam: the headset copy shares the model,
so its press reached the desktop window's handler. Help is now an affordance of the **surface**,
handed over by the host like search and the turn-figures dialog already were — `PanelView.EnableHelp`
— and `VrPanelSurface` never calls it, so the headset copy has no button rather than a hidden one.
The `OpenHelp`/`HelpRequested` pair on the model went with it; its comment said the view "asks
rather than acts" so as not to know what a desktop is, and that reasoning is kept, moved one seam
over to where the two surfaces actually diverge.

### 23. Remind me to buy limpets — shipped 0.47.0

Asked for 2026-08-21, built as list.md Phase 41. Two of the Commander's own corrections
reshaped it mid-build: limpets are bought through **Advanced Maintenance** rather than the
commodity market, which killed a design built on reading `Market.json`; and the carrier gap was
ruled to be accepted rather than special-cased. The rulings and the measurements are in
`list.md` Phase 41 and in `LimpetCallout`'s own summary.

### 22. Say when a system might be holding High Grade Emissions — shipped 0.46.0

Asked for 2026-08-21: *"Notifies me when I am in a system that has a chance of having High Grade
Emissions available, and what the material(s) is/are. Skips if I'm already full of that material …
Should support multiple material types when a system matches multiple conditions … not just Core
Dynamics Composites plus the related one that can be found in the same HGE, but when completely
different ones are there."* With an on/off row in settings.

**Built as list.md Phase 40.** Everything except the table was in hand from the start — the
conditions are all in one `FSDJump`, and "skip what I am full of" is exact rather than estimated —
so this waited on a source rather than on code, because hand-writing game data is the one thing
`CLAUDE.md` forbids outright.

#### Where the table came from

- **[Elite Dangerous Wiki — High Grade Emissions](https://elite-dangerous.fandom.com/wiki/High_Grade_Emissions)**
  is the best of them, and not only because it has the table. It is the only source found that
  states the **mechanic**: a signal is assigned to *one faction*, its contents come from that
  faction, and where a faction meets several conditions a hidden rank order picks between them.
  That is what makes the rest predictable instead of a list of folklore.
- **[Frontier Forums — Unidentified Signal Sources: A Complete Guide](https://forums.frontier.co.uk/threads/unidentified-signal-sources-a-complete-guide.377716/)**
  (2017, edited 2018), on Frontier's own site, is the primary community research the rest descends
  from. Found through [EDEngineer issue #196](https://github.com/msarilar/EDEngineer/issues/196),
  which cites it as *the* reference. It corroborates every group.
- **[edgalaxy.net/hge](https://edgalaxy.net/hge)** is not a table but a live one — HGE detections
  reported over EDDN since the last tick. Its six filter groups match the six below exactly, which
  is behavioural corroboration rather than a second copy of the same prose.
- **And a fourth, already in the repo.** `Materials.tsv` carries these conditions in its own origins
  column — "Signal source (High grade emissions, Boom)" and the rest — for all ten materials,
  generated by `tools/gen-materials.py`. So `EmissionRules` is asserted against a shipped, generated
  table both ways: no rule without a row, no row without a rule. A regenerated table that disagrees
  fails a test rather than drifting away from a callout nobody would think to re-read.

#### The groups, in the wiki's stated rank order

| Condition | Materials |
|---|---|
| Federal faction | Core Dynamics Composites, Proprietary Composites |
| Imperial faction | Imperial Shielding |
| Civil Unrest | Improvised Components |
| War or Civil War | Military Grade Alloys, Military Supercapacitors |
| Boom | Proto Heat Radiators, Proto Light Alloys, Proto Radiolic Alloys |
| Outbreak | Pharmaceutical Isolators |

#### Four places the two prose sources disagree, and how each was settled

Recorded because a table that hides its choices is one nobody can check later. All four ruled on by
the Commander, 2026-08-21.

1. **Proprietary Composites** — wiki lists it beside Core Dynamics for Federal space, the 2017 guide
   does not. **Ruled: include it** (the wiki).
2. **Expansion** — wiki says *Boom or Expansion* for the Proto materials, the guide says Boom only,
   and a third account adds *Investment*. **Ruled: Boom only.** This one is not cosmetic: `Expansion`
   is the second commonest state in the corpus after `None`, so the other reading would have made
   this the chattiest callout d47 has.
3. **Population** — wiki gates Outbreak on a million, the guide gates nothing. **Ruled: the floor
   applies to every group.**
4. **Whether superpower overrides state — the load-bearing one.** The wiki says a Federation or
   Imperial faction *never* yields anything but composites or shielding. The 2017 guide says the
   opposite in as many words: *"If you need Imperial Shielding and Pharmaceutical Isolators, look
   for an Imperial system in Outbreak."* **Ruled: superpower wins** — the wiki is newer, and its
   reading is the one consistent with the rank-order mechanic the same page states. Pinned by three
   tests, and the fault was reintroduced to watch them go red.

#### What the journal turned out to give

`FSDJump` carries a `Factions` array, each entry with `Allegiance`, `FactionState`, `Government`,
`Influence` and — the useful one — **`ActiveStates`, a list**. So the evaluation is **per faction**,
which is what the wiki's mechanic asks for and what makes the Commander's *"completely different
ones"* case fall out rather than needing a rule of its own. **84 of 400** recent corpus jumps are
into a system holding a Federal faction *and* an Independent or Alliance one, so a system offering
two unrelated groups at once is ordinary. The state spellings are the journal's own tokens, so no
name-matching guesswork was needed anywhere.

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
