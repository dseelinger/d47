# Open bugs

Defects only. Feature and polish work lives in [list.md](list.md).

An item leaves this file when it ships, and its record from then on is the line it gets
in `CHANGELOG.md` under the release that fixed it. There is deliberately no
`fixed-bugs.md`: a second copy of that history is one nobody reads and one that rots.

Each entry states what was seen, what was verified in the code, and what is still only a
hypothesis. **A lead is not a diagnosis** — reproduce before fixing, and per the standing
rule, reintroduce the fault afterwards and watch the new test fail.

---

## Eleven open, and one partly confirmed.

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

---

## Open: "Set tab to checklist" is not a shape the panel phrases recognise

Reported 2026-08-23, from the headset, in the **VR mini panel**. Said *"Set tab to checklist"* and
expected the mini panel to move to the Checklist. The utterance fell through to the model, which
answered honestly:

> I don't have a tool that switches panel tabs in the headset display. That's likely a manual or
> voice-command action on your end, Commander.

**Verified in the code, and it is a near miss of a fix that already shipped.** `PanelPhrases` builds
its vocabulary as opener + name + suffix (`src/D47.Core/Interface/PanelPhrases.cs:61-91`). `Openers`
carries fifteen entries — bare, `show `, `open `, `go to `, `switch to `, `select ` and their `the`
variants — and **`set ` is not among them**. `Suffixes` is bare or `" tab"`, which is what the
2026-08-21 report added, and that comment names this exact failure mode: *"Select the checklist tab"
was the reported miss … the phrase fell through to the model, which has no tool for the panel and
said so.*

So the grammar covers *"select the checklist tab"* — name in the middle, "tab" as a suffix — and
**does not cover "set tab to checklist"**, which inverts the order and puts the destination last. No
combination of the two lists produces it; the word "set" appears nowhere in the file.

**The fix is not simply another opener.** Adding `set ` alone still would not match, because the
destination has to be extractable and here it sits *after* the word "tab". This wants a second
pattern — "set tab to NAME", and probably "set the tab to NAME", "go to tab NAME", "tab NAME" —
rather than another row in `Openers`. Whatever is added must reach the same route the existing
phrases use, so the drawn and spoken paths stay one thing.

**Still only a hypothesis, and the reason this is not a one-liner:** whether the mini panel in the
headset would have honoured the move even had the phrase matched. `PanelNavigation.Register`
(`src/D47.Core/Interface/PanelNavigation.cs:201`) is called per surface and the destination
vocabulary is the union of what each surface registered (`:128-131`) — and mini hides the tab strip,
so it is unproven that the VR mini surface registers the Checklist root at all. **Reproduce both
halves before fixing either**: that the phrase matches, and that the headset's mini panel actually
moves. A phrase that matches and then reports a move nobody can see would be worse than the current
honest refusal.

Related and already known: `list.md` Phase 51 records that `ApplyChrome` hides the tab strip in mini
while leaving `PagePane` visible, so a surface driven into mini on the wrong tab draws a page with no
way to leave it. That hole and this one meet in the same place.

---

## Open: a setting command matches only its exact phrase, so "switch to full panel" misses

Reported 2026-08-23, from the headset, in the VR mini panel, minutes after the entry above. Said
*"Switch to full panel"* and expected the quad to grow. Instead:

> I've no action for a combined "full panel" view — only the individual ones: left, right, comms
> and role panels. Want me to open one of those?

That answer is the model reaching for Elite's own ship panels, which is a reasonable reading of
"panel" once the utterance has already fallen past every model-free route.

**Verified, and the phrase exists.** `VrCapability.cs:146-147` registers exactly what was wanted:

    new SettingCommandPhrase("mini panel", "mini"),
    new SettingCommandPhrase("full panel", "full"),

