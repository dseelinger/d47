# Open bugs

Defects only. Feature and polish work lives in [list.md](list.md).

An item leaves this file when it ships, and its record from then on is the line it gets
in `CHANGELOG.md` under the release that fixed it. There is deliberately no
`fixed-bugs.md`: a second copy of that history is one nobody reads and one that rots.

Each entry states what was seen, what was verified in the code, and what is still only a
hypothesis. **A lead is not a diagnosis** — reproduce before fixing, and per the standing
rule, reintroduce the fault afterwards and watch the new test fail.

---

## Four open, and one partly confirmed.

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

## Open: the audition pair's five-second timeouts are a separate fault

Split out of the entry above, because the diagnosis disproved "treat them as one investigation":
`PlayingASecondVoiceCancelsTheFirst` (three appearances, once alongside
`TheGlyphBecomesStopWhileItIsTalkingAndStopsWhenPressed`) times out awaiting a cancellation, and
that is **not** the dispatcher hijack. A test that dies this way leaves an un-cancelled token and
an infinite delay that nothing will ever complete — a pure leak that *cannot* later touch the
dispatcher, so it cannot cause the cleanup failure. Occurrence 9 in the old record — both timeouts,
no cleanup failure in the run — was already evidence of independence.

**The recorded "stale detached button" lead is dead**, three ways, from the code as written:
pressing a glyph does not rebuild the row — `PickerChoice.Playing` raises INPC on the same object,
and rows rebuild only when filter text changes, which these tests never type; `Glyph()` walks
`GetVisualDescendants()`, which cannot return a detached control — a vanished button would make
`.First()` throw, a different failure; and a detached button still raises `Click` into its own
handler, so "Click into nothing" has no mechanism.

**The surviving lead is threadpool starvation.** The second press's cancellation callbacks run on
a threadpool work item (`CancelAsync`), the wait is five seconds, and the suite loads the pool
while the session runs: `EchoCancellationTests` spins three near-100% CPU threads for about a
second as a plain `[Fact]`, in parallel with the Avalonia session. All three appearances were on
busy CI runners. Unproven — a lead is not a diagnosis. What would settle it: a wall-clock trace
around the second press on a loaded runner, or starving the pool deliberately and watching the
same timeout arrive on demand.

## Open: an engineer was offered as a material trader, with rates attached

Reported 2026-08-20 from the running app. Recorded as scratch — **not diagnosed, not fixed.**

What the Commander heard, across two turns:

> You were 12 Conductive Polymers short. Lei Cheung is here, at Trader's Rest, and can do 52
> items on your list.
>
> *"What do we need the Conductive Polymers for?"*
>
> Conductive Polymers are a grade 4 manufactured material. You hold 3 of 150. Your plans call for
> 15 of them, so you are 12 short. I can't tell you from here which single blueprint eats them —
> the shortfall is netted across every live plan at once, and there are a great many. They come
> from transport ship salvage, surface points of interest, or mission rewards. **Lei Cheung, here
> at Trader's Rest, would trade for them: 2 Core Dynamics Composites or 2 Proto Heat Radiators
> buys one, or 1 Biotech Conductor buys three.**

**Lei Cheung is not a material trader.** The Commander says he is not a Manufactured Materials
Trader, and the table agrees on something stronger: `Engineers.tsv:35` has him as an *engineer* —
Laksak, Trader's Rest, Sensors/Shield Generator/Surface Scanner grade 5, unlocked with 200 Gold.
He is not a trader of any category. The app named a person who cannot do the thing it offered.

**The lead, and it is only that.** The two halves of that sentence look like they come from
different places and were fused by the model, not by code:

- *"Lei Cheung is here, at Trader's Rest, and can do 52 items on your list"* is grounded —
  `EngineerAtHand` says exactly this shape, off `Engineers.tsv`.
- The rates are grounded too, but in a different subject: `EngineeringRules.TradeRate` and
  `PlanGap` compute what *a* material trader would charge to cover a shortfall, and `PlanGap.cs:41`
  says outright that it is "what a trader could cover it with" — an anonymous one. Nothing in that
  path names a station or a person.

So the suspicion is that both facts arrived in one context, the only name present was the
engineer's, and the model attached the rates to him. If that is right, the defect is **not** in the
rates and **not** in the engineer callout; it is that the gap analysis hands over a trade offer with
no owner, in the same breath as a named person who is not that owner.

**What would settle it.** Read the actual turn: whether the trade rates reached the model through
the gap tool with no trader named, and whether the engineer-at-hand callout was in the same window.
The installed build's logs are the place to look, not a re-run — this is reproducible from the
record rather than from the game.

**Two adjacent claims from the same turn, unverified, worth checking while there:**

- "can do 52 items on your list" — whether 52 is real or also invented.
- "2 Core Dynamics Composites or 2 Proto Heat Radiators buys one, or 1 Biotech Conductor buys
  three" — whether those are what `TradeRate` actually returns for grade 4 from grade 4 and
  grade 5, or the model's arithmetic on top of them.

**Not a defect, but noted from the same turn:** "I can't tell you from here which single blueprint
eats them — the shortfall is netted across every live plan at once." That is honest and correct
about what the tool returns, and it is also a capability the Commander asked for and did not get.
If it is worth having, it is a `list.md` item, not this file.
