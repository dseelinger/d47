---
title: Callouts
---

What Directive 47 says without being asked: danger, fuel, route progress, arrivals and material
milestones.

All of it comes from the files Elite already writes, as they change. Nothing here waits on the
language model, so a warning arrives when the thing happens rather than after something has
finished thinking about it — which for an interdiction is after it is over.

## Ask for it

> "what are you watching for"
> "stop calling things out"
> "start calling things out"

```text
I speak up about:
  danger: on
  fuel: on
  route: on
  long-jump: on
  arrival: on
  materials: on
Route progress every 3 jumps.
Home system is Shinrarta Dezhra.
```

## What it speaks up about

### Danger {#danger}

Interdiction, shields down, hull damage, overheating, and a full cargo hold.

Said once as it happens rather than repeatedly while it lasts — your shields going down is news;
your shields still being down half a second later is not. Submitting to an interdiction was your
decision and is not read back to you as an emergency.

An urgent warning cuts in rather than waiting its turn. A warning that arrives after Directive 47
has finished reading you a commodity list has arrived too late to be one.

### Fuel and range {#fuel}

Low fuel is the easy half. The half that matters is this one:

```text
Route warning. Hyades Sector DB-X d1-112 is class T and cannot be scooped, and the jump beyond
it is 61.2 light years against a maximum range of 52.3. Replot before you jump.
```

That is not a low-fuel warning — your tank can be nearly full when it becomes true — and without
it the first you know is when you are parked at a brown dwarf with no way out.

Every number in it came from your own game: the route and star classes from the route file, your
jump range from your ship's loadout, your fuel from the status file, and how much you actually
burn per jump averaged from the jumps you have already made this session. Nothing is looked up
anywhere.

### Route progress {#route}

Jumps remaining, what is next, and what is coming:

```text
14 jumps remaining. Next is Wredguia WD-K d8-30, scoopable.
Ahead on the route: Praea Euq QI-T d3-3 (a neutron star).
```

A hazard on the *very next* jump is always mentioned, whether or not it was a reporting jump.
Arriving unprepared at a neutron star is the thing this exists to prevent, and every-five-jumps
would miss it four times out of five.

Whether a star can be scooped is worked out properly rather than from the first letter of its
class. The KGBFOAM rule gets two cases wrong in opposite directions, and both matter: Herbig AeBe
stars start with A and cannot be scooped, while orange giants carry a suffix and can. A class
Directive 47 does not recognise is called unknown rather than unscoopable — routing you around a
star that would have refuelled you is its own kind of harm.

### Long jumps {#long-jump}

A little conversation during a longer-than-usual jump. It starts once you are actually in
hyperspace, not when the drive begins charging — throttle up and cancel and you will hear
nothing.

### Arrivals {#arrival}

Your home system, where your carrier is, ships you have stored where you have just arrived, and
stations that offer engineering.

**There is no built-in list of which engineer lives where.** That kind of list goes stale every
game update, and inventing one is exactly the confident wrong answer Directive 47 is built not to
give. Engineering is recognised from what the station itself advertises when you dock — which
also means it keeps working when a new engineer is added.

### Material milestones {#materials}

Your first unit of a material, then a quarter, half and three-quarters full, a running count
after that, and full.

Start Directive 47 after Elite and it catches up on the session so far first, so materials you
already had do not all announce themselves as firsts.

Elite never reports how much of a material you can carry — the game simply stops accepting more —
so the percentages come from a table of material grades built from the community-maintained id
list that Coriolis and EDEngineer use, rather than from anyone's memory. A material too new for
that table still announces your first unit; the percentages stay quiet rather than being counted
against a number nobody checked.

## Settings

### Speak without being asked {#enabled}

Off means Directive 47 only ever answers. Every warning above stops with it; everything else
keeps running.

> "stop calling things out"
> "start calling things out"

### Route progress interval {#route-interval}