**The match is exact whole-utterance equality.** `KeywordRouter.MatchSetting`
(`src/D47.Core/Conversation/KeywordRouter.cs:186-202`) ends in
`string.Equals(utterance, Utterance(command.Phrase), StringComparison.OrdinalIgnoreCase)`. So
*"full panel"* matches and *"switch to full panel"* does not. **Saying the bare phrase works
today** — this is a gap in what a Commander may say around it, not a missing capability.

**This is the same defect as the entry above, in a different code path, and that is the finding
worth acting on.** Both are natural openers defeating a rigid phrase table:

| Said | Route | Why it missed |
|---|---|---|
| "Set tab to checklist" | `PanelPhrases` | "set" is not an opener, and the destination is in the wrong position |
| "Switch to full panel" | `MatchSetting` | no opener stripping at all |

`PanelPhrases` already solved this once — it carries fifteen `Openers` including `"switch to "`
(`src/D47.Core/Interface/PanelPhrases.cs:61-77`). `SettingCommandPhrase` has no equivalent, so every
setting command in the app has the same hole, not just this one.

**The fix must not be "loosen the matching", and the code says why.** Whole-utterance is deliberate
and one notch stricter than the keyword route *because this path acts rather than answers* — the
comment at `KeywordRouter.cs:204-215` makes that argument, and remediation 16 is the precedent for
what a loose match costs. The shape that keeps the property is to **strip a closed set of leading
openers before comparing**, exactly as `PanelPhrases` does: still whole-utterance, still a closed
grammar, just with "switch to" / "go to" / "set" / "show me" removed from the front first. Sharing
one opener list between the two routes would be better than a second copy.

**Reproduce before fixing**, and check the collision `list.md` Phase 51 flags: *mini panel* and
*full panel* belong to the headset, and the desktop's equivalents are *mini window* and *full
window*. An opener-stripping change widens both vocabularies at once, so it must not make a phrase
said at a desk reach the quad or the reverse.

---

## Open: the checklist filter is per-surface, so the headset and the window disagree

Reported 2026-08-23. Mini panel in VR showing the Checklist; applied the "What Lei Cheung can do
here" filter in the Windows app; switched back to the headset. **The mini panel still showed the
unfiltered list.** The two surfaces are drawing the same list through two different filters and
neither says so.

**Verified, and this is a mistake the file has already made once and fixed.**
`src/D47.App/Panel/ChecklistPage.cs:146` and `:149`:

    private string _chosen = Everything;
    private string _query = string.Empty;

Both are **instance fields on the page**, and there is one page per surface, so each surface keeps
its own filter and its own search text.

The precedent that settles it is a few files away. `ChecklistService.Selected`
(`src/D47.Core/Checklists/ChecklistService.cs:63-78`) carries this comment:

> **Here rather than on the page, because two surfaces have to agree what "it" is.** The selection
> used to be a string inside `ChecklistPage`, so a spoken "move it up" had nothing to refer to and
> said so — while the panel, a foot away, was drawing a highlight round the very line that was
> meant.

That was reported 2026-08-21 and fixed by moving the state into Core. **`_chosen` and `_query` are
the same string on the same page that was not moved with it.** This is therefore an oversight rather
than a design choice, and Phase 45's rule points the same way: what you are *reading* is shared, and
only *how a surface draws it* — mini/full, zoom — stays where it is. A filter changes what is on the
list, not how big it is.

**One place it is not a straight copy of the `Selected` fix.** That comment continues:

> **Not persisted, and that is the distinction.** A selection is where a Commander is looking this
> minute, not a preference.

A filter is closer to a preference — the same Commander asked on 2026-08-23 for the filter to be
**remembered between sessions**, which selection deliberately is not. So the filter wants sharing
*and* persistence, and the two decisions should be made together: shared state in Core, and a home
for the persisted value. `settings.json` is append-only, and there is a `ViewState` for things that
are view preferences rather than settings — decide which, with the precedent of how zoom, mode and
window placement are stored.

