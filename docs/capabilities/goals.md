---
title: Goals
group: Conversation
nav_order: 147
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
<p class="intro">Three steps to goals that track their own progress.</p>
<section>
<h2><span class="num">1</span> Ask how your goals are going.</h2>
<p class="say">Say "how are my goals going".</p>
<p class="expect">D47 reads back each long goal with how far along it is, how long it has been running and where the figure came from. No language model is needed.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question about goals typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">how are my goals going</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"what are my goals" does the same.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Progress is worked out from your journal, so there is nothing to tell it.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Open the goals and give them an age.</h2>
<p class="say">Open Commander › Checklist, tick Goals, then press Read my journals.</p>
<p class="expect">Each goal shows its progress. Read my journals reads the journals on your disk once, and every goal then shows how long it has been running. The button is there until that has been done.</p>
<svg viewBox="0 0 880 246" role="img" aria-label="The Goals view on the Checklist page">
 <rect x="20" y="16" width="840" height="210" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <rect x="20" y="16" width="840" height="42" fill="var(--surface)"/>
 <text x="44" y="44" font-size="16" font-weight="700" fill="var(--accent)">Goals (10 running)</text>
 <text x="44" y="92" font-size="16" fill="var(--text)">Elite V in Trade</text>
 <text x="836" y="92" text-anchor="end" font-size="16" fill="var(--text-muted)">Tycoon · running 400 days</text>
 <text x="44" y="130" font-size="16" fill="var(--text)">Every engineer unlocked</text>
 <text x="836" y="130" text-anchor="end" font-size="16" fill="var(--text-muted)">from your journal</text>
 <text x="44" y="168" font-size="16" fill="var(--text)">The ship collection</text>
 <text x="836" y="168" text-anchor="end" font-size="16" fill="var(--text-muted)">from your fleet</text>
 <text x="44" y="212" font-size="15" fill="var(--text-muted)">Open a goal for its next step. Remove takes it off the page.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Take a goal off the page, or bring it back.</h2>
<p class="say">Say "remove the trade goal", then "recover the trade goal".</p>
<p class="expect">The goal leaves the Goals view and D47 says so, then comes back. Removed goals wait under Removed until you press Recover.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="Spoken phrases that remove and recover a goal">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">remove the trade goal</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Combat, trade, exploration, mercenary, exobiology, imperial navy, federal navy,</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">powerplay, engineers and ship collection are the names it knows.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Open a career goal and press Ask D47.</p>
<p class="expect">Rank has no line to put on a list, because it comes from playing the career. Ask D47 sends the question for that ladder as if you had typed it, and the answer appears in the transcript.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="A career goal offers Ask D47 rather than a checklist line">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">A career goal has no Suggest a line.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Engineers and the ship collection do. Career and Powerplay goals offer Ask D47 instead.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Your checklist holds this week. Goals hold the months-long campaigns, and work out their own progress.</p>
<section>
<h2><span class="num">1</span> Two different questions, kept apart.</h2>
<svg viewBox="0 0 880 170" role="img" aria-label="The checklist holds what you are doing this week and goals hold what you are doing this year">
 <rect x="20" y="30" width="400" height="110" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="76" text-anchor="middle" font-size="19" font-weight="800" fill="var(--text)">CHECKLIST</text>
 <text x="220" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">what you do this week</text>
 <rect x="460" y="30" width="400" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="76" text-anchor="middle" font-size="19" font-weight="800" fill="var(--text)">GOALS</text>
 <text x="660" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">what you work towards for months</text>
</svg>
<p class="body">A checklist line is ticked by hand. A goal has a definition of done, such as Elite V in Trade or every engineer unlocked, and no tick box at all.</p>
</section>
<section>
<h2><span class="num">2</span> Progress is read off your journal, never typed.</h2>
<svg viewBox="0 0 880 170" role="img" aria-label="The journal says a rank and the goal reports it with its name">
 <rect x="20" y="30" width="250" height="110" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="76" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">Rank event</text>
 <text x="145" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">written by Elite</text>
 <line x1="282" y1="85" x2="302" y2="85" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="316,85 300,77 300,93" fill="var(--accent-muted)"/>
 <rect x="325" y="30" width="230" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="76" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">THE GOAL</text>
 <text x="440" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">reads it again</text>
 <line x1="567" y1="85" x2="587" y2="85" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="601,85 585,77 585,93" fill="var(--accent-muted)"/>
 <rect x="610" y="30" width="250" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="735" y="76" text-anchor="middle" font-size="16" fill="var(--text)">Tycoon, part of</text>
 <text x="735" y="100" text-anchor="middle" font-size="16" fill="var(--text)">the way to Elite V.</text>
