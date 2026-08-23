---
title: Commander's log
group: Conversation
nav_order: 141
---

Directive 47 can turn a session — or a week — into a readable log, written to plain markdown beside
the executable so you can keep it, edit it, and post it wherever you like.

Elite's community has been writing Commander's logs by hand since 3300. This writes the first draft.

## The one thing that leaves this machine

Everything else d47 does stays here. This does not — **because you chose to take it.** That is a
different thing from telemetry, and it is worth saying plainly: d47 sends nothing anywhere on its
own, and a log leaves only when you pick up the file and put it somewhere.

## Every sentence traces to an event

This is the whole quality bar, and it is why the feature is not simply a prompt.

A model handed your journal will write a better evening than the one you had. It will promote a
routine docking into a narrow escape, because that reads better and nothing stopped it. So the model
is **never handed your journal.** It is handed a numbered list of facts d47 computed:

```
[3] (travel) 42 hyperspace jump(s), 1,204 ly in total, Shinrarta Dezhra out to Deciat.
[4] (mishap) Was interdicted by Ordo Vulpes, and submitted.
[5] (trade) Sold 640 tonnes across 9 sale(s) for 12,410,000 Cr, mostly Painite.
```

Every sentence it writes has to end with the numbers of the facts it rests on — `[4]`, or `[4,11]`.
When the log comes back, d47 reads it again and checks all of it. A sentence citing nothing came from
somewhere other than your journal; a sentence citing a fact that does not exist is worse, because it
looks like evidence. Both are **marked in the file where they stand**, counted in the header, and
listed at the foot.

The brackets stay in the finished file on purpose. A Sources section that no sentence points into
records nothing. Delete them before you post it — that is ten seconds of the editing this file
already expects.

Nothing you or anybody else typed in game is ever part of it. `ReceiveText` and `SendText` have no
handler at all, so a hostile message in an open system cannot travel into the largest prompt d47
assembles, or out of it into something you posted.

## Whose log it is
{: #voice }

Three voices, and they are not interchangeable.

| Setting | What it writes |
|---|---|
| **You write it, in your own words** | Your own account, first person. The shipped default, and the plain one. |
| **D47 writes about you** | The ship's AI, in the personality you have chosen, writing about your flying. |
| **You write, D47 chips in** | Your account, with D47 interjecting a handful of times. |

The second is the one only d47 can do, which is exactly why it is the one you opt into rather than
the one that happens to you the first time you press a button.

**A log is d47 speaking at length, so it inherits the persona's protection.** With personality
switched off, the ship's-AI voice writes plainly rather than writing as somebody else, and the
commentary voice loses its commentary. Either way the file's header says which voice you asked for
and which one actually wrote it.

## What a log covers
{: #range }

The last session, today, the last seven days, or the last thirty. A session means **since you last
entered the game** — not since the journal file started, because Elite rolls a long session into a
continuation file without saying so.

Two exact dates are available from the panel. They are not available by voice, and that is
deliberate: the phrase router matches whole phrases and extracts nothing from them, which is what
stops it guessing at a date and confidently writing up the wrong fortnight.

## It costs money, and says so first
{: #cost }

Prose over a long session is the largest single request d47 will ever make. So:

- **It never happens by itself.** There is no schedule, no trigger, no callout that starts one.
- **It quotes you first.** Working out the cost reads your journals on this machine and sends
  nothing.
- **It reports what it actually cost** afterwards, through the same spend ledger as everything else,
  so `what has this cost this month` includes it.

Asking is two steps, and they are separate on purpose:

> **you:** write my commander's log
>
> **D47:** A log of the last session: 34 things I can account for, out of 4,812 events in 2 journal
> file(s). Writing it would cost about $0.04 — about 2,100 tokens in and up to 1,800 tokens of prose
> back, through claude-opus-5. Say "write the log" and I will.
>
> **you:** write the log

A Commander who set a monthly cap and found it eaten by an unrequested essay about their Tuesday
would be right to be angry. Nothing here can do that.

How long a log runs is a setting — brief, standard or full — because the output budget is most of
what that figure is pricing.

## Writing one
{: #writing }

Open **Commander's log** in the panel, or say:

> write my commander's log

Files land in `data/commander-log/` as markdown, named for the day and the span. **A run never
overwrites one**: the first may already have been edited, and this is the one file in `data/` that
d47 does not consider its own.

## Tools

Every tool here is protected — the panel, a hotkey and the phrase router reach them, and the
language model does not. Phase 32 kept its tools away from the model because the model is the
component that reads untrusted text. The same reasoning carries one step further here: the model is
also not the component that gets to authorise the largest request d47 makes.

### `estimate_log`

Reads the window, prices it, and gets ready. Spends nothing.

```json
{"type":"object","properties":{"range":{"type":"string","description":"What span to cover. Omit for whatever the settings say.","enum":["session","today","week","month"]}},"required":[],"additionalProperties":false}
```

### `write_log`

Writes the log that was just quoted. Refuses unless a quote has been given, and consumes the quote
whether it succeeds or not — a second attempt is a second spend, and gets a second figure.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `list_logs`

What has been written, and where.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```
