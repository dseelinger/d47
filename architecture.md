# TheApp — Architecture

**How** TheApp is built, and why those choices were made. What it does is described release by release in [CHANGELOG.md](CHANGELOG.md); nothing here restates that, and where a decision was forced by a particular phase, the phase is named.

Target platform is Windows 11 / .NET, C#. Elite Dangerous is the only game integration.

---

## 1. Constraints that drive everything

These come from the checklist, not from taste. Each one eliminates otherwise-reasonable options, so they are stated first.

| Constraint | Source item | What it rules out |
|---|---|---|
| Single statically linked binary, per-user install, no elevation | *TheApp installs on a clean machine* | CEF (~150–200 MB + subprocess model), any kernel driver, anything needing an MSI or admin rights |
| One widget tree renders to both desktop and VR | *TheApp's panel works in VR* | Two UI codebases; a VR surface that is a screenshot of the desktop window |
| Permissive licenses only, no copyleft | Established during Whisper selection | FFmpeg (GPL builds), anything LGPL that resists static linking |
| Every input path answerable with zero capabilities | *Capabilities as state, not guard* | Hard dependencies on the LLM, TTS, or network anywhere on the turn path |
| No telemetry leaves the machine | *TheApp says what went wrong* | Crash reporters, analytics SDKs, hosted logging |
| Testable with no game, headset, or hardware | *Journal behavior is testable without the game* | Any component that owns its own thread or clock |
| Safety-critical rows unreachable by the model | *Protect safety-critical settings from the model* | A single settings-mutation tool the LLM can call |

**The first constraint is the sharpest.** "Single statically linked binary" plus "no copyleft" is what kills the CEF-based HTML overlay outright, and it also constrains what can be done in .NET — see §9.

---

## 2. Stack

| Concern | Choice | License | Rationale |
|---|---|---|---|
| UI framework | **Avalonia** | MIT | The only mature .NET UI toolkit that renders the same visual tree to a window *and* to an offscreen surface. Satisfies "one widget tree" directly. |
| VR compositing | **OpenVR** (`IVROverlay`) via `openvr_api` | BSD-3-Clause | Elite never calls OpenXR; SteamVR overlays composite over any VR app without touching its process. |
| Speech-to-text | **Whisper.net** + `Whisper.net.Runtime` | MIT (whisper.cpp + ggml MIT) | No FFmpeg anywhere in the graph. |
| Audio capture / render | **NAudio** (WASAPI) | MIT | Mic capture, resampling to 16 kHz mono, and the single output arbiter. |
| Echo cancellation | **WebRTC AEC3**, via `SoundFlow.Extensions.WebRtc.Apm` | BSD-3-Clause native, MIT wrapper | Consumes the arbiter's render reference tap. The wrapper is the only .NET route to AEC3 on NuGet and drags `SoundFlow` along unused; NAudio still owns capture and render. Weighed in Phase 13 against hand-writing an adaptive filter and a delay estimator, which is a specialist's month and then a lifetime of speaker nonlinearity and two clocks. |
| Text-to-speech | Edge Neural (free) / ElevenLabs (paid) | provider terms | Per-role selection; see *ElevenLabs*. |
| LLM | **Anthropic SDK** (`Anthropic` NuGet) + an OpenAI-protocol client, default `claude-opus-5` | MIT (SDK) | Behind an `ILlmProvider` seam — OpenAI and third parties speaking its protocol are first-class; see §6. |
| Logging | **Serilog** | Apache-2.0 | Two sinks: human-readable and structured JSON. |
| Secrets at rest | **DPAPI** (`ProtectedData`) | OS | Per-user, no key management. |

Licenses should be re-verified against the resolved transitive graph at first build — the copyleft problem that motivated the Whisper choice lived in a transitive dependency, not a direct one.

---

## 3. Component map

```mermaid
graph TB
    subgraph Sources["Untrusted input"]
        J[Journal + Status.json]
        MIC[Microphone]
        NET[INARA / Spansh / Web search]
    end

    subgraph Core["Core - no UI, no hardware"]
        JR[Journal Reader<br/>pull-based, no thread]
        GS[Game State]
        REG[Capability Registry<br/>descriptors, tool profiles]
        TURN[Turn Loop<br/>LLM or keyword router]
        SET[Settings + Secret Store]
    end

    subgraph Voice["Audio"]
        GATE[Gate Policy<br/>PTT / toggle / VAD / wake word]
        STT[Whisper.net]
        ARB[Audio Arbiter<br/>one queue + reference tap]
        TTS[TTS Provider]
        AEC[AEC3]
    end

    subgraph Out["Surfaces"]
        UI[Avalonia widget tree]
        WIN[Desktop window]
        VR[OpenVR overlays<br/>panel + captions]
        KEY[Key Injector<br/>SendInput scancodes]
    end

    LLM[ILlmProvider<br/>Anthropic / OpenAI-protocol]

    J --> JR --> GS
    MIC --> AEC --> GATE --> STT --> TURN
    GS --> TURN
    REG --> TURN
    TURN <--> LLM
    NET --> TURN
    TURN --> ARB --> TTS
    ARB -.render reference.-> AEC
    ARB --> VR
    TURN --> KEY --> ELITE[Elite Dangerous]
    GS -->|proactive callouts| ARB
    GS -->|autonomous actions, per-action opt-in| KEY
    GS --> UI
    UI --> WIN
    UI --> VR
    SET --> REG
```

Dependency direction is one-way: **Core knows nothing about Voice, Surfaces, or providers.** That is what makes the replay harness possible — Core can be driven end to end by a recorded journal with no audio device, no headset, and no model.

Two edges deliberately bypass the model. Journal-triggered callouts (`GS → Arbiter`) fire on the event and never at the model's discretion — an alert that depends on a turn completing is not an alert. Autonomous actions (`GS → Injector`) fire a game input with nobody asking; each is off by default behind its own opt-in, the honk being the first member. The keyword router inside the turn loop is the model-free command path — which is why "no model" still leaves every input answerable.

---

## 4. Process and thread model

One process. Four owners of work:

| Owner | Threading | Notes |
|---|---|---|
| **UI / dispatcher** | Avalonia UI thread | Owns the widget tree. VR texture submission is marshalled here or to a dedicated render thread — never called from arbitrary threads. |
| **Tick loop** | One timer-driven loop, ~4–10 Hz | Polls the journal reader, recomputes game state, fires journal-triggered callouts. |
| **Audio** | WASAPI capture + render callbacks | Real-time constrained. Never blocks on the model, disk, or network. |
| **Turn execution** | `async`/`await` on the thread pool | Model calls, tool execution, TTS synthesis. |

**The journal reader owns no thread and no timer.** It exposes `Poll()` and returns the events read since the last call. The tick loop calls it in production; the replay harness calls it in a tight loop at 100×. This is the single design decision that makes *Journal behavior is testable without the game* achievable rather than aspirational.

File handle: open with `FileShare.ReadWrite | FileShare.Delete` — Elite holds the journal open, and a plain `File.OpenRead` will fail intermittently.

---

## 5. Key decisions

### D1 — Avalonia offscreen, not an embedded browser

**Decision.** Render the Avalonia visual tree to an offscreen surface and submit the pixels to `IVROverlay`. *Amended in Phase 12: the submission is `SetOverlayRaw`, not a shared D3D11 texture — see below.*

**Status: proven end to end.** See [docs/spikes/vr-texture.md](docs/spikes/vr-texture.md) — a live Avalonia panel visible as a SteamVR overlay, measured. The amendments below come out of that spike.

