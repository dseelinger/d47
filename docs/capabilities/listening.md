---
title: Listening
group: Voice
nav_order: 123
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Whisper turns your voice into words, on your own machine — and how it decides you were talking to it.</p>
<section>
<h2><span class="num">1</span> Four ways to open the microphone, and two of them change what is kept.</h2>
<svg viewBox="0 0 880 296" role="img" aria-label="Four listening modes: hold a key, toggle a key, listen whenever anyone speaks, or listen when you say its name">
 <rect x="20" y="30" width="410" height="102" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="225" y="66" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">HOLD THE KEY</text>
 <text x="225" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">speak, let go. The shipped default,</text>
 <text x="225" y="118" text-anchor="middle" font-size="15" fill="var(--text-muted)">and nothing is kept unless you held it</text>
 <rect x="450" y="30" width="410" height="102" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="655" y="66" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">TOGGLE THE KEY</text>
 <text x="655" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">press once to start, again to stop.</text>
 <text x="655" y="118" text-anchor="middle" font-size="15" fill="var(--text-muted)">Same rule, no finger held down</text>
 <rect x="20" y="152" width="410" height="102" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="225" y="188" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">WHENEVER I SPEAK</text>
 <text x="225" y="218" text-anchor="middle" font-size="15" fill="var(--text-muted)">every stretch of speech in the room</text>
 <text x="225" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">is transcribed to find out if it was for it</text>
 <rect x="450" y="152" width="410" height="102" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="655" y="188" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">WHEN I SAY ITS NAME</text>
 <text x="655" y="218" text-anchor="middle" font-size="15" fill="var(--text-muted)">the same, and then thrown away</text>
 <text x="655" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">unless it is followed by a request</text>
 <text x="440" y="280" text-anchor="middle" font-size="15" font-weight="700" fill="var(--danger)">The bottom two are off out of the box.</text>
</svg>
<p class="body">The key works in all four. A policy that decides for itself is not a reason to take away the one that does not.</p>
</section>
<section>
<h2><span class="num">2</span> One download, and then nothing about your speech goes anywhere.</h2>
<svg viewBox="0 0 880 236" role="img" aria-label="The speech model is downloaded once from huggingface.co; after that audio and transcripts stay on your machine">
 <rect x="20" y="34" width="250" height="104" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">ONCE</text>
 <text x="145" y="102" text-anchor="middle" font-size="15" fill="var(--text-muted)">the speech-to-text model, from</text>
 <text x="145" y="124" text-anchor="middle" font-size="15" fill="var(--text-muted)">huggingface.co</text>
 <line x1="282" y1="86" x2="318" y2="86" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="332,86 316,78 316,94" fill="var(--accent-muted)"/>
 <rect x="342" y="34" width="518" height="104" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="601" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">EVER AFTER</text>
 <text x="601" y="102" text-anchor="middle" font-size="15" fill="var(--text-muted)">your voice becomes words on this computer.</text>
 <text x="601" y="124" text-anchor="middle" font-size="15" fill="var(--text-muted)">No audio and no transcript leaves it.</text>
 <rect x="20" y="158" width="840" height="52" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="190" text-anchor="middle" font-size="16" fill="var(--text)">A bigger model hears you better and costs more of your machine. Nothing about that choice is a subscription.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Elite might already be using that key.</h2>
