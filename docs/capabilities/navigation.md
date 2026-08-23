---
title: Navigation
group: Acting on the game
nav_order: 134
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Putting a system name where you can use it, and — if you let it — driving the map itself.</p>
<section>
<h2><span class="num">1</span> The clipboard is the part that always works.</h2>
<svg viewBox="0 0 880 230" role="img" aria-label="The system name always goes on the clipboard first, and driving the map is a convenience on top of it">
 <rect x="20" y="40" width="390" height="104" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="215" y="80" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">THE CLIPBOARD</text>
 <text x="215" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">always first, always works</text>
 <rect x="460" y="40" width="400" height="104" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="80" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text-muted)">DRIVING THE MAP</text>
 <text x="660" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">a convenience on top that can fail</text>
 <text x="440" y="186" text-anchor="middle" font-size="16" fill="var(--text)">Elite’s galaxy map has a search box, and pasting into it works every time —</text>
 <text x="440" y="214" text-anchor="middle" font-size="16" fill="var(--text)">in every language, whatever your controls look like.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Seven steps, two of them scar tissue.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="The seven steps of driving the galaxy map, with the two that exist because of earlier failures picked out">
 <rect x="25" y="40" width="110" height="76" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="80" y="72" text-anchor="middle" font-size="14" fill="var(--text)">open the</text>
 <text x="80" y="96" text-anchor="middle" font-size="14" fill="var(--text)">map</text>
 <rect x="145" y="40" width="110" height="76" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="200" y="72" text-anchor="middle" font-size="14" fill="var(--text)">up, then</text>
 <text x="200" y="96" text-anchor="middle" font-size="14" fill="var(--text)">select</text>
 <rect x="265" y="40" width="110" height="76" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="320" y="72" text-anchor="middle" font-size="14" fill="var(--text)">paste, then</text>
 <text x="320" y="96" text-anchor="middle" font-size="14" font-weight="700" fill="var(--accent)">return</text>
 <rect x="385" y="40" width="110" height="76" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="72" text-anchor="middle" font-size="14" fill="var(--text)">wait 3s for</text>
 <text x="440" y="96" text-anchor="middle" font-size="14" fill="var(--text)">the camera</text>
 <rect x="505" y="40" width="110" height="76" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="560" y="72" text-anchor="middle" font-size="14" fill="var(--text)">brush the</text>
 <text x="560" y="96" text-anchor="middle" font-size="14" font-weight="700" fill="var(--accent)">camera</text>
 <rect x="625" y="40" width="110" height="76" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="680" y="72" text-anchor="middle" font-size="14" fill="var(--text)">hold select</text>
 <text x="680" y="96" text-anchor="middle" font-size="14" fill="var(--text)">1.2 seconds</text>
 <rect x="745" y="40" width="110" height="76" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="800" y="72" text-anchor="middle" font-size="14" fill="var(--text)">close the</text>
 <text x="800" y="96" text-anchor="middle" font-size="14" fill="var(--text)">map</text>
 <text x="440" y="152" text-anchor="middle" font-size="15" font-weight="700" fill="var(--accent)">The two picked out exist because of what went wrong without them.</text>
 <text x="440" y="190" text-anchor="middle" font-size="16" fill="var(--text)">Return rather than the UI down key — the search box keeps focus, and a UI</text>
 <text x="440" y="216" text-anchor="middle" font-size="16" fill="var(--text)">key sent to it is a character. The first cut of this typed an S into the box.</text>
</svg>
<p class="body">The camera brush is right-then-left, for the same 30 milliseconds each. What arms the selector is movement; what knocks it off the star is net displacement. A key is full deflection for as long as it is down, so a single tap is twitchy — two equal taps in opposite directions are a stick excursion and return, done with keys.</p>
</section>
<section>
<h2><span class="num">3</span> Three answers, and they mean different things.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="Course plotted, assume it did not work, and cannot tell are three distinct outcomes">
 <rect x="20" y="36" width="270" height="124" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="155" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">COURSE PLOTTED</text>
 <text x="155" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">a route to that system</text>
 <text x="155" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">really is in the file</text>
 <rect x="305" y="36" width="270" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="76" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">ASSUME IT DID NOT</text>
 <text x="440" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">the file was readable</text>
 <text x="440" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">and no such route appeared</text>
 <rect x="590" y="36" width="270" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="725" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">CANNOT TELL</text>
 <text x="725" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">the file never became</text>
 <text x="725" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">readable at all</text>
 <text x="440" y="198" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">None of them is “done” said hopefully.</text>
 <text x="440" y="230" text-anchor="middle" font-size="15" fill="var(--text-muted)">A companion that leaves you flying towards a course you do not have is worse than one that never tries.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> It takes all five keys, or none of them.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="Driving the map needs five keys, and the first one it cannot press stops the attempt before anything is sent">
 <rect x="41" y="36" width="150" height="64" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="116" y="74" text-anchor="middle" font-size="15" fill="var(--text)">galaxy map</text>
 <rect x="203" y="36" width="150" height="64" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="278" y="74" text-anchor="middle" font-size="15" fill="var(--text)">UI up</text>
 <rect x="365" y="36" width="150" height="64" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="74" text-anchor="middle" font-size="15" fill="var(--text)">UI select</text>
 <rect x="527" y="36" width="150" height="64" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="602" y="74" text-anchor="middle" font-size="15" fill="var(--text)">camera right</text>
 <rect x="689" y="36" width="150" height="64" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="764" y="74" text-anchor="middle" font-size="15" fill="var(--text)">camera left</text>
 <text x="440" y="136" text-anchor="middle" font-size="16" fill="var(--text)">All five, on the keyboard or the mouse. A key on a stick is one it cannot press.</text>
 <text x="440" y="174" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So the first one it cannot press stops the whole attempt.</text>
 <text x="440" y="208" text-anchor="middle" font-size="15" fill="var(--text-muted)">Opening the map with no “select” to follow leaves it open over the cockpit — worse than nothing.</text>
