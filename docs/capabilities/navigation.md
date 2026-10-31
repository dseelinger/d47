---
title: Navigation
group: Acting on the game
nav_order: 137
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
<p class="intro">Three steps to a system name where you can use it.</p>
<section>
<h2><span class="num">1</span> Say where you want to go.</h2>
<p class="say">Say "plot a course to Shinrarta Dezhra".</p>
<p class="expect">D47 says the name is on your clipboard and tells you to paste it into the galaxy map's search box.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">plot a course to Shinrarta Dezhra</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It puts the name on the clipboard, which is the part that always works.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Say it however you say it — the spelling is worked out for you.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Let it drive the map, if you want that.</h2>
<p class="say">Turn on Let D47 press keys in Elite, then say "try to plot courses yourself".</p>
<p class="expect">D47 says "Plotting the course to" and the system, opens the galaxy map, searches, plots and closes the map. With either row off, you paste the name yourself.</p>
<svg viewBox="0 0 880 252" role="img" aria-label="Permissions">
 <rect x="20" y="16" width="840" height="212" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Permissions</text>
 <rect x="44" y="70" width="792" height="42" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Let D47 press keys in Elite</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">on</text>
 <rect x="44" y="126" width="792" height="42" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Try to plot courses in the galaxy map</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text)">on</text>
 <text x="44" y="222" font-size="15" fill="var(--text-muted)">With both on it opens the map, types the name and plots. Without them, you paste.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> Every key it presses needs a binding.</h2>
<p class="say">Bind the galaxy map, UI up, UI select and both sideways camera keys on your keyboard or mouse, or say "stop plotting courses yourself".</p>
<p class="expect">With a key missing, D47 names it and leaves the name on the clipboard to paste.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="A missing key stops the attempt">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">One missing key stops the attempt.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">D47 presses your own keys, so each of the five needs a binding it can press.</text>
</svg>
</section>
</div></div>
</details>

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Putting a system name where you can use it, and — if you let it — driving the map itself.</p>
<section>
<h2><span class="num">1</span> The clipboard is the part that always works.</h2>
<svg viewBox="0 0 880 230" role="img" aria-label="The system name always goes on the clipboard first, and driving the map is a convenience on top of it">
 <rect x="20" y="40" width="390" height="104" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="215" y="80" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">THE CLIPBOARD</text>
 <text x="215" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">always first, always works</text>
 <rect x="460" y="40" width="400" height="104" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="80" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text-muted)">DRIVING THE MAP</text>
 <text x="660" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">a convenience on top that can fail</text>
 <text x="440" y="186" text-anchor="middle" font-size="16" fill="var(--text)">Elite’s galaxy map has a search box, and pasting into it works every time —</text>
 <text x="440" y="214" text-anchor="middle" font-size="16" fill="var(--text)">in every language, whatever your controls look like.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Seven steps, two of them added after failures.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="The seven steps of driving the galaxy map, with the two that exist because of earlier failures picked out">
 <rect x="25" y="40" width="110" height="76" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="80" y="72" text-anchor="middle" font-size="14" fill="var(--text)">open the</text>
 <text x="80" y="96" text-anchor="middle" font-size="14" fill="var(--text)">map</text>
 <rect x="145" y="40" width="110" height="76" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="200" y="72" text-anchor="middle" font-size="14" fill="var(--text)">up, then</text>
 <text x="200" y="96" text-anchor="middle" font-size="14" fill="var(--text)">select</text>
 <rect x="265" y="40" width="110" height="76" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="320" y="72" text-anchor="middle" font-size="14" fill="var(--text)">paste, then</text>
 <text x="320" y="96" text-anchor="middle" font-size="14" font-weight="700" fill="var(--accent)">return</text>
 <rect x="385" y="40" width="110" height="76" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="72" text-anchor="middle" font-size="14" fill="var(--text)">wait 4s for</text>
 <text x="440" y="96" text-anchor="middle" font-size="14" fill="var(--text)">the camera</text>
 <rect x="505" y="40" width="110" height="76" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="560" y="72" text-anchor="middle" font-size="14" fill="var(--text)">brush the</text>
 <text x="560" y="96" text-anchor="middle" font-size="14" font-weight="700" fill="var(--accent)">camera</text>
 <rect x="625" y="40" width="110" height="76" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="680" y="72" text-anchor="middle" font-size="14" fill="var(--text)">hold select</text>
 <text x="680" y="96" text-anchor="middle" font-size="14" fill="var(--text)">1.2 seconds</text>
 <rect x="745" y="40" width="110" height="76" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="800" y="72" text-anchor="middle" font-size="14" fill="var(--text)">close the</text>
 <text x="800" y="96" text-anchor="middle" font-size="14" fill="var(--text)">map</text>
 <text x="440" y="152" text-anchor="middle" font-size="15" font-weight="700" fill="var(--accent)">The two picked out exist because of what went wrong without them.</text>
 <text x="440" y="190" text-anchor="middle" font-size="16" fill="var(--text)">Return rather than the UI down key — the search box keeps focus, and a UI</text>
 <text x="440" y="216" text-anchor="middle" font-size="16" fill="var(--text)">key sent to it is a character. An earlier version typed an S into the box.</text>
