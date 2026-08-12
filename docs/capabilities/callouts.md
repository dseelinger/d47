---
title: Callouts
---

# Callouts

**Group:** Voice
**Capability id:** `callouts`

What d47 says without being asked: danger, fuel, route progress, arrivals and material
milestones. Everything here fires from the journal and the state files Elite writes, on the tick
loop, with no model in the path.

**These fire on the event, never at the model's discretion.** That is the checklist's own wording
and it is the whole design. An alert routed through a turn arrives after the model has finished
thinking, which for an interdiction is after it is over. Nothing here consults the language
model, and nothing in the journal can talk a warning out of firing — which matters, because
journal content is untrusted input (architecture.md §7).

## Try it

> "what are you watching for"
> "stop calling things out"
> "start calling things out"

## Tool

### `get_callouts`

Read-only. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

```text
I speak up about:
  danger: on
  fuel: on
  route: on
  long-jump: on
  arrival: on
  materials: on
Route progress every 5 jumps.
Home system is Shinrarta Dezhra.
```

**The model can ask what d47 is watching for; it cannot switch a warning off.** Every toggle
below is a protected settings row — reachable from the panel, from a hotkey and through the
model-free keyword router, and not from the tool surface. This is the trust boundary rather than
caution: anything the model can call, a hostile in-game message can attempt to invoke, and a
model that can disable the interdiction warning is one that can be told to by the Commander
doing the interdicting.

## What gets called out

### Danger {#danger}

Interdiction, shields down, hull damage, overheating, and a full cargo hold.

Two sources, deliberately. The journal reports **transitions** — shields went down, hull was
hit — and `Status.json` reports **conditions** — shields are *still* down, fuel is *still* low. A
warning built on events alone goes quiet the moment the game stops repeating itself; one built on
conditions alone cannot tell a new emergency from an ongoing one.

Conditions are announced on the edge into the condition, not while it holds. `Status.json` is
rewritten several times a second, so announcing on the level would be a warning per tick.

Submitting to an interdiction is a choice the Commander made, and is not announced back to them
as an emergency.

### Fuel and range {#fuel}

The low-fuel warning is the easy half. The half that matters is the one the checklist names:
**the next star on the route is unscoopable and the one after it is out of range.** That is not a
low-fuel condition — the tank can be nearly full when it becomes true — and it is invisible until
the Commander is already sitting at a brown dwarf with no way out.

```text
Route warning. Hyades Sector DB-X d1-112 is class T and cannot be scooped, and the jump beyond
it is 61.2 light years against a maximum range of 52.3. Replot before you jump.
```

Every figure behind that sentence was reported by the game: hop coordinates and star classes from
`NavRoute.json`, the jump range from the `Loadout` event, the fuel level from `Status.json`, and
fuel actually burned per jump averaged from the `FSDJump` events already seen this session. A
figure derived from what happened beats one derived from a formula, and neither is a lookup.

The warning is unconditional rather than something the model may decide is not worth mentioning.

### Route progress {#route}

Jumps remaining, the next system, and hazards ahead — read from the route file Elite writes
locally, so no route-planning service is involved.

```text
14 jumps remaining. Next is Wredguia WD-K d8-30, scoopable.
Ahead on the route: Praea Euq QI-T d3-3 (a neutron star).
```

A hazard on the *very next* jump is said whether or not this was a reporting jump. Arriving
unprepared at a neutron star is the failure this exists to prevent, and "every 5 jumps" would
land on it four times out of five.

**Scoopable-star ambiguity is resolved rather than guessed**, which the checklist asks for
explicitly. The KGBFOAM mnemonic is a rule about a star's first letter, and applying it as a
first-letter test gets two cases wrong in opposite directions: Herbig `AeBe` stars begin with A
and are *not* scoopable, while `K_OrangeGiant` and the other giant and supergiant variants carry
a suffix and *are*. A class d47 does not recognise is reported as unknown rather than as
unscoopable — routing a Commander around a star that would have refuelled them is its own kind
of harm.

