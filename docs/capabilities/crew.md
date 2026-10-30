---
title: Crew
group: Ship
nav_order: 103
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
<p class="intro">Two steps to talking to somebody other than the ship.</p>
<section>
<h2><span class="num">1</span> Hire a pilot in the game. D47 reads your roster.</h2>
<p class="say">Hire a pilot in the game, then say "who is in my fighter bay".</p>
<p class="expect">D47 names the crew from your journal roster, each with their own voice.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">who is in my fighter bay</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Nothing to set up. Your crew comes out of the journal.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Each one keeps their own voice and their own way of talking.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Select them on the Crew channel, then use the comms key.</h2>
<p class="say">Say "comms Vasquez", then hold the comms push-to-talk and say "how is the fighter holding up".</p>
<p class="expect">The crew member answers, and answers every comms question while they stay selected.</p>
<svg viewBox="0 0 880 212" role="img" aria-label="A question on the comms key with a crew member selected, answered in that crew member's own voice">
 <rect x="20" y="20" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="53" font-size="17" fill="var(--text)">how is the fighter holding up?</text>
 <text x="44" y="116" font-size="15" font-weight="700" fill="var(--accent)">[VASQUEZ]</text>
 <text x="180" y="116" font-size="16" fill="var(--text)">Still flying, Commander. Wing took a knock.</text>
 <text x="20" y="150" font-size="16" fill="var(--text-muted)">With CREW · VASQUEZ selected, the comms key reaches them instead of the core.</text>
 <text x="20" y="184" font-size="15" fill="var(--text-muted)">Push-to-talk still reaches the core, even when you start with their name.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Use the name of a crew member you have hired.</p>
<p class="expect">That crew member answers; any other name reaches the core, which says they are not aboard.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="Only the crew you have actually hired can answer.">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Only the crew you have actually hired can answer.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">A name that is not on your roster reaches the core, which will say so rather than invent somebody.</text>
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
<p class="intro">The pilots you have hired, and how to talk to one of them instead of your core.</p>
<section>
<h2><span class="num">1</span> The name has to be at the front.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="Opening with a crew member's name sends the turn to them; mentioning them later is a question for your core">
 <rect x="20" y="40" width="400" height="110" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" fill="var(--text)">“Vance, what’s the fighter</text>
 <text x="220" y="102" text-anchor="middle" font-size="16" fill="var(--text)">looking like?”</text>
 <text x="220" y="132" text-anchor="middle" font-size="15" font-weight="700" fill="var(--accent)">→ goes to Vance</text>
 <rect x="460" y="40" width="400" height="110" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
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
 <rect x="20" y="40" width="400" height="112" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">A GUARDIAN CORE</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">a million years old,</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">with tools and tables</text>
 <rect x="460" y="40" width="400" height="112" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">SOMEBODY YOU HIRED</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text)">at a station, last week</text>
 <text x="660" y="134" text-anchor="middle" font-size="15" fill="var(--text)">no tools, no database</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">Asked something they cannot see from where they sit, they say so.</text>
