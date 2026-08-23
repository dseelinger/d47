---
title: Community goals
group: Knowledge
nav_order: 118
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">What community goals are running, what tier they have reached, and how you are doing in them.</p>
<section>
<h2><span class="num">1</span> Your journal already knows most of this.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="The journal carries the whole community goal board, and an Inara key only adds goals running where you have not been">
 <rect x="20" y="40" width="400" height="118" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="78" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">YOUR JOURNAL</text>
 <text x="220" y="110" text-anchor="middle" font-size="14" fill="var(--text-muted)">the whole board: tier, contributors,</text>
 <text x="220" y="134" text-anchor="middle" font-size="14" fill="var(--text-muted)">handed in, and your own share</text>
 <rect x="460" y="40" width="400" height="118" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">WHAT A KEY BUYS</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">goals running somewhere</text>
 <text x="660" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">you have not been</text>
 <text x="440" y="198" text-anchor="middle" font-size="16" fill="var(--text)">The event is written off the noticeboard at a station, so it reports where you are.</text>
 <text x="440" y="228" text-anchor="middle" font-size="15" fill="var(--text-muted)">Which is narrower than “everything you have not joined” — your journal covers that on its own.</text>
</svg>
<p class="body">The key <em>is</em> the switch: with none stored, nothing is requested and nothing leaves this machine. What goes to Inara is your key and nothing else — not your Commander name, not your Frontier id, not where you are.</p>
</section>
<section>
<h2><span class="num">2</span> The trap: the board is a snapshot, not a list of live goals.</h2>
<svg viewBox="0 0 880 244" role="img" aria-label="A board written on 21 January reported a goal that had already ended on the 17th">
 <rect x="20" y="36" width="840" height="100" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="46" y="74" text-anchor="start" font-size="16" fill="var(--text)">a board reported on 21 January…</text>
 <text x="46" y="108" text-anchor="start" font-size="16" fill="var(--danger)">…for a goal that ended on the 17th, still carrying “IsComplete: true”</text>
 <text x="440" y="172" text-anchor="middle" font-size="16" fill="var(--text)">It fires every time you dock, so a stale entry is the common case, not the edge one.</text>
 <text x="440" y="204" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So every goal is checked against the clock before it is listed.</text>
 <text x="440" y="234" text-anchor="middle" font-size="15" fill="var(--text-muted)">The deadline sits on the second line of every entry rather than buried among the figures.</text>
</svg>
<p class="body">Announcing a finished goal as something you can still fly for is a wrong answer that reads exactly like the feature working. Expired ones are hidden unless you ask — which is how you find out what a goal paid you.</p>
</section>
<section>
<h2><span class="num">3</span> Two sources, never blended.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="Journal goals and Inara goals are listed separately and merged only on an exact name match">
 <rect x="20" y="40" width="390" height="104" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="215" y="80" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">FROM YOUR JOURNAL</text>
 <text x="215" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">and what you handed in</text>
 <line x1="440" y1="36" x2="440" y2="148" stroke="var(--border)" stroke-width="2"/>
 <rect x="470" y="40" width="390" height="104" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="665" y="80" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">FROM INARA</text>
 <text x="665" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">and never your standing</text>
 <text x="440" y="192" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">A duplicate is visible. A wrong merge is silent.</text>
 <text x="440" y="222" text-anchor="middle" font-size="15" fill="var(--text-muted)">A listing entry carries no id, so the only shared field is the name — the field two sources spell differently.</text>
</svg>
<p class="body">Inara knows what the world handed in; your journal knows what <em>you</em> handed in. And if the listing fails you do not lose the journal half — the goals you have seen are reported as usual, with the reason for the missing half added at the end.</p>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="journal.html"><span class="ct">Journal →</span><span class="cd">The file all of this is read out of, and what else is in it.</span></a>
<a class="card" href="privacy.html"><span class="ct">Privacy →</span><span class="cd">What the Inara request actually contains, computed rather than claimed.</span></a>
<a class="card" href="routes.html"><span class="ct">Routes →</span><span class="cd">Getting to the station a goal is flown from.</span></a>
</div>
</div>
</div></div>