**Why not CEF.** ~150–200 MB and a multi-process model. Incompatible with "a single statically linked binary" and with per-user install without elevation.

**Why not WebView2.** No supported offscreen-to-texture path; the workaround is DirectComposition plus Windows.Graphics.Capture, which needs a live window and reintroduces the desktop-window dependency that *Keep working when the main window is minimized* is trying to remove.

**Why not capture the desktop window.** WGC requires a non-minimized window, which directly contradicts the minimize requirement, and it makes the VR surface a strictly-worse copy of the desktop one rather than a peer.

**"One widget tree" means one *definition*, not one live instance.** A `Visual` belongs to exactly one visual tree, so the desktop window and the VR surface each get their own instantiation of the same view, bound to the same view model. The constraint is still satisfied — there is no second UI codebase, and no path where the VR surface is a screenshot of the desktop one — but the phrase is shorthand and the framework will not do the literal thing.

**Consequence — mini mode.** Mini mode is a `DataTemplate` selection over the same view models, not a second surface. That falls out of the decision rather than needing enforcement.

**Consequence — the copy is a CPU roundtrip, and that is fine.** Zero-copy GPU→GPU is *not* reachable: Avalonia 12 closes `ITopLevelImpl` and the platform render surfaces to external implementation via reference-assembly guards, `RenderTargetBitmap` is a CPU raster surface, and `ICompositionGpuInterop` is import-only. Measured at panel size (1024×640), the whole end-to-end frame costs **0.75 ms live against SteamVR** — of which the Skia rasterise is 0.30 ms and the copy itself is 0.05 ms. At 4–10 Hz that is under 1% of one core, so the roundtrip is a non-issue rather than a compromise, and the fast path is not worth reaching for.

**Consequence — the pixels go over as raw bytes, not as a texture.** *Amended in Phase 12.* The original decision named a shared D3D11 texture and `SetOverlayTexture`: Avalonia rasterises to a `RenderTargetBitmap`, `CopyPixels` writes into a mapped staging texture, `CopyResource` moves it to a shared one. That path was built, and the overlays were **invisible in the headset while SteamVR reported them visible** — for a day, against five separate explanations that each turned out to be wrong. Every part of it that could be checked, checked out: the CPU→GPU copy was proven pixel-for-pixel by test, the device landed on the adapter `GetDXGIOutputInfo` asked for, the shared texture carried `MiscFlags.Shared`, `ETextureType.DirectX` was given the `ID3D11Texture2D*` and not the share handle, and no OpenVR call on the path ever returned anything but `None`. The rasterise itself was then ruled out by writing the first frame of a live session to `data/vr-PanelFull.png` and looking at it — a correct, fully styled panel.

So the texture is gone and `SetOverlayRaw` takes its place: OpenVR is handed a pinned buffer and does its own upload. This was never weighed and rejected — it was not considered — and D1's own reasoning argues for it. If zero-copy is unreachable and the roundtrip is a non-issue, then the shared texture is buying nothing but the 0.05 ms `CopyResource` it replaces, and it costs a graphics device, an adapter match, a share flag, the Vortice dependency, and **one graphics device per process** — a trap that took the runtime down from inside `vrclient` in a previous implementation. What it costs instead is a full-surface copy inside OpenVR per submit, and submits are per change rather than per frame.

