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

## Amended 2026-09-05: the ear chose Turbo, so everything below was tuned on the wrong model

**The blind A/B is finished and Turbo won** — 94 of 140 decisive trials, 67.1%, p = 0.0001
(`spike/ChatterboxAb`). Every latency number in the two sections after this one is a Nano number,
and Turbo's language model costs about two and a half times Nano's per token. Re-tuned on Turbo, on
the same machine, the same line and Doug's own voice, five measured passes per configuration after
a warm one:

| Turbo, one line, warm medians | first sound | total | realtime | stalls |
|---|---|---|---|---|
| q4f16, `--threads 8 --decoder-threads 16` — the settings Nano was tuned to | 1,224 ms | 5,257 ms | ×0.92 | 95 ms |
| q4f16, decoder threads dropped to 8 | 1,181 ms | 3,304 ms | ×1.47 | 0 |
| … and the decoder on a thread of its own | 1,124 ms | 3,220 ms | ×1.49 | 0 |
| … and q4 rather than q4f16 | 915 ms | 3,133 ms | ×1.81 | 0 |
| **… and pieces of 20 tokens with 3 of context** | **796 ms** | **3,120 ms** | **×1.82** | **0** |
| the same, on a 9.0s line in twelve pieces | 821 ms | 5,059 ms | ×1.78 | 0 |

Whisper reads every line above back correctly. Spread across the five passes is 1–2%.

**Running the decoder behind the language model is the single largest change, and it had never been
run.** `stream` measured the two stages one after the other and *added their durations up* to
report "zero stalls with the decoder on its own thread". `--pipeline` now actually hands each piece
to a consumer thread. The projection was wrong in both directions: overlapped, each decode costs
30–70% more than it did alone, and the line still finishes 23–35% sooner. Turbo goes from ×0.97 to
×1.49 at q4f16, and from ×1.17 to ×1.81 at q4. Every pieced arrangement measured zero stalls once
the decoder was pipelined, including ones running at ×0.7 sequentially.

**`--decoder-threads 16` was a Nano setting and it costs Turbo a third of its headroom.** Eight
intra-op threads — the P-core count — is right for both graphs; 8/8 through 8/12 is a flat plateau
and everything outside it is worse. Sixteen threads for either graph spills onto the E-cores
(×1.06), and 24 collapses (×0.21). `--spin off` costs 13% and the default is already on.

**Piece size is the one real trade left.** Smaller pieces reach first sound sooner and cost total
throughput, because every decode carries the reference clip's tokens as its prompt whatever it is
decoding:

| tokens per piece | first sound | realtime |
|---|---|---|
| 10 | 890 ms | ×0.61–0.79, and it stalls |
| 15 | 1,000 ms | ×1.07 |
| **20** | **1,120 ms** | **×1.31** |
| 25 | 1,265 ms | ×1.39 |
| 30 | 1,380 ms | ×1.44 |
| 40 | 1,650 ms | ×1.43 |

Twenty is the knee: past thirty there is no throughput left to buy and first sound keeps rising.
Overlap is worth under 2% anywhere between three and eight tokens. The reference clip still matters
as much as §"Where the time goes" says — 5s gives ×1.48, Doug's 6.4s take ×1.40 and a 10s clip
×0.96 — so the 5–7s rule the recorder is to enforce is confirmed at the short end of its range.

### q4 rather than q4f16, and the version pin comes off

Turbo is the only variant Resemble publish all five precisions of, and only q4f16 had ever been
measured. All four runnable ones, at 8/8 threads with the decoder pipelined:

| Turbo | download | first sound | realtime | verdict |
|---|---|---|---|---|
| **q4** — int4 weights, fp32 activations | 691 MB | **915 ms** | **×1.81** | the one to use |
| q4f16 | 539 MB | 1,124 ms | ×1.49 | 152 MB smaller, a third less headroom |
| fp16 | 1,587 MB | 1,329 ms | ×1.25 | three times the download to be slower |
| q8 (`quantized`) | 1,071 MB | 8,032 ms | ×0.14 | unusable, and consistently so |

