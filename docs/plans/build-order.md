# What to build next, and why

**The phase numbers are the order, with nothing running ahead of them.** Read the numbers; that is
the sequence. This document carries the reasoning behind it, which the numbers alone cannot.

Ranked on four things, in this order: **is it blocked**, **what does it unblock**, **value per hour
to a Commander**, and **how likely is the work to be wasted**.

## Two renumbers, on the same day, and why the second one was right

**2026-08-15, first pass.** Phases 15 to 21 were renumbered into build order at the maintainer's
instruction — a deliberate, one-off exception to the rule in `CLAUDE.md`. The cost was paid rather
than dodged: every reference moved with it, including three code comments in `MainWindow.axaml.cs`,
`UpdateChecker.cs` and `UpdateInstaller.cs`.

**2026-08-15, second pass.** Phase 15 shipped in `v0.11.0`, and this document then described three
items as running "before any of it" — two spikes and the first-run key prompt, listed above Phase 16
while belonging to Phases 17 and 18. That reads as *Phase 17 before Phase 16*, and it was read that
way within the hour.

The first fix attempted was better wording. **The maintainer's fix was better than that: give those
three items a phase of their own and make it 16.** The ordering is now expressed by the numbers
rather than explained in a paragraph beside them, which is the only version that cannot be misread —
a document that has to say "the order below is not what it looks like" has already lost.

**Both renumbers are still one-off exceptions and the rule stands.** New phases are appended, an
existing one is never renumbered, and the frozen set only grows. It is now **1 to 21**. Phase 15
shipped on 2026-08-15 carrying 22 citations across 18 files, joining the several hundred that make 1
to 14 immovable; 16 through 20 shipped after it, and **Phase 21 was appended on 2026-08-16** — the
first phase added under the appended-only rule rather than moved by one of the two passes below.

The mapping, for anything written before 2026-08-15:

| Originally | After the first pass | **Now** | Phase |
|---|---|---|---|
| — | — | **16** | Before the rest of it *(new — three items pulled out of 17 and 18)* |
| 21 | 15 | **15** | Warnings that arrive in time — **shipped, `v0.11.0`** |
| 16 | 16 | **17** | Checklists |
| 15 | 17 | **18** | Activity assistants |
| 17 | 18 | **19** | Session tooling and release polish |
| 19 | 19 | **20** | On-foot engineering |
| 18 | 20 | **21** | HOTAS switches |
| 20 | 21 | **22** | Reading the screen |

**The one thing a remap cannot do for you.** Both passes moved numbers correctly and both got the
same class of thing wrong by hand: a citation pointing at an *item* that changed phase, which a
faithful remap carries to the wrong place — still resolving, so nothing reports it. The first pass
sent two spike pages to the phase the NPC-comms measurement had left. The second pass would have sent
`list.md`'s "whether that table exists at all is Phase 17's spike" to Phase 18, when the spike it
names had just moved to 16. **Remap the numbers mechanically; check the moved items by name.**

---

## ~~Phase 15 — Warnings that arrive in time~~ — shipped in `v0.11.0`, 2026-08-15

Kept rather than deleted, because the reasoning is the worked example: it was ranked first for being
the readiest work in the repo — both items measured against 912 journals, both naming their own
false positives, neither needing a source, a table or a network call — and it closed in a day.

**The ranking held, and one line of it did not.** "There is nothing to research" was wrong in a way
worth recording: the measurement was re-run against the corpus rather than taken from `list.md`, and
that pass is what found the three id groups that clear 40% and are still not shipped, and the
`npc`-channel requirement that closes a spoof nobody had written down. Cheap, but not nothing. See
[../spikes/journal-corpus-warnings.md](../spikes/journal-corpus-warnings.md).

## ~~Phase 16 — Before the rest of it~~ — complete 2026-08-16

**Three items pulled out of two later phases on 2026-08-15**, and given a phase so that the running
order is the numbers rather than a paragraph explaining the numbers. Two are spikes; one is the gap a
new install falls into. All three closed on 2026-08-16.

**What the spikes changed.** Colonisation tracking got *easier* — the depot event is a snapshot, so
it needs no source at all — and colonisation *planning* got smaller, because no licence-clean facility
table exists. Exobiology gained a route planner that already returns per-species values, and lost the
mass-code heuristic, which the corpus cannot test. Both findings are in `docs/spikes/`, and both land
on Phase 17 and Phase 18 before either starts, which is what this phase existed to do.