In jumps. The right answer depends entirely on the trip: every 3 jumps is reassuring over 20 and
unbearable over 300. Set it to `0` to silence the progress line while keeping the hazard warnings.

### Long jump threshold {#long-jump-threshold}

In seconds, counted from entering hyperspace.

### Home system {#home-system}

Where you consider home, for the arrival callout. There is no default — no journal event reports
that.

---

**The model can ask what Directive 47 is watching for; it cannot switch a warning off.** Every
toggle here is changeable from the panel, by a bound key, or by saying one of the phrases above —
and not by anything the model calls. Directive 47 reads in-game messages from anyone in range,
and a model that could disable the interdiction warning is one that could be told to by the
Commander doing the interdicting.

### Ambient remarks {#ambient}

The occasional in-character observation about where you are, said because *nothing* has
happened. Everything else on this page speaks because something did.

Three rules keep it from being noise. It waits out an interval — forty-five seconds out of the box,
and in seconds rather than minutes because the interesting end of the range is finer than a minute.
It waits for the situation to have settled for ninety seconds rather than firing on the
transition, because Status.json flips several times a minute during an approach and a remark
about being docked that arrives as you are lifting off is worse than silence. And it never
remarks on the same situation twice running.

Seven situations are covered: docked, landed, supercruise, normal space, fuel scooping, in the
SRV, and on foot.

With a language model configured, the core aboard writes its own line and it is genuinely theirs
— Chart will tell you about the sky, the Quartermaster about what the run cost. Without one,
there are ten written lines per situation, shared across all eleven cores rather than one set
each. That is consistent rather than a compromise: with no model there is no persona flavour
anywhere else either.

```text
The drive note has not changed in some minutes. That is what it sounds like when it is right.
```

**Switching personality off silences these entirely**, which is the one place that switch
reaches a callout. It is in that item's own acceptance criteria: plain answers, no flavour, no
ambient remarks.

Set the interval to `0` if you want the switch without finding the switch.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `get_callouts`

Read-only. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Two sources feed the warnings, deliberately. The journal reports **transitions** — shields went
down, hull was hit — and `Status.json` reports **conditions** — shields are *still* down. A
warning built on events alone goes quiet the moment the game stops repeating itself; one built on
conditions alone cannot tell a new emergency from an ongoing one. Conditions announce on the edge
into the condition, because `Status.json` is rewritten several times a second and announcing on
the level would be a warning per tick.

The material grade table is **derived, not written**. `tools/gen-material-grades.py` generates
`MaterialGrades.g.cs` from [EDCD/FDevIDs](https://github.com/EDCD/FDevIDs) `material.csv`. The
generator is not part of the build and the app never runs it or reaches the network for it; its
output is committed and regenerated when Frontier adds or reclassifies a material. This is not
academic: the first draft of the tests asserted Antimony was grade 5, from memory. The derived
table said grade 4, and it was right — raw materials come in categories of four graded 1–4, so no
raw material is grade 5 at all. A hand-written table would have shipped that error, and it would
have surfaced as a milestone firing at the wrong count with nothing reporting a problem.

The grade-to-capacity rule — 300/250/200/150/100 for grades 1–5 — lives beside the generated
table rather than in it: five numbers that do not change when Frontier adds a material, so
regenerating cannot disturb them.

The tick loop is synchronous and must not block, so a callout does not speak. It returns an
`Announcement`, the engine queues it, and the app drains that queue onto the thread pool.
Announcements are spoken one at a time in the order queued — two synthesised concurrently would
arrive in whichever order the network returned them, and "shields are down" is not
interchangeable with "route complete". An urgent one silences the queue before speaking, on the
`Alert` channel above `Speech` in the one audio arbiter.

Condition-based warnings carry a cooldown keyed by what they are about, coarse enough that a
repeat is suppressed and specific enough that a different warning is not. A callout that throws
is logged and skipped and the rest still run: one broken callout must not take the danger
warnings down with it.

</details>
