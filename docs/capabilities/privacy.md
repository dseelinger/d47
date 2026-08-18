---
title: Privacy
group: Foundation
nav_order: 138
---

Exactly what Directive 47 sends off this machine, to whom, and whether it is sending it right
now.

## Ask for it

> "what are you sending"
> "what leaves this machine"

## What you get

```text
2 of 4 destinations are active right now.

Language model → Anthropic
  Anthropic is selected. Your question, D47's reply so far, the guardrails, the persona and your About Me text, and the game state D47 assembled from your journal — system, body, station and docking state — are sent on every turn the model answers. Journal files themselves are never uploaded.

Update check → api.github.com, and github.com if you accept an update
  One request for the latest release tag at startup. Nothing about you goes with it — no key, no journal content, and no identifier beyond the request itself. Accepting an offered update downloads that release from github.com and replaces D47 with it; nothing is downloaded unless you ask for it.

Diagnostics and logs → nothing sent
  Logs are written beside the executable and never uploaded. There is no analytics endpoint, no metrics endpoint and no crash reporter.

Journal files → nothing sent
  Your journal is read from disk and never uploaded. Facts drawn from it — system, body, station — can reach the model as game state when one is configured; see the language model row.
```

The answer is computed from your settings as they stand at that moment. It is not a description
of what Directive 47 could do in general, and it cannot go stale the way a page like this one
can — which is why the answer is worth more than the page.

## The destinations

**Language model** — the only one that receives anything from your gameplay. What goes: your
question, the conversation so far, the guardrails, the persona, your About Me text, and the game
state assembled from your journal. What does not: journal files, your key, or anything about your
machine. Inactive when the provider is `none`, and also when a provider that *needs* a key is
chosen and has none stored — selected-but-inert sends nothing.

**And inactive when the endpoint is on this machine.** If you have pointed the OpenAI-compatible
provider at a loopback address, this row says that nothing leaves the machine at all and names the
address so you can check it. That is the first time the honest answer to *what is leaving* has
been *nothing* — and it is a property of that address in these settings right now, not a promise
about Directive 47. The judgement is deliberately literal: the address is read, never resolved, so
a hostname that happens to point at your machine today is treated as remote and disclosed in full.
Being wrong in that direction is the only safe way to be wrong here.

**Web search** — the same host as the language model, doing something different. When the
[web search row](conversation.md#let-the-model-search-the-web) is on and a question needs current
information, your provider runs the search and reads the pages; Directive 47 only ever sees the
reply. It is listed separately even though it adds no new address, because "your provider gets
your question" and "your provider will go and fetch pages about it" are not the same disclosure,
and organising this list by host rather than by what is done would have hidden the second behind
the first. Off by default, and inactive whenever no model is usable — **and inactive on an
endpoint that offers no search**, which a custom endpoint generally is. Until v0.26.1 that last
case read as active and went on to describe pages being fetched and a penny being billed, none of
which could happen there. Claiming a transfer that cannot occur is the safe direction to be wrong
in and is still wrong: a disclosure is only worth reading if it describes this machine, these
settings, right now.

**Update check** — `api.github.com` once at startup, for the latest release tag. Pressing
**Update now** adds one more transfer: the build itself, from GitHub's release downloads, which
redirect to their asset storage — so the bytes arrive from `objects.githubusercontent.com`. That
is a download rather than an upload, nothing about you goes with it, and it only happens when you
press the button. Directive 47 refuses any URL that is not an asset on a release of this
repository.

The download is checked against the `d47.zip.sha256` published beside it, and deleted rather than
unpacked if it does not match. That catches a truncated transfer or a mirror serving something else.
It is **not** a signature: the hash and the bytes come from the same server, so it cannot detect a
compromised GitHub. The same caveat applies to the speech models.

**Diagnostics and logs** — never active. Logs are written to `data/logs/` beside the executable.
There is no analytics endpoint, no metrics endpoint and no crash reporter; this row exists to be
able to say so.

**Journal files** — never active. Journals are read from disk and never uploaded. Facts drawn
from them can reach the model as game state, which is disclosed under the language model above
rather than twice.

## Your microphone is not a destination

It is worth saying plainly, because Phase 13 added hands-free listening and "the microphone is
open all the time" is a sentence that deserves an answer rather than a shrug.

**No audio ever leaves this machine, in any setting.** Speech becomes words through a model
running on your own computer; there is no cloud transcription option and there is deliberately no
row to turn one on. Audio is never written to disk either — it lives in a half-second ring buffer
and is overwritten.

What the hands-free settings change is what gets *kept*, locally, for long enough to transcribe.
In push-to-talk that is only what you held the key for; in the two hands-free settings it is every
stretch of speech in the room, and in wake-word mode the ones that were not addressed to Directive
47 are discarded without reaching the transcript, the panel or the log. Both are off out of the
box, the row that turns them on is [unreachable by the model](listening.md#mode), and the panel
shows the microphone's state the whole time it is open — on the desktop and in the headset.

The one thing that does cross the network for listening is the speech model file itself, fetched
once from `huggingface.co`. It is listed on the settings surface for as long as a model is
selected.

## Settings

### Check for updates at startup {#update-check}

One request to `api.github.com` at startup. Off means Directive 47 makes no network call of its
own, and offers no update to install.

> "stop checking for updates" / "turn off update checks"
> "start checking for updates" / "turn on update checks"

The whole sentence has to be the phrase rather than merely contain it. This is the one spoken
route that changes a setting, so a sentence that only mentions it gets an answer, not a change.

**The model cannot reach this row.** It decides whether anything leaves at all, and a model that
could switch that back on is a model that could be told to by text it read in an in-game message.

## The disclosure rows {#egress-llm}

The settings panel carries one row per destination, saying the same things this page does
{#egress-websearch} {#egress-updates} {#egress-diagnostics} {#egress-journal}
{#egress-tts} {#egress-galaxy} {#egress-communitygoals} {#egress-models} — but computed live from your settings
rather than written down once. They are read-only: not something you set, something Directive 47
says, sitting next to the settings that change it.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `get_data_egress`

Lists every destination, whether it is active under the current settings, and what is sent there.
Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

The panel's disclosure rows and this tool read one list, so neither can drift from the other.

</details>
