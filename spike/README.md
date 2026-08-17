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
| `CorpusReplay` | Do Phases 17-19 survive 912 real journals, and where does Elite spell one thing two ways? | [docs/spikes/journal-corpus-soak.md](../docs/spikes/journal-corpus-soak.md) |
| `MirrorProbe` | Is there anything in Elite's desktop mirror to read — can a world-space panel be located in it at all? | [docs/spikes/mirror-panel-locatability.md](../docs/spikes/mirror-panel-locatability.md) — **method and instrument only; the measurement is untaken** |
| `RadioAudition` | Does `RadioVoice` actually sound like a radio? | 0.22.0 shipped from it — **instrument, kept: the question recurs every time the filter is touched** |

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

`MirrorProbe` is the one directory here that is not throwaway *yet*: it is the instrument for a
measurement that has not been taken, so it survives until a headset has been in front of it. It
references `OpenCvSharp4` and `OpenCvSharp4.runtime.win.slim` — **never `OpenCvSharp4.runtime.win`**,
which declares Apache-2.0 and packs an LGPL-2.1 FFmpeg binary. `spike/` is outside `d47.slnx` and so
outside `PackageLicenceGateTests`, which is why the reason is written beside the reference in the
csproj rather than left to a gate that does not run here. It writes captures to a gitignored
`captures/`, because they are pictures of somebody's game session.
