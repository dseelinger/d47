---
title: In Ship
group: General help
nav_order: 6
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
<p class="body">Each turn is a message under a badge naming who spoke. A note about the conversation, such as a core change, sits across the middle.</p>
</section>
<section>
<h2><span class="num">2</span> Two ways to input your requests.</h2>
<svg viewBox="0 0 880 288" role="img" aria-label="The ask box sends on Enter; the microphone indicator shows its state">
 <rect x="20" y="26" width="620" height="52" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="58" font-size="16" fill="var(--text-muted)">Type here, and Enter sends it</text>
 <rect x="656" y="26" width="204" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="758" y="58" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">SEND</text>
 <text x="440" y="118" text-anchor="middle" font-size="15" fill="var(--text-muted)">Or speak — and the line above the box shows the state of the microphone.</text>
 <rect x="20" y="140" width="270" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <circle cx="60" cy="176" r="6" fill="var(--accent)" stroke="var(--accent)" stroke-width="2"/>
 <text x="82" y="182" font-size="15" fill="var(--text-muted)">PTT READY</text>
 <text x="155" y="216" text-anchor="middle" font-size="14" fill="var(--text-muted)">holding nothing; press your key</text>
 <rect x="305" y="140" width="270" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <circle cx="345" cy="176" r="6" fill="var(--accent)" stroke="var(--accent)" stroke-width="2"/>
 <text x="367" y="182" font-size="15" fill="var(--text-muted)">LISTENING</text>
 <text x="440" y="216" text-anchor="middle" font-size="14" fill="var(--text-muted)">waiting for its name, no key held</text>
 <rect x="590" y="140" width="270" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <circle cx="630" cy="176" r="6" fill="var(--accent)" stroke="var(--accent)" stroke-width="2"/>
 <text x="652" y="182" font-size="15" fill="var(--text-muted)">MIC ON</text>
 <text x="725" y="216" text-anchor="middle" font-size="14" fill="var(--text-muted)">your voice is being kept</text>
</svg>
<p class="body">Type and press Enter, or speak. The line above the box shows whether the microphone is armed, listening, open or loading.</p>
</section>
<section>
<h2><span class="num">3</span> Additional controls.</h2>
<svg viewBox="0 0 880 320" role="img" aria-label="Copy, Search, Spend, and Scroll what is shown">
 <rect x="20" y="26" width="270" height="126" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <rect x="143" y="44" width="16" height="16" fill="none" stroke="var(--text)" stroke-width="3"/>
 <rect x="151" y="52" width="16" height="16" fill="var(--surface)" stroke="var(--text)" stroke-width="3"/>
 <text x="155" y="96" text-anchor="middle" font-size="14" fill="var(--text-muted)">copies the whole conversation,</text>
 <text x="155" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">not just selected text</text>
 <text x="155" y="140" text-anchor="middle" font-size="14" fill="var(--text-muted)">— Ctrl+Shift+C</text>
 <rect x="305" y="26" width="270" height="126" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="64" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SEARCH</text>
 <text x="440" y="96" text-anchor="middle" font-size="14" fill="var(--text-muted)">finds every match and</text>
 <text x="440" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">steps you through them</text>
 <text x="440" y="140" text-anchor="middle" font-size="14" fill="var(--text-muted)">forward or back.</text>
 <rect x="590" y="26" width="270" height="126" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="725" y="64" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SPEND</text>
 <text x="725" y="96" text-anchor="middle" font-size="14" fill="var(--text-muted)">tracks how much this and</text>
 <text x="725" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">previous sessions cost,</text>
 <text x="725" y="140" text-anchor="middle" font-size="14" fill="var(--text-muted)">in detail</text>
 <rect x="20" y="180" width="840" height="76" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="212" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">Ctrl+L scrolls past what is shown</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">It deletes nothing — scroll back up and it's all still there for the session.</text>
 <text x="440" y="288" text-anchor="middle" font-size="15" fill="var(--text-muted)">Scroll up and a "↓ Newest" button appears to bring you back to the latest.</text>
 <text x="440" y="312" text-anchor="middle" font-size="15" fill="var(--text-muted)">Spend opens a window with the figures.</text>
</svg>
<p class="body">Copy, Search and Spend sit beside the page. The right-click menu offers <code>Header="Scroll what is shown above the fold"</code>, which deletes nothing.</p>
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

### How it is drawn

Each response is a message on the left, under a badge naming who spoke: `D47` in the theme's own
colour, `CMDR` in a dimmer grey, a persona or a role like `Tower` in between. When Directive 47
notes something *about* the conversation rather than saying something in it — the core changing
under you — that sits across the middle in the accent with no badge.

A turn you speak through the comms push-to-talk carries a grey `comms` tag after `CMDR`; turns spoken through the core key, and typed turns, have none.

The headset's big panel does the same, and so does the mini panel.

### The controls

**Copy**, the button showing two overlapping squares beside the search box, puts the entire
conversation on the clipboard. The name that appears when you point at it reads *Copied* or
*Copy failed* for two seconds after. **Ctrl+Shift+C** does the same. To copy only part of it,
select the text and press Ctrl+C. A search query does not
narrow what it copies — you asked for the conversation, not for the matches.

**Search** highlights every match, counts them, and steps forward and back through them. The ✕
inside the box empties it, and Escape does the same.

**Newest** appears over the text once you have scrolled away from the live end, and takes you back
to it. This reading grows downwards, so its arrow points down.

**Scroll what is shown above the fold** — on the right-click menu, and <kbd>Ctrl</kbd>+<kbd>L</kbd> —
scrolls the page past everything on it, so new turns start on a clear screen. It deletes nothing:
scroll back up and the session is still there, and it works the same on all four readings. In a
headset it is a tile instead, since the headset has no right-click menu.

**Hide comms traffic** and **Hide invented chatter** are checkboxes that leave those turns out of
the page; they hide, and delete nothing.

**SPEND** at the right of the status line opens the receipt for the most recent response, the
session so far and the totals, with the price of each.

It is a note rather than a coin on purpose. These are dollars on a provider account, not your
in-game balance, and a coin in a cockpit overlay is the thing that gets read as credits.

**The ask box** takes typing, and Enter sends. **SEND** beside it does the same thing and exists
so that the first thing you do with Directive 47 is not a guess. The box grows as you type, up to a fixed height, then scrolls. It is on this tab only — from anywhere else, say what you want instead.

**The microphone line** above the box is never silent about its own state, and only its dot
changes colour: one colour when ready, a second while listening or while the speech model loads,
and the danger colour when no microphone is open. *PTT READY* means push-to-talk is armed and
nothing is being kept. *LISTENING* means there is no key to hold and it is waiting to hear its
name; "D47" always works as that name. *LOADING MODEL* means the speech model is still loading. *MIC ON* means your voice is being captured right now. *MIC OFF* means no microphone is open,
and its dot is hollow — a filled dot always means a device is open, so the state stays readable if
the colours are hard to tell apart.

### Saying where you want to go

This reading answers to **"in ship"**, **"conversation"** and **"thread"**.

### What decides the answers

Three settings sections, and they fail independently:

| If this is wrong | You see |
|---|---|
| **Listening** — the Whisper model, the microphone, the mode | Nothing appears when you speak, though typing works |
| **Language model** — the provider and key | Only the questions Directive 47 answers on its own |
| **Speech** — the voice | Answers appear on the page but are never read aloud |

That is why a quiet page is usually one of the three switched off rather than everything broken,
and why the three links above go to those rows rather than to three more explanations.