## The details

What community goals are running, what tier they have reached, and how you are doing in them.

> "what community goals are running"
> "how am I doing in the community goal"
> "what tier is the community goal at"

Most of this comes off your own journal and needs nothing. The one thing it cannot do without a key
is see a goal running somewhere you have not been.

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

### Inara API key

The one setting, and there is no separate on/off switch: **the key is the switch**. With no key
stored, nothing is requested and nothing leaves this machine, and the answer says plainly that it
is only what your journal has seen. Clearing the key is how you turn it off.

Get one from your Inara profile, under API keys. It is stored encrypted for this Windows account
and is write-only — d47 will never show it back to you.

What goes to [inara.cz](https://inara.cz) is your key and nothing else. Not your Commander name,
not your Frontier ID, not where you are, and nothing from your journal — the request is a read of a
public board, so it says nothing about anybody. The Privacy section computes that same statement
rather than repeating it by hand, so it cannot go stale.

d47 does not use a shared application key, though Inara issues them for read-only requests like
this one. d47 ships as a public binary with its source beside it, so a key baked into it would be a
published key, and a published key gets abused until it is revoked for everybody.

### Two sources, never blended

Goals from Inara are listed separately, under a line saying so:

```text
1 more reported by Inara, which your journal has not seen. Nothing here says anything about your
own contribution:

Rescue Operation in the Pleiades
  The Oracle, Pleiades Sector IR-W d1-55 — 2 days left
  Deliver Occupied Escape Pods, Damaged Escape Pods, Black Boxes and Personal Effects
  Tier 6, 2,038 contributors, 40,001 delivered, met.
  Inara last updated this 9 hours ago.
```

A listing entry carries **no CGID**, so the only field the two sources could be matched on is the
goal's name — which is exactly the field two sources spell differently. So they are merged only
when the names match exactly, and anything else is allowed to appear twice. A duplicate is visible;
a wrong merge is silent.

Your standing never comes from Inara. It knows what the world handed in; your journal knows what
*you* handed in.

### When Inara cannot answer

The listing failing does not lose you the journal half. The goals you have seen are reported as
usual and the reason for the missing half is added at the end:

```text
Inara rejected the request. Check the API key.
```

An HTTP 200 from that site is not a success — a bad key, a malformed request and "nothing found"
all arrive as 200 with a status code inside the body. Reading the transport code as the answer
would report a rejected key as an empty board, which is the one wrong answer here that looks
exactly like a right one.

### Tools

#### `get_community_goals`

```json
{"type":"object","properties":{"include_finished":{"type":"boolean","description":"Also list goals that have already expired, with what they paid out. Default false \u2014 an expired goal cannot be contributed to."},"name":{"type":"string","description":"Only goals whose title contains this. Leave out for all of them."}},"required":[],"additionalProperties":false}
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
"no tier reached yet" is a fact rather than a gap. Inara's `tierMax` is **0 when it does not know** —
the journals carry no maximum tier, so an entry built from a journal upload has zero there, and
reading it literally announces a goal whose top tier is zero.

The goal's GalNet copy — several hundred words per goal, in `goalDescriptionText` — is dropped at
the seam rather than trimmed. It is the largest piece of untrusted third-party text d47 could put
in front of a model, and nobody asked for it. The one-line objective comes through, capped.

The wire shape follows [Inara's API documentation](https://inara.cz/elite/inara-api-docs/) as read
on 2026-08-15: one endpoint, an envelope of a `header` and an `events` array, and per-event status
codes underneath the HTTP one. `getCommunityGoalsRecent` takes no properties.

Findings behind all of this are in [docs/spikes/community-goals.md](../spikes/community-goals.md).