q4 is faster in both stages that matter — 16.6 ms per token against q4f16's 20.9, and 756 ms of
decode against 862 — which is Microsoft's own account of int4 on x64: fp32 activations take the
CPU-native `MatMulNBits` path and fp16 ones do not. Part of its realtime advantage is also that it
speaks a little more slowly, 5.68s of audio where q4f16 gives 4.80s for the same sentence; the
per-token figures are the honest comparison and they favour q4 on their own. q8 is not a near miss
but an order of magnitude out, repeatably, at 1% spread.

**And the ONNX Runtime 1.28.0 pin was a Nano fact.** Measured back to back on both packages, same
line, same clip, eight threads:

| q4f16 language model | ORT 1.28.0 | ORT 1.29.0 | |
|---|---|---|---|
| Nano — the community conversion | 7.6 ms/token | 12.2 ms/token | **×1.61 slower**, as recorded |
| Turbo — Resemble's own export | 20.9 ms/token | 22.6 ms/token | ×1.08 |
| Turbo q4 | 16.6 ms/token | 19.7 ms/token | ×1.19 |

Through the pieced, pipelined path the difference is smaller still, because the language model's
extra cost hides behind decode work that is happening anyway: q4f16 reads ×1.49 on 1.28.0 and ×1.51
on 1.29.0, and q4 ×1.81 against ×1.75. **So d47 can ship Chatterbox on 1.29.0, the version
`D47.Tts` already references, and does not need a second runtime.** The Nano control reproduces the
recorded 1.7×, so this is a difference between the two exports rather than a change in method —
[onnxruntime#32255](https://github.com/microsoft/onnxruntime/issues/32255) lands hard on the
community conversion's graph and barely touches Resemble's.

**What to build against, on this chip.** Turbo q4, eight intra-op threads for both the language
model and the decoder, the decoder on its own thread behind the language model, pieces of 20 tokens
with 3 of context, a 5s reference clip, encoder output cached per voice. **First sound 796 ms and
×1.82 realtime, with no stall on any line measured.** A thread count that is right here is wrong on
another chip, so whatever ships still needs a heuristic rather than these constants.

---

## Amended 2026-09-04: the premise changed, and two numbers below are wrong

**The question is no longer whether Chatterbox clears Kokoro's bar.** The maintainer ruled that
Kokoro goes and Chatterbox is offered as an option, lag and all; the job became getting first sound
as low as it will go on the CPU, with the GPU a bonus. Everything from here was measured after that
ruling, on the same machine and line, five warm runs each with the thread pool pinned — because on
the default pool the same configuration varies ±25% run to run, and pinned it varies ±3%.

**Two corrections to the text below.** The 3,375 ms headline in §2 was taken on ONNX Runtime's
default thread pool; at eight intra-op threads (the P-core count) the same measurement reads
2,044 ms on 1.29.0. And "nothing available moves that by an order of magnitude" is false: the
runtime version, the reference clip's length and decoding the line in pieces each move it by more
than the section allowed for, and together they take first sound from 3,375 ms to under 500.

### Where the time goes, and what moves it

| Nano q4f16, one 2.84s line, warm medians | first sound | encode | language model | decoder |
|---|---|---|---|---|
| ORT 1.29.0, default pool (the §2 number) | 3,375 ms | | | |
| ORT 1.29.0, 8 threads | 2,044 ms | 308 | 1,007 (14.4 ms/token) | 733 |
| ORT 1.28.0, 8 threads | 1,574 ms | 323 | 541 (7.7 ms/token) | 710 |
| ORT 1.28.0, 8 threads, decoder on 16 | 1,537 ms | 336 | 551 | 651 |
| … and a 5s reference clip instead of 10s | 1,139 ms | 160 | 574 | 403 |
| … and decoded in pieces of 25 tokens, encoder cached | **498 ms** | — | 243 | 255 |

- **ONNX Runtime 1.29.0 runs this language model 1.7× slower than every version before it.** Built
  and measured against 1.24.4, 1.25.0, 1.26.0, 1.27.1 and 1.28.0: all between 7.7 and 8.7 ms per
  token. 1.29.0 alone reads 13.7–14.4. The decoder is unchanged across all six. 1.29.0 is the
  version `D47.Tts` ships; the §2 paragraph calling the pin "worth nothing" measured on the
  default pool, where the noise hid it. It is a known bug, not a tuning question:
  [onnxruntime#32255](https://github.com/microsoft/onnxruntime/issues/32255) — 1.29.0 registered
  fp16 `MatMul` and `Gemm` CPU kernels on every target, and on x64, which has no fp16 GEMM, they fall
  through to an Eigen product; 1.28 promoted those nodes to fp32 instead. The q4f16 export has 24
  such nodes per token (attention's QKᵀ and PV), the q4 decoder has none. The fix
  ([#32301](https://github.com/microsoft/onnxruntime/pull/32301)) merged to `main` on 2026-09-01
  and is in no package; nothing in `SessionOptions` recovers it. Pin 1.28.0, or ship an export
  with fp32 activations — which is also Microsoft's own CPU recipe for int4 decoders.
- **The decoder's cost is the reference clip, not the line.** Every decode carries the reference's
  own speech tokens as its prompt — 250 of them for a 10s clip — so a 25-token piece costs almost
  what the whole line does: 442 ms against 651. A 5s clip halves the decoder (403 ms) and the
  encoder (160 ms) and Whisper reads the line back unchanged. **A 3s clip breaks it:** the model
  stopped after 24 tokens and Whisper heard "Ace to Pre". Five seconds is the floor, not a target.
- **Decoding in pieces works, and §2 was wrong to dismiss it.** The graph is one-shot, but nothing
  makes the line one piece: `stream` decodes each 25 tokens as they exist, with five tokens of the
  previous piece in front for context and a 10 ms crossfade at the seam. Whisper small.en reads
  every pieced line back correctly — the 2.84s line at pieces of 10, 15, 20 and 25 tokens, and an
  8.6s line in eleven pieces: *"Docking permission granted at Shinrata Desra. Proceed to pad 32 and
  mind the Anaconda on approach, Commander. Fuel is at 40%."* Playback never waits: each piece is
  ready before the one before it has finished playing, on one thread. **The seams are measurable
  and never the sharpest thing in the file.** Spectral flux — the mean change of the dB spectrum
  between 20 ms frames, every 5 ms — puts every seam between the 85th and 99th percentile of its
  own file, so a seam is sharper than most frames; and the model's own consonant onsets are twice
  as sharp as any seam, in the one-piece file as much as the pieced one. Two of twenty-one seams
  across three files reached the 99th percentile; those are the candidates for an ear. Pieces of
  25 tokens put seams at exactly 1.0, 2.0, 3.0… s, so a listener can be told where to listen. The
  WAVs are under `%LOCALAPPDATA%\d47-spike\chatterbox\out\` (`stream-*.wav`, `s-c*.wav`,
  `long-*.wav`, `doug*.wav`, `ship-*.wav`; `*-marked.wav` carry a tick a quarter second before
  each seam).
- **Long lines scale fine once pieced.** 8.6s of audio, 213 speech tokens: first sound 424 ms with a
  5s clip (645 with 10s), total synthesis 4.1s, realtime ×2.1, no stalls.
- **Threads.** The language model wants *more* threads, not fewer — 1: 41.9 ms/token, 2: 26.2,
  4: 18.2, 8: 14.4, 12: 13.2 on 1.29.0 — because it is not dispatch-bound, it is op-count-bound:
  the export runs about 1,400 nodes per token, of which 73 are matmuls carrying 44% of the time and
  the rest are Cast (166), Unsqueeze (182), Reshape (149), Concat (136), Gather (126) and Add (113).
  The decoder is best at 16 (651 ms; 733 at 8, 941 at 24, where the pool spills onto E-cores and
  its idle spinning slows the *next* line's encoder). `session.intra_op.allow_spinning=0` loses
  5%. One environment-wide pool shared by the four sessions measures the same as one pool each.
  A count that is right on this chip is wrong on another; whatever ships needs a heuristic.

**The next lead, not taken:** a fused re-export of the language model with fp32 activations. The
1,400-node step is the plain transformers export with fp16 KV cache, which the CPU provider wraps
in casts; ONNX Runtime's transformers optimiser folds the shape chains and fuses attention, and
"q4" (fp32 activations) is the CPU-native MatMulNBits path. That is Python tooling and a graph d47
would host itself. The per-token embedding dispatch (lead 4 of the brief) and an in-place KV cache
(lead 5) are smaller than either.

### The GPU, revisited: one route works for one graph

`Microsoft.ML.OnnxRuntime.EP.WebGpu` 0.3.0 (2026-08-24, MIT) is a plugin provider that sits beside
the base package, so it needs no version move — the one thing DirectML could not offer. Dawn over
Direct3D 12; `-p:Ep=webgpu` builds against it, `--provider webgpu` runs on it.

| graph | on WebGPU |
|---|---|
| language_model | **runs correctly** — 69 of 70 tokens identical to the CPU's, the one difference a tie-break, and Whisper reads the line back. 17.4 ms/token as wired, slower than the CPU: the KV cache round-trips to the card every step and 124 nodes fall back to the CPU |
| embed_tokens | runs |
| speech_encoder | computes wrong speech-token ids (the quantised gather after it caught index 6564 of a 6561 table). Runs once per voice; keep it on the CPU |
| conditional_decoder | **aborts the process.** After about 55 s of first-run shader work, five `_com_error` exceptions inside the provider and a fail-fast (0xC0000409) with the NVIDIA D3D12 driver on the stack. The same for Turbo's official export at fp16 and q4f16, so not the conversion. Forcing any one op type to the CPU changes nothing (`forceCpuNodeNames` is capped at 8,192 characters, so the large op types could only be tried in halves). No FXC fallback: without `dxcompiler.dll` the provider refuses to initialise. The Vulkan backend cannot load `vulkan-1.dll` from System32 (error 87); with the loader copied beside the exe it initialises, and the decoder dies the same way — so the common factor is the NVIDIA driver's shader path, not Direct3D or DXC |

So the GPU can take the language model today and not the decoder, and the language model on the
GPU only pays once the KV cache stays on the card (IoBinding) and the export stops falling back.

**What the crash is, as far as it could be taken.** No issue on the ONNX Runtime tracker matches
it, and `_com_error` is thrown nowhere in ONNX Runtime, Dawn or the shader compiler — the NVIDIA
user-mode driver is the only other C++ module on the stack, and it is known to raise that type.
It is not a display timeout: the System log has no event 4101, and a timeout would come back as
an exception, not a fail-fast. The shape fits a C++ exception escaping the driver's pipeline
compile on a Dawn worker thread, whose entry point terminates the process. Serialising pipeline
builds (`maxNumPendingDispatches=1`) with environment-level verbose logging and the shader dump
(`ORT_WEBGPU_EP_SHADER_DUMP_FILE`) named nothing: the dump was empty and the last log lines were
arena reservations. Untried: the same provider on another card or driver, and forcing the whole
of `Conv`, `LayerNormalization` or `MatMulNBits` to the CPU, which the 8,192-character cap
prevented — a graph rewrite would get round it.

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

**These are failures, not refusals, and the difference is the whole reason they are fatal.** The
sessions build; DirectML *takes* those nodes at partition time and throws from inside its own kernel
implementation (`MLOperatorAuthorImpl.cpp`) at execute time. Had it declined the operators instead,
ONNX Runtime would have placed them on the CPU and the pipeline would have run, slowly and correctly.
`80070057` out of DirectML is a long-running family of bugs — reported against `Add`, `Resize`,
`InstanceNormalization`, `MatMul` and `ConvTranspose` over several years, with the same models
succeeding on the CPU and on CUDA — which fits a provider Microsoft has in sustained engineering with
new work moved to Windows ML.

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

**And "the GPU leg does not exist" means DirectML, which is narrower than it sounds.** Three other
routes to the card are untested: CUDA, Windows ML, and ONNX Runtime's newer WebGPU plugin execution
provider — and the Nano conversion reports its graphs running through ONNX Runtime Web on WebGPU on
one RTX 3060, so a working GPU path is not a hopeless idea. It is not one worth chasing either. Even
if the language model *and* the vocoder both went five times faster, a Nano line would land near 780
ms against Kokoro's 321, and it would still be the slower option for a download 1.8× the size. The
CPU measurement decides this on its own; the GPU was only ever the thing that might have rescued it.

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
