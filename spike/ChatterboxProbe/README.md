# ChatterboxProbe

What Chatterbox costs through the ONNX path, driven from C# — which is the point.
[#293](https://github.com/dseelinger/d47/issues/293) gates a whole phase on the measurement, and
every Chatterbox ONNX wrapper that exists is Python. A Python wrapper's account of the latency would
not tell d47 anything it can ship.

**Finding: [docs/spikes/chatterbox-through-onnx.md](../../docs/spikes/chatterbox-through-onnx.md).**

## Getting the files

Nothing here is committed: the smallest working set is 539 MB. It lives outside the repository, and
every command takes `--root`.

```powershell
$m = "$env:LOCALAPPDATA\d47-spike\chatterbox"
New-Item -ItemType Directory -Force $m\turbo\onnx, $m\nano\onnx, $m\voices | Out-Null

# Turbo — Resemble's own export, MIT. q4f16 is 539 MB and is the one worth measuring;
# fp16 is 1,587 MB and was slower on the CPU, not faster.
$t = "https://huggingface.co/ResembleAI/chatterbox-turbo-ONNX/resolve/main"
curl.exe -sL "$t/tokenizer.json" -o "$m\turbo\tokenizer.json"
foreach ($g in 'embed_tokens','speech_encoder','language_model','conditional_decoder') {
    curl.exe -sL "$t/onnx/${g}_q4f16.onnx"      -o "$m\turbo\onnx\${g}_q4f16.onnx"
    curl.exe -sL "$t/onnx/${g}_q4f16.onnx_data" -o "$m\turbo\onnx\${g}_q4f16.onnx_data"
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
| `bench <text> <ref.wav> [runs]` | the same line on each provider, after a warm run |
| `gpu` | the card, what it is holding, and which process is holding it |
| `elite <text> <ref.wav>` | Elite's frame time with and without a line being spoken |

```powershell
dotnet run --project spike/ChatterboxProbe -c Release -- shape --variant nano
dotnet run --project spike/ChatterboxProbe -c Release -- say "Docking permission granted at Shinrarta Dezhra." $m\voices\andrew.wav out.wav --variant nano
dotnet run --project spike/ChatterboxProbe -c Release -- bench "Docking permission granted at Shinrarta Dezhra." $m\voices\andrew.wav 5 --variant nano --provider cpu
```

`--variant turbo|nano`, `--dtype fp32|fp16|q4|q4f16|q8`, `--provider cpu|dml`, `--root <dir>`,
`--max-tokens`, `--penalty`, `--watch`, `--cpu-graphs <names>`, `--presentmon <exe>`.

## Four things that will bite the next reader

**`-p:Ep=cpu` is not cosmetic.** The default build references
`Microsoft.ML.OnnxRuntime.DirectML` **1.24.4**, which is the last DirectML build Microsoft published;
`-p:Ep=cpu` swaps in `Microsoft.ML.OnnxRuntime` **1.29.0**, the version `D47.Tts` ships. The two
packages cannot coexist in one process, so a CPU number taken from the default build is a 1.24.4
number. Both were measured; the difference is inside the noise.

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
DirectML package — so it was a throwaway console app outside the repository. If the question comes
back, build that again rather than trusting the waveform.
