---
title: Writing an adventure
group: Conversation
nav_order: 205
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.

  The editor's own page, asked for 2026-08-23. adventures.md is about what an adventure is and
  how one is flown; this is the form, which is the half nobody can guess at from the tab.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">A story is a name and a list of beats. Everything else on this form is optional, and the beats are the only part that does anything.</p>
<section>
<h2><span class="num">1</span> Three things are required. The rest is flavour.</h2>
<svg viewBox="0 0 880 256" role="img" aria-label="A key, a name and at least one beat are required; the opening line and the five spine questions are optional">
 <rect x="20" y="30" width="410" height="150" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="225" y="66" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">REQUIRED</text>
 <text x="225" y="100" text-anchor="middle" font-size="15" fill="var(--text)">a key — its short id</text>
 <text x="225" y="126" text-anchor="middle" font-size="15" fill="var(--text)">a name — what you call it</text>
 <text x="225" y="152" text-anchor="middle" font-size="15" fill="var(--text)">at least one beat</text>
 <rect x="460" y="30" width="400" height="150" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="66" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">OPTIONAL</text>
 <text x="660" y="100" text-anchor="middle" font-size="15" fill="var(--text-muted)">the opening line</text>
 <text x="660" y="126" text-anchor="middle" font-size="15" fill="var(--text-muted)">the five spine questions</text>
 <text x="660" y="152" text-anchor="middle" font-size="15" fill="var(--text-muted)">a title on each beat</text>
 <rect x="20" y="200" width="840" height="46" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="230" text-anchor="middle" font-size="16" fill="var(--text)">Save stays greyed out until all three are there, and says which one is missing.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> A beat waits for one of five things. There is no sixth.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="The five triggers a beat can wait for: arrive, dock, land, scan, reach a rank">
 <rect x="20" y="30" width="164" height="92" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="102" y="68" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">ARRIVE</text>
 <text x="102" y="98" text-anchor="middle" font-size="14" fill="var(--text-muted)">at a system</text>
 <rect x="196" y="30" width="164" height="92" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="278" y="68" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">DOCK</text>
 <text x="278" y="98" text-anchor="middle" font-size="14" fill="var(--text-muted)">at a station</text>
 <rect x="372" y="30" width="164" height="92" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="454" y="68" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">LAND</text>
 <text x="454" y="98" text-anchor="middle" font-size="14" fill="var(--text-muted)">on a body</text>
 <rect x="548" y="30" width="164" height="92" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="630" y="68" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">SCAN</text>
 <text x="630" y="98" text-anchor="middle" font-size="14" fill="var(--text-muted)">a body</text>
 <rect x="724" y="30" width="136" height="92" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="792" y="68" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">RANK</text>
 <text x="792" y="98" text-anchor="middle" font-size="14" fill="var(--text-muted)">reach one</text>
 <rect x="20" y="142" width="840" height="72" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="172" text-anchor="middle" font-size="16" fill="var(--text)">All five are things your journal already records, which is why they are the whole list.</text>
 <text x="440" y="198" text-anchor="middle" font-size="15" fill="var(--text-muted)">A story cannot wait for something Elite never writes down — so it is not offered.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> The spine is for the core, not for you.</h2>
<svg viewBox="0 0 880 248" role="img" aria-label="The five spine answers are given to the ship's AI so its improvised lines stay in the same story">
 <rect x="20" y="30" width="330" height="150" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="185" y="62" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">THE FIVE ANSWERS</text>
 <text x="185" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">about · want · stake</text>
 <text x="185" y="116" text-anchor="middle" font-size="14" fill="var(--text-muted)">turn · what the end means</text>
 <text x="185" y="152" text-anchor="middle" font-size="14" fill="var(--text-muted)">every one of them optional</text>
 <line x1="362" y1="104" x2="398" y2="104" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="412,104 396,96 396,112" fill="var(--accent-muted)"/>
 <rect x="422" y="30" width="438" height="150" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="641" y="62" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">THE CORE READS THEM</text>
 <text x="641" y="92" text-anchor="middle" font-size="15" fill="var(--text-muted)">so when you ask it about the story,</text>
 <text x="641" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">the answer it improvises is in</text>
 <text x="641" y="140" text-anchor="middle" font-size="15" fill="var(--text-muted)">the same story you wrote</text>
 <text x="440" y="216" text-anchor="middle" font-size="16" fill="var(--text)">Leave them blank and the beats still fire. You just get less around them.</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">They are in the craft's order, which is why they read like questions rather than fields.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="capabilities/adventures.html"><span class="ct">Adventures →</span><span class="cd">What an adventure is, how one is flown, and the rows that control it.</span></a>
<a class="card" href="capabilities/persona.html"><span class="ct">Persona →</span><span class="cd">Whose voice tells it, and why each core tells it differently.</span></a>
<a class="card" href="capabilities/journal.html"><span class="ct">Journal →</span><span class="cd">Where a beat's trigger is noticed: your own flight log, read as you fly.</span></a>
</div>
</div>
</div></div>

## The details

The **Write** page on the Adventures tab, reached from *Write an adventure* — and the same form
reached from *Edit* on a story you already have.

### The three things Save waits for

| | Why |
|---|---|
| **A key** | The short id an adventure is stored and asked for under |
| **A name** | What you and the ship's AI call it |
| **At least one beat** | A story with no beats can never advance, so it is not a story |

The red lines under the form are not errors — they are the list of what is still missing, and they
disappear as you fill each one in. Nothing is saved until you press Save, so leaving the page loses
the draft.

**Save** keeps it. **Save and begin** keeps it and starts it immediately, which is the usual choice
for something you wrote because you are about to go and fly it.

### Beats, and the five triggers

A beat is *something that happens*, *where*, and *the line said when it does*. Adding one asks
those three in turn.

The trigger is one of five, and the list is closed:

- **Arrive at a system**
- **Dock at a station**
- **Land on a body**
- **Scan a body**
- **Reach a rank**

That is not a starting set. Those are the five things your own journal records unambiguously, and a
story that waits for something Elite never writes down would be a story that never advances — so
it is not offered rather than offered and broken.

**Where** is either *here* — with the ids of wherever you are standing, which is the quickest way
to pin a beat to a real place — or a name you type, resolved through the galaxy search. For a rank
beat it is the rank instead.

**A scan beat also fires on going there.** Elite writes a `Scan` the first time a body enters your
discovered set and then, in the overwhelming majority of cases, never again — so a story that sent
you to somewhere you had already scanned used to wait for an event the game had already spent, with
no way past it. Arriving at the body counts as well, so pick the body you want the story to visit
and do not worry about whether you have been there before.

Beats fire **in order**. Progress comes from your journal as you fly, not from anything you press.

### The spine

Five optional questions, in the order a writer would answer them:

> What is this about · What do you want in it · What is really at stake · Where does it turn ·
> What does the end mean

**They are not shown to you anywhere.** They are given to the ship's AI, so that when you ask it
about the story mid-flight — or remark on something that just happened — what it improvises belongs
to the story you wrote rather than to a story it is inventing as it goes.

Leave every one of them blank and the adventure works exactly the same. You simply get less around
the beats.

### The opening

Said once, when the adventure begins. Blank means it begins without ceremony.

### Limits

An adventure has a maximum number of beats, and the editor says so rather than silently refusing
the button. The cap exists because a story is read aloud between things you are doing, and one with
forty beats in it is a chore rather than a story.