</svg>
<p class="body">Your rank, engineers and hulls come from the journal and your fleet. When Elite has said nothing this session, the last figure stands with its date, and never resets to nothing.</p>
</section>
<section>
<h2><span class="num">3</span> A goal names today's step, and you decide whether it goes on the list.</h2>
<svg viewBox="0 0 880 170" role="img" aria-label="A goal offers a step as a suggestion that you accept on the checklist">
 <rect x="20" y="30" width="250" height="110" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="76" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">Every engineer</text>
 <text x="145" y="100" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">unlocked</text>
 <line x1="282" y1="85" x2="302" y2="85" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="316,85 300,77 300,93" fill="var(--accent-muted)"/>
 <rect x="325" y="30" width="230" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">UNLOCK SOLVER</text>
 <text x="440" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">picks the next engineer</text>
 <line x1="567" y1="85" x2="587" y2="85" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="601,85 585,77 585,93" fill="var(--accent-muted)"/>
 <rect x="610" y="30" width="250" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="735" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">SUGGESTION</text>
 <text x="735" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">you accept it</text>
</svg>
<p class="body">Suggest a line on a goal puts its step on the Suggestions list. Nothing reaches the checklist until you accept it there. Engineers and the ship collection offer a step this way.</p>
</section>
<section>
<h2><span class="num">4</span> Career goals ask a question instead of planning.</h2>
<svg viewBox="0 0 880 130" role="img" aria-label="The question Ask D47 sends for the Mercenary ladder">
 <rect x="20" y="24" width="840" height="82" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="72" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">“What's the fastest way to gain Mercenary rank from where I stand? Search the web if you can.”</text>
</svg>
<p class="body">Rank is earned by playing the career, so there is no route to plot. Ask D47 sends the question for that ladder, built in <code>GoalCatalogue.cs</code>, and the model answers from your rank.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="checklists.html"><span class="ct">Checklists →</span><span class="cd">Where an accepted step lands, and what it does once it is there.</span></a>
<a class="card" href="engineers.html"><span class="ct">Engineers →</span><span class="cd">The solver the engineer arc hands its question to.</span></a>
<a class="card" href="ships.html"><span class="ct">Ships →</span><span class="cd">The fleet the collection arc counts, and the hull it names next.</span></a>
</div>
</div>
</div></div>

## The details

Your checklist holds what you are doing this week. Goals hold what you are doing this year.

Elite in each career. Every engineer unlocked. The ship collection. Directive 47 calls each of
these an **arc**: a named ambition with a definition of done, a progress figure nobody typed, and
an age.

### Progress is worked out, never typed

An arc cannot be ticked by hand and does not offer to be. Your rank, your unlocked engineers and
your owned hulls are read off your journal, so the figure is a fact rather than an opinion — the
same rule the checklist already draws about derived lines, for the same reason.

Where the journal cannot say, d47 says so rather than guessing:

- **An arc d47 cannot currently evaluate reports as of when it last could.** If Elite has not said
  anything about your rank this session, the figure from your journals stands with its date on it.
  It never resets to nothing on the absence of evidence.

### The ten that ship

| Arc | Done when | Where the figure comes from |
|---|---|---|
| Elite V in Combat | Combat rank 13 — Elite V | Live journal state |
| Elite V in Trade | Trade rank 13 — Elite V | Live journal state |
| Elite V in Exploration | Explore rank 13 — Elite V | Live journal state |
| Elite V as a Mercenary | Soldier rank 13 — Elite V | Live journal state |
| Elite V in Exobiology | Exobiologist rank 13 — Elite V | Live journal state |
| Imperial Navy | Empire rank 14 — King | Live journal state |
| Federal Navy | Federation rank 14 — Admiral | Live journal state |
| Powerplay rank | Powerplay rank 100 | Your live pledge |
| Every engineer unlocked | Every engineer in the directory | Live journal state |
| The ship collection | One of every hull, owned at once; the Cobra MkIV counts only when owned | Your fleet, plus what you are flying |

**Each rank says its name**, from Harmless up through Elite V — so d47 says *Trailblazer, 12% into
it* rather than *rank 5 of 8, 12% into it*. A career arc is done at Elite V, not at Elite. The two
navy arcs read the same way — *Master, 8% into it* rather than *rank 3* — but each runs to the top
of its own ladder, King for the Empire and Admiral for the Federation, because neither has an
Elite.

**The Powerplay arc is only on the page while you are pledged.** Pledge to a Power and it
appears, reporting the rank you hold against 100; leave, and it goes again without a restart.
Rank 0 of 100 for nobody is not a goal you hold. Its rank is read off your live pledge rather than
off a journal read, so a promotion moves the figure the moment the game writes it.

**There is no CQC arc**, because almost nobody plays it and an arc permanently at nothing is a line
of the page spent telling you about a thing you are not doing. If any of the others is not yours
either, remove it — it goes off the page and into the **Removed** list below the goals, and stays there until you press **Recover**.

**Ask D47 on a rank or Powerplay goal.** Rank comes from playing the career, so there is no line
to put on your list. Open one of the five career goals, a navy goal or the Powerplay goal and press
**Ask D47** beside **Remove**. d47 sends the question for that ladder at once, as if you had typed
it — *"What's the fastest way to gain Mercenary rank from where I stand? Search the web if you
can."* — and the answer appears below it in the transcript. The model reads your rank from your
goals, and searches the web only when web search is on. A finished goal has no button, and the
button does nothing while d47 is still answering.

