---
title: Diagnostics
group: Foundation
nav_order: 101
---

Where Directive 47 keeps its files, and how much detail it writes down about what it is doing.

This is the one to reach for when something else is misbehaving. It needs nothing else to be
working — no game running, no model configured, no microphone, no headset — so it can still
answer when nothing else can.

## Ask for it

> "what's your status"
> "turn journal logging up to debug"
> "set voice logging back to information"

## What it tells you

```text
Directive 47 0.1.0
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

The version, where everything writable lives, and how loud each part of the app currently is.
The paths are the useful part when you are looking for a log to send with a bug report.

## Turning the detail up

Ask for more detail from one part of the app and it takes effect on the next line written —
no restart, and nothing to reload:

> "turn journal logging up to debug"

```text
Journal logging is now at Trace.
```

Ask for a part that does not exist and you are told what does, rather than nothing happening:

```text
'Telepathy' is not a valid subsystem. Expected one of: App, Capabilities, Settings, Journal, Llm, Voice, Vr, Input.
```

### The parts you can turn up

| Part | Covers |
|---|---|
| `App` | Startup, composition, the window |
| `Capabilities` | The registry and tool dispatch |
| `Settings` | Settings and secret stores |
| `Journal` | Journal and `Status.json` reading |
| `Llm` | Model calls and token accounting |
| `Voice` | Capture, transcription, synthesis, the audio arbiter |
| `Vr` | OpenVR overlays and texture submission |
| `Input` | Key injection and binds parsing |

### How loud

`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. `Trace` is everything;
`None` silences that part entirely.

Both log files get the same detail — `d47-<date>.log` to read and `d47-<date>.jsonl` to search
or hand to something that parses JSON. Turning a part up to `Trace` affects both, and `Trace`
grows a log file quickly, so it is worth turning back down once you have what you needed.

## Settings

| Row | What it does |
|---|---|
| Default log level | Applies to any part without its own level |
| *&lt;Part&gt;* log level | One row per part, offering the same levels |

Asking out loud and changing the row do the same thing, and both survive a restart. There is no
second, temporary kind of change to keep track of.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

The model reaches this capability through two tools. The schemas below are the current ones,
quoted from the registry — the documentation gate fails the build if they drift.

### `get_app_status`

Reports the version, where writable files live, and the current level of every subsystem.
Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `set_log_verbosity`

Changes one subsystem's minimum level. Serilog reads the level switch on every event, so the
change applies to the next log line — there is no restart and no config reload.

```json
{"type":"object","properties":{"level":{"type":"string","description":"The new minimum level. Trace is the most detailed; None silences the subsystem.","enum":["Trace","Debug","Information","Warning","Error","Critical","None"]},"subsystem":{"type":"string","description":"Which subsystem to change the log level for.","enum":["App","Capabilities","Settings","Journal","Llm","Voice","Vr","Input"]}},"required":["level","subsystem"],"additionalProperties":false}
```

Both arguments are closed vocabularies, declared once and emitted into the schema as `enum`,
and checked before the handler runs — which is why an invented subsystem comes back with the
real list rather than being silently ignored.

</details>
