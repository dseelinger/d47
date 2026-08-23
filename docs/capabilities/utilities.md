---
title: Clocks and timers
group: Interface
nav_order: 137
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">What time it is in both worlds at once, and timers and alarms that say their own name.</p>
<section>
<h2><span class="num">1</span> Two dates. One instant.</h2>
<svg viewBox="0 0 880 250" role="img" aria-label="The same moment written twice, 1286 years apart">
 <rect x="30" y="40" width="360" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="210" y="84" text-anchor="middle" font-size="24" font-weight="800" fill="var(--text)">17 Aug 3312</text>
 <text x="210" y="116" text-anchor="middle" font-size="16" fill="var(--text-muted)">out there</text>
 <text x="440" y="86" text-anchor="middle" font-size="20" font-weight="800" fill="var(--accent)">+1286</text>
 <text x="440" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">years</text>
 <rect x="490" y="40" width="360" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="670" y="84" text-anchor="middle" font-size="24" font-weight="800" fill="var(--text)">17 Aug 2026</text>
 <text x="670" y="116" text-anchor="middle" font-size="16" fill="var(--text-muted)">where you are</text>
 <text x="440" y="196" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">One moment presented twice, not two clocks.</text>
 <text x="440" y="228" text-anchor="middle" font-size="16" fill="var(--text-muted)">Which is why they can never drift apart, and why D47 never asks a model to add 1286.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> A timer is a stretch. An alarm is a moment.</h2>
<svg viewBox="0 0 880 288" role="img" aria-label="Timers measure a stretch and do not survive a restart; alarms name a moment and do">
 <rect x="20" y="20" width="410" height="150" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="225" y="62" text-anchor="middle" font-size="22" font-weight="800" fill="var(--text)">TIMER</text>
 <text x="225" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">forty minutes for the mining run</text>
 <line x1="70" y1="122" x2="380" y2="122" stroke="var(--accent-muted)" stroke-width="6" stroke-linecap="round"/>
 <text x="225" y="152" text-anchor="middle" font-size="16" fill="var(--text-muted)">a stretch of time</text>
 <rect x="450" y="20" width="410" height="150" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="655" y="62" text-anchor="middle" font-size="22" font-weight="800" fill="var(--text)">ALARM</text>
 <text x="655" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">seven in the morning</text>
 <line x1="500" y1="122" x2="810" y2="122" stroke="var(--border)" stroke-width="6" stroke-linecap="round"/>
 <circle cx="700" cy="122" r="11" fill="var(--accent)"/>
 <text x="655" y="152" text-anchor="middle" font-size="16" fill="var(--text-muted)">a single moment</text>
 <rect x="20" y="196" width="410" height="58" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="225" y="232" text-anchor="middle" font-size="17" fill="var(--text-muted)">gone if D47 restarts</text>
 <rect x="450" y="196" width="410" height="58" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="655" y="232" text-anchor="middle" font-size="17" fill="var(--accent)">survives a restart</text>
 <text x="440" y="282" text-anchor="middle" font-size="16" fill="var(--text-muted)">Half of forty minutes through a crash is a question nobody can answer, so it is not asked.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> One it could not sound, it owns up to.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="An alarm that came due while D47 was closed is reported at the next start rather than sounded late">
 <rect x="20" y="26" width="250" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="64" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">07:00</text>
 <text x="145" y="92" text-anchor="middle" font-size="15" fill="var(--text-muted)">due, and D47 was closed</text>
 <line x1="282" y1="69" x2="308" y2="69" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="322,69 306,61 306,77" fill="var(--accent-muted)"/>
 <rect x="334" y="26" width="526" height="86" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="597" y="62" text-anchor="middle" font-size="17" fill="var(--text)">“That alarm was due at 07:00 and I was not running.</text>
 <text x="597" y="88" text-anchor="middle" font-size="17" fill="var(--text)">I have not sounded it since.”</text>
 <text x="440" y="164" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">Told at the next start, with when it was due.</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text-muted)">Sounding it hours late as though nothing had happened would be worse than not sounding it.</text>
 <text x="440" y="224" text-anchor="middle" font-size="16" fill="var(--text-muted)">A chime says something finished; the name says which.</text>
