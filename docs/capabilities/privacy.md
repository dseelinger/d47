---
title: Privacy
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

## Running with nothing leaving at all

Local-only is a setting, not an aspiration. Set the language model
[provider](conversation.md#provider) to `none`, turn the update check off, and asking again gets:

```text
Nothing is leaving this machine right now.
```

Everything that can work without a network still does: spoken commands Directive 47 recognises
on its own, the journal reading, and every way of asking it something.

## The four destinations

**Language model** — the only one that receives anything from your gameplay. What goes: your
question, the conversation so far, the guardrails, the persona, your About Me text, and the game
state assembled from your journal. What does not: journal files, your key, or anything about your
machine. Inactive when the provider is `none`, and also when a provider is chosen but has no key
stored — selected-but-inert sends nothing.

**Update check** — `api.github.com` once at startup, for the latest release tag. Pressing
**Update now** adds one more transfer: the build itself, from GitHub's release downloads, which
redirect to their asset storage — so the bytes arrive from `objects.githubusercontent.com`. That
is a download rather than an upload, nothing about you goes with it, and it only happens when you
press the button. Directive 47 refuses any URL that is not an asset on a release of this
repository.

The download is checked against the `d47.exe.sha256` published beside it, and deleted rather than
run if it does not match. That catches a truncated transfer or a mirror serving something else.
It is **not** a signature: the hash and the bytes come from the same server, so it cannot detect a
compromised GitHub. The same caveat applies to the speech models.

**Diagnostics and logs** — never active. Logs are written to `data/logs/` beside the executable.
There is no analytics endpoint, no metrics endpoint and no crash reporter; this row exists to be
able to say so.

**Journal files** — never active. Journals are read from disk and never uploaded. Facts drawn
from them can reach the model as game state, which is disclosed under the language model above
rather than twice.

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
{#egress-updates} {#egress-diagnostics} {#egress-journal} — but computed live from your settings
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