**Both spikes** — `Spike: what can be known about exobiology before you land`, and `Spike: what is
already known about colonisation, and by whom`, which were the first two items of what is now Phase
18. They are research, they are cheap, and **their results reorder everything below**. A null on the
colonisation facility table shrinks what Phase 17 can promise — specifically *A colonisation plan
writes the checklist*; a null on the exobiology sources halves Phase 18. Running them first means the
rest of this list is built on measurements rather than on this document's guesses.

**A spike gating the phase it lives in is not a gate.** That is the structural argument for the move
and it is worth more than the ordering convenience: both spikes sat inside phases whose other items
could not be specified until the spike returned, so the phase could not be planned as a unit and its
own plan had to reach backwards past its first item.

**Ask for the keys on the first run that needs them** — from what is now Phase 19, and the one item
here that is not a spike. A fresh install has no language-model key, and this is the difference
between a new Commander having a companion and having a window that will not answer. It was buried in
a polish phase behind eleven other things.

**Its rank is the one question here about you rather than about the code.** It is first only if
somebody other than the maintainer is installing d47. If nobody is yet, it is the least urgent line
on this page rather than the most — see "What would change this order". The two spikes do not depend
on that answer, which is why they lead within the phase.

**Release:** this phase closes when the two findings pages exist and first-run asks for a key. Two of
its three items produce documents rather than code, so it is the one phase here whose minor release
is mostly a promise that the next two are built on something measured.

## ~~Phase 17 — Checklists~~ — complete 2026-08-16

**What everything else is waiting on.** Engineering's planning half was deferred here from Phase 14
and has been waiting since `v0.10.0` closed — the tables are built, so *An engineering plan writes
the checklist* can land the moment the substrate exists. Colonisation's planning half needs the same
substrate, which is the whole argument for not writing a second one.

The design was settled on 2026-08-15 and is written up in
[phase-17-checklists-and-colonisation.md](phase-17-checklists-and-colonisation.md): one surface, two
kinds of item, three groups, completion that is not removal, revision as a diff. **Item identity is
the decision to get right before any code** — two plans can only be diffed if an item knows what it
is independently of its position in a list.

One caveat: *A colonisation plan writes the checklist* needs **Phase 16's** colonisation spike. That
is the dependency that used to be expressed as "the spike sits above this line", and it is now
expressed as 16 coming before 17.

## ~~Phase 18 — Activity assistants~~ — complete 2026-08-16

Split, because the parts do not share a readiness.

**Colonisation tracking needs nothing and could ship any day** — `ColonisationConstructionDepot`
carries required and provided amounts, so it is subtraction over data already on disk, and it
replaces arithmetic the maintainer is currently doing on paper.

**Prospector callouts need decisions, not research** — thresholds and cadence, which are a
conversation rather than a spike.

**Exobiology is the long pole**: position plumbing that does not exist yet, per-body state that
outlives a session, and two items whose shape depends on what **Phase 16's** exobiology spike finds.
The plan is [phase-18-exobiology.md](phase-18-exobiology.md), which now begins by saying its own
spike is no longer in it.

## ~~Phase 19 — Session tooling and release polish~~ — complete 2026-08-16

Nine items, three subjects wearing one hat: voices, the log surfaces and the docs. It was flagged
as a candidate for splitting and it did not need to be — the hat turned out to be real, because
five of the nine are one subject and the other four are two pairs.

**The ordering guess held.** *Give the composition root a test harness* was ranked first for
getting more expensive the longer it waited, and running it first is what made the four voice items
cheap: two pure functions came out of `AppHost` — one deciding when the client is rebuilt and the
voice list refetched, one deciding what happens to stored voices on a provider switch — and every
voice item after it was a change to one of those with a test already pointing at it. It also found
a third fault in the code it was lifting past, which the two known ones had not: the speech key
check reported "accepted the key — 0 voices" for a key that had just been refused.

**Two items came back different from how they were written, and both in the same direction — the
thing being feared was not the thing that was wrong.**

The ElevenLabs spike predicted an empty picker on a fresh paid account. `GET /v1/voices` turns out
to need no account at all, so the picker is populated for everybody, and the real defect was the
one the item mentioned in passing: four different empties arriving as one, with nothing above the
seam able to tell them apart. See
[../spikes/elevenlabs-voice-sources.md](../spikes/elevenlabs-voice-sources.md).

