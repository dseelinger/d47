---
title: Clocks and timers
group: Interface
nav_order: 135
---

What time it is, in both worlds at once, and timers and alarms that say their own name.

## Ask for it

> "what's the date"
> "what time is it"
> "set a timer for forty minutes for the mining run"
> "wake me at 07:00"
> "cancel the mining timer"

The first two need no AI configured at all. So does cancelling.

## Two clocks, one instant

Elite Dangerous runs **1286 years ahead**, so today is also a date in 3312. That is arithmetic
over the same moment rather than a second clock — one instant presented twice, which is why the
two can never drift out of step with each other.

```text
21:04 on 17 August 3312 out here, and 21:04 on Monday 17 August 2026 where you are.
```

The galactic date is written the same way for everybody, because the galaxy's calendar is not a
regional format and a date that reads as 08/17 in one place and 17/08 in another reads as two
different days. Your own clock is written the way your computer writes dates.

**Directive 47 answers this itself.** No turn is taken, no provider is needed, and nothing is
spent — it works with no key configured and no network. Both dates also go into the block of live
game state that rides along with every conversation, already worked out, so the ship's AI can
mention the date without asking for it. It is never asked to add 1286 to anything: that is
arithmetic, and a model doing arithmetic in prose is wrong occasionally and confidently.

## Timers and alarms

A **timer** is a stretch of time — forty minutes for a mining run. An **alarm** is a moment —
seven in the morning.

They are set from the Utilities tab or by saying so, and they are named, because the name is how
Directive 47 tells you which one finished.

### What happens when one goes off

A short rising chime, and then Directive 47 **says the name**. One clip for all of them rather
than a tone per timer: the sound says *something finished* and the sentence says which, which the
voice does better than a chime ever could. It goes through the same audio arbiter as everything
else, so it either waits for a sentence to end or lands on top of it according to the same rules.

### Alarms survive a restart. Timers do not.

An alarm for 07:00 is a promise about a wall-clock moment that outlives the process. A
forty-minute timer through a crash is a question nobody can answer — half elapsed, or start
again? Neither answer is right, so it is not asked.

**An alarm that could not sound says so afterwards.** Directive 47 cannot fire while it is closed,
so an alarm that came round in the meantime is reported the next time it starts, with when it was
due:

```text
Wake up: that alarm was due at 07:00 and I was not running. I have not sounded it since.
```

Sounding it hours late as though nothing had happened would be worse than not sounding it at all.

### The file

Alarms live in one file beside the executable, and it is yours to edit:

```json
{
  "alarms": [
    {
      "id": "8f2c1e40b7a94d2f",
      "name": "Wake up",
      "due": "2026-08-18T06:00:00+00:00"
    }
  ]
}
```

Edited by hand, it takes effect without a restart. A line the file gets wrong is **reported rather
than silently dropped** — an alarm somebody relies on to leave the house is not a thing to lose
quietly — and the rest of the file still loads.

## Cancelling is yours

**Not offered to the AI, and refused if it asks.** Cancelling is reachable from the Utilities tab
and from the phrases above, and from nowhere else. Protected is about *who is asking* rather than
*how* — so saying "cancel the alarm" works, and a hostile message arriving in your comms panel
cannot reach one.

Naming one that matches nothing, or matches two, is answered rather than guessed at. Cancelling
the wrong alarm of two is worse than being asked which.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `say_the_time`

The date and time in both worlds. Answered by D47 itself rather than by the model: no turn, no
provider, no tokens.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**Protected**, which here is about cost as much as about safety: the advertised tool surface is
paid for on every turn whether or not anybody asks the time, and this question does not need the
model at all. The keyword router reaches it; the model never sees it.

### `set_timer`

Start a countdown that says its own name when it finishes. Minutes, from the moment it is set.
Timers do not survive D47 restarting; use an alarm for a wall-clock moment.

```json
{"type":"object","properties":{"minutes":{"type":"number","description":"How long, in minutes."},"name":{"type":"string","description":"What to call it. Said back when it finishes."}},"required":["minutes","name"],"additionalProperties":false}
```

Advertised, unlike the rest of this capability, and the asymmetry is the point: a duration arrives
as English — "about forty minutes", "an hour and a half" — which is the one thing a model does
better than a closed grammar, and the keyword router cannot extract an argument from free text
without starting to guess.

### `set_alarm`

Set an alarm for a wall-clock time today or tomorrow. Alarms survive D47 restarting, and one that
came round while D47 was closed is reported afterwards rather than sounded late.

```json
{"type":"object","properties":{"at":{"type":"string","description":"Local time for the Commander, as 24-hour HH:mm."},"name":{"type":"string","description":"What to call it. Said back when it goes off."}},"required":["at","name"],"additionalProperties":false}
```

"Seven in the morning" said at nine in the evening means the seven that is coming rather than the
one that went, so a time already past today is set for tomorrow rather than refused.

### `cancel_reminder`

Cancel a timer or an alarm by name. The Commander's own act: not offered to the model, and refused
if it asks.

```json
{"type":"object","properties":{"name":{"type":"string","description":"Which one. Omit to cancel everything running."}},"required":[],"additionalProperties":false}
```

</details>
