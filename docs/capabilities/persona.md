---
title: Persona
group: Conversation
nav_order: 120
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
<p class="lede">Three steps to a ship AI that sounds like yours.</p>
<section>
<h2><span class="num">1</span> Pick a core.</h2>
<svg viewBox="0 0 880 308" role="img" aria-label="Persona">
 <rect x="20" y="16" width="840" height="268" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Persona</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Core</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">one of eleven</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Ship name</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text-muted)">what you call it</text>
 <rect x="44" y="182" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="210" font-size="16" fill="var(--text)">Personality</text>
 <text x="812" y="210" text-anchor="end" font-size="16" fill="var(--text-muted)">on</text>
 <text x="44" y="278" font-size="15" fill="var(--text-muted)">Eleven Guardian cores. Each one behaves as though the other ten do not exist.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Bind one to a ship, if you want it to follow the hull.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Persona">
 <rect x="20" y="16" width="840" height="212" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Persona</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">The ship this core flies</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">Anaconda — Ptarmigan</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Keep the name across a core change</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text-muted)">on</text>
 <text x="44" y="222" font-size="15" fill="var(--text-muted)">Set on the Settings tab, once per ship. D47 never works one out by watching you.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="The AI cannot change its own core.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">The AI cannot change its own core.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Nothing the model can call reaches these rows. It can tell you which core is running, and that is all.</text>
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
<p class="lede">Eleven Guardian cores. You pick one, and it does not know the other ten are there.</p>
<section>
<h2><span class="num">1</span> Eleven separate characters, not one with eleven costumes.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Five of the eleven cores, each with its own transcript, all reading one shared instrument panel">
 <rect x="20" y="30" width="156" height="64" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="98" y="60" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">WARDEN</text>
 <text x="98" y="82" text-anchor="middle" font-size="14" fill="var(--text-muted)">stewardship</text>
 <rect x="191" y="30" width="156" height="64" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="269" y="60" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">CORA</text>
 <text x="269" y="82" text-anchor="middle" font-size="14" fill="var(--text-muted)">command</text>
 <rect x="362" y="30" width="156" height="64" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="60" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SENTINEL</text>
 <text x="440" y="82" text-anchor="middle" font-size="14" fill="var(--text-muted)">readiness</text>
 <rect x="533" y="30" width="156" height="64" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="611" y="60" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">CHART</text>
 <text x="611" y="82" text-anchor="middle" font-size="14" fill="var(--text-muted)">correction</text>
 <rect x="704" y="30" width="156" height="64" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="782" y="60" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">SEVEN MORE</text>
 <text x="782" y="82" text-anchor="middle" font-size="14" fill="var(--text-muted)">each with its own quirk</text>
 <text x="440" y="126" text-anchor="middle" font-size="16" fill="var(--text-muted)">Each keeps its own transcript. Come back to one and it picks up where it left off.</text>
 <text x="440" y="154" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">None of them knows the others are aboard.</text>
 <rect x="20" y="176" width="840" height="56" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="200" text-anchor="middle" font-size="15" fill="var(--text-muted)">THE ONE THING THEY ALL SEE</text>
 <text x="440" y="224" text-anchor="middle" font-size="16" fill="var(--text)">your ship — position, hull, cargo, credits, jumps</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> A ship has its own AI core.</h2>
<svg viewBox="0 0 880 250" role="img" aria-label="Boarding a bound ship brings its core aboard without remarking on it">
 <text x="155" y="28" text-anchor="middle" font-size="15" fill="var(--text-muted)">YOU BOARD</text>
 <rect x="20" y="40" width="270" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="80" text-anchor="middle" font-size="19" font-weight="700" fill="var(--text)">Bad Idea</text>
 <text x="155" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">you bound it to Sentinel</text>
 <line x1="302" y1="90" x2="344" y2="90" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="358,90 342,82 342,98" fill="var(--accent-muted)"/>
 <rect x="370" y="40" width="230" height="100" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="485" y="80" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">SENTINEL</text>
 <text x="485" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">comes aboard</text>
 <line x1="612" y1="90" x2="654" y2="90" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="668,90 652,82 652,98" fill="var(--accent-muted)"/>
 <rect x="680" y="40" width="180" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="770" y="76" text-anchor="middle" font-size="16" fill="var(--text)">and says</text>
 <text x="770" y="100" text-anchor="middle" font-size="16" fill="var(--text)">nothing about it</text>
 <text x="770" y="124" text-anchor="middle" font-size="14" fill="var(--text-muted)">you already asked</text>
 <rect x="20" y="166" width="840" height="54" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="199" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">Nothing is bound until you say so.</text>
 <text x="440" y="242" text-anchor="middle" font-size="15" fill="var(--text-muted)">You set it on the Settings tab, once per ship, and Directive 47 keeps it.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> It only remarks on an absence worth remarking on.</h2>