**Unverified, and worth checking before building:** whether the VR mini panel draws the filter
control at all. `ChecklistPage` has no mini awareness — mini/full is applied by `PanelView.ApplyChrome`
around the page rather than inside it — so the chip row is *probably* drawn, but nobody has looked.
It matters: if a shared filter can be switched on from the desk and the headset has no control and no
label for it, the Commander gets a short list in VR with nothing on screen explaining why. Sharing the
filter without showing its state on both surfaces would trade a disagreement for a mystery.

`_query` should move with `_chosen` or the same report arrives again about the search box.

---

## Open: a parked ship's lines carry no verdict, and the comment saying why is now false

Found 2026-08-23 while diagnosing the engineer filter; **not the cause of that report**, and recorded
because the reasoning beside it has stopped being true.

`ChecklistEvaluator.Ship` (`src/D47.Core/Checklists/ChecklistEvaluator.cs:63-72`) reads `state.Ship`
— the ship being flown — and returns null for anything else:

    var loadout = state.Ship;
    …
    if (!IsActive(item.Scope, loadout)) { return null; }

So a line about any ship the Commander is not sitting in has no verdict and no next-action text,
which is the asymmetry that made one report look like d47 "could not see" a ship. It can: the same
line reads *"0A Shield Booster"* rather than *"TinyHardpoint2"*, and that text comes from
`ChecklistWording.InSlot` reading `state.Loadouts.For(shipId)`
(`src/D47.Core/Checklists/ChecklistWording.cs:140-143`). **The line and the verdict beneath it read
different sources**, and v0.60.0 fixed the engineer join while leaving the verdict on the old rule.

The comment at `:67-69` — that a plan for a ship in another dock "cannot be diffed at all" — was
true before list.md Phase 37 remembered loadouts and is false now. Leaving it standing beside code
that no longer needs to obey it is the thing the change-requests file warns about.

**The fix, when it is wanted**, is the `LoadoutFor` treatment `EngineerAtHand.cs:224-240` already
has. **With one condition**: a remembered loadout is a fact about a moment, and `RememberedShip`
already carries `SeenAt` (`src/D47.Core/Journal/ShipLoadouts.cs:5-10`) for exactly this. A verdict
derived from a month-old snapshot presented as current is the one way this change can do harm, so
it must say when the ship was last seen.

---

## Open: the filtered checklist does not say how big the answer is

Reported 2026-08-23 as "the engineer filter only shows some of my ships", and **that reading was
wrong** — the filter returns every match across the fleet. What it does not do is say so.

`ChecklistPage.Rebuild` builds one flat `StackPanel` with no cap, no headings and no count
(`src/D47.App/Panel/ChecklistPage.cs:454`, `:469-472`), and ordering floats the flown ship's project
to the top (`src/D47.Core/Checklists/ChecklistOrdering.cs:135`) with everything below it in file
order. One Commander's second project holds 49 lines, ten of them rendering byte-identically because
several slots hold the same module — so the first screenful is one ship, repeated, and reads as the
whole answer.

**This cost a false bug report and an afternoon**, which is the argument for fixing it: the list is
correct and looks broken. The smallest thing that would have prevented it is one muted line above
the rows — *"Lei Cheung can roll 45 of your lines, across 7 ships."* That does not fight the stated
"no headings between scopes" decision at `ChecklistPage.cs:467-469`, because the scope already rides
each line.

Identified while diagnosing, and **not chosen for 0.60.1** — the Commander took the correctness fix
alone. Recorded so it is not rediscovered from a second report.

---

## Open: a stale publish artifact in the build output silently overrode every build

Reported 2026-08-23, and it cost about two hours and a wiped data folder before anybody looked at a
file size.

**What was there.** `src/D47.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64/d47.exe` was
**74,742,715 bytes** — a self-contained single-file bundle with every assembly inside it. A normal
apphost is ~150 KB. That exe never reads the `d47.dll` beside it, so `dotnet build` and `dotnet run`
faithfully rebuilt the DLLs and the running app ignored them completely.

