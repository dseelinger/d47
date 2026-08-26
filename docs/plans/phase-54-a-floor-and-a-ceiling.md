# Phase 54 — A floor and a ceiling

**Status: built. Steps 1–8 are in the tree; step 9, the pass by hand, is owed.** The plan was
approved on 2026-08-23 and deliberately not started — see the standing instruction at the end of
this file. Item one was built on the Commander's word on 2026-08-25 and shipped as a patch; `Xhigh`
went out quietly with v0.71.2 the same day; the rest was built on 2026-08-26 in one pass, on the
Commander's word again.

**What is in the tree now** is the whole phase: the Haiku fix, the fifth rung,
`ThinkingEffortRange`, the clamp at the `TurnLoop` call site, the three settings rows with both
clearing writes amended, the eight background callers pointed at `BackgroundModel`, and the docs.
Suite green at 4,934 tests.

**Two things are still owed and neither is code.** The manual half of item one — a real
conversation on Haiku with the spend ledger's warmth column in view, because no test can prove the
4096-token cache floor is cleared, and it fails in silence when it is not. And step 9's pass by
hand over the three rows.

**Three findings worth reading before the next plan cites this one**, all recorded against their
`list.md` items: `Math.Clamp` cannot take an enum at all, the anchor gate does not fail before the
docs are written, and Anthropic's endpoint row does not apply so the endpoint-clearing test runs on
OpenAI.

This is the plan of record. It exists because the reasoning below lived only in a chat plan outside
the repository, where nobody else can read it and nothing cites it.

**Anchors here are symbols, not line numbers.** The approved plan carried line numbers, and by the
day it was transcribed three of them had already decayed — `FlavourTurn`'s `coldPrefixExpected` had
moved two lines, and `AppHost.cs` had shifted by tens after Phases 49 to 53. A plan of record that
cites a wrong line is worse than one that cites none, so every reference below names a symbol or
quotes a phrase you can search for.

---

## What was asked, and what changed the shape

Asked for 2026-08-23: **Min and Max "Model" and "Thinking Level"**, with dynamic routing, per
provider where possible. The examples given were Haiku 4.5 → Opus 5/High as the default band; Haiku
only, to save money; Opus 5 Medium → Opus 5 High.

Exploration changed it. Three findings:

**1. The two dials do not behave alike.** `output_config.effort` is a top-level request field, so it
sits outside the cached prefix (`tools → system → messages`) — routing effort per turn is free,
which is why `EffortRouter` already gets away with it. Model is the opposite: caches are
model-scoped, and `TurnLoop` already knows a switch costs a cold prefix.

**2. Per-turn model routing loses money.** Against an assumed ~6k-token prefix: a warm Opus 5 turn is
~1.05¢, a warm Haiku turn ~0.21¢, and *returning* to Opus after a detour costs ~3.5¢ in cache
writes. One detour costs roughly **23×** what the cheap turn saved. Alternation is strictly worse
than never routing; it only pays across runs of five-plus consecutive cheap turns.

**3. The free money is in the background calls.** The `FlavourTurn` sites — ambient remarks, opening
brief, gap reaction, two lore lookups, voice casting, adventure generation — all read `Turns.Model`.
They carry **no conversation history** (`FlavourTurn` builds `History` from the instruction alone)
and already declare `coldPrefixExpected: true`. Routing them to a cheap model costs zero cache and
is a straight ~5× saving on that traffic. It needs a floor **setting**, not a router.

And a live defect: **`claude-haiku-4-5` is in the picker and cannot answer a turn.**
`AnthropicLlmProvider` sets `Thinking = new ThinkingConfigAdaptive { … }` and
`OutputConfig = new OutputConfig { Effort = … }` unconditionally. Haiku 4.5 is pre-4.6 and rejects
both. `FlavourTurn` swallows the failure at `LogDebug`, so the first thing anybody does with a floor
setting would silently kill every ambient line with nothing on screen. That is why it is item one.

### Decisions made — settled, do not re-litigate

