# Change requests

Wanted changes that are not defects. **Bugs are not here** — those are
[GitHub Issues](https://github.com/dseelinger/d47/issues). Everything here behaves as built; the
request is that it be built differently.

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
phase-renumbering rule in [CLAUDE.md](../../CLAUDE.md) exists to name. Everything through 38 has
shipped and been pruned, so **the next number is 45** — the count is not the length of this file.

**41 was declined and its number is retired with it.** It asked for a picker among ElevenLabs'
synthesis models; the answer, 2026-08-25, was to move the pin to the best one and offer no choice
at all — see `ElevenLabsTtsProvider.DefaultModel` for why, and `CHANGELOG.md` for the release that
carried it. Declining an entry retires its number exactly as shipping one does: it was cited while
it was open, and a later entry reusing 41 would leave those citations resolving to something the
number was never about.

**And this very paragraph arrived as a merge conflict**, because two branches edited the line that
records the next number — which is the failure the line exists to prevent, arriving by the road it
warns about. It conflicted rather than resolving quietly, which is the outcome to want.

**So a number cited in the source is often not here, and that is normal rather than a dangling
reference.** Comments across the codebase cite these by number — `change-requests.md 18` seven
times, and it was pruned well before today. The entry is in [CHANGELOG.md](../../CHANGELOG.md) under
the release that carried it, and in this file's history; the number is the identifier, not an index
into what happens to be open today.

---

## Open

## 44 — Two identical modules give two identical checklist lines

Reported 2026-08-26, after an hour spent believing d47 had missed an experimental effect it had
not missed.

The Kestrel *Tulimiekka* carries **two** 2D Hull Reinforcement Packages, in `Slot04_Size2` and
`Slot05_Size2`. Both have a plan item for Deep Plating. One is applied and one is not, and the
checklist draws them as:

```
Deep Plating on 2D Hull Reinforcement Package
  Tulimiekka (Kestrel Mk II) · 2D Hull Reinforcement Package has no experimental effect on it.
```

— twice, identically, with nothing on either line to say which module it is about. One is done and
one is open, and the only way to tell them apart is to go and look.

**Nothing here is wrong.** The intents are slot-addressed, the verdicts are correct, and the stored
text on the item even says `Deep Plating on Slot04_Size2`. What is drawn is the module *type*,
resolved from the slot at render time by `ChecklistWording.InSlot`.

### It refines a stated ruling rather than reversing one

That method's own comment records why it names the type:

> **The module type, never the mounting point.** Reported as "Utility Mount 8 and Compartment 4
> don't tell me the module type", and d47 knew all along — the ship plan stores the module beside
> the blueprint and this method had never been shown it.

That request was right and the change was an improvement: `Slot04_Size2` tells a Commander nothing
about what belongs there. The cost only appears when a ship carries two of the same module, which
this one is the first to do — and then the line loses the one fact that distinguishes it.

**So the ask is not to go back to slot ids.** It is that where two lines on the same ship would
otherwise read identically, the mounting point comes back as a qualifier — the type first, because
that is what the earlier ruling was about, and the slot after it only where it is doing work.

### The open question — this wants an answer before the code

**Is "would two lines read the same" the condition, or "does the ship carry two of this module"?**
The first is exact and is what a Commander actually experiences, but it means a line's wording
depends on what else is in the list — which changes as items are filtered, ordered and completed,
so the same item could read two ways on two screens. The second is stable and slightly broader: a
ship with two 2D HRPs qualifies both lines whether or not both are on screen.

Leaning to the second, on the grounds that a line should read the same wherever it appears.

### Where the code is

`ChecklistWording.InSlot` resolves the subject to `ChecklistEvaluator.Describe(module)` and returns
it. The slot's own readable form is already available on the line below it —
`EliteSpecifications.Slot(hull, subject)?.Describe()` — and is what the method already falls back
to when nothing is fitted, so both halves exist and nothing new has to be derived.

## 39 — The engineer filter should cover the fleet, not the ship you are in

Asked for 2026-08-23, in the Commander's own words:

> All checklist items fulfillable by the Engineer should appear for that filter, not just for the
> ship I'm in.
>
> If that's undoable, then at least notice when I switch ships and re-filter for the new ship — but
> I'd rather the previous bullet be implemented instead.

**The fallback is named as the lesser option and should be read that way.** Re-filtering on a ship
switch is a consolation prize for a filter that cannot see past the cockpit; it is not the ask, and
building it instead would close the entry without answering it.

`ChecklistService.FilterAxes` offers the row — *"What {engineer} can do here"* — and
`EngineersHere.For(live, State)` decides what is under it, per engineer, in `EngineerAtHand`.

### The ground moved after this was reported, and that is the first thing to check

Two changes landed between the ask and this entry, both of which touch exactly this code, so
**reproduce it before designing anything**:

- **Other ships' modules became readable.** `EngineerAtHand.LoadoutFor` already answers with the
  remembered loadout for a ship the Commander is not in — *"the live one for the ship being flown …
  the remembered one for any other ship"* — which is the per-ship loadout memory shipped in v0.41.1.
  Whatever was scoping the filter to one cockpit in August, it was not a lack of data about the
  others.
- **A fitted gate was added on 2026-08-25** (GitHub issue 41), because every line offered under
  *"What Selene Jean can do here"* was about a slot with nothing in it. `IsFitted` passes an item
  whose ship has **never been seen** — `LoadoutFor(…) is null` returns true — so it is permissive
  rather than restrictive about the rest of the fleet.

So the entry may already be half-answered, or the restriction may live somewhere neither of those
touched. **Open question, and it wants measuring rather than arguing**: with a plan on a stored ship
and its engineer in the current system, does that line appear under the filter today?

### What it must not become

**Not a filter that offers work the Commander cannot do.** The three bands `EngineerAtHand` keeps
apart — ready, out of rank, and partial — exist because folding them together was reported as noise,
and widening the filter across the fleet must not quietly widen it across those bands too. A line
about a ship in another system is still work; it is *"fly there first"* work, and if the filter grows
to include it, the row has to say which ship it is about or it becomes a list of errands with no
addresses.

---

## 40 — Voices that suit the names they are speaking

Asked for 2026-08-23, queued behind Phase 54 for discussion: **multilingual and ethnic voices that
match NPC names where appropriate.** An engineer called Hera Tani, Marco Qwent or Etienne Dorn read
in the same flat English as everyone else is a small thing that adds up over a hundred hours.

`VoicePairing` is where a voice is chosen, and it already asks a model to do the choosing, so the
question is what it is allowed to choose *from* and what it is allowed to say about language.

### This contradicts two stated rulings, and both are in the source

The file's own rule is that a request overturning a comment says so, because leaving the comment
standing beside code that no longer obeys it turns the file into a liar. Two comments are in the way,
and **neither is wrong** — they are answers to a question this request asks differently.

**Turbo 2.5 rather than Multilingual 2, and the reason is language.** `ElevenLabsTtsProvider`
records it: Multilingual 2 *"infers the language of every line from the line, which is the behaviour
it is built for and which produced a material milestone read half in German."* The 2.5 generation
accepts `language_code` and holds it; Multilingual 2 rejects the parameter outright, so there was no
version of pinning English that kept that model. It is also half the price. **A request for voices
that sound like their names is not a request for lines that switch language mid-sentence**, and the
difference between those two is the whole of this entry's design problem.

**`Language = "en"`, fixed, and the argument behind it is about untrusted input.** The comment: *"the
one thing an in-game message can do to this path is arrive in another language — which is not a
reason to let it choose the voice's."* Anything here that lets the spoken language follow the text
has to answer that, because in-game comms are untrusted input by invariant. An accent chosen from a
**name d47 already knows** — an engineer from the shipped directory — is a different proposition from
a language inferred from a line, and that distinction is probably where the answer lives.

### Open questions — these want answers before the code

1. **Accent or language?** A voice with a French accent reading English is not the same feature as a
   line spoken in French, and only the first is obviously wanted. If it is accent only, the model
   pin may not need to move at all — it becomes a casting question rather than a synthesis one.
2. **Which names?** The engineers are a shipped table with known origins. Station and system names
   are not, and a Commander's own invented ship names certainly are not. Naming the source list is
   most of the scope.
3. **What happens on the other providers?** Edge Neural is the free path and most Commanders' path,
   and its voice list is its own. A feature that only exists on the paid provider needs to say so on
   the row rather than being discovered.

### What this is not

**Not accents for the Commander, and not for d47 itself.** The personas are written in English and
cast deliberately; this is about the voices d47 *quotes*, not the voice it *has*. And **not
translation** — nothing here proposes that d47 speak anything but English to the Commander.

---

## 43 — A speaking rate per category, not only per provider

Raised 2026-08-25 while proposing per-role voice providers, and deliberately kept out of that
phase — see [per-role-voice-providers.md](per-role-voice-providers.md) §1.4. The Commander's lean:
brisk NPCs, a measured ship's core.

### It overturns a stated ruling, which is why it is an entry rather than a line in the phase

`VoiceCast.Rate`'s own comment: *"One value for the whole cast rather than per role: it is a
property of how fast the Commander likes to be spoken to, not of who is speaking."*

**That comment is not wrong, it is answering a narrower question.** It was written when one
provider spoke for everybody, and against that world it is plainly right. What it did not have to
consider is a cast drawn from several providers at once.

### Most of what is wanted arrives free with the phase, and that is the argument for waiting

Rate is stored per **provider** today (`SpeechSettings.ProviderRates`), because providers disagree
about units and range — Edge takes a wide percentage offset, ElevenLabs a multiplier it refuses to
exceed — and the settings row narrows to the selected one's range.

So once each category names its own provider, **NPCs on Edge and the ship's core on ElevenLabs are
already two independently-set rates.** What this entry adds is only the case where **two categories
share one provider** and the Commander wants them at different speeds — over-the-air voices all
default to Edge, so that case is the common one rather than the exotic one, but it is still
narrower than "rate is per role".

### The open question — this wants an answer before the code

**Which range does the row narrow to when the categories disagree?** `speech.md {#rate}` documents
one row narrowing to the selected provider's limits, and ElevenLabs rejects an out-of-range speed
outright rather than clamping — so a row showing one range while writing a value for a category on
a different provider is the failure `docs/capabilities/listening.md` already names in the other
direction: a control that appears to work and does nothing.

Either the row becomes one per category, each narrowed to its own provider, or there is one row and
the value is clamped per category on the way out. The first is honest and is six rows; the second
is one row that silently means different things. **Neither is obviously right**, which is most of
why this is not in the phase.
