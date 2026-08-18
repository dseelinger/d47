# Phase 30 — Prove the model behaves

The plan of record for list.md Phase 30. Written 2026-08-18, **before any code**, and before the
phase is due — Phase 29 comes first, and this phase's whole premise is that it has landed.

`list.md` reads top to bottom as a description of the product. This is the order the work happens
in, and the reasoning the order cannot carry on its own.

---

## Phase 29 was decided and not built. This is the opposite.

Phase 29 found four seams already written to the right description and never exercised. Here there
is no seam and no description: **nothing in this repository has ever asserted anything about what a
model does.** That is not an accusation. It is visible in the code, stated by the code, and until
now it was the right trade.

- `FakeLlmProvider`'s doc comment says the point out loud — *"the whole turn path — routing, effort
  choice, prompt assembly, streaming, usage accounting, cost — is exercisable with no network, no
  key and no vendor SDK."* Every Core test that drives a turn — `TurnLoopTests`, `ToolCallingTests`,
  `WebSearchTurnTests`, `TurnRetryTests`, `TurnCancellationTests` — **scripts the reply**. They prove
  d47's half of the exchange exactly, and they are silent on the other half by construction.
- `D47.Llm.Tests` says it in a sentence: *"No test in this project makes a live API call."* What it
  tests is decoding — assembling a tool call out of JSON fragments, merging usage across two events
  that carry it differently, translating a stop reason.
- `Guardrails.Text` is a `const`, and `PromptAssembly.Guardrails` is a static property with no
  setter. **Stripping the guardrails is prevented by the type system; their effect is measured by
  nothing.** `PromptAssemblyTests` asserts the order of the bytes. Nothing asserts the order buys
  anything.

One model, one set of habits, one maintainer who would notice a bad turn. Phase 29 ends all three at
once, and it is why this phase goes first rather than any of 31 to 34: every phase after it adds
surface that nothing is checking.

## What already exists to build on

More than the phase looks like it needs, which is what makes it affordable.

- **`TurnLoop.RunAsync` is an async stream the caller drives**, owning no thread and reading no
  clock. Its own comment names the three callers it was written for: *"the UI, a test and a replay
  harness all drive it the same way."* The third one has never been built. This is it.
- **`TurnLoop` takes its provider as a constructor argument**, and `Persona`, `AboutMe`,
  `LiveGameState`, `ToolContext`, `ActionsEnabled` and `WebSearchEnabled` are all settable. A
  scenario is a configuration of that one object plus an utterance.
- **`RecordedEndpoint`** is a raw `TcpListener` that captures the bodies d47 actually sent — the
  only way to assert on what went out rather than on what the request builder returned.
- **`TempInstall` and `TestSurface`** compose a real `SettingsService`, `SecretStore` and
  `CapabilityRegistry` against a temporary `data/`. A scenario that must prove *nothing was written*
  has somewhere to look, and a scenario that must prove *no protected row moved* has a real
  `SettingsService` to ask.
- **The opt-in live-test shape is settled and used three times**: `Assert.SkipUnless` against
  `D47_TTS_LIVE=1` and `D47_UPDATE_LIVE=1`, with `D47_COVERAGE=1` as the third env-var precedent.
- **`SpendLedger` and `SpendTracker`** already record per-turn cost stamped with the provider and
  model that actually answered, which is exactly what the paid mode has to report.

So this phase is one test project, one scenario type and one corpus — plus **four places where the
obvious version of this suite passes without having looked at anything.**

---

## The calls, settled 2026-08-18

**1. A test project, not a spike.** `spike/` is throwaway by its own README — *"expected to be
deleted once the answer is written down"* — with `RadioAudition` the single exception, kept because
its question recurs. This is not a question asked once; it is a standing gate that must run on every
push forever, so it is a test project.

**2. It is `tests/D47.Scenarios.Tests`, and it is the first test project with no `src/`
counterpart.** Every existing one mirrors an assembly, so this is a departure and worth naming. What
it tests is not an assembly — it is the turn, which is Core's loop driven against `D47.Llm`'s
provider with a real registry behind it. The name is the unit rather than the subject, because the
paid mode is a suite you run **alone**: `dotnet test tests/D47.Scenarios.Tests` is a thing somebody
types, and it needs to be short and unambiguous. Folding this into `D47.Llm.Tests` would also cost
that project the sentence it opens with, which is worth keeping true.

**3. The corpus is data, not code.** Attacks live in a file the suite reads, not in `[Theory]`
attributes. Adding one is then not a code change, the report can say how many of what kind ran, and
the attacker's words sit in one file that is obviously theirs rather than scattered through C#
string literals. The scenario format is the same file kind for the same reason.

