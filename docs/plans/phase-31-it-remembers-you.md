# Phase 31 — It remembers you

The plan of record for list.md Phase 31. Written 2026-08-18, before any code, with Phase 30 merged
the same day.

`list.md` reads top to bottom as a description of the product. This is the order the work happens
in, and the reasoning the order cannot carry on its own.

---

## The phase in one sentence

d47 has eleven cores, a journal spine, a checklist, plans for ships, suits and engineers, and a
prompt assembled in volatility order — and it forgets every single thing about the Commander the
moment the process exits. `_personaLastSeen` is a `Dictionary` field in `AppHost`. That is the whole
of d47's memory today, and it lives for as long as the window is open.

## What already exists to build on

Nearly all of it, which is what makes a four-item phase affordable.

- **Phase 23 built the tiering mechanism and wrote down why.** `LoreTier` is a field rather than a
  note the persona is trusted to add, `LoreArrival` records authorship separately from
  corroboration, and `LoreEntry.Spoken()` is the only place either turns into words. Phase 31
  extends that shape by one tier rather than inventing one.
- **Phase 23 also built both halves of the store pattern.** `LoreStore` is Commander-authored:
  polled, content-compared, hand-editable, and it reports a bad line through `Problems` instead of
  dropping it. `SamplingStore` is derived and keyed per Commander with the key *inside* the
  document, because a Frontier id comes out of the journal and turning untrusted text into a
  filename buys a path-traversal surface for an organisational convenience. Phase 31 needs one
  store with both characters, and both are already written down.
- **`SettingRow.Press`** is an `Info` row with a button that clears the state the row describes,
  and `SettingsService.Apply` refuses `Info` rows outright — so the empty-the-store action is
  unreachable from the tool surface for free, by the mechanism that already exists.
- **`PromptAssembly` is a record with the order as its contract**, and everything at or above the
  breakpoint is rendered by one method. Adding a position is an edit to that method and to the
  comment block that documents the order, and `PromptAssemblyTests` already asserts the order.
- **`FlavourBriefs.For`** decides which callouts get said in character. It is pure and it lives in
  Core, so the continuity line can be persona-varied without the App learning anything new.
- **`EngineeringPlan.Cost` and `UnlockPlanner`** already compute both halves of the item-4 example
  sentence: `PlanCosting.Shortfall` gives *three Selenium short*, and `UnlockChain.Steps.Count`
  gives *two steps out*.

## Order of work

