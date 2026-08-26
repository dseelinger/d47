---
title: Language model
group: Conversation
nav_order: 119
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Which model answers you, whether anything leaves this machine at all, and what it has cost.</p>
<section>
<h2><span class="num">1</span> You choose where your turns go — including nowhere.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Four providers: none, Anthropic, OpenAI, or a model you run yourself">
 <rect x="20" y="34" width="195" height="124" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="117" y="72" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">NONE</text>
 <text x="117" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">nothing leaves</text>
 <text x="117" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">this machine</text>
 <rect x="235" y="34" width="195" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="332" y="72" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">ANTHROPIC</text>
 <text x="332" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">your turns go</text>
 <text x="332" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">to Claude</text>
 <rect x="450" y="34" width="195" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="547" y="72" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">OPENAI</text>
 <text x="547" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">or xAI, or</text>
 <text x="547" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">OpenRouter</text>
 <rect x="665" y="34" width="195" height="124" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="762" y="72" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">YOUR OWN</text>
 <text x="762" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">a model you run,</text>
 <text x="762" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">here, priced at zero</text>
 <rect x="20" y="180" width="840" height="52" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="212" text-anchor="middle" font-size="16" fill="var(--text)">Picking a provider picks where your turns go — so the panel changes it, and the model never can.</text>
</svg>
<p class="body">With <strong>None</strong> you still have a companion: it reads your journal, answers what it recognises on its own, and says so when it cannot. A capability without its key is off, not broken.</p>
</section>
<section>
<h2><span class="num">2</span> “Stop” and “cancel” are different, and the difference is on your bill.</h2>
<svg viewBox="0 0 880 266" role="img" aria-label="Stop ends only the speaking while the model keeps costing; cancel ends the model and the spend too">
 <rect x="20" y="40" width="400" height="170" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="84" text-anchor="middle" font-size="21" font-weight="800" fill="var(--text)">“stop”</text>
 <text x="220" y="122" text-anchor="middle" font-size="16" fill="var(--text)">the speaking stops</text>
 <text x="220" y="152" text-anchor="middle" font-size="16" fill="var(--text-muted)">the model keeps working</text>
 <text x="220" y="182" text-anchor="middle" font-size="16" font-weight="700" fill="var(--danger)">and keeps costing</text>
 <rect x="460" y="40" width="400" height="170" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="84" text-anchor="middle" font-size="21" font-weight="800" fill="var(--text)">“cancel”</text>
 <text x="660" y="122" text-anchor="middle" font-size="16" fill="var(--text)">the speaking stops</text>
 <text x="660" y="152" text-anchor="middle" font-size="16" fill="var(--text)">the model stops</text>
 <text x="660" y="182" text-anchor="middle" font-size="16" font-weight="700" fill="var(--accent)">and so does the spend</text>
 <text x="440" y="246" text-anchor="middle" font-size="16" fill="var(--text-muted)">Both act on a turn in flight — the only thing either of them has to act on.</text>
</svg>
<p class="body">One honest limit: whatever the model had already produced before you cancelled was already billed by the provider. Cancelling saves the work that had not happened yet, not the work that had.</p>
</section>
<section>
<h2><span class="num">3</span> Two bills, one answer.</h2>
<svg viewBox="0 0 880 256" role="img" aria-label="The model billed per token and the voices billed per character add up to one running total">
 <rect x="20" y="30" width="300" height="90" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="170" y="66" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">THE MODEL</text>
 <text x="170" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">per token — $0.0412</text>
 <rect x="20" y="136" width="300" height="90" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="170" y="172" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">THE VOICES</text>
 <text x="170" y="202" text-anchor="middle" font-size="15" fill="var(--text-muted)">per character — 1,204</text>
 <line x1="332" y1="75" x2="400" y2="75" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <line x1="332" y1="181" x2="400" y2="181" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <line x1="400" y1="75" x2="400" y2="181" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <line x1="400" y1="128" x2="452" y2="128" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="466,128 450,120 450,136" fill="var(--accent-muted)"/>
 <rect x="478" y="66" width="382" height="124" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="669" y="106" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">ONE ANSWER</text>
 <text x="669" y="136" text-anchor="middle" font-size="16" fill="var(--text)">to “what has this cost”</text>
 <text x="669" y="166" text-anchor="middle" font-size="15" fill="var(--text-muted)">7 days · 30 days · this week · this month</text>
 <text x="440" y="248" text-anchor="middle" font-size="15" fill="var(--text-muted)">A charge it cannot price is kept without a figure, and the total says at least.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> An endpoint that cannot do something loses that, not your turn.</h2>
