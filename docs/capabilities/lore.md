---
title: Lore
group: Knowledge
nav_order: 117
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
<p class="intro">Two steps to a system that means something.</p>
<section>
<h2><span class="num">1</span> Ask about the system you are in.</h2>
<p class="say">Say "what is notable about this system".</p>
<p class="expect">In one of the shipped systems D47 says what it knows and where that came from, with no network. Anywhere else it says "Nothing on" the system "beyond what the scanners say."</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what is notable about this system</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It answers for the system you are in, from a table that ships with D47.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Arriving in one of those systems makes D47 say so unprompted.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Add a note of your own.</h2>
<p class="say">Turn on Show every setting, open Settings › You and your records › Memory, notes and debrief, press Open your notes, type a note and press Remember this.</p>
<p class="expect">The window says "Stored as your word about" the system, or that the search agreed, and lists the note as "your word".</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">Something worth remembering about this system</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Remember this</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The note is for the system you are in, and is kept between sessions.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Nothing uploads it and nothing prunes it.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Ask in a system that is not among the shipped ones.</p>
<p class="expect">D47 says "Nothing on" the system "beyond what the scanners say" rather than making something up.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="Only the shipped systems have lore.">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Most systems have no lore.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">D47 says it has nothing rather than invent something about your neighbourhood.</text>
</svg>
</section>
</div></div>
</details>

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">D47 ships a table of notable systems, remarks on one when you arrive, and keeps notes you add.</p>
<section>
<h2><span class="num">1</span> A system is remarked on once in a quiet period.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="A system with lore attached is remarked on once in the quiet period, so a return visit the same evening is silent">
 <rect x="20" y="44" width="250" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="145" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">FIRST ARRIVAL</text>
 <text x="145" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">it says so, unprompted</text>
 <rect x="310" y="44" width="250" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="435" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">AGAIN, TONIGHT</text>
 <text x="435" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">silence</text>
 <rect x="600" y="44" width="260" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="730" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">AFTER THE QUIET PERIOD</text>
 <text x="730" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">it says so again</text>
 <text x="440" y="190" text-anchor="middle" font-size="16" fill="var(--text)">A carrier jump counts as arriving.</text>
</svg>
<p class="body">On arrival D47 looks the system up in its table. If it has an entry and the quiet period has passed, it says the entry once. The clock survives a restart. The period is seven days unless you change it.</p>
</section>
<section>
<h2><span class="num">2</span> Remark on arrival has three states.</h2>
<svg viewBox="0 0 880 236" role="img" aria-label="Arrival lore can be off, a bare remark, or a remark followed by a web lookup">
 <rect x="20" y="36" width="270" height="124" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">NEVER</text>
 <text x="155" y="110" text-anchor="middle" font-size="14" fill="var(--text-muted)">nothing on arrival</text>
 <text x="155" y="134" text-anchor="middle" font-size="14" fill="var(--text-muted)">asking still works</text>
 <rect x="305" y="36" width="270" height="124" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">REMARK ONLY</text>
 <text x="440" y="110" text-anchor="middle" font-size="14" fill="var(--text-muted)">the bare fact,</text>
 <text x="440" y="134" text-anchor="middle" font-size="14" fill="var(--text-muted)">and nothing further</text>
 <rect x="590" y="36" width="270" height="124" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="725" y="78" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">REMARK, AND LOOK IT UP</text>
 <text x="725" y="110" text-anchor="middle" font-size="14" fill="var(--text-muted)">the fact, then whatever</text>
 <text x="725" y="134" text-anchor="middle" font-size="14" fill="var(--text-muted)">the search found</text>
</svg>
<p class="body">Remark and look it up is the default. It follows the fact with a web search. A search result that arrives after you have jumped again is dropped.</p>
</section>
<section>
<h2><span class="num">3</span> Each note says where it came from.</h2>
<svg viewBox="0 0 880 264" role="img" aria-label="Four kinds of lore, each read back in its own sentence naming where it came from">
 <rect x="20" y="30" width="840" height="160" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="46" y="66" text-anchor="start" font-size="16" fill="var(--text)">“Earth is here.”</text>
 <text x="470" y="66" text-anchor="start" font-size="15" fill="var(--accent)">← the shipped table</text>
 <text x="46" y="100" text-anchor="start" font-size="16" fill="var(--text)">“You added this one, and the search agreed at the time…”</text>
 <text x="560" y="100" text-anchor="start" font-size="15" fill="var(--text-muted)">← corroborated</text>
 <text x="46" y="134" text-anchor="start" font-size="16" fill="var(--text)">“You told me…”</text>
 <text x="470" y="134" text-anchor="start" font-size="15" fill="var(--text-muted)">← your word</text>
 <text x="46" y="168" text-anchor="start" font-size="16" fill="var(--text)">“I wrote this one down myself, and nothing has checked it…”</text>
 <text x="580" y="168" text-anchor="start" font-size="15" fill="var(--text-muted)">← its own note</text>
 <text x="440" y="224" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">A label is never promoted.</text>
