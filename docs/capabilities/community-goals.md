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
  the two identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to knowing where you stand in a goal.</p>
<section>
<h2><span class="num">1</span> Ask what is running.</h2>
<p class="say">Turn on web access, then ask what community goals are on.</p>
<p class="expect">D47 lists the running goals; with web access off it says it cannot look.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what community goals are on</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">This one needs the internet. It is off until you turn web access on.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Your own contribution comes out of your journal, not the network.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Read the tier and your own share.</h2>
<p class="say">Listen for the tier and your own contribution.</p>
<p class="expect">You hear the goal's current tier and the tonnes you have handed in, taken from your journal.</p>
<svg viewBox="0 0 880 200" role="img" aria-label="A community goal showing its tier, the contribution so far and where the Commander sits">
 <rect x="20" y="16" width="840" height="168" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="54" font-size="17" font-weight="700" fill="var(--text)">Alliance Rescue Effort</text>
 <text x="836" y="54" text-anchor="end" font-size="16" fill="var(--accent)">TIER 4 of 8</text>
 <rect x="44" y="76" width="792" height="14" fill="var(--border)"/>
 <rect x="44" y="76" width="396" height="14" fill="var(--accent)"/>
 <text x="44" y="128" font-size="16" fill="var(--text)">You have handed in 1,240 tonnes.</text>
 <text x="44" y="164" font-size="15" fill="var(--text-muted)">Your figure is read from your own journal, so it is right even when the site is slow.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Turn on web access before you ask.</p>
<p class="expect">The answer names the goal and your share; the Privacy page lists what leaves your machine.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="It needs the internet, and that is off by default.">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">It needs the internet, and that is off by default.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">With web access off there is nothing to report. The privacy page says exactly what leaves.</text>
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
<p class="intro">What community goals are running, what tier they have reached, and how you are doing in them.</p>
<section>
<h2><span class="num">1</span> Your journal already knows most of this.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="The journal carries the community goal board at every station you have docked at">
 <rect x="20" y="40" width="840" height="118" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="78" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">YOUR JOURNAL</text>
 <text x="440" y="110" text-anchor="middle" font-size="14" fill="var(--text-muted)">the whole board: tier, contributors,</text>
 <text x="440" y="134" text-anchor="middle" font-size="14" fill="var(--text-muted)">handed in, and your own share</text>
 <text x="440" y="198" text-anchor="middle" font-size="16" fill="var(--text)">The event is written off the noticeboard at a station, so it reports where you are.</text>
 <text x="440" y="228" text-anchor="middle" font-size="15" fill="var(--text-muted)">So it covers the stations you have docked at, and no others.</text>
</svg>
<p class="body">Nothing is requested and nothing leaves this machine. Goals running where you have not docked are not listed.</p>
</section>
<section>
<h2><span class="num">2</span> The trap: the board is a snapshot, not a list of live goals.</h2>
<svg viewBox="0 0 880 244" role="img" aria-label="A board written on 21 January reported a goal that had already ended on the 17th">
 <rect x="20" y="36" width="840" height="100" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="46" y="74" text-anchor="start" font-size="16" fill="var(--text)">a board reported on 21 January…</text>
 <text x="46" y="108" text-anchor="start" font-size="16" fill="var(--danger)">…for a goal that ended on the 17th, still carrying “IsComplete: true”</text>
 <text x="440" y="172" text-anchor="middle" font-size="16" fill="var(--text)">It fires every time you dock, so a stale entry is the common case, not the edge one.</text>
 <text x="440" y="204" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So every goal is checked against the clock before it is listed.</text>
 <text x="440" y="234" text-anchor="middle" font-size="15" fill="var(--text-muted)">The deadline sits on the second line of every entry rather than buried among the figures.</text>
</svg>
<p class="body">Announcing a finished goal as something you can still fly for is a wrong answer that reads exactly like the feature working. Expired ones are hidden unless you ask — which is how you find out what a goal paid you.</p>
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

All of this comes off your own journal and needs nothing. It cannot see a goal running somewhere you
have not docked.

### Your journal already knows more than you would expect

Elite writes a `CommunityGoal` event carrying the whole board — the goal, where it is flown, the
tier it has reached, how many Commanders are on it, how much has been handed in, your own
contribution, your percentile band and whether you are in the top rank. All of that is on disk
already.

