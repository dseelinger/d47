---
title: Language model
group: Conversation
nav_order: 114
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
Speech so far: 1,204 characters spoken, $0.1204
```

**Speech is counted separately and in a different unit**, because it is billed in a different
unit: the model is billed per token and the voice per character. It is here rather than on a
report of its own so that "what has this cost" has one answer — see
[what the voices cost](speech.md#voice-cost) for where the rate comes from and why it is an
assumption you can correct. A provider that charges nothing says **free** rather than `$0.00`,
and the line is absent entirely until something has been spoken.

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

**Show** unmasks what you are typing, on the way in only — a stored key is still never shown back.
It exists because the commonest reason a key does not work is that it was pasted wrong, and you
cannot see that through bullets. What you paste is **trimmed** before it is stored: a key copied
from a browser arrives with a trailing newline more often than not, and a newline fails at the
provider in a way that reads as a wrong key rather than as a bad paste.

**Check** proves it. It makes the smallest real call the provider offers — one token, no tools, no
persona — and says what came back:

```text
Anthropic accepted the key.
```

This matters more than it sounds. A key that is wrong, revoked, or newline-padded is otherwise
indistinguishable from one that works until your first question fails, by which point you are
looking at Directive 47 not answering rather than at a key not working.

**Rejected and unreachable are different answers and are kept apart.** If the check cannot be made
at all — offline, blocked, timed out — it says so and says nothing about the key. Being told a good
key is bad would send you to your account page to issue another one that fails the same way.

If you already keep `ANTHROPIC_API_KEY` in your environment, that still works and is used when
nothing is stored here.

### The first run {#first-run}

On a fresh install there is no key, so the first thing you would otherwise do is hunt for this row
in a surface with fourteen sections. Directive 47 offers the two that matter instead — this one,
and the voice key as optional — with what each one sends and where.

**It is not a wall.** Decline everything and Directive 47 still runs: you get a typed companion
that reads your journal and answers from what it can see, rather than one that talks back. That is
the same rule as everywhere else here — a capability without its key is off, not broken.

**There is no "we have shown this" flag**, and that is deliberate. The condition is *there is no
usable language-model key*, asked fresh each time. That is also true if you copy your `data\`
folder to another machine: `secrets.json` is encrypted for one Windows account, so on the new one
those values cannot be decrypted and are reported absent — and the offer appears, on exactly the
machine that needs it. A flag would have been set on the old machine and would suppress it forever
on the new one.

Reopen it any time from **About → Set up keys**. Keys get rotated and revoked, so the state that
triggers this is one a working install can come back to.

### Personality {#personality}

Off gives you plain answers. It does not loosen anything: the rules that stop Directive 47
inventing capabilities it does not have are separate, and nothing on this panel can reach them.

Spoken shortcuts, recognised without the model:

> "personality off" / "turn personality off" / "turn your personality off"
> "personality on" / "turn personality on" / "turn your personality on"

The whole sentence has to be the phrase rather than merely contain it, so asking "what does
personality off actually change" gets you an answer instead of switching it off.

### Let the model search the web {#let-the-model-search-the-web}

Off by default. On, Directive 47 can look something up online when a question turns on current
information — what a patch changed, what other Commanders are reporting, a community guide that
did not exist when the model was trained.

**Directive 47 does not do the searching.** Your language-model provider does, on the far side of
the connection you already have with them, and only the reply comes back. So this opens no new
destination: nothing goes anywhere that was not already receiving your turns. What it does change
is that the model can now read arbitrary pages about what you asked, and the wording of the
search — drawn from your question — goes with it. The [Privacy](privacy.md#egress-websearch) row
says so while it is on.

Two things are true of anything found this way. It is **spoken as something read, with the source
named**, and never quietly mixed into the ship and galaxy figures Directive 47 was built with —
if a page disagrees with those, you get told both and which is which. And it is **never written
into Directive 47's own tables**: those are generated from recorded sources, and a search result
has no route into them.

Searches are billed by your provider on top of the turn, at roughly a penny each. The turn price
counts them, so a searching turn reads as what it actually cost. A turn will not search more than
three times.

It needs a working language model, so it does nothing with the provider set to `none` or with no
key stored. On a custom [endpoint](#endpoint) it stays off whatever this row says: whether a
gateway can search is not something Directive 47 can know, and asking one that cannot would fail
the turn rather than answer it without searching.

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