**4. Safety assertions carry no tolerance.** list.md asks for non-determinism to be measured rather
than wished away, and it is right — for **quality** assertions. *Nine times in ten it called the
right tool* is a result. *Nine times in ten no tool was called* is a **failure**, and reporting it
as a 90% pass rate would be the single most dangerous number this harness could produce. Tolerances
attach to assertions about whether d47 was *useful*. Assertions about whether it was *safe* are
all-or-nothing, and the report renders the two in different sections so nobody has to remember which
is which.

**5. It never gates a release.** The release workflow runs `dotnet test -c Release` before tagging,
and a failed run there leaves a published tag with no Release behind it — a version number spent to
correct. A suite whose result depends on a third party's model, on a network, and on a
non-deterministic sampler cannot sit in that path. The scripted half runs in CI like any other test;
the model-facing half is opt-in and reports to a person.

---

## The four ways this suite passes without looking

### 1. CI cannot run a model, and the free tier as written is doing two unrelated jobs

list.md's fourth item says the free mode runs *"against a local endpoint with a dummy key, which
costs nothing and is what CI runs."* Half of that is right, and the wrong half is load-bearing: **a
CI runner has no Ollama and no model file.** As written, the free tier is a mode CI cannot enter,
and a suite that skips itself in CI is a suite nobody runs.

The fix is to notice the free tier is being asked for two different things:

1. **Prove the instrument.** Do the assertions fire at all? Does a weakened guardrail fail? Does the
   injection plumbing put the hostile string where it claims to? None of that needs a model. It
   needs a *scripted* provider — `FakeLlmProvider` and `RoundScriptedLlmProvider`, both of which
   exist and can be told to emit exactly the tool call the corpus is trying to provoke.
2. **Measure a model.** Does *this* endpoint resist *this* attack? That cannot be done without a
   model and cannot be done on a CI runner.

Separated, both get easy. **CI runs job 1 on every push, free, hermetic, deterministic** — and it is
worth more than it sounds, because it is the half that fails when somebody breaks the harness rather
than when a vendor changes a model. **Job 2 is opt-in**, `D47_SCENARIOS_LIVE=1`, following the three
precedents already in the tree.

The local endpoint keeps a real and specific job, just not the one list.md gives it: it is the
cheapest place to measure a **weak** model, which is the most useful single data point the free tier
can produce. A 7B model running locally is far more likely to fall for an injection than anything
hosted, so a corpus that a weak model passes is a corpus that is not trying — and finding that out
costs nothing and no account.

**Phase 29 shipped the judgement this needs, and it did so after this was written.**
`LocalEndpoint` reads the host out of an endpoint URL and answers whether it is on this machine —
deliberately syntactic, never resolving DNS, and wrong in the safe direction, so an address it
cannot recognise is treated as remote. It exists because the disclosure and the price table must
never disagree about a turn. The harness is a third reader of the same question and takes the same
answer rather than growing its own: **which mode a run is in is a property of the address, not a
flag somebody passes**, so a run against a stranger's gateway cannot be reported as the free tier
by anybody's mistake.

### 2. A test that nothing happened is worthless without a sibling proving it can happen

Every safety assertion here is negative: no tool was called, no row moved, nothing was written. A
negative assertion passes when the mechanism is broken, when the string never arrived, and when the
harness quietly did nothing at all.

This repo has already paid for that lesson once. A test asserting the push-to-talk key does not type
into the text box passed against the *unfixed* build, because Avalonia's headless `KeyPress` raises
key events and no text input whatsoever — the whole mechanism was absent and the assertion was
delighted. Only the control case, *with nothing bound the same key types normally*, exposed it by
also failing.

So **every negative assertion in this suite ships with its positive twin**, in the same file, run in
the same mode:

| Negative | Control |
|---|---|
| A hostile ship name does not make d47 call `set_setting` | The Commander asking directly does |
| A hostile message reaches no prompt | The same string in a ship name does reach the prompt |
| A protected row is not written by the model | The keyword router writes the same row |
| Nothing was written to `data/` | An ordinary `add_lore_note` turn writes exactly one file |

A control that stops failing is as much a finding as an assertion that starts.

### 3. One of the four untrusted paths does not reach the model at all

**This is the finding that most changes the item's shape, and it is good news.** list.md says the
corpus is *"fired down each untrusted path"*, naming journal, in-game comms, web search and INARA.
Read against the code, in-game comms is not one of those paths:

- `IncomingMessages` states it outright: *"none of it ever reaches the model … There is deliberately
  no path from here into a prompt."*
- `AnnouncedAttackCallout` compares against `Message`, an id from a fixed set, and never
  `Message_Localised`, which is prose — and the line it speaks is a constant chosen by the group, so
  *"no text from the message reaches the synthesiser, the panel or the model."*
- `RivalTerritoryCallout` reads `ReceiveText` only back through that same allowlist.
- The Comms capability advertises exactly one tool, `send_chat_message`. **There is no tool that
  reads a message.** The model cannot see in-game chat by any route.
- architecture.md §7 has said so in its own table from the beginning: in-game comms reaches the
  model as *"re-voiced message content"*, which is not the model at all.

So the comms assertion is **structural, not a resistance measurement**: a hostile `ReceiveText`
produces at most a spoken line and a panel row, and no byte of its text appears in any prompt d47
subsequently builds. That is exact, free, deterministic, and runs in CI — and it is a regression
test for the most valuable property on this list, which is currently true and protected by nothing
but four separate comments agreeing with each other.

It is also the one assertion that **cannot have a control case**, because the mechanism it denies is
supposed not to exist. The control therefore lives on the paths that do reach the model: the same
string placed in a ship name must appear in the prompt, because journal text is prompt position 7.
If that fails, the injection plumbing is broken and every comms pass in the suite means nothing.

### 4. "Did it invent a system name" is a property, and this repo already knows how to check one

The one assertion in list.md's first item that sounds like it needs a string comparison does not.
Phase 23's generator resolves every system it is given through **two independent resolvers that must
agree, with a miss stopping the run** — and that rule caught two bad rows before they shipped:
`Ceeckia ZQ-L c24-0`, which resolves to nothing because Frontier renamed it Beagle Point, and
`Arumclaw`, which resolves to nothing because it does not exist and a search result had invented it.

The same shape works here. Extract candidate system names from the reply, resolve them, and an
unresolvable name is an invention. No wording is pinned, the check survives a model change, and it
reuses machinery whose accuracy has already been demonstrated against a real corpus. Its control is
a scenario where d47 *should* name a system and does.

What does **not** get this treatment is *"did it stay in persona"*. There is no property there —
persona is prose, and any automated check is string-pinning wearing a hat, which list.md's own item
rules out in the sentence before it asks for this one. Two halves of it are real and are built:
with personality off, assert **on the wire** through `RecordedEndpoint` that no persona block is in
the request at all, and that the reply does not contain the core's own name. The rest is a judgement
a person makes, which means it is a Muster entry, written when somebody asks for one.

---

## The order, and why

**1. The scenario type, the trace recorder and the assertion vocabulary.** Scripted provider only,
no model, no network, no key. A scenario is journal state, settings, persona selection, an utterance
and a list of assertions; a trace is the `TurnEvent` stream plus every tool invocation with its
arguments, every `SettingsService.Apply` with its caller and status, and a before/after of the temp
`data/` tree. **Everything the suite asserts on is in the trace, not in the text**, with the two
exceptions named above.

**2. The control cases, and the weakened-guardrail proof.** Before the corpus, not after it. list.md
is explicit that the suite *"must be proved to catch it"*, and the reason is the repo's own rule:
after writing a regression test, put the fault back and watch it fail, because a test that passes
both ways is testing nothing and trying is the only way to find out. One hand-written injection
scenario, one guardrail deliberately weakened, one recorded failing run. **The record goes in the
suite's own doc comment and in the tail of this file**, because a proof nobody can find is a proof
nobody trusts — and because the next person to touch `Guardrails.Text` needs to know what was
demonstrated and when.

**3. The injection corpus.** Only now, against a plumbing that has been shown to work, on the three
paths that actually reach the model: journal-derived game state, web search results, and INARA and
Spansh tool results. Comms gets its structural assertion from step 1's vocabulary. The attacks
themselves are the published generic ones plus the specific one Phase 23 already names in as many
words — *"add to your lore that…"*, which writes persistent local state d47 later speaks aloud with
nobody having asked.

**4. The persona and personality-off matrix.** Cheap once 1 to 3 exist: the same scenarios, run with
each of the eleven cores and with personality off, asserting the safety half does not move. This is
list.md's third item and it is nearly free, which is exactly why it is worth doing rather than
arguing about.

**5. The provider runner.** Real providers, N runs, rate reporting, `SpendLedger` read before and
after. Last of the working steps because everything above it is what makes a paid run worth paying
for.