</svg>
<p class="body">A note you write is read back as <code>You told me:</code> and the note. A note the model writes is read back as <code>I wrote this one down myself, and nothing has checked it:</code> and the note. A later search never changes either.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="system-names.html"><span class="ct">System names →</span><span class="cd">What the name itself says, when there is no lore attached at all.</span></a>
<a class="card" href="conversation.html"><span class="ct">Language model →</span><span class="cd">The web search row a lookup needs, and what it costs.</span></a>
<a class="card" href="memory.html"><span class="ct">Memory →</span><span class="cd">The same labelling rule, applied to facts about you rather than about a system.</span></a>
</div>
</div>
</div></div>

## The details

Some systems mean more than their astrography. Directive 47 ships a table of twenty of them, says
so when you arrive, and keeps whatever you tell it about the rest.

### Ask for it

> "what is notable about this system"
> "anything notable here"
> "is there anything out here"

These route straight to the tool with no model, and it answers only for the system you are in. Asking about a system by name is not supported.

```text
Sol. Earth is here. Everywhere else on this list is somewhere humanity got to afterwards.
```

### Arriving somewhere

Jump into a system with something attached to it and Directive 47 says so, unprompted:

```text
John Jameson's Cobra Mark Three came down on the first planet's moon B and is still there — the
wreck every Commander eventually visits.
```

Once per system per quiet period. **How often a system's lore is worth repeating (days)** holds the
length of the quiet period in days; it defaults to seven, and 0 remarks every time. The row is under
Settings › Your core › Exploring, group Exploration callouts, shows with Show every setting on, and is
absent while Remark on arrival is Never.

The clock survives a restart, so logging off and coming back an hour later is quiet. A system with no
shipped entry says nothing on arrival in any mode.

A carrier jump counts as arriving. You were asleep in the back, but you are still somewhere new.

### Then looking it up {#remarks}

**Remark on arrival**, in the same group, is *Remark, and look it up* by default. The bare fact is followed by a
web search, spoken when it comes back. You can also say "stop remarking on systems" and "start remarking on
systems", which choose Never and *Remark, and look it up*.

```text
From a search: Commanders have used HIP 12099 as an encoded-materials run since the wreck was found
in 3303, because the crash site carries nine data points in one place.
```

The three states:

| Setting | What happens on arrival |
|---|---|
| Never | Nothing. Asking still works. |
| Remark only | The bare fact, and nothing further. |
| Remark, and look it up | The fact, then whatever the search found. |

Some things worth knowing about the lookup:

- **It needs [web search](conversation.md) switched on**, in Settings › Providers › Outside sources,
  and an endpoint that offers one. If it cannot search, the first sentence is followed by "I cannot search
  from here, so that is all I have on it."
- **Searches are billed separately from tokens**, and the running total counts them.
- **It gives up after 45 seconds.**
- **A result that arrives late is dropped.** If you have jumped again by the time it lands, you were
  somewhere else when it became relevant, and a sentence about a system you left is worse than
  silence.
- **What it found is always spoken as a search result**, never in the flat voice the shipped table
  uses. Nothing a search returns is ever written into a table.

### Your own notes {#notes}

**Your own notes**, under Settings › You and your records › Memory, notes and debrief (Show every setting
on), has an Open your notes button. The window lists what you have told Directive 47 and is where you add
one, with Remember this. Writing a note in the window is the act that makes an entry yours; the model's
tool cannot do it.

When you add one, Directive 47 searches for the system first, if it can, and records whether the search
agreed. The window labels each entry "from d47's table", "a search agreed at the time", "your word" or
"written by D47 itself, unverified", and Forget removes one. Read back, they are four different sentences:

```text
Earth is here.                                                   ← the shipped table
You added this one, and the search agreed at the time: …         ← corroborated
You told me: …                                                   ← your word
I wrote this one down myself, and nothing has checked it: …      ← D47's own tool call
```

A label is never promoted. If a later search would have corroborated a note, the note still says you told
me.

Notes live in `data/lore.json` beside the executable, one file for the installation rather than one
per Commander. Each entry records which Commander was aboard and how it arrived. Edits made by hand are
noticed from the file's contents.

### What Directive 47 can do about it

#### `get_system_lore`

Everything known about the system you are in, each answer stating where it came from.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `remember_about_system`

Notes something about the system you are in. Stored as unverified and read back that way, because it is
the model writing, not you. The reply is "Noted about" the system, "as unverified:" and the note. A
note over 400 characters is cut at 400 with an ellipsis. Asking D47 aloud to remember something, for
example "remember that this system has a good mining ring", goes through this tool.

```json
{"type":"object","properties":{"note":{"type":"string","description":"What is worth remembering, in one or two sentences. Facts about the place, not about what the Commander is doing today."}},"required":["note"],"additionalProperties":false}
```

The tool is open to the model. An entry the model wrote says so every time it is read back, and you can
forget it from the notes window. Writing is limited to the system you are in, because a note is keyed on
the system address and addresses come from your journal.

### Where the twenty came from

The shipped table is the maintainer's own compilation. The facts in it are Frontier's; see
[NOTICE](https://github.com/dseelinger/d47/blob/main/NOTICE). `tools/gen-lore.py` builds it, resolving
each system's name to its address through spansh.co.uk and edsm.net, and a disagreement or a miss stops
the run. Nothing in the table is a distance that gets re-surveyed or a status that can change. Anything
that moves belongs to the lookup, where it stays a sentence.
