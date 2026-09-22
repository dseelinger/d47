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
<p class="intro">The conversations you have with those in your ship.</p>
<section>
<h2><span class="num">1</span> Reads like a chat log.</h2>
<svg viewBox="0 0 880 268" role="img" aria-label="Every message on the left under a badge naming who spoke, and a note when the ship's persona changes across the middle">
 <rect x="20" y="24" width="2" height="56" fill="var(--border)"/>
 <rect x="36" y="24" width="56" height="22" fill="var(--border)"/>
 <text x="64" y="40" text-anchor="middle" font-size="14" font-weight="700" fill="var(--text-muted)">CMDR</text>
 <text x="104" y="40" font-size="14" fill="var(--text-muted)">14:02</text>
 <text x="36" y="72" font-size="16" fill="var(--text-muted)">Where am I?</text>
 <rect x="20" y="96" width="2" height="56" fill="var(--border)"/>
 <rect x="36" y="96" width="44" height="22" fill="var(--accent)"/>
 <text x="58" y="112" text-anchor="middle" font-size="14" font-weight="700" fill="var(--background)">D47</text>
 <text x="92" y="112" font-size="14" fill="var(--text-muted)">14:02</text>
 <text x="36" y="144" font-size="16" fill="var(--text-muted)">We're holding at Jameson Memorial.</text>
 <text x="440" y="190" text-anchor="middle" font-size="15" font-weight="700" fill="var(--accent)">[Switched to Cora]</text>
 <text x="440" y="226" text-anchor="middle" font-size="15" fill="var(--text-muted)">The badge says who spoke. A note about the conversation sits across the middle.</text>
</svg>
<p class="body">Other sub-tabs are from files on your disk, and each has its own help file.</p>
</section>
<section>
<h2><span class="num">2</span> Two ways to input your requests.</h2>
<svg viewBox="0 0 880 288" role="img" aria-label="The ask box sends on Enter; the microphone indicator shows one of three states">
 <rect x="20" y="26" width="620" height="52" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="58" font-size="16" fill="var(--text-muted)">Type here, and Enter sends it</text>
 <rect x="656" y="26" width="204" height="52" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="758" y="58" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">SEND</text>
 <text x="440" y="118" text-anchor="middle" font-size="15" fill="var(--text-muted)">Or speak — and the line above the box always shows one of these three states.</text>
 <rect x="20" y="140" width="270" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <circle cx="60" cy="176" r="6" fill="var(--accent)" stroke="var(--accent)" stroke-width="2"/>
 <text x="82" y="182" font-size="15" fill="var(--text-muted)">PTT READY</text>
 <text x="155" y="216" text-anchor="middle" font-size="14" fill="var(--text-muted)">holding nothing; press your key</text>
 <rect x="305" y="140" width="270" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <circle cx="345" cy="176" r="6" fill="var(--accent)" stroke="var(--accent)" stroke-width="2"/>
 <text x="367" y="182" font-size="15" fill="var(--text-muted)">LISTENING</text>
 <text x="440" y="216" text-anchor="middle" font-size="14" fill="var(--text-muted)">waiting for its name, no key held</text>
 <rect x="590" y="140" width="270" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <circle cx="630" cy="176" r="6" fill="var(--accent)" stroke="var(--accent)" stroke-width="2"/>
 <text x="652" y="182" font-size="15" fill="var(--text-muted)">MIC ON</text>
 <text x="725" y="216" text-anchor="middle" font-size="14" fill="var(--text-muted)">your voice is being kept</text>
