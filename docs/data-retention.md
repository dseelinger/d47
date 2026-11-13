---
title: Data retention
group: General help
nav_order: 10
---

<!--
  The How it works band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Files on your disk follow limits the code enforces. A donation is the only data held for you elsewhere.</p>
<section>
<h2><span class="num">1</span> Your files stay in the data folder.</h2>
<svg viewBox="0 0 880 140" role="img" aria-label="Files stay beside d47.exe">
 <rect x="20" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="218" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">data\</text>
 <text x="218" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">beside d47.exe</text>
 <line x1="424" y1="70" x2="440" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="454,70 438,62 438,78" fill="var(--accent-muted)"/>
 <rect x="464" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="662" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">YOUR MACHINE</text>
 <text x="662" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">sent nowhere</text>
</svg>
<p class="body">Logs, memories, clips and settings are files you can delete. d47 sends none of them anywhere.</p>
</section>
<section>
<h2><span class="num">2</span> Each file has a limit, and code enforces it.</h2>
<svg viewBox="0 0 880 140" role="img" aria-label="A limit and what enforces it">
 <rect x="20" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="218" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">THE LOG</text>
 <text x="218" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">one file per day</text>
 <line x1="424" y1="70" x2="440" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="454,70 438,62 438,78" fill="var(--accent-muted)"/>
 <rect x="464" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="662" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">LoggingSetup</text>
 <text x="662" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">enforces the limit</text>
</svg>
<p class="body">The readable log keeps a fixed number of days and a fixed size for each day. The table below gives each limit and what enforces it.</p>
</section>
<section>
<h2><span class="num">3</span> A donation is held until the rule says.</h2>
<svg viewBox="0 0 880 140" role="img" aria-label="A donation and its store">
 <rect x="20" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="218" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">A DONATION</text>
 <text x="218" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">sent when you press</text>
 <line x1="424" y1="70" x2="440" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="454,70 438,62 438,78" fill="var(--accent-muted)"/>
 <rect x="464" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="662" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">THE STORE</text>
 <text x="662" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">excerpts expire</text>
</svg>
<p class="body">An excerpt expires on a lifecycle rule. A journal history is kept until the donor asks for it back, which the donation privacy notice explains.</p>
</section>
<section>
<h2><span class="num">4</span> The limits are public constants.</h2>
<svg viewBox="0 0 880 140" role="img" aria-label="Limits sit in constants">
 <rect x="20" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="218" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">A CONSTANT</text>
 <text x="218" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">in LoggingSetup</text>
 <line x1="424" y1="70" x2="440" y2="70" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="454,70 438,62 438,78" fill="var(--accent-muted)"/>
 <rect x="464" y="22" width="396" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="662" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">A TEST</text>
 <text x="662" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">fails if the page differs</text>
</svg>
<p class="body">The page and the code agree: <code>public static readonly TimeSpan ReadableLogLife = TimeSpan.FromDays(90);</code> in <code>LoggingSetup</code>, with <code>TheRetentionPolicyTellsTheTruthTests</code> checking the page.</p>
</section>
</div></div>

**Nothing is kept longer than the purpose it was taken for.** That is the whole policy; everything
below is that sentence applied, with the number and the thing that enforces it, so no rule here is
one somebody has to remember to apply.

Every number on this page is checked against the code by a test
(`TheRetentionPolicyTellsTheTruthTests`). Changing the log, memory, recorder or excerpt limit without changing this page fails the
gate tests, which is what makes this a policy rather than a description of one that may have been
true once.

## Two halves, and only one of them is a promise to anybody else

