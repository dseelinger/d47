---
title: Community goals
group: Knowledge
nav_order: 113
---

What community goals are running, what tier they have reached, and how you are doing in them.

> "what community goals are running"
> "how am I doing in the community goal"
> "what tier is the community goal at"

Most of this comes off your own journal and needs nothing. The one thing it cannot do without a key
is see a goal running somewhere you have not been.

## Your journal already knows more than you would expect

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

## The trap: the board is a snapshot, not a list of live goals

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

## Inara API key

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

## Two sources, never blended

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

## When Inara cannot answer

The listing failing does not lose you the journal half. The goals you have seen are reported as
usual and the reason for the missing half is added at the end:

```text
Inara rejected the request. Check the API key.
```

An HTTP 200 from that site is not a success — a bad key, a malformed request and "nothing found"
all arrive as 200 with a status code inside the body. Reading the transport code as the answer
would report a rejected key as an empty board, which is the one wrong answer here that looks
exactly like a right one.

## Tools

### `get_community_goals`

```json
{"type":"object","properties":{"include_finished":{"type":"boolean","description":"Also list goals that have already expired, with what they paid out. Default false \u2014 an expired goal cannot be contributed to."},"name":{"type":"string","description":"Only goals whose title contains this. Leave out for all of them."}},"required":[],"additionalProperties":false}
```

## Notes for anyone reading the code

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