</svg>
<p class="body">Besides <em>push-to-talk (PTT)</em> you can have D47 listen all the time, or on a wake-word, like "Alexa" or "Hey Google."</p>
</section>
<section>
<h2><span class="num">3</span> Additional controls.</h2>
<svg viewBox="0 0 880 320" role="img" aria-label="Copy, Search, Spend, and Clear what is shown">
 <rect x="20" y="26" width="270" height="126" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="64" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">COPY</text>
 <text x="155" y="96" text-anchor="middle" font-size="14" fill="var(--text-muted)">copies the whole conversation,</text>
 <text x="155" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">not just selected text</text>
 <text x="155" y="140" text-anchor="middle" font-size="14" fill="var(--text-muted)">— that is Ctrl+C</text>
 <rect x="305" y="26" width="270" height="126" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="64" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SEARCH</text>
 <text x="440" y="96" text-anchor="middle" font-size="14" fill="var(--text-muted)">finds every match and</text>
 <text x="440" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">steps you through them</text>
 <text x="440" y="140" text-anchor="middle" font-size="14" fill="var(--text-muted)">forward or back.</text>
 <rect x="590" y="26" width="270" height="126" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="725" y="64" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SPEND</text>
 <text x="725" y="96" text-anchor="middle" font-size="14" fill="var(--text-muted)">tracks how much this and</text>
 <text x="725" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">previous sessions cost,</text>
 <text x="725" y="140" text-anchor="middle" font-size="14" fill="var(--text-muted)">in detail</text>
 <rect x="20" y="180" width="840" height="76" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="212" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">Ctrl+L clears the page for less cluttered viewing</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">It deletes nothing — scroll back up and it's all still there for the session.</text>
 <text x="440" y="288" text-anchor="middle" font-size="15" fill="var(--text-muted)">Scroll up and a "↓ Newest" button appears to bring you back to the latest.</text>
 <text x="440" y="312" text-anchor="middle" font-size="15" fill="var(--text-muted)">Spend is on only the desktop.</text>
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

Each response is a message on the left, under a badge naming who spoke: `D47` in the theme's own
colour, `CMDR` in a dimmer grey, a persona or a role like `Tower` in between. When Directive 47
notes something *about* the conversation rather than saying something in it — the core changing
under you — that sits across the middle in the accent with no badge.

The headset's big panel does the same, and so does the mini panel.

### The controls

**COPY** puts the entire conversation on the clipboard, and says *COPIED* or *COPY FAILED* for two
seconds after. To copy only part of it, select the text and press Ctrl+C. A search query does not
narrow what it copies — you asked for the conversation, not for the matches.

**Search** highlights every match, counts them, and steps forward and back through them. **CLEAR**
beside the box empties it, and Escape does the same.

**Newest** appears over the text once you have scrolled away from the live end, and takes you back
to it. This reading grows downwards, so its arrow points down.

**Clear what is shown** — on the right-click menu, and <kbd>Ctrl</kbd>+<kbd>L</kbd> — empties this
page. It is offered here and greyed on the other three readings, which are files on disk that
Directive 47 only reads: a control appearing to empty one would be offering to delete it.

It clears the page and not the record. The model still remembers the conversation, so a follow-up
question is answered as if you had not cleared anything, and the log file on disk is untouched.

**SPEND** at the right of the status line opens the receipt for the most recent response —
the tools it ran, the tokens and characters it spent, and the price. It opens a window, so
it is on the desktop and not in a headset, and there is no second place these figures are shown.

It is a note rather than a coin on purpose. These are dollars on a provider account, not your
in-game balance, and a coin in a cockpit overlay is the thing that gets read as credits.

**The ask box** takes typing, and Enter sends. **SEND** beside it does the same thing and exists
so that the first thing you do with Directive 47 is not a guess. The box grows to three lines as
you type, then scrolls. It is on this tab only — from anywhere else, say what you want instead.

**The microphone line** above the box is never silent about its own state, and only its dot
changes colour: one colour when ready, a second while listening or while the speech model loads,
and the danger colour when no microphone is open. *PTT READY* means push-to-talk is armed and
nothing is being kept. *LISTENING* means there is no key to hold and it is waiting to hear its
name. *MIC ON* means your voice is being captured right now. *MIC OFF* means no microphone is open,
and its dot is hollow — a filled dot always means a device is open, so the state stays readable if
the colours are hard to tell apart.

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