Two things the raw path has to get right, both of which fail as a wrong picture rather than as an error. The buffer is **RGBA**, and Avalonia rasterises **BGRA** — `VrPixels.ToRgba` converts, and skipping it swaps red and blue while every call still succeeds (a previous project of Doug's does exactly that, which is a fair part of why its overlays are remembered as working "semi-OK"). And the rows are **unpadded**: `SetOverlayRaw` derives the stride from the width and the bytes-per-pixel, so a padded buffer is read as a sheared image.

**Consequence — the VR path needs a window, and never shows it.** *Amended in Phase 9; the spike's claim was true of what the spike rendered and false of a real view.* The spike proved a detached `Visual` rasterises correctly, using a hand-built tree of borders and text blocks carrying literal brushes. A real view is neither. A `UserControl` is a **templated** control: its template comes from a control theme, control themes arrive through styling, and styling only runs for an element attached to a logical tree with a root. Detached, the real panel measures to 0x0, materialises **one** visual against 51 hosted, and rasterises as an empty rectangle — no exception, no warning, just a blank quad in the headset. `DynamicResource` fails the same way and for the same reason: the lookup walks the logical tree and there is none, so every themed brush resolves to unset.

So the offscreen surface hosts the view in a `Window` that is **constructed and never shown** (`OffscreenSurface`). It has no desktop presence, no taskbar entry, and nothing the Commander can reach.

Minimise-safety is unaffected, and it is worth being precise about why, because the original wording put the weight in the wrong place. It never really rested on there being no window; it rests on the VR path not depending on the state of the window the Commander *can* see. A window that is never shown has no state to depend on. What the spike genuinely closed stands: there is no capture of a desktop surface anywhere in the chain, and the VR panel is a peer of the window rather than a picture of it.

It also means animations have no clock — anything moving on the VR panel must be driven by the tick loop, which is the intended view-model-driven model anyway.

**One thing to get right from the spike**, and it survives the amendment: render only on change, since the panel is view-model-driven and the measured cost is a worst case rather than a target. The spike's other instruction — create the D3D11 device on the adapter `IVRSystem.GetDXGIOutputInfo` names, rather than passing `null` and taking the system default, which on a hybrid-graphics machine can be the iGPU — went with the texture it was protecting.

### D2 — OpenVR overlays, not OpenXR

Elite renders through OpenVR. `XR_EXTX_overlay` is barely implemented across runtimes. `IVROverlay` composites out-of-process, which is also what keeps TheApp clear of anything resembling game injection.

Two content handles, not three, plus two for the aim. *Amended in Phase 9, and again 2026-08-17
when the aim stopped being SteamVR's to draw.*

| Handle | Locking | Input |
|---|---|---|
| Panel | Head- or world-locked, per *VR Panel locking*. `Full` and `Mini` are content modes of it, each with its own placement | Ray-cast and carried by the trigger |
| Captions | Head-locked, fixed | None — output only |
| Beam | Follows the aiming hand | None — it *is* the input, drawn |
| Cursor | Sits on the hit point | None |

The beam and the cursor are separate handles rather than pixels in the panel for two reasons: they
move at headset rate while the panel repaints a few times a second, so compositing them in would
drag the panel's whole texture along at 90 Hz; and a cursor stamped into the texture is a texel
whose real 3D position only SteamVR knows, which leaves the beam with nothing to stop on. Both fail
soft — no beam and no cursor is a panel that can still be carried, just unguided.

The original table gave mini its own handle. *TheApp's panel works in VR* is explicit that "Mini is a mode of the same panel — a reduced content set — not a separate surface or a scaled-down copy", and that line is the acceptance criterion, so mini is a mode on the view model and the two modes share one handle and one quad. They do **not** share a size: apparent text size in a headset is the texture's pixel count and the quad's width in metres together, so mini is a smaller image at a smaller width, not the same image hung nearer. Each mode therefore carries its own placement, which is what the "same transform family" row was reaching for.

Captions stay a separate handle precisely so *Overlay Positioning & Look* cannot accidentally apply to them.

**Controller input is `IVRInput`, not overlay mouse events.** *Amended 2026-08-17, reversing what
this section said for three phases.*

The overlay mouse channel — `SetOverlayInputMethod(Mouse)` plus
`MakeOverlaysInteractiveIfVisible` — opts the quad in to **SteamVR's own laser**, and SteamVR only
runs that laser over **its own dashboard**. With Elite holding the headset,
`PollNextOverlayEvent` returns nothing, forever: no pointer, no grab, no error. It works perfectly
with the game closed, which is exactly what made three implementations believe it worked at all.
Measured rather than reasoned, in COVAS++ (`#264`), whose smoke test passes with Elite closed and
does nothing with it running.

This section previously recorded that an `IVRInput` manifest had been "built twice, accepted, and
never went live", and concluded the mouse channel was the only road. Both attempts were missing the
same step, and its absence is silent:

- **Register the application.** `AddApplicationManifest(path, temporary: true)` then
  `IdentifyApplication(pid, appKey)`. SteamVR files bindings under an application key; a process it
  does not recognise has none, so there is nothing for a binding to attach to. The action manifest
  still loads, the handles still resolve, and the action stays bound to nothing forever. It does not
  appear under Manage Controller Bindings either, so it cannot be fixed by hand. The only place that
  says so is `vrserver.txt`.
- **Activate the set at overlay priority.** `k_nActionSetOverlayGlobalPriorityMin`. At priority 0 the
  set loses to the running scene application and receives nothing. An action set is active only for
  the frame it is passed to `UpdateActionState`, so *not* calling it is the release.
- **And only while it is wanted.** Claimed while a ray is on the panel or a carry is running, and
  not otherwise: Elite does not bind motion controllers, but Virtual Desktop and the SteamVR
  dashboard do, and holding global priority for a session takes them hostage. The tell is physical —
  the trigger's own haptic tick stops.
- **Declare `oculus_touch` *and* `rift`.** The Oculus driver asks for each profile in turn and one
  missing binding disables input entirely: `[Input] ... (rift) has no configured binding. Input will
  not be available`.

`IVRSystem.GetControllerState` remains dead for controllers that are connected and tracking, and
that part was always right.

**Two consequences follow from losing the laser.**

- **The ray is ours to cast, before the button is read.** Whether a ray is on the panel is what
  decides whether we claim the controllers at all, so position and claim come out of one
  intersection each frame. `ComputeOverlayIntersection` is not enough: on a curved overlay it was
  measured returning true for every ray with `vUVs` stuck at `(0, 1)`, so it cannot say *where*.
  d47 casts its own in `D47.Core/Vr/VrRay.cs`, flat and cylindrical, which also puts it where a test
  can reach it without a headset.
- **The aim has to be drawn.** SteamVR is no longer drawing it, and a panel you cannot see yourself
  pointing at is one you cannot grab. A beam overlay along the aim and a cursor overlay at the hit
  point, both separate handles: the beam moves at headset rate while the panel repaints a few times
  a second, and the cursor has to be a real 3D object or the beam has nothing to stop on. One
  distance feeds both, so they meet by construction whatever the curvature model gets wrong.

**A controller's reported pose is the grip, not the tip.** OpenVR reports the pose inside the
handle; on Touch controllers the tip differs by a large angle, enough that a ray cast from the grip
lands nowhere near where the Commander is aiming. The correction is read out of the render model —
`Prop_RenderModelName_String`, then `GetComponentState` on `tip`, falling back to `openxr_aim` and
then to identity — never hardcoded, because it differs per controller.

**`trackedDeviceIndex` on an overlay event is not the hand** — measured as
`k_unTrackedDeviceIndexInvalid` against tracked devices 1 and 2. Which hand grabbed is recovered
from the nearest ray hit, and resolved **only at the press**: re-deriving it mid-carry would let a
flicker in the curvature model hand the panel to the other hand, or drop it.

**Grabbing is one frozen offset.** `offset = hand⁻¹ · overlay` at the press, `overlay = hand · offset` every frame after, always measured from the grab origin rather than accumulated from the last answer — accumulating makes the overlay's speed a function of how often it is asked, which reads as broken tracking rather than as wrong arithmetic. Nothing re-faces the panel at the Commander while it is held: a panel forced upright and square cannot be tilted to read from below, which is most of what moving one is for.

**Conventions, each of which is invisible until it is not.** `HmdMatrix34_t` is column-vector where `System.Numerics.Matrix4x4` is row-vector, so the rotation transposes on the crossing — and the error is invisible for any placement square to the view, so the test that matters rotates on all three axes. An overlay quad faces its own **+Z**; a controller and the head face **-Z** — and this was written here correctly while `VrPlacementMath.Resting` shipped with its pitch sign inverted against it, tilting a knee-height panel to face the floor. Assert on the direction a face ends up pointing, never on the angle: the angle is the right size either way, which is why a test measuring `-Z` agreed with the bug. A tracked pose is only real if `bPoseIsValid` **and** `bDeviceIsConnected`: an all-zero device slot converts to a perfectly valid-looking unit quaternion at the origin, so an unchecked slot is a grabbed panel dragged to the Commander's feet the moment a controller sleeps. Quaternions out of a tracking runtime drift off unit length over a session and are normalised at the boundary.

**Lifecycle, beyond what the state machine already says.** `VR_Init` is called once per process and nothing in OpenVR refuses a second call, so the refusal lives in our code. `EVROverlayError.KeyInUse` means another copy of d47 owns the key, not a generic failure, so overlay keys are claimed before anything expensive is built. Recovery from a mid-session SteamVR restart rebuilds every handle rather than repairing any — the first refused call unwinds, the session is dropped, and the next poll starts clean. `ClearOverlayTexture` precedes `DestroyOverlay`, because an overlay outliving the device that owns its image leaves SteamVR querying a destroyed one.

**Lifecycle.** SteamVR may start after TheApp, exit under it, or restart mid-session. The overlay subsystem is a state machine (`Unavailable → Connecting → Active`) polled from the tick loop, and every handle is re-created on reconnect. *Order agnostic Overlay* is this state machine; there is no initialization ordering to get right.

**The readiness signal is `VR_Init` succeeding, not SteamVR running.** The spike hit `VR_Init` returning `Init_HmdNotFound` while `vrserver.exe` was already up: "SteamVR is running" and "an HMD is present and usable" are different conditions, and the process is not evidence of the second. The state machine must therefore poll `VR_Init` and treat its return code as the transition, never a process check — otherwise `Connecting` latches on a machine that will never produce a headset.

**The binding is vendored, not packaged.** There is no canonical Valve NuGet package for OpenVR. Valve's official `openvr_api.cs` is BSD-3 and is vendored in-tree; a stale third-party binding fails as an `FnTable` vtable mismatch — wrong function called, no error — rather than as anything diagnosable. The spike's copy under `spike/` is the one to promote.

**Re-anchor.** Elite's recenter is invisible to SteamVR. Re-anchor reads `GetDeviceToAbsoluteTrackingPose` for the HMD, computes the delta against the stored anchor, and applies it to every world-locked handle as a group so relative layout survives.

### D3 — Whisper.net + NAudio, no media framework

`WasapiCapture` → float PCM → `MediaFoundationResampler` to 16 kHz mono → `Whisper.net`. Live mic means no container to demux and no codec to decode, so FFmpeg has no reason to exist in the graph.

GPU is opt-in and defaults off when a headset is present — see *STT Model Choice*. The runtime package (`Whisper.net.Runtime` vs `.Cuda`) is selected at load time, not compile time, so the toggle does not require a reinstall.

### D4 — Key injection: scancodes, no driver, no hook

`SendInput` with `KEYEVENTF_SCANCODE`. Three rules, all load-bearing:

1. **Never install a low-level keyboard hook.** A `WH_KEYBOARD_LL` hook is a global input chokepoint; a stall in TheApp becomes a stall in the Commander's controls.
2. **`release_all()` is unconditional** — `try/finally`, plus on app exit and on focus loss. A stranded key here is a throttle that will not stop.
3. **Verify Elite is foreground before injecting.** `GetForegroundWindow` against the cached Elite HWND. A voice command must never type into a browser.

Virtual joystick drivers (vJoy, ViGEmBus) were considered and rejected: a kernel driver contradicts "per-user install, no elevation" and is a support burden disproportionate to the benefit.

**Two deliberate widenings of “scancodes only”, added in Phase 10, each with its reason. Neither touches the three rules above.**

1. **Mouse buttons go through the same `SendInput` call.** Elite’s own default keyboard preset, `KeyboardMouseOnly`, binds `PrimaryFire` to `Mouse_1` and `SecondaryFire` to `Mouse_2` — and the discovery scanner has no binding of its own, because it fires as a fire-group weapon. A keyboard-only injector therefore cannot honk on the setup most Commanders start with, which would make *TheApp honks when you arrive in a system* ship inert on a default install. Buttons are subject to the same foreground check and the same unconditional `release_all()`.
2. **Chat text uses `KEYEVENTF_UNICODE`.** A scancode is a physical key position, and the character it produces depends on the Commander’s keyboard layout: sending a system name by scancode types something else entirely on AZERTY. Unicode injection is layout-independent and Elite’s chat field accepts it. This applies to *text* only — every binding, action and macro step still goes as a scancode, because that is what DirectInput reads.

Binds are **read-only**. The parser produces a map of action → device + key, which feeds both *Know which actions the Commander can actually reach* and *Report a key that is bound twice*. TheApp never writes the Commander's bindings.

**Resolving which file to read is the part that is easy to get wrong.** There are three traps, and the naive version of this walks into all of them:

1. **The active preset is named in `StartPreset.<major>.start`, not assumed.** On the development machine that file reads `KeyboardMouseOnly` — a preset shipped with the game, not a custom one. Reading `Custom.*.binds` unconditionally would parse a file the Commander is not using and confidently report the wrong keys.
2. **A built-in preset does not live in the user profile.** `%LOCALAPPDATA%\Frontier Developments\Elite Dangerous\Options\Bindings\` holds custom presets; the shipped ones live under the game's install directory. Both paths have to be searched.
3. **The version suffix moves.** The custom file on that machine is `Custom.4.2.binds` while the start-preset file is still `StartPreset.4.start`, so the two numbers are not the same number and neither may be hardcoded. Match by pattern and take the highest version present.

A Commander who has never customised their binds is the common case, not an edge case, and it is exactly the case a hardcoded `Custom.4.0.binds` fails on — silently, by finding nothing or by finding a stale file, and then advertising an action set that does not match the keyboard in front of them.

### D5 — Capability registry as the single source

One `CapabilityDescriptor` per capability, registered once at startup and never mutated. It declares: identity, tool schemas, help text, settings rows, display model, and documentation page slug.

Five things are **projections** of the registry rather than parallel structures:

- Tool schemas sent to the model
- Settings rows rendered in the UI
- Spoken help (*Answer what can you do, from the registry*)
- The keyword router's vocabulary
- The docs CI gate (*Every capability has a documentation page*)

Immutability is not stylistic. It is what makes tool schemas byte-identical across turns, which is what makes prompt caching work — see §6.

### D6 — Two stores, atomic writes, DPAPI

Settings and secrets are separate stores because one loader cannot both fail loudly and shrug: a corrupt settings file should surface as an error, a missing secret should surface as a capability being off.

Writes go to a `.writing` sibling then `File.Move` with overwrite — atomic on NTFS. Secrets are DPAPI-encrypted with `DataProtectionScope.CurrentUser` — which is why the install is per-user. The single writable folder beside the executable is the checklist's portability decision; DPAPI works wherever the file lives.

### D7 — One audio arbiter

A single priority queue in front of WASAPI render. Every audible thing — speech, cues, thinking bed, music, ambience — enters through it. Ducking, interruption, supersede, and caption timing are all properties of the queue rather than separate mechanisms.

The arbiter exposes a **render reference tap** from day one: a copy of the mixed output buffer, timestamp-aligned, for AEC3 to subtract from capture. Retrofitting this later means opening the one component every voice path depends on.

*Collected in Phase 13.* It was one subscription, and the arbiter did not change. `EchoCanceller` sits between the microphone and the gate as an `ICaptureSink`, takes the tap on the render thread and capture on the capture thread — which is the arrangement the APM holds separate locks for — and is otherwise framing: WASAPI hands over whatever buffer size it likes on both sides and the module takes exactly 10 ms, with the remainder carried rather than padded, since padding tells a canceller the Commander stopped talking several times a second. The rejected alternative is worth recording beside FFmpeg and cloud STT: a **loopback capture** is a second WASAPI stream, opened on a device that may not be the one d47 is rendering to, arriving late with a clock of its own.

`Shut up` is a queue operation (flush + stop current), not a feature layered on top. That is why it can be instant.

---

## 6. LLM integration

The turn loop talks to an **`ILlmProvider` seam**, never to a vendor SDK directly. Two implementations ship: **Anthropic** (the `Anthropic` NuGet) and **OpenAI-protocol** — which covers OpenAI itself and every third party speaking its API (OpenRouter, Ollama, LM Studio, vLLM). The seam owns tool-schema translation (Anthropic `input_schema` vs OpenAI `parameters`) and reports what the active endpoint supports into *Capabilities as state*, so a feature the provider lacks is a capability that is off rather than a failure to handle. Changing the endpoint resets the model list to that endpoint's namespace (*Show the controls the active provider actually has*).

> **Amended 2026-08-18, and scheduled as Phase 29.** "Two implementations ship" has been written in the present tense since Phase 1 and only one of them exists. It stays as the design, and the paragraph below it was the plan. Phase 29 shipped; see `CHANGELOG.md`.
>
> The seam held. `ILlmProvider.Id` was documented as *"`anthropic`, `openai`"* from the start, `AcceptsCustomEndpoint` says in its own comment that it exists for the OpenAI-shaped providers, `ModelsFor` already empties the model list for an address d47 does not know, and the settings rows are generated from the catalog — so a second entry draws its own rows and the surface costs nothing. What did **not** hold is four places where Anthropic's arithmetic is the general case: `LlmUsage.TotalInputTokens` sums three counts because Anthropic's `input_tokens` excludes the cached part and OpenAI's `prompt_tokens` includes it; `ModelPrice` derives cache rates at 1.25x and 0.1x of input, which are Anthropic's terms and not OpenAI's; `SpendTracker`'s cold-prefix detector watches for a cache **write** that OpenAI never reports, so it would read as caching being perfect rather than as not measuring; and `NeedsKey` is derived from a secret name existing, which makes a local server with no account unreachable by construction rather than by decision. Each is wrong silently, and three of them produce a plausible number.
>
> One sentence above is also now too strong. **The protocol split is per entry rather than one client**: the OpenAI entry speaks the Responses API and the compatible entry speaks Chat Completions, because server-side web search is a named entry in the tools array wherever it exists and the address decides whether it can be reached — xAI moved `x_search` to Responses and deprecated `search_parameters` on Chat Completions on 2026-01-12, and OpenAI's `web_search` is likewise a Responses tool. So the seam owns two translations rather than one, and the schema note in the paragraph above describes only the Chat Completions half; Responses declares a tool flat rather than nested under `function`.

> **Amended again 2026-08-18, on building it.** Phase 29 shipped, and **two of the four seams above were described wrongly** — in the safe direction, because the plan required the published rates to be read at implementation time rather than written from memory, and reading them is what caught it.
>
> **OpenAI bills cache writes, and reports them.** The plan said it charges nothing to write an entry, which was true before the GPT-5.6 family and is not true now: writes bill at **1.25x uncached input** — the same factor as Anthropic — and arrive as `cache_write_tokens` beside `cached_tokens`. Their worked example is three figures rather than two, so `prompt_tokens` includes the written part as well as the read part, and a conversion subtracting only the read half would have billed those tokens at the uncached rate *and* at the write rate. `LlmUsage.FromInclusiveInput` takes both counts. The per-row cache factors were still needed — the older models discount reads by 0.5 rather than 0.1 — but for a different reason than the one recorded.
>
> **The cold-prefix detector can fire after all**, on GPT-5.6 and later, since a write is reported there. It was rebuilt anyway, and had to be: the fix is not "add the inverse signal" but "stop the counter meaning one provider's evidence". `SpendTracker` now takes a `PrefixWarmth` the caller computes, and reports `UnmeasuredPrefixes` beside `UnexplainedColdPrefixes` so a zero can be told from a zero nobody was in a position to measure. The inverse signal — nothing read when the prefix had not changed — is what covers Chat Completions, guarded by the minimum cacheable prefix so a turn too short to cache is not counted as a regression.
>
> The other two seams were exactly as described. A fifth item was added during the work: `Demotable`, the closed set of request fields an endpoint may refuse, which is how *advertise, then demote* stays a fixed list rather than a search.

Default: Anthropic, `claude-opus-5`, adaptive thinking. Effort is chosen **per turn**, not per install — *Model Level and Thinking* has the router gauging low through max, never "off" unless the LLM itself is set to none.

The response is **streamed**: the checklist's largest perceived-latency win — *"synthesis is sentence-chunked so speaking starts at the first sentence boundary"* — only exists if sentences leave the model before the completion finishes.

```csharp
using Anthropic;
using Anthropic.Models.Messages;

var parameters = new MessageCreateParams
{
    Model = "claude-opus-5",
    MaxTokens = 8192,
    Thinking = new ThinkingConfigAdaptive(),
    OutputConfig = new OutputConfig { Effort = effortForThisTurn },
    System = new List<TextBlockParam>
    {
        new() { Text = personaAndGuardrails,
                CacheControl = new CacheControlEphemeral() },
    },
    Tools = profile.Tools,
    Messages = history,
};

await foreach (RawMessageStreamEvent ev in client.Messages.CreateStreaming(parameters))
{
    if (ev.TryPickContentBlockDelta(out var delta) &&
        delta.Delta.TryPickText(out var text))
    {
        // Completed sentences are enqueued on the audio arbiter as they close,
        // so speech starts at the first sentence boundary, not at end of turn.
        sentenceSplitter.Push(text.Text);
    }
}
```

The caching mechanics below are Anthropic-specific; the **ordering discipline is not**. OpenAI-protocol endpoints cache automatically on a prefix match with no breakpoints to place, so the same assemble-by-volatility order is what earns their cache hits too. Assembly is provider-neutral; only breakpoint placement lives behind the seam.

### Prompt assembly order

The API renders `tools` → `system` → `messages`, and caching is a **prefix match**: any byte change invalidates everything after it. Assembly is therefore ordered strictly by volatility.

| Position | Content | Volatility |
|---|---|---|
| 1 | Tool profile schemas | Per mode, from a closed set |
| 2 | Anti-invention guardrails | Never |
| 3 | Persona block | Per persona selection |
| 4 | Commander's About Me | Per session |
| 5 | Remembered facts about the Commander | Rarely, and never per turn |
| 6 | ← **cache breakpoint** | |
| 7 | Conversation history | Per turn |
| 8 | Live game state | Per turn |

**Amended 2026-08-18, Phase 31.** Position 5 is new and the breakpoint moved down one row to make
room for it. It is above the breakpoint deliberately and the obvious placement is the wrong one:
game state is where *changing* things go, memories change rarely, so paying for them once and reading
them cached is strictly cheaper — and a block that moved every turn would invalidate the whole prefix
rather than a tail of it, taking ~39,000 bytes of tool schema cold with it every time. What makes that
safe is that the rendered text carries no system, no ship and no live figure, and the owner assigns it
only when the bytes actually change (`MemoryRecall`, `AppHost.ApplyRecall`). A cache miss then happens
when the Commander flies somewhere they have history, which is the turn where the memory is the reason
they wanted it.

**Guardrails sit above the persona**, which is what makes *Personality on/off* safe: switching the persona off truncates block 3 and invalidates from there down, but the guardrails are in the cached region and are structurally unremovable by any setting.

### Tool profiles and the caching conflict

Tool definitions serialize first, so a per-turn change to the tool set invalidates the *entire* prefix — not a tail of it. Mode-gated advertisement and budget-gated selection both want to mutate the tool set per turn. The resolution in *Offer only the actions that work right now* is to quantize: a closed enumeration of profiles (on-foot / SRV / supercruise / normal-space / no-game), each byte-identical every time it ships. Four or five cache entries, each warm for as long as the Commander stays in that mode.

There is now a first-class alternative worth evaluating: **mid-conversation tool changes** (beta `mid-conversation-tool-changes-2026-07-01`, Claude Opus 5 onward). Tools are declared up front with `defer_loading: true` and surfaced later via `tool_addition` / `tool_removal` blocks on a `{"role": "system"}` message — the tool set changes without invalidating the cached prefix. If the C# SDK exposes these block types, it collapses the profile enumeration into a single warm prefix and removes the mode-transition cache miss entirely. **Verify SDK support before designing around it** — typings for these blocks lag the API in several SDKs.

### Live game state without invalidation

Game state changes every turn and must not sit in the cached region. Two options, in preference order:

1. **A `{"role": "system"}` message appended to `messages[]`.** Preserves the cached prefix, and carries operator authority rather than arriving as user text — which matters because journal content is untrusted (§7). Model-gated: supported on Claude Opus 5 and Opus 4.8, **not** on Sonnet 5, and varying across OpenAI-protocol endpoints — so it slots naturally into *Capabilities as state*: where the active provider lacks it, the capability is off and path 2 is used.
2. **A `<system-reminder>` text block in the user turn** — same caching profile, weaker trust signal. This is the fallback path.

### The 20-block lookback

Each cache breakpoint walks back at most **20 content blocks** looking for a prior entry. An agentic turn that fires several tools produces a `tool_use` + `tool_result` pair each, and a multi-tool turn can blow past 20 blocks in one exchange — after which the next turn's breakpoint silently finds nothing and the whole prefix is re-billed.

Mitigation: place an intermediate breakpoint roughly every 15 blocks during long tool sequences. The budget is 4 breakpoints per request, so this competes with the system-prompt breakpoint — spend them deliberately.

### Other invalidators to design around

- **Switching model or provider invalidates everything** (caches are model-scoped). A Commander changing providers mid-session pays a cold prefix; that is expected and should not be reported as a regression.
- **Minimum cacheable prefix is model-dependent** — 512 tokens on Claude Opus 5, 1024 on Opus 4.8 and Sonnet 5, higher on older models. A short system prompt silently will not cache: no error, just `CacheCreationInputTokens: 0`.

  **Measured 2026-08-12, and currently biting.** A first live turn on `claude-opus-5` reported 472
  input tokens with `CacheCreationInputTokens: 0` — the cached region is the ~409-token guardrails
  block alone, because position 1 (tool schemas) is empty and position 3 (persona) is null until
  Phases 10 and 11. Nothing caches yet, exactly as this bullet warns, and the cost arithmetic
  confirms it independently: the turn priced at plain input rates rather than the 1.25x cache-write
  rate. The prefix crosses 512 on its own once tools are advertised and a persona block is present,
  so the fix is those phases arriving rather than anything here. Padding the prompt to reach the
  threshold would be inventing prompt content to satisfy a metric.

  **It is the cached region that must clear the minimum, not the request.** A later turn in the same
  session reported 1020 input tokens and still cached nothing, because conversation history sits
  *below* the breakpoint — it can grow without bound and will never make a short system block
  cacheable. Watching total input as the signal is the easy mistake here, and it reads as "caching
  is broken" when the truth is "there is not yet enough above the breakpoint to cache".
- **Non-deterministic serialization** of tool schemas (unordered dictionaries) breaks byte-identity. Serialize with a stable key order.

`LLM Turn Price` reads `response.Usage.CacheReadInputTokens` and `CacheCreationInputTokens`; on the OpenAI protocol the analogous signal is `usage.prompt_tokens_details.cached_tokens`. The price table is per provider and per model, so the running total survives an endpoint switch. A cold prefix with no accompanying profile switch is the regression signal.

---

## 7. Trust boundaries

TheApp feeds the model text it did not author. Enumerated:

| Source | Attacker | Reaches the model as |
|---|---|---|
| Journal / Status.json | Another Commander's chosen name, ship name, or in-game message | Game state, situational awareness |
| In-game comms | Any player in range | Re-voiced message content |
| Web search results | Anyone on the internet | Tool result |
| INARA / Spansh | Third-party service | Tool result |
| The model endpoint itself | Whoever runs the address in `llm.endpoint` | Tool calls, directly |

**Consequence:** anything the model can call, a hostile in-game message can attempt to invoke. This is why *Protect safety-critical settings from the model* is a caller property, not a modality property — the protected set is unreachable from the tool surface entirely, and reachable from the panel, a hotkey, and the model-free keyword router.

Guardrails are static prompt material in the cached region. They cannot be stripped by a budget setting, an effort setting, or a persona change, because none of those touch that block.

Egress is enumerated per provider and disclosed — see *Say what each provider receives*. Every destination can be turned off, and the logging subsystem sends nothing regardless.

> **Amended 2026-08-18, for Phase 29.** The fifth row is a different kind of entry from the four above it, and the difference is worth stating rather than leaving to be inferred. Those four are content arriving *through* a trusted provider — a hostile string that has to talk the model into acting on it. This one is the provider. A Commander pointing `llm.endpoint` at an OpenAI-shaped address they do not control gives whoever runs it the ability to emit tool calls **directly**, with no model to persuade.

> **Amended 2026-08-19, for Phase 36.** The Spansh row now carries **third-party market data**, which is a larger surface than the system and station facts it carried before — a few hundred priced commodity rows per station, across a hundred and fifty stations, all of it crowd-reported. Two things follow and both are built. It reaches the model only as *arithmetic that d47 did*: prices are read into numbers, ranked here, and what the model sees is a route, never a market row echoed through. And nothing in that data can name a place the plan then sends the Commander to without also being a station the search returned for the radius they asked about. The Commander's own `Market.json` is the same class of input as the rest of the journal, and is preferred over the reported version only when it is newer.
>
> **Nothing was built for it, and that is the point.** The protected set is unreachable from the tool surface entirely, and protection is a property of the caller rather than of the modality — the panel, a hotkey and the model-free keyword router reach those rows, and nothing arriving over the wire does, whoever is on the other end. That invariant was written for an in-game message and it is what makes pointing d47 at a stranger's gateway a decision the Commander gets to make rather than a hole. The row is added because an enumeration that omits the case is the failure this section exists to prevent, not because the case needs new machinery.

> **Amended in Phase 12.** This used to promise *local-only operation* as a named, supported configuration, and d47 reported "Nothing is leaving this machine right now" when it held. Both are gone. The disclosure now counts destinations rather than making a claim about the machine, for two reasons. A blanket sentence reads as a property of d47 rather than of these settings at this moment — and as a promise it prices every later feature against itself: a speech model that downloads when selected, a first-run fetch, anything that reaches a network for a reason the Commander would want, all arrive as violations of a slogan rather than as rows in a list. What the disclosure owes the Commander is that nothing reaches a network without appearing in it, which is a property of the enumeration and survives every feature added to it.

> **Amended in Phase 11.** This sentence used to list *Edge TTS* as part of local-only operation. It is not: Edge Neural is `speech.platform.bing.com`, and every line d47 speaks is sent there to be turned into audio. The free provider is free, not local. The correction matters because the disclosure had no text-to-speech entry at all until Phase 11 added the second provider — so every spoken word went to Microsoft without appearing anywhere in the enumeration, which is the one thing this section exists to prevent. `EgressDisclosure.TextToSpeech` is now part of the enumerated set.

---

## 8. Testability

| Surface | Substitute | What it proves |
|---|---|---|
| Journal | Recorded fixtures, replayed at 1× and 100× | State derivation, callout triggering, milestone priming |
| Model | Keyword router; canned responses | Every input path answerable with no LLM |
| TTS | Null sink recording queue operations | Ducking, supersede, interruption ordering |
| Microphone | WAV file into the gate | Gate policy transitions without hardware |
| SteamVR | `Unavailable` state | Order-agnostic startup |
| Elite window | Injector in dry-run mode | Key sequences asserted, nothing sent |

Fixtures are byte-preserved via `.gitattributes` (`*.log -text -diff`) and scrubbed of the Commander name and real system visits — the repository is public.

The property that makes all of this work is that no Core component owns a thread or reads the clock directly. Time is injected.

---

## 9. Packaging: the honest version

*TheApp installs on a clean machine* asks for "a single statically linked binary." In .NET that phrase does not have a clean referent, and it is worth naming the gap now rather than discovering it at 0.1.0.

| Approach | Result | Cost |
|---|---|---|
| **Single-file publish** + `IncludeNativeLibrariesForSelfExtract` | One `.exe`; native DLLs extract to a temp dir at first run | Not actually static. Extraction is visible to AV heuristics, and an unsigned binary doing it is a plausible SmartScreen trigger. |
| **NativeAOT** | Genuinely ahead-of-time compiled | Avalonia supports it. Whisper.net and `openvr_api` are native P/Invoke targets that still need to ship alongside or be statically linked — neither is turnkey. Reflection-based serialization must be replaced with source generators. |
| **Framework-dependent** | Small download | Requires a .NET runtime on the machine. Contradicts "clean machine." |

Recommendation: **self-contained single-file publish, unsigned, with a published SHA-256**, and treat "statically linked" in the checklist as shorthand for "one file the Commander downloads and runs, no runtime prerequisite, no elevation." NativeAOT is worth revisiting once the native dependency set is stable, but making it a 0.1.0 blocker would trade a shipping date for a property no user can observe.

**Amended after 0.5.13, for the same reason the table's first row warns about extraction.** Whisper.net's loader never sees the single-file bundle: it probes the disk for `runtimes\win-x64\whisper.dll` beside the executable and throws when the folder is absent — so a bundled whisper.dll is a whisper.dll that cannot be found, and transcription had never worked in any published build while working in every dev build. The natives now publish as loose files under `runtimes\win-x64\` (a csproj target marks them `ExcludeFromSingleFile`), the release artifact is `d47.zip` — the exe plus that folder — and the in-app updater downloads, verifies and swaps the set atomically-with-rollback rather than one file. COVAS++ reached the same layout independently, from PyInstaller's side of the same trade. The managed dependencies and the runtime stay inside the one exe; `d47.exe --selftest`, run by CI and the release workflow against the *published* output, is what keeps this class of gap from shipping silently again.

**Amended again at 0.5.16: there is an installer.** `d47-setup.exe` (Inno Setup, `installer/d47.iss`) is now the normal way in, with the zip still published for portable use and still what the in-app updater fetches. It installs per-user into `%LOCALAPPDATA%\Programs\d47` with `PrivilegesRequired=lowest`, so neither property below is traded. The install directory is deliberately **fixed rather than versioned**, which is also why the update frameworks that would otherwise fit — Squirrel, Velopack — were rejected: they install into per-version folders, so the executable moves on every update and the `data\` folder beside it is orphaned, taking settings, DPAPI secrets and a several-hundred-megabyte model with it. d47 keeps its own in-place updater and the installer does first install and clean uninstall only. Uninstall asks before removing `data\` and defaults to keeping it. The shortcut is written to the same path `StartMenuShortcut` checks, so the app's own offer stands down without needing to know how it was installed; `InstallerScriptTests` pins that agreement, along with the natives keeping their folder structure.

Two things that *are* observable and should not be traded away: no elevation, and no runtime prerequisite.

---

## 10. Rejected alternatives

| Rejected | In favour of | Reason |
|---|---|---|
| CEF / HTML-to-texture | Avalonia offscreen | 150–200 MB; multi-process; kills single-binary. **Reopened in Phase 12 — see open question 6.** Two of these three reasons have been withdrawn, and the elevation claim behind them was wrong; the objections that survive are different ones. |
| WebView2 + WGC | Avalonia offscreen | No offscreen texture path; needs a live window. This one stands, and it is what rules out Tauri and a localhost page as well — see open question 6. |
| Desktop-window capture (OVR Toolkit style) | Native overlay | Contradicts minimize requirement; VR becomes a worse copy of desktop |
| OpenXR overlay extension | OpenVR `IVROverlay` | Elite doesn't call OpenXR; extension poorly supported |
| FFmpeg (any wrapper) | NAudio | GPL builds; unnecessary for live mic |
| Cloud STT | Whisper.net local | Network dependency on the core input path; egress of every utterance |
| vJoy / ViGEmBus | `SendInput` scancodes | Kernel driver vs. per-user no-elevation install |
| `WH_KEYBOARD_LL` hook | Foreground check + `SendInput` | Global input chokepoint; a stall becomes the Commander's stall |
| Writing the Commander's binds file | Read-only parse + guidance | ED caches binds; format is version-sensitive; silently rewriting a HOTAS config is unforgivable |
| Per-turn dynamic tool selection | Closed profile enumeration | Destroys prompt caching on the exact turn caching starts to matter |
| Separate audio path per voice | One arbiter | Separate paths per voice are how a line gets spoken in the wrong one |
| Loopback capture as the AEC reference | The arbiter's render reference tap | A second WASAPI stream, possibly on a different device, arriving late with its own clock — where the tap is the exact buffer the mixer produced |
| An acoustic wake-word model | Whisper plus the voice-activity gate | A second model to ship and download, and a vocabulary fixed at build time that could never be the name the Commander gave their ship's AI |
| A neural VAD | Energy over an adaptive noise floor | Runs on every buffer on the audio thread; a Commander who finds it too eager turns one number. A model there is a second download for a decision Whisper re-checks a moment later |

---

## Open questions

1. **Mid-conversation tool changes in the C# SDK** — if supported, it removes the mode-transition cache miss on the Anthropic path. It is Opus-5-onward and Anthropic-only, so the profile enumeration stays regardless: it is the portable design every provider gets, and the beta is an optimization on one of them.

   **The SDK half is answered.** *Checked in Phase 14 against `Anthropic` 12.40.0 by reflecting over the shipped assembly.* The typings are present and public: `Tool.DeferLoading`, and `MidConversationSystemBlockParam` as a case of the `ContentBlockParam` union. So "verify SDK support before designing around it" is satisfied — what is still unmeasured is whether the round trip behaves, which needs a live call rather than more reading. The profile enumeration stays either way.
2. **Overlay transparency, in the compositor.** *Narrowed in Phase 9, not closed.* The caption layer is transparent everywhere its box is not, and it is asserted to be — an opaque rectangle across the Commander's view is the failure that would matter, and a headless test reads the alpha channel and refuses it. What is still unanswered is what the **compositor** does with it: `IVROverlay` expects premultiplied alpha, `RenderTargetBitmap`'s locked framebuffer reports `Premul`, and nobody has looked at the result in a headset. A previous implementation ran a partly transparent overlay successfully without ever pinning down which convention it was relying on, which is encouraging and is not evidence. Everything else d47 draws is opaque, so this is confined to captions.

3. **Two concurrent overlay handles.** *Narrowed in Phase 9; the risk was withdrawn in Phase 12.* Panel and captions. Under the shared-texture path these had to share one D3D11 device — not as a saving but as a requirement, because a previous implementation gave each surface its own and it ran perfectly until the first moment two of them submitted in the same session, then took the runtime down from inside `vrclient` with an access violation. `SetOverlayRaw` has no graphics device to share or to get wrong, so what is left is two handles and two buffers. Still unmeasured; the 0.75 ms figure still covers one.

4. **Whether the headset path reads well in a headset.** *Narrowed in Phase 12.* The panels are visible and correct — that much was settled once `SetOverlayRaw` replaced the D3D11 texture (D1) and a Commander looked through the lens. What is still unanswered is everything a face answers rather than a test. The tiers are deliberate — the state machine, the placement arithmetic, the caption rules and the matrix conventions are all asserted with no hardware, and what is left on the far side of `IVrRuntime` is interop that no test can reach. Namely: whether the panel is legible at 1.1 m, whether the placement defaults read well, whether grabbing feels attached, whether captions survive a starfield. SteamVR's `driver_null` would let the texture handoff and the transform round-trip be asserted against a real compositor with no headset attached, which is the largest piece of this that could stop needing a person.

5. **The model cannot call a tool yet, and Phase 10 is what makes that expensive.** *Found in Phase 10. **Closed in Phase 14**, which built the agentic half as the prerequisite for that phase's knowledge tools.*

   **What landed.** `LlmStreamEvent.ToolUse` carries a call once it is whole — the Anthropic path assembles it across `content_block_start`, a run of `input_json_delta` and `content_block_stop`, because the arguments are not parseable until the last fragment lands and a tool run on a half-built object is the one mistake this must not permit. `ConversationMessage` stopped being text-only: history now carries `Text`, `ToolUse` and `ToolResult` blocks, since a `tool_result` with no `tool_use` above it is a protocol error rather than a recoverable one, and the pairing has to survive into the next turn. The turn loop runs rounds — each a billed request — executing calls against the registry between them, and `SupportsToolCalls` is true on Anthropic.

   **Three things the design turns on.** A pure-text turn still serialises as a bare string rather than a one-element block list, so a text-only session's cache entries survive the upgrade. Usage accumulates across rounds instead of reporting the last one, because an eight-call lookup priced as a single question is the number the Commander is least able to check. And `MaxToolRounds` ends the rounds by *withdrawing the tools* on the last one rather than cutting the turn off — a model that cannot call anything else answers in words, so the ceiling produces a reply instead of silence with a bill behind it.

   **The 20-block lookback is handled where it is caused.** Intermediate breakpoints are spent on `tool_result` blocks, at 15 blocks rather than 20 so a breakpoint is not one block of drift from finding nothing, against a budget of 4 with 1 already on the system block. Nothing in the turn loop parses a `tool_use` block, runs it and appends a `tool_result`, and no provider sends tool definitions at all — so all thirty-odd registered tools are reachable only through the model-free routes: the keyword router, and the declared command phrases Phase 10 added. That was a defensible gap while the tools were all *reporting* tools, and it stops being one now that four of them fly the ship: "gear down" works because a phrase was written down for it, and anything phrased differently reaches nothing. The half of this that Phase 10 owns is done and tested — `ToolProfiles` decides which tools would ship, quantized by mode so the prefix stays warm — and it is gated behind `LlmProviderCapabilities.SupportsToolCalls`, which is false everywhere, because advertising a tool the loop would silently drop is worse than not offering it: the model then tells the Commander it has done something that never happened. What is left is the agentic half, plus the 20-block breakpoint handling above that a multi-tool turn immediately runs into.

6. **Whether the view layer should be HTML in CEF rather than Avalonia.** *Reopened in Phase 12, parked undecided.* D1 and §10 reject CEF for three reasons; two have been withdrawn by the person who set them, and the third is not true.

   **Withdrawn.** *"~150–200 MB"* — a first-install download is acceptable, and CEF can be fetched by the installer into a versioned shared runtime rather than shipped in the package, so an upgrade that does not move the pin costs nothing. *"Kills single-binary"* — an installer is now preferred to a portable executable. This half of §1 traces to a **shipped** acceptance criterion (*TheApp installs on a clean machine*, "one self-contained file with no runtime prerequisite"), so acting on it means amending something already delivered. That item already concedes most of the purity: "native libraries self-extract on first run", so today's build is a self-extracting bundle rather than a single binary, and the difference from a CEF folder is degree rather than kind.

   **Not true.** *"No elevation"* — CEF requires none. No service, no driver, no HKLM, and it runs from `%LOCALAPPDATA%`; this is how every Electron app that installs without a UAC prompt works. The one place elevation creeps in is a machine-wide Visual C++ runtime, avoidable by deploying it app-locally.

   **What actually survives, none of which D1 argued.** The transitive **licence graph** is unverified in both directions: CEF and CefSharp are BSD-3, but Chromium bundles FFmpeg, and §1 forbids copyleft while §10 separately rejects FFmpeg over GPL builds. Chromium's is normally the LGPL configuration, dynamically linked inside `libcef`, which §1's own carve-out permits — but that needs checking rather than asserting. **No telemetry** needs deliberate configuration and then a packet capture to confirm: Chromium has a variations seed fetch, a component updater and safe-browsing machinery. And **untrusted input reaches a DOM**: §1 and CLAUDE.md both state that journal text, in-game comms, web search and INARA are hostile. In Avalonia a hostile string in a `TextBlock` is inert glyphs; in HTML the same string through the wrong sink is script execution, with whatever JS-to-.NET bridge exists behind it — and behind *that* sits a capability surface including key injection and the settings the model is deliberately denied. Manageable with escaping at every sink, a strict CSP, and a narrow typed message channel rather than exposed objects. Also permanent, and architectural.

   The Chromium **CVE stream** was raised and is not a serious objection: Google fixes Chromium, CEF rebases, CefSharp releases, and the work is a package bump, a rebuild and a retest a few times a year. It does mean each bump is the one upgrade that re-downloads the runtime.

   **The constraint that decides the shape.** *One widget tree renders to both surfaces* is an invariant (§1), and it eliminates every other web option: WebView2, Tauri, and a local HTTP server viewed in the Commander's own browser all lack an offscreen buffer, so none can put the same view in a headset. If the panel must be the same panel, CEF is the only web path — its offscreen renderer is first-class and hands over BGRA, which is exactly what `VrPixels` takes since D1's Phase 12 amendment. `D47.Vr` would not change at all.

   **Two shapes, and they trade against each other.** *Two browser hosts* — one windowed, one offscreen, same route — is structurally what exists today, one definition instantiated twice, so the invariant stays shorthand; it also introduces state drift that is currently impossible by construction, because both Avalonia surfaces bind one `PanelViewModel` and there is one copy of the state. The VR host is built when a headset connects, so it *always* starts mid-session: each host would need a snapshot on connect plus sequence-numbered deltas, with a gap forcing a resnapshot. *One offscreen host painted to both* removes drift entirely and makes the invariant literally true for the first time — but it cannot render two sizes or two content sets, so **mini mode could not exist**, and the desktop window becomes a bitmap blit that gives up native text rendering and takes forwarded input.

   **Measured cost of the swap**, from the tree at 0.5.7: ~6,100 of ~35,000 lines. `D47.Core` (21,664) and every other project are untouched, and `AppHost.cs` — 1,962 lines, the composition root — mentions Avalonia twice, both in comments. The dependency boundary is what makes this a swap rather than a rebuild. About 3,300 lines plus 450 of markup are genuinely rewritten in HTML; ~830 (`OffscreenSurface`, `VrPanelSurface`, much of `VrHost`) are deleted outright. The largest single file, `SettingsView.axaml.cs` at 1,152 lines, is a renderer over `settings.Sections` — the 90 rows are generated from capability descriptors, so it ports mechanically. Not in that number: the JS-to-.NET bridge, which is new design, and a replacement for the headless render-capture discipline that is how UI is verified here — that replacement should exist *before* a port, or the whole view layer gets rewritten with no way to see what broke.

   **Evidence from the neighbouring project.** `C:/dev/covas++` is web-based and reached the opposite conclusion for the headset: its VR overlay is `covas/capabilities/vr_hud.py`, a Pillow rasteriser producing an RGBA buffer for `IVROverlay.setOverlayRaw` — the same architecture d47 arrived at independently in Phase 12 — while its desktop control panel is Flask on localhost in the Commander's own browser, with a schema-driven settings page much like this one. It previously had a transparent `/hud` page composited in-headset by a third-party OpenXR tool and **removed it** in favour of one native path. Its VR panel is also explicitly non-interactive, "a view, not a control surface", where d47's takes a pointer and can be grabbed. So the surface being admired there is a hand-written rasteriser, not HTML, and the web half of it never reaches a headset.

   **What would decide this** is a spike rather than more prose: a CEF panel rendering the real panel as HTML, in a window and offscreen through `SetOverlayRaw`, with the licence graph and the telemetry question checked first, since either could end it before the spike is worth building.

**Resolved.** *Avalonia → SteamVR overlay* was the highest-risk unknown in the stack, and it earned that rating: the spike proved the shared-texture route end to end (D1, [docs/spikes/vr-texture.md](docs/spikes/vr-texture.md)) and the real build was still invisible in a headset until Phase 12 replaced the texture with `SetOverlayRaw`. The rasterise half — an Avalonia visual tree to correct pixels, offscreen — is what the spike genuinely settled and is what carried over. *Keep working when the main window is minimized* falls out of the same result with one correction Phase 9 had to make: there is a window in the VR path, because a templated control needs a logical root to be styled at all, and it is never shown. Minimise-safety does not rest on there being no window; it rests on the VR path not depending on the state of the window the Commander can see. See D1.
