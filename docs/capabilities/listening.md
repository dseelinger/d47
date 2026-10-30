---
title: Listening
group: Voice
nav_order: 124
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
<p class="intro">Four steps to being heard.</p>
<section>
<h2><span class="num">1</span> Pick a microphone.</h2>
<p class="say">Open Settings › Hearing and sound › Voice Input, group Microphone, and pick a Microphone.</p>
<p class="expect">The row shows the device. Left unset it follows your Windows Default Device.</p>
<svg viewBox="0 0 880 150" role="img" aria-label="The Microphone row">
 <rect x="20" y="20" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="44" y="53" font-size="17" font-weight="700" fill="var(--text)">Microphone</text>
 <text x="836" y="53" text-anchor="end" font-size="17" fill="var(--text)">(the system default)</text>
 <text x="20" y="112" font-size="16" fill="var(--text-muted)">Pick the real device, not a virtual one.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Hold push-to-talk and ask.</h2>
<p class="say">Hold Scroll Lock, the default Push-to-talk key, and say "can you hear me".</p>
<p class="expect">The reply begins "Yes" and names the microphone. If it begins "No — not properly", it lists each fault.</p>
<svg viewBox="0 0 880 140" role="img" aria-label="The ask row with a hearing check typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">can you hear me</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Cancel, Ctrl+Alt+X by default, stops the turn.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Change when D47 listens.</h2>
<p class="say">Say "listen for your name", then "only listen when I hold the key".</p>
<p class="expect">The row How D47 decides you are talking to it reads Listen when I say its name, then Press to talk (PTT).</p>
<svg viewBox="0 0 880 150" role="img" aria-label="The How D47 decides you are talking to it row">
 <rect x="20" y="20" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="44" y="53" font-size="17" font-weight="700" fill="var(--text)">How D47 decides you are talking to it</text>
 <text x="836" y="53" text-anchor="end" font-size="17" fill="var(--text)">Press to talk (PTT)</text>
 <text x="20" y="112" font-size="16" fill="var(--text-muted)">"listen whenever I speak" is the third value.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> Ask for the word behind a letter.</h2>
<p class="say">Say "what is the word for k".</p>
<p class="expect">D47 answers "K is kilo." Those words are what you say to spell a value onto the drawn keyboard.</p>
<svg viewBox="0 0 880 140" role="img" aria-label="The ask row with a spelling question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what is the word for k</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"the phonetic alphabet" gives every letter's word.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> You are not heard.</h2>
<p class="say">Check the Microphone row names your real microphone, not a virtual device from VR or streaming software.</p>
<p class="expect">Then "can you hear me" answers Yes and names that device.</p>
<svg viewBox="0 0 880 150" role="img" aria-label="A virtual device in the Microphone row">
 <rect x="20" y="20" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="44" y="53" font-size="17" font-weight="700" fill="var(--text)">Microphone</text>
 <text x="836" y="53" text-anchor="end" font-size="17" fill="var(--text)">a virtual device</text>
 <text x="20" y="112" font-size="16" fill="var(--text-muted)">It can be capturing and still send silence.</text>
</svg>
</section>
</div></div>
</details>

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Your voice becomes text on this computer unless you choose a hosted provider, and one setting decides when the microphone opens.</p>
<section>
<h2><span class="num">1</span> One setting decides when the microphone opens.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="Four values: press to talk, toggle, listen whenever I speak, and listen for its name">
 <rect x="20" y="24" width="840" height="46" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="53" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">PRESS TO TALK   TOGGLE</text>
 <rect x="20" y="80" width="840" height="46" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="109" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">WHENEVER I SPEAK   ITS NAME</text>
</svg>
<p class="body">The row How D47 decides you are talking to it takes one of four values. Press to talk is the default, and it opens the microphone only while the key is held.</p>
</section>
<section>
<h2><span class="num">2</span> Speech becomes words on this computer.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="A local speech model, or a hosted provider">
 <rect x="20" y="24" width="840" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">HEARING PROVIDER</text>
 <text x="440" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">this computer by default; Groq, OpenAI, Deepgram or ElevenLabs if chosen</text>
