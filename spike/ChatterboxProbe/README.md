# ChatterboxProbe

What Chatterbox costs through the ONNX path, driven from C# — which is the point.
[#293](https://github.com/dseelinger/d47/issues/293) gates a whole phase on the measurement, and
every Chatterbox ONNX wrapper that exists is Python. A Python wrapper's account of the latency would
not tell d47 anything it can ship.

**Finding: [docs/spikes/chatterbox-through-onnx.md](../../docs/spikes/chatterbox-through-onnx.md).**

## Getting the files

Nothing here is committed: the working set is 691 MB for Turbo q4, 539 MB at its smallest. It lives outside the repository, and
every command takes `--root`.

```powershell
$m = "$env:LOCALAPPDATA\d47-spike\chatterbox"
New-Item -ItemType Directory -Force $m\turbo\onnx, $m\nano\onnx, $m\voices | Out-Null

# Turbo — Resemble's own export, MIT, and the one the ear chose. q4 is 691 MB and is the one to
# use: int4 weights with fp32 activations, faster than q4f16 in both stages. q4f16 is 539 MB and
# a third slower; fp16 is 1,587 MB and slower still; `quantized` (q8) is 1,071 MB and unusable.
$t = "https://huggingface.co/ResembleAI/chatterbox-turbo-ONNX/resolve/main"
curl.exe -sL "$t/tokenizer.json" -o "$m\turbo\tokenizer.json"
foreach ($g in 'embed_tokens','speech_encoder','language_model','conditional_decoder') {
    curl.exe -sL "$t/onnx/${g}_q4.onnx"      -o "$m\turbo\onnx\${g}_q4.onnx"
    curl.exe -sL "$t/onnx/${g}_q4.onnx_data" -o "$m\turbo\onnx\${g}_q4.onnx_data"
}

# Nano — a community conversion, MIT, 546 MB, and the only ONNX Nano there is: ResembleAI's own
# chatterbox-nano publishes safetensors only. Mixed precision, so the four names differ.
$n = "https://huggingface.co/owensong/chatterbox-nano-ONNX/resolve/main"
curl.exe -sL "$n/tokenizer.json" -o "$m\nano\tokenizer.json"
foreach ($g in 'embed_tokens_fp16','speech_encoder_q4f16','language_model_q4f16','conditional_decoder_q4') {
    curl.exe -sL "$n/onnx/$g.onnx"      -o "$m\nano\onnx\$g.onnx"
    curl.exe -sL "$n/onnx/$g.onnx_data" -o "$m\nano\onnx\$g.onnx_data"
}
```

**A `.onnx_data` file must sit beside its `.onnx` under exactly that name** — the graph names it, and
ONNX Runtime resolves it relative to the graph.

**And a reference clip.** A voice *is* a clip here, five to ten seconds of clean speech, any sample
rate. `RadioAudition` makes one without leaving the repository, which is what the measurements below
used:

```powershell
dotnet run --project spike/RadioAudition -- "Directive forty seven online. All systems nominal, Commander." "$m\voices\andrew.wav"
```

That is fine for measuring and is **not** a model for shipping: a clip d47 distributes has to be one
it has the right to distribute, which is a question #293 raises and this probe does not answer.

## Commands

| | |
|---|---|
| `shape` | the four graphs: which file was opened, its bytes, its inputs and outputs, and the tag vocabulary |
| `sizes` | download size per published variant and precision, from the Hub's file listing — nothing downloaded |
| `tokens <text>` | what the tokeniser makes of a line, tags included |
| `say <text> <ref.wav> <out.wav>` | one line end to end, with the time split across the four stages |
| `bench <text> <ref.wav> [runs]` | the same line on each provider, after a warm run; the median of each stage on its own line |
| `stream <text> <ref.wav> <out.wav>` | the line decoded in pieces as its tokens arrive: first sound per piece, and whether playback would have waited |
| `gpu` | the card, what it is holding, and which process is holding it |
| `elite <text> <ref.wav>` | Elite's frame time with and without a line being spoken |

```powershell
dotnet run --project spike/ChatterboxProbe -c Release -- shape --variant nano
dotnet run --project spike/ChatterboxProbe -c Release -- say "Docking permission granted at Shinrarta Dezhra." $m\voices\andrew.wav out.wav --variant nano
dotnet run --project spike/ChatterboxProbe -c Release -p:Ep=cpu -p:OrtVersion=1.28.0 -- bench "Docking permission granted at Shinrarta Dezhra." $m\voices\andrew.wav 5 --variant nano --provider cpu --threads 8 --decoder-threads 16
dotnet run --project spike/ChatterboxProbe -c Release -p:Ep=cpu -p:OrtVersion=1.28.0 -- stream "Docking permission granted at Shinrarta Dezhra." $m\voices\andrew.wav out.wav --variant nano --threads 8 --decoder-threads 16 --chunk 25 --overlap 5

# Turbo, tuned for Turbo: 796 ms to first sound, x1.82 realtime, no stalls.
dotnet run --project spike/ChatterboxProbe -c Release -p:Ep=cpu -p:OrtVersion=1.28.0 -- stream "Docking permission granted at Shinrarta Dezhra." $m\voices\doug1-5s.wav out.wav --variant turbo --dtype q4 --lm-threads 8 --decoder-threads 8 --chunk 20 --overlap 3 --pipeline --runs 5
```

`--variant turbo|nano`, `--dtype fp32|fp16|q4|q4f16|q8`, `--provider cpu|dml|webgpu`, `--root <dir>`,
`--max-tokens`, `--penalty`, `--watch`, `--cpu-graphs <names>`, `--presentmon <exe>`.

Threads: `--threads n` for every graph, `--encoder-threads`, `--lm-threads` and `--decoder-threads`
to override one; `--spin on|off`; `--global-pool` for one environment-wide pool instead of one per
session. **Pin the threads before believing any A/B**: on the default pool one configuration varies
±25% run to run, pinned it varies ±3%. `--profile <prefix>` writes ONNX Runtime's per-op profile,
one JSON per graph; `--verbose` opens the environment at verbose so a provider's own messages show.
`--ep key=value` passes a provider option through, repeatable. `stream` takes `--chunk` (tokens per
piece, 25 is a second), `--overlap` (tokens of the previous piece decoded again as context) and
`--crossfade` (samples blended at the seam). `--pipeline` runs the decoder on a thread of its own
behind the language model, which is worth 23-35% of total time on Turbo; `--runs n` repeats the
measured pass and reports the median and the spread across runs.

## Ten things that will bite the next reader

**The 1.28.0 pin below is a Nano fact, and Turbo does not need it.** Measured back to back on
2026-09-05: the q4f16 language model costs 7.6 ms/token on 1.28.0 and 12.2 on 1.29.0 for the
community Nano conversion, but 20.9 against 22.6 for Resemble's own Turbo export. Through the
pieced, pipelined path Turbo reads ×1.49 on 1.28.0 and ×1.51 on 1.29.0 at q4f16, ×1.81 against
×1.75 at q4. Build on 1.28.0 to compare against the numbers already written down; ship on whatever
`D47.Tts` uses.

**`-p:Ep=cpu` is not cosmetic, and neither is `-p:OrtVersion`.** The default build references
`Microsoft.ML.OnnxRuntime.DirectML` **1.24.4**, which is the last DirectML build Microsoft published;
`-p:Ep=cpu` swaps in `Microsoft.ML.OnnxRuntime` at `OrtVersion` (default **1.29.0**, the version
`D47.Tts` ships), and `-p:Ep=webgpu` adds the `Microsoft.ML.OnnxRuntime.EP.WebGpu` plugin beside it.
The packages cannot coexist in one process, so a CPU number is a number for one runtime version —
and **1.29.0 runs Nano's q4f16 language model 1.7× slower than 1.24.4 through 1.28.0**
([onnxruntime#32255](https://github.com/microsoft/onnxruntime/issues/32255): fp16 `MatMul` on x64
fell through to Eigen; fixed on `main`, in no package). Measure on 1.28.0 unless the question is
1.29.0 itself — but see the paragraph above for how little of that reaches Turbo.

**A reference clip need not be a WAV.** Anything Media Foundation decodes — `.m4a`, `.mp3`,
`.wma`, `.flac` — is read through NAudio and downmixed, because Windows Sound Recorder writes AAC
by default. Its settings do offer `WAV (lossless)`, which is still the better choice for a clip
being cloned.

**The decoder does not return the same number of samples for the same tokens.** It drops the
voice's prompt itself, but not to the sample: two clips of the same length whose prompts differ by
one token give pieces that differ by one token's audio. `stream` therefore measures the overlap to
discard from the *end* of each piece. An offset assumed from the front stutters at every seam, and
only some clips show it.

**The reference clip's length is the decoder's cost.** Every decode carries the clip's own speech
tokens as its prompt, so a 10s clip makes a 25-token piece cost nearly what a whole line does. A 5s
clip halves the decoder and the encoder; a 3s clip made the model stop after 24 tokens. The
`voices\` clips are 8–10s; `andrew-5s.wav` was cut from `andrew.wav` for the amended finding.

**A provider option is capped at 8,192 characters.** `forceCpuNodeNames` cannot carry the decoder's
larger op types whole (305 `LayerNormalization` names alone are over it), so a bisect by op type
stops at the ones with a few dozen nodes. Rewriting the graph is the way past it.

**`--provider dml` mostly does not work, and the failures are informative rather than a setup
problem.** Two graphs error with `80070057`, the language model fail-fasts the process at q4f16 and
produces unintelligible audio at fp16. `--cpu-graphs speech_encoder,embed_tokens,conditional_decoder`
is how the one runnable configuration was isolated. See §3 of the finding.

**The `elite` command needs PresentMon and does not bundle it.** Per-process VRAM comes from a
Windows performance counter and works unaided; frame time has no API a bystanding process can call,
so without `--presentmon <exe>` the command says so instead of substituting GPU utilisation for it.

**A WAV of the right length is not evidence.** Every claim in the finding about what the model said
was checked by handing the output back to the Whisper model d47 already ships. Doing that from inside
this probe is not possible — `D47.Stt` references ONNX Runtime 1.29.0 and would collide with the
DirectML package — so it lives next door: `ChatterboxAb hear <wav|glob> …` transcribes anything, and
that project never loads ONNX Runtime so it can reference `D47.Stt` directly.

**A stall this probe reports may be arithmetic rather than a measurement.** `StreamTiming` carries
two projections over the stage times — one thread, and the decoder on its own — and both assume the
stages cost the same overlapped as they did taking turns. On Turbo that is wrong by 30–70% per
decode. `MeasuredStallMs`, and each piece's `ReadyMs`, are clock readings; the projections are
printed beside them so the gap stays visible rather than standing in for the measurement.
