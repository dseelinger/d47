---
title: Data retention
group: General help
nav_order: 6
---

**Nothing is kept longer than the purpose it was taken for.** That is the whole policy; everything
below is that sentence applied, with the number and the thing that enforces it, so no rule here is
one somebody has to remember to apply.

Every number on this page is checked against the code by a test
(`TheRetentionPolicyTellsTheTruthTests`). Changing a limit without changing this page fails the
build, which is what makes this a policy rather than a description of one that may have been true
once.

## Two halves, and only one of them is a promise to anybody else

**Almost everything d47 keeps is yours and never leaves your machine.** It goes in `data\` beside
`d47.exe`, nothing transmits it, and deleting it is deleting a file. There is nothing to ask
anybody for, because nobody else has it. Elite's own journals are not d47's at all — Frontier
writes them, d47 reads them, and how long they sit on your disk is between you and the game.

**The other half is the part that needs a policy in the ordinary sense.** Since
[#175](https://github.com/dseelinger/d47/issues/175) a donation can be sent to a store this project
runs, so there is data here that belongs to somebody else. Who holds it and on what basis is the
[donation privacy notice](donation-privacy.html); how long is the second table below.

## On your own machine

| What | Where | Kept | What enforces it |
|---|---|---|---|
| The readable log | `data\logs\d47-*.log` | **90 days**, and at most **4 MB** of any one day | `LoggingSetup` |
| The machine-parsing log | `data\logs\d47-*.jsonl` | **14 days**, and at most **4 MB** of any one day | `LoggingSetup` |
| What d47 remembers about you | `data\memories.json` | **90 days** by default — the row says *Three months* — and *Never* is one of the choices | `MemoryStore.Expire`, on the tick |
| Audio flight recorder clips | `data\flight\` | a rolling **200 MB**, oldest evicted first on every write | `FlightLog` |
| Flight rows you pressed *keep* on | `data\flight\kept\` | **until the wipe**, and eviction never reaches them | `FlightLog`, deliberately |
| Snapshots of `data\` | `data\backups\*.zip` | **the last 10 deploys**, one per deploy | `tools/data-backup.ps1` |
| A downloaded update | `data\updates\` | **until the first start after it installs** | `UpdateInstaller` |
| Your own copies of what you donated | `data\donations\` | **kept until you delete it** | nothing — it is your receipt |
| Everything else in `data\` — settings, secrets, the checklist, your ships, the spend ledger, your commander log, your own cues, and the coverage record if you switched it on | `data\`, one file or folder each | **kept until you delete it** | nothing |
| Downloaded voice and transcription models | `data\models\` | **kept until you delete it** | nothing — and they are the vendors', not yours |
| The conversation itself | nowhere | **not kept**; it lives in memory and is gone when d47 closes | there is no file to enforce anything on |

Three of those want a word.

**The two logs are asymmetric on purpose.** The readable one is what a bug report quotes and what
an incident excerpt cuts its log half out of, so its reach is worth buying; the JSON one is 63% of
the bytes the two hold between them and nobody reads it. Ninety days of the readable log is about
16 MB. The per-day ceiling is there because a time limit alone is not a bound — d47 has had a day
with a runaway loop in it, and ninety days multiplied by an unbounded day is unbounded. A day that
hits the ceiling stops rather than rolling on to a second file, so the pile cannot exceed 360 MB of
`.log` and 56 MB of `.jsonl` however badly a day goes.

**The flight recorder is the sharpest thing on this page and it is off unless you turn it on.**
It exists only while `D47_FLIGHT_RECORDER=1` is set, and what it holds is a rolling recording of
audio in your home: what the transcriber was handed, and what came out of the speakers. That is
more sensitive than the journal, the log, or anything else d47 has ever written down, which is why
the cap is enforced by the code that writes rather than by anybody's discipline, and why it shipped
with the feature rather than after it. A row you press *keep* on is exempt from eviction — outliving
the window is what keeping means — and the wipe on the Privacy panel takes those too, because a wipe
that spared them would be a button that says deleted and is not.

**`models\`, `logs\`, `flight\` and `updates\` are left out of the snapshots.** 1,064 MB of a
1,072 MB install is downloadable models that are identical across versions, and ten snapshots
holding them would be ten gigabytes to protect eight megabytes. `audio\` is kept, because the cues
and beds in it are yours and nothing else holds a copy.

## What this project has received

Only what somebody deliberately donated, and only through the one route that can send anything —
the review window, one payload at a time, after reading exactly what would leave. There is no
second road: no crash reporter, no analytics, no request log at the endpoint.

| What | Where | Kept | What enforces it |
|---|---|---|---|
| An incident excerpt | `excerpts/<identifier>/` in the store | **30 days** | a lifecycle rule on the bucket |
| A donated journal history | `corpus/<identifier>/` in the store | **indefinitely** — see below | nothing, deliberately |
| Which requests were made | nowhere | **not kept** | `observability` is off in `wrangler.toml` |
| Who donated | nowhere | **not kept** | there is no account, no email and no directory of identifiers — a donation carries a random per-installation token and nothing else |

**Nothing donated has been committed to this repository, and a journal history never will be.** A
committed fixture cannot be erased without rewriting the history of a public repository, which is
not a promise anybody can keep, so the corpus route stores objects and commits nothing. Whether an
excerpt may ever become a committed replay fixture is
[#167](https://github.com/dseelinger/d47/issues/167)'s to settle, and its own rule is that it is
settled **before** the first one lands rather than after.

**The 30 days is the rule; closing the defect sooner is a practice.** An excerpt exists to fix one
thing, and once that is fixed the copy in the store is spare. It is deleted when that happens — but
what *enforces* an excerpt's disappearance is the lifecycle rule and the calendar, and this page
says so rather than claiming a person's habit as a mechanism.

## Forever, and why

Two things are kept with no end date. Both are deliberate, and saying so is harder than leaving
them off a table, which is exactly why they are on one.

**A donated journal history, indefinitely.** That is the whole point of it. It becomes a replay
case that `spike/CorpusReplay` drives through the same fold the running app uses, so a defect can
be proven fixed against play that really happened — and a regression case that expires stops being
one. Permanent retention is also what makes the anonymity load-bearing rather than decorative: a
donation is scrubbed by field list, another player's words are dropped rather than scrubbed, and
the stand-in names are deliberately not stable between donations so two of them cannot be joined.
It goes when the donor asks.

**Everything in `data\` that no rule above names.** Your settings, your checklist, your ships, your
spend history. Kept forever because they are what the app is — a checklist that expired would be a
defect — and it costs nothing to say so, because they are on your disk, they were never sent
anywhere, and deleting the file is the whole of deleting them.

## What is never kept at all

- **Another player's words.** In-game chat is dropped from a donation rather than scrubbed, because
  a donor cannot consent on somebody else's behalf.
- **A microphone between utterances.** The flight recorder sees the gated utterance the transcriber
  was given and nothing else; the half-second ring push-to-talk runs on at rest is never written.
- **Audio in a donation.** Voice is biometric, and it is the one payload that showing it before it
  leaves cannot make safe enough to be worth it — so no excerpt and no journal history has ever
  carried any.
- **Telemetry, of any kind, anywhere.** No crash reporter, no analytics, no metrics endpoint, and
  no log of who reached the donation endpoint or when.

## Making any of it shorter

- **The memory store** has an expiry setting of its own — *Three months* out of the box, and a
  month, a year or *Never* are the other choices — and a wipe on the Privacy panel.
- **The flight recorder** has a wipe on the same panel, and stops existing the moment
  `D47_FLIGHT_RECORDER` is unset.
- **The logs and the snapshots** are files. Delete them; d47 writes the next one and nothing breaks.
- **A donation you have already sent** is the one thing you cannot reach yourself. The
  [donation privacy notice](donation-privacy.html) says how to have it deleted, and your receipt in
  `data\donations\` names the exact object to quote.
