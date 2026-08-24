# Open bugs

Defects only. Feature and polish work lives in [list.md](list.md).

An item leaves this file when it ships, and its record from then on is the line it gets
in `CHANGELOG.md` under the release that fixed it. There is deliberately no
`fixed-bugs.md`: a second copy of that history is one nobody reads and one that rots.

Each entry states what was seen, what was verified in the code, and what is still only a
hypothesis. **A lead is not a diagnosis** — reproduce before fixing, and per the standing
rule, reintroduce the fault afterwards and watch the new test fail.

---

## Three open, and one partly confirmed.

Three left in 0.60.2 and their record is that release's section: the two spoken routes that knew
a phrase and not the words in front of it — *"switch to full panel"* and *"set tab to checklist"* —
and the filtered checklist that did not say how big its answer was. They are named here because the
count above was left at eleven when they shipped, and a file that overstates what is open is one
nobody trusts to say what is closed. The engineer offered as a material trader left in 0.60.3,
diagnosed off the 2026-08-20 log exactly as its entry said it could be, and the stream of "X is
done" announcements in 0.60.4 — the entry's own lead was wrong and the log said so, which is the
argument for writing the lead down.

The four that were here shipped in 0.16.2, and the log-routing one in 0.21.1. The
headless-session cleanup failure shipped in 0.47.0 — its changelog line was missed at the time
and added on 2026-08-21 — and the two-Commanders ship-id keying in 0.47.1, whose live half
(`ShipCoreService._aboard` and `ShipDriftWatch._aboard` as bare ints) shipped with list.md
Phase 44 in 0.50.0, on the Commander-switch signal that phase built. Each entry's record is its
section of the changelog.

The VR grab that 0.16.2 recorded as "fixed but not confirmed" was not fixed. The two flags it
called are the wrong road entirely — they opt the quad in to SteamVR's own laser, which only runs
over SteamVR's dashboard, so the events they unlock never arrive while Elite holds the headset.
0.22.1 replaced the whole channel; see its changelog section.

**Partly confirmed.** The trigger does arrive and the panel can be carried — reported from the
headset against 0.22.1. Two faults it then showed, flicker under a live carry and a lock that did
not follow the grab, shipped in 0.22.2.

## Open: the motion controller stops answering some way into a headset session

Reported 2026-08-22 against 0.52.0 and 0.52.1, and on 2026-08-21 against 0.48.x as "Motion
Controller appears hung": after d47 has been running for fifteen minutes or more, the Touch
controller answers nothing — not d47's ray, not SteamVR's dashboard, not Virtual Desktop's own menu
— and the remedies are restarting the headset, or holding the Quest's power button until the power
dialog appears and cancelling, after which hand tracking re-engages in Horizon OS, then the
controller, and everything works again for a while. The Commander is 99.9% sure it never happens
without d47, and nothing in two days of logs contradicts him.

**What is verified**, from `vrserver.txt` and d47's own logs for 2026-08-21 and 22.

1. **The 0.48.6 hand-back never handed anything back, and could not have mattered if it had.**
   `UpdateActionState` with an empty set list is refused — `NoActiveActionSet`, six times in thirty
   seconds in the installed log at 09:37 on the 22nd — and leaves the last list standing; the code
   then marked the claim released and never retried. 0.52.2 fixes the call (the set at priority
   zero, retried on refusal; `spike/GrabSpike --release-probe` proves both shapes against the live
   runtime). But the overlay priority range only takes inputs from other applications when SteamVR's
   *Enable global input from overlays* developer setting (`globalActionSetPriority`) is on, and it is
   off by default and absent from the Commander's `steamvr.vrsettings` — so the claim never took
   anything, and the 2026-08-21 diagnosis was wrong. Independently of that: the 64-minute session
   from 09:40 to 10:44 on the 22nd made no claim at all (no ray ever crossed the panel; there is no
   release line in it) and is long enough to contain the failure.

2. **What SteamVR sees is one controller that goes to standby and never comes back.** Only the right
   Touch controller exists to SteamVR (`1PASH5D1P17365_Controller_Right`, tracked device 1).
   driver_oculus logs `controller state connected` when it is picked up and `disconnected` five to
   twenty seconds after it is put down — the Quest's hand-tracking auto-switch, relayed by Virtual
   Desktop's LibOVR shim — and vrserver logs `1 - entering standby` 5 min 10 s after a disconnect,
   which is its own `turnOffControllersTimeout` of 300 s on top of the 10 s idle threshold. A device
   leaves standby only when its driver submits changing poses or a boolean input changes. In the
   failing case the controller **never reports connected again** — two hours on the 22nd, the whole
   evening on the 21st — until a headset-level wake (`0 - leaving standby`, from the power dialog or
   a restart), after which `connected` follows within seconds and `1 - leaving standby` 30 ms later.

