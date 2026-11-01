---
title: Clock
group: Interface
nav_order: 142
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and the class decides behaviour, not just
  appearance. HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under
  that class would silently become what the in-app panel draws on this page. The docs site styles
  the two identically (main.scss extends one from the other); the app draws this band first and the How it works band after it.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to the time in both worlds. No AI, no key and no network needed.</p>
<section>
<h2><span class="num">1</span> Ask what time it is, in either world.</h2>
<p class="say">Say "what time is it".</p>
<p class="expect">D47 gives the time in the galactic calendar and in your own, with no AI configured.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what time is it</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"what's the date" — "what day is it"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">It answers with both dates, so you never have to convert.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Ask for the date or the day.</h2>
<p class="say">Say "what's the date" or "what day is it".</p>
<p class="expect">D47 answers with the galactic date and your own date together.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a date question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what's the date</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Both dates are one instant, so they never drift apart.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The galactic date reads the same for everyone.</h2>
<p class="say">Read the galactic date as D47 says it, whatever your computer shows.</p>
<p class="expect">It reads as, for example, 17 August 3312, however your computer writes dates.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The galactic date is written one way">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what day is it</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The galactic date is not a regional format.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Your own date follows your computer.</text>
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
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">D47 answers the date and time itself, in both calendars, from one instant.</p>
<section>
<h2><span class="num">1</span> One instant is written as two dates.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="The same moment written as a galactic date and a local date">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">You say "what time is it".</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">3312 out here, 2026 where you are.</text>
</svg>
<p class="body">The galactic date is your own date moved forward by a fixed number of years, so the two can never drift apart. No AI, key or network is needed.</p>
</section>
<section>
<h2><span class="num">2</span> The reply is one fixed sentence.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="The reply is built from two clocks">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">"It is 21:04 on 17 August 3312 out here, and 21:04 on Monday 17 August 2026 where you are."</text>
 <text x="440" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">GalacticTime.cs</text>
</svg>
<p class="body"><code>GalacticTime.Say</code> builds this sentence. The galactic date is written the same way for everyone; your own date follows your computer.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="utilities.html"><span class="ct">Timers and alarms →</span><span class="cd">Countdowns and alarms that say their own name, behind the --utilities switch.</span></a>
<a class="card" href="conversation.html"><span class="ct">Conversation →</span><span class="cd">What the core is told with every question, the date included.</span></a>
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
It is 21:04 on 17 August 3312 out here, and 21:04 on Monday 17 August 2026 where you are.
```

The galactic date is written the same way for everybody, because the galaxy's calendar is not a
regional format and a date that reads as 08/17 in one place and 17/08 in another reads as two
different days. Your own clock is written the way your computer writes dates.

**Directive 47 answers this itself.** No turn is taken, no provider is needed, and nothing is
spent — it works with no key configured and no network. Both dates also go into the block of live
game state that goes with every conversation, already worked out, so the core can mention
the date without asking for it. The core is told not to calculate either date from the other.

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

**Protected**: the model is not offered it. The keyword router reaches it.

</details>
