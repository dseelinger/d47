# spike/

Throwaway. Not in the solution, not referenced by anything in `src/`, not held to the
project's conventions. Each directory here exists to answer one question and is expected to
be deleted once the answer is written down.

| Directory | Question | Finding |
|---|---|---|
| `GrabSpike` | Do controller poses move, does the trigger arrive, and does an absolutely-placed quad stay put? | open — v0.22.1 shipped a ray that does not track the hand |
| `OverlaySpike` | Can Avalonia reach a SteamVR overlay through a shared D3D11 texture? | [docs/spikes/vr-texture.md](../docs/spikes/vr-texture.md) |
| `RefBypassProbe` | Can Avalonia 12's reference-assembly guard be bypassed? | same |
| `HotasProbe` | Can a desktop process read HOTAS switch positions, and what identifies a device? | [docs/spikes/hotas-switch-read.md](../docs/spikes/hotas-switch-read.md) |
| `tools/ApiDump` | What does an assembly actually expose? | supporting tool |
| `MaterialLineProbe` | Can the material trader's lines be parsed out of EDDiscovery's C#, and do they price real trades? | [docs/spikes/material-lines.md](../docs/spikes/material-lines.md) |
| `OperationsProbe` | What does a pre-engineered module look like in the journal, and do the blueprint sources know it? | [docs/spikes/operations-pre-engineered.md](../docs/spikes/operations-pre-engineered.md) |
| `ColonisationProbe` | Is `ColonisationConstructionDepot` a snapshot or a delta, can two sites be open at once, is a claim visible to anybody, and what will the galaxy index say about a system nobody lives in? | [docs/spikes/colonisation-sources.md](../docs/spikes/colonisation-sources.md) |
| `ExobiologyProbe` | Does the mass code predict what pays, what is the first-footfall multiplier, and does `ScanOrganic` carry a position? | [docs/spikes/exobiology-sources.md](../docs/spikes/exobiology-sources.md) |
| `MiningProbe` | What does a prospector limpet report, and does Elite's own `Content` grade track what a miner cares about? | [docs/spikes/mining-callouts.md](../docs/spikes/mining-callouts.md) |
| `ElevenLabsProbe` | What voices does an ElevenLabs account offer, does a fresh one see any, and is the shared library a second source? | [docs/spikes/elevenlabs-voice-sources.md](../docs/spikes/elevenlabs-voice-sources.md) |
| `CorpusReplay` | Do Phases 17-19 survive 912 real journals, where does Elite spell one thing two ways, and which events in the corpus does nothing in d47 handle? | [docs/spikes/journal-corpus-soak.md](../docs/spikes/journal-corpus-soak.md) — **instrument, kept: since #270 it ends with the corpus subtracted from `HandledEvents`, which is the diff that finds the gaps nobody reported** |
| `HabitProbe` | What does Phase 32's miner actually claim about 914 real journals, and does each detector clear its own floor? | list.md Phase 32 — **instrument, kept: every figure in the plan and the changelog came out of it** |
| `LogbookProbe` | What does Phase 33's digest make of a real session, what would writing it cost, and does the finished log survive being read against the journal by hand? | list.md Phase 33 — **instrument, kept: it is the acceptance test the item asks for, and its dry mode spends nothing** |
| `RadioAudition` | Does `RadioVoice` actually sound like a radio? | 0.22.0 shipped from it — **instrument, kept: the question recurs every time the filter is touched** |
| `KokoroProbe` | Does Kokoro blend voices through the ONNX path from C#, what does it cost, and can d47 phonemise without the GPL one? | [docs/spikes/kokoro-through-onnx.md](../docs/spikes/kokoro-through-onnx.md) — **instrument, kept while #101 is open: it is where the pronunciation question gets re-measured** |
| `ChatterboxProbe` | Can Chatterbox's four ONNX graphs be driven from C# at all, what does a line cost against Kokoro, and does DirectML run it? | [docs/spikes/chatterbox-through-onnx.md](../docs/spikes/chatterbox-through-onnx.md) — **answered and kept while #293 is open: it is the instrument to re-run if Resemble ships a streaming decoder** |
| `TranscribeFloorProbe` | What does one transcription actually cost, and which part of it is the name-hint prompt? | [docs/spikes/transcription-floor.md](../docs/spikes/transcription-floor.md) — **instrument, kept: it is where the speech path gets re-timed, and it drives `WhisperTranscriber` itself rather than a copy** |

`RadioAudition` says a sentence through Edge Neural, optionally over the comms link, writes it as a
WAV and plays it. It exists because the one thing a test cannot assert about an audio effect is
whether it sounds right: `RadioVoiceTests` pins the band shape, the loudness match, the noise floor
and the determinism, and all of those were already passing when the tail was still too quiet to
hear. `--everywhere` plays on every active endpoint rather than the default, which is how to reach
somebody wearing a headset — the Windows default here is frequently something nobody is listening
to.

```
dotnet run --project spike/RadioAudition -- "Scanning." out.wav --radio --play
```

`OverlaySpike/vendor/openvr_api.cs` is Valve's official binding, BSD-3-Clause, vendored
from `ValveSoftware/openvr`.

`MirrorProbe` was here until 2026-08-18 and was deleted with Phase 22, which it was the instrument
for. The measurement it was built to take — can a world-space panel be located in Elite's desktop
mirror — was never taken, so it is not a probe that answered its question and was retired; it is one
whose question was withdrawn. It is recoverable from git if screen reading ever comes back.

**One thing it knew that outlives it**, since `spike/` is outside `d47.slnx` and therefore outside
`PackageLicenceGateTests`: a computer-vision spike must reference `OpenCvSharp4.runtime.win.slim` and
**never `OpenCvSharp4.runtime.win`**, which declares Apache-2.0 and packs an LGPL-2.1 FFmpeg binary
twelve lines below the declaration. No gate runs here, so that is a thing to know rather than a thing
that will be caught.
