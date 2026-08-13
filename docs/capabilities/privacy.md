---
title: Privacy
---

# Privacy

**Group:** Foundation
**Capability id:** `privacy`

Exactly what d47 sends off this machine, to whom, and whether it is sending it right now.

This page is the long form of the disclosure rows on the settings panel. Both read the same
list, so neither can drift from the other, and both describe the settings as they stand rather
than as they were documented.

## Try it

> "what are you sending"
> "what leaves this machine"

## Tools

### `get_data_egress`

Lists every destination d47 can send to, whether it is active with the current settings, and
what is sent there. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Real output, with Anthropic configured and update checks on:

```text
2 of 4 destinations are active right now.

Language model → https://api.anthropic.com
  Anthropic is selected. Your question, d47's reply so far, the guardrails, the persona and your About Me text, and the game state d47 assembled from your journal — system, body, station and docking state — are sent to the endpoint below on every turn the model answers. Journal files themselves are never uploaded.

Update check → api.github.com, and github.com if you accept an update
  One request for the latest release tag at startup. Nothing about you goes with it — no key, no journal content, and no identifier beyond the request itself. Accepting an offered update downloads that release from github.com and replaces d47 with it; nothing is downloaded unless you ask for it.

Diagnostics and logs → nothing sent
  Logs are written beside the executable and never uploaded. There is no analytics endpoint, no metrics endpoint and no crash reporter.

Journal files → nothing sent
  Your journal is read from disk and never uploaded. Facts drawn from it — system, body, station — can reach the model as game state when one is configured; see the language model row.
```

## Local-only operation

Local-only is a configuration, not a theoretical one. Set the language model
[provider](conversation.md#provider) to `none` and turn the update check off, and the same
tool answers:

```text
Nothing is leaving this machine right now.
```

Everything still works that can work without a network: the model-free keyword router answers
what it recognises, the journal spine keeps reading, and every input path stays answerable.

## Settings

### Check for updates at startup {#update-check}

One request to `api.github.com` for the latest release tag, made once at startup. Off means
d47 makes no network call of its own, and offers no update to install.

**Protected.** This row decides whether anything leaves at all, so it is unreachable from the
tool surface — a model that could switch egress back on is a model that could be told to by
text it read in an in-game message.

Spoken shortcuts, matched by the model-free router:

> "stop checking for updates" / "turn off update checks"
> "start checking for updates" / "turn on update checks"

The whole utterance has to be the phrase, not merely contain it — this is the one router path
that writes, so a sentence that merely mentions a phrase gets an answer, not a change.

## Disclosures

The four rows below are read-only. They are not something you set; they are something d47
says, and they sit next to the settings that change them rather than in a document nobody
opens. The row headings are fixed; the text under each is computed from your current settings.

### Language model {#egress-llm}

The only destination that receives anything derived from your gameplay. What goes: the turn
text, the conversation so far, the guardrails, the persona, your About Me text, and the game
state d47 assembled from the journal. What does not go: journal files, your key, or anything
about your machine.

Reads as inactive when the provider is `none`, and also when a provider is selected but has no
key stored — selected-but-inert sends nothing.

### Update check {#egress-updates}

`api.github.com`, once at startup, controlled by the row above.

Accepting an offered update adds a second transfer: the build itself, fetched from
`https://github.com/dseelinger/d47/releases/download/...`, which redirects to GitHub's asset
storage — so the bytes arrive from `objects.githubusercontent.com`. It is a download and not an
upload; nothing about you goes with it either. It only ever happens when you press **Update
now**, and d47 refuses any URL that is not an asset on a release of this repository.

The download is checked against the `d47.exe.sha256` published beside it and deleted rather
than run if it does not match. That catches a truncated transfer or a mirror serving something
else. It is **not** a signature: the hash and the bytes come from the same server, so it cannot
detect a compromised GitHub. The same caveat applies to the speech models.

### Diagnostics and logs {#egress-diagnostics}

Never active. Logs are written to `data/logs/` beside the executable. There is no analytics
endpoint, no metrics endpoint and no crash reporter — this row exists to be able to say so.

### Journal files {#egress-journal}

Never active. Journals are read from disk and never uploaded. Facts drawn from them can reach
the model as game state, which is disclosed under the language model row rather than here, so
that one destination has one description.
