---
title: Journal
---

# Journal

**Group:** Foundation
**Capability id:** `journal`

Reports the current Commander's star system, body and docking state, read straight from the
Elite Dangerous journal. This is the first capability the journal spine makes possible:
d47 tails the newest journal file, tracks per-Commander state from it, and this capability is
just an answer projected out of that state — no game, model or network access needed to
demonstrate it.

## Try it

> "where am I"
> "what system is this"
> "am I docked"

## Tool

### `get_location`

Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Before any journal has been found — a fresh install, or Elite has never run:

```text
No Elite Dangerous journal has been detected yet.
```

That is the true answer whenever it applies, not a fallback: this capability, like every
other one, is a state to read rather than a failure to guard against (architecture.md,
*Capabilities as state, not guard*).

In transit:

```text
Fixture One is in Fixture Nebula Point, near Fixture Nebula Point A.
```

Docked:

```text
Fixture One is in Fixture Nebula Point, near Fixture Nebula Point A, docked at Fixture Outpost.
```

## Where the answer comes from

d47 watches the journal folder Elite writes to
(`%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous` by default; overridable for
development with the `D47_JOURNAL_DIR` environment variable) and always tails the newest file
by filename, not by file modification time — the filename already encodes the session start
time, and that is what survives being copied.

Reading is pull-based: nothing in d47 owns a background thread or a timer for this. The same
file-reading code that runs against a live game also runs against a recorded session replayed
as fast as a test can call it, which is what makes journal behaviour testable without Elite,
a headset or any other hardware.

## Multiple Commanders

State is kept per Commander, keyed by their Frontier ID from the journal's own `Commander`
event. A second Commander's session on the same machine gets its own bucket — it is never
merged into the first Commander's location, and, as the same mechanism extends to cover them
in a later phase, the same is true of fleet and materials.

## Surviving a journal schema change

A journal line that is not valid JSON, or has no `event` field, is logged and skipped without
stopping the rest of the file from being read. An event type d47 does not yet recognise still
parses and is logged — it simply has no effect on this capability's answer until d47 is taught
what it means. Elite adds and changes journal events several times a year; this is what keeps
that a non-event.
