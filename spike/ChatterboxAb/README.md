# ChatterboxAb

Whether Doug's ear prefers Chatterbox **Turbo** or **Nano** — blind, by one listener, with the
models hidden and latency kept out of it entirely, because every clip is made before it is heard.

The answer matters beyond taste: Nano is roughly twice as fast to first sound, so if the ear cannot
tell them apart, Nano ships and the GPU question closes with it. See
[the finding](../../docs/spikes/chatterbox-through-onnx.md) and `spike/ChatterboxProbe`.

## The corpus lives outside the repository

2.4 GB at `%LOCALAPPDATA%\d47-spike\chatterbox\corpus\`, beside the models, and nothing here is
committed:

| | |
|---|---|
| `raw/<category>/<voice>-<n>.<ext>` | what the sourcing agents downloaded, untouched |
| `prepared/<category>/<voice>.wav` | five to seven seconds, mono, 24 kHz, trimmed and levelled |
| `manifests/*.json` | what each agent said it fetched, and from where |
| `candidates.json` | one row per voice: id, label, seconds, licence, sources |
| `verdicts.json` | the ear's judgement per voice |
| `viability.json` | whether an approved voice clones at all, machine-checked |
| `trials.json`, `picks.json` | the A/B pairs, and what was chosen |
| `synth/` | each surviving voice speaking each line, on each model |

**261 voices, 134 approved, 132 clone at all**: Star Trek from TOS to Enterprise, ship computers (HAL, GLaDOS,
SHODAN, KITT, Mother, WOPR, the Borg), film and cartoon characters, actors whose voice is an
instrument, real-world audio and accents, and 48 deliberately dull controls — twelve LibriSpeech
readers and six RAVDESS actors in six moods each, so the interesting ones have something to be
judged against.

Every clip is for this listening test and nothing else. Much of it is copyrighted performance and
could never ship; the shipping question is settled elsewhere and the answer is Doug's own voice.

## Commands

```powershell
$c = "$env:LOCALAPPDATA\d47-spike\chatterbox\corpus"
dotnet run --project spike/ChatterboxAb -c Release -- prepare $c
dotnet run --project spike/ChatterboxAb -c Release -- serve   $c 8765
dotnet run --project spike/ChatterboxAb -c Release -- viable  $c <path to ChatterboxProbe.exe> nano
dotnet run --project spike/ChatterboxAb -c Release -- synth   $c <path to ChatterboxProbe.exe>
dotnet run --project spike/ChatterboxAb -c Release -- stretch $c <path to ChatterboxProbe.exe> characters/brian 12
```

`prepare` is idempotent and safe to re-run whenever clips change: it cuts only what is missing,
upgrades a label when a manifest lands late, and drops a candidate whose audio has gone. `serve`
hosts the review page at `/` and the A/B at `/ab`. `viable` clones one ~10s phrase per approved
voice on one model and Whisper-checks the result against the input text, dropping anything that
comes back as no words, the wrong words, or garbage — a reference clip that is fine to *listen* to
can still be too short or too processed to clone at all. `synth` and `stretch` both need a probe
built on **ORT 1.28.0** (`-p:Ep=cpu -p:OrtVersion=1.28.0`), because 1.29.0 runs the language model
1.7× slower; `synth` builds the trials, and `stretch` re-cuts one voice past the corpus-wide 5-7s
cap to ask whether more reference audio clones it better, without moving the cap everything else is
cut and cached against.

## The four verdicts

`interesting` is the only one that reaches the test. The other three say *why* a voice does not:

| | |
|---|---|
| **meh** | heard, and not worth a trial |
| **unusable** | not the voice it claims to be, or music under it that would corrupt a clone |
| **begins with another's voice** | an announcer, an interviewer or an archive notice speaks first, and the five seconds run out before the real voice does |

The last one earns its own button because the fix is not re-sourcing. The recording holds the right
voice further in, so the window moves later and the clip is cut again — which is what recovered all
24 dialect-archive accents, every one of which opened with *"This recording is copyright the
International Dialects of English Archive."* Marking those unusable would have thrown away every
accent in the corpus.

## The stopping rule

Ties are not evidence, so they are dropped; what remains is a coin-flip question, and an **exact
two-sided binomial test** answers it without an approximation that a small sample would break. The
test stops at 95% confidence with at least 20 decisive trials, or at 100 trials — and stopping at
100 without significance is itself the finding, because it means the ear cannot tell the two models
apart. The page shows the running tally, the Wilson interval and the p-value as you go.

## Four things that will bite the next reader

**A downloaded clip that plays is not a clip.** A site whose signed link has expired serves a spoken
placeholder — *"Please refresh the page to hear the sounds"* — with a 200 and an audio content type,
so nothing upstream can tell it from a voice. Twenty-one reached this corpus from **two** sites,
101soundboards and moviesoundclips, one of them nineteen times, and they surfaced only when a person
played them. `prepare` now hashes every file under 400 KB and deletes any whose exact bytes appear
under more than one voice: two real recordings of two different actors never collide, so a collision
is a placeholder by construction, and no list of known hashes has to be kept up to date.

**Deleting the bad input is not rebuilding the output.** The first sweep deleted the placeholder
files and left the clips already cut from them, so two voices went on playing the placeholder after
the sweep claimed to have cleaned them. Any voice cut in a run now has its verdict cleared too — the
old verdict was about audio that no longer plays.

**Transcribe, don't trust.** Both traps were caught by handing every prepared clip back to the
Whisper model d47 ships. It is cheap, it is the only check that reads what is actually in the file,
and it is the same check `viable` runs against a clone rather than a source recording. `D47.Stt`
references cleanly from here — this project never loads ONNX Runtime, so there is nothing for
Whisper.net's native runtime to collide with; that collision is `ChatterboxProbe`'s own problem
(its README), not this one's.

**A clone that says the right words can still not sound like the source.** `viable` only checks
that Chatterbox produced *some* coherent voice saying the input text — it says nothing about timbre.
Brian Griffin and Bugs Bunny both pass viability cleanly and both still read back as a generic
narrator with the shape of an impression, not the character. The instinct is to blame the 5-7s
reference cap, but it isn't that: both voices' raw soundboard clips support building a reference
**twice** as long (`stretch … 12` uses all of it, no floor hit), and the 12s clone is barely
different from the 5-7s one on the same line. The fallback to generic is Chatterbox itself — small
and heavily quantised (`q4f16`) on an unusual voice — not a starved reference, so the corpus-wide
cap stays where it is rather than doubling the run time of every synth pass for a difference this
small.