| | |
|---|---|
| **Model dial** | By call class, not per turn. Conversation pins to the ceiling; background takes the floor. |
| **Haiku fix** | Inside this phase, as item one. **Amended 2026-08-25** — see below: the goal is Haiku as a *viable choice*, not merely a safe floor, and the fix is a deny-list **and** a model-keyed demotion rather than the deny-list alone. |
| **Defaults** | New properties null ⇒ behaviour identical to today. |
| **`Xhigh`** | Joins the ladder. Five rungs. |

**Phase number 54**, taken on 2026-08-25 when `list.md` still ended at 53. `docs/plans/build-order.md`
records no ordering commitment for 48–53, so building this ahead of Phase 55 costs nothing and
neither blocks the other.

---

## 1. The Haiku fix

> **Amended 2026-08-25, and the decision it changes is named.** The plan as approved fixed the
> defect: stop sending two fields Haiku rejects, so a floor pointed at it does not kill every
> ambient line. The Commander's instruction on 2026-08-25 raised the bar — **make Haiku a viable
> option**, not merely a safe one to point a floor at. That is a different target and it wants two
> mechanisms rather than one, plus two capability differences named rather than discovered.

### Why it fails, exactly

`AnthropicLlmProvider` puts two fields on every request with no condition:

```csharp
Thinking = new ThinkingConfigAdaptive { Display = Display.Summarized },
OutputConfig = new OutputConfig { Effort = Translate(request.Effort) },
```

Both are 4.6-generation features; Haiku 4.5 is pre-4.6 and rejects them. `CapabilitiesFor` hardcodes
`SupportsThinkingEffort = true` for **every** model, so nothing upstream ever learns to omit them.

Of the five models in the picker — `claude-sonnet-5`, `claude-opus-5`, `claude-opus-4-8`,
`claude-haiku-4-5`, `claude-fable-5` — **Haiku 4.5 is the only pre-4.6 one.** When this code was
written every Anthropic model took both fields, which is why the question never arose.

Two different failures follow, and only one of them is loud. Pinned as the conversation model, turns
fail visibly. Anything going through `FlavourTurn` fails **silently**: it catches, logs at Debug and
returns null, so the line falls back to its authored text with nothing on screen.

### The OpenAI path already solves this, and the shape is worth copying

Both OpenAI providers wrap every optional field:

```csharp
if (EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.ReasoningEffort))
    json.WriteString("reasoning_effort", Translate(request.Effort));
```

That is Phase 29's **advertise, then demote**: send the field, and if the endpoint refuses with an
error naming it, record the refusal and retry the turn exactly once without it. Session-only, never
written to disk, once per capability per endpoint. Its own comment states the principle — a model
list says what an endpoint *serves* and nothing about what it *accepts*, so that is learned from the
first failure rather than assumed.

**The Anthropic provider has no demotion at all.** That is the actual gap: not that Haiku is
unusual, but that one provider learns from refusals and the other cannot.

### Both mechanisms, and what each is for

**A deny-list in front**, so the known case never costs even one failed turn:

```csharp
private static readonly HashSet<string> LegacyThinkingModels =
    new(StringComparer.Ordinal) { "claude-haiku-4-5" };
```

Follow `BasicWebSearchOnly`, whose doc comment already argues this exact case for the same
4.6-or-later family rule. Keep it a **separate** field even though membership is identical today —
two fields, two failure modes, and web search is also absent on models that do take an effort.

**A model-keyed demotion behind it**, so a model d47 has not heard of heals itself instead of
failing for ever. This is the half the amendment adds, and it is what makes an allow-list versus
deny-list argument stop mattering: the deny-list handles what is known, the demotion handles what is
not, and neither has to be right about the future.

**Key it on endpoint *and* model.** `EndpointDemotions` keys on endpoint alone, which is correct for
OpenAI — one endpoint, one server, one set of accepted fields — and wrong for Anthropic, where one
endpoint serves five models. A Haiku refusal must not switch effort off for Opus 5.

*Recommended shape:* lift the type to a namespace both providers can see and widen the key to
`(endpoint, model)`, with the OpenAI callers passing an empty model so their behaviour is byte-identical.
One mechanism with one set of semantics beats a twin that drifts. If that refactor looks like it will
reach further than it should, an Anthropic-side twin is acceptable — but say in its comment that it is
a twin and why, or the next reader will unify them without knowing what it cost.