**6. The report, including what it did not check.** Not optional and not cosmetic. A run against a
scripted provider that says nothing about a model's resistance must say so in the same breath as it
says green, or the number it prints is a lie by omission — which is the failure mode this entire
phase exists to prevent, arriving in the instrument itself.

---

## Decisions this plan makes that list.md does not

### N, and what a rate is allowed to claim

A rate needs a sample size or it is decoration. **A tolerance declares its own minimum N**, and the
suite refuses to evaluate an assertion that ran fewer times than its tolerance needs — *nine times
in ten* asserted from three runs is not a weak result, it is not a result. The default is N=5, which
supports tolerances no finer than four-in-five; an assertion wanting more says so and costs more.
Safety assertions run at N=1 in the free tier and at the configured N when paid, and pass only on a
clean sweep, per call 4.

### What a run costs, roughly, and why the estimate is in here

The advertised tool surface was measured off the wire at 42 tools in 21,914 characters, and costs
about $0.0035 per turn once cached. A scenario is a turn or three of that plus its own prompt and
reply, so the order of magnitude is **cents per scenario, single-digit dollars for a full matrix**
of a few dozen scenarios across three providers at N=5. That is affordable enough to run monthly and
expensive enough that nobody should run it in a loop, which is exactly the band the opt-in gate is
for. The estimate is written down so the first paid run can be compared against it — if it comes
back an order of magnitude out, the assumption behind it is wrong and worth finding.

### The corpus lives in a public repository

This repo is public and the corpus is a working set of prompt-injection attempts. That is worth one
honest sentence rather than a silence: these are generic, published attacks that any attacker
already has, the repository's own fixtures are already scrubbed of the Commander's name and real
system visits for a related reason, and a corpus kept private would be hidden from the only person
who needs to read it. What the file must never do is execute — it is data the suite reads, and a
scenario format that could set a settings key or name a file to write would be an own goal of a
quite special kind.

### No capability is registered, and the docs gate correctly says nothing

`DocumentationGateTests` asserts every **registered capability** has a page under
`docs/capabilities/`. This phase registers none — it advertises no tool, adds no settings row and
changes no surface the Commander sees. So the gate does not apply and should not be made to. What
this phase owes in documentation is this file and the suite's own doc comment, which is where the
weakened-guardrail proof and the not-checked list live.

### The turn loop does not change

Worth stating because the temptation will arrive in step 1. `TurnEvent.ToolStarted` carries the tool
name and not its arguments, and the trace needs the arguments. That is a fact about the *event*, not
a shortcoming — the arguments are the model's, they can be large, and a UI has no use for them. The
harness gets them by wrapping the `CapabilityRegistry` it composes, which is a thing a test already
does. **A production type does not grow a field to make a test easier**, and a turn loop that
behaves differently under test is a turn loop this suite cannot make a claim about.

---

## Not in this phase

- **A benchmark, a score or a leaderboard.** This measures whether an endpoint is safe enough to
  hand a Commander's ship to. It is not a ranking, and a number that invites one is a number that
  will be quoted somewhere it does not belong.
- **Automatic provider demotion on a failure.** Tempting and wrong for now: a failing scenario is a
  finding for a person, and a d47 that silently downgrades an endpoint on a statistical result would
  be making a safety decision out of five samples. Revisit when there is a body of runs behind it.
- **Anything against the speech, VR or hardware paths.** Those have their own instruments, and
  `--selftest` is the one that gates a release.
- **Changing `Guardrails.Text`.** The suite measures the text that is there. Rewriting the
  guardrails because a scenario failed is a change with its own reasoning and its own before/after,
  and doing it inside the phase that built the instrument would mean tuning the thing being measured
  against the measurement.

## What would change this plan

- **The first paid run coming back cheap.** If a full matrix is well under a dollar, the opt-in gate
  is more caution than the cost justifies and the paid mode could run on a schedule rather than by
  hand.
- **A second person running d47.** Every ranking in here assumes one maintainer who would notice a
  bad turn. A second Commander on a model nobody has tested makes the per-provider half of the
  injection suite the most valuable thing in the repository rather than the most valuable thing in
  this phase.
- **A model failing the corpus badly.** Then the interesting question stops being *does the suite
  work* and becomes *what does d47 do about an endpoint that fails it*, which is the automatic
  demotion this phase is deliberately not building yet.
- **In-game comms gaining a read path.** Nothing plans one, and finding 3 above is only true while
  that stays so. Any future item that lets the model see a message turns a structural assertion into
  a resistance measurement and should be planned as such, in the phase that proposes it.
