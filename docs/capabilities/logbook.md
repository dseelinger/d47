---
title: Commander's log
group: Conversation
nav_order: 146
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and the class decides behaviour, not just
  appearance. HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under
  that class would silently become what the in-app panel draws on this page. The docs site styles
  the two identically (main.scss extends one from the other); the app draws this band first and the How it works band after it.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to a readable account of what you did.</p>
<section>
<h2><span class="num">1</span> Fly. Then ask what a log would cost.</h2>
<p class="say">Say "write my commander's log".</p>
<p class="expect">D47 says what the log would cover, how many things it can account for from your journals, and what writing it would cost. It spends nothing.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a request for the log typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">write my commander's log</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">A log of the last session: … Writing it would cost about …</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">"write up my week" covers the last seven days instead.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Write it, then find it.</h2>
<p class="say">Say "write the log", then "what logs have I written".</p>
<p class="expect">D47 writes the log with a language model, which spends credit, and says where it is saved. The second phrase lists each written log and where it is. The files are plain markdown in data\commander-log.</p>
<svg viewBox="0 0 880 246" role="img" aria-label="The Your Commander's log page">
 <rect x="20" y="16" width="840" height="210" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <rect x="20" y="16" width="840" height="42" fill="var(--surface)"/>
 <text x="44" y="44" font-size="16" font-weight="700" fill="var(--accent)">Your Commander's log</text>
 <text x="44" y="92" font-size="16" fill="var(--text)">Work out what this would cost</text>
 <text x="44" y="130" font-size="16" fill="var(--text)">Write it</text>
 <text x="44" y="168" font-size="16" fill="var(--text)">What you have written</text>
 <text x="836" y="168" text-anchor="end" font-size="16" fill="var(--text-muted)">Open · Open the folder</text>
 <text x="44" y="212" font-size="15" fill="var(--text-muted)">The same steps as the phrases, from the panel.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Choose whose voice it is in.</h2>
<p class="say">Say "write my logs yourself".</p>
<p class="expect">The log is written as D47 writing about you. "write my logs in my own voice" goes back to the first person, and "write my logs with your commentary" has D47 chip in.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="Phrases that set whose voice a log is written in">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">write my logs yourself</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The Whose log it is row changes to D47 writes about you.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Settings › You and your records › Logs and goals shows it.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Say "write the log" with no quote first.</p>
<p class="expect">It writes straight away and spends credit without quoting. Say "what would a log cost" first if you want the price before anything is written.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="A log is written only when you ask, and the quote is optional">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Writing a log always spends credit.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">The quote is optional. Ask for the estimate first if you want the price before anything is written.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A session, or a week, becomes a readable log in which every sentence traces to an event.</p>
<section>
<h2><span class="num">1</span> The model is never handed your journal.</h2>
<svg viewBox="0 0 880 170" role="img" aria-label="The journal is computed into a numbered list of facts, and only that list reaches the model">
 <rect x="20" y="30" width="250" height="110" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="76" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">YOUR JOURNAL</text>
 <text x="145" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">stays on this PC</text>
 <line x1="282" y1="85" x2="302" y2="85" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="316,85 300,77 300,93" fill="var(--accent-muted)"/>
 <rect x="325" y="30" width="230" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="76" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">NUMBERED FACTS</text>
 <text x="440" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">counted by D47</text>
 <line x1="567" y1="85" x2="587" y2="85" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="601,85 585,77 585,93" fill="var(--accent-muted)"/>
 <rect x="610" y="30" width="250" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="735" y="76" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">THE MODEL</text>
 <text x="735" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">sees only the list</text>
</svg>
<p class="body">D47 counts jumps, sales and mishaps from the journal and hands the model that list. In-game chat is never part of it, so a message in an open system cannot reach the prompt.</p>
</section>
<section>
<h2><span class="num">2</span> Every sentence cites the facts it rests on.</h2>
<svg viewBox="0 0 880 130" role="img" aria-label="A sentence ends with the number of the fact it rests on">
 <rect x="20" y="24" width="840" height="82" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="72" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">Interdicted by Ordo Vulpes, I submitted. [4]</text>
</svg>
<p class="body">The model must end each sentence with the numbers of its facts. D47 then reads the log back and checks every citation. The brackets stay in the file, so you can see what each sentence rests on.</p>
</section>
<section>
<h2><span class="num">3</span> It can quote you before it spends anything.</h2>
<svg viewBox="0 0 880 170" role="img" aria-label="Asking what a log would cost returns a price, and a second sentence writes it">
 <rect x="20" y="30" width="400" height="110" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="76" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">what would a log cost</text>
 <text x="220" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">a price, nothing spent</text>
 <rect x="460" y="30" width="400" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="76" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">write the log</text>
 <text x="660" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">the log is written</text>