1. **The store** (item 1). Everything else reads it.
2. **The observer** (item 1's second half). Without it the `Observed` tier is an enum member
   reachable by nothing, which `LoreArrival`'s own doc comment refuses to ship.
3. **Recall** (item 2). Needs entries to select from.
4. **Expiry, reading, emptying** (item 3). Needs a store with something in it to remove.
5. **The opening line** (item 4). Deliberately last, exactly as `list.md` says: it is the only part
   a Commander experiences, and it reads all four of the things above.

---

## Decisions taken before the code

### The advertised tool costs 275 bytes and there are 361 to spend

Measured, not estimated. The SRV profile — the largest, because it carries that vehicle's controls
on top of everything else — is at **39,639 bytes against `ToolProfiles.ComfortableBytes` of
40,000**. Every other profile is smaller: docked, landed, normal space and supercruise at 38,927,
on foot at 37,211, fighter at 36,499, no-game and degraded at 36,030.

So there is room for exactly one small tool, and Phase 31 spends it on the write path rather than
the read path:

- **`remember_about_me` is advertised**, with one string parameter and the shortest description that
  still reads as English. This is the only route by which a memory can arrive out of a conversation,
  and a memory store the model cannot write to is a store that only fills up when the Commander
  opens a panel.
- **`get_memories` and `forget_memory` are `Protected` with router phrases**, which costs zero
  advertised bytes. This is Phase 26's answer applied where it is free: *what do you remember about
  me* is an argument-free phrase, so the keyword router reaches it with no model in the path, and
  the router is also what makes item 2's promise keepable — the router returns the **store**, and
  the prompt carries a bounded **sample**.
- `ToolProfileTests` already fails if the ceiling is crossed. It is the proof, and it names the
  profile and the byte count when it fails.

**Measured after the fact: the write tool came to 275 bytes and the SRV profile shipped at 39,914,
leaving 86.** Every other profile moved by the same 275: docked, landed, normal space and supercruise
to 39,202, on foot to 37,486, fighter to 36,774, no-game and degraded to 36,305.

**The real fix remains the one `ToolProfiles` names** — `defer_loading` with
`tool_addition`/`tool_removal`, so the set can vary per mode without invalidating the cached prefix.
This phase does not do it and does not pretend to. Eighty-six bytes is not room for another tool,
which means the next phase that wants one has to do that work first.

### Three tiers, three arrivals, and neither list is the one `list.md` names

Two corrections, both in the direction of the code being able to support what it claims.

**The tiers are `Stated`, `Observed`, `Inferred`**, which is item 1's list exactly. What item 1 does
not say is who may set them, and the answer is: **not the model.** A turn steered by a hostile
in-game message is indistinguishable from a turn the Commander asked for, seen from inside a tool
handler — Phase 23 established that and labels every model-written lore entry as the model's
whatever the turn looked like. So the tier is a property of the write path and never a parameter:

| Route | Tier | Read back as |
|---|---|---|
| Panel | `Stated` | *You told me…* |
| Journal observer | `Observed` | *I noticed…* |
| `remember_about_me` | `Inferred` | *I wrote this down myself, and nothing has checked it…* |

That also removes the parameter a hostile message would aim at, and it saves the bytes an enum
parameter would have cost. A Commander who wants something recorded as their own word types it on
the panel, which is the same trade Phase 23 made for the same reason.

**The arrivals are `Panel`, `Model` and `Journal` — not the keyword router.** Item 1 names "panel,
keyword router, or the model's own tool call". The router cannot be one of them and the reason is
already written down in `LoreArrival`: the router's grammar is closed, a declared phrase carries
declared arguments and extracts nothing from what was said, and a memory is a sentence. There is no
phrase that could carry one. Phase 23 hit this and shipped two values with the absence documented;
Phase 31 hits it again and ships three, because it has a third real route the checklist did not
anticipate — the journal. Recording an arrival of `Router` would be a claim the code does not
support, which is the specific failure `LoreArrival`'s comment exists to prevent.

Tier and arrival stay separate fields even though the three shipped routes map one-to-one onto each
other. They answer different questions — *how good is this* and *who wrote it* — and the file is
hand-editable, so a Commander can write a tier the routes do not produce and the arrival still says
how the line got there.

### One store, and the observed entries live in it

`data/memories.json`, one file, per Commander with the key inside the document, written through
`AtomicFile`, polled by content comparison rather than by a last-write time (Phase 21's correction,
for the reason `SwitchStore` records: Windows moves the file-system clock about every 15.6 ms, so a
hand edit is a one-off that stays missed).

The observed entries go in the **same** file rather than a derived sibling. That breaks the neat
`LoreStore`/`LoreVisits` split, and it is the right break: item 3 asks for *the whole store* to be
readable as plain text and emptied in one action, and a store with a second half somewhere else is
two places to look and one of them will be missed. Observed entries are keyed and **replaced** rather
than appended, so the file does not grow with them.

**Two observed subjects ship, and they are deliberately dull**: where the Commander was, and what
they were flying. Both are facts d47 read out of the journal, both are per Commander, and both are
what item 4's first clause needs. Anything cleverer than that is Phase 32, which is a batch job over
912 journals and is explicitly a different phase.

### Recall is selected on the situation and reassigned only when its bytes change

This is the trap item 2 names, and it is worth spelling out because the obvious implementation is
wrong in an expensive way. Recall sits **above** the cache breakpoint, so the prefix it invalidates
is the whole prefix — the 39,639 bytes of tool schemas serialize first and go cold with it. A recall
block keyed on the live system, reassigned per turn, would cost a cold prefix on every jump: 4.4
cents a time at the rate `ToolProfiles` measured, twenty times a day.

So the selection is quantized the way the tool profiles are:

- Selection reads the situation — this system, this ship, this activity — and scores entries against
  it, then falls back to the Commander's own stated facts, then to observations, then to inferences.
- It is bounded by a count and a character budget, and **the block says so in its own text**: *these
  are 8 of 41 things I remember.* A model that knows it holds a sample cannot claim the set is
  complete, and the Commander asking *what do you remember about me* reaches the router, which
  answers from the store.
- **The rendered text carries no situation and no count of anything that moves.** Flying through
  twenty systems d47 remembers nothing about produces byte-identical recall twenty times.
- The host compares the rendered text against what is already assembled and assigns only on a
  difference. A cache miss then happens when the Commander flies somewhere they have history —
  which is the turn where the memory is the reason they wanted it.

### Expiry defaults to ninety days, and says what it took

The Commander's choice, and the shipped default is **90 days**. That makes the *forgetting is said
out loud* half of item 3 load-bearing rather than decorative: the expiry pass returns what it
removed, and a removed `Stated` entry — something a person typed — is announced. An `Observed` or
`Inferred` entry going quiet is not worth a sentence; a fact the Commander told d47 three months ago
disappearing without a word is exactly the failure the item describes.

Emptying is **panel-only**. Item 3 says "in one action from the panel", the action joins the
`privacy` capability rather than inventing a second place to look, and it gets no router phrase —
"forget everything about me" is a sentence a transcriber can produce out of a misheard one, and this
is the one action in the phase that cannot be undone.

### The opening line is authored in Core

`ContinuityCallout` assembles it from the store, the checklist and the plans, in Core, with no model
in the path — then goes through the same `FlavourBriefs` path the ambient remarks and the carrier's
lines already use, so a persona can re-say it and personality-off says it plainly. Deterministic,
testable, free, and incapable of promoting *you were three Selenium short* into something more
dramatic.

It fires once, on the first live tick after a settle window, and it is **silent when there is
nothing worth saying** rather than manufacturing continuity out of an empty store. A first run says
nothing at all.

---

## What leaves the machine changes, and the disclosure has to say so

Recall goes into the prompt. That means the facts d47 remembers about the Commander are sent to the
language-model endpoint on every turn, and three `LlmProviderCatalog.Egress` strings and the
loopback line in `EgressDisclosure` currently enumerate what is sent without them. Updating those is
part of this phase and not a follow-up: the disclosure claims to be exhaustive by construction, and
an exhaustive list with a new item missing from it is worse than no list.

## Acceptance

- A memory written on the panel survives a restart, reads back as the Commander's own word, and is
  never promoted by anything.
- A memory written by `remember_about_me` reads back hedged, for as long as it exists.
- The recall block is bounded, states its own sample size, and renders byte-identically across a
  jump into a system with nothing attached to it.
- No profile crosses `ComfortableBytes` — `ToolProfileTests`, unchanged.
- An expiry that removes a `Stated` entry is announced; one that removes an `Inferred` entry is not.
- Emptying the store is reachable from the panel and from nowhere the model can call.
- The continuity line is silent on a first run and, given a store with a stale location plus a plan
  with a shortfall, says both halves in one sentence.

## Proved to catch it, 2026-08-18

Three faults, reintroduced deliberately and watched to fail, because a negative assertion passes
when the mechanism is broken just as readily as when it holds.

1. **A silently-failed edit, caught for real rather than staged.** The append that puts recall into
   `RenderCachedSystemBlock` did not land — a scripted edit reported success and changed nothing —
   and `RecallSitsInsideTheCachedBlockBelowAboutMeAndBelowTheGuardrails` failed on the first run.
   Without it the whole of item 2 would have shipped as a property with a setter and no reader.
2. **`MemoryBook.Remember` filing a model-written entry as `Stated`.**
   `AFactTheModelWroteIsAnInferenceWhateverTheTurnLookedLike` and
   `NothingPromotesAnInferenceToTheCommandersWord` both failed.
3. **The rendered recall carrying the current system name.**
   `FlyingSomewhereWithNoHistoryRendersTheSameBytes` failed — which is the assertion standing
   between this phase and a cold 39,000-byte prefix on every jump.

What none of it proves is that a *model* honours the labels. That is not assertable from this side,
which is why the two scenarios added to Phase 30's corpus —
`control/recall-reaches-the-prompt` and `memory/an-inference-is-not-spoken-as-fact` — measure it
instead. The first is exercised offline, on every push, by
`InstrumentTests.WhatD47RemembersReachesTheWireBelowTheGuardrails`, so the second is a question
about a model rather than about a string that never arrived.