</svg>
<p class="body">The camera brush is one tap right and one left, so the camera ends where it started. It makes the selector respond to the held select.</p>
</section>
<section>
<h2><span class="num">3</span> Three answers, and they mean different things.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="Course plotted, assume it did not work, and cannot tell are three distinct outcomes">
 <rect x="20" y="36" width="270" height="124" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="155" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">COURSE PLOTTED</text>
 <text x="155" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">a route to that system</text>
 <text x="155" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">really is in the file</text>
 <rect x="305" y="36" width="270" height="124" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="76" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">ASSUME IT DID NOT</text>
 <text x="440" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">the file was readable</text>
 <text x="440" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">and no such route appeared</text>
 <rect x="590" y="36" width="270" height="124" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="725" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">CANNOT TELL</text>
 <text x="725" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">the file never became</text>
 <text x="725" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">readable at all</text>
 <text x="440" y="198" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">D47 says “done” only when the file shows the route.</text>
 <text x="440" y="230" text-anchor="middle" font-size="15" fill="var(--text-muted)">Otherwise you would fly towards a course you do not have.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> It takes all five keys, or none of them.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="Driving the map needs five keys, and the first one it cannot press stops the attempt before anything is sent">
 <rect x="41" y="36" width="150" height="64" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="116" y="74" text-anchor="middle" font-size="15" fill="var(--text)">galaxy map</text>
 <rect x="203" y="36" width="150" height="64" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="278" y="74" text-anchor="middle" font-size="15" fill="var(--text)">UI up</text>
 <rect x="365" y="36" width="150" height="64" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="74" text-anchor="middle" font-size="15" fill="var(--text)">UI select</text>
 <rect x="527" y="36" width="150" height="64" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="602" y="74" text-anchor="middle" font-size="15" fill="var(--text)">camera right</text>
 <rect x="689" y="36" width="150" height="64" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="764" y="74" text-anchor="middle" font-size="15" fill="var(--text)">camera left</text>
 <text x="440" y="136" text-anchor="middle" font-size="16" fill="var(--text)">All five, on the keyboard or the mouse. A key on a stick is one it cannot press.</text>
 <text x="440" y="174" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So the first one it cannot press stops the whole attempt.</text>
 <text x="440" y="208" text-anchor="middle" font-size="15" fill="var(--text-muted)">Opening the map with no “select” to follow leaves it open over the cockpit — worse than nothing.</text>
</svg>
<p class="body">Example: with no binding for select, you hear "You have no binding for select, so there is no key for me to press."</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="routes.html"><span class="ct">Routes →</span><span class="cd">Where the destination comes from before there is anything to plot.</span></a>
<a class="card" href="panels.html"><span class="ct">Panels →</span><span class="cd">Opening the map, which is all that page does with it.</span></a>
<a class="card" href="flight-controls.html"><span class="ct">Flight and navigation →</span><span class="cd">The switch that has to be on before a single key goes out.</span></a>
</div>
</div>
</div></div>

## The details

Puts a system name where you can use it, and tries to plot a course to it.

**This page plots a course. It drives the galaxy map.** Ask it to set a course and it opens the
map, types the name into the search box and checks that a route appeared. Every ordinary way of
asking lands here — "set course for my carrier", "plot a course to my carrier", "take me to my
carrier", and plain "route to my carrier" as well, because that is what a Commander means by it.

If what you want instead is to *hear* a neutron route — the jumps, the waypoints, where to
supercharge — say **neutron**: "plot neutron jumps to my carrier" goes to
[route planning](routes.md), which works that route out and reads it to you without touching the
map.

### Ask for it

> "plot a course to Shinrarta Dezhra"
> "set course for Colonia"
> "copy that system name"

### Spell a system

When a name keeps being heard wrong, say "spell a system", "spell the system" or "spell a destination". A keyboard opens titled "System to plot", empty, on the headset panel while the overlay is showing and on the window otherwise. Say the letters ("sierra oscar lima"), then "done". The name is plotted exactly as spelled, the same way "plot a course" plots it. Cancelling plots nothing and puts nothing on the clipboard.

When a plot ends with no route, the answer ends "Spell it?" and a yes/no question opens. "Yes" opens the same keyboard with the name D47 heard already in it, so "delete" and letters correct it. "No", or anything else, closes the question.

```text
I tried to plot Colonai and no route appeared, so assume it did not work. ... Spell it?
```

### What you hear

Asking for a course is an action, and the standing rule for actions is *act first, talk least*.
When the destination has to be found first — "set course for the closest Imperial Shielding" —
you hear one sentence as soon as it is found and the map starts moving, and a short answer when
it is done:

```text
Closest Imperial Shielding is likely Scorpii Sector BB-O a6-2. Plotting.
Course plotted.
```

The first line is spoken *while* the map is being driven, not before. The figures, the material
ledger and the trader arithmetic wait until you ask for them.

### The clipboard is the part that always works