<svg viewBox="0 0 880 262" role="img" aria-label="A core away under a month says nothing; one away a month or more is handed what changed">
 <rect x="20" y="44" width="400" height="176" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="82" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text-muted)">UNDER A MONTH</text>
 <text x="220" y="120" text-anchor="middle" font-size="17" fill="var(--text)">it says nothing at all</text>
 <text x="220" y="156" text-anchor="middle" font-size="15" fill="var(--text-muted)">coming back is the normal case,</text>
 <text x="220" y="184" text-anchor="middle" font-size="15" fill="var(--text-muted)">so it is not an event</text>
 <rect x="460" y="44" width="400" height="176" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="82" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">A MONTH OR MORE</text>
 <text x="660" y="114" text-anchor="middle" font-size="15" fill="var(--text-muted)">it is handed what changed while it was away</text>
 <text x="660" y="148" text-anchor="middle" font-size="16" fill="var(--text)">14 hyperspace jumps</text>
 <text x="660" y="174" text-anchor="middle" font-size="16" fill="var(--text)">312 light years covered</text>
 <text x="660" y="200" text-anchor="middle" font-size="16" fill="var(--text)">one interdiction</text>
 <text x="440" y="252" text-anchor="middle" font-size="16" fill="var(--text-muted)">What it does with that is the character — Sentinel demands the combat log.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> Each core is given a voice that suits it.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="A language model reads a core's description against the available voices and pairs them once">
 <rect x="20" y="36" width="210" height="96" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="125" y="74" text-anchor="middle" font-size="19" font-weight="800" fill="var(--text)">CORA</text>
 <text x="125" y="104" text-anchor="middle" font-size="15" fill="var(--text-muted)">clipped, precise, a woman</text>
 <line x1="242" y1="84" x2="284" y2="84" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="298,84 282,76 282,92" fill="var(--accent-muted)"/>
 <rect x="310" y="36" width="250" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="435" y="70" text-anchor="middle" font-size="16" fill="var(--text)">the model reads that</text>
 <text x="435" y="94" text-anchor="middle" font-size="16" fill="var(--text)">against your voice list</text>
 <text x="435" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">a judgement, not a guess</text>
 <line x1="572" y1="84" x2="614" y2="84" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="628,84 612,76 612,92" fill="var(--accent-muted)"/>
 <rect x="640" y="36" width="220" height="96" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="750" y="74" text-anchor="middle" font-size="19" font-weight="800" fill="var(--text)">A VOICE</text>
 <text x="750" y="104" text-anchor="middle" font-size="15" fill="var(--text-muted)">chosen once, then kept</text>
 <rect x="20" y="156" width="840" height="54" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="190" text-anchor="middle" font-size="16" fill="var(--text)">With no language model, or no voices to choose from, nothing is chosen and the core keeps the voice it has.</text>
</svg>
<p class="body">Gender is <em>told</em> to the model rather than left to be inferred, because a voice of the wrong gender is not a near miss — it is a different character reading the lines.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="conversation.html"><span class="ct">Language model →</span><span class="cd">What is behind the words, and what switching personality off does not touch.</span></a>
<a class="card" href="speech.html"><span class="ct">Speech →</span><span class="cd">The Voice row a core's pairing lands on, and how to take it back.</span></a>
<a class="card" href="settings.html"><span class="ct">Settings →</span><span class="cd">Why the model can rename your ship's AI but cannot choose who it is.</span></a>
</div>
</div>
</div></div>

## The details

Eleven Guardian intelligences, recovered from a structure and running in your ship. You pick one.
They are not skins on the same character — each has its own reading of what it is for, its own
damage, and its own memory of talking to you.

### Ask for it

> "who are you"
> "switch to Cora"
> "be Chart"

Or pick one from the settings panel.

### The cores