### Your story is a goal too

The stock story you are running appears beside the others, with its clues given out of the story's
total and its stage and current objective as the note, for example "Bad Guys Close In, clue 11 of
18. Now: kill bonds for LTT 7786 Labour, 3 of 8". The note says so when the story is paused or
switched off, a finished story shows as done, and an abandoned one is not shown. You can remove it
like any other goal, but it cannot be put on the checklist or ticked: it is worked out from the
story itself.

### Ages come from your journals

Nothing happens until you ask. Tick **Goals** on the Checklist bar — it says how many arcs are
running — and the goals replace the list until you untick it. Each rank goal has two bars: one to
the top of its ladder, labelled **To Elite V**, **To King** or **To Admiral**, and one to the next
rank. Press **Read my journals** at the bottom of the goals. One pass
over the journals already on your disk gives every arc its start date. It is arithmetic over
events on your own machine — **nothing leaves it, and no journal is ever sent to a model.**

A read looks like this:

```
Elite V in Combat: Mostly Harmless, 39% into it. Running 11 months.
Elite V in Trade: Tycoon, 40% into it. Running 13 months.
Elite V in Exploration: Pathfinder, 12% into it. Running 13 months.
Elite V as a Mercenary: Defenceless.
Elite V in Exobiology: Directionless.
Imperial Navy: Serf, 4% into it. Running 8 months.
Federal Navy: Cadet, 61% into it. Running 8 months.
Powerplay rank: rank 8 of 100 with Li Yong-Rui. Running 8 months.
Every engineer unlocked: 19 of 53 unlocked, 2 invited. Running 12 months.
The ship collection: 11 of 47 hulls owned. Running 13 months.
Ages come from 914 journals on this disk, the oldest from 4 Jul 2025.
```

### The checklist points at the arc

The join is what makes both worth having: given a goal that takes months, **what is the concrete
thing to do about it today?**

Ask about one arc and d47 answers with the next step, and offers to put it on your list. Accepting
is your act — an arc proposes through the same pending-proposal path everything else does, and you
accept or decline it on the Checklist page.

A line that came from an arc says so, so finishing it visibly moves something bigger than itself.

What each arc offers:

- **Every engineer unlocked** hands the question to the unlock solver, which already answers it
  properly — ranked by fastest unlock and distance together, and the chain it promotes carries an
  access step beside each modification.
- **The ship collection** names the cheapest hull you do not own, off the shipyard table.
- **The career arcs propose nothing, and say why.** Rank is earned by doing the career; there is no
  route to it anyone can plot. Where d47 has a tool that helps — `plot_trade_route`,
  `plot_exploration_route`, `plot_exobiology_route` — it names that instead of inventing a plan.

### Talking to it

> how are my goals going

> what are my goals

> how are my long term goals

To take a goal off the page or bring it back, say "remove" or "recover", then "the", the goal's name
and "goal": combat, trade, exploration, explorer, mercenary, exobiology, imperial navy, federal navy,
powerplay, engineers or ship collection. For example "remove the mercenary goal" and "recover the
mercenary goal". Both answer without the model.

#### `get_goals`

Reads every arc back: how far along each is, how long it has been running, and where the figure came
from. The one tool here the model can call on its own, because *how is the Elite exploration push
going* is a thing you say mid-flight and an arc d47 cannot see is an arc it cannot be asked about.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `get_goal_step`

Says what to do about one arc today, and offers it as a checklist line. Protected — it writes to the
pending proposals, and writing is yours. No phrase calls it: open a goal on the Checklist page's
Goals view and press **Suggest a line**.

```json
{"type":"object","properties":{"goal":{"type":"string","description":"Which goal, by name or key, as read back by get_goals."}},"required":["goal"],"additionalProperties":false}
```

#### `remove_goal`

Takes an arc off the page and puts it in the Removed list. Protected, for the same reason. A removed
arc stays removed through a re-read of your journals — it is a decision you made, not a figure d47
recomputes.

```json
{"type":"object","properties":{"goal":{"type":"string","description":"Which goal, by name or key."}},"required":["goal"],"additionalProperties":false}
```

#### `recover_goal`

Puts a removed arc back on the page where it was. Protected.

```json
{"type":"object","properties":{"goal":{"type":"string","description":"Which goal, by name or key."}},"required":["goal"],"additionalProperties":false}
```

### Where it is kept
{: #backfill }

`data/goals.json`, beside the executable, keyed per Commander on the Frontier id in your journals —
so two characters on one machine do not report each other's progress.

The mined marks are a recomputation and are replaced wholesale by the next read. **The arcs you
removed are yours**, are stored separately, and a re-read never touches them. The file is plain JSON and meant
to be readable; a line d47 cannot parse is reported rather than silently dropped.
