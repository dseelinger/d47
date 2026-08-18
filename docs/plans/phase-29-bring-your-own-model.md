# Phase 29 — Bring your own model

The plan of record for list.md Phase 29. Written 2026-08-18, **before any code**, and before the
phase is due — Phases 27 and 28 come first. (It was written the same day Phase 22 was cut, and the
sequencing note in [build-order.md](build-order.md) still reads against it, because what was decided
was that this phase does not jump the queue rather than which phase happened to be at the front of
it.)

`list.md` reads top to bottom as a description of the product. This is the order the work happens
in, and the reasoning the order cannot carry on its own.

---

## Most of this was decided in Phase 1 and then not built

That is unusual enough to state plainly, because it changes what the phase is. `architecture.md` §6
has said from the beginning that **two implementations ship**, that the seam exists so an
OpenAI-protocol endpoint is *"a first-class peer rather than a port"*, and that the seam owns
tool-schema translation and reports what an endpoint supports into *Capabilities as state*. The
interfaces were written to that description and the description held:

- `ILlmProvider.Id` is documented as *"the stable identifier used by the price table — `anthropic`,
  `openai`"*. The second half of that sentence has been a promise for twenty-eight phases.
- `LlmProviderInfo.AcceptsCustomEndpoint` exists, and its comment says outright that **the providers
  it exists for are the OpenAI-shaped ones**, where the same protocol is spoken by a dozen
  implementations and the endpoint is how you choose between them.
- `ModelsFor(endpoint)` already returns nothing for an address d47 does not know, and the model row
  already accepts free text, so the picker degrades correctly on day one.
- `LlmProviderCapabilities` already carries five flags and already treats a missing feature as a
  capability that is off rather than as a failure to handle.
- The endpoint, model and key rows are generated from the catalog, each declaring when it applies.
  **The settings surface costs nothing.** A second entry in `LlmProviderCatalog.All` draws its own
  rows.

So this phase is one provider project's worth of wire code, one catalog entry per protocol, and —
the part worth planning — **four seams that are Anthropic-shaped, say nothing about it, and would be
wrong quietly rather than loudly.**

## The four calls, settled 2026-08-18

**1. Language model only.** The same endpoints serve `/v1/audio/transcriptions` and
`/v1/audio/speech`, and a Whisper-compatible server would offload the GPU cost the STT row already
warns about in VR. It is still a different seam with a different catalog, a different key row and a
different egress entry. A phase is a minor release, and one that cannot be finished holds its ready
items hostage.

**2. Two catalog entries rather than one pointed elsewhere.** `Egress` is one string per provider,
and no single string can say both *everything goes to OpenAI* and *nothing leaves this machine*. It
splits the secret as well, which is correct on its own terms — an OpenRouter key is not an OpenAI
key — and it splits the price rows, which matters because one set is published and the other cannot
exist.

**3. Responses for the OpenAI entry, Chat Completions for the compatible one.** The reasoning is web
search, and it moved recently enough to be worth recording. Server-side search is now a **named
entry in the tools array** everywhere it exists — OpenAI's `web_search`, xAI's `x_search` and
`web_search`, OpenRouter's `openrouter:web_search` — so the protocol can express it and the
*address* decides whether it is reachable. xAI deprecated `search_parameters` on Chat Completions on
2026-01-12 and moved its search tools to `https://api.x.ai/v1/responses`; OpenAI's is likewise a
Responses tool. Chat Completions therefore reaches OpenRouter's search and nothing else.

That is what makes the second decoder earn its place rather than being protocol tourism:
**Responses is where the hosted vendors keep search, and Chat Completions is where every local
server lives** — and no local server has server-side search anyway. The split lands exactly on the
catalog split from call 2, which is the sign it is a real seam and not a preference.

**4. Hand-rolled HTTP and SSE, not the official SDK.** `OpenAI` 2.13.0 is MIT, targets `net10.0`,
supports streaming, tools, reasoning effort and a custom endpoint, and its transitive graph is
Microsoft packages that would pass the licence gate. It was still declined. The Edge and ElevenLabs
voices are the precedent for hand-rolling a provider in this repo, the protocol is one POST and a
stream of `data:` lines, and **a strongly-typed client is precisely where tolerance for a server
that deviates goes to die** — the compatible half of this phase exists to talk to implementations
that get details wrong. The cost is owning the request and response shapes, which is also the
benefit: the wire is what the tests assert on, and `RecordedEndpoint` is already a raw TCP listener
rather than anything that knows about Anthropic.

---

## The four seams that are Anthropic's and do not say so

Each is wrong silently. None throws, none logs, and three of them produce a plausible number.

### 1. Usage accounting is inverted

`LlmUsage.TotalInputTokens` sums input, cache writes and cache reads, and its comment is right about
why: *"reading `InputTokens` alone under-reports a cached turn substantially."* That is true because
Anthropic's `input_tokens` **excludes** the cached part.

