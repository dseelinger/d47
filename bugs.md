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
