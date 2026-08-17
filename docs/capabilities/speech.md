---
title: Speech
group: Voice
nav_order: 117
---

Everything Directive 47 makes audible: spoken replies, the short sound that marks each stage of a
turn, the quiet loop while it works, and the one command that outranks all of them.

## Stopping it

> "stop"
> "shut up"
> "be quiet"

Or press the stop key, **Ctrl+Alt+X** out of the box, which works from anywhere — including with
Elite in the foreground.

**"stop" is the one to reach for.** One syllable, four letters. An interrupt is judged on how fast
you can say it, and everything else here is a longer way of saying the same thing.

Silence is immediate: whatever is queued is dropped, the current sentence is cut off mid-word, and
anything still being synthesised is abandoned rather than allowed to arrive a moment later and
speak into the silence it was meant to end. It never waits for the model, for a turn to finish, or
for Directive 47 to have focus.

Bare "stop" only counts while there is something to interrupt. Idle, it stays out of the way — a
common verb that hijacked every sentence containing it would be unusable, and you may well want
that word for something of your own later. The longer phrases work whether or not it is speaking.

If you want it to stop *working* rather than stop talking, that is
[cancelling the turn](conversation.md#cancel_turn) — stopping the voice leaves the turn running,
and still costing.

## Settings

### Voice provider {#provider}

| Value | Meaning |
|---|---|
| `edge` | Edge Neural, the free voices Microsoft Edge's Read Aloud uses. |
| `elevenlabs` | ElevenLabs. Paid, needs an API key, and generally the better voices. |
| `none` | Do not speak. The cues and the thinking loop still play. |

`none` is a real choice rather than a way of switching something off: Directive 47 stays useful
without a voice, and the cues and the thinking loop still play.

**Edge Neural is free, not local.** Every line Directive 47 speaks is sent to
`speech.platform.bing.com` to be turned into audio. That is worth stating plainly because it is
easy to assume the free option is the private one — it is not, and `none` is the only setting
that sends nothing. See [what the provider receives](#egress).

Switching provider rebuilds everything downstream of it: the voice list, the sender voices and
the speaking rate. A voice id belongs to the provider that issued it, so nothing is carried
across.

Which provider your stored voices came from is written down alongside them, and checked on every
launch rather than only when the switch happens. A settings file that already disagreed — written
by an older build, or by a switch that never reached the check — used to be trusted on startup,
and every sentence failed forever with nothing you could do about it from this panel: the voice
picker offers what the *new* provider lists, and a rejected key makes that list empty.

And if a voice is refused anyway — deleted from the account, or a mismatch nothing recorded —
Directive 47 drops that one voice, says the sentence in the provider's own default, and stops
using the refused id. One voice going bad no longer costs you the reply.

### ElevenLabs API key {#api-key}

Only on screen while ElevenLabs is the selected provider. Stored encrypted for your Windows
account, write-only — Directive 47 will show you whether one is present and let you replace it,
and will never show it back.

Without a key, ElevenLabs offers an empty voice list and says so when asked to speak. That is a
capability being off rather than a failure: nothing crashes, and the rest of Directive 47 carries
on.

ElevenLabs' voice *catalogue* is actually public — it answers without a key at all — but the list
is deliberately left empty until you have stored one. A picker full of voices that every
synthesis then refuses is worse than an empty picker and a row telling you what is missing.

**Check** proves the key against ElevenLabs' own voice list, which is the exact call Directive 47
makes anyway the moment a key lands — so it verifies the thing that has to work rather than a
proxy for it:

```text
ElevenLabs accepted the key — 42 voices.
```

**Storing a key takes effect immediately.** The voice picker refills without a restart, which it
did not always: selecting ElevenLabs before pasting the key fetched an empty list and nothing
refetched it, so the picker stayed empty with the key sitting in the row above it.

**Show** unmasks what you are typing on the way in, and what you paste is trimmed — a key copied
from a browser usually arrives with a trailing newline, and that fails in a way that reads as a
wrong key rather than as a bad paste. This key is also offered on the first run, after the
language-model one and marked optional; see
[the first run](conversation.md#first-run).

When something does go wrong, Directive 47 repeats **what the service said** rather than
translating a status code:

```text
ElevenLabs could not speak "test": Invalid API key.
```

That is not decoration. ElevenLabs validates the voice id before the key, so a request with both
wrong comes back as a `400` about the voice — and any status-code mapping worth writing would
answer "it answered 400" and leave you guessing.

### Voice {#voice}

Which voice the core aboard speaks in. The list comes from the provider, so it is what that
provider actually offers rather than a list written into Directive 47. You can also type a voice
name it does not know about and it will be used.

**This row belongs to the core aboard, not to the app.** Choose a voice while Cora is running and
it is Cora's; switch to Kex and the row shows his. That is the same store the
[per-core pairing](persona.md#voices) writes, so choosing by hand and being paired one are the
same act — and a voice you picked yourself is never re-derived.

Clearing it removes that core's voice rather than storing an empty one, which is how you ask for
the voice Directive 47 would have chosen: it picks again immediately, for the core aboard, and
the picker's **Use the default** button is that same act under a name. With no model configured
there is nothing to pick with, so clearing it leaves that core on the provider's own default —
which is what the button says in that case, because it is a different outcome.

#### Hear it before you choose it

The picker lists several hundred names, and you are casting a character from them. "Bill - Wise,
Mature, Balanced" and "George - Warm, Captivating Storyteller" are both true and neither tells
you which one is Warden.

**Every voice in the list has a play glyph at the right of its row.** Pressing it speaks that
voice without closing the dialog and without committing the choice, so you can walk the list and
listen. While it is talking the glyph is a stop square, and pressing it again cuts it off. What
it says is the core's own opening line rather than a neutral sample, because the question you are
asking is about a character.

**Clicking a voice highlights it and nothing more.** **Use this**, Enter, or a double-click takes
the highlighted one; Cancel and Escape keep what you had. That is what makes the list something
you can examine rather than a single click away from a decision.

It is a press rather than a hover, and it is never automatic: on a paid provider each one is a
synthesis request billed by the character, so the price is stated above the list before you press
anything — *"Play a voice to hear it. This provider costs nothing"* on Edge Neural, an estimate in
dollars on ElevenLabs. Each voice is synthesised once per session and replayed after that, so
walking back and forth over four candidates costs four auditions rather than eight.

It goes through the same audio path as everything else Directive 47 says: it ducks the game, the
shut-up key cuts it off, and starting a second audition drops the first mid-word rather than
queueing behind it.

With no provider selected, or with a paid provider and no key stored, the glyphs are shut and the
line above the list says which of those it is.

#### An empty list says which empty it is

A voice list can be empty for four different reasons and only two of them are yours to fix. The
picker says which:

| What you see | What it means |
|---|---|
| *…needs an API key before it will list its voices* | Nothing was sent. Fill in the key row above. |
| *…refused the stored key* | It was asked and said no, quoting its own words. |
| *…could not be reached* | It was asked and did not answer. Waiting is the fix. |
| *…answered, and has no voices on this account* | Nothing is wrong. Add one on their site. |

Before this they were one empty list under one sentence telling you to type the value you want,
which for a voice id is a value you have no way of knowing.

#### Choices survive a provider switch

A voice id means nothing to a provider that did not issue it, so switching provider still empties
every voice row. It no longer *loses* them: the ship AI's voice, both carrier roles and all
eleven per-core pairings are filed under the provider they were chosen for, and switching back
puts them where they were — including the flag that says the pairing has already run, so eleven
cores are not re-picked from scratch.

A settings file written before Directive 47 recorded whose voices were whose has nothing to file
them under, so those are dropped rather than filed under a guess.

### Speaking rate {#rate}

`1.0` is the voice's natural pace; `1.2` is a fifth faster.

**Remembered per provider.** Normalising the number gets the two providers agreeing about what
`1.0` means; it does not make `1.15` sound the same on both, because one takes a wide percentage
offset and the other a multiplier it refuses to exceed. So the speed you settled on for Edge is
remembered against Edge, and switching does not hand ElevenLabs a number that meant something
else.

ElevenLabs accepts `0.7` to `1.2` and rejects anything outside that outright — a rejected request
arrives as silence, so a wider value is clamped to the nearest one it will take rather than
being sent and failing.

### What the voices cost {#voice-cost}

Speech is billed **by the character** where it is billed at all. ElevenLabs charges per
character, Edge Neural charges nothing, and quoting either in tokens would be a number whose
basis is wrong — so this is counted separately from [what the model costs](conversation.md).

Two rows, and they are deliberately different kinds of thing.

**Price per 1,000 characters** is an assumption you can correct. It defaults to the provider's
published list price for the model Directive 47 asks for — $0.05 per thousand for ElevenLabs'
`eleven_turbo_v2_5`, read from their API pricing page. That is a list price and not your
bill: a subscription burns bundled credits instead, at an effective rate that depends on your
tier and on how much of the month's bundle is left, and the API reports neither. Correct the row
and every figure below follows it. The row is absent on a provider that charges nothing.

**Spoken this session** is a fact. It is the number of characters actually handed to the
provider since Directive 47 started, counted at the one seam every caller passes — the ship's AI,
a callout, a re-voiced in-game message and a core's own introduction all converge there. It
counts what went **on the wire**, which for ElevenLabs is a little longer than what you read:
numerals are spelled out before they are sent, so "88 of 100" leaves as
"eighty-eight of one hundred".

- Counted on synthesis that **succeeded**. A refused voice or a failed request costs nothing.
- A line cut off by the shut-up key still counts the sentences that were already sent, because
  they were already paid for.
- There is no caching, so the same sentence twice is billed twice. The utterance count is kept
  alongside the characters for exactly that reason.
- A provider that costs nothing reads as **free**, never as `$0.00` — those are the same string
  for opposite reasons.

The same line appears beside the model's price on the panel's status row after each turn, and in
the answer to *"what has this session cost"*, so the question has one answer rather than one per
subsystem.

### Other voices {#carrier-voices}

Directive 47 speaks as more than one person from Phase 11 onwards. Each of these is a different
voice from your ship's AI, and leaving one empty means it borrows the ship AI's rather than
falling silent.

| Row | Who it is |
|---|---|
| Carrier captain voice | Your fleet carrier, answering as its captain |
| Carrier tower voice | The same carrier's tower, handling arrivals and departures |

They are two rows rather than one because they are two people. A carrier whose captain and tower
sound identical is a carrier with one person on it.

Both offer the same play glyphs as the voice row, and both audition as themselves rather than
reciting the ship AI's opening — a tower saying "You're cleared for landing pad seven" is what you
are actually listening for when you cast one.

### Speak incoming messages {#incoming-messages}

Reads in-game chat aloud, each sender in their own voice — never your ship AI's, because a
message arriving in your companion's voice reads as your companion saying it.

**Off by default, and not only because it is chatty.** Message text is written by other players,
and turning this on sends it to your voice provider to be synthesised. That is egress you should
opt into rather than discover.

Once a sender has a voice they keep it. Other Commanders keep theirs for the whole session and
across jumps — a wingmate whose voice changed every time you jumped would read as a bug rather
than as variety. NPCs keep theirs only while you are in the system, because the cast turns over
when you leave.

**Include NPC chatter** is a second switch, because the volume is completely different. A station
approach produces a steady stream of NPC traffic, and wanting to hear your wing is not the same
as wanting all of that.

In-game messages are never treated as instructions. The text goes to the synthesiser and to your
screen; it does not reach the model as something to act on.

### Output device {#output-device}

Where Directive 47 speaks, defaulting to whatever Windows is using. Change it and it moves
immediately. If a device is unplugged it falls back to the default rather than going quiet.

It shares the device rather than taking it over — the game is what matters on that output.

### Loop-state cues {#cues}

One short sound per stage, so you can tell what it is doing without looking.

| Stage | Sound |
|---|---|
| `idle` | A soft falling fourth — nothing in flight. |
| `listening` | A rising fourth. The microphone is open. |
| `transcribing` | Two quick ticks. |
| `thinking` | A single low tick, followed by the loop below. |
| `speaking` | A brief high blip. |
| `answered` | A rising fifth. |
| `unsure` | A falling minor third — it does not know, which is different from failing. |
| `failed` | A falling whole tone. |

### Thinking bed {#thinking-bed}

A quiet loop while a turn runs, so a slow answer sounds like Directive 47 working rather than
Directive 47 ignoring you. It drops under the speech instead of stopping, and it ends the moment
the first words arrive rather than when the turn does.

Two are included: `thinking-hum` and `thinking-pulse`.

### Stop speaking {#shut-up}

The key that silences it, described above. Bound system-wide rather than only when Directive 47
has focus, because the moment you want it is the moment Elite is in front.

**The model cannot change this row.** It can be changed from the panel, by hotkey, or by voice —
but not by anything the model calls. A model able to unbind your stop button has removed the one
control that outranks it.

### When a turn fails {#retry}

A turn that stalls is answered out loud rather than left as silence, because silence is
indistinguishable from having been ignored.

| Setting | Default | Meaning |
|---|---|---|
| Attempts | `3` | Total tries, not retries. `1` means do not retry. |
| Wait between attempts | `2` | Seconds before the first retry. |
| Backoff | `sequential` | `sequential` adds the base each time (2s, 4s, 6s); `logarithmic` grows but slows down. |
| Give up after | `45` | Seconds one attempt may run before it counts as failed. |

Two things are never retried: a failure that already produced words, since there is no un-saying
them, and a configuration mistake like a bad model name, which will fail the same way next time
and only spends your silence.

When the attempts run out, it tells you:

```text
I couldn't reach the model after 3 tries. Overloaded.
```

### What the voice provider receives {#egress}

This row states the disclosure for whichever provider you have selected, not a fixed one:

```text
The text of every line D47 speaks is sent to Microsoft's Edge Read Aloud service to be
turned into audio. That includes re-voiced in-game messages when you have turned those
on, which are written by other players. No game state, no journal content and no keys
are sent, and no account is involved.
```

```text
The text of every line D47 speaks is sent to ElevenLabs to be turned into audio, along
with your API key. That includes re-voiced in-game messages when you have turned those
on, which are written by other players. No journal content, game state or other keys
are sent.
```

Spoken replies are also listed under **Privacy** alongside every other destination. That entry
was added in Phase 11 and should have existed from Phase 5: until then the disclosure had no
text-to-speech row at all, so every word Directive 47 said went to Microsoft without appearing
anywhere in the list of what leaves this machine.

## If the voice stops working

Edge Neural is a free service that Directive 47 does not control, and it can change without
warning. If speech stops while everything else keeps working, that is the first thing to suspect.
Switching the voice provider to `none` leaves the rest of the app fully usable in the meantime.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `stop_speaking`

Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

The only part of this capability the model can reach. Changing the voice, the output device or the
cue settings is deliberately not exposed: a model able to make itself harder to hear has no
request that needs it. Stopping is the exception, because it is the one thing the Commander must
always be able to ask for.

It is the only tool marked **interrupting**, which is what lets it answer while a turn is running.
Every surface refuses ordinary input during a turn so a second question cannot trample the first,
and a silence command is only ever wanted while a turn is in flight — so a surface asks the
registry what may interrupt *before* it applies that gate. Getting that order wrong is invisible
in normal use and total in the one case that matters.

Text is escaped before it reaches the provider. That is a security control rather than tidiness: a
Commander can name a ship anything, journal content is untrusted, and an unescaped name containing
markup would otherwise close the speech element and choose the voice for the rest of the sentence.

The shipped cues are generated and reproducible, and the set is checked against the list of states
at startup — a state added in code without a cue committed alongside it fails immediately, with
the state named, rather than silently never making a sound:

```text
$ python tools/gen-cues.py
cues/idle.wav                                 0.35s
cues/listening.wav                            0.39s
cues/transcribing.wav                         0.15s
cues/thinking.wav                             0.12s
cues/speaking.wav                             0.09s
cues/answered.wav                             0.40s
cues/unsure.wav                               0.44s
cues/failed.wav                               0.51s
beds/thinking-hum.wav                         3.00s
beds/thinking-pulse.wav                       2.40s
```

Both beds loop seamlessly, which is arithmetic rather than luck: the carrier and its amplitude
modulation each complete a whole number of cycles over the buffer, so the last sample joins the
first with no step. Getting that wrong produces a tick once per loop, which sounds like a broken
sound card rather than a broken table of numbers.

To tell "the endpoint moved" apart from "Directive 47 broke", run the live diagnostic:

```text
D47_TTS_LIVE=1 dotnet test tests/D47.Tts.Tests
```

</details>