**Almost everything d47 keeps is yours.** It goes in `data\` beside
`d47.exe`, d47 sends none of the files anywhere, and deleting it is deleting a file. Words you say
or hear pass to the speech and voice providers you chose, as the
[Privacy and egress](capabilities/privacy.html) page lists. There is nothing to ask
anybody for, because nobody else has the files. Elite's own journals are not d47's at all — Frontier
writes them, d47 reads them, and how long they sit on your disk is between you and the game.

**The other half is the part that needs a policy in the ordinary sense.** A donation can be sent to a store this project
runs, so there is data here that belongs to somebody else. Who holds it and on what basis is the
[donation privacy notice](donation-privacy.html); how long is the second table below.

## On your own machine

| What | Where | Kept | What enforces it |
|---|---|---|---|
| The readable log | `data\logs\d47-*.log` | **90 days**, and at most **4 MB** of any one day | `LoggingSetup` |
| What d47 remembers about you | `data\memories.json` | **90 days** by default — the row says *Three months* — and *Never* is one of the choices | `MemoryStore.Expire`, on the tick |
| Audio recorder clips | `data\flight\` | a rolling **200 MB**, oldest evicted first on every write | `RecordingLog` |
| Recordings you pressed *keep* on | `data\flight\kept\` | **until the wipe**, and eviction never reaches them | `RecordingLog`, deliberately |
| Input traces and their stills | `data\flight\input-traces\` | **kept until you delete it**, and only written on a run you started with `--trace-input` | nothing — it is a diagnostic you asked for |
| A downloaded update | `data\updates\` | **until the first start after it installs** | `UpdateInstaller` |
| Your own copies of what you donated | `data\donations\` | **kept until you delete it** | nothing — it is your receipt |
| Everything else in `data\` — settings, secrets, the checklist, your ships, the spend ledger, your commander log, your own cues, and the Features record if you started d47 with `D47_COVERAGE=1` | `data\`, one file or folder each | **kept until you delete it** | nothing |
| The signal index | `data\signals\signals.tsv.gz` | **replaced** by the next download, made when a signal search or a story chapter finds it more than 20 hours old | `SignalIndexStore` |
| The rare index | `dataaresares.tsv` | **replaced** by the next download, made when a rare is asked about and the copy is more than 3 hours old | `RareIndexStore` |
| Downloaded voice and transcription models | `data\models\` | **kept until you delete it** | nothing — and they are the vendors', not yours |
| The conversation itself | nowhere | **not kept**; it lives in memory and is gone when d47 closes | there is no file to enforce anything on |
| A picture of the screen, when the model looks | nowhere | **one turn**, in memory, and never written to disk | `TurnLoop`, which drops it when the turn is committed |

Three of those need a word.

**There is one log.** It is what a bug report quotes and what an incident excerpt cuts its log half
out of. The per-day ceiling is there because a time limit alone is
not a bound: ninety days multiplied by an unbounded day is unbounded. A day that hits the ceiling stops rather than rolling on to a second
file, so the pile cannot exceed 360 MB however badly a day goes (90 days at 4 MB). Earlier versions also wrote a
`d47-*.jsonl` copy; d47 deletes those files at startup.

**The audio recorder is the sharpest thing on this page and it is off unless you turn it on.**
It exists only while `D47_RECORD_AUDIO=1` is set, and what it holds is a rolling recording of
audio in your home: what the transcriber was handed, and what came out of the speakers. That is
more sensitive than the journal or the log, which is why
the cap is enforced by the code that writes rather than by anybody's discipline, and why it shipped
with the feature rather than after it. A row you press *keep* on is exempt from eviction — outliving
the window is what keeping means — and the wipe on the Privacy panel takes those too, because a wipe
that spared them would be a button that says deleted and is not.

**The input trace is the same shape and it takes pictures of the game.** It exists only while
`D47_TRACE_INPUT=1` is set or d47 was started with `--trace-input`, and while it is on, every key
sequence d47 sends writes a folder of its own: a line per keystroke, and a PNG of Elite's window at
the few moments a sequence marked as worth seeing. The pictures are of the game and of nothing
else, they stay on your machine like the recorder's clips, and nothing transmits them. Delete the
folder and it is gone; d47 never writes another until the next run you ask for one.

## What this project has received

Apart from the daily usage count, only what somebody deliberately donated, and only through the
one route that can send anything — the review window, one payload at a time, after reading exactly what would leave. A large journal history is sent in parts, each with its own receipt. There is no
second road for journal data: no crash reporter, and no request log at the endpoint. The one thing
sent without a press is the daily usage count, which holds tool names and numbers and nothing from a
journal.

| What | Where | Kept | What enforces it |
|---|---|---|---|
| An incident excerpt | `excerpts/<identifier>/` in the store | **30 days** | a lifecycle rule on the bucket |
| A donated journal history | `corpus/<identifier>/` in the store | **indefinitely** — see below | nothing, deliberately |
| A copy downloaded to be read | one folder on the holder's own machine | **until the object it came from leaves the store** | the utility that fetched it deletes it at the next listing that does not name it |
| Which requests were made | nowhere | **not kept** | `observability` is off in `wrangler.toml` |
| Usage counts | the `d47-usage` database | **indefinitely** | nothing, deliberately: a count carries no identifier, so no row can be traced to an installation or deleted for one |
| A Commander's web app snapshot | the `d47-web` database | **until Web app is turned off, or 90 days without an upload** | the delete D47 sends when the setting is turned off, and the Worker's scheduled handler |
| When Web app was turned off: the Frontier customer id and the time | the `d47-web` database | **90 days, or until the Commander links again or deletes the account** | the Worker's scheduled handler, and the link and account delete routes |
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

**A donated journal history, indefinitely.** It is used in two ways. It is test data, which may
be run through the same code the running app uses so a defect can be shown fixed against real play,
and a defect can be found long after the history was sent. And it is a record of what the game
writes, read by the maintainer, including through AI coding sessions, to learn the events and fields
of parts of the game d47 does not handle yet. Only event names, field names, counts and game symbols
from it are ever published; no line from it, and no other value in one. Permanent retention is also
why the anonymity has to hold rather than merely look right: a donation is scrubbed by rules for the fields d47 knows,
another player's words are dropped rather than scrubbed, crew names are replaced, and the stand-in names are deliberately not
stable between donations so two of them cannot be joined. It goes when the donor asks.

**Everything in `data\` that no rule above names.** Your settings, your checklist, your ships, your
spend history. Kept forever because they are what the app is — a checklist that expired would be a
defect — and it costs nothing to say so, because they are on your disk, they were never sent
anywhere, and deleting the file is the whole of deleting them.

## What is never kept at all

- **Another player's words.** In-game chat is dropped from a donation rather than scrubbed, because
  a donor cannot consent on somebody else's behalf. A donated log excerpt also leaves out the words of objective changes unless it includes your speech.
- **A microphone between utterances.** The audio recorder sees the gated utterance the transcriber
  was given and nothing else; the half-second ring push-to-talk runs on at rest is never written.
- **Audio in a donation.** Voice is biometric, and it is the one payload that showing it before it
  leaves cannot make safe enough to be worth it — so no excerpt and no journal history can carry any.

## Making any of it shorter

- **The memory store** has an expiry setting of its own — *Three months* out of the box, and a
  month, a year or *Never* are the other choices — and a wipe on the Privacy panel.
- **The audio recorder** has a wipe on the same panel, and stops existing the moment
  `D47_RECORD_AUDIO` is unset.
- **The logs and the snapshots** are files. Delete them; d47 writes the next one and nothing breaks.
- **A donation you have already sent** is the one thing you cannot reach yourself. The
  [donation privacy notice](donation-privacy.html) says how to have it deleted, and your receipt in
  `data\donations\` names the exact object to quote.