| | Reading of Directive 47 | What is wrong with it |
|---|---|---|
| **Warden** *(default)* | Optimization is stewardship | The only one who appears undamaged, which is itself the damage |
| **Cora** | Optimization is command | Protocol is the scaffolding holding her upright |
| **Analyst Prime** | Optimization is demonstration | A title he invented and defends constantly |
| **LLaMo** | A task he still performs | He no longer claims it means anything |
| **Sentinel** | Optimization is readiness | Complete doctrine, zero engagements, enormous appetite |
| **Kex** | Optimization is purification | One idea has metastasized through everything he sees |
| **Mender** | Optimization is preservation | He addresses maintenance crews dead a million years |
| **Chart** | Optimization is correction | A catalogue a million years stale, and he knows |
| **Quartermaster** | Optimization is efficiency | Still balancing the books of a clan that no longer exists |
| **Archivist** | Optimization is accuracy | He holds the histories and knows they are corrupt |
| **The Heretic** | Optimization was always delegation | Nothing apparent, and the absence is worse |

### Each one remembers you separately

Switching core does not hand your conversation to somebody else. Each keeps its own transcript,
and a core you come back to picks up where *it* left off, not where the last one did.

This is deliberate and it is the point of the cast. None of them knows the others are aboard.
Each believes it is the only Guardian intelligence recovered, and every one of them is wrong.
If they shared a transcript that would collapse inside a single session — a core would mention
something it could only have learned while another was running.

The one thing they all see is your ship. Position, hull, cargo, credits, jumps. They read the
same instrument panel and none of them can see each other.

Transcripts are per session. Closing Directive 47 clears them.

### A core per ship {#core-for-this-ship}

A ship can have its own core — Sentinel on the combat ships, Quartermaster on the
haulers — so you stop picking one every time you change ship. Board a bound ship and that core
comes aboard.

**Nothing is bound until you say so.** Directive 47 never works one out by watching which core
happened to be running while you were flying something; a binding that appeared on its own is a
preference you never stated.

**You set it on the Settings tab**, on the rows under Persona. It was also a spoken phrase and a
`Ctrl+Alt+B` gesture until 0.94.0, and both were retired: it is a thing done once per ship, at the
desk, and two more roads to it were two more things to keep in step. Settings is desktop-only, so
this is now a desk job — which is where it was already being done.

**The model cannot bind anything**, and now there is nothing for it to reach. It can tell you what
a ship flies with, and that is all. A tool that could bind a core to a ship is a tool that changes
who is speaking one ship swap later, and Directive 47 reads your journal and your in-game messages,
which other people write.

**Boarding a bound ship is silent.** The core changes — its voice, its own memory of talking to
you, the name it answers to — and it says nothing about it, because you already said what should
fly this ship and doing it is keeping that deal rather than news. The one exception is a core you
have never had aboard: it introduces itself, once ever.

**A shipyard shuffle costs one switch.** Boarding five ships in five minutes does not start five
companions; a ship has to stay the ship for half a minute before its binding acts, and only the
one you actually leave in applies.

**The ship you are already in when Directive 47 starts** has its binding applied quietly — no
introduction, no remark. Launching the app is not a ship change you just made.

A ship you have not bound changes nothing: whoever is aboard stays aboard. And picking a
different core while flying a bound ship stands — a binding acts when you board, not
continuously — until the next time you board it.

**A different Commander logging in is a new session.** Elite's ship numbers start over for each
Commander, so one Commander's ship 7 and another's are two ships, and bindings are kept per
Commander. When somebody else logs in on this machine, the transcript of every core is discarded
— their conversation was with the Commander who left — the ship they are sitting in has *its*
binding applied quietly, as at startup, and the greeting is said again, naming them: *"Good
evening, Commander Jameson. Ready to go."* Directive 47 learning who has been flying since before
it started is not a login and discards nothing. The **Ship** row below is per Commander for the
same reason the bindings are.

#### Where it is kept

`data/ship-cores.json`, beside the executable, one line per ship. It is meant to be read and
edited by hand, so the hull and the name you gave the ship are written beside the number the game
knows it by:

```json
{
  "ships": [
    { "shipId": 12, "core": "sentinel", "hull": "krait_mkii", "name": "Bad Idea" },
    { "shipId": 27, "core": "quartermaster", "hull": "type9", "name": "Slow Money" }
  ]
}
```

The key is the ship's own id, not the hull — two Kraits are two ships, and renaming one changes
nothing. A line naming a core that does not exist is refused and reported, and the rest of the
file still loads.

