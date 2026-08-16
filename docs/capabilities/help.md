---
title: Help
group: Foundation
nav_order: 100
---

What Directive 47 can actually do, answered from its own list of capabilities rather than from
the model's memory.

## Ask for it

> "what can you do"
> "what are your capabilities"
> "tell me about the voice capabilities"

This one works with no model configured and no network — it never needs to ask anything outside
the machine what is on the machine.

## What you get

An overview first, because reading forty capabilities aloud is not help:

```text
I have 9 capabilities, in these groups:
  Foundation — Journal, Diagnostics, Privacy, Settings
  Conversation — Conversation
  Voice — Speech, Callouts
  Interface — Interface
Ask about a group by name for the detail.
```

Then, by group, the detail and the phrases that actually work — your words rather than internal
names, because a list you cannot say out loud is a list you cannot use:

```text
Speech: Speak replies aloud, mark each loop state with its own cue, and stop on command.
  Try: "stop"; "be quiet"; "shut up"
Callouts: Speak up about danger, fuel, route progress and arrivals without waiting to be asked.
  Try: "what are you watching for"; "stop calling things out"; "start calling things out"
```

Ask for a group that does not exist and it names the real ones instead of refusing. If you asked
for the wrong group you want the right one, and this is the moment Directive 47 knows both:

```text
I have no group called "Navigation". I have: Foundation, Conversation, Voice, Interface.
```

## Why it will not make things up

**The model is never asked what Directive 47 can do.** Ask a model to describe its own abilities
and it produces a fluent, confident, partly invented list — and you have no way to tell which
half is which until you ask for something that turns out not to exist.

So the answer is built from the actual list of registered capabilities. It *cannot* name one that
does not exist, because it is assembled from the ones that do.

## The order it reads them in

Within a group, whichever you have used most comes first, so the things you actually reach for
rise to the top of the list. Groups themselves stay in a fixed order — a spoken list whose
headings move between askings is harder to follow than one that does not.

Reaching for something counts even when it fails, since a capability you keep trying is worth
hearing about early on the days it is not working.

The count covers the current session only. It starts fresh each time Directive 47 does, rather
than claiming a usage history it does not have.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `get_capabilities`

Projects the registry: groups, the capabilities in them, and their declared example phrasings.
The optional group name expands one of them.

```json
{"type":"object","properties":{"group":{"type":"string","description":"Optional group name to expand, such as Voice. Omit for the overview."}},"required":[],"additionalProperties":false}
```

The keyword router only routes to a tool with no *required* parameters, because it deliberately
does not extract argument values from free text — a router that guesses at arguments is one that
calls the right tool with the wrong ones. The router originally refused any tool with parameters
at all, which made "what can you do" match nothing: the one capability whose entire purpose is
being answerable without the model was unreachable without it, purely for offering a refinement
it does not need. The rule is now "no required parameters", and the router invokes with empty
arguments.

</details>
