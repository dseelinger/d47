# Phase 33 — The Commander's log

The plan of record for list.md Phase 33. Written 2026-08-18, before any code, with Phase 32 merged
the same day.

`list.md` reads top to bottom as a description of the product. This is the order the work happens
in, and the reasoning the order cannot carry on its own.

---

## The phase in one sentence

Every previous phase either kept something on this machine or said something out loud and let it go;
this one produces **a file the Commander takes away** — and the whole difficulty is that a model
handed a journal will write a better story than the one that happened.

## What already exists to build on

- **`SessionSummary` already folds a session into totals**, and it does it from figures Elite
  reported rather than from a price table. It is the shape the digest generalises: the same fold,
  over an arbitrary window rather than since the last `LoadGame`.
- **`ILlmProvider` is the seam and Core owns it.** A log is a one-shot completion, not a turn, so it
  drives the provider directly rather than going through `TurnLoop` — which owns conversation
  history, a tool profile and a cache prefix that a 40-kilobyte essay has no business disturbing.
- **`PromptAssembly` puts guardrails above the persona and makes them unsettable.** A log is d47
  speaking at length, which is precisely the case item 3 has in mind, and the existing ordering
  means a persona-voiced log cannot strip them.
- **`SpendLedger` already answers "what has this cost this month"** and takes a stamped row per
  charge. Item 4 asks for exactly that and for nothing new.
- **`PriceTable` prices a model or refuses to**, and `PriceTable.Free` is what a loopback endpoint
  gets. Both matter here, because this is the one request whose price is worth quoting in advance.
- **`HabitMiner` proved the batch walk over the corpus is affordable** — 697,787 events in 3.6
  seconds — so reading a week of journals to build a digest was never the expensive half.
- **`SettingRow.Press` on an `Info` row is refused by `SettingsService.Apply`**, which is how Phases
  31 and 32 kept an expensive local action off the tool surface. The same mechanism is used again.

## Order of work

1. **The range** — what span a log covers, and how "the last session" becomes two instants.
2. **The digest** — the structured summary of facts, each carrying the events behind it. Everything
   downstream reads it, and it is the whole of item 2's defence.
3. **The voices** — three of them, and the persona protection that governs two.
4. **The estimate** — before a byte is sent, because item 4 is about consent rather than about
   accounting.
5. **The writer** — one request, one file, one ledger row.
6. **The audit** — every sentence checked against the facts it claimed, after the fact and in the
   file.
7. **The capability and the panel** — reachable, readable, and unreachable from the model.

---

## Decisions taken before the code

### The model is handed facts and never a journal

This is item 2 and it is the phase. The generator receives a `LogDigest`: a numbered list of
`LogFact`s, each one a statement d47 computed, with the journal events that support it recorded
beside it. There is no path by which a journal line reaches the prompt.

Three consequences follow and each is load-bearing.

**Facts are aggregated, not transcribed.** Forty-two `FSDJump` events are one fact — *42 jumps,
1,204 ly, Sol to Shinrarta Dezhra* — citing all forty-two. Individual facts are reserved for things
that happened once and matter: a death, a first discovery, an interdiction, an engineer visit, a
ship bought, a rank earned, a sale over a threshold. This is what keeps the largest request d47 makes
bounded — a week of heavy play is a few hundred facts, not two hundred thousand events — and it is
also the anti-embellishment mechanism, because a model given *docked at Jameson Memorial, 20:14*
cannot promote it to a narrow escape without inventing a fact that is not in the list.

**No in-game text of any kind is in the digest.** `ReceiveText` and `SendText` are excluded outright
rather than filtered. Those are the untrusted-input channel architecture.md §7 names, this is the
largest prompt d47 ever assembles, and a phase whose output the Commander posts somewhere is the
worst possible place to discover that a hostile message travelled. Names Elite reports — systems,
stations, ships, factions, missions — do reach the prompt, because a log without them is not a log;
they arrive as digest fields below the cache breakpoint, labelled journal-derived, the same
treatment `PromptAssembly.LiveGameState` already gets.

**Facts are capped, and the cap says so.** 150 facts, and a digest that would exceed it rolls the
remainder into its totals and states in words that it did — because a log silently written from a
third of a week is the same class of lie as a habit claimed without a count.

### Every sentence cites, and the file keeps the citations

The prompt requires each sentence to end with the bracketed ids of the facts behind it — `[7]`, or
`[7,12]`. After the reply lands, `LogAudit` splits the prose and checks all of it: a sentence with no
marker is **uncited**, a marker naming an id the digest does not contain is **unknown**, and both are
bugs of the kind item 2 exists to prevent.

**The markers stay in the written file.** The obvious kindness is to strip them so the log reads
cleanly, and it is wrong: item 2 says the output records which events it drew on, and a Sources
section that no sentence points into records nothing. The header says the brackets are references
and can be deleted before posting — the item's own framing is that this file is *kept and edited*,
and deleting fifteen bracketed numbers is ten seconds of the editing it already expects.

