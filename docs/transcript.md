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
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">The page you land on: what every control does, and which three settings decide the answers.</p>
<section>
<h2><span class="num">1</span> Three readings of the same conversation.</h2>
<svg viewBox="0 0 880 268" role="img" aria-label="One exchange read three ways: as conversation, as the working behind it, and as the raw log file">
 <rect x="20" y="26" width="840" height="46" rx="10" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <rect x="28" y="33" width="266" height="32" rx="7" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="161" y="55" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">Thread</text>
 <text x="450" y="55" text-anchor="middle" font-size="16" fill="var(--text-muted)">Details</text>
 <text x="730" y="55" text-anchor="middle" font-size="16" fill="var(--text-muted)">D47 Log</text>
 <rect x="20" y="96" width="266" height="120" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="153" y="132" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">WHAT WAS SAID</text>
 <text x="153" y="162" text-anchor="middle" font-size="14" fill="var(--text-muted)">your words and its</text>
 <text x="153" y="184" text-anchor="middle" font-size="14" fill="var(--text-muted)">answers, in order</text>
 <rect x="307" y="96" width="266" height="120" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="132" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">THE WORKING</text>
 <text x="440" y="162" text-anchor="middle" font-size="14" fill="var(--text-muted)">what it looked up,</text>
 <text x="440" y="184" text-anchor="middle" font-size="14" fill="var(--text-muted)">and what that cost</text>
 <rect x="594" y="96" width="266" height="120" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="727" y="132" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">THE RAW RECORD</text>
 <text x="727" y="162" text-anchor="middle" font-size="14" fill="var(--text-muted)">the log on disk,</text>
 <text x="727" y="184" text-anchor="middle" font-size="14" fill="var(--text-muted)">a screenful at a time</text>
 <text x="440" y="248" text-anchor="middle" font-size="15" fill="var(--text-muted)">Each remembers where you were. Leaving Transcript and coming back returns to the one you were reading.</text>
</svg>
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
<svg viewBox="0 0 880 250" role="img" aria-label="Copy All takes the whole conversation, Search steps through matches, and Details opens the receipt for one turn">
 <rect x="20" y="30" width="270" height="130" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">COPY ALL</text>
 <text x="155" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">the whole conversation,</text>
 <text x="155" y="124" text-anchor="middle" font-size="14" fill="var(--text-muted)">not the selection</text>
 <text x="155" y="146" text-anchor="middle" font-size="14" fill="var(--text-muted)">— that is Ctrl+C</text>
 <rect x="305" y="30" width="270" height="130" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SEARCH</text>
 <text x="440" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">counts every match and</text>
 <text x="440" y="124" text-anchor="middle" font-size="14" fill="var(--text-muted)">steps you through them</text>
 <text x="440" y="146" text-anchor="middle" font-size="14" fill="var(--text-muted)">both ways</text>
 <rect x="590" y="30" width="270" height="130" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="725" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">DETAILS</text>
 <text x="725" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">the receipt for the turn</text>
 <text x="725" y="124" text-anchor="middle" font-size="14" fill="var(--text-muted)">you just heard — what it</text>
 <text x="725" y="146" text-anchor="middle" font-size="14" fill="var(--text-muted)">used, and what it cost</text>
 <text x="440" y="200" text-anchor="middle" font-size="15" fill="var(--text-muted)">Copy All and Search are on every reading. Details opens a window, so it is on the desktop only.</text>
 <text x="440" y="230" text-anchor="middle" font-size="15" fill="var(--text-muted)">In a headset the same figures are on the Technical reading, one tab across.</text>
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

The switcher at the top of the page offers the same session read three ways.

**Thread** is what was said — your questions and the answers, in order, with a marker where the
core changed. This is the reading you want almost always.

**Details** is the working behind it: which capability answered, which tools it called, what it
looked up, and what the turn cost. When an answer surprises you, this is the page that says why.

**D47 Log** is Directive 47's own record on disk, read a screenful at a time. Nothing here is its
to clear — it is a file — which is why the clear control is refused on this reading and offered on
the other two.

Each is a word you can **say** as well as press, which is why they are short and why none of them
is a parenthetical: a crumb is matched by the keyword router, and nobody says "log file, brackets,
raw".

All three are **roots** rather than levels. Pressing Transcript while several levels deep in
another tab returns you to whichever of the three you were last reading, not to a fixed one.

### The controls around the conversation

**Copy All** puts the entire conversation on the clipboard. It is deliberately not called *Copy*:
the text on the page is selectable, and Ctrl+C already copies a selection, so a button called
*Copy* beside selectable text is a button that means two things.

**Search** filters and steps. It counts the matches, moves forward and back through them, and
clears in one press.

**Details** opens the receipt for the most recent turn — the tools it ran, the tokens and
characters it spent, and the price. It opens a window, so it exists on the desktop and not in a
headset; the same figures are on the Technical reading, which both surfaces have.

**The ask box** takes typing, and Enter sends. The button beside it does the same thing and exists
so that the first thing you do with Directive 47 is not a guess.

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
