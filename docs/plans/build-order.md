# What to build next, and why

**The phase numbers are the order.** Phases 15 to 21 were renumbered into build order on
2026-08-15 at the maintainer's instruction; this document carries the reasoning behind that
sequence, which the numbers alone cannot.

The renumber was a deliberate, one-off exception to the rule in `CLAUDE.md`, and the cost was paid
rather than dodged: every reference moved with it, including three code comments in
`MainWindow.axaml.cs`, `UpdateChecker.cs` and `UpdateInstaller.cs` that cite what is now Phase 18.
Phases 1 to 14 were not touched — they are built, and they carry the several hundred citations that
make renumbering expensive. From here, new phases are appended.

**And Phase 15 has joined them.** It shipped on 2026-08-15 in `v0.11.0` and now carries 22 citations
across 18 files, so 1–15 are frozen by the same argument and the frozen set only ever grows. A
second renumber was raised the same evening and declined on exactly this ground: there was no
reordering to express — the request came from reading the section below as phases jumping the queue,
which they are not — and the numbers already said the right thing. **If this document ever seems to
put a later phase before an earlier one, the bug is in this document's wording, not in the order.**

The mapping, for anything written before that date:

| Was | Is | Phase |
|---|---|---|
| 21 | **15** | Warnings that arrive in time |
| 16 | **16** | Checklists |
| 15 | **17** | Activity assistants |
| 17 | **18** | Session tooling and release polish |
| 19 | **19** | On-foot engineering |
| 18 | **20** | HOTAS switches |
| 20 | **21** | Reading the screen |

Ranked on four things, in this order: **is it blocked**, **what does it unblock**, **value per hour
to a Commander**, and **how likely is the work to be wasted**.

---

## Three individual items that run ahead of their own phase

**These are items, not phases. The phase order below is not affected by anything in this
section**, and reading it as "Phase 17 before Phase 16" is the misreading it has already caused
once. `list.md` is a list of phases; each phase is a list of items; and an item can be worth doing
before the phase around it without the phase moving. Nothing here is renumbered, reordered, or
promoted — these are three lines pulled forward out of two phases that otherwise stay exactly where
they are.

**Two of the three are spikes**, which is the ordinary reason an item runs early: a spike produces
a findings page, and the work that consumes those findings cannot be specified until it exists. A
spike running ahead of its phase is the normal shape of this list rather than an exception to it.

**Both Phase 17 spikes** — `Spike: what can be known about exobiology before you land`, and
`Spike: what is already known about colonisation, and by whom`. They are research, they are cheap,
and **their results reorder everything below**. A null result on the colonisation facility table
shrinks what Phase 16 can promise — specifically *A colonisation plan writes the checklist*, which
is why the caveat under Phase 16 points back here; a null on the exobiology sources halves Phase 17
itself. Running them first means the rest of this list is built on measurements rather than on this
document's guesses.

**Ask for the keys on the first run that needs them** — an item in Phase 18, and the one thing here
that is not a spike. A fresh install has no language-model key, and this item is the difference
between a new Commander having a companion and having a window that will not answer. It is the most
user-facing gap in the file and it is currently buried in a polish phase behind eleven other things.
Nothing else here matters to somebody who cannot get past the first run.

**It is also the one item whose rank is a question about you rather than about the code.** It is
first only if somebody other than the maintainer is installing d47. If nobody is yet, it is the
least urgent line on this page rather than the most — see "What would change this order".

---

## 1. ~~Phase 15 — Warnings that arrive in time~~ — shipped in `v0.11.0`, 2026-08-15

Kept rather than deleted, because the reasoning is the worked example: it was ranked first for being
the readiest work in the repo — both items measured against 912 journals, both naming their own
false positives, neither needing a source, a table or a network call — and it closed in a day.

**The ranking held, and one line of it did not.** "There is nothing to research" was wrong in a way
worth recording: the measurement was re-run against the corpus rather than taken from `list.md`, and
that pass is what found the three id groups that clear 40% and are still not shipped, and the
`npc`-channel requirement that closes a spoof nobody had written down. Cheap, but not nothing. See
[../spikes/journal-corpus-warnings.md](../spikes/journal-corpus-warnings.md).

## 2. Phase 16 — Checklists — **next**

**What everything else is waiting on.** Engineering's planning half was deferred here from Phase 14
and has been waiting since `v0.10.0` closed — the tables are built, so *An engineering plan writes
the checklist* can land the moment the substrate exists. Colonisation's planning half needs the same
substrate, which is the whole argument for not writing a second one.

The design was settled on 2026-08-15 and is written up in
[phase-16-checklists-and-colonisation.md](phase-16-checklists-and-colonisation.md): one surface, two
kinds of item, three groups, completion that is not removal, revision as a diff. **Item identity is
the decision to get right before any code** — two plans can only be diffed if an item knows what it
is independently of its position in a list.

One caveat: *A colonisation plan writes the checklist* needs Phase 17's colonisation spike, which is
why that spike is above this line.

## 3. Phase 17 — Activity assistants

Split, because the parts do not share a readiness.

**Colonisation tracking needs nothing and could ship any day** — `ColonisationConstructionDepot`
carries required and provided amounts, so it is subtraction over data already on disk, and it
replaces arithmetic the maintainer is currently doing on paper.

**Prospector callouts need decisions, not research** — thresholds and cadence, which are a
conversation rather than a spike.

**Exobiology is the long pole**: position plumbing that does not exist yet, per-body state that
outlives a session, and two items whose shape depends on what the spike finds.

## 4. Phase 18 — Session tooling and release polish

Ten items left, and they are three subjects wearing one hat: voices, first-run, and the log and docs
surfaces. **Give the composition root a test harness** is the one that gets more expensive the
longer it waits — `AppHost` is where the app is actually assembled and it has no tests — so it
belongs earlier than the polish around it.

If this phase is still one hat by the time it comes up, it is a candidate for splitting; moving items
between unbuilt phases is free, and a phase that cannot be finished holds its ready items hostage.
That is exactly why the warnings left Phase 17.

## 5. Phase 19 — On-foot engineering

Well specced, with its own spike and a sources document already written. It is a whole second
engineering domain, and it ranks here rather than higher for one reason: it is worth the most to a
Commander who plays on foot, and least to one who does not. That is a question about the maintainer's
own play, not about the code.

## 6. Phase 20 — HOTAS switches

Fully specced, and the hardest to verify — it needs the physical hardware in front of whoever is
building it, and the failure modes are all about a switch and the game disagreeing. Narrow audience
by construction: it does nothing for a Commander without a switch panel.

## 7. Phase 21 — Reading the screen

**Last deliberately.** It is the only phase whose spike might close it outright: if the VR mirror
carries nothing a panel can be located in, what remains is a desktop-only feature. Everything in it
is unproven, it would be the first input d47 reads that the game did not deliberately write down,
and its failure mode is a confident wrong answer rather than an error.

It is also the only one that would retire other phases' impossibilities — the contacts panel that
Phase 15's rival-Power warning is written around — so it earns its place on the list. Just not yet.

---

## What would change this order

- **A spike comes back empty.** Exobiology sources missing pushes that half of Phase 17 down;
  colonisation sources missing shrinks Phase 16's last item.
- **Somebody other than the maintainer installs it.** Then the first-run items and the log surfaces
  outrank everything that is not a safety warning.
- **The maintainer's play changes.** Phases 17, 19 and 20 are ranked partly on what gets played, and
  that is the one input this document cannot measure.