<svg viewBox="0 0 880 244" role="img" aria-label="A keyboard key bound in both Elite and Directive 47 simply does nothing in one of them, with no error anywhere">
 <rect x="20" y="34" width="250" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">ONE KEYBOARD KEY</text>
 <text x="145" y="102" text-anchor="middle" font-size="15" fill="var(--text-muted)">bound in both places</text>
 <line x1="282" y1="82" x2="318" y2="82" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="332,82 316,74 316,90" fill="var(--accent-muted)"/>
 <rect x="342" y="34" width="250" height="96" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="467" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">ONE LOSES</text>
 <text x="467" y="102" text-anchor="middle" font-size="15" fill="var(--text-muted)">and no error, anywhere</text>
 <line x1="604" y1="82" x2="640" y2="82" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="654,82 638,74 638,90" fill="var(--accent-muted)"/>
 <rect x="664" y="34" width="196" height="96" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="762" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">SO IT SAYS SO</text>
 <text x="762" y="102" text-anchor="middle" font-size="15" fill="var(--text-muted)">by name, before you fly</text>
 <rect x="20" y="152" width="840" height="72" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="182" text-anchor="middle" font-size="16" fill="var(--text)">Directive 47 reads your Elite bindings and never writes them.</text>
 <text x="440" y="208" text-anchor="middle" font-size="15" fill="var(--text-muted)">It will tell you which Elite action you clashed with. Which of the two to move is your call.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> Five things can stop it hearing you.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="Five separate reasons speech might not reach Directive 47, all reported together in one answer">
 <rect x="20" y="30" width="164" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="102" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">NO KEY</text>
 <text x="102" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">bound</text>
 <rect x="196" y="30" width="164" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="278" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">NO MIC</text>
 <text x="278" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">chosen</text>
 <rect x="372" y="30" width="164" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="454" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">MIC GONE</text>
 <text x="454" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">unplugged</text>
 <rect x="548" y="30" width="164" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="630" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">KEY CLASH</text>
 <text x="630" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">with Elite</text>
 <rect x="724" y="30" width="136" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="792" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">NO MODEL</text>
 <text x="792" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">yet</text>
 <rect x="20" y="138" width="840" height="72" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="170" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">ASK "CAN YOU HEAR ME"</text>
 <text x="440" y="196" text-anchor="middle" font-size="15" fill="var(--text-muted)">All five are tested and tell you the result.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card settings" href="speech.html"><span class="ct">Speech →</span><span class="cd">The other half of talking: the voice that reads the answer back.</span></a>
<a class="card" href="../transcript.html"><span class="ct">The Transcript page →</span><span class="cd">What the microphone badge is telling you, and the rest of the controls there.</span></a>
<a class="card" href="privacy.html"><span class="ct">Privacy →</span><span class="cd">Everything that reaches a network, counted rather than promised.</span></a>
</div>
</div>
</div></div>

## The details

Talking to Directive 47 instead of typing at it. Hold a key, speak, let go, and what you said is
handled exactly as if you had typed it — or put your hands back on the stick and let it decide for
itself when you are talking to it.

**No audio and no transcript leaves your machine.** Speech is turned into words by a model
running on your own computer. The model file itself is downloaded once, from `huggingface.co`;
after that, nothing about your speech goes anywhere.

### Ask for it

> "can you hear me"
> "what microphone are you using"
> "is my push to talk key bound twice"

```text
Microphone: Yeti Nano, capturing.
Gate: hands free, opening when you say my name.
Right now: the microphone is open and I am waiting to hear you start.
Push-to-talk: CapsLock (hold).
Warning: CapsLock is also bound in Elite (KeyboardMouseOnly) to HeadLookToggle. One of the two
will not work, and neither will say so — pick another key for one of them.
I answer to: D47.
Echo cancellation: running, so you can talk over me.
Transcription: base.en loaded.
```

Everything comes back together on purpose. "It cannot hear me" has five possible causes — no key
bound, no microphone, the microphone gone, the key clashing with Elite, or no speech model — and
you should not have to guess which.

### Getting set up

Three things, in this order:

**1. Bind a key.** Already done — **right shift**, out of the box. Clear the row if you would
rather Directive 47 never opened the microphone at all.

**2. Download a speech model.** Also already done, or under way. Directive 47 ships with **Tiny
(English only)** selected and fetches it from `huggingface.co` the first time it starts — about
75 MB, once. Until it lands, Directive 47 captures your voice and tells you plainly that it
cannot understand it yet.

**3. Check the key is yours alone.** If Elite is already using it, say so is exactly what
Directive 47 does — see below.

