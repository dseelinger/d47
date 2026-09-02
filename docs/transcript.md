---
title: The Transcript page
group: General help
nav_order: 4
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.

  The three cards at the foot carry class="card settings". On the site that is an ordinary
  link to the page about the same subject; in the panel it jumps to those rows in Settings.
  One href serves both, which is the whole reason the marker is a class and not an address.

  The readings named here are NavCrumb.Word values registered in PanelView.axaml.cs, and this
  page went stale once already because nothing compared the two (#251). HelpNamesTheReadings
  now fails the build if a retired reading's name appears in any help page.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">The page you land on: what every control does, and which three settings decide the answers.</p>
<section>
<h2><span class="num">1</span> Three readings in the drop-down, and a fourth behind a switch.</h2>
<svg viewBox="0 0 880 322" role="img" aria-label="The drop-down offers In Ship, Log File and Journal File; a Raw switch beside it turns the journal into the file's own JSON">
 <rect x="20" y="22" width="620" height="46" rx="10" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <rect x="28" y="29" width="190" height="32" rx="7" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="123" y="51" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">In Ship</text>
 <text x="330" y="51" text-anchor="middle" font-size="16" fill="var(--text-muted)">Log File</text>
 <text x="530" y="51" text-anchor="middle" font-size="16" fill="var(--text-muted)">Journal File</text>
 <text x="668" y="51" font-size="16" fill="var(--text-muted)">Raw</text>
 <rect x="712" y="30" width="60" height="30" rx="15" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <circle cx="729" cy="45" r="10" fill="var(--text-muted)"/>
 <rect x="20" y="92" width="270" height="128" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="155" y="128" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">IN SHIP</text>
 <text x="155" y="158" text-anchor="middle" font-size="14" fill="var(--text-muted)">what was said, here,</text>
 <text x="155" y="180" text-anchor="middle" font-size="14" fill="var(--text-muted)">between you and it</text>
 <rect x="305" y="92" width="270" height="128" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="128" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">LOG FILE</text>
 <text x="440" y="158" text-anchor="middle" font-size="14" fill="var(--text-muted)">what the whole app wrote</text>
 <text x="440" y="180" text-anchor="middle" font-size="14" fill="var(--text-muted)">to disk, this session</text>
 <rect x="590" y="92" width="270" height="128" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="725" y="128" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">JOURNAL FILE</text>
 <text x="725" y="158" text-anchor="middle" font-size="14" fill="var(--text-muted)">what the game wrote,</text>
 <text x="725" y="180" text-anchor="middle" font-size="14" fill="var(--text-muted)">as sentences you can read</text>
 <rect x="20" y="240" width="840" height="46" rx="10" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="269" text-anchor="middle" font-size="15" fill="var(--text)">The Raw switch is the fourth reading: the same journal events, as the JSON the game actually wrote.</text>
 <text x="440" y="312" text-anchor="middle" font-size="15" fill="var(--text-muted)">Each remembers where you were. Leaving Transcript and coming back returns to the one you were reading.</text>
</svg>
<p class="body">The first two are about Directive 47 and the last two are about Elite. That is the line to hold on to: <em>"why did it say that"</em> is the log file, and <em>"what did the game just do"</em> is the journal.</p>
</section>
<section>
<h2><span class="num">2</span> Two ways in, and the microphone always says which.</h2>
<svg viewBox="0 0 880 288" role="img" aria-label="The ask box sends on Enter; the microphone indicator shows one of three states">
 <rect x="20" y="26" width="620" height="52" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="58" font-size="16" fill="var(--text-muted)">Type here, and Enter sends it</text>
 <rect x="656" y="26" width="204" height="52" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="758" y="58" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">SEND</text>
 <text x="440" y="118" text-anchor="middle" font-size="15" fill="var(--text-muted)">Or speak — and the badge beside the box is always in one of these three states.</text>
 <rect x="20" y="140" width="270" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <circle cx="60" cy="176" r="9" fill="none" stroke="var(--text-muted)" stroke-width="2.5"/>
 <text x="82" y="182" font-size="17" font-weight="800" fill="var(--text-muted)">PTT Ready</text>
 <text x="155" y="216" text-anchor="middle" font-size="14" fill="var(--text-muted)">holding nothing; press your key</text>
 <rect x="305" y="140" width="270" height="96" rx="10" fill="var(--surface)" stroke="var(--info)" stroke-width="2.5"/>
 <circle cx="345" cy="176" r="9" fill="none" stroke="var(--info)" stroke-width="2.5"/>
 <text x="367" y="182" font-size="17" font-weight="800" fill="var(--info)">Listening...</text>
 <text x="440" y="216" text-anchor="middle" font-size="14" fill="var(--text-muted)">waiting for its name, no key held</text>
 <rect x="590" y="140" width="270" height="96" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <circle cx="630" cy="176" r="9" fill="var(--accent)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="652" y="182" font-size="17" font-weight="800" fill="var(--accent)">MIC ON</text>
 <text x="725" y="216" text-anchor="middle" font-size="14" fill="var(--text-muted)">your voice is being kept</text>
 <text x="440" y="268" text-anchor="middle" font-size="15" fill="var(--text-muted)">Filled circle, not just a colour — so the state that matters is readable without telling two colours apart.</text>
</svg>
<p class="body">The third does not say <em>push-to-talk</em> on purpose. A key you are holding and a gate Directive 47 opened for itself are the same fact about your microphone, and naming the key there would be false half the time.</p>
</section>
<section>
<h2><span class="num">3</span> Finding a line again, and taking it with you.</h2>
<svg viewBox="0 0 880 314" role="img" aria-label="Copy All takes the whole reading, Search finds a line, the banknote opens the receipt, and Newest returns you to the live end">
 <rect x="20" y="30" width="270" height="130" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">COPY ALL</text>
 <text x="155" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">the whole reading,</text>
 <text x="155" y="124" text-anchor="middle" font-size="14" fill="var(--text-muted)">not the selection</text>
 <text x="155" y="146" text-anchor="middle" font-size="14" fill="var(--text-muted)">— that is Ctrl+C</text>
 <rect x="305" y="30" width="270" height="130" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SEARCH</text>
 <text x="440" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">steps through matches</text>
 <text x="440" y="124" text-anchor="middle" font-size="14" fill="var(--text-muted)">on three readings, and</text>
 <text x="440" y="146" text-anchor="middle" font-size="14" fill="var(--text-muted)">filters the journal</text>
 <rect x="590" y="30" width="270" height="130" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="725" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">THE BANKNOTE</text>
 <text x="725" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">the receipt for the response</text>
 <text x="725" y="124" text-anchor="middle" font-size="14" fill="var(--text-muted)">you just heard — what it</text>
 <text x="725" y="146" text-anchor="middle" font-size="14" fill="var(--text-muted)">used, and what it cost</text>
 <rect x="20" y="184" width="840" height="72" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="214" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">Scroll away from the live end and a Newest button appears over the text.</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">Its arrow points where the newest line actually is — down on the first two readings, up on the two journal ones.</text>
 <text x="440" y="286" text-anchor="middle" font-size="15" fill="var(--text-muted)">Copy All, Search and Newest are on every reading. The banknote opens a window, so it is on the desktop only.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> Three settings stand behind every answer on this page.</h2>
<svg viewBox="0 0 880 262" role="img" aria-label="Your voice is heard by Whisper, answered by the language model, and spoken by a voice — three separate settings">
 <rect x="20" y="34" width="234" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="137" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">HEARD</text>
 <text x="137" y="102" text-anchor="middle" font-size="15" fill="var(--text-muted)">Whisper turns your</text>
 <text x="137" y="124" text-anchor="middle" font-size="15" fill="var(--text-muted)">voice into words</text>
 <line x1="266" y1="90" x2="302" y2="90" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="316,90 300,82 300,98" fill="var(--accent-muted)"/>
 <rect x="323" y="34" width="234" height="112" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">ANSWERED</text>
 <text x="440" y="102" text-anchor="middle" font-size="15" fill="var(--text-muted)">the language model,</text>
 <text x="440" y="124" text-anchor="middle" font-size="15" fill="var(--text-muted)">or nothing at all</text>
 <line x1="569" y1="90" x2="605" y2="90" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="619,90 603,82 603,98" fill="var(--accent-muted)"/>
 <rect x="626" y="34" width="234" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="743" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">SPOKEN</text>
 <text x="743" y="102" text-anchor="middle" font-size="15" fill="var(--text-muted)">a voice reads the</text>
 <text x="743" y="124" text-anchor="middle" font-size="15" fill="var(--text-muted)">answer back</text>
 <rect x="20" y="170" width="840" height="52" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="202" text-anchor="middle" font-size="16" fill="var(--text)">Three separate settings, and a quiet page is one of them switched off rather than all three broken.</text>
 <text x="440" y="246" text-anchor="middle" font-size="15" fill="var(--text-muted)">The three links below go straight to those rows.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card settings" href="capabilities/listening.html"><span class="ct">Listening →</span><span class="cd">Which Whisper model hears you, your microphone, and how talking is switched on.</span></a>
<a class="card settings" href="capabilities/conversation.html"><span class="ct">Language model →</span><span class="cd">Who answers, whether anything leaves this machine, and what it has cost.</span></a>
<a class="card settings" href="capabilities/speech.html"><span class="ct">Speech →</span><span class="cd">The voice that reads it back, and how fast.</span></a>
</div>
</div>
</div></div>

## The details

The Transcript page is where Directive 47 talks to you. It is the tab the panel opens on, and the
one it returns to when you leave everything else.

### The readings

The drop-down at the top of the page offers three readings, and a switch beside it offers a fourth.
Two of them are about Directive 47 and two are about Elite.

**In Ship** is what was said — your questions and the answers, in order, with a marker where the
core changed. This is the reading you want almost always, and it is drawn as a conversation: each
response in its own bubble, yours on the right.

**Log File** is Directive 47's own record on disk, read a screenful at a time. It is where every
diagnostic goes: what started, what the model was asked, what the headset did, and every error with
its cause.

**Journal File** is Elite's journal, the file the game itself writes, read as sentences rather than
as JSON. Docking, jumping, taking damage, a message from another Commander — the things that
happened, in the order they happened. Choosing a line opens its fields beside the list, and the
divider between the two panes can be dragged.

**Raw Journal** is the fourth reading, and the switch marked *Raw* beside the drop-down is how you
get to it: the same journal events as the file's own JSON. It is not an entry in the drop-down on
purpose — it is the same events seen another way rather than a fourth subject, and two entries would
read as two. The switch is in the headset as well as on the desktop, and *"raw journal"* reaches it
by voice.

Every one of them is a place you can **say** as well as press. Where the drawn name is not a phrase
anybody would utter in a cockpit, the reading answers to a shorter one instead — *"journal"* reaches
Journal File. Where a reading has been renamed, the name it used to carry goes on working: saying
*"conversation"* or *"thread"* still reaches In Ship. The label on the screen answers *what am I
looking at*; the spoken phrase answers *how do I ask for it*, and they stopped having to be the same
string in 0.96.0.

All four are **roots** rather than levels. Pressing Transcript while several levels deep in another
tab returns you to whichever one you were last reading, not to a fixed one. A reading that no longer
exists — one stored by an older version — falls back to In Ship rather than to a blank page.

### The controls around the readings

**Copy All** puts the entire reading on the clipboard. It is deliberately not called *Copy*: the
text on the page is selectable, and Ctrl+C already copies a selection, so a button called *Copy*
beside selectable text is a button that means two things. A search query does not narrow what it
copies — you asked for the reading, not for the matches.

**Search** does one of two things, and which one depends on the reading:

| Reading | What a search does |
|---|---|
| In Ship, Log File, Raw | Highlights every match, counts them, and steps forward and back through them |
| Journal File | Filters the list to the matching lines and says *"12 of 4,318"*; there are no steppers, because every line on screen is a hit |

On the Journal File reading it matches the event's own name as well as the sentence drawn from it.
That is worth knowing, because the thing you are hunting is frequently the name: `ShieldState`
appears nowhere in *"Shields back up"*, and typing it should not come back empty on the page whose
whole job is showing that event.

**Newest** appears over the text once you have scrolled away from the live end, and takes you back
to it. Its arrow points where the newest line actually is: **down** on In Ship and Log File, which
grow downwards, and **up** on the two journal readings, which are written newest-first. It is hidden
while you are already at the newest line, because a control that does nothing sitting over the text
it does nothing to is worse than no control.

**The banknote** at the right of the status line opens the receipt for the most recent response —
the tools it ran, the tokens and characters it spent, and the price. It was the word *Details* until
0.93.0; hovering still says what it does, and so does a screen reader. It opens a window, so it is
on the desktop and not in a headset, and there is no second place these figures are shown.

It is a note rather than a coin on purpose. These are dollars on a provider account, not your
in-game balance, and a coin in a cockpit overlay is the thing that gets read as credits.

**The ask box** takes typing, and Enter sends. The button beside it does the same thing and exists
so that the first thing you do with Directive 47 is not a guess.

**Clear what is shown** — on the right-click menu, and <kbd>Ctrl</kbd>+<kbd>L</kbd> — empties the
reading you are looking at. In practice that means **In Ship**, because it is the only reading that
is Directive 47's own: the other three are files on disk that it reads and does not write, and a
control appearing to empty one would be offering to delete it. It is greyed on those three rather
than quietly doing nothing, so you can see the refusal before you press it.

It clears the page and not the record. The model still remembers the conversation, so a follow-up
question is answered as if you had not cleared anything, and the log file on disk is untouched.

**The microphone badge** is never silent about its own state. *PTT Ready* means push-to-talk is
armed and nothing is being kept. *Listening...* means there is no key to hold and it is waiting to
hear its name. *MIC ON* means your voice is being captured right now — and it is drawn filled as
well as coloured, so it stays readable if the two colours are hard to tell apart.

### What decides the answers

Three settings sections, and they fail independently:

| If this is wrong | You see |
|---|---|
| **Listening** — the Whisper model, the microphone, the mode | Nothing appears when you speak, though typing works |
| **Language model** — the provider and key | Only the questions Directive 47 answers on its own |
| **Speech** — the voice | Answers appear on the page but are never read aloud |

That is why a quiet page is usually one of the three switched off rather than everything broken,
and why the three links above go to those rows rather than to three more explanations.