### Long jumps {#long-jump}

Flavour during a longer-than-normal hyperspace jump.

Fired once hyperspace has **actually been entered**, not on the jump being initiated. That
distinction is the item's own wording and it matters: `StartJump` is written while the FSD is
still charging, and a Commander who throttles up and cancels never enters hyperspace at all.
Only `JumpType: "Hyperspace"` counts — the same event says `"Supercruise"` far more often.

### Arrivals {#arrival}

Your home system, where your carrier is, ships stored where you have just arrived, and stations
that offer engineering.

**No table of engineer bases or notable stations is shipped.** A hardcoded list of which engineer
lives where is game data that goes stale on every update and that d47 has no source for;
inventing one would be exactly the confident wrong answer the guardrails exist to prevent.
Engineering is recognised from the station's own advertised services in the `Docked` event, which
also means it keeps working when a new engineer is added.

### Material milestones {#materials}

The first unit of a material, then 25/50/75%, a running count above 75%, and full.

**The tracker is primed from the session backlog at startup**, which the checklist calls out and
which is why the tick loop marks its first tick. Without it, starting d47 after Elite means every
material already gathered counts as a "first unit" the moment the backlog is read, and the real
milestones never fire because they have already been passed silently.

**The percentage milestones are currently inert, and this is stated rather than faked.** Elite's
per-material caps are set by material grade, and no journal event, status file or inventory
snapshot reports either the grade or the cap — the game simply stops accepting more. A table of
roughly 130 material grades is game data d47 would carry with no way to verify it, and a wrong
entry surfaces as a milestone announced at the wrong number, which is indistinguishable from
working correctly. So capacity is a lookup supplied from outside, it currently answers "unknown"
for every material, and every milestone that needs it stays silent. **The first-unit milestone
needs no capacity and works today.**

## Settings

### Speak without being asked {#enabled}

Off means d47 only ever answers. Every warning stops with it; everything else keeps running.

Reachable by voice through the model-free keyword router — "stop calling things out", "enable
callouts" — because a protected row still has to be settable without hands on the panel.

### Route progress interval {#route-interval}

In jumps. The checklist makes this a setting because the right answer depends entirely on the
route: every 5 jumps is reassuring on a 20-jump trip and unbearable on a 300-jump one. `0`
silences the progress line while leaving the hazard warnings on.

### Long jump threshold {#long-jump-threshold}

In seconds, defaulting to the 20 the checklist specifies, and measured from entering hyperspace
rather than from starting the jump.

### Home system {#home-system}

Named for the arrival callout. There is no default: where someone considers home is not something
any journal event reports.

## How a synchronous tick produces speech

The tick loop is synchronous and must not block — a subscriber that waits on the network stalls
push-to-talk edge detection and every callout behind it. So a callout does not speak. It returns
an `Announcement`, the engine queues it, and the app drains that queue onto the thread pool.

Announcements are spoken one at a time and in the order they were queued. Two callouts landing on
the same tick and synthesised concurrently would arrive in whichever order the network happened
to return them, and "shields are down" is not interchangeable with "route complete".

An urgent callout silences the queue before speaking rather than joining it, on the
`Alert` channel that sits above `Speech` in the one audio arbiter (architecture.md D7). That is
the difference between a warning and a remark: an interdiction announced after d47 finishes
reading out a commodity list has arrived after the interdiction.

## Repetition

A condition-based warning is true on hundreds of consecutive ticks at 10 Hz. Each announcement
therefore carries a cooldown keyed by what it is about, so "low fuel" is said once every couple
of minutes rather than ten times a second. The keys are coarse enough that a repeat is
suppressed and specific enough that a different warning is not.

A callout that throws is logged and skipped, and the callouts after it still run. One broken
callout must not silence the rest, and must certainly not take the danger warnings down with it.
