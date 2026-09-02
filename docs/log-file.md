---
title: Log File
group: General help
nav_order: 5
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.

  One page per reading (#262). This one is the Log File reading — what the control in front of
  the Commander is and what it shows. The Diagnostics capability page is the subject: log
  levels, where files live, what to ask for. Keep the split; it is the same one #251 was
  about, and this page is on the "what am I looking at" side of it.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Everything Directive 47 wrote this session, as it wrote it — the reading that still answers when nothing else does.</p>
<section>
<h2><span class="num">1</span> One file, every part of the app at once.</h2>
<svg viewBox="0 0 880 292" role="img" aria-label="Eight subsystems all writing into one log file, which this reading shows">
 <rect x="20" y="24" width="196" height="44" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="118" y="52" text-anchor="middle" font-size="14" fill="var(--text-muted)">App</text>
 <rect x="234" y="24" width="196" height="44" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="332" y="52" text-anchor="middle" font-size="14" fill="var(--text-muted)">Journal</text>
 <rect x="448" y="24" width="196" height="44" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="546" y="52" text-anchor="middle" font-size="14" fill="var(--text-muted)">Voice</text>
 <rect x="662" y="24" width="198" height="44" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="761" y="52" text-anchor="middle" font-size="14" fill="var(--text-muted)">Llm</text>
 <rect x="20" y="80" width="196" height="44" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="118" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">Capabilities</text>
 <rect x="234" y="80" width="196" height="44" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="332" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">Settings</text>
 <rect x="448" y="80" width="196" height="44" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="546" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">Vr</text>
 <rect x="662" y="80" width="198" height="44" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="761" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">Input</text>
 <line x1="440" y1="132" x2="440" y2="160" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="440,174 432,158 448,158" fill="var(--accent-muted)"/>
 <rect x="20" y="182" width="840" height="86" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="216" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">LOG FILE</text>
 <text x="440" y="248" text-anchor="middle" font-size="15" fill="var(--text-muted)">today's file on disk, beside the executable in data\logs — never in AppData</text>
</svg>
<p class="body">This is where every diagnostic goes: what started, what the model was asked and what it cost, what the headset did, in-game comms, and every error with its cause.</p>
</section>
<section>
<h2><span class="num">2</span> It answers with nothing else working.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="The log file needs no game, no model, no microphone and no headset">
 <rect x="20" y="30" width="200" height="82" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="120" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text-muted)">NO GAME</text>
 <text x="120" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">running</text>
 <rect x="234" y="30" width="200" height="82" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="334" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text-muted)">NO MODEL</text>
 <text x="334" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">configured</text>
 <rect x="448" y="30" width="200" height="82" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="548" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text-muted)">NO MIC</text>
 <text x="548" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">and no headset</text>
 <rect x="662" y="30" width="198" height="82" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="761" y="66" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">STILL READS</text>
 <text x="761" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">every time</text>
 <rect x="20" y="132" width="840" height="72" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="162" text-anchor="middle" font-size="16" fill="var(--text)">It is a file. Nothing has to be working for it to be there and be true.</text>
 <text x="440" y="188" text-anchor="middle" font-size="15" fill="var(--text-muted)">This is the first reading to open when something is wrong, not the last.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Read when you open it, not tailed.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="The reading is re-read on opening it, shows a working indicator, and refuses to be cleared">
 <rect x="20" y="26" width="410" height="128" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="225" y="62" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">SWITCH AWAY AND BACK</text>
 <text x="225" y="94" text-anchor="middle" font-size="15" fill="var(--text-muted)">to re-read it. A log nobody is</text>
 <text x="225" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">looking at is not worth a file</text>
 <text x="225" y="138" text-anchor="middle" font-size="15" fill="var(--text-muted)">read on every tick</text>
 <rect x="450" y="26" width="410" height="128" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="655" y="62" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">CLEAR IS GREYED</text>
 <text x="655" y="94" text-anchor="middle" font-size="15" fill="var(--text-muted)">this is a file on disk, and a</text>
 <text x="655" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">control that appeared to empty</text>
 <text x="655" y="138" text-anchor="middle" font-size="15" fill="var(--text-muted)">it would be offering to delete it</text>
 <rect x="20" y="176" width="840" height="52" rx="10" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="208" text-anchor="middle" font-size="15" fill="var(--text)">A spinner beside the picker while it reads. This is the only reading that has one, because it is the only one that waits.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card settings" href="capabilities/diagnostics.html"><span class="ct">Diagnostics →</span><span class="cd">How loud each part of the app is, and how to turn one up while you chase something.</span></a>
<a class="card" href="capabilities/privacy.html"><span class="ct">Privacy →</span><span class="cd">What is in a log, and what never reaches one.</span></a>
<a class="card" href="journal-file.html"><span class="ct">Journal File →</span><span class="cd">The other half of an incident: what the game did, as opposed to what Directive 47 did.</span></a>
</div>
</div>
</div></div>

## The details

**Log File** is the second of the Transcript's readings: today's log, as Directive 47 wrote it.
Every subsystem writes into the one file, in order, so this is the page that says what actually
happened rather than what was said about it.

### What is in it

Startup and composition. Which journal is being tailed. Every model request with its token counts
and price. What the voice provider was asked for and which voice answered. What the headset did.
In-game comms, with their sender. And every error, with its cause.

It is the only place several of those are written down, which is deliberate: a diagnostic that also
appeared in the conversation would be a page repeating another page, and the conversation is the
one reading that has to stay readable.

### Reading it

**It is read when you open the page**, not tailed continuously — a log nobody is looking at is not
worth a file read on every tick, and one you *are* looking at is open because something already
went wrong. Switch away and back to re-read it. A spinner beside the picker shows while it reads;
this is the only reading with one, because it is the only one that waits on a disk.

**Search** highlights every match, counts them, and steps forward and back through them.

**Newest** appears once you have scrolled away from the end and takes you back. The log grows
downwards, so its arrow points down.

**Copy All** puts the whole file as shown on the clipboard — the same text you are looking at, and
not something assembled for the occasion.

**Clear what is shown is greyed here.** There is nothing of Directive 47's to clear: this is a file,
and a control that appeared to empty it would be offering to delete a log. The same is true of both
journal readings.

**The share button** offers this reading to a bug report, because a log and the game's own journal
are the two halves of an incident. What leaves your machine is reviewed by you first — see
[Privacy](capabilities/privacy.html).

### It is a file, not a feed

Nothing here is Directive 47 talking to you. The lines are written for grepping, they are in the
order things happened, and they do not stop when the model is unavailable or the microphone is off.
That is the whole value of this reading: it is the one that still answers.

### Turning the detail up

Each part of the app carries its own level, and you can raise one without raising the rest:

> "turn journal logging up to debug"

The change takes effect on the next line written — no restart. `Trace` grows a file quickly, so
turn it back down once you have what you came for. What the parts are and what each covers is on
the [Diagnostics](capabilities/diagnostics.html) page.

### Where the file is

Beside the executable, in `data\logs`, never in `%APPDATA%`. Ask *"what's your status"* and
Directive 47 names the folder along with the version and every other place it writes.
