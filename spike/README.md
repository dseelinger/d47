# spike/

Throwaway. Not in the solution, not referenced by anything in `src/`, not held to the
project's conventions. Each directory here exists to answer one question and is expected to
be deleted once the answer is written down.

| Directory | Question | Finding |
|---|---|---|
| `OverlaySpike` | Can Avalonia reach a SteamVR overlay through a shared D3D11 texture? | [docs/spikes/vr-texture.md](../docs/spikes/vr-texture.md) |
| `RefBypassProbe` | Can Avalonia 12's reference-assembly guard be bypassed? | same |
| `tools/ApiDump` | What does an assembly actually expose? | supporting tool |

`OverlaySpike/vendor/openvr_api.cs` is Valve's official binding, BSD-3-Clause, vendored
from `ValveSoftware/openvr`.
