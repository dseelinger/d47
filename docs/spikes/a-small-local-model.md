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

- **Whether the machine can run it while Elite is running.** A 12B at Q4 next to Elite Dangerous
  on one GPU is a different question and wants measuring on the real machine, not inferring.
- **Whether a local model can hold a conversation.** These eight sites are the *background* class.
  The conversation model is 74 tools, history, a warm cache and a Commander waiting — nothing here
  says anything about it.
- **Anything about egress.** A local model sends nothing anywhere, which is Phase 59's whole
  point, and needs no measuring.