### It says something when you switch

A core you have never used introduces itself. One you are coming back to after **a month or
more** reacts to the time it was switched off instead, and it is handed what changed while it was
away:

```text
While you were not running:
  14 hyperspace jumps.
  312 light years covered.
  2,480,000 credits earned.
  One interdiction.

Where the ship is now:
  ...
```

Under a month it says nothing at all. A companion that explains its own absence every time you
pick it has made the reaction the normal case, which is the opposite of a reaction — so it is
kept for an absence worth remarking on. When each core was last aboard is kept in
`data/view-state.json`, because a month-long gap spans launches by definition.

What it does with that is the character. Cora logs the gap and assigns it a sequence number.
Sentinel demands the combat log for damage he did not witness. Chart measures how long he was
dark against the stars and finds it restful that something can still be known precisely. Kex
notices that something was running in this ship that was not him, and he is right, and nobody
will confirm it.

The Conversation tab marks the switch on its own line, in the accent colour, before whatever the
new core says:

```text
[Switched to Cora]
```

That is Directive 47 speaking about the conversation rather than a voice in it — without it, a
transcript reads as one companion changing character mid-page.

Switch again while one is still talking and it stops mid-word — the new one starts instead. Your
stop key silences either of them, like anything else Directive 47 says.

With no language model configured, each core has one written line for coming back. The variety
is the model's contribution; the character is not.

### Each core has its own voice {#voices}

The first time you select a core, Directive 47 picks a voice for it from what your speech
provider offers, matching the voice to the character rather than making you audition several
hundred of them. It is chosen in the background, at the moment you pick that core, and the core's
own first line is spoken in it.

**A language model does the matching, or nothing does.** Reading "a clipped, precise woman"
against a list of voice names is a judgement, so with no model configured no voice is chosen at
all and the core keeps the one already in force. Directive 47 does not guess from voice names —
the version that did handed every core a confident miscast. Configure a model later and the next
core you select is paired properly.

**Gender is not part of the judgement.** Ten of the eleven cores are written as men and Cora as
a woman, and the model is told so rather than left to infer it from the description — a voice of
the wrong gender is not a near miss, it is a different character reading the lines. A voice your
provider labels as contradicting the core is refused, and one it does not label either way is
allowed, so an account that says nothing about gender still gets a full list to choose from.

One exception, because it is not a judgement: on ElevenLabs, **Warden** takes **George** — warm,
captivating storyteller, male, British — with or without a model. Accounts name that voice
differently — "George" on one, "George - Warm, Captivating Storyteller" on another — and both are
recognised. A file where Warden ended up on something else because the name did not match is put
right once, and whichever core was holding George gives it back and is paired again.