<svg viewBox="0 0 880 244" role="img" aria-label="Every request offers everything; a refusal that names its field drops that one capability and the turn is sent again">
 <rect x="20" y="40" width="230" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="135" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">EVERY REQUEST</text>
 <text x="135" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">offers everything</text>
 <line x1="262" y1="88" x2="298" y2="88" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="312,88 296,80 296,96" fill="var(--accent-muted)"/>
 <rect x="325" y="40" width="230" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">IT REFUSES ONE</text>
 <text x="440" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">and names the field</text>
 <line x1="567" y1="88" x2="603" y2="88" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="617,88 601,80 601,96" fill="var(--accent-muted)"/>
 <rect x="630" y="40" width="230" height="96" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="745" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SENT AGAIN</text>
 <text x="745" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">without that one thing</text>
 <rect x="20" y="160" width="840" height="52" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="192" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">You see an answer, not an error and not a retry.</text>
 <text x="440" y="236" text-anchor="middle" font-size="15" fill="var(--text-muted)">Once per address, and never written to disk — a saved demotion outlives the upgrade that fixed it.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="persona.html"><span class="ct">Persona →</span><span class="cd">Who is answering, and why the model cannot change that.</span></a>
<a class="card" href="privacy.html"><span class="ct">Privacy →</span><span class="cd">Every destination that is open right now, computed rather than written down.</span></a>
<a class="card" href="speech.html"><span class="ct">Speech →</span><span class="cd">The other half of the bill, and where its rate comes from.</span></a>
</div>
</div>
</div></div>

## The details

Which model answers you, whether it can be reached right now, and what the session has cost so
far. This is also where you decide whether Directive 47 talks to anything outside this machine
at all.

### Ask for it

> "which model are you using"
> "what has this session cost"
> "personality off"

### What it tells you

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

#### What it has cost over time {#running-totals}

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

### Stopping a turn

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

### Settings

The rows here follow the provider you pick. Choose **None** and the rest disappear rather than
sitting there greyed out.

#### Provider {#provider}

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

#### Endpoint {#endpoint}

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

#### Model {#model}

Which model answers. Leave it empty for the provider's default, shown greyed out so "I have not
chosen" stays distinguishable from "I chose that one".

