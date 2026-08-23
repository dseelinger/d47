---
title: Focus the game
group: Interface
nav_order: 128
---

Brings Elite Dangerous back to the front, so flight commands work again.

## Ask for it

> "set focus to game"
> "focus the game"
> "switch to Elite"
> "back to the game"

```text
Elite is in front.
```

## Why this exists

Directive 47 will not press a key unless Elite is the window in front. That check is the one
thing standing between a voice command and typing into your browser, and it is not negotiable.

The awkward consequence is that alt-tabbing away turns every flight command off, and the only
way back was the mouse — the one thing you were trying not to reach for. This is the single
action that could not be delegated to the thing already refusing to act.

## Windows may refuse, and it will say so

**Windows does not let a background application take the foreground.** A program that does not
already have it can only ask, and what usually happens is the taskbar button flashes instead.
Directive 47 has no way around this that does not involve faking keyboard input at the operating
system, which is exactly the thing it promises never to do outside Elite.

So the honest version: this works when you ask from Directive 47's own window, and Windows will
often refuse it when you ask from somewhere else. When that happens you are told, rather than
left with silence that reads like the microphone having failed:

```text
Windows would not let me bring Elite forward from the background. Its taskbar button should be
flashing; click that, or alt-tab.
```

It also says when there was nothing to do, for the same reason:

```text
Elite is already in front.
```

## The model cannot do this

Only the spoken phrases above reach it, through the keyword router. **It is not a tool the model
can call**, and that is deliberate: your journal, in-game messages, web search results and INARA
are all untrusted text, and anything the model can call, a hostile in-game message can try to
invoke. A message that could yank your focus while you were typing is a nuisance at best.

The phrases are all more than one word on purpose. "Elite" on its own would have been convenient
and is not safe: the router matches phrases anywhere in what you said and runs before the model,
so a bare "Elite" would swallow *"what is my Elite rank in combat"* and answer it by moving a
window. Elite is the top rank in every career the game has.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `focus_the_game`

Brings Elite Dangerous to the foreground. Takes no arguments, and is **protected** — reachable
from the keyword router and never from the model.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Its own capability rather than a tool on an existing one, for the reason
[re-anchor](reanchor.md) is: the keyword router reaches a capability's *first* argument-free
tool, so hanging this on a capability that already has one would make it unreachable without a
model — which is the configuration it exists for.

`SingleInstance` calls `SetForegroundWindow` too and discards the result. That is not evidence
this works: a second copy of d47 being launched is one of the cases Windows exempts, and this is
not. The foreground is re-read after the call rather than trusting the return value, because it
can report success for a call that only flashed.

</details>
