---
title: Help improve D47
group: General help
nav_order: 10
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.

  This page is what the ? on the Help improve D47 window opens (#252), and that mark opens the
  site rather than the in-app band — a dialog has no panel to take to a help level. So this
  band is read in a browser, and everything below it is read in the same tab. Keep the split
  anyway: the band is the concise answer and The details is the nitty-gritty.

  **The trim rule this page exists to serve.** The dialog keeps every statement of what leaves
  and where it goes, because a Commander who never presses the mark must still have read it.
  This page takes the mechanism. Do not move a disclosure here to shorten the dialog.

  **Three levels since #269, and this page is the deepest of them.** The dialog's intro is the
  disclosures alone; the ⓘ beside the ? holds the reasoning — why real journals, what the scrub
  keeps, why a history is a report — in about a paragraph each; this page is all of it at length
  with the diagrams, and the ⓘ's own button is what opens it. So the glyph and the sections below
  say the same three things at two depths on purpose. Edit them together, or the short form starts
  promising something the long form no longer says.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">What the two things you can share actually are, and what happens to them.</p>
<section>
<h2><span class="num">1</span> Two shapes, and the toggle picks which.</h2>
<svg viewBox="0 0 880 268" role="img" aria-label="One incident excerpt, or a scrubbed history of many journals, chosen by the Include journal history toggle">
 <rect x="20" y="24" width="410" height="150" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="225" y="62" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">ONE INCIDENT</text>
 <text x="225" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">the minutes around a thing</text>
 <text x="225" y="120" text-anchor="middle" font-size="15" fill="var(--text-muted)">that went wrong: your journal</text>
 <text x="225" y="144" text-anchor="middle" font-size="15" fill="var(--text-muted)">and d47's log, side by side</text>
 <rect x="450" y="24" width="410" height="150" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="655" y="62" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">A HISTORY</text>
 <text x="655" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">many journals at once, as far</text>
 <text x="655" y="120" text-anchor="middle" font-size="15" fill="var(--text-muted)">back as the scale says —</text>
 <text x="655" y="144" text-anchor="middle" font-size="15" fill="var(--text-muted)">for finding what nobody reported</text>
 <rect x="20" y="196" width="840" height="52" rx="10" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="228" text-anchor="middle" font-size="16" fill="var(--text)">Include journal history is the switch between them. Everything else on the window follows from it.</text>
</svg>
<p class="body">An incident is for a defect you can point at. A history is for the ones nobody has noticed yet — a callout that fires in a situation no one thought to test.</p>
</section>
<section>
<h2><span class="num">2</span> You read it before it goes, and it is not the raw file.</h2>
<svg viewBox="0 0 880 236" role="img" aria-label="Your journals are read, scrubbed, and turned into something you review before anything is sent">
 <rect x="20" y="30" width="180" height="76" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="110" y="62" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">READ</text>
 <text x="110" y="88" text-anchor="middle" font-size="14" fill="var(--text-muted)">from your disk</text>
 <line x1="208" y1="68" x2="242" y2="68" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="256,68 240,60 240,76" fill="var(--accent-muted)"/>
 <rect x="264" y="30" width="180" height="76" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="354" y="62" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">SCRUBBED</text>
 <text x="354" y="88" text-anchor="middle" font-size="14" fill="var(--text-muted)">on this machine</text>
 <line x1="452" y1="68" x2="486" y2="68" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="500,68 484,60 484,76" fill="var(--accent-muted)"/>
 <rect x="508" y="30" width="180" height="76" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="598" y="62" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">SHOWN</text>
 <text x="598" y="88" text-anchor="middle" font-size="14" fill="var(--text-muted)">to you, in full</text>
 <line x1="696" y1="68" x2="730" y2="68" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="744,68 728,60 728,76" fill="var(--accent-muted)"/>
 <rect x="752" y="30" width="108" height="76" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="806" y="62" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">YOU</text>
 <text x="806" y="88" text-anchor="middle" font-size="14" fill="var(--text-muted)">press, or don't</text>
 <rect x="20" y="130" width="840" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="162" text-anchor="middle" font-size="16" fill="var(--text)">The scrub works from a list of fields it keeps, not a list of things to remove.</text>
 <text x="440" y="192" text-anchor="middle" font-size="15" fill="var(--text-muted)">A field nobody has thought about is dropped by default — which is the only way it stays right as Elite adds events.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> A history is offered as a report about itself.</h2>
<svg viewBox="0 0 880 216" role="img" aria-label="A history is summarised into a report naming each kind of event with one real scrubbed line of each">
 <rect x="20" y="26" width="250" height="82" rx="10" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="58" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">HUNDREDS OF MB</text>
 <text x="145" y="84" text-anchor="middle" font-size="14" fill="var(--text-muted)">nobody could read this</text>
 <line x1="278" y1="66" x2="318" y2="66" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="332,66 316,58 316,74" fill="var(--accent-muted)"/>
 <rect x="340" y="26" width="520" height="82" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="600" y="58" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">A REPORT YOU CAN ACTUALLY READ</text>
 <text x="600" y="84" text-anchor="middle" font-size="14" fill="var(--text-muted)">every kind of event included, and one real scrubbed line of each</text>
 <rect x="20" y="132" width="840" height="62" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="160" text-anchor="middle" font-size="16" fill="var(--text)">So consent is possible. Reviewing the thing itself would mean agreeing to something nobody read.</text>
 <text x="440" y="184" text-anchor="middle" font-size="15" fill="var(--text-muted)">The report is what you see; the scrubbed history is what would be sent.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="donation-privacy.html"><span class="ct">Donation privacy notice →</span><span class="cd">Who ends up holding it, why they may, how long it lasts, and how to have it deleted.</span></a>
<a class="card" href="capabilities/privacy.html"><span class="ct">Privacy and egress →</span><span class="cd">Where Forget lives, and everything else that has ever left this machine.</span></a>
<a class="card" href="community.html"><span class="ct">Reporting something →</span><span class="cd">What makes a report worth acting on, and where to put it.</span></a>
</div>
</div>
</div></div>

## The details

This page explains **what the window is offering**. What happens to a donation afterwards — who
holds it, on what legal basis, for how long, and how to have it erased — is the
[donation privacy notice](donation-privacy.html), and it is a different question deliberately kept
on a different page.

Nothing here happens unless you press. There is no setting that turns any of it on, and there is no
standing consent to withdraw, because none is ever given.

### Why real journals

A defect in Directive 47 is nearly always a defect about a *situation*: a callout that fires when it
should not, a ship state nobody anticipated, an event Frontier added that the parser had never seen.
Reproducing one from a description means guessing at the situation. Reproducing it from the journal
that produced it means replaying it.

That is what the replay harness is for. A donated incident becomes a case it can run — the same
events in the same order — so a fix can be proved against what actually happened rather than against
somebody's account of it.

### The two shapes

**An incident excerpt** is the minutes around one thing that went wrong: your Elite journal and
Directive 47's own log for that window, side by side. That pairing is the point — the journal says
what the game did and the log says what Directive 47 made of it, and a defect is nearly always in
the gap between them.

**A journal history** is many journals at once, as far back as the scale you choose. It is for the
defects nobody has reported: with enough real journals, a callout that misfires in some rare
situation shows up as a pattern rather than as one Commander's bad evening.

The **Include journal history** toggle is the switch between them, and everything else on the window
follows from it — what is previewed, what the buttons say, and how big the result is.

### What the scrub does

Your Commander name and the IDs that identify you are replaced with stable stand-ins, so a sequence
of events still reads as one Commander's without naming you. Other people's words are dropped
entirely: another Commander's chat is theirs, and you cannot consent on their behalf.

**It works from a list of fields it keeps, rather than a list of fields to remove.** That is the
part worth understanding, because it is what makes it hold: a field nobody has thought about is
dropped by default. Frontier adds events, and a scrub written the other way round would leak every
one of them until somebody noticed.

The scrub runs on your machine, before anything is displayed. What you review is the scrubbed
result, not the original.

### Why a history is shown as a report

A history runs to hundreds of megabytes. Nobody reads that, so reviewing it directly would mean
agreeing to something you had not read — which is not consent.

So Directive 47 builds a **report about** the history instead: it names every kind of event
included, and shows one real scrubbed line of each. You are reading a faithful sample of the actual
content rather than a promise about it, at a size a person can get through.

An incident excerpt is small enough to show in full, so it is shown in full.

### Reading your journals

**Read my journals** walks the journal folder and scrubs as it goes. It writes nothing and sends
nothing; it can be stopped part way, and stopping leaves nothing behind. On a long history this
takes a while, and the window says how far it has got.

### If there is no send address

A build with no destination configured can still do all of this — read, scrub and review — and then
save the result to a file instead of sending it. Where that file goes afterwards is entirely yours.
Anything posted publicly can be archived beyond anyone's reach, which is worth knowing before you
paste one into a forum.
