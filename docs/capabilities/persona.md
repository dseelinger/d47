---
title: Persona
group: Conversation
nav_order: 120
---

Eleven Guardian intelligences, recovered from a structure and running in your ship. You pick one.
They are not skins on the same character — each has its own reading of what it is for, its own
damage, and its own memory of talking to you.

## Ask for it

> "who are you"
> "switch to Cora"
> "be Chart"

Or pick one from the settings panel.

## The cores

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

## Each one remembers you separately

Switching core does not hand your conversation to somebody else. Each keeps its own transcript,
and a core you come back to picks up where *it* left off, not where the last one did.

This is deliberate and it is the point of the cast. None of them knows the others are aboard.
Each believes it is the only Guardian intelligence recovered, and every one of them is wrong.
If they shared a transcript that would collapse inside a single session — a core would mention
something it could only have learned while another was running.

The one thing they all see is your ship. Position, hull, cargo, credits, jumps. They read the
same instrument panel and none of them can see each other.

Transcripts are per session. Closing Directive 47 clears them.

## A core per ship {#core-for-this-ship}

A ship can remember the core that flies it — Sentinel on the combat ships, Quartermaster on the
haulers — so you stop picking one every time you change ship. Board a bound ship and that core
comes aboard.

**Nothing is bound until you say so.** Directive 47 never works one out by watching which core
happened to be running while you were flying something; a binding that appeared on its own is a
preference you never stated. Sitting in the ship, say it, press it, or use the gesture:

```text
"remember this core for this ship"    "you fly this ship"    "this ship flies with you"
"forget this ship's core"             "this ship has no core"
```

`Ctrl+Alt+B` does the same thing, and works while Elite has the foreground — which is when you
will want it. Pressing it again with that same core already bound takes the binding back.

**The model cannot bind anything.** It can tell you what a ship flies with, and that is all. A
tool that could bind a core to a ship is a tool that changes who is speaking one ship swap later,
and Directive 47 reads your journal and your in-game messages, which other people write.

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

### Where it is kept

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

## It says something when you switch

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

## Each core has its own voice {#voices}

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

## Settings

### Persona

Which core answers you.

**Not reachable by the model.** Directive 47 reads your journal and in-game messages, and those
are written by other people. A message asking Directive 47 to change who it is would be a message
that gets to choose your companion and discard the conversation you were having. You can change
it from the panel or by saying so — the spoken route does not go through the model.

Saying so works with a fixed set of phrasings per core:

```text
"switch to cora"    "be cora"    "become cora"    "persona cora"    "wake cora"
```

### Ship AI name

What you call your ship's AI. Leave it empty and it is the core's own name, which means it
follows when you switch.

Set it to something else and the core is told — it does not simply answer to a new label. Several
of them have opinions about their designation, and one of them shortened his himself because
there was nobody left to say the whole thing to.

This row *is* reachable by the model, unlike the one above. "Call yourself Fred" changes nothing
anything depends on, and refusing it would be protecting you from a nickname.

### Keep Ship AI name on persona switch {#keep-ship-ai-name}

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

### Introductions

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

### Core for this ship

What the ship you are in flies with, and the button that binds it to the core aboard. See
[A core per ship](#core-for-this-ship) above for what a binding does.

Not reachable by the model — it reports what a binding is and nothing writes one but you.

### Cores by ship {#cores-by-ship}

Every ship you have bound and what it flies with, and the button that forgets the one you are in.
A line the file refused — a core that does not exist, a ship bound twice — is reported here
rather than silently dropped.

## Personality off

The [conversation](conversation.md) capability has a **Personality** switch. Off gives plain
answers with no core, no flavour and no ambient remarks.

The anti-invention guardrails are unaffected. They sit *above* the persona in the assembled
prompt, and there is no code path that can vary them — switching personality off truncates a
later block and cannot reach that one. A guardrail a setting could remove would be a guardrail in
name only.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `state_identity`

Answers "who are you" with the name and nothing else — `I am Warden.`, or `I am Fred.` when you
have named the ship's AI yourself. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Listed first on purpose. The keyword router answers with a capability's first tool that needs no
arguments, and every phrase this capability declares is a question about identity — so this is
the tool that answers them. It used to be the one below, which meant asking your companion its
name got a status report and a list of the ten it was not.

### `describe_persona`

Reports which persona is active, what the Commander calls it, and what else is available. Takes
no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `bind_ship_core`

Binds the ship the Commander is in to the core aboard. Takes no arguments — the ship and the core
are both read from where they already are.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `forget_ship_core`

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
