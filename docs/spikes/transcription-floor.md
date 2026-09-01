# What one transcription actually costs

**Measured 2026-08-29, on the machine d47 runs on**, for
[#182](https://github.com/dseelinger/d47/issues/182): *"Transcription costs about 3 seconds flat
regardless of device or audio length: measure the name-hint prompt's share."*

Taken with `spike/TranscribeFloorProbe`, which drives `WhisperTranscriber` directly — the shipped
class, not a copy of it — against the audio recorder's own kept clips
([#164](https://github.com/dseelinger/d47/issues/164)) and the installed `ggml` models. Same clip,
same model, one variable at a time. Every figure is a median of five or seven calls with the range
beside it, and the first call after a load is thrown away.

**In one line: the name hints are not the floor, and the floor was four threads.**

---

## 1. The hints are real and they are not the floor

The issue's prime suspect, measured. One clip, `small.en`, steady state — the processor is already
built for these names before the clock starts:

| Hints | Time | Cost of the hints |
|---|---|---|
| 0 | 2,493 ms | — |
| 5 | 2,589 ms | +96 ms |
| 20 | 2,674 ms | +181 ms |
| 40 | 2,854 ms | +361 ms |
| 60 | 2,990 ms | +497 ms |

*(Mean over the five short clips whose spreads were tightest; the full 22-clip table is in the
probe's output and tells the same story with more scatter.)*

About **8 ms per hint**, so the 36-39 hints every in-game call in #182 carried were costing roughly
**300 ms of a 2,900 ms call — a sixth of it.** Real, worth having back, and **not the three
seconds.** Turn every hint off and the call still costs 2.5 seconds.

**So the prime suspect is acquitted**, which is the result the issue asked for either way.

### The one 0-hint run on record was a different model, not a different prompt

#182 flags its own confound: `Transcribed 1s of audio in 333ms with 0 name hints` was ten times
faster, but it was also `tiny.en`. Measured separately, that is settled — `tiny.en` at 0 hints is
**344 ms**, which is that log line almost exactly. The ten-times figure was **model size and
nothing else**.

## 2. The floor is Whisper's fixed 30-second window

The same clip padded with silence to a spread of durations, 0 hints, `small.en`:

| Audio | Time |
|---|---|
| 0.25 s | 2,572 ms |
| 1 s | 2,580 ms |
| 2 s | 2,522 ms |
| 5 s | 2,608 ms |
| 10 s | 2,735 ms |
| 20 s | 2,819 ms |
| 29 s | 2,912 ms |
| **31 s** | **5,367 ms** |
| 45 s | 5,726 ms |
| 61 s | 8,170 ms |

**A quarter of a second of audio costs what twenty-nine seconds costs, and thirty-one seconds costs
twice that.** Whisper encodes a 30-second mel spectrogram whatever you give it, padding the rest
with silence; the step at 31 s is the second window opening. That is the fixed per-call cost #182
inferred from the log, located exactly.

It also explains the 58.6 s / 9,473 ms row in the issue that read as proof of a working GPU: three
windows, and three windows is what three windows cost.

## 3. What the floor actually was: four threads

**Whisper's own default is `min(4, hardware_concurrency)`, and d47 never overrode it** — so this
24-core machine transcribed on four threads. Same clip, same model, same 40 hints, straight against
Whisper.net:

| Setting | `small.en` | `base.en` | `tiny.en` |
|---|---|---|---|
| what d47 shipped (threads unset) | 2,939 ms | 889 ms | 431 ms |
| `WithThreads(4)` | 2,972 ms | 892 ms | 429 ms |
| `WithThreads(8)` | 1,754 ms | 541 ms | 274 ms |
| `WithThreads(12)` | 1,343 ms | 481 ms | 226 ms |
| **`WithThreads(16)`** | **1,166 ms** | **401 ms** | **215 ms** |
| `WithThreads(24)` | 1,155 ms | 407 ms | 234 ms |

Three things this says, and each of them is load-bearing:

- **The unset row and the four-thread row are the same row.** That is the confirmation that nothing
  was setting this, rather than something setting it to a value that happened to be small.
- **The knee is sixteen on all three models.** Twenty-four bought 11 ms over sixteen on `small.en`
  and was *slower* on the other two, so asking for the whole machine takes it for nothing.
- **The transcript is identical at every thread count.** `ggml` reduces across threads, so the
  arithmetic is not bit-identical and the question is fair — it was asked, and "Can you hear me?"
  came back from all eight rows.

The hints get cheaper with it too, because the decode is what they cost: 40 hints cost **361 ms at
four threads and 144 ms at sixteen**.

### What that is worth, driven through the shipped class

`WhisperTranscriber` now sets the thread count. Re-measured afterwards on one 4.3-second clip —
the same clip, before and after, through `TranscribeAsync` rather than against the builder:

| Model | Before, 0 hints | After, 0 hints | Before, 40 hints | After, 40 hints |
|---|---|---|---|---|
| `tiny.en` | 348 ms | **176 ms** | 425 ms | **215 ms** |
| `base.en` | 744 ms | **324 ms** | 900 ms | **395 ms** |
| `small.en` | 2,713 ms | **1,074 ms** | 3,021 ms | **1,182 ms** |

**`small.en` went from three seconds a call to one.** The 30-second window is still there and still
the shape of the cost — 1,035 ms at a quarter-second of audio, 1,069 ms at 29 s, 1,982 ms at 31 s —
it is simply two and a half times cheaper to encode.

## 4. Two suspects cleared

**Rebuilding the processor on a changed hint set costs about 90 ms**, consistently, on the rows the
machine was quiet for. A different set of names means a new `WhisperProcessor`, and that is what a
new one costs — against a call of 2,900 ms, and against a set of names that turns over on a jump
rather than on an utterance. **Caching the encoded prompt is a fix for a problem that is not
there**, and #182 lists it as a candidate; it is not one.

**`WithProbabilities` is free.** 3,005 ms with it and 2,987 ms without, which is noise. The
confidence figure the panel reads costs nothing.

## 5. The prompt cap sits exactly on `ProperNouns.Limit`

Hints past the point Whisper stops reading them, `small.en`:

| Hints | Prompt | Time |
|---|---|---|
| 0 | 0 chars | 2,808 ms |
| 20 | 258 chars | 2,834 ms |
| 40 | 538 chars | 3,005 ms |
| 60 | 836 chars | 3,171 ms |
| 80 | 1,123 chars | 3,122 ms |
| 120 | 1,777 chars | 3,117 ms |
| 200 | 3,024 chars | 3,125 ms |
| 400 | 6,175 chars | 3,240 ms |

**The cost stops rising at sixty and never resumes** — 400 hints cost what 80 hints cost. Whisper
keeps `n_text_ctx / 2` tokens of an initial prompt and drops the rest, and this locates that cap at
around 836 characters of comma-separated Elite proper nouns.

`ProperNouns.Limit` is **60**. So the budget is currently spent exactly, with no margin, and the
practical finding is a negative one: **raising the cap would buy nothing at all** — the names past
it are not slow, they are ignored.

**What this measurement does not settle is which end gets dropped** when the list does overrun.
d47 appends the shipped engineers *last*, on the stated reasoning that "where the Commander is now
beats every engineer in the galaxy" — and if Whisper keeps the *tail* of the prompt, that ordering
achieves the opposite of what it intends. Answering that needs an accuracy experiment against clips
containing the names in question, which the recorder's kept clips do not have. It is worth its own
issue and is not one this measurement can close.

## 6. Not part of #182, found while here: the GPU toggle does nothing

**Only CPU natives ship.** The installed build's `runtimes\win-x64` holds `whisper.dll`,
`ggml-whisper.dll`, `ggml-base-whisper.dll` and `ggml-cpu-whisper.dll` — no CUDA backend, and
`D47.Stt.csproj` references `Whisper.net.Runtime` alone.

Driven with `--gpu`, the probe loads without complaint, reports `UsingGpu=True`, and produces
**timings indistinguishable from CPU**: 2,602 ms against 2,572 ms at 0.25 s of audio, 5,080 ms
against 5,367 ms at 31 s.

The reason it reports success is that `UsingGpu` is assigned from the flag that was *asked for*
rather than from anything the native side said:

```csharp
_processor = Processor(_factory, prompt: null);
UsingGpu = useGpu;
```

So the load line says "on the GPU" whenever the setting is on, and #182's inference that "the GPU
is genuinely working" rests on that line. It was not working; both devices were the CPU, which is
why both cost the same three seconds.

This is precisely the failure `docs/capabilities/listening.md` promises is prevented — *"If the CUDA
runtime is not installed, Directive 47 **says so** and leaves transcription unavailable rather than
quietly using the CPU. A GPU switch that silently does nothing is the same undiagnosable problem in
the other direction."* — and it is a separate defect from this one. Recorded here rather than fixed.

## 7. A caveat about the machine

d47 was running and being driven by the Commander through part of the sweep, so the 22-clip hint
table carries scattered outliers — 5,800 ms in a column whose neighbours are 2,800 ms — and the
rebuild table has rows where the *baseline* caught the contention and the difference came out
negative. Medians of five absorb most of it and the tight-spread rows agree with the noisy ones.

Every conclusion above was re-taken afterwards and reproduced: the thread curve twice, the
transcript identity, the plateau, and the GPU result. **Contention inflates, so the floor figures
are if anything generous.**
