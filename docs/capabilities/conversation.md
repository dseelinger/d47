---
title: Language model
group: Conversation
nav_order: 118
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

### What it has cost over time {#running-totals}

The line under the panel says what the last turn cost, and **Details** beside it opens the rest:
the token counts, what the session has come to, and four running totals — **the last 7 days**,
**the last 30 days**, **this week** (Sunday to Saturday) and **this calendar month**.

Those four are kept in `data/spend.jsonl`, one line per charge, written as it happens and read
back when Directive 47 starts. Both the model and the voices go in it; a total covering only half
of what you spent would be worse than no total at all.

Each row records the instant it happened, in UTC. "This week" and "this month" are worked out
against **your** clock at the moment you ask, which is what keeps them right across a daylight
saving change and right if you ask from a different timezone than you flew in.

A charge Directive 47 could not price — a model with no published rate, or a voice provider you
have not set a rate for — is recorded with its tokens or characters and no dollar figure. Any
window containing one reports **at least** its total rather than presenting a figure that quietly
leaves part of the cost out.

The file is only ever appended to, so nothing that has already been written can be lost by a
later crash. Delete it and the running totals start again from empty; nothing else is affected.

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
| `anthropic` | Claude models, over the Anthropic Messages API. |
| `openai` | GPT models, over the OpenAI Responses API. Also reaches xAI and OpenRouter, which speak the same protocol at their own addresses. |
| `openaiCompatible` | A model **you** run — Ollama, LM Studio, vLLM, llama.cpp — or any gateway speaking the older Chat Completions protocol. |

**The model cannot change this.** Picking a provider picks where your turns go, so it is the
panel's to change and not the model's.

Changing it clears the model choice with it — model names belong to their provider, and carrying
one across is how you end up with a selection that fails at the first question.

**Why the last two are separate entries** and not one row you retarget: what leaves your machine
is written per provider, and no single sentence can say both *everything goes to OpenAI* and
*nothing leaves this machine*. Splitting them splits the key as well, which is right on its own
terms — an OpenRouter key is not an OpenAI key — and it splits the prices, which matters because
one set is published and the other cannot be.

The protocol split is the same line. Server-side web search now lives in the tools array on the
Responses API everywhere it exists, and Chat Completions is where every local server lives. No
local server has a web search anyway, so the two halves land exactly where they belong.

### Endpoint {#endpoint}

Only appears for providers where pointing somewhere else means something — a gateway, a proxy, a
local server speaking the same protocol. Anthropic has one address and no reason to accept
another, so with Anthropic selected there is no row here to worry about.

For the two OpenAI-shaped providers this is the row that matters most. **Include the version
segment**, the way every OpenAI-compatible client wants it:

```text
http://127.0.0.1:11434/v1
```

That is Ollama's default and the value the row starts with. LM Studio is usually
`http://127.0.0.1:1234/v1`; a vLLM or llama.cpp server is whatever port you started it on. If you
paste a bare origin with no path at all, Directive 47 fills the `/v1` in for you — anything else
you type is left exactly as typed, because a client that rewrites addresses is a client that
cannot reach the one place you needed.

Changing it empties the model list, and then Directive 47 **asks the endpoint what it serves** and
fills the list back in with the endpoint's own answer. Directive 47 still knows nothing about
which models live at an address it has never heard of — that has not changed and should not, since
a model name carried across from another provider is a selection that fails at the first question.
What changed is that there is now somebody to ask.

If the endpoint does not answer, or answers with an empty catalogue, the list stays empty and you
type the name yourself. That has always been supported.

### Model {#model}

Which model answers. Leave it empty for the provider's default, shown greyed out so "I have not
chosen" stays distinguishable from "I chose that one".

Anthropic's default is the highest Sonnet — currently **Claude Sonnet 5**. A companion answering
questions about a game in flight is not the work the Opus tiers are priced for, and the Opus
models are the next entries in the list if you want one. OpenAI's default is the middle tier for
the same reason.

**The OpenAI-compatible provider has no default at all**, and that is deliberate: your server
serves whatever you loaded into it, so any guess would fail at the first question. Pick from the
list the endpoint gave back, or type the name.

The offered list is every model Directive 47 can price, so anything picked from it keeps the
running cost honest. Type one by hand and it is accepted, but counted as unknown rather than as
free. Models the *endpoint* offered are in that second category — Directive 47 has no published
rate for a model it has never heard of, and inventing one would be worse than saying so.

