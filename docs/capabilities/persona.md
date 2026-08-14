---
title: Persona
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

## It says something when you switch

A core you have not used this session introduces itself. One you are coming back to reacts to
the time it was switched off instead, and it is handed what changed while it was away:

```text
While you were not running:
  14 hyperspace jumps.
  312 light years covered.
  2,480,000 credits earned.
  One interdiction.

Where the ship is now:
  ...
```

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

One exception, because it is not a judgement: on ElevenLabs, **Warden** takes **George** — warm,
captivating storyteller, male, British — with or without a model.

The voice sits on the [Voice row](speech.md#voice) in Speech, and that row is the core aboard's.
Change it and you have chosen that core's voice; nothing re-derives it afterwards. Clear it and
the next selection picks again.

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

A core introduces itself the first time you pick it after Directive 47 starts. Every time after
that it reacts to the gap instead, which is the better line once you have heard the first one —
and the wrong line when you are working through the cast and want to hear how each of them
opens.

**Forget introductions** puts all eleven back to their first line at once. Nothing else is
touched: transcripts, voices and the core aboard stay exactly as they were. The row states which
cores are spent before you press it, and states that none are afterwards.

The core currently aboard is forgotten with the rest, but hearing it again means switching away
and back — selecting the core that is already running is not a switch, and never has been.

This row is not reachable by the model, for the same reason the persona row above is not.

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

There is deliberately no tool for *changing* persona. The `persona.id` row is marked protected,
so `set_setting` refuses it like any other protected row, and the model is never shown the row in
`list_settings`. The panel, the keyword router and the settings surface all reach it — protected
is a property of the caller, not of the modality (architecture.md §7).

The persona block is prompt position 3, above the cache breakpoint. Its bytes change when the
Commander picks a different core or renames the AI, and at no other time — anything else varying
it would be paying for a cold prefix nobody asked for.

</details>
