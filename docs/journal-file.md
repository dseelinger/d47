---
title: Journal File
group: General help
nav_order: 6
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.

  One page per reading (#262), and this one serves both journal readings: Raw Journal is the
  same events seen another way rather than a fourth subject, which is why it is not an entry
  in the picker either.

  Only describe controls a Commander can actually reach. ShowJournalNoise and ShowJournalDetail
  are public methods with no production caller — there is no drawn toggle for either, and
  documenting one would be the exact fault #251 was about.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">What the game did, in the order it did it — Elite's own journal, as sentences you can read.</p>
<section>
<h2><span class="num">1</span> Elite writes it. Directive 47 only reads it.</h2>
<svg viewBox="0 0 880 250" role="img" aria-label="Elite writes the journal file; the reading shows it as sentences, newest first">
 <rect x="20" y="26" width="250" height="72" rx="10" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="58" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">ELITE DANGEROUS</text>
 <text x="145" y="82" text-anchor="middle" font-size="14" fill="var(--text-muted)">writes a line per event</text>
 <line x1="278" y1="62" x2="318" y2="62" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="332,62 316,54 316,70" fill="var(--accent-muted)"/>
 <rect x="340" y="26" width="250" height="72" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="465" y="58" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">THE JOURNAL FILE</text>
 <text x="465" y="82" text-anchor="middle" font-size="14" fill="var(--text-muted)">JSON, one event per line</text>
 <line x1="598" y1="62" x2="638" y2="62" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="652,62 636,54 636,70" fill="var(--accent-muted)"/>
 <rect x="660" y="26" width="200" height="72" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="760" y="58" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">THIS READING</text>
 <text x="760" y="82" text-anchor="middle" font-size="14" fill="var(--text-muted)">a sentence per event</text>
 <rect x="20" y="122" width="840" height="94" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="152" font-size="15" fill="var(--text)">14:02:11  Docked at Jameson Memorial, Shinrarta Dezhra</text>
 <text x="44" y="178" font-size="15" fill="var(--text)">14:01:47  Evans Port: Docking request granted.</text>
 <text x="44" y="204" font-size="15" fill="var(--text)">13:58:03  Jumped to Shinrarta Dezhra — 29.44 ly</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">Newest first, which is the file's own order and the opposite of the two readings before it.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> The line, and the fields behind it.</h2>
<svg viewBox="0 0 880 258" role="img" aria-label="A list of sentences on the left, the chosen event's own JSON on the right, and a divider you can drag">
 <rect x="20" y="26" width="370" height="180" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="40" y="58" font-size="14" fill="var(--text-muted)">14:02:11  Docked at Jameson Me…</text>
 <rect x="30" y="70" width="350" height="30" rx="5" fill="var(--accent-muted)" stroke="var(--accent)" stroke-width="2"/>
 <text x="40" y="90" font-size="14" fill="var(--text)">14:01:47  Evans Port: Docking r…</text>
 <text x="40" y="126" font-size="14" fill="var(--text-muted)">13:58:03  Jumped to Shinrarta D…</text>
 <text x="205" y="184" text-anchor="middle" font-size="14" font-weight="700" fill="var(--text-muted)">WHAT HAPPENED</text>
 <rect x="404" y="26" width="12" height="180" rx="6" fill="var(--border)"/>
 <text x="410" y="232" text-anchor="middle" font-size="14" fill="var(--text-muted)">drag</text>
 <rect x="430" y="26" width="430" height="180" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="452" y="58" font-size="14" fill="var(--text-muted)">"event": "ReceiveText",</text>
 <text x="452" y="82" font-size="14" fill="var(--text-muted)">"From_Localised": "Evans Port",</text>
 <text x="452" y="106" font-size="14" fill="var(--text-muted)">"Message_Localised": "Docking …</text>
 <text x="452" y="130" font-size="14" fill="var(--text-muted)">"Channel": "npc"</text>
 <text x="645" y="184" text-anchor="middle" font-size="14" font-weight="700" fill="var(--text-muted)">THE FIELDS, AS ELITE WROTE THEM</text>
 <text x="440" y="248" text-anchor="middle" font-size="15" fill="var(--text-muted)">The sentence is prose and can be wrong. The fields cannot — they are the file.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> The Raw switch is the same events, unread.</h2>
<svg viewBox="0 0 880 216" role="img" aria-label="The Raw switch beside the picker turns the sentences into the file's own JSON">
 <rect x="20" y="24" width="300" height="46" rx="10" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="170" y="53" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">Journal File</text>
 <text x="348" y="53" font-size="16" fill="var(--text-muted)">Raw</text>
 <rect x="392" y="32" width="60" height="30" rx="15" fill="var(--accent)" stroke="var(--accent)" stroke-width="2"/>
 <circle cx="437" cy="47" r="10" fill="var(--surface)"/>
 <rect x="20" y="94" width="410" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="225" y="126" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">OFF — SENTENCES</text>
 <text x="225" y="156" text-anchor="middle" font-size="14" fill="var(--text-muted)">what happened, in words</text>
 <rect x="450" y="94" width="410" height="86" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="655" y="126" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">ON — RAW JOURNAL</text>
 <text x="655" y="156" text-anchor="middle" font-size="14" fill="var(--text-muted)">the file itself, one event per line</text>
 <text x="440" y="206" text-anchor="middle" font-size="15" fill="var(--text-muted)">Not an entry in the picker on purpose: it is the same events seen another way, not a fourth subject.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="log-file.html"><span class="ct">Log File →</span><span class="cd">The other half of an incident: what Directive 47 did, as opposed to what the game did.</span></a>
<a class="card" href="in-ship.html"><span class="ct">In Ship →</span><span class="cd">The conversation, and every control around it.</span></a>
<a class="card" href="capabilities/privacy.html"><span class="ct">Privacy →</span><span class="cd">What a journal holds about you, and what is taken out before any of it is shared.</span></a>
</div>
</div>
</div></div>

## The details

**Journal File** is Elite's own journal, read as sentences rather than as JSON. Docking, jumping,
taking damage, a station talking to you — the things that happened, in the order they happened.

Directive 47 only ever reads this file. Everything on this reading was written by the game.

### Newest first

Both journal readings run newest-first, which is the file's own order and the opposite of In Ship
and Log File. The **Newest** button knows it: on these two its arrow points **up**, because that is
where the newest line actually is. A control that names a direction has to be right about it.

### The two panes

The list of sentences is on the left and the chosen event's own fields are on the right,
pretty-printed exactly as Elite wrote them. **The divider between them can be dragged**, and where
you leave it is remembered.

The two are not redundant. The sentence is prose written by Directive 47 and can be wrong about
what an event meant; the fields cannot be, because they are the file. A bug report made from the
right-hand pane is worth something.

### Searching it

**Search behaves differently here than on the other readings.** They highlight and step; this one
**filters the list** to the matching lines and tells you how many are left — *"12 of 4,318"*. There
are no steppers, because every line on screen is a hit.

It matches **the event's own name as well as the sentence**, which is worth knowing because the
thing you are hunting is frequently the name: `ShieldState` appears nowhere in *"Shields back up"*,
and typing it should not come back empty on the page whose whole job is showing that event.

### The Raw switch

The switch marked **Raw** beside the picker turns the sentences into the file itself — one event
per line, the shape a Commander comparing it against Frontier's own documentation is expecting. It
is a reading in its own right, called **Raw Journal**, and *"raw journal"* reaches it by voice.

It is deliberately **not** an entry in the picker: it is the same events seen another way rather
than a fourth subject, and two entries would read as two. The switch is in the headset as well as
on the desktop.

**Where you leave it is where you find it.** The switch keeps its position across launches, so a
Commander who reads the raw file gets the raw file the next time they open the journal reading, and
one who does not never sees it. It is the *reading* that is remembered, not the page: d47 still
opens on the conversation whichever way the switch is set.

Neither journal reading is formatted as markup. A journal carries other players' text and JSON full
of asterisks and underscores, none of which is emphasis, so both are drawn exactly as written.

### Messages from other people

A station, an NPC or a channel notice draws as the sender and what they said — *"Evans Port:
Docking request granted."* Those are Frontier's own words.

**A message another Commander typed draws as "Message received"**, with their words in the fields
pane beside it. That is on purpose. Another player's text is untrusted input, and a summary line
carrying it could be made to read like one of Directive 47's own event lines. Select the line and
you can read the message; it is one click, and it is plainly somebody's data rather than the
page's prose.

### Clearing, and sharing

**Clear what is shown is greyed on both journal readings.** This is Elite's file, not Directive
47's, and there is nothing here of its to empty.

**The share button** offers this reading to a bug report, alongside the log — the two are the two
halves of an incident, which is what the game did and what Directive 47 did with it. What leaves
your machine is reviewed by you first.