</svg>
<p class="body">A speech model runs locally after one download. The Hearing provider row can send your audio to a hosted provider instead, and the row says what that sends.</p>
</section>
<section>
<h2><span class="num">3</span> You hold the key and speak.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="Key held, words transcribed, text handled as if typed">
 <rect x="20" y="24" width="250" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">Hold Scroll Lock</text>
 <text x="145" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">say it</text>
 <line x1="282" y1="69" x2="296" y2="69" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="310,69 294,61 294,77" fill="var(--accent-muted)"/>
 <rect x="320" y="24" width="250" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="445" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">Transcript</text>
 <text x="445" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">can you hear me</text>
 <line x1="582" y1="69" x2="596" y2="69" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="610,69 594,61 594,77" fill="var(--accent-muted)"/>
 <rect x="620" y="24" width="240" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="740" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">Answer</text>
 <text x="740" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">Yes, and the device</text>
</svg>
<p class="body">D47 transcribes what you said when you let go and handles the text as if you had typed it. The status tool then answers from the live microphone state.</p>
</section>
<section>
<h2><span class="num">4</span> Not being heard has a named cause.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="The status tool reports the microphone, the key, the model and any clash with Elite">
 <rect x="20" y="24" width="840" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">listening status</text>
 <text x="440" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">microphone, key, model, key clash</text>
</svg>
<p class="body">The status tool is declared as <code>Report whether D47 can hear the Commander</code>, and it lists each fault it finds.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card settings" href="speech.html"><span class="ct">Speech →</span><span class="cd">The other half of talking: the voice that reads the answer back.</span></a>
<a class="card" href="../in-ship.html"><span class="ct">In Ship →</span><span class="cd">What the microphone badge is telling you, and the rest of the controls there.</span></a>
<a class="card" href="privacy.html"><span class="ct">Privacy →</span><span class="cd">Everything that reaches a network, counted rather than promised.</span></a>
</div>
</div>
</div></div>

## The details

Talking to Directive 47 instead of typing at it. Hold a key, speak, let go, and what you said is
handled exactly as if you had typed it — or put your hands back on the stick and let it decide for
itself when you are talking to it.