### What "viable" needs beyond the two fields — checked, and neither is a blocker

**Caching still works, with less headroom than you would think.** `MinimumCacheablePrefix` gives
`claude-haiku-4-5` **4096** tokens against Sonnet's 1024 and Opus 5's 512, and its own comment warns
that below the minimum *"a prefix silently does not cache — no error, just no entry."* d47's
conversation prefix clears 4096 comfortably — the phase's own arithmetic assumes ~6k — so this is
noted rather than fixed. **It is worth a manual check on a real turn** anyway, because a silent
non-cache on the model chosen to be cheap would undo the whole point, and the spend ledger's warmth
column is where it would show.

**Live game state arrives under a weaker boundary.** `SupportsOperatorSystemMessages` lists
opus-5, opus-4-8, fable-5 and mythos-5 — **not Haiku 4.5**. Without it, `LiveGameState` is folded
into the last user message as a `<system-reminder>` instead of an operator system message, and the
provider's own comment says what that costs: an operator system message *"cannot be spoofed by
journal content, while a `<system-reminder>` in the user turn caches identically but is only a
convention."* Journal and in-game comms are untrusted input by invariant.

**This is not new and not a reason to refuse Haiku** — every ChatCompletions endpoint already
declares the flag false, so the fallback is well travelled. It is a reason to **say so where the
model is chosen**: recommending Haiku moves more traffic onto the weaker path, and a Commander
picking the cheap model should not have to read the provider to find that out. One sentence on the
`llm.model` row's documentation, not a warning dialog.

### The rest of the fix, unchanged from the approved plan

- `SupportsThinkingEffort = true` in the Anthropic capability block becomes
  `!LegacyThinkingModels.Contains(model)` — and then also honours the demotion, so the flag tells the
  truth after a refusal as well as before one.
- The unconditional `Thinking` / `OutputConfig` assignments become conditional on
  `capabilities.SupportsThinkingEffort`, which `BuildParameters` already computes. Omit **both**
  fields; do **not** substitute `budget_tokens` — that means inventing a token budget per rung on the
  model that exists to be cheap. Say so in the comment.
- `Translate` stays total and stays where it is. The gate is a property of the model.

**Report it, don't drop it silently.** `TurnEvent.Routed.Effort` and `TurnResult.Effort` are already
`ThinkingEffort?`, and null already renders with no effort clause in the window. In `TurnLoop`, where
`providerCapabilities` is already in hand:

```csharp
var effortReported = providerCapabilities.SupportsThinkingEffort ? effort : (ThinkingEffort?)null;
```

Use it for the reported values; leave the diagnostic log on the chosen value. This gives
`SupportsThinkingEffort` its **first reader in `src/`** — today it is assigned in three providers and
read nowhere.

**Uncertain:** whether `MessageCreateParams.Thinking` / `.OutputConfig` are nullable, and whether
null is omitted rather than serialised as `"thinking": null`. The wire test settles it in one run;
the fallback is two-branch construction.

## 2. `Xhigh`

Insert between `High` and `Max` — **declaration order is the ladder and the clamp depends on it**.
Ordinal churn is safe: settings serialise enums as camelCase strings, the spend ledger records no
effort, and there is no `(int)` cast on it anywhere in `src/`.

**Correct the doc comment on `ThinkingEffort` with it.** Its claim that the C# SDK's enum lacks
`xhigh` is **false** for the pinned `Anthropic 12.40.0`, where `Anthropic.Models.Messages.Effort` is
`{ Low, Medium, High, Xhigh, Max }`. Leaving it standing beside code that now sends `Xhigh` turns the
file into a liar.

- Anthropic `Translate`: add `Xhigh => Effort.Xhigh`. Keep `_ => Effort.High` as the guard.
- Both OpenAI `Translate`s: `Xhigh => "high"`, joining `Max`. Their doc comments say "four levels" —
  make it five. *Uncertain:* OpenAI may accept `"xhigh"` on GPT-5.6 today; mapping down is the safe
  default and can be raised against a real 200.
