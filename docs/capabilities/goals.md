---
title: Goals
group: Conversation
nav_order: 142
---

Your checklist holds what you are doing this week. Goals hold what you are doing this year.

Elite in each career. Every engineer unlocked. The ship collection. The exploration milestones that
take months. Directive 47 calls each of these an **arc**: a named ambition with a definition of
done, a progress figure nobody typed, and an age.

## Progress is worked out, never typed

An arc cannot be ticked by hand and does not offer to be. Your rank, your unlocked engineers and
your owned hulls are read off your journal, so the figure is a fact rather than an opinion — the
same rule the checklist already draws about derived lines, for the same reason.

Where the journal cannot say, d47 says so rather than guessing:

- **A goal you invented** is yours to call done, like a checklist line you wrote yourself.
- **An arc d47 cannot currently evaluate reports as of when it last could.** If Elite has not said
  anything about your rank this session, the figure from your journals stands with its date on it.
  It never resets to nothing on the absence of evidence.

## The nine that ship

| Arc | Done when | Where the figure comes from |
|---|---|---|
| Elite in Combat | Combat rank 8 | Live journal state |
| Elite in Trade | Trade rank 8 | Live journal state |
| Elite in Exploration | Explore rank 8 | Live journal state |
| Elite as a Mercenary | Soldier rank 8 | Live journal state |
| Elite in Exobiology | Exobiologist rank 8 | Live journal state |
| Every engineer unlocked | Every engineer in the directory | Live journal state |
| The ship collection | One of every hull, owned at once | Your fleet, plus what you are flying |
| Systems visited | The next milestone, up to fifty thousand | Your journals |
| Distance flown | The next milestone, up to a million light years | Your journals |

**Ranks are counted, not named.** Elite writes a number for your rank and never a word, so d47 says
*rank 5 of 8, 12% into it* and names only Elite. Shipping the rank ladders would mean hand-writing a
table of Frontier's own words, which is exactly what this repository does not do with game data.

**There is no CQC arc**, because almost nobody plays it and an arc permanently at nothing is a line
of the page spent telling you about a thing you are not doing. If any of the others is not yours
either, set it aside — it goes off the page and stays off until you ask for it back.

## Ages come from your journals

Nothing happens until you ask. Open **Goals** in the panel and press **Read my journals**. One pass
over the journals already on your disk gives every arc its start date and counts the two milestone
arcs. It is arithmetic over events on your own machine — **nothing leaves it, and no journal is ever
sent to a model.**

A read looks like this:

```
Elite in Combat: rank 1 of 8, 39% into it. Running 11 months.
Elite in Trade: rank 7 of 8, 40% into it. Running 13 months.
Elite in Exploration: rank 5 of 8, 12% into it. Running 13 months.
Elite as a Mercenary: rank 0 of 8.
Elite in Exobiology: rank 0 of 8.
Every engineer unlocked: 19 of 53 unlocked, 2 invited. Running 12 months.
The ship collection: 11 of 45 hulls owned. Running 13 months.
Systems visited: 4,182 of 5,000 systems, next milestone. That is as of 17 Aug 2026, from your
journals. Running 13 months.
Distance flown: 214,908 of 500,000 ly, next milestone. That is as of 17 Aug 2026, from your
journals. Running 13 months.
Ages and milestones come from 914 journals on this disk, the oldest from 4 Jul 2025.
```

## The checklist points at the arc

The join is what makes both worth having: given a goal that takes months, **what is the concrete
thing to do about it today?**

Ask about one arc and d47 answers with the next step, and offers to put it on your list. Accepting
is your act — an arc proposes through the same pending-proposal path everything else does, and you
accept or decline it on the Checklist page.

A line that came from an arc says so, so finishing it visibly moves something bigger than itself.

What each arc offers:

- **Every engineer unlocked** hands the question to the unlock solver, which already answers it
  properly — ranked by fastest unlock and distance together, and the chain it promotes carries an
  access step beside each modification.
- **The ship collection** names the cheapest hull you do not own, off the shipyard table.
- **The milestone arcs** name the gap to the next rung.
- **The career arcs propose nothing, and say why.** Rank is earned by doing the career; there is no
  route to it anyone can plot. Where d47 has a tool that helps — `plot_trade_route`,
  `plot_exploration_route`, `plot_exobiology_route` — it names that instead of inventing a plan.

## Talking to it

> how are my goals going

> what are my goals

### `get_goals`

Reads every arc back: how far along each is, how long it has been running, and where the figure came
from. The one tool here the model can call on its own, because *how is the Elite exploration push
going* is a thing you say mid-flight and an arc d47 cannot see is an arc it cannot be asked about.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `get_goal_step`

Says what to do about one arc today, and offers it as a checklist line. Protected — it writes to the
pending proposals, and writing is yours.

```json
{"type":"object","properties":{"goal":{"type":"string","description":"Which goal, by name or key, as read back by get_goals."}},"required":["goal"],"additionalProperties":false}
```

### `set_goal_aside`

Takes an arc off the page, or brings it back. Protected, for the same reason. A set-aside arc stays
set aside through a re-read of your journals — it is a decision you made, not a figure d47
recomputes.

```json
{"type":"object","properties":{"aside":{"type":"boolean","description":"True to set it aside, false to bring it back. Defaults to true."},"goal":{"type":"string","description":"Which goal, by name or key."}},"required":["goal"],"additionalProperties":false}
```

## Where it is kept
{: #backfill }

`data/goals.json`, beside the executable, keyed per Commander on the Frontier id in your journals —
so two characters on one machine do not report each other's progress.

Three things live in that file and only one of them is d47's. The mined marks are a recomputation
and are replaced wholesale by the next read. **The goals you wrote and the arcs you set aside are
yours**, are stored separately, and a re-read never touches either. The file is plain JSON and meant
to be readable; a line d47 cannot parse is reported rather than silently dropped.
