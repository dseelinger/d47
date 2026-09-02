---
title: In Ship
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

  One page per reading (#262). This one is In Ship and nothing else: the log file and the
  journal have their own, and the three used to be crammed into one that was about none of
  them. Keep it that way — the temptation is to explain "the readings" here because this is
  the one a Commander lands on.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">The reading you land on: you and the ship's AI, everything said, in order.</p>
<section>
<h2><span class="num">1</span> This reading is the conversation, drawn as one.</h2>
<svg viewBox="0 0 880 268" role="img" aria-label="Your question on the right, the answer on the left, and a note about the conversation across the middle">
 <rect x="470" y="24" width="390" height="52" rx="12" fill="var(--accent-muted)" stroke="var(--accent)" stroke-width="2"/>
 <text x="844" y="56" text-anchor="end" font-size="16" fill="var(--text)">Where am I?</text>
 <rect x="20" y="92" width="470" height="52" rx="12" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="40" y="124" font-size="16" fill="var(--text)">We're holding at Jameson Memorial.</text>
 <text x="440" y="182" text-anchor="middle" font-size="15" font-weight="700" fill="var(--accent)">[Switched to Cora]</text>
 <text x="440" y="212" text-anchor="middle" font-size="15" fill="var(--text-muted)">Yours on the right. The ship's on the left. A note about the conversation sits across the middle.</text>
 <text x="440" y="244" text-anchor="middle" font-size="15" fill="var(--text-muted)">A mark like that is the panel speaking, not a voice in the conversation — so it takes no side.</text>
</svg>
<p class="body">The other three readings are files on disk, and each has its own help. This one is held in memory and is the only reading that is Directive 47's own.</p>
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
<h2><span class="num">3</span> The controls around it.</h2>
<svg viewBox="0 0 880 320" role="img" aria-label="Copy All, Search, the banknote, and Clear what is shown">
 <rect x="20" y="26" width="270" height="126" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="64" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">COPY ALL</text>
 <text x="155" y="96" text-anchor="middle" font-size="14" fill="var(--text-muted)">the whole conversation,</text>
 <text x="155" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">not the selection</text>
 <text x="155" y="140" text-anchor="middle" font-size="14" fill="var(--text-muted)">— that is Ctrl+C</text>
 <rect x="305" y="26" width="270" height="126" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="64" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SEARCH</text>
 <text x="440" y="96" text-anchor="middle" font-size="14" fill="var(--text-muted)">counts every match and</text>
 <text x="440" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">steps you through them</text>
 <text x="440" y="140" text-anchor="middle" font-size="14" fill="var(--text-muted)">both ways</text>
 <rect x="590" y="26" width="270" height="126" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="725" y="64" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">THE BANKNOTE</text>
 <text x="725" y="96" text-anchor="middle" font-size="14" fill="var(--text-muted)">the receipt for the last</text>
 <text x="725" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">response — what it used,</text>
 <text x="725" y="140" text-anchor="middle" font-size="14" fill="var(--text-muted)">and what it cost</text>
 <rect x="20" y="180" width="840" height="76" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="212" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">Clear what is shown — right-click, or Ctrl+L</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">Empties this page and nothing else. It is greyed on the three readings that are files on disk.</text>
 <text x="440" y="288" text-anchor="middle" font-size="15" fill="var(--text-muted)">Scroll away from the newest line and a ↓ Newest button appears to bring you back.</text>
 <text x="440" y="312" text-anchor="middle" font-size="15" fill="var(--text-muted)">The banknote opens a window, so it is on the desktop only.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> Three settings stand behind every answer here.</h2>
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

**In Ship** is the Transcript's first reading and the one the panel opens on. It is what was said
between you and Directive 47 — your questions and its answers, in order, with a marker where the
core changed.

It is the only reading held in memory rather than read from a file, and that one fact decides most
of what follows.

### How it is drawn

Each response gets a bubble: yours on the right in the theme's own colour, the ship's on the left.
When Directive 47 notes something *about* the conversation rather than saying something in it —
the core changing under you — that sits across the middle in the accent with no bubble, because it
is not a side.

The headset's big panel does the same. The mini panel does too and spends less on it: the same
sides and the same colours, with the gutter and most of the padding given back, because a surface
512 pixels across cannot afford to say twice over which side a line is on.

### The controls

**Copy All** puts the entire conversation on the clipboard. It is deliberately not called *Copy*:
the text on the page is selectable and Ctrl+C already copies a selection, so a button called *Copy*
beside selectable text is a button that means two things. A search query does not narrow what it
copies — you asked for the conversation, not for the matches.

**Search** highlights every match, counts them, and steps forward and back through them. It clears
in one press, and Escape does the same.

**Newest** appears over the text once you have scrolled away from the live end, and takes you back
to it. This reading grows downwards, so its arrow points down.

**Clear what is shown** — on the right-click menu, and <kbd>Ctrl</kbd>+<kbd>L</kbd> — empties this
page. It is offered here and greyed on the other three readings, which are files on disk that
Directive 47 only reads: a control appearing to empty one would be offering to delete it.

It clears the page and not the record. The model still remembers the conversation, so a follow-up
question is answered as if you had not cleared anything, and the log file on disk is untouched.

**The banknote** at the right of the status line opens the receipt for the most recent response —
the tools it ran, the tokens and characters it spent, and the price. It was the word *Details*
until 0.93.0; hovering still says what it does, and so does a screen reader. It opens a window, so
it is on the desktop and not in a headset, and there is no second place these figures are shown.

It is a note rather than a coin on purpose. These are dollars on a provider account, not your
in-game balance, and a coin in a cockpit overlay is the thing that gets read as credits.

**The ask box** takes typing, and Enter sends. The button beside it does the same thing and exists
so that the first thing you do with Directive 47 is not a guess. The box is on this tab only — from
anywhere else, say what you want instead.

**The microphone badge** is never silent about its own state. *PTT Ready* means push-to-talk is
armed and nothing is being kept. *Listening...* means there is no key to hold and it is waiting to
hear its name. *MIC ON* means your voice is being captured right now — and it is drawn filled as
well as coloured, so it stays readable if the two colours are hard to tell apart.

### Saying where you want to go

This reading answers to **"in ship"**, and to **"conversation"** and **"thread"**, which are what
it was called before. The label on the screen answers *what am I looking at*; the spoken phrase
answers *how do I ask for it*, and they stopped having to be the same string in 0.96.0.

### What decides the answers

Three settings sections, and they fail independently:

| If this is wrong | You see |
|---|---|
| **Listening** — the Whisper model, the microphone, the mode | Nothing appears when you speak, though typing works |
| **Language model** — the provider and key | Only the questions Directive 47 answers on its own |
| **Speech** — the voice | Answers appear on the page but are never read aloud |

That is why a quiet page is usually one of the three switched off rather than everything broken,
and why the three links above go to those rows rather than to three more explanations.