OpenAI's `prompt_tokens` **includes** `prompt_tokens_details.cached_tokens`. The same sum then
counts the cached tokens twice, on every cached turn, forever.

The seam normalises to the existing convention rather than the type growing a mode:
`InputTokens` becomes `prompt_tokens` less `cached_tokens`, `CacheReadInputTokens` becomes
`cached_tokens`, and `CacheCreationInputTokens` is zero. A test asserting that one known usage block
prices identically under both providers is the guard, and it should be watched failing before it is
kept.

### 2. Cache economics are Anthropic's, and are derived rather than declared

`ModelPrice` lists two numbers per model and computes the other two: `CacheWritePerMillion` is
input × 1.25, `CacheReadPerMillion` is input × 0.1. Its comment explains the choice well — quoting
them separately would be two more numbers to keep in step with the first — and the choice is correct
for one provider.

OpenAI charges **nothing** to write a cache entry, and discounts reads by its own factor. Left
alone, every OpenAI turn is mispriced upward: d47 would invoice a cache write that does not exist.

These become per-row values with the current pair as the default, so no Anthropic row changes and no
existing test moves. **The published rates are read at implementation time, not written from memory
now** — a price table with invented numbers is worse than no entry, because the unpriced path
already exists and is honest about itself.

### 3. The cold-prefix detector cannot fire, and would read as good news

`SpendTracker.UnexplainedColdPrefixes` counts turns that **write** cache with no sanctioned cause,
and it is the regression signal list.md Phase 3 asked for: a profile switch is the only legitimate
reason for a cold prefix, so anything else means caching is being defeated by non-deterministic
schemas or a mutated descriptor.

A provider that never reports a cache write cannot trip it. The counter would sit at zero on OpenAI
and read as *caching is perfect* rather than as *this instrument is not measuring here*, which is
the more expensive of the two failures, because nobody investigates good news.

The analogue is the inverse signal: a turn reporting **no cache read** when the prefix did not
change. Whichever is implemented, the requirement is that the counter never silently means something
different depending on who answered.

### 4. A key is required by construction rather than by decision

`NeedsKey` is derived from whether a secret name exists (`LlmProviderCatalog.cs:51`), and
`ApplyLlmSettings` only builds a provider inside a successful key resolution (`AppHost.cs:1422`),
falling through to *"No API key is stored."* The same assumption sits in `FirstRun.IsNeeded` and in
the disclosure's usability test (`EgressDisclosure.cs:242`).

Ollama has no key. llama.cpp has no key. **The most private configuration d47 can offer is currently
unreachable, and not because anybody decided it should be.**

`LlmProviderInfo` gains a flag for a key that is accepted but not required — the row still exists,
because a gateway may want one — and the four call sites read it.

### And a fifth, which is only bookkeeping

`AppHost.cs:1424` and `AppHost.cs:3376` both switch on the provider id to construct one, both end in
a null default, and both then say *"D47 has no client for {Name} yet."* Two is a coincidence; three
is drift. One factory, called from both.

---

## The order, and why

**1. Usage normalisation and the price table.** First, Core-only, before either provider exists. It
is the contract two implementations will depend on, it is the thing that goes wrong without
complaining, and it is cheap now and expensive once a ledger holds numbers computed the old way.
Phase 26 made the same call for the same reason — *before anything writes a plan to disk*.

**2. The optional key, and the factory.** Also before either provider, and for a practical reason as
much as a structural one: it is what makes step 3 testable against a local model with **no account,
no key and no bill**.

**3. The Chat Completions provider, and the compatible catalog entry.** First of the two decoders
despite being second in list.md, because it can be driven for free against Ollama for as many turns
as it takes, and because it is the simpler shape. Everything it builds — message translation, tool
translation, tool-call assembly across deltas, stop-reason and failure translation — is what step 4
reuses.

**4. The Responses provider, and the OpenAI catalog entry.** Reuses step 3's translation layer and
diverges on the request body and the stream event names: `response.output_text.delta`,
`response.function_call_arguments.delta`, `response.completed`. **The event names and field shapes
come from a capture, not from memory** — the repo already has the instrument for that, a local
listener with a dummy key, and this is exactly what it is for.

**5. The handshake, the model list, and capability demotion.** After both providers, because it
needs both before there is anything to demote.

**6. The disclosure, the documentation page and the settings copy.** Last, and not optional: every
registered capability needs a page and CI enforces it.

Two traps sit inside steps 3 and 4 rather than being steps of their own, and both are worth naming
before they are met. Streamed Chat Completions returns **no usage block at all** unless it is asked
for, and a missing usage block priced as zero reports a paid session as free — strictly worse than
the unpriced path, because `Priced` would be true. And the byte-identical-schema invariant applies to
both new wire shapes: Chat Completions nests a tool under `function`, Responses is flat, and both
must serialise with a stable key order or prompt caching dies exactly as it would on the Anthropic
path.

