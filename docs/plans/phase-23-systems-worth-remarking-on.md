# Phase 23 — Systems worth remarking on

The plan of record for list.md Phase 23, written 2026-08-17 and finished the same day.

`list.md` reads top to bottom as a description of the product. This is the order the work happened
in, the four calls that were settled before any of it, and the reasoning the order cannot carry on
its own.

---

## The four calls, settled before the first line of code

`list.md` left four questions open in so many words. Three were answered as the checklist leaned,
and one was not.

1. **Who compiles the shipped table.** The checklist says "short and written by the maintainer",
   which reads as *do not import somebody's point-of-interest list*. It was read too narrowly at
   first — as *write it from memory*. The call was to **go and mine the sources, then compile**:
   several sources, each the authority on one thing, and the selection the maintainer's own. That
   is a different act from copying EDSM's Galactic Mapping names, and it is the act that makes the
   twenty rows defensible.
2. **Per Commander or per installation.** Per installation, with the Frontier id recorded on each
   entry. `SamplingStore` keys on the FID because sampling progress belongs to a *character*; a
   note about a system is true whichever character is flying, and the person writing it is the same
   person either way. Recording who was aboard costs one field and is worth having; keying on it
   would mean lore added as one character is silent as another.
3. **Whether a later corroboration promotes an entry.** **Never.** A tier is set when the entry
   arrives and is a record of how it arrived rather than a verdict on it. There is no code that
   performs a promotion, which is stronger than a rule saying not to.
4. **Whether a runtime search cache earns its keep.** **No.** The remark already fires at most once
   per system per day, so a cache keyed on `SystemAddress` would almost never be hit — and a stored
   search result sits close to the edge of the standing rule that a result stays a sentence.

## The number the checklist asked for, measured before it was picked

list.md asks that the 24-hour window be "measurable rather than a guess", and names
`spike/CorpusReplay` as the thing that can produce it. Across the 913-journal corpus:

| | |
|---|---|
| `FSDJump` events | 7,966 |
| Re-entering a system visited within 5 minutes | 7 (0.09%) |
| …within 1 hour | 1,409 (17.7%) |
| …within 6 hours | 2,011 (25.2%) |
| **…within 24 hours** | **2,397 (30.1%)** |
| …within 7 days | 2,735 (34.3%) |

So without the rule, **nearly a third of arrivals would be something already heard**. Widening it
to a week buys only 4.2 points more, because **88% of all repeat visits happen inside the first
day** — which is what makes 24 hours the right shoulder of the curve rather than a round number.

Measured again against the twenty rows actually shipped: 228 arrivals into seeded systems over
thirteen months, of which **99 would be spoken and 129 suppressed**. About seven remarks a month,
concentrated on HIP 12099 (31), Lave (20) and Shinrarta Dezhra (15).

## What the checklist assumed that turned out to be already done

list.md says the persistence question is "the one `docs/plans/change-requests.md` already asks
about `PersonaHost`'s in-memory `_introduced` set, and both are better answered once than twice".
**That request shipped in 0.21.0** — `IIntroductionMemory` exists and `PersonaHost` already
persists. So there was one question here rather than two, and the pattern to follow was already in
the tree.

## Where the twenty rows came from

`tools/gen-lore.py` carries the full account. In short: Frontier's own GalNet and in-game fiction
for the human history; the fandom wiki and Canonn for the discovery record; spansh and EDSM for
identity only — a name in, an id64 out, contributing no row.

**Two rows were caught by the generator's own assertion rather than shipped.** `Ceeckia ZQ-L c24-0`
resolves to nothing because Frontier renamed it Beagle Point, and `Arumclaw` resolves to nothing
because it does not exist — a search result claimed Commander Salomé was killed there. The rule
that both resolvers must agree, and that a miss stops the run, is what turned two would-be bad rows
into two lines of output. Eleven of the twenty were also matched against addresses Frontier
themselves wrote into an `FSDJump` in the local corpus.

## The three things the trust boundary decided

**Adding a note is not protected, and that is a departure.** The checklist and the callout rows are
both model-unreachable for reasons that do not apply here: a note presses no key and silences no
warning. What it does do is write persistent state that d47 later speaks aloud unprompted — so the
answer is a label rather than a lock. `LoreArrival.Model` is recorded on anything the model writes,
**whatever the turn looked like from inside the handler**, because a turn steered by a hostile
in-game message is indistinguishable from one the Commander asked for at that depth.

**The checklist names three arrival routes and the code has two.** The missing one is the keyword
router, and it is missing because the router's grammar is closed by design: a declared phrase
carries declared arguments and extracts nothing from what was said. A lore entry is a sentence, so
no phrase could carry one. An enum member reachable by nothing would be a claim the code does not
support.

**The panel is the only route to the Commander's own tier**, and it gets there through an
`Info` row — which `SettingsService.Apply` refuses outright, whoever is asking. That was not a new
mechanism; it is what `SettingRow.Press` already documents about its own trust position.

## Two things found on the way past

**The privacy disclosure had become wrong.** `EgressDisclosure`'s web-search entry said a search
happens "when a question needs current information". After this phase one can also happen on a
jump, with nobody having asked — which is exactly the kind of thing that row exists to state.
Amended, along with the conversation documentation page.

**`spike/CorpusReplay` could not run while Elite was running.** Its second pass opened each journal
without `FileShare.ReadWrite`, so the newest file — the one Elite holds open — threw after the soak
had already reported a clean run. `JournalReader` has always opened them shared; this pass did not.
Fixed, because the soak is a gate and a gate that only runs when the game is closed is one that
gets skipped.

## What shipped

- `Knowledge/Lore.tsv` and `LoreDirectory` — twenty rows, keyed on system address.
- `Lore/` — `LoreEntry` and its tiers, `LoreStore` (the Commander's notes, hand-editable, change
  detected by comparing content), `LoreVisits` (the absolute stamps), `LoreBook`, `LoreLookup`.
- `LoreCallout` — the arrival remark, `FSDJump` and `CarrierJump`, once per system per 24 hours.
- `LoreCapability` — `get_system_lore`, `remember_about_system`, the three-state remark row and the
  notes disclosure.
- `LoreWindow` — where a note is written, and where every entry says which tier it is.
- `FlavourTurn` gained a `webSearch` argument; `JournalLocation` gained `SystemAddress`.
- 41 tests, of which the three that carry the most weight were verified by reintroducing the fault
  and watching them fail: the 24-hour rule, its survival across a restart, and the model's
  inability to file its own note as corroborated.