</svg>
<p class="body">They speak in their own voice, kept for the session like any other. Each keeps their own conversation, the way the carrier's captain does, and the core is told each exchange as one it overheard rather than one it had — it can refer to what was said, but the pilot's answers are never added to its own conversation.</p>
</section>
<section>
<h2><span class="num">3</span> They stay on comms while selected.</h2>
<svg viewBox="0 0 880 212" role="img" aria-label="A selected pilot answers every comms question, and push-to-talk always reaches the core">
 <rect x="20" y="36" width="840" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="70" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">An intercom does not fade</text>
 <text x="440" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">a pilot's line carries as clearly on the last word as the first</text>
 <text x="440" y="160" text-anchor="middle" font-size="16" fill="var(--text)">Every comms question reaches the selected pilot, with no time limit.</text>
 <text x="440" y="188" text-anchor="middle" font-size="15" fill="var(--text-muted)">Push-to-talk always reaches the core.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> The roster is short because Elite's is.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Elite records a crew member's name, rating and duty state and nothing else — there is no engineer, gunner or navigator">
 <rect x="20" y="36" width="400" height="130" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">WHAT ELITE WRITES</text>
 <text x="46" y="104" text-anchor="start" font-size="15" fill="var(--text)">name · combat rating</text>
 <text x="46" y="130" text-anchor="start" font-size="15" fill="var(--text)">on duty, or off the books</text>
 <text x="46" y="154" text-anchor="start" font-size="15" fill="var(--text-muted)">and that is all of it</text>
 <rect x="460" y="36" width="400" height="130" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
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
<a class="card" href="persona.html"><span class="ct">Core →</span><span class="cd">The voice a crew turn steps aside from, and comes back to.</span></a>
<a class="card" href="ships.html"><span class="ct">Ships →</span><span class="cd">The hulls a posting is remembered against.</span></a>
<a class="card" href="journal.html"><span class="ct">Journal →</span><span class="cd">The four events this whole page is built from.</span></a>
<a class="card" href="characters.html"><span class="ct">Characters →</span><span class="cd">People you name yourself, aboard or elsewhere.</span></a>
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

Select one on the Crew comms channel and use the comms key to talk to them directly, for example "comms Vance Ilo".
```

### Talking to one of them

Select them on the Crew [comms channel](comms.md#choose-who-your-comms-key-reaches), then speak
on the comms push-to-talk, and the turn goes to them instead of your core.

> "comms Vance", then on the comms key: "what's the fighter looking like?"

Push-to-talk goes to your core, so "Vance, what's the fighter looking like?" on it is a question
for your core *about* Vance.

They speak in their own voice, kept for the session like any other. They are people you hired at
a station, not another Guardian core: they are not a million years old, they have no tools, no
database and no way to look anything up, and asked something they cannot see from where they sit
they will say so.

An intercom does not fade the way the carrier's captain does over distance. Every comms question
reaches the same pilot while they stay selected. The core is told each exchange as one it overheard,
so it can refer to what was said, but a pilot's answers are never added to the core's own
conversation.

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

#### "Per-ship rosters", and what they actually are

Elite's `CrewAssign` names no ship. Your hired crew are a pool that belongs to *you*, and
whichever one is on duty flies in whatever hull you happen to be sitting in.

So the posting shown above is **derived, not reported**: where Directive 47 saw you assign
somebody, it remembers which ship you were in at the time. A ship it never saw an assignment
aboard reports nobody rather than everybody, and crew hired before Directive 47 was watching have
no posting at all until you next reassign them.

If a roster has never been seen, it says that rather than saying you have no crew — those are
different answers, and only one of them is true.

### A picture for each pilot

Each hired pilot's row on Fleet › Crew has **Change picture** and **Use the default**. The picture is kept
under the pilot's game crew id, not their name, so two pilots with the same name keep separate pictures. It
shows beside that pilot's turns on the Conversation page. A pilot you fire keeps their file, and a rehire
with the same crew id gets it back.

### People of your own

This page covers the pilots you hire in the game. People you name yourself, aboard or elsewhere, are
characters: see [Characters](characters.md). Crew seats from earlier versions became characters the
first time this version loaded, one per name, aboard, with the seat's role in their notes, and their
callouts went back to the core. The old seat file is kept as `data\crew-seats.migrated.json`.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### The pilot speaks when the fighter moves

The pilot in the fighter bay speaks, unprompted, when the fighter launches, docks, is destroyed or is
rebuilt. The lines are fixed phrases, not model output, and state only what the journal event says.
With no pilot assigned nothing is said. See [Fighter pilot](callouts.html#fighter).

#### `describe_crew`

Reports the hired pilots, their combat ranks, who is in the fighter bay and which hull each was
assigned aboard. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

`CrewLine` answers a comms turn while the Crew channel selects a pilot by `CrewId`, the way
`CaptainLine` answers for the captain on the Carrier channel, with no tool offered on any of those
turns.

Each pilot keeps their own transcript, one per `CrewId`, rather than sharing the core's. The
core is told each exchange as overheard rather than reading it from a shared history, so a
pilot's words are never mistaken for its own.

</details>
