---
title: Habits
group: Conversation
nav_order: 140
---

Directive 47 can read through the journals already on your disk and tell you what you keep doing.

Not what you did last night — what you *keep* doing, with the count behind it. Every claim comes
with how often it happened, out of how many chances, over what window, and how much of that was
recent. Without those, an observation about a person is an insult.

## It cannot be copied

This is the one thing d47 does that nothing else can. Anybody can ship the same tables and the same
personas; nobody else has your thirteen months of flying. The analysis is arithmetic over events on
your own machine — **nothing leaves it, and no journal is ever sent to a model.**

That is a privacy statement and it is also a design one. The language model is the one part of d47
that reads untrusted text — in-game messages, journal strings, search results — so it is not the
part that gets handed a list of your mistakes. The mining, the store, the callout and the readback
are all local, and the model can reach none of them.

## Reading your journals

Nothing happens until you ask. Open **Habits** in the panel and press **Read my journals**, or say:

> what have you noticed about me

A pass over 914 journals — about 700,000 events — takes under four seconds. It runs off the tick
loop, so it costs nothing while you are flying, and it never starts by itself.

Results are keyed **per Commander**, on the Frontier id in your journals rather than on a character
name. Nine names across three accounts is a normal thing for a long-running corpus to contain, and
telling one Commander about another's flying would be the exact failure this page spends its length
guarding against.

## What it looks for

| Pattern | What it counts |
|---|---|
| Flying into things on the way in | Hull damage with nobody shooting, within five minutes of arriving somewhere |
| Overshooting and going round again | A drop, a climb back to supercruise within four minutes, and a second drop at the same body |
| Submitting to interdictions | `Interdicted` events where you submitted, out of all of them |
| Dying on foot at settlements | Deaths to suit AI or settlement turrets, out of all your deaths |
| Landing on heavy worlds | Touchdowns over one g, out of landings on bodies you had scanned |

A real report reads like this:

```
I read 403 journals, 2025-09-02 to 2026-07-10.

What I noticed:
- You have put your hull into something on the way in, with nobody shooting at you — 9 of 2370
  arrivals. Counted over 403 of your journals, 2025-09-02 to 2026-07-10.
- You submit rather than run — 24 of 24 interdictions. Counted over 403 of your journals,
  2025-09-02 to 2026-07-10.

What I looked at and would not claim:
- landing on heavy worlds — you have not landed anywhere over one g, the heaviest of your 241
  landings was 0.69 g
```

## Two things Elite does not write down

Worth stating plainly, because both are things a Commander reasonably expects to be detectable.

**There is no impact or proximity event.** The warning your HUD gives you when you are about to fly
into a station is not in the journal, in any form, and never has been. What *is* there is the
consequence — hull damage with no attacker — which is the first row of the table above and the
strongest signal d47 found.

**There is no landing-gear event either.** *You always forget the gear* is the classic example of a
habit claim, and it turns out to be undetectable at any sample size. Which is rather the point of
insisting on the count.

## When it says something

Off until you switch it on, under **Callouts → Things you keep doing**. Every other callout fires
because the game said something; this one fires because of a claim d47 made about *you*, and that is
a different deal.

When it is on, a claim is said at the moment the situation it is about comes round — arriving at a
station, entering orbital cruise, being interdicted, walking up to a settlement. One claim stays
quiet for four hours after being said, and no two habit lines arrive within twenty minutes of each
other.

Two phrases work the moment one is said:

> why did you say that

> stop telling me that

The first shows the working. The second drops that claim permanently — and **a dismissal survives
re-mining**, so the same observation cannot come back next month. That is deliberate: an observation
you have already refused arriving again is the worst thing this feature could do.

## How much has to be there

Three floors, and none of them is adjustable. A Commander who could lower the bar would use it to
confirm something they already believed.

| Floor | Value | Why |
|---|---|---|
| Journals | 20 | Nothing is claimed from a fortnight of flying |
| Occurrences | 5 | Below five, a pattern is a coincidence with a count |
| Opportunities | 10 | 50 of 52 is a habit; 2 of 2 is a Tuesday |

Under the journal floor, nothing is examined at all and d47 says so:

```
There are 3 of your journals here, and I do not draw conclusions about somebody from fewer
than 20. Fly for a while and ask me again.
```

**A detector that found nothing still reports.** "Nothing to say about you" and "not enough of you
to say" are different answers, and giving you the first when the second is true is how a companion
starts being wrong about a person.

## Where it lives

`data/habits.json`, beside the executable, plain text, keyed per Commander. Edit it in any text
editor while d47 is running and the change is live — the file is compared by content, so a hand edit
is never missed.

```json
{
  "commanders": [
    {
      "frontierId": "F12484034",
      "minedAt": "2026-08-18T21:14:03+00:00",
      "journals": 403,
      "from": "2025-09-02T18:22:41+00:00",
      "to": "2026-07-10T02:55:19+00:00",
      "claims": [
        {
          "key": "interdiction-submit",
          "subject": "submitting to interdictions",
          "observation": "You submit rather than run",
          "denominator": "interdictions",
          "occasion": "beingInterdicted",
          "occurrences": 24,
          "opportunities": 24,
          "recent": 0,
          "from": "2025-09-02T18:22:41+00:00",
          "to": "2026-07-10T02:55:19+00:00",
          "journals": 403
        }
      ],
      "quiet": [
        {
          "key": "high-gravity",
          "subject": "landing on heavy worlds",
          "why": "you have not landed anywhere over one g — the heaviest of your 241 landings was 0.69 g"
        }
      ],
      "dismissed": ["overshoot"]
    }
  ]
}
```

`dismissed` is the only part of that file a person wrote, which is why it is the only part that
survives everything else. Mining replaces the claims; **Forget what you noticed** in Privacy throws
them away entirely; neither touches the dismissals.

## Tools

All three are protected — reachable from the panel and from the phrases below, never from the
language model.

### `get_habits`

Everything mined, with its counts, plus every pattern that was looked at and not claimed.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `explain_habit`

The working behind the last thing said.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `dismiss_habit`

Drops one claim for good. With no key, the one just said — which is what makes *stop telling me
that* work as a phrase at all.

```json
{"type":"object","properties":{"key":{"type":"string","description":"Which one, as read back by get_habits. Omit for the last one spoken."}},"required":[],"additionalProperties":false}
```
