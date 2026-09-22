---
title: Help
group: Foundation
nav_order: 100
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and the class decides behaviour, not just
  appearance. HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under
  that class would silently become what the in-app panel draws on this page. The docs site styles
  the two identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to finding out what something does.</p>
<section>
<h2><span class="num">1</span> Ask, in the words you already have.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what can you do about engineering</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"how does the checklist work" — "what is the gap page"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">It answers from these pages, so the app and the site cannot disagree.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Or press HELP on any card.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Any settings card">
 <rect x="20" y="16" width="840" height="212" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Any settings card</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Push-to-talk</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">Right Shift</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">HELP</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text-muted)">opens this page at that row</text>
 <text x="44" y="222" font-size="15" fill="var(--text-muted)">HELP takes you to the section for that exact row, not to the top of a page.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="Help is about the page, not the subject.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Help is about the page, not the subject.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">It explains D47, not Elite. For what a Guardian beacon does, ask the model instead.</text>
</svg>
</section>
</div></div>
</details>

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.

  In the panel this page is also the front door: HelpPageView appends a generated list of every
  page that has a band, so nothing here should try to list them by hand.
-->
<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">How to ask what something does, and where the answer comes from.</p>
<section>
<h2><span class="num">1</span> Two ways to ask, and both mean “this page”.</h2>
<svg viewBox="0 0 880 254" role="img" aria-label="The mark in the corner and the spoken word both open help for the page you are on">
 <rect x="20" y="26" width="390" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <circle cx="120" cy="82" r="26" fill="none" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="120" y="93" text-anchor="middle" font-size="30" font-weight="800" fill="var(--accent)">?</text>
 <text x="176" y="76" font-size="18" font-weight="700" fill="var(--text)">press the mark</text>
 <text x="176" y="104" font-size="15" fill="var(--text-muted)">top right of the panel</text>
 <rect x="450" y="26" width="410" height="112" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="655" y="70" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">or just say so</text>
 <text x="655" y="100" text-anchor="middle" font-size="16" fill="var(--accent)">“help”  ·  “what is this”</text>
 <text x="655" y="124" text-anchor="middle" font-size="15" fill="var(--text-muted)">no model needed, no network</text>
 <text x="440" y="184" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">Both open help for whatever you are looking at.</text>
 <text x="440" y="216" text-anchor="middle" font-size="16" fill="var(--text-muted)">Saying it matters more than pressing it: with a headset on, your hands are on the stick</text>
 <text x="440" y="240" text-anchor="middle" font-size="16" fill="var(--text-muted)">and there is nothing to point at the mark with.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> It sits on top, and “back” puts it away.</h2>
<svg viewBox="0 0 880 236" role="img" aria-label="Help covers the page and back returns to exactly where you were">
 <rect x="20" y="30" width="300" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="170" y="80" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">the page</text>
 <text x="170" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">right where you left it</text>
 <line x1="332" y1="74" x2="358" y2="74" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="372,74 356,66 356,82" fill="var(--accent-muted)"/>
 <text x="415" y="60" text-anchor="middle" font-size="14" fill="var(--text-muted)">help</text>
 <line x1="372" y1="96" x2="346" y2="96" stroke="var(--accent)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="332,96 348,88 348,104" fill="var(--accent)"/>
 <text x="415" y="122" text-anchor="middle" font-size="14" fill="var(--accent)">back</text>
 <rect x="460" y="30" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="80" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">help, over the top of it</text>
 <text x="660" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">nothing underneath moves</text>
 <text x="440" y="192" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">“Back” is the same word that leaves anything else.</text>
 <text x="440" y="222" text-anchor="middle" font-size="16" fill="var(--text-muted)">Say it, press the breadcrumb, or use the controller button — all three are one gesture.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> The pictures are here. The detail is on the site.</h2>
