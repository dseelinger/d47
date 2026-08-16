# spike/

Throwaway. Not in the solution, not referenced by anything in `src/`, not held to the
project's conventions. Each directory here exists to answer one question and is expected to
be deleted once the answer is written down.

| Directory | Question | Finding |
|---|---|---|
| `OverlaySpike` | Can Avalonia reach a SteamVR overlay through a shared D3D11 texture? | [docs/spikes/vr-texture.md](../docs/spikes/vr-texture.md) |
| `RefBypassProbe` | Can Avalonia 12's reference-assembly guard be bypassed? | same |
| `HotasProbe` | Can a desktop process read HOTAS switch positions, and what identifies a device? | [docs/spikes/hotas-switch-read.md](../docs/spikes/hotas-switch-read.md) |
| `tools/ApiDump` | What does an assembly actually expose? | supporting tool |
| `MaterialLineProbe` | Can the material trader's lines be parsed out of EDDiscovery's C#, and do they price real trades? | [docs/spikes/material-lines.md](../docs/spikes/material-lines.md) |
| `OperationsProbe` | What does a pre-engineered module look like in the journal, and do the blueprint sources know it? | [docs/spikes/operations-pre-engineered.md](../docs/spikes/operations-pre-engineered.md) |
| `ColonisationProbe` | Is `ColonisationConstructionDepot` a snapshot or a delta, can two sites be open at once, and is a claim visible to anybody? | [docs/spikes/colonisation-sources.md](../docs/spikes/colonisation-sources.md) |

`OverlaySpike/vendor/openvr_api.cs` is Valve's official binding, BSD-3-Clause, vendored
from `ValveSoftware/openvr`.