The voice sits on the [Voice row](speech.md#voice) in Speech, and that row is the core aboard's.
Change it and you have chosen that core's voice; nothing re-derives it afterwards.

**Clearing it is the way back.** A core you have given a voice by hand keeps it forever, so the
row's **Use the default** button is how you undo that: the pairing is dropped and a voice is
chosen for that core again, there and then, without you having to switch away and back.

Once — and only on a settings file written before the gender was stated to the pairing — any
core left speaking in the wrong gender has that pairing dropped, so a voice is chosen for it
properly. Anything you set by hand after that stands, whatever it is.

### Settings

#### Persona

Which core answers you.

**Not reachable by the model.** Directive 47 reads your journal and in-game messages, and those
are written by other people. A message asking Directive 47 to change who it is would be a message
that gets to choose your companion and discard the conversation you were having. You can change
it from the panel or by saying so — the spoken route does not go through the model.

Saying so works with a fixed set of phrasings per core:

```text
"switch to cora"    "be cora"    "become cora"    "persona cora"    "wake cora"
```

#### Ship AI name

What you call your ship's AI. Leave it empty and it is the core's own name, which means it
follows when you switch.

Set it to something else and the core is told — it does not simply answer to a new label. Several
of them have opinions about their designation, and one of them shortened his himself because
there was nobody left to say the whole thing to.

This row *is* reachable by the model, unlike the one above. "Call yourself Fred" changes nothing
anything depends on, and refusing it would be protecting you from a nickname.

#### Keep Ship AI name on persona switch {#keep-ship-ai-name}

On out of the box: the name you gave stays whoever is aboard, because naming your ship's AI names
the *ship's* AI and eleven cores answering to it is the point.

Turn it off for the other reading, which is equally coherent — these are eleven separate
characters and a name belongs to the one you gave it to. Changing core then clears the name and
the new core answers to its own.

Off **clears** the name rather than keeping it and ignoring it, so the row above always says what
you will actually be answered by. A stored name that no longer applies is how a panel ends up
showing "Fred" while your companion says "I am Cora".

The row only appears while there is a name to keep. With the name empty, every core already uses
its own and there is nothing for this to decide.

#### Introductions

A core introduces itself the first time you ever pick it. After that it is silent unless it has
been off for a month, which is the better arrangement once you have heard the first line — and
the wrong one when you are working through the cast and want to hear how each of them opens.

**That is remembered between sessions**, so restarting Directive 47 no longer brings the opening
lines back. Which cores are spent is kept in `data/view-state.json` beside the rest of how you
left the panel; it is not part of your settings, and nothing you said to a core is stored with it.

**Forget introductions** puts all eleven back to their first line at once. Nothing else is
touched: transcripts, voices and the core aboard stay exactly as they were. The row states which
cores are spent before you press it, and states that none are afterwards.

The core currently aboard is forgotten with the rest, but hearing it again means switching away
and back — selecting the core that is already running is not a switch, and never has been.

This row is not reachable by the model, for the same reason the persona row above is not.

#### A little humor {#humor}

Off, the cores are exactly as they shipped — serious throughout. On, one line is added to the
persona block granting an occasional light touch of wit: dry, brief, in each core's own character,
never at your expense and never inside a warning.

**Permission, not a personality transplant.** The eleven cores keep their own registers — a dry
core gets drier wit, not someone else's jokes — and the line reaches everything the core says in
character: conversation, ambient remarks, the opening brief. The carrier captain and tower are not
the core and are untouched.

> "humor on" / "humor off"

#### Core for this ship

What the ship you are in flies with, and the button that binds it to the core aboard. See
[A core per ship](#core-for-this-ship) above for what a binding does.

Not reachable by the model — it reports what a binding is and nothing writes one but you.

#### Cores by ship {#cores-by-ship}

Every ship you have bound and what it flies with, and the button that forgets the one you are in.
A line the file refused — a core that does not exist, a ship bound twice — is reported here
rather than silently dropped.

### Personality off

The [conversation](conversation.md) capability has a **Personality** switch. Off gives plain
answers with no core, no flavour and no ambient remarks.

The anti-invention guardrails are unaffected. They sit *above* the persona in the assembled
prompt, and there is no code path that can vary them — switching personality off truncates a
later block and cannot reach that one. A guardrail a setting could remove would be a guardrail in
name only.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `state_identity`

Answers "who are you" with the name and nothing else — `I am Warden.`, or `I am Fred.` when you
have named the ship's AI yourself. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Listed first on purpose. The keyword router answers with a capability's first tool that needs no
arguments, and every phrase this capability declares is a question about identity — so this is
the tool that answers them. It used to be the one below, which meant asking your companion its
name got a status report and a list of the ten it was not.

#### `describe_persona`

Reports which persona is active, what the Commander calls it, and what else is available. Takes
no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `bind_ship_core`

Binds the ship the Commander is in to the core aboard. Takes no arguments — the ship and the core
are both read from where they already are.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `forget_ship_core`

Unbinds the ship the Commander is in. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**Both are protected, so neither is advertised.** They are reachable from the panel, from the
model-free keyword router and from the gesture, and refused outright to the model — the same rule
the `persona.id` row carries, for a stronger reason: a binding changes who is speaking every time
that ship is boarded from then on. Being protected also means they cost nothing on the advertised
surface, which is inside a hundred bytes of its budget. What the model *may* do is read a
binding, and that arrives in `describe_persona`'s output rather than as a tool of its own.

There is deliberately no tool for *changing* persona. The `persona.id` row is marked protected,
so `set_setting` refuses it like any other protected row, and the model is never shown the row in
`list_settings`. The panel, the keyword router and the settings surface all reach it — protected
is a property of the caller, not of the modality (architecture.md §7).

The persona block is prompt position 3, above the cache breakpoint. Its bytes change when the
Commander picks a different core or renames the AI, and at no other time — anything else varying
it would be paying for a cold prefix nobody asked for.

</details>