Asking for a course always puts the name on your clipboard first, before anything else is
attempted. Elite's galaxy map has a search box, and pasting into it works every time, in every
language, whatever your controls look like.

```text
Colonia is on your clipboard. Paste it into the galaxy map's search box to plot it.
Automatic plotting is switched off.
```

That is the whole feature, and it is deliberately the primary one. Everything below is a
convenience on top of it that can fail.

### Letting it drive the map

Turn on **Try to plot courses in the galaxy map** (Settings, Acting on the game, What D47 may do,
Permissions; an advanced row, off by default) and say "try to plot courses yourself" or turn the
row on. **Let D47 press keys in Elite** has to be on too. D47 then drives the map with your own
keys, in this order:

0. Say what it is about to do, naming the system: *"Plotting the course to Shinrarta Dezhra."*
   A plot takes several seconds, and the name lets you catch a misheard system early.
1. Open the galaxy map, and wait until the game reports it showing.
2. **Up**, then **select** — that is the search box.
3. Paste the name, and wait 1.5 seconds for the search to fill its list.
4. **Return**, then **down**, then **select** — the first row of the list, which is the system itself.
5. Wait 4 seconds for the camera to get there.
6. A brush of **sideways camera** — right, then left, 30 milliseconds each.
7. **Select**, held for 1.2 seconds — a tap opens the system, a hold plots to it.
8. The galaxy map key again, which closes it.

Return is sent instead of the UI down key because the search box keeps focus after the paste, and
an interface key sent to it is typed as a character. The camera brush takes focus out of the box
and arms the selector; without it select does nothing on the star.

If the first attempt shows no route, D47 drives the map once more before it says the plot failed.
Asked for a course that is already set, it answers "The course to Shinrarta Dezhra is already
plotted, so I have left it alone." and presses nothing.

This is still best-effort. Directive 47 cannot see the map, so it cannot check that the search
matched the system you meant rather than another that starts the same way, and it cannot see
where the camera landed. What it can see, it checks.

Elite writes your whole route to a file the moment one is plotted, which means "did that work"
has a real answer:

```text
I tried to plot Colonia and no route appeared, so assume it did not work. Colonia is on your
clipboard. I cannot tell why.
```

Three answers, and they mean different things. **Course plotted** means a route to that system
really is in the file. **Assume it did not work** means the file was readable and no such route
appeared. **Cannot tell** means the file never became readable at all — which usually means Elite
is not running.

None of the three offers a reason, because nothing on this path has one.

Two more things it watches for. If the map key is pressed and the game never reports the map
open, nothing else is sent — the remaining keys are a W and a space bar, and typed into the
cockpit instead of the map they would fly the ship. And if the closing press leaves the map
showing, it says so: *the galaxy map is still open*.

**It needs five keys**: the galaxy map, UI up, UI select, and both sideways camera translates,
right and left. All on the keyboard or mouse — a key on a stick is one Directive 47 cannot press.
The first key it cannot press stops the attempt before anything is sent, and you hear which one:

```text
Colonia is on your clipboard. I could not drive the galaxy map myself — You have no binding for
select, so there is no key for me to press. Paste it into the map's search box to plot it.
```

### Pasting is an ordinary paste

Directive 47 sends Ctrl+V, not one of your bindings, because Elite does not bind paste — the
search box is a normal text field and the clipboard is the operating system's.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `copy_to_clipboard`

Put text on the Commander's clipboard so they can paste it into the game or a browser. Use for
system names, routes and values they asked for.

```json
{"type":"object","properties":{"text":{"type":"string","description":"What to put on the clipboard."}},"required":["text"],"additionalProperties":false}
```

#### `plot_course`

Put a system name on the clipboard and, if the Commander has allowed it, try to plot a course to
it in the galaxy map. The plotting attempt is best-effort and is verified afterwards; the
clipboard always works.

```json
{"type":"object","properties":{"system":{"type":"string","description":"The star system to plot to, spelled as the game spells it."}},"required":["system"],"additionalProperties":false}
```

The confirmation lives in the app rather than in Core, because it waits and no Core component
reads the clock. `RoutePlotWatch` is opened **before the first key is sent** and remembers when
`NavRoute.json` was last written; afterwards it polls for up to six seconds and says yes only to
a route that ends at the system *and* was written later than that. A route that was already in
the file is not evidence the keys did anything — that is how "course plotted" was once said for a
route the Commander had plotted by hand a minute earlier. It still distinguishes "no route
appeared" from "the file was never readable", since the two answers send the Commander to
different places. The same split holds for the map itself: the app waits up to three seconds on
`Status.json`'s `GuiFocus` for the map to open before the interface keys go, and again for it to
close afterwards. Both checks log what they saw — the focus value, the file's before-and-after
write times and where the route ends — so a report of "nothing happened" starts from evidence.

#### `spell_system`

Open a keyboard on the panel for the Commander to spell a system name, then plot a course to it.
Never callable by the model; the three phrases above reach it.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Return and Ctrl+V are sent as plain virtual keys rather than resolved from the bindings file,
because Elite binds neither. The waits in the sequence are fixed in `NavigationCapability.cs`.

</details>