**A failed audit flags the file rather than discarding it.** An untraceable sentence is marked
`[unsupported]` where it stands, counted in the header, and listed at the foot. Throwing the whole
log away would spend the Commander's money and hand back nothing, and quietly keeping it would be the
bug. **There is no second request** — a retry doubles the cost of the largest thing d47 buys, on the
Commander's card, to fix a fault they can see and edit in a text editor.

### Three voices, and only two of them are d47's to protect

The item names two; the third was asked for while this plan was being written and it is the
interesting one.

| Voice | Who writes | Persona |
|---|---|---|
| `first-person` | The Commander, in their own words | none — this is the plain one, and the shipped default |
| `ships-ai` | The persona, writing about the Commander | required |
| `first-person-with-commentary` | The Commander, with the persona interjecting | required for the interjections only |

Item 3's protection applies to the second and third: **a log is d47 speaking at length, so a
personality switched off writes plainly rather than writing as somebody else.** With
`llm.personalityEnabled` false, `ships-ai` degrades to plain third-person reportage and
`first-person-with-commentary` degrades to `first-person` — and in both cases the file's header says
which voice was asked for and which was used, because a Commander who chose a narrator and got a
report deserves to be told rather than to wonder.

The default is `first-person`. Item 3 says the shipped default is the plain one, and the plain one is
the Commander's own voice: the ship's-AI log is the thing only d47 can do, and a thing only d47 can
do is a thing to opt into, not a thing to have happen to you the first time you press a button.

### It reuses the conversation model, and quotes the price in advance

No `logbook.model` row. The model that answers in the cockpit writes the log, which means the price
the Commander is quoted is the price they already understand, and there is one fewer setting to be
wrong about. A Commander who wants prose from a different model changes `llm.model`, which is one
row they already know.

The estimate is arithmetic and it shows it: the digest's characters over four for input tokens, the
length preset for output tokens, `PriceTable.For` for the rate. Three outcomes and all three are
sayable — a figure, *free, because that endpoint is on this machine*, or *I cannot price this model*,
which is the honest answer `PriceTable` already gives everywhere else rather than a zero that
reassures.

Length is a setting because the estimate is meaningless without one:

| `logbook.length` | Output budget | Reads as |
|---|---|---|
| `brief` | 700 tokens | a few paragraphs |
| `standard` | 1,800 tokens | the default, a page |
| `full` | 4,500 tokens | the long write-up |

### Nothing is written until asked, and asking is two steps

Item 4's sentence — *a Commander who set a monthly cap and found it eaten by an unrequested essay
about their Tuesday would be right to be angry* — is met by there being no path that spends money
without a person having seen the figure first.

`estimate_log` builds the digest, prices it, says so, and **arms** the writer. `write_log` refuses
unless an estimate is armed, and spends the arming when it runs. The panel shows the same figure on
the same button. The keyword router reaches both with argument-free whole-utterance phrases, so
*write my commander's log* quotes a price and *write it* is the second act rather than the first.

Both tools are `ToolDefinition.Protected`, and so is `list_logs`. This is the same reasoning Phase 32
recorded one step further along: the model is the component that reads untrusted text, and it is not
the component that gets to spend the largest single request d47 makes. It also costs zero advertised
bytes, which matters — Phase 31 shipped the SRV profile at 39,914 against `ToolProfiles.
ComfortableBytes` of 40,000 and said the next phase wanting an advertised tool does the
deferred-loading work first. This phase does not want one.

### The range is a session, a preset, or two dates

Item 1 says *a session — or a week*, and both halves ship.

- **The session** resolves by scanning backwards for the last `LoadGame` and taking its timestamp as
  the start. Not the last file: Elite rolls a long session into a continuation journal without
  re-emitting `LoadGame`, and `SessionSummary` already records that this is the trap.
- **Presets** — today, the last 7 days, the last 30 days — are what the router phrases carry, because
  the router's grammar is closed and a date spoken aloud is exactly the argument extraction it
  refuses to do.
- **Two dates** are reachable from the panel and from the tool's optional `from`/`to` parameters,
  which only the panel and the router can reach because the tool is Protected.

Files are pre-filtered on the timestamp in their own name before a line is read, so a one-day log
does not walk thirteen months. Elite's filenames are the session start, so the file *before* the
window's start is read too — a session that began at 23:50 and ran past midnight is in it.

### It writes to `data/commander-log/`, and not to `data/logs/`

`AppPaths.Logs` is already `data/logs` and holds d47's diagnostic logs. Two different things called
"logs" one folder apart is a support question waiting to be asked, so the Commander's log gets
`data/commander-log/` and one file per run, named `2026-08-18-session.md`. Markdown, because item 1
says plain markdown so it can be kept, edited and posted somewhere.

