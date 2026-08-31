---
title: Adventures
group: Knowledge
nav_order: 114
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and that is load-bearing rather than cosmetic.
  HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under that class
  would silently become what the in-app panel draws on this page. The docs site styles the two
  identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. That separation is the
  whole point of there being two, and it is the thing that will erode first.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="lede">Three steps to a story that runs while you fly.</p>
<section>
<h2><span class="num">1</span> Ask for one, in the Adventures tab or out loud.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a request for an adventure typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">tell me a story about this system</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Or press Adventures in the tab strip and pick one there.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Either way you get a first beat, and the story waits for you.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Fly. The next beat arrives when your journal earns it.</h2>
<svg viewBox="0 0 880 168" role="img" aria-label="A jump or a docking in the journal moves the story to its next beat">
 <rect x="20" y="24" width="250" height="72" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="56" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">YOU JUMP</text>
 <text x="145" y="80" text-anchor="middle" font-size="15" fill="var(--text-muted)">or dock, or scan</text>
 <line x1="282" y1="60" x2="306" y2="60" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="320,60 304,52 304,68" fill="var(--accent-muted)"/>
 <rect x="334" y="24" width="526" height="72" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="597" y="56" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">THE NEXT BEAT IS SPOKEN</text>
 <text x="597" y="80" text-anchor="middle" font-size="15" fill="var(--text-muted)">in your ship AI's own voice</text>
 <text x="20" y="146" font-size="16" fill="var(--text-muted)">Nothing is on a timer. Say "where am I up to" to hear the story so far.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="A story only moves when Elite writes something to the journal">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">A story moves when the game does.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Sitting in the menu, nothing happens. Say "next" if you want it moved on anyway.</text>
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
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">A story you fly, told by the ship's AI, moved along by your own journal.</p>
<section>
<h2><span class="num">1</span> A story, not a list of stops.</h2>
<svg viewBox="0 0 880 300" role="img" aria-label="A spine of premise, want, stake, turn and ending, with beats hung on real places">
 <rect x="20" y="20" width="360" height="216" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="52" y="58" font-size="19" font-weight="800" fill="var(--accent)">THE SPINE</text>
 <text x="52" y="96" font-size="16" fill="var(--text)">what it is about</text>
 <text x="52" y="126" font-size="16" fill="var(--text)">what you want in it</text>
 <text x="52" y="156" font-size="16" fill="var(--text)">what is really at stake</text>
 <text x="52" y="186" font-size="16" fill="var(--text)">where it turns</text>
 <text x="52" y="216" font-size="16" fill="var(--text)">what the end means</text>
 <line x1="398" y1="128" x2="428" y2="128" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="442,128 426,120 426,136" fill="var(--accent-muted)"/>
 <rect x="458" y="20" width="402" height="60" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="56" font-size="16" fill="var(--text)">a beat, standing on a real place</text>
 <rect x="458" y="92" width="402" height="60" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="128" font-size="16" fill="var(--text)">a beat, standing on a real place</text>
 <rect x="458" y="164" width="402" height="60" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="484" y="200" font-size="16" fill="var(--text)">a beat, standing on a real place</text>
 <text x="440" y="268" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">The shape is written first. The places are where that shape can stand.</text>
 <text x="440" y="294" text-anchor="middle" font-size="16" fill="var(--text-muted)">Which is why D47 is never asked for five stops — it is asked for a story.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Your journal moves it. There is nothing to tick.</h2>
<svg viewBox="0 0 880 268" role="img" aria-label="A beat fires when you reach its place, and nothing before you began counts">
 <rect x="20" y="30" width="250" height="86" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="145" y="68" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">YOU BEGIN IT</text>
 <text x="145" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">the clock starts here</text>
 <line x1="282" y1="73" x2="308" y2="73" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="322,73 306,65 306,81" fill="var(--accent-muted)"/>
 <rect x="334" y="30" width="250" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="459" y="68" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">YOU FLY THERE</text>
 <text x="459" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">arrive, dock, land or scan</text>
 <line x1="596" y1="73" x2="622" y2="73" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="636,73 620,65 620,81" fill="var(--accent-muted)"/>
 <rect x="648" y="30" width="212" height="86" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="754" y="68" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">IT SPEAKS</text>
 <text x="754" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">and says where next</text>
 <rect x="20" y="148" width="840" height="60" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="185" text-anchor="middle" font-size="16" fill="var(--text)">Nothing you did before you began counts, and only the current beat can fire.</text>
 <text x="440" y="242" text-anchor="middle" font-size="16" fill="var(--text-muted)">Fly with D47 closed and it catches up when you start it. Wander off and the story waits —</text>
 <text x="440" y="264" text-anchor="middle" font-size="16" fill="var(--text-muted)">going somewhere else is what a sandbox is for.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> It cannot spoil itself.</h2>
<svg viewBox="0 0 880 274" role="img" aria-label="What the ship's AI is told, and when">
 <rect x="20" y="24" width="840" height="62" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="56" y="62" font-size="18" font-weight="700" fill="var(--accent)">always</text>
 <text x="824" y="62" text-anchor="end" font-size="16" fill="var(--text)">the premise, what you want, what is at stake</text>
 <rect x="20" y="98" width="840" height="62" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="56" y="136" font-size="18" font-weight="700" fill="var(--text)">once it has happened</text>
 <text x="824" y="136" text-anchor="end" font-size="16" fill="var(--text)">the turn, and what the ending meant</text>
 <rect x="20" y="172" width="840" height="62" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="56" y="210" font-size="18" font-weight="700" fill="var(--danger)">never</text>
 <text x="824" y="210" text-anchor="end" font-size="16" fill="var(--text)">the beats ahead of you</text>
 <text x="440" y="268" text-anchor="middle" font-size="16" fill="var(--text-muted)">A storyteller who knows the ending leaks it. So the AI is simply never told what is coming.</text>
