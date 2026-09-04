# Chatterbox through the ONNX path

**Run 2026-09-04, from C#, on the machine d47 runs on.** The spike
[#293](https://github.com/dseelinger/d47/issues/293) gates everything else on:

> 1. First-sound latency and realtime factor on the CPU… If a ten-word line takes longer to start
>    than Kokoro takes to finish, the answer is no, whatever it sounds like.
> 2. Does the ONNX path work from .NET at all?
> 3. The ear test.

Measured with `spike/ChatterboxProbe`, against `Microsoft.ML.OnnxRuntime.DirectML` **1.24.4** and
`Microsoft.ML.OnnxRuntime` **1.29.0** — the second being the version `D47.Tts` ships.

**In one line: the port works and the delivery tags are real, but the latency is not close and the
GPU leg does not exist. DirectML errors on two of Chatterbox's four graphs, crashes or garbles the
third, and runs only the decoder.**

The machine: Intel Core Ultra 9 285K, 24 logical cores, 32 GB; NVIDIA GeForce RTX 5080 16 GB,
driver 610.74; Windows 11.

---

## 1. The port works, and the proof is not "it produced a WAV"

The four graphs drive from C# with no wrapper in the middle. Everything Resemble's own sample gets
from `numpy`, `librosa` and `transformers` is written out in the probe: a GPT-2 byte-level BPE, a
RIFF reader and resampler, the greedy sampling loop with its repetition penalty, and a 48-tensor KV
cache passed back as `OrtValue`s so it never touches the managed heap.

A WAV that is the right length and has plausible energy in it proves nothing, so the check was to
hand the output back to the Whisper model d47 already ships:

| asked for | heard back |
|---|---|
| `Docking permission granted at Shinrarta Dezhra.` (Turbo) | *Docking permission granted at Shinrarte Dejra.* |
| `Docking permission granted at Shinrarta Dezhra.` (Nano) | *Docking permission granted at Shinwar to Desrah.* |
| `Frame shift drive charging. Five. Four. Three. Two. One. Engage.` | *Frame shift drive charging. Five, four, three, two, one. Engage.* |
| `[whispering] There is something on the scanner. It is not a ship.` | *There is something on the scanner. It is not a ship.* |
| `[chuckle] You brought a Sidewinder to a Thargoid fight, Commander.` | *Heh, you brought a sidewinder to a Thargoid fight, Commander.* |

**The second row is the pronunciation problem arriving in a new shape**, and it is worth noting even
though the phase is being declined: ordinary English comes back exactly, and Shinrarta Dezhra does
not. Kokoro answers that with `pronunciations.json` and an IPA string; Chatterbox takes text, so the
same problem would have needed a respelling table and a harness that compares against text rather
than against phonemes — which #293 already says, and which the ear here confirms is not optional.

**The last row is the tag question answered.** `[chuckle]` is one of nineteen tokens added to the
tokeniser's vocabulary — `[laugh]`, `[cough]`, `[sigh]`, `[whispering]`, `[sarcastic]` and the rest —
matched whole before the BPE sees the text. It reaches the model, the model acts on it, and an
independent listener transcribes the result as a laugh. That is the capability #293 wants and it is
genuinely there.

**The tokeniser is about two hundred lines and needs no package.** Stock GPT-2 byte-level BPE, read
out of `tokenizer.json`, plus the added tags. One detail is load-bearing and would not have been
guessed: the post-processor appends **two** `<|endoftext|>` and nothing at the front.

## 2. What it costs on the CPU, against the bar #293 set

Five runs each after a warm run, on an otherwise quiet machine, ONNX Runtime 1.24.4, one line:
`Docking permission granted at Shinrarta Dezhra.`

| | audio | first sound, median | realtime |
|---|---|---|---|
| **Kokoro** fp32 — what ships today | 3.40s | **321 ms** | ×10.6 |
| Kokoro q4f16 | 3.40s | 403 ms | ×8.4 |
| **Chatterbox Nano** q4f16 | 2.84s | **3,375 ms** | ×0.84 |
| Chatterbox Turbo q4f16 | 2.80s | 6,473 ms | ×0.43 |
| Chatterbox Turbo fp16 | 2.68s | 6,767 ms at best, 29,442 ms under memory pressure | ×0.09–0.40 |

**First sound is the whole synthesis, and that is a fact about the model rather than a limitation of
the probe.** Chatterbox's decoder is not incremental: it takes the finished speech-token sequence and
returns the finished waveform in one call, so there is no earlier moment that could be measured.
The community Nano conversion says the same thing in its own README — *"Do not market this package as
streaming."*

Where the time goes, one Nano line (14 text tokens → 70 speech tokens → 2.84s):

| stage | | |
|---|---|---|
| speech encoder | 316 ms | once per **voice**, not per line — cacheable, so treat it as free |
| language model | 981 ms | 14.0 ms per speech token, and the token count tracks the audio length |
| conditional decoder | 1,344 ms | one call, the whole waveform |
| **first sound** | **2,641 ms** | |
| session load | 4,982 ms | once per process |

Turbo's language model costs 32.0 ms per token instead of Nano's 14.0, which is the whole difference
between the two rows in the table above.

**The bar is missed by about ten times.** #293 says the answer is no if a line takes longer to *start*
than Kokoro takes to *finish*. Kokoro finishes in 321 ms, or about 560 ms once the grapheme-to-phoneme
step in front of it is counted. Chatterbox Nano starts at 3,375 ms.
Nothing available moves that by an order of magnitude: caching the speech encoder saves 316 ms, and
the runtime version is worth nothing (below).

**And the version pin is free, which is one thing that went the easy way.** The same measurement on
`Microsoft.ML.OnnxRuntime` 1.29.0, the version `D47.Tts` references, read 4,201 and 5,324 ms across
two passes against 1.24.4's 3,375 and 3,574. The ordering repeated but two passes will not carry a
claim that 1.29.0 is slower; what it does carry is that neither runtime is near the bar.

## 3. The GPU leg: DirectML cannot run this model

#293's amended ruling made the GPU allowed and asked whether it fits. It does not, and the reason is
earlier than fit: **DirectML 1.24.4 runs one of Chatterbox's four graphs.**

| graph | on DirectML |
|---|---|
| `speech_encoder` | `MultiHeadAttention` fails — `80070057 The parameter is incorrect`. fp16 and q4f16 alike |
| `embed_tokens` | `Slice /Slice_1` fails — the same `80070057` |
| `language_model` | q4f16 **fail-fasts the process** (`0xC0000409`). fp16 runs, and is wrong: 31 speech tokens where the CPU generates 70, 1.28s of audio Whisper transcribes as nothing at all |
| `conditional_decoder` | runs correctly |

The one graph that runs and the one that runs-but-lies are both worth a number, because they say the
GPU would not have been the answer even if the errors were fixed. The language model alone, fp16, on
the card, with the other three graphs pinned to the CPU:

| | |
|---|---|
| VRAM the model takes | **+1,112 MB** |
| GPU utilisation while generating | **5%** |
| first sound | 8,440 ms on the first line, **42,253 ms** median after |

Five per cent utilisation and 81 ms per token against the CPU's 14 is the signature of dispatch
overhead, not of compute: an autoregressive decoder feeds the card one token at a time on shapes that
change every step, and DirectML pays a per-step cost that swamps the work. This is not a tuning
problem with a knob behind it.

**The DirectML package stopped at 1.24.4.** `Microsoft.ML.OnnxRuntime` is at 1.29.0;
`Microsoft.ML.OnnxRuntime.DirectML` has not been published past 1.24.4, and the two cannot coexist in
one process. So adopting it would have meant moving the whole app back four minor versions of ONNX
Runtime — a cost worth knowing about even now that nothing is being adopted.

**CUDA was not tried, and that is deliberate.** #293 says to try DirectML first *"before CUDA, whose
runtime carries NVIDIA's own terms and a licence-gate question"*. There is no CUDA toolkit on this
machine and `CUDA_PATH` is unset, so trying it means a machine-wide install under those terms. That
is a decision to be typed on purpose rather than one a spike makes on the way past.

## 4. What the download would be

From the Hub's own file listing, so these are bytes rather than impressions:

| | | |
|---|---|---|
| **Kokoro**, for comparison | | **310 MB** |
| Chatterbox **Nano** — the whole community repository | mixed precision | **546 MB** |
| Chatterbox **Turbo** q4f16 | 4 graphs | 539 MB |
| Chatterbox Turbo q4 | 4 graphs | 691 MB |
| Chatterbox Turbo q8 | 4 graphs | 1,071 MB |
| Chatterbox Turbo fp16 | 4 graphs | 1,587 MB |
| Chatterbox Turbo fp32 | 4 graphs | 3,168 MB |

The smallest working set is 539 MB against Kokoro's 310 — a 1.7× first run, which on its own would
have been an easy trade.

## 5. Three things in the issue that turn out not to be true

**Nano has no official ONNX export.** `ResembleAI/chatterbox-nano` publishes safetensors only. The
Nano measurements above come from `owensong/chatterbox-nano-ONNX`, an independent conversion whose
own README says it *"is not an official Resemble AI release"* and publishes no quality, latency or
speaker-similarity claims. Only **Turbo** has an export from Resemble. So the CPU fallback #293 names
would rest on one community repository, and the fastest configuration measured here is the one with
the least standing behind it.

**Nothing is watermarked.** #293 says every output carries Resemble's Perth watermark and that it is
"not switchable". Through ONNX it is not applied at all: Resemble's own sample computes the waveform
and *then* optionally calls the separate `perth` Python package, with `apply_watermark = False` in the
published code. A .NET port produces unwatermarked audio unless somebody ports Perth too.

**"Turbo, lower compute and VRAM" is a comparison with the 500M model, not with Nano.** Turbo is the
slower of the two on the CPU here, by a factor of two.

## 6. What was not measured

**Elite's frame time with and without a line being spoken.** Elite was not running. It cannot change
the outcome: every number above is a floor that having Elite drawing would raise, on a leg that already
misses its bar by ten times, and the GPU leg has no working configuration to measure. The probe can
take it when the question comes back — `elite` samples per-process VRAM either way and reads frame time
through PresentMon when `--presentmon` names one.

## 7. What this settles

**Chatterbox is declined here, on latency and on the GPU path, and Kokoro stays** — which is the
outcome #293 wrote down for exactly this result. The zero-shot voices and the delivery tags both
work, and both are worth wanting; they are not available at a price the speech path can pay. Nothing
in the "if the spike passes" half of the issue — the phonemiser retirement, the clip catalogue, the
tag pass-through, the removal of Kokoro — should be started.

**The two things worth keeping.** The tag vocabulary is a capability d47 has nowhere to put today and
would still want from any future provider that offers it; and the probe itself is the instrument to
re-run if Resemble ships a streaming decoder, or if the DirectML errors above turn out to be a
1.24.4 bug that a later export or a later runtime clears. Neither is a reason to open anything now.