**A run never overwrites.** A second log for the same day and range gets `-2`, because the first one
may already have been edited and this is the one artefact of d47 the Commander is expected to own.

## The acceptance read, 2026-08-18

Three real requests against `claude-opus-5`, over one real session — 1,435 events, 22 h 57 m,
2026-08-17 20:27 to 2026-08-18 15:25 — read against that session's events by hand. Six cents.

**The dry pass found three defects before a byte was sent**, which is the argument for the probe
having a mode that spends nothing:

- **The closing line cited an event that never happened.** `Finished docked at BNH-T2F` carried a
  fabricated `LogSource("Location", lastEventSeen)`. A citation naming an event that does not exist
  is the precise failure `LogAudit` exists to catch, arriving from the one side the audit cannot
  see — it checks the prose against the facts, and takes the facts' own provenance on trust. It now
  records the event that actually established the place.
- **One evening at an engineer produced 113 `EngineerCraft` events, 25 near-identical facts and 88
  dropped.** A whole session crowded out by one bench. Rolled up per module, blueprint and engineer,
  the same evening is three facts that each say what was worked on, how many rolls it took and where
  it finished — and `FactsDropped` went from 88 to 0.
- **Ship names arrived as `anaconda` and `corsair`.** `Named` where `Spoken` was meant, which is the
  distinction `JournalJson` documents at length; one log would have called it an Anaconda in one line
  and an anaconda in the next.

**The hand-read of the first-person log: no fabricated events.** Fifteen sentences, thirteen traced,
and every one of the thirteen checked against the journal — the twelve ship swaps in the right order
with the Panther Clipper appearing only in the second pass, 65/16/32 rolls to grade 5 on the three
mounts, 470 ly across eight jumps, six dockings at three stations. The two flagged sentences were
editorial rather than false: *Most of the time went on the hangar* and *The real work was at Tod
McQuinn's*.

**One clause got through, and it is worth recording as the mechanism's limit.** *Two hundred and
forty-three units of material picked up along the way, which is roughly what that many rolls costs
[20,24]* — the count is fact 20 and the economics is an invention riding on a cited sentence.
Citation is per sentence, so a supported clause can carry an unsupported one. This is not fixable by
checking harder at this granularity, and it is the honest boundary of the claim the file makes.

### The persona voice invents, and that is measured now rather than assumed

| Run | Voice | Sentences | Traced | Untraceable |
|---|---|---|---|---|
| 1 | first person | 15 | 13 | **2** — both editorial |
| 2 | ship's AI, as first written | 27 | 13 | **14** |
| 3 | ship's AI, instruction tightened | 23 | 14 | **9** |

Run 2 invented **another Guardian core** (*Cora would have called that sloppy*), **its own downtime**
(*There were stretches in the twenty-three hours where I was not running*), and **the state of the
hold** (*weapons finished, hold stocked, hull whole*). Character is the thing a model reaches for
extra material to fill, so run 3 named those three inventions and refused them by name.

**It helped and it did not fix it.** Untraceable sentences fell from 14 to 9 — and Cora came back
anyway, twice, in a run that had been told not to invent a companion. That is the whole argument of
item 2 arriving as evidence rather than as a position: **an instruction is not a quality bar, and
this is why the phase is not simply a prompt.** The nine are marked in the file, counted in its
header and listed at its foot, which is the outcome the item actually asks for.

Two further things the read turned up, both fixed and neither verified against a live model:

- **`1 launch or loss` read as a launch.** Launches and losses were one counter, so a Commander who
  put an SRV down a crater was told they had used one. Two counters now, and a loss is a mishap.
- **The persona wrote *took him back* and *He finished docked*** about a real person the facts never
  described. The instruction block now says to use *they* unless the facts say otherwise, and that a
  name is not evidence.

**The estimate over-quotes, which is the safe direction.** About $0.05 quoted against $0.02, $0.04
and $0.03 actual — the budget is a ceiling on output tokens and the model used less of it than the
quote assumed. A Commander is never charged more than they agreed to.

## Acceptance

- A log generated from a real corpus session, read against that session's events by hand, once. **Done
  — three runs, recorded above.**
- Every sentence that claims to trace does trace, every fact to journal events that exist, and every
  sentence that does not is marked. **Done**; the residual limit is the clause-inside-a-cited-sentence
  recorded above.
- A digest built over a window contains no `ReceiveText` or `SendText` content, by test.
- The estimate is quoted before anything is sent, and the actual spend lands in `SpendLedger`.
- `write_log` refuses when nothing is armed.
- `llm.personalityEnabled` false degrades both persona voices and says so in the file.
- The default `D47Settings` has voice `first-person`.
- No advertised tool byte count moves — `ToolProfileTests`, unchanged.
