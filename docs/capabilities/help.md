---
title: Help
---

**Group:** Foundation
**Capability id:** `help`

What D47 can actually do, answered from its own capability registry.

**The model is never asked what D47 can do.** That is the whole point of this capability, and it
is not a stylistic preference. A model asked to describe its own abilities produces a fluent,
confident, partly invented list, and the Commander has no way to tell which half is which — they
find out when they ask for something that does not exist. The registry already holds every
capability's identity, summary and example phrasings (architecture.md D5), so the honest answer
is a projection of it: one that *cannot* name a capability that does not exist, because it is
built from the ones that do.

## Try it

> "what can you do"
> "what are your capabilities"
> "tell me about the voice capabilities"

Answered by the model-free keyword router, so it works with no provider configured and no
network.

## Tool

### `get_capabilities`

```json
{"type":"object","properties":{"group":{"type":"string","description":"Optional group name to expand, such as Voice. Omit for the overview."}},"required":[],"additionalProperties":false}
```

The overview names groups and the capabilities in them, then offers to go deeper. Reading forty
capabilities aloud is not help:

```text
I have 9 capabilities, in these groups:
  Foundation — Journal, Diagnostics, Privacy, Settings
  Conversation — Conversation
  Voice — Speech, Callouts
  Interface — Interface
Ask about a group by name for the detail.
```

With a group, the detail and the example phrasings — the Commander's own words rather than tool
names, because that is what makes a list actionable by voice:

```text
Speech: Speak replies aloud, mark each loop state with its own cue, and stop on command.
  Try: "stop"; "be quiet"; "shut up"
Callouts: Speak up about danger, fuel, route progress and arrivals without waiting to be asked.
  Try: "what are you watching for"; "stop calling things out"; "start calling things out"
```

Asking for a group that does not exist names the real ones rather than saying no. A Commander who
asked for the wrong group wants the right one, and this is the moment D47 knows both:

```text
I have no group called "Navigation". I have: Foundation, Conversation, Voice, Interface.
```

## Ranked by real usage

Within each group, capabilities are ordered by how often they have actually been invoked, with
registration order as the tiebreak — so an unused set comes out in the order the app declared it
rather than in dictionary order. Group order stays fixed: a spoken list whose headings move
between askings is harder to follow than one that does not.

Counting happens on the attempt rather than on success, since a capability the Commander keeps
reaching for is worth ranking highly even on the days it fails.

**The count is session-scoped and is not persisted.** Persisting it means a store, a schema and a
migration for a ranking; the phase that needs help to survive a restart can add those. What it
must not do in the meantime is invent a usage history that never happened.

## Why this is reachable by voice at all

The keyword router only routes to a tool with no *required* parameters, because it deliberately
does not extract argument values from free text — a router that guesses at arguments is a router
that calls the right tool with the wrong ones.

`get_capabilities` takes one optional parameter, and the router originally refused any tool with
parameters at all. That made "what can you do" match nothing: the one capability whose entire
purpose is being answerable without the model was unreachable without it, purely for offering a
refinement it does not need. The rule is now "no required parameters", and the router invokes
with empty arguments.