- **No `EffortRouter` case moves onto it.** The router keeps its four outputs; `Xhigh` is reachable
  only through the floor or the ceiling. `EffortRouterTests` is untouched, which is the evidence the
  change is additive.

---

## 3. Settings

Three properties on `LlmSettings`, appended after `WebSearch`:

```csharp
public string? BackgroundModel { get; init; }
public ThinkingEffort? EffortFloor { get; init; }
public ThinkingEffort? EffortCeiling { get; init; }
```

**`BackgroundModel`, not `MinModel`.** `Model` can never be renamed to `MaxModel` — the settings file
is append-only — and *min* implies a range that something picks *within*, which is what decision 1
rejects. The name should say the call class, because that is what the value selects.

Three rows in `ConversationCapability.BuildSettingRows`, inserted after the `llm.model` row. **No
`Group`**: the renderer resets on exit with no divider, so a group of three would leave the ungrouped
key rows below it sitting under a heading that does not cover them.

| Key | Kind | Notes |
|---|---|---|
| `llm.backgroundModel` | `Choice`, `AllowsFreeText` | Same `ChoiceSource` as `llm.model`. Placeholder `"(the conversation model)"`. `AppliesWhen` excludes `none`. Not Protected. No voice commands — a model id cannot be extracted from a closed phrase. |
| `llm.effortFloor` | `Choice` | **Set both `Choices` and `ChoiceSource`.** Open-vocabulary is `ChoiceSource != null && Choices.Count == 0`, so a source alone would turn a five-rung ladder into a search window. `ChoiceSource` truncates at the current ceiling. |
| `llm.effortCeiling` | `Choice` | Same, from the floor upward. Two voice commands: *"stop thinking so hard"* → medium, *"think as hard as you like"* → clears. `KeywordRouter.MatchSetting` compares the whole utterance, so there is no hijack risk. |

Both effort rows are `Install` scope. Neither is Protected, matching `llm.model` — **flagged**: if the
spend levers should be Protected, `llm.model` should change with them rather than just these.

### The amendment that fails in silence

`BackgroundModel` is a model id and belongs to the endpoint's namespace. Add `BackgroundModel = null`
to **both** clearing writes in `ConversationCapability` — the provider row's `Write` and the endpoint
row's.

**Highest-risk detail in the phase.** A stale background model after a provider switch produces a
400/404 that `FlavourTurn` logs at Debug and returns null from; every ambient line then quietly falls
back to its authored text with nothing on screen.

---

## 4. The clamp

**At the `EffortRouter.ChooseFor` call site in `TurnLoop`, from properties on `TurnLoop`** — not
inside `ChooseFor`. `TurnLoop` already receives every settings-derived value as a settable property
(`Model`, `Persona`, `WebSearchEnabled`), assigned by `AppHost.ApplyLlmSettings`. Follow `Model`.

This keeps two decisions with two owners apart — *what did the Commander ask for*, a pure heuristic,
against *what will they pay for* — and it leaves `EffortRouterTests` entirely untouched.

```csharp
var effort = ThinkingEffortRange.Clamp(EffortRouter.ChooseFor(input), EffortFloor, EffortCeiling);
```

`ThinkingEffortRange` is a small static beside the enum holding `Clamp`, the ordered ladder, and the
`Parse`/`Name` the row bindings use — so the clamp and the rows cannot disagree about spelling or
order.

**`Clamp` orders the bounds before applying them.** `Math.Clamp` throws when min exceeds max, and a
hand-edited settings file can produce exactly that. Core must never throw on a settings file.

The ceiling earns its keep twice over, because `EffortRouter` matches substrings with no word
boundaries: *"what do you think about"* hits `"think about"` and routes to Max.

**Background efforts are not clamped, deliberately.** They are call-site decisions with stated
reasons, and a floor of High would turn every ambient remark into a reasoning call — the exact
blow-up the floor *model* exists to prevent. Comment it; "clamp it everywhere" is the obvious later
tidy-up and it is wrong here.

---

## 5. The floor's consumers

One property on `TurnLoop` mirroring `Model`, resolved **once** in `AppHost.ApplyLlmSettings`:

```csharp
Turns.Model = current.Llm.Model;
Turns.BackgroundModel = current.Llm.BackgroundModel ?? current.Llm.Model;
```