**This is the model your *conversation* takes.** The things Directive 47 says without being asked
can be sent somewhere cheaper — see [Model for the quiet calls](#background-model), two rows down.

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

**The cheaper models carry live game state under a weaker guarantee, and it is worth knowing before
you pick one.** On Claude Opus 5, Opus 4.8 and Fable 5, what your ship is doing right now reaches
the model under a role that journal content cannot imitate. Everywhere else — Claude Haiku 4.5,
Sonnet 5, and every OpenAI-compatible endpoint — it is folded into the message instead, marked off
by a convention rather than by a boundary. That is the well-travelled path rather than a new risk,
and the guardrails that say in-game text is information rather than instruction are above all of it
either way. But a hostile ship name has one more thing it can try on the cheap models than on the
expensive ones, so the choice is a real one and not only about money.

#### Model for the quiet calls {#background-model}

Which model writes the things you did not ask for: an ambient remark, the brief when you sit down,
what Directive 47 says after a long gap, a lore lookup, and choosing a voice for a core. Leave it
empty and they use the model above, which is what every version before this one did.

**It is close to free money, and the reason is caching rather than the rate card.** A conversation
turn re-sends everything said so far, and the provider charges the cheap cached rate for a prefix
it has seen before — but a cache belongs to one model, so *alternating* between two models pays to
write the cache again each time you come back. One detour costs several times what the cheap turn
saved, which is why Directive 47 will never switch models question by question. The quiet calls are
the opposite case: none of them carry the conversation, every one of them already starts cold, so
sending them somewhere cheaper costs nothing at all and saves most of what Directive 47 spends
while you are not talking to it.

**Two calls deliberately ignore this row.** Writing an adventure and writing your Commander's log
both stay on the model above — you pressed a button and are waiting, the output has to name real
systems exactly, and the log is quoted at a price before it is written.

It clears itself when you change provider or endpoint, exactly as the model row does: a model name
belongs to the endpoint that serves it, and one carried across would fail on the first ambient
remark, which is a failure nobody is watching for.

#### Think at least this hard {#effort-floor}

The least effort a question gets, however plain it looked.

Directive 47 gauges every question on its own — a lookup gets the cheapest setting, planning a
route gets more, and asking it to think carefully gets the most. That gauge is a heuristic reading
your words, and it is sometimes wrong in the cheap direction. This row is where you say the bottom
rung is not enough for you.

The rungs are **Low, Medium, High, Xhigh and Max**. Leave it empty and the gauge decides, which is
the default.

Only your *conversation* is held to it. The quiet calls above are not, and that is deliberate: a
floor of High would turn every ambient remark into a reasoning call, which is exactly the spending
the row two above exists to stop.

#### Never think harder than this {#effort-ceiling}

The most effort a question gets, however hard it sounded.

Thinking is most of what a turn costs, so this is the dial that decides what an expensive-looking
question is allowed to spend. It also catches the gauge being wrong in the other direction: it
matches on the words you used and not on grammar, so an idle "what do you think about the Corvette"
contains "think about" and is priced as a request to deliberate.

Each of the two rows offers only the rungs the other allows, so you cannot set a floor above a
ceiling from the panel.

**Two things you can say out loud**, because this is the row you will want to reach with your hands
on the stick:

```text
stop thinking so hard      → the ceiling becomes Medium
think as hard as you like  → the ceiling is cleared
```

#### API key {#api-key}

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

#### When your endpoint cannot do something {#demotion}

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

#### The first run {#first-run}

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

#### Personality {#personality}

Off gives you plain answers. It does not loosen anything: the rules that stop Directive 47
inventing capabilities it does not have are separate, and nothing on this panel can reach them.

Spoken shortcuts, recognised without the model:

> "personality off" / "turn personality off" / "turn your personality off"
> "personality on" / "turn personality on" / "turn your personality on"

The whole sentence has to be the phrase rather than merely contain it, so asking "what does
personality off actually change" gets you an answer instead of switching it off.

#### Let the model search the web {#let-the-model-search-the-web}

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

#### Character sheet {#character-sheet}

Who your Commander is, in a few lines — name, where they are from, age, how they speak. Kept
between sessions.

D47 carries it with every turn **and with everything it says in character**: the ambient remarks,
the greeting when you sit down, a core's first words. Before this row existed those lines were
written by a model that had never heard of the person flying, which is why they felt generic — ten
ways to say *docked at a station* are still ten ways to say nothing. Keep it short: it is some forty
tokens, and those forty go out on every remark.

The carrier's captain and tower do not get it. They are strangers on a comms channel and do not know
your history — the same reason they do not get the core's persona.

**Per Commander.** Another Commander logging in on this machine sees their own sheet, not yours —
see [Some rows are the Commander's](settings.md#some-rows-are-the-commanders-not-the-installations).

#### About Me {#about-me}

Your Commander's story, in your own words, as long as you like — how you fly, what you are working
towards, where you have been. Kept between sessions.

**It is treated as true.** A biography names people, places and factions that are your own invention
rather than Frontier's lore, and the answer is not a disclaimer: it is your character in your game,
and the ship's AI is told the story is true of the world you share and to inhabit it. A core that
speaks of a Power with suspicion because you have reason to is the feature. Operational answers are
unaffected — those come from tools and tables, not from this box.

It is sent with every turn, where it sits inside the cached prefix and costs nothing after the first.
It also goes with **about one ambient remark in four** — the story is the expensive half, and a remark
about a docking bay does not need thirteen hundred tokens of history every time, only now and then
when it might land. Which remark is chosen by the same count that picks the stock line, never by a
clock, so a recorded session replays to the same calls.

**Per Commander, and empty is meaningful.** Each Commander on this machine has their own story;
one who has never written one reads the installation's, and one who clears the box reads nothing —
not the installation's, and not another Commander's. When a different Commander logs in, the
prompt is rebuilt around their story, and because the story sits inside the cached prefix that
rebuild throws the cache away. That is one slower first turn per login, accepted on purpose: a
login is rare, and a Commander must never be answered from somebody else's cached history.

Both go to the provider along with everything else. See [Privacy](privacy.md).

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `cancel_turn`

Abandons the turn currently running: stops speaking, tears down the provider stream, and stops
spending. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Marked **interrupting**, so it is answered while a turn is in flight rather than queued behind
it. Bare "cancel" is kept out of the general command vocabulary and only consulted when there is
something to cancel — too common a verb to claim outright.

#### `get_model_status`

Reports the selected provider and model, whether it is reachable, and the session's running
spend. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

The endpoint is reported only when the Commander has chosen one; a line stating where Anthropic
lives tells them something they knew. **The model for the quiet calls follows the same rule** and
appears only where it differs — otherwise it would repeat the model named on the line above.

About Me and the character sheet sit inside the cached prompt prefix, so editing either costs one
cold prefix on the next turn and nothing after that. Off the turn path there is no such shelter:
`FlavourBrief.NeedsAboutMe` says whether a line carries position 4 at all and `NeedsStory` whether
the story goes with the sheet, decided in Core from the announcement's `Variant` —
`CommanderStory.StoryEvery` is the cadence. Measured on 2026-08-21 against a forty-token sheet and a
thirteen-hundred-token story at claude-opus-5 list price: the sheet and its label add about **0.06¢**
to a remark, the story about **0.7¢** to the one in four that carries it — 0.22¢ per remark averaged
over the cadence (see `list.md` Phase 43 for the figures and how they were taken).
Secrets are refused for the model caller unconditionally, whether or
not the row is also marked protected. Only the *source* of a key is ever logged:

```text
[Information] Anthropic configured from the secret store
```

</details>
