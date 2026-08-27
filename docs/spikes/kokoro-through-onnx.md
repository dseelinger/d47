# Kokoro through the ONNX path

**Run 2026-08-27, from C#, on the machine d47 runs on.** The question
[#101](https://github.com/dseelinger/d47/issues/101) says must be answered before the phase is
designed: *"Kokoro's voice blending through the ONNX path is measured before anything is designed
around it."*

Measured with `spike/KokoroProbe`, against `Microsoft.ML.OnnxRuntime` **1.29.0** — the same version
[per-role-voice-providers.md](../plans/per-role-voice-providers.md) §1.2 downloaded and read, so the
licence finding and the measurement describe one artifact.

**In one line: the model half is better than the plan hoped, and the phase is gated on something
neither document mentions — Kokoro cannot be handed text, and the words d47 exists to say are
exactly the ones no pronunciation dictionary contains.**

---

## 1. Blending is real, continuous, and needs no wrapper

**The headline question, and the answer is yes.** A voice file is `522,240` bytes = **510 buckets of
256 floats**, the bucket chosen by token count. A blend is a weighted average of two of those vectors
before inference — plain arithmetic on a `float[256]`, no library in the middle.

It is not merely *accepted*. The audio moves, monotonically, on every axis, blending
`af_heart` (American female) into `bm_george` (British male) on one fixed line:

| t | seconds | RMS | spectral centroid | pitch |
|---|---|---|---|---|
| 0.00 | 2.40 | 0.0678 | 4266 Hz | 188 Hz |
| 0.25 | 2.58 | 0.0710 | 3426 Hz | 180 Hz |
| 0.50 | 2.67 | 0.0757 | 2890 Hz | 166 Hz |
| 0.75 | 2.80 | 0.0808 | 2707 Hz | 168 Hz |
| 1.00 | 2.98 | 0.0857 | 2589 Hz | 156 Hz |

**That is the finding that matters**: the model responds to the interpolated vector rather than
snapping to whichever end dominates. Duration, brightness and pitch all move together and in the
direction the two endpoints imply. Cartesia's speed control was measured the same way in
[cartesia-voices-and-speed.md](cartesia-voices-and-speed.md) and *failed* that test — accepted and
ignored. This one passes it.

### Listened to on 2026-08-27, and the ears found the rule the numbers had already written down

The Commander's verdict on the clips above: **"they all sound like people, but a couple are very
non-binary — somewhere between male and female."**

**Both halves matter.** *Sounding like people* is the thing that could not be measured and is the
answer to whether a blend is usable at all: it produces a coherent voice, not two voices at once and
not an artifact. **And the androgyny is not a defect** — it is what interpolating across a gender
boundary *is*, and the table above predicted it without anybody noticing: `t=0.5` measures 166 Hz,
between the two endpoints, which is exactly where "between male and female" lives.

So the follow-up was to blend *within* a gender, and that settles the design rule:

| blend at `t=0.5` | pitch | centroid |
|---|---|---|
| `af_heart` alone | 187.9 Hz | 4266 |
| **`af_heart` × `bm_george`** — across gender | **166.0 Hz** | 2890 |
| `bm_george` alone | 156.3 Hz | 2589 |
| **`af_heart` × `af_bella`** — two American women | **184.9 Hz** | 4267 |
| **`am_michael` × `am_onyx`** — two American men | **100.2 Hz** | 2516 |
| **`af_heart` × `bf_emma`** — across *accent*, same gender | **179.8 Hz** | 4315 |

**Blend within a gender and the result stays in it** — `af × af` lands beside `af_heart` itself.
**Blend across accent and it also stays put**, which is the more useful axis: it is a new voice that
still reads as a specific person. **Blend across gender and you get androgyny**, which is a choice
rather than a failure — and plausibly the right one for a Guardian core, which is not a person and
has never been claimed to be. That is the Commander's call, not the layout's.

**A caveat about the instrument, stated because the table invites over-reading it.** The pitch figure
is the strongest FFT peak in a 70–320 Hz band over the whole clip, which is crude: `bm_george`
*alone* measures 156 Hz and is plainly a man. **The ordering is trustworthy and the absolute
boundaries are not** — the finding rests on the listening, and the numbers only explain it.

## 2. The interface, from C#

```
inputs   input_ids  Int64   [1,-1]      output  waveform  Single  [1,-1]
         style      Single  [1,256]
         speed      Single  [1]
```

24 kHz mono float. d47's arbiter wants 48 kHz, so the resample is a doubling — **the same shape
Phase 60 shipped for Cartesia and which no measurement had ever covered**. Worth reusing rather than
re-deriving.

## 3. Quantisation costs speed here, which is the opposite of the assumption

Measured on this machine, median of 7 runs after a warm run, CPU only:

| variant | size | short line (2.4 s) | comms line (6.75 s) | realtime |
|---|---|---|---|---|
| **fp32** | 310 MB | 325 ms | 747 ms | ×9.0 |
| **fp16** | 155 MB | 476 ms | 1002 ms | ×6.7 |
| **q8f16** | 82 MB | 978 ms | **3200 ms** | ×2.1 |

**The smallest model is the slowest by 4×.** "Quantise it to fit in the installer" is available and
costs latency rather than saving it. At 82 MB a six-second comms line takes 3.2 seconds to
synthesise, which is not a voice that can answer a hostile Commander in local chat.

The trade is real either way: d47's installer is **70 MB today**, so fp32 would multiply it by five.

## 4. The gate: Kokoro takes IPA, not text

**The tokenizer is 115 symbols and every one of them is a phoneme, a stress mark, or punctuation.**
There is no text path. Whatever speaks through Kokoro must do its own grapheme-to-phoneme step —
and today d47 has none, because every provider it ships does that internally and invisibly.

**The usual answer is barred.** Kokoro's reference pipeline phonemises with `misaki`, which falls
back to **espeak-ng** — GPL, which CLAUDE.md forbids outright — and the whole stack is Python, which
d47 does not ship.

### What was tested instead

[OpenPhonemizer ONNX](https://huggingface.co/lookbe/open-phonemizer-onnx) — **BSD-3-Clause-Clear**,
built on DeepPhonemizer (**MIT**), described by its own authors as *"a drop-in replacement for the
GPL-licensed espeak"*, and it runs under the runtime Kokoro already needs. Both licences are
permissive and pass CLAUDE.md.

It ships two things, and they are not equally good.

**The dictionary works, exactly.** 274,927 entries of correct IPA, already in Kokoro's own symbol
set. The whole road runs from C# with no Python and nothing GPL:

```
Commander, your shields are holding but not for long.
  -> kəmˈændɚ, jʊɹ ʃˈiːldz ɑːɹ hˈoʊldɪŋ bˌʌt nˈɑːt fɔːɹ lˈɔŋ.
  -> 3.45 s of speech, g2p 220 ms, synth 476 ms
```

**The neural fallback does not.** Scored against the dictionary's own answers on 400 random words —
words the model was almost certainly *trained* on, so this is an upper bound rather than an estimate:

| | |
|---|---|
| exact match | **0.0 %** |
| median symbol error | **56.2 %** |
| 90th percentile | 71.4 % |

`station` → `stetɔn`, losing the `ʃ` entirely. `coachbuilding` → `toæʃbːldɪ`. All four ways of framing
the input were tried (`g2pframe`) and the best is reported here, so this is not a feeding error.
Either that ONNX conversion is broken or its fallback is far weaker than the dictionary beside it;
either way **it cannot be relied on for a word the dictionary lacks.**

## 5. And the words d47 must say are precisely those words

Coverage of d47's own generated knowledge tables:

| | distinct words | CMUdict | OpenPhonemizer dict |
|---|---|---|---|
| engineer **systems** | 42 | 21.4 % | **11.9 %** |
| engineer names | 77 | 75.3 % | 35.1 % |
| engineer stations | 72 | 80.6 % | 69.4 % |
| materials | 1,072 | 77.0 % | 82.3 % |
| ships & modules | 328 | 62.5 % | 63.7 % |

**Star system names are the worst-covered category and the most-spoken one.** Achenar, Alioth,
Deciat, Leesti, Wyrd, Maia, Sothis — none is in either dictionary. End to end:

```
Docking permission granted at Shinrarta Dezhra.
  -> dˈɑːkɪŋ pɚmˈɪʃən ɡɹˈæntᵻd æt ˈɪnːɹɾ ˈzɹe.
```

The sentence is right up to the system name, which loses its `ʃ` and two syllables — it reads aloud
as roughly *"innert zray"*. **410 distinct words** in d47's shipped tables fall through to the
fallback.

## 6. What this means for the phase

**Not a blocker. A different phase from the one the three bullets describe.** The model, the licence
and the blend are all fine; the work is a pronunciation layer d47 has never needed before.

Three things follow, in the order they should be decided:

1. **A pronunciation table is generated, not guessed.** d47's knowledge TSVs are already derived by
   generators with provenance recorded — a phoneme column is the same pattern, and it turns 410 OOV
   words into a finite, reviewable, once-only job. This is the repo's existing answer to
   "the source does not carry the fact d47 needs".
2. **Unbounded names still need an answer, and it is not the fallback net.** Elite has 400 billion
   systems; procedural names (`Col 285 Sector XY-Z b12-3`) are structured and mostly spelled out
   rather than pronounced, but hand-named systems are a long tail no table will finish. What a
   Commander hears for an untabled name has to be a decision rather than whatever the net emits.
3. **The size/latency trade is a settings question**, not an implementation detail: 310 MB at ×9
   realtime, or 82 MB at ×2.1. Neither is obviously right for an installer that is 70 MB today.
4. **If blending is ever offered, the axis is accent and not gender.** Blending two voices of the
   same gender gives a new voice that still reads as a specific person; blending across gender gives
   androgyny. Both are legitimate — but they are different features, and a slider that does not know
   which one it is doing will produce the second by accident.

**What is not yet known**: how any of this behaves under the arbiter's 48 kHz resample, and whether
the original PyTorch OpenPhonemizer fallback is better than its ONNX conversion. The last would need
a Python run this spike deliberately did not take, because a Python result would not tell d47
anything it can ship.

---

**Reproduce:** `spike/KokoroProbe/README.md`. Model files live outside the repository
(`%LOCALAPPDATA%\d47-spike`) — 310 MB of weights is not a thing to commit.