**A model on your own machine is priced at zero, and says why.** If the endpoint is a loopback
address, the turn is free — that is a fact about the address rather than a guess about the model,
and reporting "unknown" forever about something that genuinely costs nothing is noise pretending
to be rigour.

### API key {#api-key}

Encrypted for your Windows account and kept in `data/secrets.json` beside the executable.

**For the OpenAI-compatible provider the key is optional**, and the row says so. A model running
on your machine has no account and no key to paste, and leaving the box empty is a complete
configuration rather than an unfinished one. The row is still there because a gateway speaking the
same protocol may want one.

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

**Verify Key** proves it. It is shut until you have pasted something — on an empty box the only
answer it could give is that an empty key is not a valid one — and pressing it stores what you
typed and then checks *that*, so it is never answering about the key you have just replaced. The
check is the smallest real call the provider offers — one token, no tools, no persona — and it
says what came back:

```text
Anthropic accepted the key.
```

This matters more than it sounds. A key that is wrong, revoked, or newline-padded is otherwise
indistinguishable from one that works until your first question fails, by which point you are
looking at Directive 47 not answering rather than at a key not working.

**Rejected and unreachable are different answers and are kept apart.** If the check cannot be made
at all — offline, blocked, timed out — it says so and says nothing about the key. Being told a good
key is bad would send you to your account page to issue another one that fails the same way.

**On an OpenAI-shaped endpoint the check asks for the model list instead**, and answers something
like `OpenAI answered — 5 models.` That is the better probe here for three reasons: it works with
no key, which is the whole point of running your own model; it works with no model chosen, which a
local server may well be; and it is the exact call Directive 47 makes anyway to fill the picker,
rather than a proxy for it. A server that is simply not started yet reads as unreachable, not as a
wrong address.

If you already keep `ANTHROPIC_API_KEY` in your environment, that still works and is used when
nothing is stored here.

### When your endpoint cannot do something {#demotion}

An OpenAI-compatible server is a moving target: the protocol has a dozen implementations and they
do not agree about the optional parts. Whether tool calls work, whether reasoning effort is a
field it knows, whether it will report token usage — none of that is in a model list, and
Directive 47 does not guess.

So it **advertises, then demotes**. Every request offers everything. If the endpoint refuses and
names the field it refused, that one capability is switched off for that address and the turn is
sent again without it. You see an answer, not an error and not a retry.

Four things can be dropped this way, and each costs something small rather than the turn:

| Refused | What you lose |
|---|---|
| Tool definitions | A model that can talk but not act. |
| Reasoning effort | The effort router's lever; the endpoint decides for itself. |
| Usage reporting | The turn is **unpriced** rather than mispriced. A session reported as free when it was paid for is worse than one that admits it does not know. |
| The newer token-limit field | Nothing visible — the older field is sent instead. A reply still has to stop. |

**Once per capability per address, and only for as long as Directive 47 is running.** It is never
retried in a loop, because a client hunting for a request shape the server will accept is
indistinguishable from an outage from where you are sitting. And it is never written to disk,
because a demotion saved to a file outlives the server upgrade that fixed it — and you would have
no way of knowing why the tools quietly stopped being offered.

Nothing is demoted on a guess. A refusal that names no field turns nothing off.

### The first run {#first-run}

On a fresh install there is no key, so the first thing you would otherwise do is hunt for this row
in a surface with fourteen sections. Directive 47 offers the two that matter instead — this one,
and the voice key as optional — with what each one sends and where.

If you have picked the OpenAI-compatible provider, there may be nothing to offer: a key that is
not required is not a missing one, so a local model is a complete configuration with an empty box
and the first run has nothing to ask you for.

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

**One search happens without you asking a question.** With the [lore remark](lore.md#remarks) set
to look things up, arriving in a system Directive 47 knows about searches for that system by name.
Nothing else about you goes with it, and it obeys the same rules as everything here — spoken as a
search result, never written into a table — but it is the one case where a search follows a jump
rather than a question. Setting that row to *Remark only*, or switching this one off, ends it.

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

**When it cannot search, it says so instead of answering from memory.** Directive 47 tells the
model which half is missing, because the two have different answers:

| What is missing | What you hear |
|---|---|
| This row is off | It cannot look things up, and you can turn this row on |
| The endpoint offers no search | It cannot look things up from this endpoint — Anthropic's own offers search, a gateway or a local model may not |

The endpoint wins when both apply. Being told to flip a switch that will not help is worse than
being told nothing, because you flip it, nothing changes, and the next explanation is one you have
a reason to distrust.

Nothing is said at all when search works, so having it on costs you no words about it.

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