---

## Decisions this plan makes that list.md does not

### The catalog ids, and therefore the settings keys

`openai` and `openaiCompatible`. The key rows are then `llm.openai.apiKey` and
`llm.openaiCompatible.apiKey`, which matches the existing `llm.webSearch` in casing and keeps a
hyphen out of a settings key. **These names are permanent once written**: the settings file is
append-only and a property is never renamed or removed — a repair needs a revision and a
replacement, which is a far worse trade than picking the name carefully now.

### What "verify" means when there is no key to verify

The key row's verification hook currently sends a one-token turn and reports *rejected* or
*unreachable*, keeping those apart deliberately: telling a Commander their key is wrong when the
machine is offline sends them to their account page for another one that will also fail. That
distinction is the right one and it survives untouched.

Two things change. For a keyless endpoint the question is not *is this key good* but **can I reach
this address and does it speak the protocol**, which the model list answers. And the existing probe
sends a one-token output budget, which a reasoning model spends entirely on reasoning or rejects
outright — so that budget is raised on this path rather than the failure being reported as a bad
key.

### Demotion is once per capability per endpoint, and is not remembered

Advertise the capability, and turn it off for that endpoint when the endpoint says no — a rejection
names the field it rejected — retrying the turn once without it. **Once, and for the session only.**
A retry policy that searches for a working request shape is indistinguishable from an outage from
the Commander's seat, and a demotion persisted to disk outlives the server upgrade that fixed it.

This is the reading of `SupportsToolCalls`'s existing rule — *advertising a tool the turn loop would
silently drop is worse than not offering it, because the model then tells the Commander it has done
something that never happened* — that survives contact with an endpoint nobody has tested. That rule
is about d47 dropping a call it asked for, which stays forbidden. A server refusing the declaration
outright is visible instead, and the demotion is what answering it looks like.

### A loopback endpoint is priced at zero, and says why

An unknown model stays unpriced, which is the existing behaviour and the honest one. A model served
from `127.0.0.1` is not unknown — it is free, and reporting *unknown* about it on every turn forever
is noise pretending to be rigour. The host is evidence rather than a guess.

### What does not change

`ModelsFor(endpoint)` keeps returning nothing for an address it does not recognise. That decision is
right — a model id belongs to its endpoint's namespace, and carrying one across is a stale selection
waiting to fail at the first turn — and the handshake does not overturn it. What changes is that
there is now something else to ask: the list becomes **the endpoint's own**, rather than this
provider's or nothing.

---

## The trust boundary widens, and needs no new machinery

Worth stating rather than leaving to be noticed. `architecture.md` §7 enumerates four sources of text
d47 did not author, and all four are content arriving *through* a trusted provider. This phase adds a
fifth kind: **the provider itself may be a stranger**. A Commander can point d47 at any address, and
whoever runs it can emit tool calls directly rather than having to talk a model into emitting them.

Nothing needs building for that, and the reason is the invariant written for a hostile in-game
message: the protected set is unreachable from the tool surface **entirely**, and protection is a
property of the caller rather than of the modality. The panel, a hotkey and the model-free keyword
router reach those rows; nothing arriving over the wire does, whoever is on the other end of it.

So pointing d47 at a stranger's gateway is a decision the Commander gets to make, rather than a
hole. The §7 amendment records the widened attacker set and the sentence above, and changes no code.

---

## Not in this phase

- **Speech.** Whisper-compatible transcription and OpenAI-shaped voices are a later phase against
  `ISttProvider` and `ITtsProvider`, which are separate seams with separate catalogs.
- **Embeddings, and anything stateful.** The Responses API can hold conversation state server-side.
  d47 assembles its own prompt by volatility and that ordering is the whole of its caching strategy;
  handing history to a provider would trade it away for nothing.
- **Mid-conversation tool changes.** Still Anthropic-only and still an optimisation on one provider,
  which is exactly why the profile enumeration stays.
- **Any change to the Anthropic provider.** If a shared abstraction wants extracting, it wants
  extracting after the second implementation exists and has said what is actually shared.

## What would change this plan

- **Open Responses arriving evenly in the local servers.** vLLM, Ollama and LM Studio have all
  signed the specification, and if support lands the compatible entry could follow the OpenAI entry
  onto Responses and one decoder could be retired. That is a later simplification rather than a
  reason to wait: Chat Completions will be spoken by something for years.
- **OpenAI moving search onto Chat Completions**, which would collapse call 3 and make this a
  one-decoder phase.
- **Somebody other than the maintainer running a local model.** The compatible entry is ranked on a
  guess about how many Commanders want one, and a real user reorders the two halves.
