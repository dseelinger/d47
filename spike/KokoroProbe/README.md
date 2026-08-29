# KokoroProbe

What Kokoro actually does through the ONNX path, driven from C# — which is the point. A Python
wrapper's account of it would not tell d47 anything it can ship, and
[#101](https://github.com/dseelinger/d47/issues/101) asks for the measurement precisely because the
community wrappers are not the thing d47 would be using.

**Finding: [docs/spikes/kokoro-through-onnx.md](../../docs/spikes/kokoro-through-onnx.md).**

## Getting the files

Nothing here is committed: the weights are 310 MB. They live outside the repository, and every
command takes `--root` if you put them somewhere else.

```powershell
$m = "$env:LOCALAPPDATA\d47-spike\kokoro"
New-Item -ItemType Directory -Force $m\onnx, $m\voices | Out-Null
$b = "https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main"

# Apache-2.0. fp32 is 310 MB. `bench` and `quality` measure whichever of the eight are present,
# and skip the rest — #139 needed all of them, which is 1.4 GB in total.
curl.exe -sL "$b/onnx/model.onnx"       -o "$m\onnx\model.onnx"
foreach ($v in 'fp16','q4','q4f16','q8f16','quantized','uint8','uint8f16') {
    curl.exe -sL "$b/onnx/model_$v.onnx" -o "$m\onnx\model_$v.onnx"
}
curl.exe -sL "$b/tokenizer.json"        -o "$m\tokenizer.json"
curl.exe -sL "$b/voices/af_heart.bin"   -o "$m\voices\af_heart.bin"
curl.exe -sL "$b/voices/bm_george.bin"  -o "$m\voices\bm_george.bin"

# The phonemiser: BSD-3-Clause-Clear, on DeepPhonemizer (MIT). Needed for `g2p` and `speak`.
$p = "$env:LOCALAPPDATA\d47-spike\phonemizer"
New-Item -ItemType Directory -Force $p | Out-Null
curl.exe -sL "https://huggingface.co/lookbe/open-phonemizer-onnx/resolve/main/model.onnx"         -o "$p\model.onnx"
curl.exe -sL "https://huggingface.co/lookbe/open-phonemizer-onnx/resolve/main/phoneme_dict.json"  -o "$p\phoneme_dict.json"
curl.exe -sL "https://huggingface.co/lookbe/open-phonemizer-onnx/resolve/main/tokenizer.json"     -o "$p\tokenizer.json"
```

## Commands

| | |
|---|---|
| `shape` | model inputs and outputs, and the voice tensor's real shape |
| `say <ipa> <voice> <out.wav>` | one line of IPA to a WAV |
| `blend <ipa> <a> <b> <t> <out.wav>` | the same line from a weighted blend of two voices |
| `bench <ipa> <voice> [runs]` | latency per line across **all eight** published builds (#139) |
| `quality <ipa> <voice> [dir]` | how far each build's audio departs from fp32's, and a WAV of each |
| `g2pshape` | the phonemiser's interface |
| `g2p <word>… [--raw]` | words to IPA through the fallback net; `--raw` shows the alignment |
| `g2pframe <word>` | the same word under all four input framings |
| `g2peval [n]` | the fallback scored against the dictionary's own answers |
| `speak <text> <voice> <out.wav>` | the whole road: text in, dictionary first, net for the rest, audio out |

```powershell
dotnet run --project spike/KokoroProbe -- shape
dotnet run --project spike/KokoroProbe -- blend "dɪɹˈɛktɪv fˈɔɹti sˈɛvən ˈɔnlIn." af_heart bm_george 0.5 blend.wav
dotnet run --project spike/KokoroProbe -- speak "Docking permission granted at Shinrarta Dezhra." af_heart out.wav
```

## Two things that will bite the next reader

**`speak` is the one that shows the problem.** A sentence of ordinary English comes out with exact
IPA from the dictionary. A sentence with a system name in it does not, and that gap — not the model,
not the licence — is what the phase turns on.

**`g2peval` scores the fallback on its own training data**, so its numbers are an upper bound rather
than an estimate. They are bad anyway, which is what makes them worth quoting.

**`quality` cannot rank the quantised builds, and saying so is the finding.** Every one of them
renders the same tokens to a *different number of samples* than fp32 does — the duration predictor
diverges, not just the waveform — so a sample-wise comparison has nothing to align. Only fp16 lines
up at all, at 6.5 dB. The WAVs are written for exactly this reason: the ear is the test left.

**And `bench` is a comparison, not a promise.** Its absolute figures move with whatever else the
machine is doing — the 2026-08-29 run for #139 read fp32 at ×4.4 where the 2026-08-28 spike read
×9.0 — while the *ordering* repeated across all three passes. Run it three times and trust the
order.