</svg>
<p class="body">Cancelling is reachable from this tab and from saying so, and from nowhere else — the AI is not offered it and is refused if it asks. Naming one that matches nothing, or two, is answered rather than guessed at: cancelling the wrong alarm of two is worse than being asked which.</p>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="checklists.html"><span class="ct">Checklists →</span><span class="cd">The other thing on the panel that remembers what you are in the middle of.</span></a>
<a class="card" href="speech.html"><span class="ct">Speech →</span><span class="cd">The voice that says the name, and the cues around it.</span></a>
<a class="card" href="settings.html"><span class="ct">Settings →</span><span class="cd">Where the file these live in sits, and everything else D47 writes.</span></a>
</div>
</div>
</div></div>

## The details

What time it is, in both worlds at once, and timers and alarms that say their own name.

### Ask for it

> "what's the date"
> "what time is it"
> "set a timer for forty minutes for the mining run"
> "wake me at 07:00"
> "cancel the mining timer"

The first two need no AI configured at all. So does cancelling.

### Two clocks, one instant

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

### Timers and alarms

A **timer** is a stretch of time — forty minutes for a mining run. An **alarm** is a moment —
seven in the morning.

They are set from the Utilities tab or by saying so, and they are named, because the name is how
Directive 47 tells you which one finished.

#### What happens when one goes off

A short rising chime, and then Directive 47 **says the name**. One clip for all of them rather
than a tone per timer: the sound says *something finished* and the sentence says which, which the
voice does better than a chime ever could. It goes through the same audio arbiter as everything
else, so it either waits for a sentence to end or lands on top of it according to the same rules.

#### Alarms survive a restart. Timers do not.

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

#### The file

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

### Cancelling is yours

**Not offered to the AI, and refused if it asks.** Cancelling is reachable from the Utilities tab
and from the phrases above, and from nowhere else. Protected is about *who is asking* rather than
*how* — so saying "cancel the alarm" works, and a hostile message arriving in your comms panel
cannot reach one.

Naming one that matches nothing, or matches two, is answered rather than guessed at. Cancelling
the wrong alarm of two is worse than being asked which.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `say_the_time`

The date and time in both worlds. Answered by D47 itself rather than by the model: no turn, no
provider, no tokens.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**Protected**, which here is about cost as much as about safety: the advertised tool surface is
paid for on every turn whether or not anybody asks the time, and this question does not need the
model at all. The keyword router reaches it; the model never sees it.

#### `set_timer`

Start a countdown that says its own name when it finishes. Minutes, from the moment it is set.
Timers do not survive D47 restarting; use an alarm for a wall-clock moment.

```json
{"type":"object","properties":{"minutes":{"type":"number","description":"How long, in minutes."},"name":{"type":"string","description":"What to call it. Said back when it finishes."}},"required":["minutes","name"],"additionalProperties":false}
```

Advertised, unlike the rest of this capability, and the asymmetry is the point: a duration arrives
as English — "about forty minutes", "an hour and a half" — which is the one thing a model does
better than a closed grammar, and the keyword router cannot extract an argument from free text
without starting to guess.

#### `set_alarm`

Set an alarm for a wall-clock time today or tomorrow. Alarms survive D47 restarting, and one that
came round while D47 was closed is reported afterwards rather than sounded late.

```json
{"type":"object","properties":{"at":{"type":"string","description":"Local time for the Commander, as 24-hour HH:mm."},"name":{"type":"string","description":"What to call it. Said back when it goes off."}},"required":["at","name"],"additionalProperties":false}
```

"Seven in the morning" said at nine in the evening means the seven that is coming rather than the
one that went, so a time already past today is set for tomorrow rather than refused.

#### `cancel_reminder`

Cancel a timer or an alarm by name. The Commander's own act: not offered to the model, and refused
if it asks.

```json
{"type":"object","properties":{"name":{"type":"string","description":"Which one. Omit to cancel everything running."}},"required":[],"additionalProperties":false}
```

</details>