**Why it took so long to see.** Every check said the fix was present, because every check was
looking at the wrong artifact: the source had it, the build succeeded, and a byte-level search of
`d47.dll` found the new strings. The process was even confirmed running from that exact directory.
Three fixes in a row appeared not to work, and each one was re-diagnosed from scratch. The tell was
sitting in plain view the whole time — `d47.exe` dated 08-23 21:42 and `d47.deps.json` dated
**08-16**, in a folder whose DLLs were minutes old. A timestamp hours in the future was noticed
early and dismissed as a clock quirk.

**Two more things that folder should not have been holding.** `bin/Debug` also contained the debug
build's **live `data/` folder** — d47 writes beside its executable, which is right for an installed
app and means dev state lives in build output. So the obvious remedy for the stale exe, deleting
`bin/Debug`, wiped the Commander's debug checklist, settings and secrets. And two dead framework
folders (`net10.0`, `net10.0-windows`) still held August 15 binaries.

**How it got there is not known.** `PublishDir` is `bin\$(Configuration)\publish\`
(`src/D47.App/D47.App.csproj:47`) and has been since Phase 21, so a current `dotnet publish` does not
write here. `PublishSingleFile` is set for **all** configurations, not just Release, so a
`dotnet publish -c Debug` from before that line existed is the likeliest source. Do not fix the
origin without establishing it.

**What is worth building is the guard, not the archaeology.** A `dotnet build` that can be silently
shadowed by a file left in its own output directory is a trap that will be walked into again, by
anyone, with no error. Candidates, cheapest first: a `--selftest`-style check that the exe about to
run is an apphost rather than a bundle; a build target that fails when the output exe exceeds a few
megabytes; or moving the dev data folder out of `bin` so the obvious remedy stops being destructive.
**The last one is worth doing whatever else is decided** — it is the reason a two-hour diagnosis
ended in data loss.

---

## Open: a stream of "X is done" announcements for things that happened while d47 was off

Reported 2026-08-23, from the debug build: *"I should not get a stream of 'X is completed'
notifications. That happened while D47 was 'off' (as far as the Debug version knows), and shouldn't
comment on it other than to mark the items done."*

**The rule already exists and is written down.** `CalloutEngine`'s `IsPriming`
(`src/D47.Core/Callouts/CalloutEngine.cs:10-18`) is for exactly this: *"True on the startup tick,
which replays the whole journal backlog. A callout must fold that backlog into its state and
announce nothing from it — otherwise starting d47 after Elite would say everything that happened
since it was last run."* `Offer` guards on it at `:143`.

**And `ChecklistCallout` honours it.** `Examine` (`src/D47.Core/Callouts/ChecklistCallout.cs:34-42`)
returns nothing while priming and calls `Prime(context)` instead. So the announcements did **not**
come from the priming tick, and the obvious fix — "check `IsPriming`" — is already in place.

**A lead, not a diagnosis.** Priming folds the *journal* backlog into the callout's state, but the
**checklist document is separate persisted state** with its own file. The debug build's
`checklist.json` was a day stale when this was reported, so a dozen rolls had been finished in the
world without that document knowing. Priming would leave the document still saying "open" while the
loadout says "engineered"; the first **live** tick then sees a genuine transition and announces every
one of them. If that is the mechanism, the fix is that priming must reconcile the document as well
as the journal — and the same thing would happen to the installed build after any long gap, not just
to a stale debug copy.

**Reproduce before fixing.** Start with a checklist whose items are open and a loadout where they are
already done, and watch the first live tick. `d47-20260823.log` has the announcements
(`CalloutEngine: Callout checklist.done.ship …`) with timestamps that can be read against the startup
line. Note also that the Commander's remedy is stated in the report and is not silence: **mark them
done, say nothing.**