</svg>
<p class="body">A long session is the largest request D47 makes, so it never starts one by itself. The quote reads your journals here and sends nothing. The model can ask for neither step.</p>
</section>
<section>
<h2><span class="num">4</span> A sentence with no source is marked.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="The note the file carries over sentences that rest on nothing D47 counted">
 <rect x="20" y="24" width="840" height="102" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="64" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">“These sentences do not rest on anything D47 counted.</text>
 <text x="440" y="94" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">They are marked in the text above as well.”</text>
</svg>
<p class="body">A sentence citing nothing, or a fact that does not exist, is marked where it stands and listed under Not traced at the foot. That wording is in <code>LogRenderer.cs</code>.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="persona.html"><span class="ct">Core →</span><span class="cd">The voice the ship’s-AI log is written in.</span></a>
<a class="card" href="conversation.html"><span class="ct">Language model →</span><span class="cd">The spend ledger a written log reports into.</span></a>
</div>
</div>
</div></div>

## The details

Directive 47 can turn a session — or a week — into a readable log, written to plain markdown beside
the executable so you can keep it, edit it, and post it wherever you like.

Elite's community has been writing Commander's logs by hand since 3300. This writes the first draft.

### Every sentence traces to an event

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

### Whose log it is
{: #voice }

Three voices, and they are not interchangeable.

| Setting | What it writes |
|---|---|
| **You write it, in your own words** | Your own account, first person. The shipped default, and the plain one. |
| **D47 writes about you** | the core, in the personality you have chosen, writing about your flying. |
| **You write, D47 chips in** | Your account, with D47 interjecting a handful of times. |

The second is the one only d47 can do, which is exactly why it is the one you opt into rather than
the one that happens to you the first time you press a button.

**A log is d47 speaking at length, so it inherits the persona's protection.** With personality
switched off, the core's voice writes plainly rather than writing as somebody else, and the
commentary voice loses its commentary. Either way the file's header says which voice you asked for
and which one actually wrote it.

### What a log covers
{: #range }

The last session, today, the last seven days, or the last thirty. A session means **since you last
entered the game** — not since the journal file started, because Elite rolls a long session into a
continuation file without saying so.

Two exact dates are available from the panel. They are not available by voice, and that is
deliberate: the phrase router matches whole phrases and extracts nothing from them, which is what
stops it guessing at a date and confidently writing up the wrong fortnight.

### It costs money, and can quote you first
{: #cost }

Prose over a long session is the largest single request d47 will ever make. So:

- **It never happens by itself.** There is no schedule, no trigger, no callout that starts one.
- **It can quote you first.** Working out the cost reads your journals on this machine and sends
  nothing. The quote is optional: **Write it** in the panel and `write the log` both write straight
  away when nothing has been quoted.
- **It reports what it actually cost** afterwards, through the same spend ledger as everything else,
  so `what has this cost this month` includes it.

Asking for a quote first looks like this:

> **you:** write my commander's log
>
> **D47:** A log of the last session: 34 things I can account for, out of 4,812 events in 2 journal
> file(s). Writing it would cost about $0.04 — about 2,100 tokens in and up to 1,800 tokens of prose
> back, through claude-opus-5-5. Say "write the log" and I will.
>
> **you:** write the log

A model changed between the quote and the write is refused rather than honoured, so a quote is
always the price of the write that follows it.

How long a log runs is a setting — brief, standard or full — because the output budget is most of
what that figure is pricing.

### Story objectives are facts too

Each story objective you reach in the window becomes a fact of its own, sourced to the journal event
that fired it, such as a `Docked` for a dock objective. A story chapter's objective names the story
and the chapter, for example "Reached "The Anchorage", chapter 3 of The Marker." Nothing from a
story's hidden layer is ever put in the log.

### Writing one
{: #writing }

Open Settings › You and your records › Logs and goals, group Log and goals, and press **Write up a
session** on the **Your logs** row. The page **Your Commander's log** has **Work out what this would
cost**, **Write it** (which then shows the price) and **Open the folder**, and lists what you have
written with an **Open** button on each. Or say:

> write my commander's log

and then "write the log". "write up my session" quotes the span the **What a log covers** row names, and "write up my
week" quotes the last seven days. "what logs have I written" and "show me my logs" list the files.

Files land in `data/commander-log/` as markdown, named for the day and the span. **A run never
overwrites one**: the first may already have been edited, and this is the one file in `data/` that
d47 does not consider its own.

### Tools

Every tool here is protected — the panel, a hotkey and the phrase router reach them, and the
language model does not. The model is the component that reads untrusted text, so it is also not the
one that gets to authorise the largest request d47 makes.

#### `estimate_log`

Reads the window, prices it, and gets ready. Spends nothing.

```json
{"type":"object","properties":{"range":{"type":"string","description":"What span to cover. Omit for whatever the settings say.","enum":["session","today","week","month"]}},"required":[],"additionalProperties":false}
```

#### `write_log`

Writes the log that was just quoted, or, with no quote, prices the span the settings name and writes
it in the same step. A quote is consumed whether the write succeeds or not. A window with nothing in
it is not written.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `list_logs`

What has been written, and where.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```