It is written off the noticeboard **at a station**, though, so it reports what is on offer where
you happen to be. Across thirteen months of one Commander's play that came to ten goals — and 952
of the 16,999 board entries in that corpus record a contribution of zero, which is the game telling
you about goals they merely docked near and never joined.

So the line an outside source buys is **everywhere you have not been**, which is wider than
"everything you have not joined". Your journal covers the second one on its own.

### The trap: the board is a snapshot, not a list of live goals

The same corpus holds a board reported on 21 January for a goal that ended on the 17th, carrying
`IsComplete: true`. Four days stale — and because the event fires every time you dock, a stale
entry is the common case rather than the edge one.

Every goal is therefore checked against the clock before it is listed, and an expired one says so
in the same breath as its name:

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

Announcing a finished goal as something you can still fly for is a wrong answer that reads exactly
like the feature working, which is why the deadline is on the second line of every entry rather
than buried in the figures.

Expired goals are hidden unless you ask for them — `include_finished` — because an expired goal
cannot be contributed to. Asking for them is how you find out what a goal paid you.

### The Community Goal search

One saved question, run again and again ([#296](https://github.com/dseelinger/d47/issues/296)).
It is the INARA commodity search a Commander flying a supply goal was typing by hand before every
run: **buy Palladium, from the goal's own system, nearest first**, within 250 light years, prices
under eight hours old, a large pad, a station within 50,000 light seconds of the star, at least
10,000 in stock, no surface stations, no carriers. Every one of those is now a knob on the galaxy
search's `find_nearest_station`, and this is the one place they are set together.

Say *"community goal search"* or *"CG search"* to run it. The phrase is matched whole and first
by the keyword router and pointed at the galaxy tool with the arguments already filled in, so it
costs no tool-surface bytes and never goes through a model to be reinterpreted.

#### Where it measures from

**The goal's system, not the ship's** ([#331](https://github.com/dseelinger/d47/issues/331)). A
supply goal is *buy near where you sell*, so the useful question is "the best supplier for this
run" — one answer that holds for the life of the goal. Measured from the ship it changed with
every jump and, once you were standing at a supplier, collapsed to "where you already are".

The origin is the `SystemName` of a **live** goal on your board: the one you are actually running —
a contribution or a sign-up is the evidence — and otherwise the most recently reported. A goal
whose `Expiry` has passed is never the origin. With no live goal there is nothing else to measure
from, so it falls back to wherever the ship is.

**The spoken answer says which it was**, because "nearest" reads the same
whichever place it was asked from:

```text
Nearest to Ega, the goal's system, for buying Palladium: Coleman Relay (Enayex), 11 ly. System is
on your clipboard.
```

and the heading above the ranking reads *"Palladium near Ega, the goal's system, nearest first"*.

To ask it from the ship on purpose — what is close enough to reach right now — say **"CG search
from here"** (or *"community goal search from here"*). That is not a setting: which of the two questions you want is a property of the sentence, not
of the installation. The clipboard and *"set a course"* keep working from wherever the ship is
either way, since plotting is the ship's business — but the **distance column is measured from
whatever the search was asked from**, the same number that ranks the list, and the heading above
the table names that system.

With more than one live goal on the board — Elite runs several at once, and the board merges rather
than replaces — the goal you are running wins, then the one whose title names the commodity you are
searching for, and only then the most recently reported. The title is a tie-break rather than a
requirement: the `CommunityGoal` event carries no commodity field and plenty of goals never name one
in their title.

#### What comes back

The spoken answer is **one sentence naming the nearest station that passes every filter**, and the
rest is left to the page: the price, the stock, the distance from the star and the quote's age are
all in the table, and the ear could not act on them. The page draws the full ranked list: station,
system, pad, distance from the star, distance, supply, price, when the quote was reported.

### The ledger

What the goal's commodity has made or lost, **net of what the cargo cost** \u2014 a Palladium bought at
48,200 and sold at 51,000 shows the 2,800 and not the 51,000. The cost is Elite's own
`AvgPricePaid` on each sale, which the game tracks across sessions and ships; when Elite writes
zero \u2014 cargo that was never bought \u2014 it falls back to the average of your own `MarketBuy` events
for the commodity, and failing that the sale is gross, which is the accurate figure for a mined load.

Three stretches; only the first is spoken automatically, and the
other two are answers to a question
([#332](https://github.com/dseelinger/d47/issues/332), [#340](https://github.com/dseelinger/d47/issues/340),
[#342](https://github.com/dseelinger/d47/issues/342)):

- **This session** \u2014 the running total rather than the sale, said automatically after every sale
  of the commodity: *"That's 2 million up this session."* You can see the sale's own figure on
  your screen at that moment; what you cannot see is where it leaves you. The
  [Community Goal sales](callouts.html#community-goal-sales) row on the Callouts page switches the
  sentence off, and nothing is said while d47 is catching up on a journal it did not watch being
  written.
- **Today** \u2014 asked for, never announced: *"how have I done today"*.
- **This week** \u2014 asked for, never announced: *"how have I done this week"*, or *"how have I
  done since the last maintenance window"* (also *"since maintenance"*, *"since the last
  maintenance"*, *"since the tick"*). **The week is the Elite week**, turning at
  [the week boundary](#week-boundary), Thursday 07:00 UTC by default \u2014 where the Powerplay cycle,
  the BGS tick and the weekly server maintenance all sit. The exact minute does not matter to a
  ledger: nothing can be sold while the servers are down, so a window opening at the hour holds
  the same sales as one opening at "live again". The answer names the boundary it used, so "this
  week" says what it means without a trip to settings:
  *"Palladium: 2.1 billion up this week, since Thursday's maintenance \u2014 7 sales, 8,400 tonnes\u2026"*
There is no fourth, goal-named stretch ([#340](https://github.com/dseelinger/d47/issues/340)):
*"how have I done on the community goal"* and *"how am I doing on the community goal"* answer
"this week", the same as asking for the week outright \u2014 that is the answer to how a run is going
while a goal is live.

The day and the week both reach across sessions because the journal files Elite keeps are read at
startup \u2014 the files that cover the last ten days \u2014 and the live journal after that. Nothing is
written under `data\`: the journals are the record, and a sale the startup read already counted is
recognised rather than counted twice when the live journal replays it.

```text
Palladium: 3 million up this week \u2014 3 sales, 300 tonnes, 15,000,000 cr in against
12,000,000 cr the cargo cost.
```

#### The week boundary {#week-boundary}

Which day, UTC, and which hour "this week" turns over on \u2014 two advanced rows under Community goal
sales on the Settings page, Thursday and 07:00 by default. Frontier has moved this boundary before, so it is a setting rather than a constant; it
is a fact about their schedule and not about d47.

### Tools

#### `get_community_goals`

```json
{"type":"object","properties":{"include_finished":{"type":"boolean","description":"Also list goals that have already expired, with what they paid out. Default false \u2014 an expired goal cannot be contributed to."},"name":{"type":"string","description":"Only goals whose title contains this. Leave out for all of them."}},"required":[],"additionalProperties":false}
```

#### `get_community_goal_earnings`

What the Community Goal commodity has made or lost, net of cost, over one of three stretches. The
nine questions above reach it through the keyword router without a model; the model can call it
for anything phrased differently.

```json
{"type":"object","properties":{"range":{"type":"string","description":"Which stretch. Default session.","enum":["session","today","week"]}},"required":[],"additionalProperties":false}
```

### Notes for anyone reading the code

The board is **merged by `CGID`, never replaced.** `CurrentGoals` looks like a complete board per
event, which would argue for replacing it — but it is the board at *one station*, and no station in
the corpus ran more than one goal at a time, so the two-stations case is untested. Replacing on an
untested assumption loses a goal you are actually running; merging keeps one that has ended, which
the expiry check already has to handle because the snapshot goes stale regardless. Only one of
those two failures is silent.

`CommunityGoalJoin`, `CommunityGoalDiscard` and `CommunityGoalReward` carry only `CGID`, `Name` and
`System` — so signing up and being paid are merged onto the board entry rather than read from it,
and they survive the next board event. Taking a fresh read's defaults would un-join a goal every
time you docked. One arriving for a goal no board has reported still counts: a Commander who joined
a goal before d47 was watching is not a Commander who has not joined it.

`TierReached` is written as the string `"Tier 3"` and only once the first success tier is met, so
"no tier reached yet" is a fact rather than a gap.
