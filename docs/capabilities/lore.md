---
title: Lore
group: Knowledge
nav_order: 114
---

Some systems mean more than their astrography. Directive 47 ships knowing about twenty of them, says
so when you arrive, and keeps whatever you tell it about the rest.

## Ask for it

> "what is notable about this system"
> "anything notable here"
> "is there anything out here"

The first two need no AI configured at all — they route straight through, with nothing guessed.

```text
Sol. Earth is here. Everywhere else on this list is somewhere humanity got to afterwards.
```

## Arriving somewhere

Jump into a system with something attached to it and Directive 47 says so, unprompted:

```text
John Jameson's Cobra Mark Three came down on the first planet's moon B and is still there — the
wreck every Commander eventually visits.
```

**Once per system per day**, and that rule is the whole difference between a companion and a tour
guide who has forgotten meeting you. It was measured rather than picked: across 913 real journals,
**30.1% of all 7,966 jumps re-enter a system visited within the last day**. Without the rule, nearly
a third of arrivals would be something you had already heard. Stretching it to a week would suppress
only 4.2 points more, because 88% of repeat visits happen inside the first day.

The clock survives a restart, so logging off in Shinrarta Dezhra and coming back an hour later is
quiet.

A carrier jump counts as arriving. You were asleep in the back, but you are still somewhere new.

## Then looking it up {#remarks}

Set **Remark on arrival** to *Remark, and look it up* and the bare fact is followed by a web search,
spoken when it comes back:

```text
From a search: Commanders have used HIP 12099 as an encoded-materials run since the wreck was found
in 3303, because the crash site carries nine data points in one place.
```

Three states rather than two switches, because a lookup with the remark switched off is detail about
something that was never announced:

| Setting | What happens on arrival |
|---|---|
| Never | Nothing. Asking still works. |
| Remark only | The bare fact, and nothing further. |
| Remark, and look it up | The fact, then whatever the search found. |

Some things worth knowing about the lookup:

- **It needs [web search](conversation.md) switched on**, and an endpoint that offers one. If it
  cannot search, the first sentence says so rather than leaving you waiting for a second that never
  comes.
- **It costs about a penny a time.** Searches are billed separately from tokens, and the running
  total counts them.
- **A result that arrives late is dropped.** If you have jumped again by the time it lands, you were
  somewhere else when it became relevant, and a sentence about a system you left is worse than
  silence.
- **What it found is always spoken as a search result**, never in the flat voice the shipped table
  uses. Nothing a search returns is ever written into a table.

## Your own notes {#notes}

**Settings → Lore → Your own notes** opens what you have told Directive 47, and is where you add
one. It is a window rather than a text box on a row because writing a note is the one act that makes
an entry *yours* — and that act is not reachable from the tool surface at all.

When you add one, Directive 47 searches for the system first and records what happened. Three tiers,
read back in three different sentences:

```text
Earth is here.                                                   ← the shipped table
You added this one, and the search agreed at the time: …         ← corroborated
You told me: …                                                   ← your word
I wrote this one down myself, and nothing has checked it: …      ← D47's own tool call
```

**A tier is never promoted.** Surviving a lookup is a label, not a verdict: an obscure but real site
finds nothing, and a search can appear to agree with something a model half-invented. If a later
search would have corroborated a note, the note still says you told me — because promotion is the
path by which an invention quietly becomes a fact Directive 47 states flatly.

Notes live in `data/lore.json` beside the executable, one file for the installation rather than one
per Commander: what is true about a system is true whichever character you are flying. Each entry
records which Commander was aboard anyway, and how it arrived. Edit the file by hand if you like —
changes are noticed by comparing the contents, so an edit is never missed.

## What Directive 47 can do about it

### `get_system_lore`

Everything known about the system you are in, each answer stating where it came from.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `remember_about_system`

Notes something about the system you are in. **Stored as unverified and read back that way** — this
is the model writing, not you.

```json
{"type":"object","properties":{"note":{"type":"string","description":"What is worth remembering, in one or two sentences. Facts about the place, not about what the Commander is doing today."}},"required":["note"],"additionalProperties":false}
```

This one is deliberately *not* locked away from the model, which is a departure from how the
[checklist](checklists.md) and the [callouts](callouts.md) are handled. Adding a note presses no key
and switches no warning off, so there is nothing here for a hostile in-game message to gain — except
persistence, and unprompted speech later. That is answered with a label rather than a lock: an entry
the model wrote says so, out loud, every time it is read back. Nothing promotes it, and you can
delete it from the notes window.

Writing is limited to the system you are actually in. That is arithmetic rather than caution — a note
is keyed on the system's address, addresses come out of your journal, and Directive 47 reaches no
network to look one up.

## Where the twenty came from

The shipped table is a compilation, and compilations belong to whoever made them — so this one is the
maintainer's rather than a community list copied across. The facts in it are Frontier's, in the same
way that which system an engineer sits in is; see [NOTICE](https://github.com/dseelinger/d47/blob/main/NOTICE).

`tools/gen-lore.py` builds it. Each system's name is resolved to its address twice, through
spansh.co.uk and edsm.net, and a disagreement or a miss stops the run — which is how two bad rows were
caught rather than shipped: one system that had been renamed, and one that never existed at all.
Eleven of the twenty were also checked against real journals, where Frontier themselves wrote the name
and the address side by side.

Nothing in the table is a distance that gets re-surveyed or a status that can change. Anything that
moves belongs to the lookup, where it stays a sentence.
