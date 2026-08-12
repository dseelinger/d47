---
title: Settings
---

# Settings

**Group:** Foundation
**Capability id:** `settings`

The model's view of the settings surface — deliberately smaller than yours.

Every d47 setting is changeable from the panel with no hand-editing, and takes effect
immediately. This capability is about the *other* callers: what a tool call may change, and
what it may not.

## The protected rule

Protection is a property of the caller, not of the modality. The same row is reachable from
the settings panel, from a bound hotkey and from the model-free keyword router, and is
unreachable from anything the model can invoke.

That asymmetry is not fussiness. d47 feeds the model text it did not author — journal entries
carrying another Commander's chosen ship name, in-game messages from anyone in range, and
later web and INARA results. Anything the model can call, a hostile message can attempt to
invoke. A guard the model can flip is not a guard.

| Caller | May change a protected row |
|---|---|
| Settings panel | Yes |
| Bound hotkey | Yes |
| Keyword router (model-free) | Yes |
| Tool call from the model | **No** |

Enforced in one place — `SettingsService.Apply` — so a new capability cannot opt itself out by
forgetting. Secrets are refused for the model caller whether or not anyone remembered to also
mark the row protected.

Protected today: the language model [provider](conversation.md#provider) and
[endpoint](conversation.md#endpoint), every API key, the
[update check](privacy.md#update-check), and every [hotkey](interface.md#open-settings) —
a gesture is one of the three trusted callers, so a model that could rebind one could hand
itself a caller it is not allowed to be.

## Tools

### `list_settings`

Lists the settings that a tool call may change, with their current values. Protected rows,
secrets and read-only disclosures are not listed: a row the model cannot change is a row it
has no reason to know the name of.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Real output:

```text
logging.default — Default log level: Information
    one of: Trace, Debug, Information, Warning, Error, Critical, None
logging.subsystems.journal — Journal log level: (default)
    one of: Trace, Debug, Information, Warning, Error, Critical, None
llm.model — Model: (default: claude-opus-5)
    one of: claude-opus-5, claude-opus-4-8, claude-sonnet-5, claude-haiku-4-5, claude-fable-5
llm.personality — Personality: true
llm.aboutMe — About Me: (default: (nothing yet))
ui.theme — Theme: elite
```

### `get_setting`

Reports one setting's current value by key.

```json
{"type":"object","properties":{"key":{"type":"string","description":"The setting key, as reported by list_settings."}},"required":["key"],"additionalProperties":false}
```

```text
Theme is elite.
```

### `set_setting`

Changes one setting. Applies immediately — there is no save step and no restart.

```json
{"type":"object","properties":{"key":{"type":"string","description":"The setting key, as reported by list_settings."},"value":{"type":"string","description":"The new value. Omit it to clear the setting back to its default."}},"required":["key"],"additionalProperties":false}
```

On success:

```text
Theme is now guardian.
```

Omitting `value` clears the setting back to its default:

```text
Model is now (default).
```

On a protected row, the refusal names where the setting lives rather than pretending it does
not exist — you asking d47 to change your provider deserves an answer, and the refusal is the
safety property, not the silence:

```text
'Provider' is protected. It can be changed from the settings panel, but not by me.
```

## Values

Every row is text on the way in, canonicalised on the way to disk, so `TRUE`, `on` and `True`
are one setting rather than three.

| Kind | Accepts |
|---|---|
| Toggle | `true`/`on`/`yes`/`enabled`/`1`, or `false`/`off`/`no`/`disabled`/`0` |
| Choice | One of the offered values, case-insensitively |
| Number | A whole number |
| Text, Hotkey | Anything |
| Secret | Write-only, and never through a tool |

A choice row can declare that values outside its list are legitimate — the model row does,
because an endpoint d47 has never seen still has model names. Every other choice row refuses
what it cannot offer, and says what it can:

```text
'chatty' is not a valid Theme. Expected one of: elite, dark, light, guardian, elite-palette.
```

Clearing is always legal: it restores the default, which is what the placeholder has been
advertising all along.