3. **The pattern that separates the cases.** Every put-down while d47 was *not* connected to SteamVR
   came back from standby by itself on the next pick-up: 16:05 → 16:34 on the 21st (d47 connected
   in between, and it still came back), 11:40 → 11:47 on the 22nd and the seven pick-ups after it.
   Every put-down while d47 *was* connected, that then reached standby, never came back on its own:
   16:35, 17:51 and 20:28 on the 21st, 09:38 on the 22nd. Quick re-pick-ups before the five-minute
   standby work either way (09:37:06 → 09:37:38, 11:39:12 → 11:39:23). So whatever goes wrong goes
   wrong at or after the put-down, not at d47's start, and "fifteen minutes or more" is the put-down
   plus the standby plus however long until the Commander reaches for the controller.

**What is only a hypothesis.** Nothing d47 does reaches the Quest directly. Its channels are
SteamVR — poses and events read at 10 Hz, four overlays, an action set that takes nothing — and
Virtual Desktop's virtual speaker and microphone, the microphone held open for the whole session,
which the game alone does not do. The symptom sits below SteamVR, and it has a documented shape:
Horizon OS has exactly one state in which a running immersive app keeps rendering but gets neither
controllers nor hands — **input focus lost** (OpenXR `VISIBLE` rather than `FOCUSED`), entered when
system UI overlays the app and left when it is dismissed. Holding the power button and cancelling
the dialog is the textbook way to force that cycle by hand, and Meta's own forum records a Quest
app stuck in `VISIBLE` after a system menu closed, with hand tracking dead until the next cycle.
Which of d47's channels, or what timing, leaves the Virtual Desktop client in that state is not
known; the one PC-side hook that fires at exactly the moment the fault is armed is SteamVR calling
the oculus driver's `EnterStandby()` for the controller, and what that does inside a closed driver
over VD's LibOVR emulation is undocumented. SteamVR's own activity rules do not help: an
application polling poses is not activity, and nothing an application can call touches a device's
standby. Two days of web search (Steam, Meta, VD, flight-sim and VRChat trackers) found no public
report with this exact signature.

**Two ten-second checks come before any experiment.** The Horizon OS build (Settings → System →
Software Update): Meta's v2.4 is tracked as losing controller *and* hand tracking inside apps, with
a Home-button bounce through system UI as the workaround — the power-dialog trick by another name —
fixed in v2.5, and v2.5 as leaving the headset "stuck reporting a controller as paired when it is
not actually connected", fixed in v2.7 on 2026-08-20; a headset on v2.4–v2.6 outranks every d47
hypothesis, and a controller firmware update is the cheapest blind test. (Virtual Desktop Streamer
is already 1.34.21, past the 1.34.16 "interference with other drivers in SteamVR" fix.) And during
the next wedge, *is the streamed image still live?* Live video with dead input is the signature of
a client stuck `VISIBLE`, which turns this into a Virtual Desktop or Horizon OS fault with a known
shape.