The docs item predicted an unreadable banner, which was true, and the reachability line it added
almost as an afterthought is what found the actual bug: nine capability pages had no front-matter
title and were reaching the header only because GitHub Pages runs `jekyll-titles-from-headings`.

**One thing the phase shipped that no item asked for**, and it is the same shape both times: a
hand-maintained copy of a generated list had drifted. `index.md`'s capability table listed sixteen
of thirty-three. It is gone rather than corrected, because correcting it would have set the same
clock running again.

**Release:** `v0.14.0`, following `v0.13.0`. Two of the nine were already shipped before the phase
was picked up — the update check and the installer, the latter pulled forward out of order because
the Commander was unpacking archives by hand in the meantime.

## ~~Phase 20 — On-foot engineering~~ — complete 2026-08-16

**"Well specced" turned out to mean well specced against a source that was wrong.** The phase's own
spike ran first — the Phase 16 lesson applied inside a phase — and it overturned the item this phase
was most confident about: *"the material cost of an on-foot build is exactly and completely
knowable"*. It is, and not from EDEngineer's numbers, every one of which predates the patch that cut
them. Shipping them would have quoted two to three times the real cost of everything on foot while
agreeing with every other tool.

**Running the spike first is the whole reason that was caught.** Had it run in item order, four items
would have been built on the published figures and the correction would have arrived as a rewrite
rather than as a generator change. See
[phase-20-on-foot-engineering.md](phase-20-on-foot-engineering.md).

**Two of the six came back different in the same direction, and it is the direction the last three
phases keep coming back in: the thing being feared was not the thing that was wrong.** The spike was
written to answer the locker cap and the barter rate, and its two biggest results were the recipe
staleness and the credit multiplier, neither of which anybody had asked about. *Who unlocks whom* was
ranked for its stale quantities and its real defect was an attribution bug that had every Odyssey
tribute filed one link down the referral chain — reading as correct, because both ends of every
mis-filing are real engineers with real tributes.

**Release:** `v0.15.0`, following `v0.14.0`.

## Phase 21 — HOTAS switches — **shipped 2026-08-16**

Fully specced, and the hardest to verify — it needed the physical hardware in front of whoever was
building it, and the failure modes were all about a switch and the game disagreeing. Narrow audience
by construction: it does nothing for a Commander without a switch panel.

## Phase 29 — Bring your own model — **appended 2026-08-18, and it does not jump the queue**

An OpenAI provider, plus any endpoint speaking its protocol. Added under the appended-only rule, and
the sequencing question was asked and answered explicitly on the day it was written: **it does not
run before Phase 22.**

**Phase 22 was cut the same day, which settles that question by removing it rather than by
answering it** — see the tombstone in [list.md](../../list.md), which is all that is kept of it. The
record of the decision stays here because the reasoning still applies to the phases that remain: it
was declined for jumping ahead even though the argument was a good one — unblocked, self-contained,
no spike, no source, no hardware — because *the phase numbers are the order* is the rule this
document exists to keep, and the first line of it is that nothing runs ahead of them. That rule did
not stop applying when the phase in front of it went away.

**Phase 29 is now next, and nothing was decided to make it so.** 27 and 28 shipped on 2026-08-18 and
22 was cut the same day, so the queue in front of it emptied by the same rule that made it wait — the
numbers, rather than a judgement about any of them. That is the point: the rule decided both times.

**It is ranked on a guess, and the guess is named.** The value of the compatible half depends on how
many Commanders want to run a local model, which is the one input this document cannot measure and
the same blind spot Phases 18, 20 and 21 were ranked with. The OpenAI half does not depend on it:
that is a Commander who already has a key for the wrong vendor, and today they cannot use d47 at
all.

The plan of record, including the four calls settled on the day it was written and the four seams
that are Anthropic-shaped and say nothing about it, is
[phase-29-bring-your-own-model.md](phase-29-bring-your-own-model.md).

---

## What would change this order

- **A spike comes back empty.** Exobiology sources missing pushes that half of Phase 18 down;
  colonisation sources missing shrinks Phase 17's last item. Both spikes are in Phase 16, so this is
  the first thing that can happen rather than a risk carried for weeks.
- **Somebody other than the maintainer installs it.** Then the first-run item leads Phase 16 instead
  of trailing it, and the log surfaces in Phase 19 outrank everything that is not a safety warning.
- **The maintainer's play changes.** Phases 18, 20 and 21 are ranked partly on what gets played, and
  that is the one input this document cannot measure.
