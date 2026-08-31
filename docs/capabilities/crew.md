---
title: Crew
group: Ship
nav_order: 103
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">The pilots you have hired, and how to talk to one of them instead of your ship's AI.</p>
<section>
<h2><span class="num">1</span> The name has to be at the front.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="Opening with a crew member's name sends the turn to them; mentioning them later is a question for your ship's AI">
 <rect x="20" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" fill="var(--text)">“Vance, what’s the fighter</text>
 <text x="220" y="102" text-anchor="middle" font-size="16" fill="var(--text)">looking like?”</text>
 <text x="220" y="132" text-anchor="middle" font-size="15" font-weight="700" fill="var(--accent)">→ goes to Vance</text>
 <rect x="460" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="78" text-anchor="middle" font-size="16" fill="var(--text)">“What does Vance think</text>
 <text x="660" y="102" text-anchor="middle" font-size="16" fill="var(--text)">of the fighter?”</text>
 <text x="660" y="132" text-anchor="middle" font-size="15" fill="var(--text-muted)">→ goes to your ship’s AI</text>
 <text x="440" y="196" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">That is a real difference, not a parsing quirk.</text>
 <text x="440" y="226" text-anchor="middle" font-size="15" fill="var(--text-muted)">A bare name works too — it is how you get somebody’s attention. Matching costs nothing and uses no model.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> They are people you hired, not another Guardian core.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="A crew member has no tools, no database and no way to look anything up, unlike a Guardian core">
 <rect x="20" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">A GUARDIAN CORE</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">a million years old,</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">with tools and tables</text>
 <rect x="460" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">SOMEBODY YOU HIRED</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text)">at a station, last week</text>
 <text x="660" y="134" text-anchor="middle" font-size="15" fill="var(--text)">no tools, no database</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">Asked something they cannot see from where they sit, they say so.</text>
</svg>
<p class="body">They speak in their own voice, kept for the session like any other. Unlike the cores, they share the active persona's transcript — the cores cannot know about each other, but the crew and the ship's AI are aboard the same ship and plainly do.</p>
</section>
<section>
<h2><span class="num">3</span> The roster is short because Elite's is.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Elite records a crew member's name, rating and duty state and nothing else — there is no engineer, gunner or navigator">
 <rect x="20" y="36" width="400" height="130" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">WHAT ELITE WRITES</text>
 <text x="46" y="104" text-anchor="start" font-size="15" fill="var(--text)">name · combat rating</text>
 <text x="46" y="130" text-anchor="start" font-size="15" fill="var(--text)">on duty, or off the books</text>
 <text x="46" y="154" text-anchor="start" font-size="15" fill="var(--text-muted)">and that is all of it</text>
 <rect x="460" y="36" width="400" height="130" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="660" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--danger)">WHAT IT DOES NOT</text>
 <text x="486" y="104" text-anchor="start" font-size="15" fill="var(--text)">no engineer</text>
 <text x="486" y="130" text-anchor="start" font-size="15" fill="var(--text)">no gunner</text>
 <text x="486" y="154" text-anchor="start" font-size="15" fill="var(--text)">no navigator</text>
 <text x="440" y="206" text-anchor="middle" font-size="16" fill="var(--text)">Elite’s hired crew are fighter pilots and nothing else, so that is the whole roster.</text>
 <text x="440" y="236" text-anchor="middle" font-size="15" fill="var(--text-muted)">Inventing posts to fill it out would be the confident wrong answer the guardrails exist to prevent.</text>
</svg>
<p class="body">A posting to a particular ship is <em>derived, not reported</em>: Elite's assignment event names no ship, so Directive 47 remembers which hull you were sitting in when you assigned somebody. A roster it has never seen says so, rather than saying you have no crew — those are different answers, and only one of them is true.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="persona.html"><span class="ct">Persona →</span><span class="cd">The voice a crew turn steps aside from, and comes back to.</span></a>
<a class="card" href="ships.html"><span class="ct">Ships →</span><span class="cd">The hulls a posting is remembered against.</span></a>
<a class="card" href="journal.html"><span class="ct">Journal →</span><span class="cd">The four events this whole page is built from.</span></a>
</div>
</div>
</div></div>

## The details

The pilots you have hired, and how to talk to them.

### Ask for it

> "who is aboard"
> "who is on duty"
> "crew roster"

```text
2 pilot(s) on the books:
  Vance Ilo (Expert) — on duty in the fighter bay, assigned aboard Long Way Home
  Ilse Bruhn (Competent) — off duty

Address one of them by name to talk to them directly, for example "Vance Ilo, status".
```

### Talking to one of them

Open with their name and the turn goes to them instead of your ship's AI.

> "Vance, what's the fighter looking like?"

The name has to be at the **front**. "What does Vance think of the fighter" is a question for
your ship's AI *about* Vance, and that is a real difference rather than a parsing quirk.

A bare name works too — it is how you get somebody's attention.

Matching is done without the model, against the names in your journal. That means it costs
nothing, it cannot pick the wrong person, and it does not need a model to know who you are
talking to — only to have them answer.

They speak in their own voice, kept for the session like any other. They are people you hired at
a station, not another Guardian core: they are not a million years old, they have no tools, no
database and no way to look anything up, and asked something they cannot see from where they sit
they will say so.

### What Directive 47 actually knows about your crew

Only what Elite writes down, which is less than you might expect:

| Known | From |
|---|---|
| Name | `CrewHire` |
| Combat rating | `CrewHire`, updated by `NpcCrewRank` |
| On duty or on shore leave | `CrewAssign` |
| Off the books | `CrewFire` |

**There is no engineer, no gunner and no navigator.** Elite's hired crew are fighter pilots and
nothing else, so that is the whole roster. Inventing posts to fill it out would be exactly the
confident wrong answer the anti-invention guardrails exist to prevent.

#### "Per-ship rosters", honestly

Elite's `CrewAssign` names no ship. Your hired crew are a pool that belongs to *you*, and
whichever one is on duty flies in whatever hull you happen to be sitting in.

So the posting shown above is **derived, not reported**: where Directive 47 saw you assign
somebody, it remembers which ship you were in at the time. A ship it never saw an assignment
aboard reports nobody rather than everybody, and crew hired before Directive 47 was watching have
no posting at all until you next reassign them.

If a roster has never been seen, it says that rather than saying you have no crew — those are
different answers, and only one of them is true.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `describe_crew`

Reports the hired pilots, their combat ranks, who is in the fighter bay and which hull each was
assigned aboard. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Addressing is resolved before the turn runs, in `CrewAddressing.Match`, against the closed set of
names the journal supplied — the same shape the keyword router uses. The crew brief replaces the
persona block for that turn only and is restored in a scope's `Dispose`, so a crew turn cannot
leak the wrong persona into the next one.

Crew share the active persona's transcript rather than owning their own. That is the opposite of
the rule for the Guardian cores, and deliberately: the cores cannot know about each other, but
the crew and the ship's AI are aboard the same ship and plainly do.

</details>