**Out of the box, no audio and no transcript leaves your machine.** Speech is turned into words by
a model running on your own computer. The model file itself is downloaded once, from
`huggingface.co`; after that, nothing about your speech goes anywhere. The
[hearing provider](#provider) row can send it to Groq, OpenAI, Deepgram or ElevenLabs instead, and says what that sends.

### Ask for it

> "can you hear me"
> "what microphone are you using"
> "is my push to talk key bound twice"

```text
No — not properly.
The microphone is not capturing. <the reason D47 recorded>
Warning: CapsLock is also bound in Elite (KeyboardMouseOnly) to HeadLookToggle. One of the two
will not work, and neither will say so — pick another key for one of them.
```

When nothing is wrong the reply begins "Yes" and names the microphone, for example
`Yes — I just heard you, on Yeti Nano.` The reply checks four faults together: no push-to-talk key
or button set, a microphone that is not capturing, no speech model, and a key or button that
clashes with an Elite binding.

### Getting set up

Three things, in this order:

**1. Bind a key.** Already done — **Scroll Lock**, out of the box. Clear the row if you would
rather Directive 47 never opened the microphone at all.

**2. Download a speech model.** Also already done, or under way. Directive 47 ships with **Base
(English only)** selected and fetches it from `huggingface.co` the first time it starts — about
142 MB, once. Until it lands, Directive 47 captures your voice and tells you plainly that it
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

**And the same for a stick button, checked against that controller only.** Elite names a
controller by its vendor and product ids, and Directive 47 reads the same ids from the controller
the button is on, so a binding on another stick on your desk is not reported. Two cases stay
uncertain, and it says so. If the controller is not connected, every controller is checked. If
Windows shows the controller as several devices, as a WinWing throttle does, Elite's binding names
one of them, and Directive 47 cannot tell which one the button is on:

```text
Warning: button 9 may collide. Windows shows that controller as 4 devices, and Elite (Custom)
binds a button of that number on one of them to ShipSpotLightToggle. I cannot tell whether it is
the device the button is on. If the microphone will not open, this is the first thing to check.
```

**Your bindings are never written to.** Directive 47 only ever reads them.

Only a clash is reported. When none is found, the reply says nothing about bindings.

### Settings

#### Microphone {#microphone}

Which input to listen on. Leave it unset and Directive 47 follows your Default Device — the
microphone Windows Sound settings shows first — not the separate Communications default some
headsets split off. Left on "system default", it keeps following that row while it runs, so
switching to a headset mid-session moves the open microphone with it. A device you have chosen by
name stays chosen instead.

If the one you chose disappears, the status answer names it rather than reporting generic
silence. No microphone at all is a feature being off, not a failure — Directive 47 stays fully
usable typed. A move that lands mid-sentence says it did not catch that rather than dropping the
words silently.

#### Push-to-talk {#push-to-talk-key}

What you hold to talk. **Scroll Lock out of the box** — Windows assigns it no shortcut and few games use it, so it
seldom collides with a key you already use.

**One row, and it takes a key, a stick button, or both.** Click the key slot and press a
key, or click the stick slot and press a button on your stick; Directive 47 works out which button it
was. A click anywhere else leaves the slot as it was.

**To have both, bind twice — one gesture each time.** Click the key slot and give it a key; click
the stick slot and give it a button. The two are stored separately, so the second does not replace the first,
and the row then shows `SCROLL` and `BUTTON 11`. Either one opens the microphone, and the last one you
let go of closes it — so letting go of the key while your thumb is still on the button does not cut
you off mid-sentence.

**It is one of each, not any number.** There is one slot for a key and one for a stick button. Give
it a second key and the key changes; give it a second button and the button changes. Nothing is ever
silently added to a list you cannot see — what the row reads is exactly what is bound, and two
gestures is the most it will ever say.

**A key on its own, like right shift, binds when you let it go.** Modifiers are the one kind of key
that cannot be taken the instant they go down, because that is also how a combination starts. Hold
one and press something else and you get the combination; press one and release it and you get the
modifier. Scroll Lock, the default, is not a modifier and binds as soon as you press it.

**Esc unbinds one slot.** While a slot listens, Esc empties that slot and leaves the other as it was.

The stick half is [described below](#push-to-talk-button).

Clear the row and Directive 47 never opens the microphone — unless you have also put
[the row below](#mode) into one of its hands-free settings, which is the reason for those and
the only case where an unbound key still leaves a live microphone.

In the two key-driven settings, nothing is kept unless the key is held.

**Pressing it does not silence Directive 47.** Interrupting is **Cancel**, the row directly below
this one, which stops the voice and abandons the turn.

Unlike the stop key, this one does not need a modifier — a bare key is the normal arrangement for
push-to-talk, which is exactly why the collision check above matters. If Scroll Lock is bound to
something in Elite on your setup, the status report above says so by name.

**The model cannot change this.** A model that could unbind your microphone key has taken away
how you talk to it.

#### The stick half {#push-to-talk-button}

It is part of the **Push-to-talk** row above. This section is about what happens when you press a
stick button at it.

Click the stick slot, then press and release the button you want, and Directive 47 works out
which one it was. It is stored separately from your key, so binding a button does not unbind the
key: you said two things rather than changed your mind about one.

**It has to be a button that springs back.** A switch that stays where you put it would hold the
microphone open until you moved it again, so the capture declines one and says why. Those belong
on the [switch panel](switches.html) instead.

**If the controller is not there when D47 starts**, it says so and your key carries on working. A
stick that is asleep is one of the ways "D47 cannot hear me" happens with no reason attached.

**The collision check is weaker here than for a key, and it says so.** Elite records a joystick
binding against its own internal name for the device, which is not the name Windows gives it, so
Directive 47 cannot tell whether Elite's *button 24* is on the *same* stick as yours. What it can
say is that a button of that number is spoken for somewhere — worth saying on a HOTAS, where every
button is usually already used:

```text
Warning: button 24 may collide. Elite (Custom) binds a button of that number to UseBoostJuice, and
I cannot tell whether that is the same controller. If the microphone will not open, this is the
first thing to check.
```

**The model cannot change this one either**, for the same reason.

#### Cancel {#cancel}

**`Ctrl+Alt+X` out of the box.** One press does three things:

- stops Directive 47 talking, instantly, mid-word;
- **abandons the turn it is working on** — the model stops, and so does the spending. This is the
  one worth knowing about: a web search that is going to take twenty seconds and cost you money is
  stopped by this and is not stopped by saying "be quiet";
- does both from anywhere, including while Elite has the foreground.

**Bind a key, a stick button, or one of each.** The button half is what makes this usable in the
middle of a fight, and it is the only other binding in Directive 47 that takes one — the interface
hotkeys stay keyboard-only, because you press those sitting at the window. Giving it the same kind
twice replaces that one; **Esc** while a slot listens unbinds that slot.

Claimed system-wide rather than only while Directive 47 has focus, because the moment you want it is
the moment Elite is in front — so the key half needs a modifier, and a bare letter is refused as you
press it. The stick half has no such rule.

**Push-to-talk does not do this.** It opens the microphone and does not silence Directive 47.

**The model cannot change this row.** It can be changed from the panel or by voice — but not by
anything the model calls. A model able to unbind the one control that outranks it has removed the
one control that outranks it, and that matters more now the same press ends the turn.

#### Comms push-to-talk {#comms-push-to-talk}

**Unbound out of the box.** A second push-to-talk, for speaking to whoever your comms channel
selection names rather than to Directive 47. Bind a key, a stick button, or one of each, the same
way as push-to-talk.

It opens the same microphone, with the same capture before the key and the same 60-second ceiling:

- in hold, hands-free and wake-word modes it is held, like push-to-talk;
- in toggle mode one press starts and the next press stops;
- while one of the two keys holds the microphone open, the other does nothing;
- when hands-free listening has already opened the microphone, pressing it makes what is being said
  a comms capture.

A comms capture goes to whoever the [comms selection](comms.md#choose-who-your-comms-key-reaches)
names, and push-to-talk goes only to the core.

**The model cannot change this row**, for the same reason as push-to-talk.

#### How D47 decides you are talking to it {#mode}

Four settings, and the key works in all four — a policy that decides for itself is not a reason to
take away the one that does not.

| | What it does |
|---|---|
| **Press to talk (PTT)** | Hold the key while you speak. The shipped default. |
| **Toggle on and off** | Press once to start, again to stop. |
| **Listen whenever I speak** | Directive 47 opens the microphone itself when it hears somebody start talking, and closes it when they stop. |
| **Listen when I say its name** | The same, except what you said is thrown away unless you addressed it by name. |

**What the last two actually mean.** The microphone is always open, so the
pre-roll has something in it, but nothing is *kept* unless the key is held. In the two hands-free
settings, every stretch of speech in the room is captured and
transcribed before Directive 47 can decide whether it was meant for it. They are off out of the box.

What does not change: **none of it is written to disk, and with the local speech model none of it
leaves your machine.** With a [hosted provider](#provider), every one of those stretches is sent to
it, because whether you said Directive 47's name is decided from the words it sends back. A stretch that was not addressed to Directive 47 is discarded without
reaching the transcript, and the panel says the microphone is open the whole time it is — see
[Seeing that the microphone is open](#indicator).

The cost is CPU. Everything said near you is transcribed and then mostly thrown away, so run these
on one of the smaller models unless you have cycles to spare.

**The model cannot change this row.** A model that could put Directive 47 into continuous
listening could start capturing on your machine. You can still say *"listen for your name"* or *"stop listening
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
the box.

#### What D47 answers to {#wake-words}

Only applies in **Listen when I say its name**. Leave it unset and Directive 47 answers to whatever
you call your core, so renaming the core renames the wake word with it. "D47" always works as
well, whatever this is set to.

Every wake phrase addresses the core. Saying one closes any conversation with the captain, a
crew member or anyone else, and what follows it goes to the core — so "Warden, captain, how's
our fuel?" reaches the captain, and "Warden, request docking" requests docking.

Set it — comma-separated — when the speech model keeps hearing the name as something else. It is
already forgiving: the comparison ignores punctuation and spacing, so `D47`, `D 47`, `d-47` and
`D47.` are all the same word. Adding a spelling is for when it comes out as something genuinely
different.

The name has to be near the front of what you said. Talking *about* Directive 47 is not talking
*to* it.

#### How long D47 keeps listening after you say its name {#wake-window}

Say the name on its own, Directive 47 sounds its listening cue, and the next thing you say is the
request — the way you would address a person. **12 seconds** out of the box, and the follow-up
closes the window again: it is one reply, not an open microphone. Nothing is said back and nothing
reaches the transcript; being called by name is not conversation.

Set it to zero if you would rather the name and the request always arrived in the same breath.

#### Cancel D47's own voice out of the microphone {#echo-cancellation}

On, in every setting rather than only the hands-free ones — holding the key while a callout is
being read out otherwise transcribes the callout.

On speakers, this is what lets you **talk over Directive 47**.

If it cannot start — the native library missing, usually — it says so and the microphone keeps
working. Hands-free listening then goes deaf while Directive 47 is speaking.

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

#### Names it has learned to hear {#corrections}

Proper nouns are where speech recognition fails hardest and least visibly. A misheard system name
does not come back as an error or as a low-confidence marker — it comes back as a plausible English
word, and the answer is confidently about the wrong place. Directive 47 already biases the
transcriber towards the names around you, but that list holds up to 240 names, 60 of them from your
journal, so a name you have not been near lately cannot be on it.

**So there is a recovery.** Ask about a system it cannot find and it will not shrug politely: it
looks through the places *you* have actually been — every system, station and faction in your own
journals — for one that sounds like what it heard, offers those back, and runs your question again
when you say which. Nothing is sent anywhere to do it; those names came off your own disk.

**What it remembers is the word, not the answer.** Correct it once about *Eurebia* and it has
learned that word, so *"the Eurybia Blue Mafia"* comes out right too, and so does every other
sentence containing it.
**It only ever learns when you correct it**, and it refuses to learn a word that already means
something — a place you have visited, or a phrase Directive 47 answers to. It is also spoken input only: if you *type* a name, that
is what you meant.

This row lists what it has learned, and **Forget them all** clears the lot. Everything here stays on
this machine and belongs to the Commander flying — nothing is learned from a chat message, a mission
description, or anything another player wrote.

#### Hearing provider {#provider}

Who turns your speech into words. **This computer** runs the Whisper model chosen below and sends
nothing. **Groq**, **OpenAI**, **Deepgram** and **ElevenLabs Scribe** are hosted: no model sits in
your memory, and the audio goes to them instead. Each uses one fixed model — `whisper-large-v3-turbo`
on Groq, `gpt-4o-mini-transcribe` on OpenAI, `nova-3` on Deepgram, `scribe_v2` on ElevenLabs. Deepgram also says how sure it is of what it
heard, so a panel prompt can ask you to say it again or type it when it is unsure.

What a hosted provider receives: the audio of every utterance Directive 47 transcribes, your API key,
and the names from your journal it uses to recognise proper nouns. In push-to-talk that is what you
held the key for. Hands free it is every stretch the microphone judged to be speech, including ones
not addressed to Directive 47. No journal files, game state or other keys are sent. The
[Privacy](privacy.md#egress-stt) section shows the same thing, computed from your settings.

**If the service cannot be reached, it says so and does nothing else.** There is no fallback to a
local model. What you hear instead:

| What went wrong | What Directive 47 says |
|---|---|
| No key stored | "Groq needs an API key. Add it in Settings." — and nothing is sent |
| No answer | "I couldn't reach Groq. Say it again, or type it." |
| Key refused | "Groq refused the key. Check it in Settings." |
| Too many requests | "Groq is limiting requests. Try again in a moment." |
| Anything else | "Groq couldn't transcribe that:" and the service's own message |

The check for words invented from silence still runs beside every hosted request, on the Tiny
model, when `ggml-tiny.en.bin` is in the models folder. Without it the check is skipped.

The **Speech model** and **GPU** rows below apply only to **This computer**.

```csharp
if (SttProviderCatalog.Selected(listening.Provider).Hosted)
{
    return new SpeechModelPlan { Action = SpeechModelAction.Unload };
}
```

#### API key {#provider-key}

One row for the selected hosted provider. Groq's and Deepgram's keys are their own; OpenAI's is the same key the OpenAI
language model and voice already use, so storing it once serves all three. ElevenLabs Scribe shares the
key the ElevenLabs voice uses, so a key already stored for the voice shows here as stored. Stored encrypted for this
Windows account and never shown back.

#### Speech model {#model}

Which Whisper model turns your speech into words. The row marks which are already on disk and
what the others would cost to fetch, so you can see which choices are already paid for.

**Base (English only)** is what a fresh install has selected. Tiny is cheaper and is offered, but
it mis-hears the words Directive 47 acts on.

**Every model here is English-only.** If your settings still name a multilingual one, you get its
English twin automatically.

**What a bigger model costs is time per *sentence*, not time per second of audio.** Whisper reads
your speech in 30-second windows and pays for the whole window whether you filled it or not, so a
two-second question and a twenty-second one cost the same. Anything over 30 seconds in one breath
costs another window. That is the whole delay between you letting go of the key and Directive 47
starting to think, so if it feels slow to answer, this row is the first place to look.

**Medium is the most accurate and effectively needs a GPU.** On the CPU it is slow, and the GPU is
faster for every model.

**The device never changes the words.** Running on the GPU is a speed and memory choice only.

**Choosing a model that is not on disk asks first.** The confirmation reads "Download N MB and use
it" and nothing downloads until you agree. A model that is selected and missing is fetched at
startup. `huggingface.co` is listed under [Privacy](privacy.md) for as long as a model is
selected.

**Directive 47 knows what each model file should be.** The SHA-256 of every model it offers is
written into the build, and a download that does not match it is discarded rather than loaded.
That is worth one sentence about what it actually buys: those values were read from
`huggingface.co` once, on a stated date, and pinning them does not make that first read
trustworthy — it means the file *changing* afterwards becomes visible.

`none` stays a real choice. Pick it and Directive 47 hears you and says plainly that it cannot
turn what it heard into words.

#### Running on the GPU {#gpu}

Off by default, and faster when you turn it on.

It costs video memory, which comes out of whatever else uses the card. It is close to the size of
the model file:

| Model | Video memory |
|---|---|
| Tiny (English only) | about 100 MB |
| Base (English only) | about 150 MB |
| Small (English only) | about 470 MB |
**In VR that trade is a real one.** Your GPU is already the scarce thing there, and taking memory
and time from the game shows up as dropped frames rather than as anything that looks like a
speech problem. On the desktop window, with headroom to spare, it is close to free.

**If your machine has no GPU D47 can use, it runs on the CPU and says so** — in the log, and in
what it reports about itself. It does not fail, and it does not claim a device it is not using.

The switch takes effect immediately, both ways: turn it off and the video memory is handed back.

D47 uses Vulkan, which works on AMD and Intel cards as well as NVIDIA.

### Seeing that the microphone is open {#indicator}

Bottom left of the panel, on the desktop **and** in the headset, in mini as well as full. Some
of its states:

| | |
|---|---|
| **PTT READY** | Push-to-talk at rest. Audio runs into the half-second ring and is overwritten, and nothing is kept. |
| **LISTENING** | Hands free. Directive 47 is deciding for itself when to listen. |
| **MIC ON** *(filled, ringed)* | The gate is open. What is arriving now will be transcribed. |

Filled or hollow is the state, not only the colour — a glance reads a shape first, and a difference
that is only colour is not a difference for everybody. Hover it for the gesture, or the name, that
would open the gate.

When no device is open it reads **MIC OFF**, hollow and red. While the speech model loads it
reads **LOADING MODEL**.

### Downloading a model {#download}

A selected model that is not on disk is fetched at startup, or after you agree to the
confirmation when you choose it. The shipped default is Base (English only).

The download is checked against the pinned checksum once it has finished, and is deleted if it
does not match. The row shows the size written into the app.

If the download fails — no network, the host refusing — the selection stays where it is and
Directive 47 says it has no speech model loaded when you ask. It tries again the next time it
starts. Choose `none` if you would rather it stopped trying.

### Spelling a value onto a keyboard {#spelling}

Wherever Directive 47 draws a keyboard — the panel's own, and the one a controller ray opens on a
text box in the headset — you can say the value instead of pointing at every key.

Say each letter as its word:

> "alpha bravo seven done"

That types `ab7` and presses Done, in one breath. **Done is a key**, so an utterance can spell a
value and commit it together. So are `delete`, `clear` and `cancel`.

The words are the NATO alphabet, which is the one spelling alphabet a Commander in Elite already
knows.
| Letter | Word | Letter | Word |
|---|---|---|---|
| A | alpha | N | november |
| B | bravo | O | oscar |
| C | charlie | P | papa |
| D | delta | Q | quebec |
| E | echo | R | romeo |
| F | foxtrot | S | sierra |
| G | golf | T | tango |
| H | hotel | U | uniform |
| I | india | V | victor |
| J | juliett | W | whiskey |
| K | kilo | X | x-ray |
| L | lima | Y | yankee |
| M | mike | Z | zulu |

Digits are said either way — "seven" or "7". The keys that are neither a letter nor a digit have
one word each: `space`, `dash`, `underscore`, `dot`, `delete`, `clear`, `done` and `cancel`.

**It is not a mode.** Every utterance is tried as spelling first. If every word is a key, every
key is pressed in order. If any word is not, **nothing is pressed at all** — the state line names
the word it could not take, and the whole utterance lands in the field as a value instead. So
"Shinrarta Dezhra" arrives whole, and "Alpha Centauri" fails on its second word and arrives whole
too. Numbers are the case for spelling, and long names are the case for saying them.

A value that is itself one word of the alphabet — a system called Delta — is spelled. There is no
escape word: say `delta echo lima tango alpha` and it comes out right.

This is live **only while a keyboard is drawn**. In the headset that is whenever the board is up.
On the panel it is once the keys are showing, which is where a hearing that failed leaves you.
Otherwise "bravo" in conversation reaches Directive 47 as the word it is.

Presses go to the drawn keyboard and nowhere else. Nothing here reaches the keyboard Elite is
listening to.

Ask if you forget:

> "how do I spell something"
> "the phonetic alphabet"
> "what is the word for K"

```text
K is kilo.
```

Those are answered from the same table the keyboard parses.

### It knows what things are called

Every utterance is transcribed knowing the names around you: the system you are in, the station,
the body, your next jump, your ship and its type, your carrier, the route ahead, and your fleet.

This matters more than it sounds. **Proper nouns are where speech recognition fails hardest and
least visibly.** A misheard system name does not come back as an error — it comes back as a
plausible English phrase. Everything proceeds confidently about the wrong system, and nothing anywhere reports a problem.

The names come from your journal. Nothing is looked up.

### What happens to what you say

Spoken and typed questions run the same path, so "where am I" means the same thing however you
said it. The exceptions are phrases that are spoken only, such as "can you hear me".

Nothing captured is written to disk. Audio sits in a small buffer and is
overwritten within about half a second unless the gate is open (the key is held, or Directive 47
heard somebody start talking). Only that stretch goes any further. With the
local speech model, "further" means a model on your own machine. With a
[hosted provider](#provider), it means that stretch is sent to Groq, OpenAI, Deepgram or ElevenLabs with your key.

A few small kindnesses:

- A press shorter than a quarter of a second is treated as a mis-press.
- If your microphone is unplugged mid-sentence, what was captured is dropped rather than
  transcribed.
- A key stuck down stops at 60 seconds and **keeps** the audio.
- Hands free, a stretch that turns out to have been somebody else talking is dropped without
  reaching the transcript, the panel or the log.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `get_listening_status`

Read-only. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `get_spelling_alphabet`

Read-only, and answered from the parser's own table rather than by the model.

```json
{"type":"object","properties":{"letter":{"type":"string","description":"One letter to give the word for. Pass all for the whole alphabet. Left out, D47 says how spelling by voice works."}},"required":[],"additionalProperties":false}
```

**One gate policy over a continuous stream.** The microphone runs whenever D47 runs, into a small
ring buffer; the gate decides which part of that stream was speech addressed to D47. It also means
the key-down path awaits nothing.

Voice activity is a detector on the same `Write` path opening the same gate; the wake word does
not touch the gate at all, because it decides about *words* rather than about audio — the detector
segments the stream, Whisper turns a segment into text, and `WakeWordGate` decides whether that
text was addressed to D47. Everything said in the room is transcribed before it can be discarded.

**The detector decides on the audio thread and the tick thread acts on it.** Opening the gate
plays a cue and closing it hands an utterance to a transcriber, and neither belongs on a real-time
callback. The cost is up to one tick at each end — which the pre-roll already covers at the front
and the 700 ms hangover dwarfs at the back.

**AEC3 consumes the arbiter's render reference tap, not a loopback capture.** Both directions are re-framed to the 10 ms the module takes, and the
remainder is carried rather than padded — padding tells the canceller the Commander stopped
talking, mid-word, several times a second. A *harmonic*
far end defeats the delay estimator entirely, which is worth knowing before writing a test with a
sine wave in it.

Whether the canceller is running is read from the canceller, never from the row that asked for it:
that boolean is what decides whether hands-free listening stays open while D47 speaks, and one
that was asked for and failed to load its native library must not be believed in.

**The key is read by polling.** `GetAsyncKeyState` polled from the tick loop reads exactly the one
bound virtual-key code and never the keyboard.

Its cost is latency — 10 Hz sampling means a key-down is seen up to 100 ms late, which is why the
tick runs at the top of the 4–10 Hz band. The pre-roll absorbs it: the gate opens *retroactively*
into the ring buffer, 500 ms by default, so audio captured before the key was noticed is still
part of the utterance. The edge is computed against the previous sample rather than trusting
`GetAsyncKeyState`'s low bit, which is shared process-wide.

**Binds resolution has three traps.** The active preset is named in `StartPreset.<major>.start`
and must not be assumed to be `Custom` — parsing `Custom.*.binds` on a machine running
`KeyboardMouseOnly` reports the wrong keys. Shipped presets live in the
game install directory rather than the user profile, so a Commander who never customised their
controls has no file under `Options\Bindings\` at all. Version suffixes compare numerically per
segment, so `4.10` sorts above `4.2`. Gestures are normalised before comparing, so `Ctrl+Alt+X`
and `LeftControl+LeftAlt+X` are the same key.

The fetch is started from the composition root and runs one at a time, since listening settings are applied on every change. The download is
hashed after it lands and the write is atomic; a half-downloaded model under its real name loads and then fails
mid-transcription. Transcription runs on the thread pool, never the audio thread. A transcript
that is entirely a bracketed annotation — `[BLANK_AUDIO]`, `(wind blowing)` — is treated as
silence. Rebinding while the key is held forces a release first, or the gate stays open with
nothing able to close it.

</details>
