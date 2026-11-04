---
title: Community goals
group: Knowledge
nav_order: 119
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
<p class="intro">Two steps to knowing where you stand in a goal.</p>
<section>
<h2><span class="num">1</span> Ask what is running.</h2>
<p class="say">Say "what community goals are running".</p>
<p class="expect">D47 lists each goal from the boards at stations you have docked at: where it is flown, the time left, its tier, the contributors, what has been delivered and a line starting "You:" with your own standing.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what community goals are running</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It reads your own journal, needs no internet and sends nothing.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Goals that have ended are left out unless you ask for them.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Ask how your sales are going.</h2>
<p class="say">Say "how have I done today".</p>
<p class="expect">D47 says what the goal's commodity has made or lost today, net of what the cargo cost, or "No Palladium sold today." when nothing was sold.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question about today's sales">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">how have I done today</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"How have I done this week" and "how have I done this session" ask the other two.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">The commodity is Palladium.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Ask before you have docked at a station that lists the goal.</p>
<p class="expect">D47 says "I have no community goals yet" and that they are reported when you dock somewhere that lists one.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="The board only comes from stations you have docked at.">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">No dock, no board.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Goals at stations you have not docked at are not listed.</text>
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
<p class="intro">Your journal carries the board, and each goal is checked against the clock before it is listed.</p>
<section>
<h2><span class="num">1</span> The board comes from stations you have docked at.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="The journal carries the community goal board at every station you have docked at">
 <rect x="20" y="40" width="840" height="118" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="78" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">YOUR JOURNAL</text>
 <text x="440" y="110" text-anchor="middle" font-size="14" fill="var(--text-muted)">the board at that station: tier, contributors,</text>
 <text x="440" y="134" text-anchor="middle" font-size="14" fill="var(--text-muted)">handed in, and your own share</text>
 <text x="440" y="198" text-anchor="middle" font-size="16" fill="var(--text)">Elite writes it when you dock, so it covers the stations you have docked at.</text>
 <text x="440" y="228" text-anchor="middle" font-size="15" fill="var(--text-muted)">Nothing is requested and nothing leaves this machine.</text>
</svg>
<p class="body">Goals running where you have not docked are not listed. The answer ends by saying so.</p>
</section>
<section>
<h2><span class="num">2</span> An ended goal is hidden, not announced.</h2>
<svg viewBox="0 0 880 200" role="img" aria-label="A board can still carry a goal that has ended">
 <rect x="20" y="36" width="840" height="100" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="46" y="74" text-anchor="start" font-size="16" fill="var(--text)">A board written when you docked can name a goal that has since ended.</text>
 <text x="46" y="108" text-anchor="start" font-size="16" fill="var(--danger)">So every goal is checked against the clock before it is listed.</text>
 <text x="440" y="172" text-anchor="middle" font-size="15" fill="var(--text-muted)">The deadline is on the second line of every entry.</text>
</svg>
<p class="body">A goal past its expiry is left out unless you ask for finished goals. The answer ends with <code>This is only what your journal has reported, which is the board at stations you have docked at.</code></p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="journal.html"><span class="ct">Journal →</span><span class="cd">The file all of this is read out of, and what else is in it.</span></a>
<a class="card" href="routes.html"><span class="ct">Routes →</span><span class="cd">Getting to the station a goal is flown from.</span></a>
</div>
</div>
</div></div>

## The details

What community goals are running, what tier they have reached, and how you are doing in them.

> "what community goals are running"
> "how am I doing in the community goal"
> "what tier is the community goal at"

The board comes off your own journal and needs nothing else. It cannot see a goal running somewhere you
have not docked. "How am I doing in the community goal" is the sales ledger (below), not the board.

### What the journal carries

Elite writes a `CommunityGoal` event carrying the board at the station you docked at: the goal, where it
is flown, the tier it has reached, how many Commanders are on it, how much has been handed in, your own
contribution, your percentile band and whether you are in the top rank. D47 merges boards by goal
rather than replacing them, so goals from earlier stops stay until they expire. Signing up, opting out
and being paid arrive as their own events and are merged onto the goal.

A board can name a goal that has ended, so every goal is checked against the clock before it is listed.
An ended goal is left out unless you ask for finished goals (`include_finished`), which is how you find
out what a goal paid you.