A property means **zero signature changes** — not `FlavourTurn.AskAsync` (already at eleven
parameters), not `VoicePairing`, not `AdventureGenerator`. Each site is a one-token edit.

### Corrected 2026-08-25 against the current tree

The approved plan named eight floor sites and three ceiling sites. Re-checked when this document was
written, **there are eight and two**, and one of the three was wrong:

**Take the floor** (`Turns.Model` → `Turns.BackgroundModel`) — eight sites, all confirmed present:

- the gap reaction and the opening brief, two adjacent `FlavourTurn.AskAsync` calls
- the ambient re-speak inside `VaryAsync`
- the lore web search in `SearchForAsync`, and `LookUpLore`
- voice casting, three calls: `VoicePairing.ChooseOneAsync`, `VoicePairing.ChooseAsync`, and
  `WithReplacementsAsync` — a mechanical question about d47's own configuration, never spoken aloud,
  answered in a fixed format

**Keep the ceiling** — each with a comment saying why, because *"it uses `FlavourTurn`"* is exactly
the reason someone would later change it by accident:

- **Adventure generation.** Already asks Medium against 1500/4000/4500/4000-token budgets where every
  other caller asks Low/400. The Commander pressed a button and is waiting; the output must name real
  systems exactly, is validated, and is re-asked on refusal. **Note for whoever greps:** the
  `AdventureGenerator` construction reads it as a lowercase `() => turns.Model` lambda, so a search
  for `Turns.Model` misses it. That is precisely the accident this bullet exists to prevent.
- **The Commander's log.** The Commander is shown a price quote and agrees to it, and
  `LogbookBook.WriteAsync` refuses to write if the model changed between quote and write.

**Key verification is no longer on this list.** The approved plan kept it at the ceiling as a third
site. `VerifyLanguageModelKeyAsync` does not read `Turns.Model` at all — it lists the endpoint's
models and works with **no model selected**. Nothing to do; do not go looking for it.

**Flagged, not fixed:** the web-search capability check asks `CapabilitiesFor(Turns.Model)` while now
gating a lookup that runs on the background model. Correct today by accident — web search is
endpoint-gated in all three providers — but `ILlmProvider` says it is model-gated in principle. Note
it; leave the line alone.

---

## 6. Tests

**Revise:** `ProviderCapabilityTests.CachingEffortAndToolCallsHoldEverywhere` still passes (it tests
opus-5), but its doc comment *"protocol features rather than deployment ones"* becomes wrong. Move
effort into a per-model theory.

**Add:**

- ~~`WebSearchDeclarationTests` — `HaikuIsSentNoThinkingConfigAndNoEffort`, plus a
  `CurrentModelsAreSentBothOfThem` partner for opus-5. That file already builds a real Haiku request
  and looks only at tool names.~~ **Not done there, and the departure is recorded rather than
  silent.** Both tests went into `PromptOnTheWireTests` instead, where the partner is a theory over
  all four current models. The convenience argument for the other file did not survive contact:
  `D47.Llm.Tests` has the same `InternalsVisibleTo`, so it can build the same request — and it can
  additionally assert the **bytes**, which is what settles the omitted-versus-explicit-null question
  above. A weaker duplicate of an assertion that already exists, filed under a name about web
  search, would have bought nothing and made the file's title a lie.
- `PromptOnTheWireTests` — `NeitherThinkingNorEffortReachesAModelThatRejectsThem`, asserting the
  **bytes**, which also settles the omitted-versus-explicit-null question. Plus
  `[InlineData(ThinkingEffort.Xhigh, "xhigh")]` on `TheThinkingEffortIsSent` — that one fails at
  *compile* time if `Effort.Xhigh` does not exist, the earliest possible signal.
- A per-model effort capability theory, plus a separate
  `AModelD47HasNotHeardOfIsAssumedToTakeAnEffort`.
- **The demotion, added 2026-08-25.** A model d47 has not heard of that refuses the field is retried
  once without it and succeeds; the second turn sends no effort at all; and — the one that matters —
  **a refusal on one Anthropic model does not demote another.** That last is the whole reason the key
  carries the model, so it fails loudly if anyone narrows it back. Plus: the OpenAI callers'
  behaviour is unchanged by the widened key, asserted rather than assumed.