</svg>
<p class="body">Foreshadowing still happens — it is written <em>into</em> the earlier beats, by the turn that did know the ending. Between beats the ship's AI wonders aloud in character and never states a new fact about the story, so nothing it says on a quiet stretch can contradict a beat you have not reached.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="persona.html"><span class="ct">Persona →</span><span class="cd">Whose story it is — each core writes the one it cares about.</span></a>
<a class="card" href="galaxy.html"><span class="ct">Galaxy search →</span><span class="cd">The catalogue a generated story picks its places from.</span></a>
<a class="card" href="checklists.html"><span class="ct">Checklists →</span><span class="cd">The other thing that remembers what you are in the middle of.</span></a>
</div>
</div>
</div></div>

## The details

An adventure is a story: someone wants something, there is a belief the events exist to test, a
turn where it stops being what it looked like, and an ending that means something. It is told by
the ship's AI, anchored to the galaxy by beats, accepted by you, and advanced by your own journal.

The drive behind it is to add story to a sandbox, which sandboxes deeply lack. It is deliberately
**not** a checklist of things to complete.

### Two ways to have one

**Write an adventure** — the editor is a level of the Adventures tab. A name, an opening, then the
five spine questions in order, each skippable. Then the beats: what happens, where, and the line.
Every field is a chooser except the prose, so the form cannot compose something the file would
refuse.

**Ask for one** — a short form and D47 writes it, in the voice of whichever core is aboard. Three
choosers, each with a default, so pressing *Go* on an untouched form is a complete ask:

- **Reach** — how far the story may go: *near here*, *a session's flying*, *anywhere*. Turned into
  light years by what you can actually move — your ship's jump range, or your carrier's if you have
  one.
- **Length** — which structure. *Short* is three beats, setup and turn and resolution. *An evening*
  is five. *Long* is eight or more. The count follows from the structure rather than the other way
  round.
- **Using** — *this ship only*, or *anything I own*. Shown only when you have a choice.

And one optional thing said: a brief — a theme, a mood, a place it must include. Empty is fine.

**Things it reads rather than asks**, because asking would be asking you to describe your own
ships: your fleet and what each hull can do, whether you have a carrier and where it is, where you
are, who is aboard, and your ranks.

### What a beat can be

Five triggers, and every one is a comparison on a structured field rather than on a name:

| Trigger | Matched on | Never on |
|---|---|---|
| Arrive at a system | the system's id | its name |
| Dock at a station | the station's market id | its name — a carrier's name is player-chosen |
| Land on a body | system and body ids | the body's name |
| Scan a body | system and body ids | the body's name |
| Reach a rank | career and a number | any rank word |

Nothing a stranger can choose — a ship name, an in-game message, a mission title — can be a
trigger. That is the safety property stated as a type rather than as a promise.

### People may be invented. Places may not.

A story needs people and the galaxy's named ones are few, so the AI may invent a contact, a rival,
a voice on a wreck's log. It may not invent a system, a station, a body, a faction, a Power or a
game mechanic — every place in a generated story is resolved against the galaxy before you are
offered it, and a miss refuses the whole draft by name.

**Invented people are told about, never met.** The game has no act for meeting anyone, and the only
thing you can actually do in a story is fly to the next beat. Ask the core whether one of them was
real and it says they are someone in the story.

### Where you are in it, and what it says

A beat speaks when it fires, and hands over to the next one in the same breath:

```text
The log's last entry is dated the day the beacon went quiet.
Next: dock at Maren Anchorage in Dyson's Hollow.
```

The hand-off names the place and the act and nothing else — never the next beat's title or its
line, which is the spoiler rule holding. A scan's hand-off says *how*, because "scan X" sent a
Commander looking for a detailed surface scanner when the ship's own scanner was what the story
meant. It also says that going there counts: a body already scanned writes no second `Scan`, so a
scan beat is satisfied by the approach as well, and a story cannot strand a Commander on somewhere
they had already been.

**No counts where you read.** The card says the story's name and where it is — *not yet begun*, the
current beat's title, or *finished*. *Beat 3 of 7* is checklist language and lives in the Technical
transcript only.

### Stopping, and starting again

**Abandon** a begun adventure and it stops telling you: no beat fires, the AI drops it from what it
knows, and a beat waiting out its settle window is discarded. The record stays, folded away at the
foot of the list with what it reached. **Begin again** on an abandoned one starts from the opening
with a fresh stamp — nothing that happened in the gap counts, because a start is a start.

Abandoning is also how a begun adventure gets edited: abandon it, change it, begin again.

**Remove** deletes the record. For a begun one it asks first, because an adventure three beats in
is work you did.

Both are yours alone, reachable from the panel and nowhere else.

### Nothing here is callable by the model

Generation, beginning, abandoning and removing are all your acts, on the panel. The ship's AI can
*read* the story — that is the whole point, so it can play off it — and can change nothing about
it. A hostile message arriving in your comms panel cannot propose a story, end one, or delete one.

It also costs nothing: none of this is on the advertised tool surface.

### Where it lives

`data/adventures.json`, beside the executable, per Commander, and hand-editable like everything
else D47 writes. Only two things are stored — the definition, and the moment you began. Everything
else is worked out from your journal each time, which is what lets a story you flew with D47 closed
be up to date the moment you open it.

### What it does not do yet

- **Branching.** One current beat, and only it can match; a beat that would match out of order is
  ignored rather than banked.
- **Importing** somebody else's adventure. The store file is already the format, so this is a copy
  and a validate when it comes.
- ***Somewhere I've been*** in the editor — D47 keeps no visited-places list, so the two ways in
  are *Here*, which reads your live position, and typing a name.
