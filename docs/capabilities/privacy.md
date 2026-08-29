---
title: Privacy
group: Foundation
nav_order: 142
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
state assembled from your journal. The lines D47 says in character without being asked — ambient
remarks, the greeting, a core's first words — carry your character sheet too, and about one ambient
remark in four carries your About Me story; the carrier's captain and tower get neither. What does
not: journal files, your key, or anything about your machine. Inactive when the provider is `none`, and also when a provider that *needs* a key is
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

### A voice that is not a destination either

Since Phase 59 there is a speech provider that sends nothing: **Kokoro** runs on this computer. Its
model is downloaded once from `huggingface.co` — which is a destination, and is listed as one for
as long as a download is happening — and after that the voice needs no network at all.

That is the whole reason it exists. Every other provider, free ones included, receives the text
Directive 47 speaks, and that includes re-voiced in-game messages written by other players. With
Kokoro selected for those slots, nobody's words leave your machine to be spoken.

**Donated excerpts and journal histories** — the newest destination, and the one that reverses a
sentence Directive 47 used to say about itself. Donation shipped with nowhere to send: an excerpt
was copied and pasted by a human, and a comment in the code said *there is no backend and there is
not going to be one*. There is one now, argued in
[#175](https://github.com/dseelinger/d47/issues/175), because the requirement changed underneath
that sentence — a donated journal history is 32.5 MB compressed followed by about 85 KB a day, and
no manual route carries that or can be reached by a stranger.

**It is not the no-telemetry rule bending.** That rule is about ambient collection: crash
reporters, analytics SDKs, hosted logging, all of which send without anybody asking. A donation
sends nothing at all until you have read a full description of it and pressed a button, every time
— no standing consent, nothing remembered, no automatic upload — and the row above says so.

**The address is the switch.** Out of the box there is no donation address, so nothing can be
uploaded: the donation windows offer a clipboard and a file, exactly as they did before. Set one
and the same windows gain a send button. Clear it and they lose it again. There is no separate
toggle because a toggle beside an empty address would be a control with nothing to control.

**What is stored, and for how long.** An incident excerpt is kept for 30 days or until the defect
it was cut for is closed, whichever comes first — enforced as a rule on the store rather than as a
number somebody remembers. A donated journal history is kept **indefinitely**, on purpose: it is a
regression case, and one that expires stops being one. Either goes on request, and either is a
single delete.

**Directive 47 keeps its own copy of what it sent.** In `data\donations`, beside the executable:
the exact bytes, and their hash. While a human was the transport, *what is shown is what leaves*
was something you could see; an upload turns it into a claim about code, and the receipt turns it
back into something you can test with `certutil -hashfile`. It is also what a deletion request
quotes.

### Your donations are grouped, and that is a reversal too {#donor-token}

Directive 47 used to say that two donations from the same Commander could not be joined. That was
true, and it is not any more.

A journal history you add to has to be recognisable as the same history, or it is not an
accumulation — it is a pile of unrelated blobs. So a send carries a **random number identifying
this installation**: made on this machine the first time you donate and never before, not derived
from your Commander name, your Frontier ID or anything about your computer, and used for donations
and nothing else. The stand-in names inside a donation are unchanged — still per-donation, still
by field list.

The claim is now weaker and still worth stating: **donations from one installation accumulate
under a random token that identifies an install, not a person.** You read it here, and in the
donation window, before the first donation rather than after
([#176](https://github.com/dseelinger/d47/issues/176)).

**Deleting it is a withdrawal, and it is no harder than consenting was.** Delete
`data\donor-token.txt` and future donations stop joining the ones already sent. It does not reach
back: what has gone stays under the old identifier until it is deleted at the store, which is a
separate ask.

**Two things are accepted rather than solved, and both are said here rather than discovered.**
A thirteen-month history is easier to re-identify than a ten-minute excerpt — more surface, more
distinctive routes, more chance one rare system pins the set to a person — and pseudonymised was
never anonymous. And a second PC, a fresh install with no restore, or a cleared `data\` folder
starts a second pile under a new number, with nothing saying the two halves are one history. The
only fix for that would be a token derived from your machine, which is the identifier this whole
design refuses.

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

### Where donations are sent {#donation-endpoint}

Empty out of the box, and empty means nothing can be uploaded. There is no address baked into the
build: the store behind it is Cloudflare R2, which needs a payment method on the account before it
will activate at all, so there is nothing to point at until somebody has deliberately provisioned
one. `worker/README.md` in the repository is that provisioning, and the Worker it deploys is the
only thing that can write to the bucket — there is no storage credential anywhere in Directive 47.

Only `https` addresses are accepted. A scrubbed journal on a plaintext connection is a worse
outcome than not donating.

**The model cannot reach this row**, for the same reason it cannot reach the update check and then
some: this one names where a scrubbed journal history goes, and a model that could set it is a
model that could be told to by an in-game message it read.

### What Directive 47 remembers about you {#memory}

A read-only summary of the [memory](memory.md) store — how many facts are kept, and how many of them
you told it yourself — with one button that empties the file.

**It empties every Commander in the file, not just the one currently aboard.** "Forget me" is not
"forget the character I happen to be logged in as", and a privacy control that left three other
characters' facts on disk would be one that reads as a control and is not.

The button is here rather than in the memory section because this is where you would look for it.
There is **no spoken phrase for it and no tool that can call it** — "forget everything about me" is a
sentence a transcriber can produce out of a misheard one, and this is the one action in that feature
that cannot be undone.

## The disclosure rows {#egress-llm}

The settings panel carries one row per destination, saying the same things this page does
{#egress-websearch} {#egress-updates} {#egress-diagnostics} {#egress-journal}
{#egress-tts} {#egress-galaxy} {#egress-communitygoals} {#egress-models} {#egress-notableplaces}
{#egress-donation} — but computed live from your settings
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