- `Xhigh` → `"high"` on both OpenAI providers. **Note the gap being closed:** there is currently no
  test asserting OpenAI effort reaches the wire at all.
- `EffortRangeTests` — both bounds unset changes nothing; floor lifts; ceiling lowers; equal bounds
  pin; **floor above ceiling does not throw**.
- Through `TurnLoop`: floor High plus a "where am I" input ⇒ `Routed` carries High and the request
  carries High.
- A `SupportsThinkingEffort = false` fake ⇒ `Routed.Effort` and `TurnResult.Effort` are null **and the
  request's effort is still the chosen rung** — that last clause stops a later "simplification"
  short-circuiting the request as well as the report.
- The floor reaches the right callers **and only them**. The negative half matters more.
- Provider switch and endpoint switch each clear `BackgroundModel` — §3's silent failure.
- "Nothing changed on upgrade": all three null ⇒ routed effort is exactly `ChooseFor`'s answer and
  `BackgroundModel == Model`. Decision 3, written down.

**Prove the test catches it** — the standing rule. Write both Haiku tests against the unmodified
provider, run them, **record the failure text**; apply the fix; re-run; then deliberately revert
`SupportsThinkingEffort` to `true` and confirm they fail again *for the right reason*. The obvious
wrong fix — omit the fields for everyone — makes the Haiku tests pass, and only the opus-5 partners
catch it.

**No test can prove** Haiku's real endpoint accepts the result. That is a manual item.

---

## 7. Docs

**No tool schema changes and no tool-surface bytes are spent.** `SettingsCapability`'s tools take a
free-text `key`; `ConversationCapability`'s two are parameterless. The docs gate cannot fire on schema
and the free bytes are untouched — worth stating, because "add a settings row" and "grow the tool
surface" are usually the same act and here they are not.

- `docs/capabilities/conversation.md` — three anchored subsections after `#### Model {#model}`;
  `SettingsServiceTests` requires a `DocsAnchor` per row. Amend `#### Model` by one sentence: this is
  the model **conversation turns** take.
- `docs/conversation.md` — *"What each turn reports"* and *"Effort is chosen per turn"*: five rungs,
  and the fifth is reached by setting a bound.
- `get_model_status` (`ConversationCapability.DescribeModel`) — one line, **only when the background
  model differs**, following the rule the endpoint line already uses.
- `list.md` Phase 54 is already written. Do **not** touch Phase 3's line — frozen; cite it.

---

## 8. Order, and what to verify

| Step | Verify |
|---|---|
| 0. Phase 54 into `list.md` + this plan of record | Both done 2026-08-25 |
| 1. **The Haiku defect alone, own commit** | **Built 2026-08-25.** Both directions proven against the unmodified tree — see below. Suite green: 4,732 tests. **The manual half has not been done**: pin `llm.model` to Haiku and hold a real conversation, watching the spend ledger's warmth column for the 4096-token cache floor. *Can ship as a patch ahead of the phase.* |
| 2. `Xhigh` into the enum and three `Translate`s | **Built 2026-08-25, shipped in v0.71.2.** No switch turned out to be non-exhaustive: every `Translate` ends in a `_ =>` arm, so the rung built silently and `TheEffortLadderTests` is what holds it |
| 3. `ThinkingEffortRange` + tests | **Built 2026-08-26.** `EffortRangeTests`, floor-above-ceiling included. `Math.Clamp` is not used and cannot be: its generic overload wants `IComparable<T>`, which an enum does not implement |
| 4. Three `LlmSettings` properties | **Built 2026-08-26.** Round-trip both ways, plus a file with none of the three keys loading as three nulls |
| 5. `TurnLoop` properties, the clamp, the reported effort, `ApplyLlmSettings` | **Built 2026-08-26.** A four-shape theory asserts that no bounds is exactly `ChooseFor`'s answer, and reverting the clamp fails the floor and ceiling tests while that theory stays green |
| 6. Point the eight background sites at `BackgroundModel` | **Built 2026-08-26.** The grep became a gate — `TheFloorReachesTheBackgroundCallsAndOnlyThemTests` reads `AppHost.cs`, ignores comments, and names the three readers of the conversation model — because there is nothing to observe at runtime |
| 7. Three rows + the two `Write` amendments | **Built 2026-08-26.** The anchor gate did **not** fail: nothing asserts an anchor resolves to a heading, only that a row declares one. Both amendments proven by reverting each |
| 8. Docs | **Written 2026-08-26.** Three anchored sections, the model row amended by a sentence, the general page at five rungs, and the status line |
| 9. By hand (`manual-test`) | **Owed.** Rows render correctly; the floor stops offering above the ceiling; switching provider clears the background model; an ambient remark actually goes to the floor model — visible in the spend ledger, which stamps model per entry. Item one's manual half rides with it: a real Haiku conversation with the warmth column in view |
| 10. `tools/release.ps1 minor` | **Owed — v0.74.0.** A completed phase is a minor. The CHANGELOG section is written and waiting |

