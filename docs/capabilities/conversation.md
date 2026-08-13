---
title: Language model
---

**Group:** Conversation
**Capability id:** `conversation`

Which model answers, where it lives, and what the session has cost. This capability owns the
settings that decide whether D47 talks to anything outside this machine at all.

## Try it

> "which model are you using"
> "what has this session cost"
> "personality off"

## Tools

### `cancel_turn`

Abandons the turn currently running: stops speaking, tears down the provider stream, and stops
spending. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**Cancelling is not the same as silencing, and the difference is billed.** `stop_speaking` stops
the mouth — the queue is flushed and the sentence is cut off — but the model carries on
generating into a void that is still charged for. `cancel_turn` ends the work itself.

| You say | What stops |
|---|---|
| "stop" | The speaking. The turn keeps running. |
| "cancel", "never mind" | The speaking, the model, and the spend. |

Like `stop_speaking`, this is marked **interrupting**: it is answered while a turn is in flight
rather than queued behind it, because a turn in flight is the only thing it has to act on. And
like `stop`, bare `cancel` is kept out of the general command vocabulary and only consulted when
there is actually something to cancel — it is too common a verb to claim outright.

One honest limit: the tokens already generated before you cancelled were already billed by the
provider, and D47 cannot see the usage figures for a stream it tore down. So a cancelled turn
records nothing in the running spend total, which slightly under-reports what the session
actually cost. Cancelling saves the generation that had not happened yet, not the generation that
had.

### `get_model_status`

Reports the selected provider and model, whether it is reachable right now, and the session's
running spend. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Real output:

```text
Provider: Anthropic
Model: claude-opus-5
Endpoint: https://api.anthropic.com
Availability: Available
Personality: on
Session so far: 3 turn(s), $0.0412
```

With no key stored, the same tool says so rather than going quiet:

```text
Provider: Anthropic
Model: claude-opus-5
Endpoint: https://api.anthropic.com
Availability: NotConfigured — No Anthropic API key is stored. Add one in Settings.
Personality: on
Session so far: 0 turn(s), $0.0000
```

## Settings

The rows below adapt to the provider you select. Choosing **None** leaves only the provider
row itself — the endpoint, key and model rows do not apply, so they are not on screen at all.
A control that is present but dead still asserts that the setting exists.

### Provider {#provider}

Which language model answers. The choices come from D47's provider catalogue:

| Id | What it is |
|---|---|
| `none` | No model. The keyword router answers what it recognises and says so when it cannot. |
| `anthropic` | Claude models over the Anthropic Messages API. |

**Protected.** Choosing a provider chooses where your turns go, so this row is unreachable
from any tool the model can call. Change it from the settings panel.

Changing the provider clears the endpoint and the model: both belong to the provider's own
namespace, and carrying one across is how a stale selection ends up failing at the first turn.

### Endpoint {#endpoint}

Points D47 at something else speaking the same protocol — a gateway, a proxy, a local
shim. Leave it empty for the provider's own endpoint, which is what the placeholder shows.

**Protected**, for the same reason as the provider row.

Changing the endpoint clears the model, and the model picker's list empties: D47 knows which
models live at `api.anthropic.com`, and it has no idea what lives at yours. The picker still
lets you type one — that is its fail-soft contract, not a special case for this row.

### Model {#model}

Which model at that endpoint. Empty means the provider's default (`claude-opus-5`), shown as
a placeholder rather than filled in as a value, so "I have not chosen" stays distinguishable
from "I chose the default".

The offered list is every model D47 can price, so anything picked from it keeps the running
total honest. A model typed by hand is accepted and priced as unknown rather than as free.

### API key {#api-key}

Stored encrypted with DPAPI for your Windows account, in `data/secrets.json` beside the
executable. **Write-only**: D47 can tell you whether a key is present and can replace it, and
there is no path — panel, tool or log — that reads one back out.

Secrets are refused for the model caller unconditionally, whether or not the row is also
marked protected.

`ANTHROPIC_API_KEY` in the environment still works and is used when the store has nothing.
Only the *source* is ever logged:

```text
[Information] Anthropic configured from the secret store, endpoint https://api.anthropic.com
```

### Personality {#personality}

Off gives plain answers. The anti-invention guardrails are unaffected either way: they sit
above the persona in the assembled prompt and nothing on this panel reaches them.

This row has spoken shortcuts, matched by the model-free router with no interpretation:

> "personality off" / "turn personality off" / "turn your personality off"
> "personality on" / "turn personality on" / "turn your personality on"

The whole utterance has to be the phrase, not merely contain it. Asking "what does personality
off actually change" is a question about the setting, and gets an answer rather than a change.

### About Me {#about-me}

Standing context about you, sent with every turn and kept between sessions. It sits inside the
cached prefix, so editing it costs one cold prefix on the next turn and nothing after that.

It is sent to the provider. See [Privacy](privacy.md).