<svg viewBox="0 0 880 244" role="img" aria-label="The panel draws the illustrated half and the website carries the reference half">
 <rect x="20" y="26" width="400" height="130" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="68" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">IN HERE</text>
 <text x="220" y="100" text-anchor="middle" font-size="16" fill="var(--text-muted)">the pictures, in your theme</text>
 <text x="220" y="128" text-anchor="middle" font-size="16" fill="var(--text-muted)">offline, and inside the headset</text>
 <rect x="460" y="26" width="400" height="130" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="68" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">ON THE SITE</text>
 <text x="660" y="100" text-anchor="middle" font-size="16" fill="var(--text-muted)">the tables, the settings, the</text>
 <text x="660" y="128" text-anchor="middle" font-size="16" fill="var(--text-muted)">reasoning and the working</text>
 <text x="440" y="200" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">Every page in here ends with a way through to its long form.</text>
 <text x="440" y="230" text-anchor="middle" font-size="16" fill="var(--text-muted)">Not every page has pictures yet. The list below is the ones that do.</text>
</svg>
<p class="body">And a page nobody has illustrated yet still has its mark: pressing it, or saying so, brings you here instead — one level broader than you asked for, rather than nothing at all.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="../conversation.html"><span class="ct">Talking to Directive 47 →</span><span class="cd">What happens between your question and its answer.</span></a>
<a class="card" href="../index.html"><span class="ct">Overview →</span><span class="cd">What D47 is, in three pictures.</span></a>
<a class="card" href="settings.html"><span class="ct">Settings →</span><span class="cd">Every switch, and the four the model cannot touch.</span></a>
</div>
</div>
</div></div>

## The details

What Directive 47 can actually do, answered from its own list of capabilities rather than from
the model's memory.

### Ask for it, out loud

> "what can you do"
> "what are your capabilities"
> "tell me about the voice capabilities"

Said or typed by you, this walks a spoken map one level at a time — a small question, then
another, down to one feature. It never needs a model or a network, because it is projected from
the same registry that decides what Directive 47 can actually do:

```text
6 areas: Flying, Trading and goals, Ship and engineering, Talking and voices, Seeing what
happened, and Settings and safety. Which one?
```

Answer with an ordinal ("the second one"), the area's own name, or enough of it to be unambiguous,
and it goes one level deeper — more areas, or a feature, whichever the map holds there:

```text
Operate the landing gear, lights, cargo scoop, hardpoints and the frame shift drive. Say 'put the
gear down' or 'retract hardpoints'. It has a page on the panel called Flight and navigation.
```

Say something the drill was not expecting and it drops the question and answers normally, rather
than insisting on an answer to a menu you have stopped answering.

### "How do I ..."

> "how do I plot a course"
> "how can I get my ship engineered"
> "how would I turn off silent running"

This lands on the one feature the words point to, from the same spoken map — never on the
model — matching a leaf by the content words a goal shares with its name, its sentence and the
phrases that reach its capability:

```text
Put a system name on your clipboard, and try to plot a course to it. Say 'plot a course to
Shinrarta Dezhra' or 'copy that system name'. It has a page on the panel called Navigation.
```

Where two or three leaves fit equally well, it asks which, the same way a near miss does:

```text
Two ways: Journal, or Commander's log. Which one?
```

Where nothing fits, a configured model answers instead; without one, this falls back to the top
level of the spoken map rather than saying it has no way to work it out.

### Asked by the model

The model itself is never handed the drill — a Commander answering "the second one" a minute
later is not something a tool call can tell apart from a fresh question, so the drill is reachable
only by the router and the panel. What the model gets instead is the plain projection below, with
an optional area name:

```text
I have 9 capabilities, in these areas:
  Flying — Everything about handling the ship in the moment.
  Trading and goals — Finding what to do next, and what a place is worth.
Ask about an area by name for the detail.
```

Asked for an area, it lists what is in it — the phrases that actually work, your words rather than
internal names, because a list you cannot say out loud is a list you cannot use:

```text
Speech: Speak replies aloud, mark each loop state with its own cue, and stop on command.
  Try: "stop"; "be quiet"; "shut up"
Callouts: Speak up about danger, fuel, route progress and arrivals without waiting to be asked.
  Try: "what are you watching for"; "stop calling things out"; "start calling things out"
```

Ask for an area that does not exist and it names the real ones instead of refusing. If you asked
for the wrong area you want the right one, and this is the moment Directive 47 knows both:

```text
I have no area called "Navigation". I have: Flying, Trading and goals, Ship and engineering,
Talking and voices, Seeing what happened, Settings and safety.
```

### What to actually say

Asked what can be done, the model reads it here rather than guessing. Asked what to *say* for a
goal — "how do I choose the next system in the neutron jump route" — it calls a second tool, which
answers from the same phrase book the router itself matches against, grouping two phrases that
reach the same thing:

```text
Plot the next stop on a stored route plan — the Neutron Plotter's waypoints, a Road to Riches
loop's stops, or a trade run's stops — through the galaxy map. Skips a stop whose system is the
one the Commander is already in. Say 'plot next neutron jump', 'plot the next neutron jump', or
'next neutron jump'. Reaches the hyperspace jump. Say 'jump to the next system'. Reaches the next
system in the route. Say 'next system', 'target the next system', or 'target the next system in
route'.
```

Where nothing in the phrase book matches, it says so plainly rather than claiming the goal cannot
be reached at all — the phrase book not matching is not the same fact as D47 not being able to do
it.

### Why it will not make things up

**The model is never asked what Directive 47 can do.** Ask a model to describe its own abilities
and it produces a fluent, confident, partly invented list — and you have no way to tell which
half is which until you ask for something that turns out not to exist.

So the answer is built from the actual list of registered capabilities. It *cannot* name one that
does not exist, because it is assembled from the ones that do.

### The order it reads them in

Within a group, whichever you have used most comes first, so the things you actually reach for
rise to the top of the list. Groups themselves stay in a fixed order — a spoken list whose
headings move between askings is harder to follow than one that does not.

Reaching for something counts even when it fails, since a capability you keep trying is worth
hearing about early on the days it is not working.

The count covers the current session only. It starts fresh each time Directive 47 does, rather
than claiming a usage history it does not have.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `get_capabilities`

Projects the registry: the top-level areas of the spoken map, the capabilities under one of them,
and their declared example phrasings. The optional area name expands one of them.

```json
{"type":"object","properties":{"group":{"type":"string","description":"Optional group name to expand, such as Voice. Omit for the overview."}},"required":[],"additionalProperties":false}
```

The keyword router only routes to a tool with no *required* parameters, because it deliberately
does not extract argument values from free text — a router that guesses at arguments is one that
calls the right tool with the wrong ones. The router originally refused any tool with parameters
at all, which made "what can you do" match nothing: the one capability whose entire purpose is
being answerable without the model was unreachable without it, purely for offering a refinement
it does not need. The rule is now "no required parameters", and the router invokes with empty
arguments.

#### `find_phrase`

Finds the phrase-book entries whose words reach the goal — the model-free router's own vocabulary,
matched the same way the spoken "how do I" answer above matches a goal, but per phrase rather than
per leaf. Two phrases that reach the same thing are said together.

```json
{"type":"object","properties":{"goal":{"type":"string","description":"What the Commander wants to do, in a few plain words."}},"required":["goal"],"additionalProperties":false}
```

#### `drill_capabilities`

No parameters. Opens the top level of the spoken map as a standing offer, read against whatever
the Commander says next.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Protected: reachable from the router and the panel, refused to the model. A model call answering
"what can you do" opens no offer of its own — nothing distinguishes its next tool call from a
Commander's next sentence, so a model-opened offer would be captured by whatever the model asked
for after it, not by the Commander.

</details>
