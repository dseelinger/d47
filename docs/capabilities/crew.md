---
title: Crew
group: Ship
nav_order: 103
---

The pilots you have hired, and how to talk to them.

## Ask for it

> "who is aboard"
> "who is on duty"
> "crew roster"

```text
2 pilot(s) on the books:
  Vance Ilo (Expert) — on duty in the fighter bay, assigned aboard Long Way Home
  Ilse Bruhn (Competent) — off duty

Address one of them by name to talk to them directly, for example "Vance Ilo, status".
```

## Talking to one of them

Open with their name and the turn goes to them instead of your ship's AI.

> "Vance, what's the fighter looking like?"

The name has to be at the **front**. "What does Vance think of the fighter" is a question for
your ship's AI *about* Vance, and that is a real difference rather than a parsing quirk.

A bare name works too — it is how you get somebody's attention.

Matching is done without the model, against the names in your journal. That means it costs
nothing, it cannot pick the wrong person, and it does not need a model to know who you are
talking to — only to have them answer.

They speak in their own voice, kept for the session like any other. They are people you hired at
a station, not another Guardian core: they are not a million years old, they have no tools, no
database and no way to look anything up, and asked something they cannot see from where they sit
they will say so.

## What Directive 47 actually knows about your crew

Only what Elite writes down, which is less than you might expect:

| Known | From |
|---|---|
| Name | `CrewHire` |
| Combat rating | `CrewHire`, updated by `NpcCrewRank` |
| On duty or on shore leave | `CrewAssign` |
| Off the books | `CrewFire` |

**There is no engineer, no gunner and no navigator.** Elite's hired crew are fighter pilots and
nothing else, so that is the whole roster. Inventing posts to fill it out would be exactly the
confident wrong answer the anti-invention guardrails exist to prevent.

### "Per-ship rosters", honestly

Elite's `CrewAssign` names no ship. Your hired crew are a pool that belongs to *you*, and
whichever one is on duty flies in whatever hull you happen to be sitting in.

So the posting shown above is **derived, not reported**: where Directive 47 saw you assign
somebody, it remembers which ship you were in at the time. A ship it never saw an assignment
aboard reports nobody rather than everybody, and crew hired before Directive 47 was watching have
no posting at all until you next reassign them.

If a roster has never been seen, it says that rather than saying you have no crew — those are
different answers, and only one of them is true.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `describe_crew`

Reports the hired pilots, their combat ranks, who is in the fighter bay and which hull each was
assigned aboard. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Addressing is resolved before the turn runs, in `CrewAddressing.Match`, against the closed set of
names the journal supplied — the same shape the keyword router uses. The crew brief replaces the
persona block for that turn only and is restored in a scope's `Dispose`, so a crew turn cannot
leak the wrong persona into the next one.

Crew share the active persona's transcript rather than owning their own. That is the opposite of
the rule for the Guardian cores, and deliberately: the cores cannot know about each other, but
the crew and the ship's AI are aboard the same ship and plainly do.

</details>