```text
2 community goals from your journal:

Alliance Research Initiative — Trade
  Neville Horizons, Kaushpoos — 3 days left
  Tier 1 of 5, 101 contributors, 10,062 delivered.
  You: you have contributed 562, top 50%, the band pays 200,000 cr, signed up.
  Reported 2 hours ago.

Operation Andronicus
  The Oracle, Pleiades Sector IR-W d1-55 — ended 4 days ago
  Tier 4, 408 contributors, 2,838,230,000 delivered, met.
  You: not signed up as far as I know.
  Reported 4 days ago.
```

The deadline is on the second line of every entry. A goal with more than thirty days left shows its
expiry date instead of a countdown. With nothing listed, D47 says "No community goals are running in what
I have seen." and adds that there may be some where you have not been.

### The Community Goal search

One saved search, run again and again. It looks for where to buy the goal's commodity, Palladium: from the
goal's own system, nearest first, within 250 light years, prices under eight hours old, a large pad, a
station within 50,000 light seconds of the star and at least 10,000 in stock. Fleet carriers are left out,
as in any galaxy search.

Say "community goal search" or "CG search" to run it. The keyword router takes the phrase whole and points
the galaxy tool at it with the arguments already filled in, so no model reinterprets it.

#### Where it measures from

The goal's system, not the ship's. The origin is the system of a live goal on your board: the one you are
running (a contribution or a sign-up is the evidence), then the one whose title names the commodity, then
the most recently reported. A goal past its expiry is never the origin. With no live goal the search
measures from the ship.

The spoken answer says which it was:

```text
Nearest to Ega, the goal's system, for buying Palladium: Coleman Relay (Enayex), 11 ly. System is
on your clipboard.
```

The heading above the ranking reads "Palladium near Ega, the goal's system, nearest first".

To ask from the ship on purpose, say "CG search from here" or "community goal search from here". The
clipboard and "set a course" work from wherever the ship is either way. The distance column is measured
from whatever the search was asked from.

#### What comes back

The spoken answer is one sentence naming the nearest station that passes every filter. The page draws the
full ranked list: station, system, pad, distance from the star, distance, supply, price and the age of the
quote.

### The ledger

What the goal's commodity has made or lost, net of what the cargo cost. The cost is Elite's own
`AvgPricePaid` on each sale. When Elite writes zero, which it does for cargo that was never bought, the
cost is the average of your own `MarketBuy` events for the commodity, and failing that the sale is counted
gross, which is the accurate figure for a mined load.

Three stretches. Only the first is spoken automatically:

- **This session**: the running total after every sale of the commodity, said automatically, for example
  "That's 2 million up this session." The
  [Community Goal sales](callouts.html#community-goal-sales) row on the Callouts page switches the
  sentence off, and nothing is said while d47 is catching up on a journal it did not watch being written.
- **Today**: asked for, never announced: "how have I done today".
- **This week**: asked for, never announced: "how have I done this week", or "how have I done since the
  last maintenance window" (also "since maintenance", "since the last maintenance", "since the tick").
  "How have I done on the community goal" and "how am I doing on the community goal" give this week too.
  The week turns at [the week boundary](#week-boundary), Thursday 07:00 UTC by default, and the answer
  names the boundary it used.

The day and the week reach across sessions because the journal files Elite keeps are read at start and
the live journal after that. Nothing is written under `data\`.

```text
Palladium: 3 million up this week — 3 sales, 300 tonnes, 15,000,000 cr in against
12,000,000 cr the cargo cost.
```

A stretch with no sales says "No Palladium sold today." (or "this week", "this session").

#### The week boundary {#week-boundary}

Which day, UTC, and which hour "this week" turns over on. These are two advanced rows, The week turns on
and …at this hour, UTC, under Settings › Your core › Callouts, group Plans and reminders. They show with
Show every setting on. The day defaults to Thursday and the hour to 7, from 0 to 23.

### Tools

#### `get_community_goals`

```json
{"type":"object","properties":{"include_finished":{"type":"boolean","description":"Also list goals that have already expired, with what they paid out. Default false \u2014 an expired goal cannot be contributed to."},"name":{"type":"string","description":"Only goals whose title contains this. Leave out for all of them."}},"required":[],"additionalProperties":false}
```

#### `get_community_goal_earnings`

What the Community Goal commodity has made or lost, net of cost, over one of three stretches. The nine
phrases above reach it through the keyword router without a model; the model can call it for anything
phrased differently.

```json
{"type":"object","properties":{"range":{"type":"string","description":"Which stretch. Default session.","enum":["session","today","week"]}},"required":[],"additionalProperties":false}
```