</svg>
<p class="body">Elite's own default keyboard preset ships the galaxy map <strong>unbound</strong>, so out of the box you get the clipboard and an explanation naming the key that was missing.</p>
</section>
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

### Ask for it

> "plot a course to Shinrarta Dezhra"
> "copy that system name"
> "put the route on my clipboard"

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

The **Course** page of the window's **Routing** tab is this as a control rather than a sentence,
in the same order: the name goes on the clipboard, then the map is driven if you asked for that,
then d47 says whether it took. Every system name drawn anywhere on that tab — a waypoint on the
route you are flying, a stop on a trade plan — copies when you press it.

### Letting it drive the map

Turn on **Try to plot courses in the galaxy map** and Directive 47 will also drive the map for
you, with your own keys, in this order:

1. Open the galaxy map, and wait until the game reports it showing.
2. **Up**, then **select** — that is the search box.
3. Paste the name, then **return** — the map flies to the first match.
4. Three seconds for the camera to get there.
5. A brush of **sideways camera** — right, then left, for the same 30 milliseconds each.
6. **Select**, held for 1.2 seconds — a tap opens the system, a hold plots to it.
7. The galaxy map key again, which closes it.

Two of those steps exist because of what went wrong without them. Return rather than the UI down
key, because the search box keeps focus after the paste and an interface key sent to it is a
character — the first cut of this macro typed an S into the box. And the camera brush, because
after the search the selector over the star is a plain circle and select does nothing, and the
search box still has the keyboard; the smallest camera movement takes focus out of the box and
draws the arrows around the selector, and with the arrows showing and the selector still on the
star, the held select plots. It is right-then-left because what arms the selector is movement and
what knocks it off the star is net displacement — a key is full deflection for as long as it is
down, so a single tap is twitchy, while two equal taps in opposite directions are a stick
excursion and return done with keys.

This is still best-effort. Directive 47 cannot see the map, so it cannot check that the search
matched the system you meant rather than another that starts the same way, and it cannot see
where the camera landed. What it can see, it checks.

Elite writes your whole route to a file the moment one is plotted, which means "did that work"
has a real answer:

```text
I tried to plot Colonia and no route appeared, so assume it did not work. Colonia is on your
clipboard. The search may have matched a different system.
```

Three answers, and they mean different things. **Course plotted** means a route to that system
really is in the file. **Assume it did not work** means the file was readable and no such route
appeared. **Cannot tell** means the file never became readable at all — which usually means Elite
is not running.

None of them is "done" said hopefully. A companion that leaves you flying towards a course you do
not have is worse than one that never tries.

Two more things it watches for. If the map key is pressed and the game never reports the map
open, nothing else is sent — the remaining keys are a W and a space bar, and typed into the
cockpit instead of the map they would fly the ship. And if the closing press leaves the map
showing, it says so: *the galaxy map is still open*.

**It needs five keys**: the galaxy map, UI up, UI select, and both sideways camera translates,
right and left. All on the keyboard or mouse — a key on a stick is one Directive 47 cannot press,
and the camera's stick axis is exactly that. It takes all five or none: a macro that opens the map and then has no
"select" leaves it open over the cockpit, which is worse than the clipboard alone. So the first
key it cannot press stops the whole attempt before anything is sent, and you hear which one:

```text
Colonia is on your clipboard. I could not drive the galaxy map myself — You have no binding for
select, so there is no key for me to press. Paste it into the map's search box to plot it.
```

Elite's own default keyboard preset ships the galaxy map **unbound**, so out of the box you will
get the clipboard and that explanation.

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

Return and Ctrl+V are sent as plain virtual keys rather than resolved from the bindings file,
because Elite binds neither. Every wait in the sequence is the Commander's own figure
(2026-08-21), not a measurement.

</details>
