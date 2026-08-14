---
title: Language model
---

Which model answers you, whether it can be reached right now, and what the session has cost so
far. This is also where you decide whether Directive 47 talks to anything outside this machine
at all.

## Ask for it

> "which model are you using"
> "what has this session cost"
> "personality off"

## What it tells you

```text
Provider: Anthropic
Model: claude-sonnet-5
Availability: Available
Personality: on
Session so far: 3 turn(s), $0.0412
```

With no key stored it says so, rather than going quiet and leaving you to work out why nothing
answers:

```text
Provider: Anthropic
Model: claude-sonnet-5
Availability: NotConfigured — No Anthropic API key is stored. Add one in Settings.
Personality: on
Session so far: 0 turn(s), $0.0000
```

## Stopping a turn

"Stop" and "cancel" are different things, and the difference is on your bill.

| You say | What stops |
|---|---|
| "stop" | The speaking. The model keeps working, and keeps costing. |
| "cancel", "never mind" | The speaking, the model, and the spend. |

Both work while a turn is running rather than waiting for it to finish — a turn in flight is the
only thing either has to act on.

One honest limit: whatever the model had already produced before you cancelled has already been
billed by the provider, and Directive 47 cannot get the figures for a turn it tore down. So a
cancelled turn adds nothing to the running total, which slightly under-reports the session.
Cancelling saves the work that had not happened yet, not the work that had.

## Settings

The rows here follow the provider you pick. Choose **None** and the rest disappear rather than
sitting there greyed out.

### Provider {#provider}

| Choice | What it is |
|---|---|
| `none` | No model at all. Directive 47 still answers what it recognises on its own, and says so when it cannot. |
| `anthropic` | Claude models. |

**The model cannot change this.** Picking a provider picks where your turns go, so it is the
panel's to change and not the model's.

Changing it clears the model choice with it — model names belong to their provider, and carrying
one across is how you end up with a selection that fails at the first question.

### Endpoint {#endpoint}

Only appears for providers where pointing somewhere else means something — a gateway, a proxy, a
local server speaking the same protocol. Anthropic has one address and no reason to accept
another, so with Anthropic selected there is no row here to worry about.

Where it does appear, changing it empties the model list: Directive 47 knows which models live at
the provider's own address and has no idea what lives at yours. You can still type one.

### Model {#model}

Which model answers. Leave it empty for the provider's default, shown greyed out so "I have not
chosen" stays distinguishable from "I chose that one".

Anthropic's default is the highest Sonnet — currently **Claude Sonnet 5**. A companion answering
questions about a game in flight is not the work the Opus tiers are priced for, and the Opus
models are the next entries in the list if you want one.

The offered list is every model Directive 47 can price, so anything picked from it keeps the
running cost honest. Type one by hand and it is accepted, but counted as unknown rather than as
free.

### API key {#api-key}

Encrypted for your Windows account and kept in `data/secrets.json` beside the executable.

**It is only ever written, never read back.** Directive 47 can tell you whether a key is stored
and can replace it; nothing — not the panel, not the model, not the logs — can show you the key
again. If you lose it, paste a new one.

The row says which state it is in, and the box changes with it: `No key` and "Paste a key to
store it", or `Key stored` and "Paste a new key to replace it".

If you already keep `ANTHROPIC_API_KEY` in your environment, that still works and is used when
nothing is stored here.

### Personality {#personality}

Off gives you plain answers. It does not loosen anything: the rules that stop Directive 47
inventing capabilities it does not have are separate, and nothing on this panel can reach them.

Spoken shortcuts, recognised without the model:

> "personality off" / "turn personality off" / "turn your personality off"
> "personality on" / "turn personality on" / "turn your personality on"

The whole sentence has to be the phrase rather than merely contain it, so asking "what does
personality off actually change" gets you an answer instead of switching it off.

### About Me {#about-me}

Standing context about you — how you fly, what you are working towards, what to call you — sent
with every turn and kept between sessions.

It goes to the provider along with everything else on a turn. See [Privacy](privacy.md).

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `cancel_turn`

Abandons the turn currently running: stops speaking, tears down the provider stream, and stops
spending. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Marked **interrupting**, so it is answered while a turn is in flight rather than queued behind
it. Bare "cancel" is kept out of the general command vocabulary and only consulted when there is
something to cancel — too common a verb to claim outright.

### `get_model_status`

Reports the selected provider and model, whether it is reachable, and the session's running
spend. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

The endpoint is reported only when the Commander has chosen one; a line stating where Anthropic
lives tells them something they knew.

About Me sits inside the cached prompt prefix, so editing it costs one cold prefix on the next
turn and nothing after that. Secrets are refused for the model caller unconditionally, whether or
not the row is also marked protected. Only the *source* of a key is ever logged:

```text
[Information] Anthropic configured from the secret store
```

</details>
