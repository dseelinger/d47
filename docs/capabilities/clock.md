---
title: Clock
group: Interface
nav_order: 139
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and the class decides behaviour, not just
  appearance. HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under
  that class would silently become what the in-app panel draws on this page. The docs site styles
  the two identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">One step. No AI, no key and no network needed.</p>
<section>
<h2><span class="num">1</span> Ask what time it is, in either world.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what time is it</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"what's the date" — "what day is it"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">It answers with both dates, so you never have to convert.</text>
</svg>
</section>
</div></div>
</details>

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">What time it is in both worlds at once.</p>
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
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="utilities.html"><span class="ct">Timers and alarms →</span><span class="cd">Countdowns and alarms that say their own name, behind the --utilities switch.</span></a>
<a class="card" href="conversation.html"><span class="ct">Conversation →</span><span class="cd">What the ship's AI is told with every question, the date included.</span></a>
</div>
</div>
</div></div>

## The details

What time it is, in both worlds at once. Part of every run.

### Ask for it

> "what's the date"
> "what time is it"
> "what day is it"

None of these needs an AI configured.

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
game state that goes with every conversation, already worked out, so the ship's AI can mention
the date without asking for it. It is never asked to add 1286 to anything: that is arithmetic,
and a model doing arithmetic in prose is wrong occasionally and confidently.

When [timers and alarms](utilities.html) are switched on, the same block also lists what is
running.

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

</details>
