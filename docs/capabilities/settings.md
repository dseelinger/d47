---
title: Settings
group: Foundation
nav_order: 143
---

Changing Directive 47's settings by asking, and the things it will not change however nicely
you ask.

Everything is changeable from the settings panel with no file to hand-edit, and every change
takes effect at once — there is no save button and no restart. This page is about the other way
in: what you can change by talking to it.

## Ask for it

> "what can you change"
> "what theme are you using"
> "switch to the guardian theme"
> "turn journal logging up to debug"

Say a value it does not offer and it tells you the ones it does, rather than quietly ignoring
you:

```text
'chatty' is not a valid Theme. Expected one of: elite, dark, light, guardian, elite-palette.
```

Clearing a setting is always allowed and puts it back to its default — which is the greyed-out
value the box has been showing you all along.

## What it will not change by voice

Some rows are marked **protected**. You can change them from the panel, with a bound key, or by
saying one of the specific phrases the app recognises on its own — but never through the
language model.

| Row | Why |
|---|---|
| Which language model answers, and where | It decides what leaves this machine |
| Any API key | It is your key |
| Check for updates at startup | It decides whether anything leaves at all |
| Every hotkey | Rebinding one hands out a way in |

The reason is worth a sentence, because it is the whole point. Directive 47 reads text nobody
here wrote: journal entries carrying another Commander's chosen ship name, in-game messages from
anyone in range, and later web results. **Anything the model can be asked to do, a hostile
message can try to make it do.** A guard the model can switch off is not a guard, so the rows
that decide what leaves your machine are not on its list at all.

Ask anyway and you get a straight answer rather than a pretence that the setting does not exist:

```text
'Provider' is protected. It can be changed from the settings panel, but not by me.
```

## Some rows are the Commander's, not the installation's

Several Commanders can play on one machine, and Elite tells Directive 47 which one is flying.
Most settings are the installation's — keys, devices, theme, zoom, hotkeys — and stay the same
whoever logs in. A few are about the *person in the chair*, and those are marked **per
Commander** on the row: the [Character sheet](conversation.md#character-sheet),
[About Me](conversation.md#about-me), and which ship the
[core-binding rows](persona.md#core-for-this-ship) are pointed at. Each Commander sees their own
value there, and a second Commander on the same machine never sees the first one's story.

How it works, because it affects what you see in the file:

- A per-Commander row reads the installation's value until that Commander sets their own. The
  installation's value is whatever was there before anyone logged in — including everything in a
  settings file from before this existed.
- Once a Commander sets a row, their value lives in their own entry under `commanders` in
  `settings.json`, keyed by their Frontier id with their name written beside it. The
  installation's value is untouched.
- **Clearing a per-Commander row is a choice, not a reset.** A Commander who empties About Me
  reads nothing — not the installation's story, and not somebody else's. In the file that is an
  empty string; a value that was never set is `null`, and the two are deliberately different.
- Anything set before Elite has said who is flying is the installation's. It is not handed to
  whoever logs in first.

What is *not* per Commander, and on purpose: the running cost of the AI. That is the person's
bill across every character they play, so the spend ledger is one ledger for the installation.

## How values are read

Settings are forgiving about how you say them. `TRUE`, `on` and `True` are one answer, not
three.

| Kind of setting | What it accepts |
|---|---|
| A switch | `true`/`on`/`yes`/`enabled`/`1`, or `false`/`off`/`no`/`disabled`/`0` |
| A choice | One of the offered values, in any case |
| A number | A whole number |
| Text, or a key to press | Anything |
| A key or password | Only ever written, never read back |

Most choices are closed — a theme is one of five. A few are deliberately open: the model name is
one, because an endpoint Directive 47 has never seen still has models it knows nothing about.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

Protection is a property of the caller rather than of the modality. The same row is reachable
from the settings panel, from a bound hotkey and from the model-free keyword router, and
unreachable from anything the model can invoke.

| Caller | May change a protected row |
|---|---|
| Settings panel | Yes |
| Bound hotkey | Yes |
| Keyword router (model-free) | Yes |
| Tool call from the model | **No** |

Enforced in one place — `SettingsService.Apply` — so a new capability cannot opt itself out by
forgetting. Secrets are refused for the model caller whether or not anyone remembered to also
mark the row protected. Hotkeys are protected because a gesture is one of the three trusted
callers: a model that could rebind one could hand itself a caller it is not allowed to be.

### `list_settings`

Lists the settings a tool call may change, with their current values. Protected rows, secrets
and read-only disclosures are not listed: a row the model cannot change is a row it has no
reason to know the name of.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

```text
logging.default — Default log level: Information
logging.subsystems.journal — Journal log level: (default)
    one of: Trace, Debug, Information, Warning, Error, Critical, None
llm.model — Model: (default: claude-opus-5)
    one of: claude-opus-5, claude-opus-4-8, claude-sonnet-5, claude-haiku-4-5, claude-fable-5
llm.personality — Personality: true
ui.theme — Theme: elite
```

### `get_setting`

Reports one setting's current value by key.

```json
{"type":"object","properties":{"key":{"type":"string","description":"The setting key, as reported by list_settings."}},"required":["key"],"additionalProperties":false}
```

### `set_setting`

Changes one setting. Applies immediately — there is no save step and no restart. Omitting the
value clears the setting back to its default.

```json
{"type":"object","properties":{"key":{"type":"string","description":"The setting key, as reported by list_settings."},"value":{"type":"string","description":"The new value. Omit it to clear the setting back to its default."}},"required":["key"],"additionalProperties":false}
```

</details>
