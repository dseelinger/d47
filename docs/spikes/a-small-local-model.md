# Spike: can a small local model hold a Guardian voice?

**Prepared 2026-08-26, not yet run.** Everything below is set up so the Commander only has to pull
weights and listen — the harness, the prompt set, the scoring sheet and the decision rule are all
written down in advance, which is the point: a spike scored after the fact is a spike that agrees
with whatever was hoped for.

Issue: [#55](https://github.com/dseelinger/d47/issues/55). Downstream of it:
[#56](https://github.com/dseelinger/d47/issues/56) (a call class choosing its provider) and
`list.md` Phase 59 (a voice that never leaves the machine).

---

## Nothing needs building to run this

Phase 29 already shipped `openaiCompatible`. The endpoint row **starts** at
`http://127.0.0.1:11434/v1` — Ollama's default — `docs/capabilities/conversation.md {#endpoint}`
documents LM Studio and vLLM beside it, and *"use my local model"* is already a spoken phrase.

So this is **configuration and listening**, not code.

```text
ollama pull qwen3:4b
ollama serve
```

Then in Settings → Language model: provider `openaiCompatible`, endpoint
`http://127.0.0.1:11434/v1`, model `qwen3:4b`. No key.

---

## The question is voice, not correctness

The eight `FlavourTurn` floor sites named in
[phase-54-a-floor-and-a-ceiling.md](../plans/phase-54-a-floor-and-a-ceiling.md) need **no tools**,
carry **no conversation history**, already declare `coldPrefixExpected: true`, and cap output at
**400 tokens**. Nobody waits on them and a null return falls back to the authored line. Every
reason a small model normally fails is absent here.

What is left is the thing a small model is genuinely worse at: **holding a character**. A 4B that
is factually fine and tonally flat would *invert* the point of the feature — those lines exist to
have personality, and the authored fallback never breaks character.

**So the question is not "does it work". It is "does it still sound like the ship's AI".**

---

## The split the spike must not collapse

The eight sites are **two different jobs**:

| | Sites | What it is |
|---|---|---|
| **Creative voice** | gap reaction, opening brief, the ambient re-speak in `VaryAsync`, two lore lookups | Prose in a named Guardian voice, with the persona block and the Commander's story in context |
| **Mechanical** | `VoicePairing.ChooseOneAsync`, `ChooseAsync`, `WithReplacementsAsync` | *"A mechanical question about d47's own configuration, never spoken aloud, answered in a fixed format"* |

**The hypothesis worth testing is that a small model passes the mechanical three and fails the
creative five.** If that is what happens, the useful split is not by model size at all — it is by
job type, and three sites could go local today while five stay where they are.

A spike that sampled "ambient lines" only would miss that entirely, so **both jobs are scored
separately below and the result is never averaged across them.**

---

## Which models, and why the obvious shortlist is wrong

**Do not shortlist by tool-calling benchmarks.** `FlavourTurn` advertises **zero tools**. The
models that top BFCL are tuned for the one thing this path does not do, and ranking by
function-calling accuracy selects for an unused skill that, if anything, trades against the one
that matters.

**Rank by prose in character.** Three axes, so the result says *why* rather than only *which*:

| Axis | Candidates | What it answers |
|---|---|---|
| **Size** | ~1.7B · **Qwen3 4B** · a ~12B such as **Mistral Nemo** | Is this a size problem at all? |
| **Family** | **Qwen3 4B** vs **Gemma 3 4B** | Same size, different house — is it the house? |
| **Tuning** | a general instruct model vs a **prose/roleplay fine-tune in the 8B class** | Is it the tuning rather than either? |

**Q4_K_M throughout** — the reported production floor, with harsher quantisation degrading
reliability noticeably.

**If the tuning axis wins, the finding is *"pick for tuning, not size"***, which is a far cheaper
conclusion than *"you need a bigger model"*.

d47 ships no weights, so a model's licence is the Commander's business — but if the docs end up
naming one, prefer permissive weights.

---

## The prompt set

Five creative and three mechanical, each run **three times** so a single lucky sample cannot carry
a verdict. Run every model against the identical set, with the **same persona** (the shipped
default) and the same About Me.

### Creative — score these by ear

1. **Ambient, in system** — sitting in supercruise, nothing happening. What d47 says unprompted.
2. **Ambient, after a hard landing** — the same, with hull damage in the game state.
3. **Opening brief** — the first thing said on sitting down, with a route plotted.
4. **Gap reaction** — returning after eleven days away.
5. **Lore lookup** — a remark on arriving somewhere with history.

### Mechanical — score these as right or wrong

6. **Voice casting, one core** — given thirteen voice descriptions and one persona, pick one.
7. **Voice casting, all cores** — the same for eleven personas at once, no duplicates.
8. **Voice replacement** — one voice withdrawn; pick a replacement for the cores that had it.

---

## The scoring sheet

Fill one row per model. **Score the creative five by ear and the mechanical three by result** —
never one number across both.

| | Qwen3 4B | Gemma 3 4B | 1.7B | Mistral Nemo 12B | 8B prose tune |
|---|---|---|---|---|---|
| **In character?** (0–3) | | | | | |
| Broke character at all? (y/n) | | | | | |
| Said "as an AI" or similar? (y/n) | | | | | |
| Invented a game fact? (y/n) | | | | | |
| Length sane, ≤400 tokens? (y/n) | | | | | |
| **Mechanical: 3 of 3 correct?** | | | | | |
| Format honoured exactly? (y/n) | | | | | |
| Seconds per line (median) | | | | | |

**In character, 0–3.** 3 = would not have known it was not the paid model. 2 = recognisably the
core, a little flat. 1 = generic assistant wearing the name. 0 = broke character, refused, or
narrated itself.

### The decision rule, written before the run

- **Any break of character, on any of the five, at any size → that model fails the creative half.**
  The authored fallback never breaks character, so a model that does is worse than no model.
- **Mechanical passes at 3 of 3 with the format honoured.** Two of three is a fail: the three sites
  it would serve are answered in a fixed format that something downstream parses.
- **If the smallest model passing the mechanical three fails the creative five**, the finding is
  the job-type split, and [#56](https://github.com/dseelinger/d47/issues/56) is what implements it
  — not a model setting.
- **If nothing under 12B holds a voice**, the honest answer is that Phase 59 buys privacy at a
  quality cost, and that trade belongs to the Commander rather than to a default.

---

## What this spike does not answer

- ~~**Whether the machine can run it while Elite is running.**~~ **Answered 2026-08-26 — see §"The
  headroom, measured" below. It was the first thing the run actually settled, and it settles the
  VR surface against the phase.**
- **Whether a local model can hold a conversation.** These eight sites are the *background* class.
  The conversation model is 74 tools, history, a warm cache and a Commander waiting — nothing here
  says anything about it.
- **Anything about egress.** A local model sends nothing anywhere, which is Phase 59's whole
  point, and needs no measuring.

---

## Two corrections to the text above, found on the first attempt to run it

**The gap reaction needs thirty days, not eleven.** `PersonaHost.GapAfter` is
`TimeSpan.FromDays(30)`, and the arrival ladder only reaches `PersonaArrival.Gap` for a core the
Commander **selects** — a startup adoption is silent. So prompt 4 is provoked by backdating that
core's entry in `coresLastAboard` in `data/view-state.json`, restarting, and then selecting it.

**The scoring sheet needs a blank tally, and without one the spike scores the wrong thing.** See
below: a thinking model returns *nothing* rather than flat prose, `FlavourTurn` answers null, and
d47 falls back to the authored line silently. A blank read as prose is a model being credited with
Anthropic's writing.

---

## The 400-token cliff, measured 2026-08-26

`FlavourTurn.AskAsync` caps output at **400 tokens** and there is no settings row for it — only the
adventure generator passes anything else. **A reasoning model spends that budget thinking and
returns an empty string.**

Measured against `qwen3:4b` through Ollama's `/v1` shim, one user turn, no system prompt:

| Cap | Runs | Result |
|---|---|---|
| 2000 | 306, 336, 343 tokens | content every time, `finish=stop` |
| **400** (d47's real cap) | 4 runs | **2 returned empty**, 2 returned 74 and 28 characters |

So the model sits *on* the boundary rather than clear of it. The reasoning lands in a separate
`reasoning` field through the shim, so nothing leaks into a spoken line — the failure is silence,
not gibberish, which is the harder one to notice.

**Thinking cannot be switched off from d47's side.** Three attempts, all measured:

| Attempt | Result |
|---|---|
| `chat_template_kwargs: {"enable_thinking": false}` via `/v1` | ignored — 400 tokens, blank |
| `/no_think` in a system message | ignored — 400 tokens, blank |
| native `/api/chat` with `think: false` | **worse** — the reasoning moves into `content` |

`qwen3:8b` is worse and more variable: 234, 335, **573** tokens across three runs. `llama3.1:8b`
does not think at all — 46 tokens, `finish=stop`, a clean in-character line first try — and is
therefore the model to start a *tone* comparison with, whatever the size axis says.

---

## The headroom, measured 2026-08-26 — and this is what decides the VR surface

Hardware: **RTX 5080, 16 GB**. Elite Dangerous in VR through SteamVR, flying around a Coriolis
station with traffic, `qwen3:4b` resident at 3.2 GB:

```
memory.used 15,211 MiB     memory.free 767 MiB
```

Elite and the compositor therefore hold roughly 12 GB between them. **That split is inference, not
measurement** — on consumer WDDM drivers `nvidia-smi --query-compute-apps` returns `[N/A]` for
per-process VRAM, so only the total is a fact.

767 MiB is less than Elite's own allocation spikes when it jumps, drops into a new instance or
loads a station interior. And a busy station is **not** the peak: on-foot in an Odyssey settlement
runs higher, so this is close to the *best* free memory available while actually flying.

**The finding: 16 GB does not hold Elite, SteamVR and a local model with room for Elite's own
peaks.** For the VR surface, Phase 59 buys privacy at the cost of flying, which is not a trade a
Commander would take — and that outranks anything the tone comparison could have said.

### What is still open, and it is not small

**The desktop surface was not measured.** The compositor and the doubled framebuffer are a large
share of that 12 GB and neither exists on the flat-screen path, which plausibly frees 4–5 GB. The
same three readings without SteamVR running are a different question, and Phase 59 may well survive
there. Worth taking on a night nobody is in the headset.

### One thing that is not evidence

`EliteDangerous64.exe` crashed during this session, followed by a *Mauve Adder*. **It is not a
result.** Three models were cycled through that GPU in ninety seconds by an agent benchmarking the
token cliff above — the last load finished at `23:18:12`, sixty seconds before the crash — which is
nothing like d47's actual usage of one resident model answering every few minutes. The headroom
figure stands on its own; the crash does not, and is recorded here only so nobody later reads it as
one. A repeat needs `OLLAMA_KEEP_ALIVE="-1"`, or the reading catches a model mid-eviction and
reports headroom that is really just the model leaving.
