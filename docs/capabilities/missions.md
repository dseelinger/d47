---
title: Missions
group: Ship
nav_order: 146
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
<p class="intro">One question, answered from the missions you have accepted. The same board, in the same order, is drawn under Commander › Missions.</p>
<section>
<h2><span class="num">1</span> Ask for the mission board.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">mission board</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"what missions do I have" — "read my missions" — "what am I hauling"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">No model is asked. It works with no key and no network.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Hear the three that matter most.</h2>
<svg viewBox="0 0 880 190" role="img" aria-label="Three missions named in order, then a count of the rest">
 <rect x="20" y="20" width="840" height="100" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="56" font-size="16" font-weight="700" fill="var(--text)">1. Expiring within the hour</text>
 <text x="44" y="82" font-size="16" font-weight="700" fill="var(--text)">2. Handed in at the station you are docked at</text>
 <text x="44" y="108" font-size="16" font-weight="700" fill="var(--text)">3. Everything else, soonest expiry first</text>
 <text x="20" y="160" font-size="16" fill="var(--text-muted)">Each names its destination and time left. The rest become a count, with the total reward.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="Only accepted missions are on the board">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">"I only see missions after you accept them."</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">The journal does not list what a station offers, so "what should I take" cannot be answered.</text>
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
<p class="intro">A short list you can listen to, ordered by what you can do about it.</p>
<section>
<h2><span class="num">1</span> Three named, the rest counted.</h2>
<svg viewBox="0 0 880 200" role="img" aria-label="Three missions read in full, the remainder as a count">
 <rect x="20" y="36" width="840" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="74" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">First, second, third, and two more.</text>
 <text x="440" y="104" text-anchor="middle" font-size="15" fill="var(--text-muted)">Then the total reward.</text>
 <text x="440" y="164" text-anchor="middle" font-size="16" fill="var(--text)">Eleven missions read in full take longer than a Commander will listen.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> A mission with no detail is named and left at that.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="A mission whose accept was never read is named by its internal name only">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="62" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">Mission_Courier</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">No destination, no time left: the journal has not shown its accept.</text>
</svg>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="journal.html"><span class="ct">Journal →</span><span class="cd">What D47 reads to know where you are.</span></a>
<a class="card" href="carrier.html"><span class="ct">Carrier →</span><span class="cd">The same kind of spoken report, for a fleet carrier.</span></a>
</div>
</div>
</div></div>

## The details

The missions you have accepted, read aloud. Part of every run.

### Ask for it

> "mission board"
> "what missions do I have"
> "read my missions"
> "what am I hauling"

```text
You have five missions. First, Courier Job Available, to Merchiston Dock, Kweleutahe, 32 minutes left. Second, Delivery, to Jameson Memorial, Shinrarta Dezhra, 5 hours 10 minutes left. Third, Massacre, to Kweleutahe, 9 hours left. And two more. 412,000 credits in rewards.
```

Missions expiring within the hour come first, then those handed in at the station you are docked at,
then the rest, soonest expiry first in each group. A mission whose accept the journal has not shown is
named by its internal name alone. With no missions the answer says so.

> "what missions should I take"

The missions a station offers are not in the journal, so the answer begins "I only see missions
after you accept them." and then reads your board.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `get_mission_board`

Reads the Commander's accepted missions: up to three, ranked by what can be acted on, each with its
destination and time left, then a count of the rest and the total reward. With `all` set it lists every
mission, one line each, with its faction, cargo, destination, progress, time left and reward.

```json
{"type":"object","properties":{"all":{"type":"boolean","description":"Set when the Commander asks about a particular mission, or about more than the three most urgent; lists every mission, one line each, in the same order."},"offered":{"type":"boolean","description":"Set when the Commander asks which missions to take; the answer then says that missions on offer at a station are not in the journal."}},"required":[],"additionalProperties":false}
```

</details>