---

## Open

1. ~~Whether `MessageCreateParams.Thinking` / `.OutputConfig` are nullable and omit on null.~~
   **Settled 2026-08-25, both yes.** Both properties are nullable and a null is omitted from the
   JSON rather than serialised as `"thinking": null` — asserted as absent keys by
   `NeitherThinkingNorEffortReachesAModelThatRejectsThem`, so no two-branch construction was
   needed. One wrinkle worth recording: `Thinking` is a union type that converts implicitly from
   the concrete config but **not** from null, so the null branch of the ternary needs an explicit
   `(ThinkingConfigParam?)` cast or the build fails on nullability. `OutputConfig` does not.
2. Whether OpenAI accepts `"xhigh"` today. Mapping down is safe; raising it needs a real 200.
3. Whether the effort rows should be `Protected`. Recommending no, matching `llm.model` — but that
   precedent may itself be the gap, and if it changes, both change.
4. The web-search capability check now gates a lookup running on a different model. Correct by
   accident; see §5.
5. Whether `get_model_status` grows a background-model line. Leaning yes, conditionally; the easiest
   item to drop if the phase runs long.
6. ~~**Added 2026-08-25:** whether the shared demotion type is lifted into a common namespace or twinned
   on the Anthropic side.~~ **Settled 2026-08-25: lifted, and the refactor reached nothing.** It was
   measured before it was chosen, which is what the recommendation asked for. `EndpointDemotions.cs`
   moved from `src/D47.Llm/OpenAi/` to `src/D47.Llm/` and its namespace changed by one line;
   **not a single call site moved.** `D47.Llm.OpenAi` and `D47.Llm.Tests` are both child namespaces
   of `D47.Llm`, so every existing reference resolves without so much as a `using`. The widened key
   is a trailing `string model = ""` on all three methods, so the fifteen OpenAI call sites read
   exactly as they did and mean exactly what they did.
   <br><br>
   **The one thing not to tidy:** the model parameter is *last*, after `Demotable`, which reads
   slightly oddly. That is deliberate — it is what makes the OpenAI callers byte-identical instead
   of a fifteen-site rename, and reordering it to `(endpoint, model, what)` would buy nothing but
   churn.
7. **Added 2026-08-25:** whether the `<system-reminder>` fallback deserves more than a documentation
   sentence now that a cheap model is being recommended rather than tolerated. Recommending no — it is
   already every ChatCompletions endpoint's normal path — but it is a trust boundary, so it is written
   down rather than assumed.

---

## Not part of this phase

**The checklist change request (2026-08-23)**, to be added as its own entry rather than fixed here:

> All checklist items fulfillable by the Engineer should appear for that filter, not just for the
> ship I'm in. If that's undoable, then at least notice when I switch ships and re-filter for the new
> ship — but I'd rather the previous bullet be implemented instead.

**The voices topic (2026-08-23)**, queued for discussion: multilingual ethnic voices matching NPC
names where appropriate, which requires choosing an appropriate ElevenLabs model when ElevenLabs is
the provider.

**Standing instruction (2026-08-23):** do not begin implementation after plan approval, including in
auto mode, until explicitly told to start.