**Optional: put the key away entirely.** [How D47 decides you are talking to it](#mode) has two
hands-free settings. They are off out of the box, deliberately — that section says what turning
one on actually means.

### Your key might already be Elite's

A key bound in both places has no symptom other than not working, in one direction or the other,
depending on which application sees it first. Nothing tells you; it simply does nothing.

So Directive 47 reads your actual Elite bindings — the preset you are really using, including the
built-in ones — and says outright when your push-to-talk key is already spoken for:

```text
Warning: CapsLock is also bound in Elite (KeyboardMouseOnly) to HeadLookToggle. One of the two
will not work, and neither will say so — pick another key for one of them.
```

**And the same for a stick button, with one difference that matters.** Elite records a joystick
binding against its own name for the device, which is not the one Directive 47 reads — so when a
button of that number is bound in Elite, it cannot tell you whether that is the same stick or a
different one on your desk. It says so in those words rather than pretending to be sure:

```text
Warning: button 7 on the Virpil Alpha may collide. Elite (Custom) binds a button of that number
to SelectTarget, and I cannot tell whether that is the same controller. If the microphone will
not open, this is the first thing to check.
```

Finding **nothing** is the stronger answer of the two, and is said plainly: no button of that
number is bound anywhere in your preset, on any device, so there is nothing left to be unsure
about.

**Your bindings are never written to.** Directive 47 only ever reads them.

If it has not managed to read them, it says nothing rather than giving you an all-clear — never
having looked is not the same as having looked and found nothing.

### Settings

#### Microphone {#microphone}

Which input to listen on. Leave it unset for whatever Windows is using.

If the one you chose disappears, the status answer names it rather than reporting generic
silence. No microphone at all is a feature being off, not a failure — Directive 47 stays fully
usable typed.

#### Push-to-talk {#push-to-talk-key}

What you hold to talk. **Right shift out of the box** — a Commander on a stick and throttle has
a spare thumb and not much else, and it is the right-hand shift specifically, so the left one you
may already be using in the game is not this.

**One row, and it takes a key, a stick button, or both.** Press **Press to bind** and Directive 47
listens for either at once: press a key and it takes the key, press a button on your stick and it
works out which one that was.

**To have both, bind twice — one gesture each time.** Press the control and give it a key; press it
again and give it a button. The two are stored separately, so the second does not replace the first,
and the row then reads `RightShift, button 11`. Either one opens the microphone, and the last one you
let go of closes it — so letting go of the key while your thumb is still on the button does not cut
you off mid-sentence.

**It is one of each, not any number.** There is one slot for a key and one for a stick button. Give
it a second key and the key changes; give it a second button and the button changes. Nothing is ever
silently added to a list you cannot see — what the row reads is exactly what is bound, and two
gestures is the most it will ever say.

**A key on its own, like right shift, binds when you let it go.** Modifiers are the one kind of key
that cannot be taken the instant they go down, because that is also how a combination starts. Hold
one and press something else and you get the combination; press one and release it and you get the
modifier. Right shift is the default here for exactly that reason — it is a key nothing else wants.

**Unbind clears both.** That is what the word says, and nobody ends up with two by accident.

There used to be two rows for this. The stick half is [described below](#push-to-talk-button) and
is unchanged in every respect except that it is no longer a separate question.

Clear the row and Directive 47 never opens the microphone — unless you have also put
[the row below](#mode) into one of its hands-free settings, which is the whole point of those and
the only case where an unbound key still leaves a live microphone.

Clearing it was the old default, and it meant a voice companion that could not hear anything until
you found this row.

In the two key-driven settings, nothing is kept unless the key is held.

**Pressing it also shuts Directive 47 up.** The press is the interrupt — every press, in every
setting, whether or not you go on to say anything. So talking over an answer you have heard enough
of is one gesture rather than two, and there is no separate stop key to reach for. If you clear
this row *and* the button below, the [Stop speaking](speech.html#shut-up) row appears on the Speech
page to give you one back.

Unlike the stop key, this one does not need a modifier — a bare key is the normal arrangement for
push-to-talk, which is exactly why the collision check above matters. If right shift is bound to
something in Elite on your setup, the status report above says so by name.

**The model cannot change this.** A model that could unbind your microphone key has taken away
how you talk to it.

#### The stick half {#push-to-talk-button}

**Not a row of its own any more** — it is the **Push-to-talk** row above. This section is about
what happens when you press a stick button at it, which is worth reading before you try.

Press **Press to bind**, then press and release the button you want, and Directive 47 works out
which one it was. It is stored separately from your key, so binding a button does not unbind the
key: you said two things rather than changed your mind about one.

**It has to be a button that springs back.** A switch that stays where you put it would hold the
microphone open until you moved it again, so the capture declines one and says why. Those belong
on the [switch panel](switches.html) instead, which is the other half of the same hardware — Phase
21 turned away every springing button because a switch needs a *position* to mean anything, and
this feature is that decision read the other way round.

**If the controller is not there when D47 starts**, it says so and your key carries on working. A
stick that is asleep is one of the ways "D47 cannot hear me" happens with no reason attached.

**The collision check is weaker here than for a key, and it says so.** Elite records a joystick
binding against its own internal name for the device, which is not the name Windows gives it, so
Directive 47 cannot tell whether Elite's *button 24* is on the *same* stick as yours. What it can
say is that a button of that number is spoken for somewhere — worth saying on a HOTAS, where every
button is usually already used:

```text
Push-to-talk button 24 may collide: Elite (Custom) binds a button of that number to
UseBoostJuice. D47 cannot tell whether that is the same controller.
```

**The model cannot change this one either**, for the same reason.

#### How D47 decides you are talking to it {#mode}

Four settings, and the key works in all four — a policy that decides for itself is not a reason to
take away the one that does not.

| | What it does |
|---|---|
| **Press to talk (PTT)** | Hold the key while you speak. The shipped default. |
| **Toggle on and off** | Press once to start, again to stop. |
| **Listen whenever I speak** | Directive 47 opens the microphone itself when it hears somebody start talking, and closes it when they stop. |
| **Listen when I say its name** | The same, except what you said is thrown away unless you addressed it by name. |

**What the last two actually mean.** The microphone was already open — it always is, so the
pre-roll has something in it — but until now nothing was ever *kept* unless you were holding the
key. In the two hands-free settings, every stretch of speech in the room is captured and
transcribed before Directive 47 can decide whether it was meant for it. That is a real change and
it is why these are off out of the box.

What does not change: **none of it leaves your machine and none of it is written to disk.** The
speech model runs locally, a stretch that was not addressed to Directive 47 is discarded without
reaching the transcript, and the panel says the microphone is open the whole time it is — see
[Seeing that the microphone is open](#indicator).

The cost is CPU. Everything said near you is transcribed and then mostly thrown away, so run these
on one of the smaller models unless you have cycles to spare.

**The model cannot change this row.** A model that could put Directive 47 into continuous
listening could start capturing on your machine — and anything the model can call, a hostile
in-game message can try to invoke. You can still say *"listen for your name"* or *"stop listening
all the time"*, which go through the keyword router rather than through the model.

#### How much louder than the room speech has to be {#sensitivity}

Only applies hands free. Directive 47 measures your room continuously — falling to a new quiet
almost at once, rising to a new loud slowly — so this is a **margin above whatever your room
happens to be**, not a fixed loudness. That is what makes one number work across a headset boom, a
desk condenser and a laptop array, whose levels differ by tens of decibels.

Lower hears more, and will open on a cough or a keyboard. Higher waits until you are clearly
talking. **9 dB** out of the box.

#### Quiet that ends a sentence {#silence}

How long you have to stop talking before Directive 47 decides you have finished. **700 ms** out of
the box, which is deliberately generous: people pause mid-sentence to look at something, and an
utterance cut at the first gap reaches the model as half a question. Being wrong the other way
costs a little dead air on the end of the clip, which the speech model ignores.

#### What D47 answers to {#wake-words}

Only applies in **Listen when I say its name**. Leave it unset and Directive 47 answers to whatever
you call your ship's AI, so renaming the core renames the wake word with it.

Set it — comma-separated — when the speech model keeps hearing the name as something else. It is
already forgiving: the comparison ignores punctuation and spacing, so `D47`, `D 47`, `d-47` and
`D47.` are all the same word. Adding a spelling is for when it comes out as something genuinely
different.

The name has to be near the front of what you said. Talking *about* Directive 47 is not talking
*to* it.

#### Seconds D47 keeps listening after you say its name {#wake-window}

Say the name on its own, Directive 47 sounds its listening cue, and the next thing you say is the
request — the way you would address a person. **12 seconds** out of the box, and the follow-up
closes the window again: it is one reply, not an open microphone. Nothing is said back and nothing
reaches the transcript; being called by name is not conversation.

Set it to zero if you would rather the name and the request always arrived in the same breath.

#### Cancel D47's own voice out of the microphone {#echo-cancellation}

On, in every setting rather than only the hands-free ones — holding the key while a callout is
being read out otherwise transcribes the callout.

On speakers, this is what lets you **talk over Directive 47**. Measured against a simulated room,
it removes upwards of 25 dB of its own voice while leaving yours essentially intact.

If it cannot start — the native library missing, usually — it says so and the microphone keeps
working. Hands-free listening then goes deaf while Directive 47 is speaking, rather than risk the
loop where it hears itself, transcribes itself and answers itself. Ask *"can you hear me"* and it
will tell you which of the two you are in.

On headphones none of this matters much, because there is no echo to cancel.

#### Take the room out of what D47 hears {#noise-suppression}

On. Suppresses steady background noise — fans, a headset's own hiss — before the speech model sees
it. It also makes the hands-free decision easier, since that decision is entirely about how far a
sound sits above the room.

#### Capture before the key {#pre-roll}

How much audio from just before the gate opened is kept. **500 ms** out of the box.

It exists because the key is sampled ten times a second, so a key-down is noticed up to 100 ms
after it happened — and without this the first syllable of every sentence is clipped, which is
where the proper nouns are. It does the same job hands free, where what it covers is the moment
before Directive 47 was willing to call the sound speech.

#### Speech model {#model}

Which Whisper model turns your speech into words. The row marks which are already on disk and
what the others would cost to fetch, so you can see which choices are already paid for.

**Base (English only)** is what a fresh install has selected. Tiny is cheaper and is offered, but
it mis-hears the words Directive 47 acts on — over a recorded corpus it heard *"Cancel that"* as
*"Cancer that"*, and "cancel that" is one of the phrases that interrupts it, so the model saved
you 67 MB and cost you the way to shut it up.

**Every model here is English-only.** The multilingual ones were withdrawn: Directive 47 asks
Whisper for English on every clip, so they cost the same download and gave back a model worse at
the one language they were being asked for. If your settings still name one, you get its English
twin automatically and nothing is lost.

**What a bigger model costs is time per *sentence*, not time per second of audio.** Whisper reads
your speech in 30-second windows and pays for the whole window whether you filled it or not, so a
two-second question and a twenty-second one cost the same — and the model you picked is what sets
that figure:

| Model | On the CPU | On the GPU | Video memory |
|---|---|---|---|
| Tiny (English only) | about 0.2 s | about 0.13 s | about 100 MB |
| Base (English only) | about 0.3 s | about 0.12 s | about 140 MB |
| Small (English only) | about 1.0 s | about 0.17 s | about 470 MB |
| Medium (English only) | about 3.0 s | about 0.33 s | about 1,460 MB |

Measured on a 24-core desktop with an RTX 5080; a smaller machine is proportionally slower, and
anything over 30 seconds in one breath costs another window. That is the whole delay between you
letting go of the key and Directive 47 starting to think — so if it feels slow to answer, this row
is the first place to look.

**Medium is the most accurate and effectively wants a GPU.** It was the only model that heard
*"Deciat"* every time it was said, where the others offered "DCI" and "DC at" — proper nouns are
where the difference shows, and they are most of what you say to a ship's computer. Three seconds
a sentence on the CPU is the price without one.

**The device never changes the words.** Running on the GPU is a speed and memory choice only:
across a 37-clip corpus, every English model produced byte-identical transcripts on CPU and GPU.

**Choosing a model downloads it.** Selecting one you do not have starts the transfer there and
then, with the size and progress on the row; the same thing happens at startup for a model that
is selected and missing. The choice is the go-ahead — the size is on the row before you make it,
and `huggingface.co` is listed under [Privacy](privacy.md) for as long as a model is selected.

**Directive 47 knows what each model file should be.** The SHA-256 of every model it offers is
written into the build, and a download that does not match it is discarded rather than loaded.
That is worth one sentence of honesty about what it buys: those values were read from
`huggingface.co` once, on a stated date, and pinning them does not make that first read
trustworthy — it means the file *changing* afterwards becomes visible, where before the expected
hash and the bytes came from the same place. The model is loaded and run on your machine, so it
is worth checking.

`none` stays a real choice. Pick it and Directive 47 hears you and says, honestly, that it cannot
turn what it heard into words.

#### Running on the GPU {#gpu}

Off by default, and **about five times faster** when you turn it on. Measured on an RTX 5080 with
Small (English only): **190 ms against 920 ms** for the same sentence.

It costs video memory, which is the part worth thinking about, because that memory comes out of
whatever else wants the card. It is close to the size of the model file, because the weights are
almost all of it:

| Model | Video memory |
|---|---|
| Tiny (English only) | about 100 MB |
| Base (English only) | about 150 MB |
| Small (English only) | about 470 MB |

**In VR that trade is a real one.** Your GPU is already the scarce thing there, and taking memory
and time from the game shows up as dropped frames rather than as anything that looks like a
speech problem — a symptom nowhere near its cause. That is why this is off out of the box rather
than on. On the desktop window, with headroom to spare, it is close to free.

**If your machine has no GPU D47 can use, it runs on the CPU and says so** — in the log, and in
what it reports about itself. It does not fail, and it does not claim a device it is not using.

The switch takes effect immediately, both ways: turn it off and the video memory is handed back.

> **This did not work before [#187](https://github.com/dseelinger/d47/issues/187).** The toggle
> shipped for months with no GPU code behind it at all — the CPU runtime accepted the request,
> loaded happily, and the log read *"on the GPU"* because it was repeating the request back
> rather than reporting what happened. If you turned it on and noticed nothing, that is why.
> D47 now uses Vulkan, which works on AMD and Intel cards as well as NVIDIA.

### Seeing that the microphone is open {#indicator}

Bottom left of the panel, on the desktop **and** in the headset, in mini as well as full. Three
states:

| | |
|---|---|
| **PTT Ready** | Push-to-talk at rest. Audio runs into the half-second ring and is overwritten, and nothing is kept. |
| **Listening...** | Hands free. Directive 47 is deciding for itself when to listen. |
| **MIC ON** *(filled, ringed)* | The gate is open. What is arriving now will be transcribed. |

The first two name the mode because the state only happens in that mode: at rest is
push-to-talk, hands free is not. The open gate is reached both ways — a held key and a gate
Directive 47 opened for itself are the same fact about the microphone — so it says neither.

Filled or hollow is the state, not only the colour — a glance reads a shape first, and a difference
that is only colour is not a difference for everybody. Hover it for the gesture, or the name, that
would open the gate.

Nothing is drawn when no device is open at all.

### Downloading a model {#download}

A selected model that is not on disk is fetched — at startup, or the moment you choose it. There
is no prompt to answer: the selection is the go-ahead, the size is on the row before you make the
choice, and the shipped default is the smallest model in the list.

The download is checked against the checksum `huggingface.co` publishes for the file, as it
arrives, and thrown away rather than kept if it does not match: a file that fails its checksum is
either a broken transfer or something that should not be loaded, and both answers are the same.
The size Directive 47 reports is the one the host actually gave, not a figure written into the
app that would go stale the first time a model was republished.

If the download fails — no network, the host refusing — the selection stays where it is and
Directive 47 says it has no speech model loaded when you ask. It tries again the next time it
starts. Choose `none` if you would rather it stopped trying.

### It knows what things are called

Every utterance is transcribed knowing the names around you: the system you are in, the station,
the body, your next jump, your ship and its type, your carrier, the route ahead, and your fleet.

This matters more than it sounds. **Proper nouns are where speech recognition fails hardest and
most quietly.** A misheard system name does not come back as an error — it comes back as a
plausible English phrase. "Shinrarta Dezhra" becomes "shin arta desha", everything proceeds
confidently about the wrong system, and nothing anywhere reports a problem.

The names come from your journal. Nothing is looked up.

### What happens to what you say

Spoken and typed questions run exactly the same path, so "where am I" means the same thing
however you said it.

Nothing captured is written to disk or sent anywhere. Audio sits in a small buffer and is
overwritten within about half a second unless the gate is open — because you are holding the key,
or because Directive 47 heard somebody start talking. Only that stretch goes any further, and
"further" means a speech model on your own machine.

A few small kindnesses:

- A press shorter than a quarter of a second is treated as a mis-press. Transcribing 80
  milliseconds of room tone produces a confident wrong word, which is worse than nothing.
- If your microphone is unplugged mid-sentence, what was captured is dropped rather than
  transcribed — half a sentence you did not finish is worse than none.
- A key stuck down stops at 60 seconds and **keeps** the audio. You said something; better late
  than lost.
- Hands free, a stretch that turns out to have been somebody else talking is dropped without
  reaching the transcript, the panel or the log.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `get_listening_status`

Read-only. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**One gate policy over a continuous stream.** The microphone runs whenever D47 runs, into a small
ring buffer; the gate decides which part of that stream was speech addressed to D47. It also means
the key-down path awaits nothing — opening a WASAPI capture device takes tens of milliseconds.

Phase 13 is what that sentence was written for, and it held. Voice activity is a detector on the
same `Write` path opening the same gate; the wake word does not touch the gate at all, because it
decides about *words* rather than about audio — the detector segments the stream, Whisper turns a
segment into text, and `WakeWordGate` decides whether that text was addressed to D47. So the wake
word is a settings row rather than a second model to ship, a second download, and a fixed
vocabulary chosen at build time that could never be the name the Commander gave their ship's AI.
Its cost is stated rather than hidden: everything said in the room is transcribed before it can be
discarded.

**The detector decides on the audio thread and the tick thread acts on it.** Opening the gate
plays a cue and closing it hands an utterance to a transcriber, and neither belongs on a real-time
callback. The cost is up to one tick at each end — which the pre-roll already covers at the front
and the 700 ms hangover dwarfs at the back.

**AEC3 consumes the arbiter's render reference tap, not a loopback capture.** The tap has existed
since Phase 5 with nothing subscribed to it, precisely so this could be one subscription rather
than surgery on the component every voice path depends on (architecture.md D7). A loopback capture
would be a second WASAPI stream, on a device that may not be the one D47 is rendering to, arriving
late with a clock of its own. Both directions are re-framed to the 10 ms the module takes, and the
remainder is carried rather than padded — padding tells the canceller the Commander stopped
talking, mid-word, several times a second. Measured against a simulated room with a 60 ms path:
upwards of 25 dB removed, and the near end kept within a few dB during double-talk. A *harmonic*
far end defeats the delay estimator entirely, which is worth knowing before writing a test with a
sine wave in it.

Whether the canceller is running is read from the canceller, never from the row that asked for it:
that boolean is what decides whether hands-free listening stays open while D47 speaks, and one
that was asked for and failed to load its native library must not be believed in.

**Why the key is polled rather than hooked.** `RegisterHotKey`, which the silence key uses,
delivers `WM_HOTKEY` on press only; push-to-talk is defined by its release edge, so it is not a
candidate. A `WH_KEYBOARD_LL` hook has both edges and is forbidden — a global input chokepoint
means a stall in D47 is a stall in the Commander's controls mid-fight. Raw Input with
`RIDEV_INPUTSINK` works and is not a hook, but delivers *every keystroke on the system* to D47's
window, including passwords typed elsewhere; rejected on privacy. `GetAsyncKeyState` polled from
the tick loop reads exactly the one bound virtual-key code and never the keyboard.

Its cost is latency — 10 Hz sampling means a key-down is seen up to 100 ms late, which is why the
tick runs at the top of the 4–10 Hz band. The pre-roll absorbs it: the gate opens *retroactively*
into the ring buffer, 500 ms by default, so audio captured before the key was noticed is still
part of the utterance. The edge is computed against the previous sample rather than trusting
`GetAsyncKeyState`'s low bit, which is shared process-wide.

**Binds resolution has three traps.** The active preset is named in `StartPreset.<major>.start`
and must not be assumed to be `Custom` — parsing `Custom.*.binds` on a machine running
`KeyboardMouseOnly` reports the wrong keys with total confidence. Shipped presets live in the
game install directory rather than the user profile, so a Commander who never customised their
controls has no file under `Options\Bindings\` at all. Version suffixes compare numerically per
segment, so `4.10` sorts above `4.2`. Gestures are normalised before comparing, so `Ctrl+Alt+X`
and `LeftControl+LeftAlt+X` are the same key.

The fetch is started from the composition root rather than the settings panel, so it happens
however the model came to be selected — the panel, the keyword router, or a hand-edited settings
file — and one at a time, since listening settings are applied on every change. The download is
hashed as it lands and the write is atomic; a half-downloaded model under its real name loads and then fails
mid-transcription. Transcription runs on the thread pool, never the audio thread. A transcript
that is entirely a bracketed annotation — `[BLANK_AUDIO]`, `(wind blowing)` — is treated as
silence. Rebinding while the key is held forces a release first, or the gate stays open with
nothing able to close it.

</details>
