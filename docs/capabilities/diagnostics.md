---
title: Diagnostics
---

# Diagnostics

**Group:** Foundation
**Capability id:** `diagnostics`

Reports where D47 keeps its files, and changes how much any one subsystem logs — without a
restart.

This is the capability that needs nothing: no game running, no model configured, no audio
device, no headset. That is why it is the first one, and it is the one to reach for when
something else is misbehaving.

## Try it

> "what's your status"
> "turn journal logging up to debug"
> "set voice logging back to information"

## Tools

### `get_app_status`

Reports the version, where writable files live, and the current level of every subsystem.
Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Real output, from an install at `C:\Tools\d47`:

```text
d47 0.1.0
Installed at: C:\Tools\d47
Writable data: C:\Tools\d47\data
Logs: C:\Tools\d47\data\logs
Log levels:
  App: Information
  Capabilities: Information
  Settings: Information
  Journal: Information
  Llm: Information
  Voice: Information
  Vr: Information
  Input: Information
```

### `set_log_verbosity`

Changes one subsystem's minimum level. Serilog reads the level switch on every event, so
the change applies to the next log line — there is no restart and no config reload.

```json
{"type":"object","properties":{"level":{"type":"string","description":"The new minimum level. Trace is the most detailed; None silences the subsystem.","enum":["Trace","Debug","Information","Warning","Error","Critical","None"]},"subsystem":{"type":"string","description":"Which subsystem to change the log level for.","enum":["App","Capabilities","Settings","Journal","Llm","Voice","Vr","Input"]}},"required":["level","subsystem"],"additionalProperties":false}
```

On success:

```text
Journal logging is now at Trace.
```

Both arguments are closed vocabularies, declared once and emitted into the schema as
`enum`. They are checked before the handler runs, so an invented subsystem is refused with
the real list rather than silently ignored:

```text
'Telepathy' is not a valid subsystem. Expected one of: App, Capabilities, Settings, Journal, Llm, Voice, Vr, Input.
```

## Subsystems

Each subsystem is a separate log target with its own level. The set is closed: the tool
schema, the settings rows and the model-free keyword router all read from one list, so there
is no second table to keep in step.

| Subsystem | Covers |
|---|---|
| `App` | Startup, composition, the window |
| `Capabilities` | The registry and tool dispatch |
| `Settings` | Settings and secret stores |
| `Journal` | Journal and `Status.json` reading |
| `Llm` | Model calls and token accounting |
| `Voice` | Capture, transcription, synthesis, the audio arbiter |
| `Vr` | OpenVR overlays and texture submission |
| `Input` | Key injection and binds parsing |

## Levels

`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. `Trace` is the most
detailed; `None` silences the subsystem entirely.

Levels are written to both sinks — `d47-<date>.log` for reading and `d47-<date>.jsonl` for
parsing. Turning a subsystem up to `Trace` affects both.

## Settings

| Row | What it does |
|---|---|
| Default log level | Applies to any subsystem without its own level |
| *&lt;Subsystem&gt;* log level | One row per subsystem, offering the same levels |

Every path writes the same settings row — the panel, the tool call, and a hand-edited
`settings.json` — so a level change is live on the next log line *and* still there after a
restart. There is no second, unsaved kind of change.