**A lead is not a diagnosis.** The experiment, SteamVR + Virtual Desktop + d47 0.52.2 and no game,
with `vrserver.txt` tailed and d47's log beside it — from 0.52.2 it records every controller
connected/tracking/activity transition on d47's clock. First compress the timer so several cycles
fit in twenty minutes: with SteamVR closed, back up `steamvr.vrsettings` and add a top-level
`"power": { "turnOffControllersTimeout": 30.0 }`; restore it afterwards. A cycle is: controller
down on the desk, hands out of the cameras' view, wait for `disconnected` then `1 - entering
standby`, wait a minute more, pick it up and press **A**; record whether hands engaged before the
pick-up, whether the VD menu opens on a long menu-press, whether vrserver logs `connected`, and
whether the picture is still live. Runs: (a) no d47, twice — the control; (b) d47 running, the ray
put on the panel and the trigger pulled once, then left alone; (c) if (b) wedges, **before the
power button**, confirm the Streamer still says connected, `taskkill /IM d47.exe /F`, and wait
thirty seconds — input returning means d47's presence *holds* the wedge (a mechanism lead and an
immediate kill-switch), nothing returning means the wedge is latched headset-side and d47 at most
triggered it; (d) if (b) does not wedge at thirty seconds, one cycle at the default 300 s — a wedge
only then means the trigger is dwell, not the standby transition; (e) d47 running with the
microphone never opened — mode *hold* with the push-to-talk key cleared, the one arrangement under
which `ListeningWiring.NeedsMicrophone` says no, and the log's "Listening on Microphone" line must
be absent; (f) Steam Link in place of Virtual Desktop, if installed. A wedge that follows the
shortened timer makes standby entry the trigger and the oculus driver's `EnterStandby()` over
Virtual Desktop the mechanism, and a huge `turnOffControllersTimeout` a legitimate workaround; one
that ignores the timer makes standby a bystander and the target the client's focus state.

## Open: the aim ray does not follow the hand

Reported against 0.22.1: the ray appears where the controller was when it first showed and does not
move with it. Not diagnosed. Ruled out so far — the transform is not being suppressed by the
"nothing is pushed unless it changed" guard, whose tolerance is a tenth of a millimetre, and the
pose call itself is the same one four working implementations under `C:/dev` use.

**A lead is not a diagnosis.** `spike/GrabSpike` prints each controller's live position and how far
it has strayed from where it was first seen; a range that stays at zero while the controllers are
being waved is the fault, and anything else means the freeze is downstream of the pose.

**Run 2026-08-20, and it ruled the pose out.** `--poses` against the headset: the held controller
moved on 837 of ~1,350 frames, over a 0.22 m range, and the ray's landing point on the panel
tracked it continuously — `0.94,0.17` → `0.13,0.06` → `0.65,0.99`. The other controller was on the
desk and the run ends with head and both hands frozen because the headset was taken off; both are
explained and neither is a fault. **The Commander confirms the ray followed the hand in the spike.**

That kills the recorded hypothesis. The spike drives the *real* `SteamVrRuntime`, `VrRay`,
`VrActionInput` and the real beam and cursor quads — so the pose read, the ray arithmetic and
`AimBeam` are all correct, and the fault is in **how the app drives them**, which is a far smaller
search than the one this entry used to describe.

**The difference between the two, and the new lead.** The spike updates the beam from its own tight
loop, roughly every 11 ms. `VrHost` updates it from `Carry()`, which runs inside `Serve` on the
**10 Hz tick**, dispatched to the **UI thread** through a `Dispatcher.UIThread.Post` that
deliberately *coalesces* — `_pending` drops a frame rather than queueing it when the previous post
has not run. So the app's ray is at best nine times slower than the one that visibly works, and at
worst it stops entirely for as long as the UI thread is busy. **A ray that "appears where the
controller was when it first showed" is what a Commander sees when those posts stop arriving.**

Worth knowing beside it: until 0.39.1 the Utilities tab rebuilt every timer row on that same UI
thread ten times a second (remediation.md 17, item 14). That is the kind of load this lead
predicts would freeze the ray, and it is now gone — so **the first thing to do is look again on
0.39.1** before changing anything, and say which tab was showing when it froze.

**Still not a diagnosis.** What would settle it: whether the beam moves at all when it looks frozen
— a 10 Hz ray is choppy and a stopped one is not — and whether it recovers when the desktop window
is idle.

## Open: opacity was set on the surface the Commander was not looking at

Reported 2026-08-23: *"Opacity control does not seem to change the opacity of the VR panels. Set to
.5 and I saw no appreciable change — no change that I could detect at all."*

**Nothing is broken, and that is the problem.** The installed build's log settles it end to end. The
value was asked for and stored — `"Model" set vr.panel.opacity to 0.8`, then `0.75`, then `0.5` at
22:00:42, and `settings.json` holds `vr/panel/opacity: 0.5` now. But d47 had been in **mini** since
21:48:26 (`"KeywordRouter" set vr.mode to mini`), and mini is a different surface with its own
number: `vr/mini/opacity` is still 0.95. SteamVR's own readback agrees — `"PanelMini": visible=True
alpha=0.95` at 22:04:19, four minutes *after* the change. The big panel's opacity did change; the
big panel was not on screen.

So the defect is not in `SetOverlayAlpha`, which is called on every serve with the placement's
opacity (`SteamVrRuntime.cs`, `overlay.Look(...)`), and not in the row, which stored what it was
given. It is that a Commander asked for a change and got a silent one to something they could not
see.

**Half of it shipped in 0.60.5.** Every row in the shared surface-settings generator now says which
of the two surfaces it governs and where the other one's copy lives, so the model picking between
`vr.panel.opacity` and `vr.mini.opacity` is choosing between two rows that say what they are rather
than two identical descriptions. The row cannot say *"and you are in mini right now"* — a descriptor
is registered once and never mutated, which is what keeps the tool surface byte-identical across
turns — so it says the part that is true at any moment.

**The rest shipped in 0.60.7, and the answer was neither of the two this entry proposed.** Both of
those — better naming, or a route that resolves the slot from `vr.mode` — kept two numbers and added
machinery to choose between them. The Commander's answer on 2026-08-24 removed the choice: *"there
should be 2 panels, 1 opacity knob."* Everything else those surfaces carry is genuinely per-surface,
because mini exists to be smaller and further out of the way; how see-through the glass is is one
preference about how much cockpit shows through d47. So `vr.opacity` is one row for both, the
per-surface copies stay on disk unread because `settings.json` is append-only, and a value already
set on either panel is carried up once so nobody sets it twice.

**Kept open for the general form**, which the fix does not touch: every other placement row still
exists twice, and a Commander who says *"move it closer"* in mini can still have the big panel's
distance move. Those rows have a real reason to differ, so the answer there is not one knob — it is
either the resolve-from-mode route this entry described or nothing, and nothing is defensible while
the rows say which surface they belong to.
