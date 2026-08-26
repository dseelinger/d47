---
title: Interface
group: Interface
nav_order: 125
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">How Directive 47 is laid out, and why one design fits a monitor and a metre-wide quad alike.</p>
<section>
<h2><span class="num">1</span> The tab is the top of the stack, not the first step into it.</h2>
<svg viewBox="0 0 880 258" role="img" aria-label="A tab sits above a stack of levels, and pressing the tab you are already on returns to its top">
 <rect x="20" y="30" width="840" height="44" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="46" y="59" text-anchor="start" font-size="16" font-weight="800" fill="var(--accent)">Loadout</text>
 <text x="200" y="59" text-anchor="start" font-size="15" fill="var(--text-muted)">← press it again to come straight back here</text>
 <rect x="60" y="82" width="800" height="44" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="86" y="111" text-anchor="start" font-size="16" fill="var(--text)">Fleet</text>
 <rect x="100" y="134" width="760" height="44" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="126" y="163" text-anchor="start" font-size="16" fill="var(--text)">Corsair</text>
 <rect x="140" y="186" width="720" height="44" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="166" y="215" text-anchor="start" font-size="16" fill="var(--text)">Hardpoint 3</text>
 <text x="440" y="250" text-anchor="middle" font-size="15" fill="var(--text-muted)">The breadcrumb under the bar is both where you are and the way back.</text>
</svg>
<p class="body">Back is three routes that agree: the breadcrumb, the <strong>grip button</strong> on either controller, and saying so. And voice jumps levels — asking for something three levels down takes you there with the whole trail behind it, rather than dropping you somewhere with nothing above.</p>
</section>
<section>
<h2><span class="num">2</span> Drilling in and reflowing are the same mechanism.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="A narrow surface shows one pane and you drill; a wider one shows two, and a wide one three">
 <text x="110" y="30" text-anchor="middle" font-size="14" fill="var(--text-muted)">MINI PANEL</text>
 <rect x="20" y="40" width="180" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <rect x="34" y="54" width="152" height="82" rx="6" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="400" y="30" text-anchor="middle" font-size="14" fill="var(--text-muted)">THE WINDOW</text>
 <rect x="240" y="40" width="320" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <rect x="254" y="54" width="142" height="82" rx="6" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="1.5"/>
 <rect x="404" y="54" width="142" height="82" rx="6" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="730" y="30" text-anchor="middle" font-size="14" fill="var(--text-muted)">WIDE, OR ZOOMED OUT</text>
 <rect x="600" y="40" width="260" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <rect x="610" y="54" width="76" height="82" rx="6" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="1.5"/>
 <rect x="694" y="54" width="76" height="82" rx="6" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="1.5"/>
 <rect x="778" y="54" width="76" height="82" rx="6" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="440" y="194" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">It is one question: how many panes fit.</text>
 <text x="440" y="226" text-anchor="middle" font-size="16" fill="var(--text)">Same stack, same breadcrumb, same phrases — one design, not four that have to agree.</text>
</svg>
<p class="body">Zoom is what moves you between them most of the time: it re-measures rather than magnifying, so zooming out gives the layout more logical room and a third pane appears.</p>
</section>
<section>
<h2><span class="num">3</span> A chooser takes the whole panel.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="A whole-panel chooser carries the slot it is choosing for in its header, which a drop-down has nowhere to put">
 <rect x="20" y="34" width="400" height="140" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="68" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">A CHOOSER, WHOLE-PANEL</text>
 <text x="220" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">carries the slot, its size,</text>
 <text x="220" y="124" text-anchor="middle" font-size="14" fill="var(--text-muted)">and what is fitted now</text>
 <text x="220" y="154" text-anchor="middle" font-size="14" font-weight="700" fill="var(--accent)">about sixteen rows, comfortably</text>
 <rect x="460" y="34" width="400" height="140" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="68" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text-muted)">A DROP-DOWN</text>
 <text x="660" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">has nowhere to put that</text>
 <text x="660" y="124" text-anchor="middle" font-size="14" fill="var(--text-muted)">and fits fewer rows</text>
 <text x="660" y="154" text-anchor="middle" font-size="14" fill="var(--text-muted)">and cannot exist on a quad at all</text>
 <text x="440" y="214" text-anchor="middle" font-size="16" fill="var(--text)">So it is a level of the stack rather than a pop-up, and Back is the way out of it.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> Text entry is voice first, and never your real keyboard.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Voice is the primary text entry with a drawn keyboard as fallback; there is deliberately no physical keyboard route">
 <rect x="20" y="36" width="400" height="124" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="74" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">VOICE FIRST</text>
 <text x="220" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">a system name is far easier said</text>
 <text x="220" y="130" text-anchor="middle" font-size="14" fill="var(--text-muted)">and it reaches the box once,</text>
 <text x="220" y="152" text-anchor="middle" font-size="14" fill="var(--text-muted)">when it is done</text>
 <rect x="460" y="36" width="400" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="74" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">A DRAWN KEYBOARD</text>
 <text x="660" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">for a number — and it comes back</text>
 <text x="660" y="130" text-anchor="middle" font-size="14" fill="var(--text-muted)">on its own for the three failures</text>
 <text x="660" y="152" text-anchor="middle" font-size="14" fill="var(--text-muted)">Directive 47 can actually detect</text>
 <text x="440" y="208" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">There is no physical keyboard route, and that is deliberate.</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">A global hook is forbidden outright, and raw input would deliver every keystroke on the system.</text>
</svg>
<p class="body">A system name arriving letter by letter is eleven wrong values on the way to the right one — so what you say lands in one piece. Confident, valid and <em>still</em> not what you meant is the one case no machine can catch, which is what the read-back is for.</p>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="vr.html"><span class="ct">Headset →</span><span class="cd">The second surface this same design is drawn on.</span></a>
<a class="card" href="settings.html"><span class="ct">Settings →</span><span class="cd">The rows behind the theme, the zoom and every bound key.</span></a>
<a class="card" href="checklists.html"><span class="ct">Checklists →</span><span class="cd">A stack worth drilling, on the one tab both surfaces carry.</span></a>
</div>
</div>
</div></div>

## The details

How Directive 47 looks, and which keys reach it.

There is nothing to ask for here — this one is all settings. The model cannot repaint the app or
rebind your keys, and was never given a way to.

### Settings

#### Show every setting {#show-every-setting}

Off by default. Directive 47 shows the settings most Commanders change, and folds the rest away
until you ask for them.

**Nothing is switched off by being hidden.** Every folded setting keeps working — at its default,
or at the last thing you set it to. This decides what is drawn and nothing else: it never writes,
clears or resets a single value.

Four kinds of row are on the page whatever this says:

- **Anything you have changed yourself.** The promise is *you are not missing anything*, and a row
  you set is by definition something you did. It also means the page adjusts itself: a new
  Commander sees the calm version, and a tinkerer sees their own work.
- **Your API keys.** A hidden key box is a Commander who cannot work out why nothing speaks.
- **Anything that decides what leaves this machine** — web search, the galaxy search, what
  Directive 47 remembers about you. A page that went calm by no longer mentioning those would be
  calm about the wrong thing.
- **The rows you need to get running**: a provider, a model, a voice, a microphone, and one switch
  that stops Directive 47 talking.

**A card with nothing left on it disappears rather than sitting there empty**, which does more for
the clutter than folding rows does — Diagnostics goes, and so does the headset card when there is
no headset.

**Following a help link always works.** If a page says "change X here" and X is folded, the jump
unfolds the page for the rest of the session. It does not turn this setting on behind your back.

You can say it out loud, too:

```text
show me every setting · show all the settings · show the advanced settings
hide the advanced settings · show fewer settings · just the usual settings
```

The label never uses the word *advanced*, and the phrases happily accept it. That is on purpose:
the phrase list is where Directive 47 meets your words, and the label is where it chooses its own.

#### Theme {#theme}

| Choice | What it looks like |
|---|---|
| `elite` | Amber on near-black. The default, and the one that matches the cockpit. |
| `dark` | Neutral dark. No colour opinion. |
| `light` | Neutral light, for a desktop that is not in a dark room. |
| `guardian` | Guardian teal and gold. |
| `elite-palette` | Elite, recoloured to match your own HUD. |

##### Matching your HUD

`elite-palette` reads the colour matrix Elite applies to its own HUD and uses it on Directive
47's amber palette, so the panel comes out the same colour as your cockpit — teal, white,
whatever you have set — without borrowing anything from the game.

It reads this file and never writes it:

```text
%LOCALAPPDATA%\Frontier Developments\Elite Dangerous\Options\Graphics\GraphicsConfigurationOverride.xml
```

If you have not set a HUD colour, or the file was written by a HUD mod in a shape Directive 47
does not recognise, the theme falls back to plain `elite` — which is what it would have looked
like anyway. Your game configuration is yours; Directive 47 is a guest in it.

#### Zoom {#zoom}

How large the panel is drawn, from 50% to 300%. The gestures are the ones your browser already
taught you:

```text
Ctrl + scroll wheel     one step per notch
Ctrl + plus             one step larger
Ctrl + minus            one step smaller
Ctrl + 0                back to 100%
```

The steps are a browser's too:

```text
50  67  75  80  90  100  110  125  150  175  200  250  300
```

This makes the whole panel larger rather than just the letters, so spacing and layout grow with
the text instead of the text growing inside a layout that stayed put. The panel reflows to the
window at every level — text rewraps, the same thing a browser does — and only scrolls sideways
when what it is showing has a real minimum width it cannot go below, which the settings rows do
and the transcript does not.

It applies to the settings window too, and the level survives a restart like the theme does.

#### Window content {#window-mode}

**Full or mini.** Full is everything. Mini is the transcript's last few lines, the ask box, and the
line under it — the same panel showing less, not a smaller copy of it.

It is the shape for the very ordinary case of one monitor and wanting Directive 47 out of the way
without losing it. The window stays interactive: **you can still type into it**, which is the
difference between a mini window worth having and one you switch off the same day.

**Four ways back.** Mini takes the tab strip, the reading control, the breadcrumb, the search box,
the banners and the header, all of it on purpose — so three of the four deliberately do not live on
the panel at all:

```text
⤢ in the corner      the expand mark, in the panel's bottom-right
Ctrl+M               the key, which works with nothing at all on the surface
"full window"        said out loud
the title bar        mini keeps its decorations, so ✕ still closes it
```

The mark is the four-corner one every video player uses for full screen, and in the full window it
is the same mark pulled inwards. Hover it for the words. It is on every page mini has, and it stays
put while a chooser is open — a chooser is exactly the state you can feel stuck in, so it is the one
control that is never the thing you are stuck behind.

That last one is a decision rather than an oversight. A chromeless strip pinned over the game is
the [overlay](#overlay) and a different thing; keeping the frame here means the window can still be
moved, resized and closed by the means you already know.

**It says "window", never "panel".** *Mini panel* and *full panel* belong to the headset and always
did. A Commander wearing one must not shrink a window they cannot see, and one at a desk must not
resize a quad they are not wearing, so each phrase reaches exactly one surface:

```text
mini window / small window / little window / shrink the window   this window
full window / big window / large window                          this window
mini panel / small panel / little panel / minimal panel          the headset
full panel / big panel / large panel                             the headset
```

**Mini shows every page except Settings.** The checklist, the engineers, the clocks and the story
all read fine at this size; Settings does not — its nav collapses below 900 pixels and its body
wants 700, against a window 512 wide. So switching to mini while you are on Settings moves to the
transcript, and switching back puts Settings up again. While you are in mini, asking for Settings is
declined rather than queued.

**Size is measured, not a number.** Mini is the headset's 512 by 280 plus whatever the ask box
actually wants, at whatever [zoom](#zoom) you are on — so mini at 150% is a bigger mini window
rather than a clipped one.

**Its rectangle is its own.** Where you leave the mini window is remembered separately from where
you leave the full one, so going mini and back lands on the pixel it started on, and a mini window
you widened stays widened. Both survive a restart. The first time you go mini it appears at the
corner the full window was at, rather than jumping across the desk.

This is machine-wide rather than per-Commander: a window is a property of the desk, not of whoever
is flying today.

#### Switch the window between full and mini {#window-mode-key}

`Ctrl+M` out of the box. It works while Directive 47 has focus — which is every moment it is
wanted, because you are looking at the window it acts on.

It flips the row above, so what you did with the key and what Settings says are one thing and
survive a restart together.

#### Window size and position

Not something to set. The window opens at a size that fits the screen it opens on, and after
that it opens where you left it.

If a remembered position would put the window on a monitor you have since unplugged, it is
ignored and the window comes back on a screen you have — a window you cannot reach with the mouse
looks exactly like the app failing to start.

#### Open settings {#open-settings}

Opens the settings window. `Ctrl+,` out of the box.

Press the combination to bind it: the row listens for one and stores what it heard, so there is
no list of key names to learn and no way to type one that does not exist. Clear it to leave the
action unbound.

This one works while Directive 47 has focus.

#### Re-anchor the headset panels {#reanchor}

Puts your world-locked headset panels back in front of you. `Ctrl+Alt+R` out of the box, and it
works **anywhere** — including with Elite in the foreground, which is the only time you want it.
See [Re-anchor](reanchor.md).

A key that works everywhere needs a modifier with it. On its own it would stop working in every
other application, so a bare key is refused when you press it, with a note saying so.

#### Focus the ask box {#focus-ask}

Puts the cursor in the ask box from anywhere in the main window. `Ctrl+L` out of the box.

#### Remember this core for this ship {#bind-ship-core}

Binds the ship you are sitting in to the core aboard, so boarding it puts that core aboard from
then on. `Ctrl+Alt+B` out of the box, and it works **anywhere** — the moment you want it is a
moment Elite has the foreground. Press it again with that core already bound and the binding is
taken back. Directive 47 says what happened out loud, because you are not looking at its window.

See [A core per ship](persona.md#core-for-this-ship).

#### Show the overlay {#overlay}

**The mini panel, on your monitor.** Off out of the box.

Turn it on and Directive 47 pins a small strip over the game: the transcript's last few lines,
and the story if one is running. It is the same panel the headset shows in mini — the same view,
the same model, the same reduced set of things — put on a monitor instead of a quad. Nothing is
kept in step, because there is nothing to keep in step; it cannot show something stale.

**It appears only while Elite is in front.** A strip pinned over a browser is a strip you turn off
within a day, so it comes up when the game has the foreground and goes away when anything else
does — including Directive 47's own window, which is right there showing strictly more.

**It carries the same tabs as the headset's mini panel** — the transcript, the checklist, the
engineers, the clocks and the story — and it follows the main window between them. Settings is the
one it does not have, for two reasons that agree: you could not touch it, and it is the one page
mini cannot fit.

**You cannot click it, and that is the design.** The pointer goes straight through: a click the
strip ate would be a click Elite did not get, and a focus steal mid-combat is worse than anything
it could have been showing. It never takes the foreground and it is not something to Alt-Tab into.
Everything that changes what it shows is somewhere else — the window, a spoken phrase, or a switch.

**Which is why it is scrolled by voice.** The wheel goes straight through it as well, so
["page down"](#scrolling) and its three companions are the only way to read past the fold on this
surface.

**And why it draws no buttons.** A control nobody can press is a control spending room the data
wants, so the strip leaves them out — the checklist's filter and its Add, and anything else whose
whole purpose was being pressed. Checkboxes and the scrollbar stay, because those show you
something.

**There is no interlock with the headset.** If you are wearing one you have no use for this, but
wanting both is real — a second monitor somebody else is watching — so nothing here silently
declines to appear because SteamVR is running.

#### Overlay size {#overlay-size}

How large the strip is drawn, on the same ladder as [Zoom](#zoom):

```text
50  67  75  80  90  100  110  125  150  175  200  250  300
```

At 100% it is 512 by 280 — the size the headset's mini panel is fixed at. It re-wraps at every
step rather than being blown up, so bigger means more readable and not blurrier.

Size is the only lever here. In the headset how big the panel looks is the pixel count and the
quad's width in metres together; on a monitor there is no width in metres, so this is the whole of
it.

#### Overlay opacity {#overlay-opacity}

How much cockpit shows through it. `1` is solid, and it does not go below `0.2` — an overlay at
zero is one that is switched on, invisible, and indistinguishable from broken.

#### Elite's display mode {#overlay-fullscreen}

Not something to set. **Directive 47 reads which display mode Elite is in and tells you**, because
this is the one way this feature fails without saying anything.

A window pinned on top draws over a **borderless** or **windowed** game. Over an
**exclusive-fullscreen** one it is simply not there: the game owns the screen, and there is no
error, no log line and nothing to diagnose. You would turn the overlay on, see nothing, and have
no way to find out why.

So it reads this file, and never writes it:

```text
%LOCALAPPDATA%\Frontier Developments\Elite Dangerous\Options\Graphics\DisplaySettings.xml
```

```xml
<DisplayConfig>
	<FullScreen>2</FullScreen>
</DisplayConfig>
```

| `FullScreen` | What it means |
|---|---|
| `0` | Windowed. The overlay draws over it. |
| `1` | Exclusive full screen. **The overlay will be invisible.** Set Elite to borderless. |
| `2` | Borderless. The overlay draws over it. |

`2` is the one confirmed against a real machine. `0` and `1` are the community's reading, so
anything else is reported by number rather than guessed at.

If the file is missing, hand-edited, or written by a mod in a shape Directive 47 does not
recognise, it says it could not tell **and draws the overlay anyway** — your game configuration is
yours and Directive 47 is a guest in it, so it never refuses to do something because it could not
read one of your files.

#### Show or hide the overlay {#show-overlay}

`Ctrl+Alt+O` out of the box, and it works **anywhere** — which is the point, because the moment you
want it is a moment the game is filling the screen. It flips the row above, so what you did with
the key and what Settings says are one thing and survive a restart together.

#### Move the overlay {#move-overlay}

`Ctrl+Alt+M` out of the box, and it works **anywhere** too.

Press it and the strip briefly takes clicks so you can drag it, with a border round it to show it
has hold of them. Let go and it hands them straight back — press again without dragging and it does
the same. It still never takes the foreground from Elite, even mid-drag.

It comes up for this even if the game is not running, so you can put it where you want it before
you launch.

**Where you put it is remembered, and it is not a setting.** A screen coordinate is not something
you typed, so it goes to `view-state.json` beside the main window's position and the headset's
panel anchors rather than into `settings.json`. If it ends up on a monitor you later unplug, it
comes back on one you have.

**Until you place it, it follows the game.** With no corner of your own chosen, the strip goes to
the bottom-right of **whichever monitor Elite is on** — asked afresh every time it appears, so
moving the game to another screen takes the strip with it. Bottom-right because that is the corner
of Elite's HUD with the least on it.

Once you have dragged it somewhere, that corner is yours and nothing moves it again. **A default
may follow the game around; a choice may not.**

---


**The model cannot change any of these rows.** A bound key is one of the ways to reach a
protected setting, so a model that could rebind one could hand itself a way in it is not allowed
to have. See [Settings](settings.md).

### The panel {#panel}

One bar along the top, and each tab is a surface of its own:

```text
Transcript   Routing   Checklist   Loadout   Engineers   Utilities   Settings
```

**The two surfaces do not carry the same tabs.** In the headset the panel is Transcript,
Checklist, Engineers, Utilities and Settings; Routing and Loadout are the window's. That is a
choice rather than a limitation — a tab appears where a host asks for it, and asking is one line
— and the reasons differ. Settings wants a 1180-pixel navigation column, which at a metre is a
wall rather than a page. Routing's plan forms want a keyboard the headset has not got, though its
Progress page would read well at a metre and may yet go there. Checklist and Loadout were both
withdrawn on the Commander's own instruction, and the checklist went back on it — what a
Commander is working on is worth reading in the one place there is no other way to read it
(Phase 39). Loadout stays where it is: a three-level drill ending in a search field is a bigger
surface than a list of short rows, and it is a separate decision.

A tab you have not got is a tab that is not drawn. The surfaces arrive as they are built.

#### Drilling in, and finding your way back {#drilling}

Every surface below the transcript is a stack — Fleet, then a ship, then a slot, then a
blueprint. **The tab is the top of that stack, not the first step into it**, which is what makes
one gesture worth knowing:

> **Press the tab you are already on to go back to the top of it.**

Below the top, a **breadcrumb** appears under the bar. It is both where you are and the way back,
because a headset has no title bar to orient by and losing your place is expensive when you
cannot glance at a second monitor. Every crumb but the last can be pressed — **and said**:

> "back"
> "corsair"
> "checklist"
> "select the checklist tab"

Back is three routes that agree: the breadcrumb, the **grip button** on either controller, and
the phrase. Say the name of the tab you are already on and you go back to its top, exactly as
pressing it does. A tab answers to its bare name, to "show", "open", "go to", "switch to" or
"select" in front of it, and to "tab" after it — and to nothing looser, because a phrase that
merely mentions a tab is not a request for it.

**Voice jumps levels.** Asking for something three levels down takes you there with the whole
trail behind it, rather than dropping you somewhere with nothing above.

Drill state survives switching tabs, and a tab with more than one mode keeps a separate stack per
mode — leave Ships halfway into a slot, look at something else, come back where you were.

#### One design, one to three panes {#panes}

Drilling in and reflowing are the same mechanism: **how many panes fit**. A wide panel shows the
level you are on beside the one above it, and a third if there is room; a narrow one shows a
single pane and you drill. Same stack, same breadcrumb, same phrases — so it is one design across
the headset's big panel, its mini panel, the desktop window and every zoom level, rather than
four arrangements that have to agree.

Zoom is what moves you between them most of the time: it re-measures rather than magnifying, so
zooming out gives the layout more logical room and a third pane appears.

#### Choosing {#choosing}

**A chooser takes the whole panel** until you dismiss it, which makes it a level of the stack
rather than a pop-up over the page. That is not a stylistic call — a pop-up cannot exist on the
headset surface at all — but it earns its place anyway:

- it fits about sixteen rows at a comfortable size, and twenty-five zoomed out, where a layer over
  the page fits fewer;
- and it **carries what you are choosing for in its header** — the slot, its size, and what is
  fitted now — which a drop-down has nowhere to put.

It behaves as a modal: nothing navigates away mid-choice, and **Back** is the way out, the same
affordance every other level has. Short lists — a five-item setting — stay as a layer over the
page instead, and which one a control gets is fixed per control rather than decided by how many
rows it happens to have today.

#### Saying it, or typing it {#entry}

Text entry is **voice first, with a drawn keyboard as the fallback**, and which one opens depends
on what is being entered: a system name is far easier said than typed, and a number is the
reverse.

While it is listening it says so and shows the words as they arrive, so you are never talking at
a blank page. What you say **reaches the box once, when it is done** — a system name arriving
letter by letter is eleven wrong values on the way to the right one.

**The keyboard comes back on its own** for the three failures Directive 47 can actually detect:

| What happened | What you see |
|---|---|
| Nothing heard at all | "I did not catch that." |
| It was not sure what it heard | "I was not sure I heard that correctly." |
| The value is not a thing | Why not — "there is no system by that name" |

Confident, valid, and *still* not what you meant is the one case no machine can catch, which is
what the read-back is for.

**There is no physical keyboard route, and that is deliberate.** Every way of receiving
keystrokes while Elite has the foreground is closed: a global keyboard hook is forbidden outright,
raw input would deliver every keystroke on the system including passwords typed into other
applications, and the polled route reads exactly one bound key. The only remaining option is
taking your keyboard focus away from Elite mid-session, which is a worse trade than a drawn
keyboard you never have to use.

### The transcript {#transcript}

The transcript tab has three **modes**, on the segmented control at the right of the tab bar.
They are three readings of one exchange rather than three destinations, which is why they are
modes and not three tabs of their own.

**Conversation** is you and the ship's AI, and nothing else. It is what opens.

It is drawn as a conversation, the way the messaging app on your phone draws one: a turn to a
bubble, **yours on the right in the theme's own colour** and **the ship's on the left**. When D47
notes something *about* the conversation rather than saying something in it — the core changing
under you — that sits across the middle in the accent, with no bubble, because it is not a side.

The headset's big panel does the same. The mini panel does too and spends less on it: the same
sides and the same colours, with the gutter and most of the padding given back, because a surface
512 pixels across cannot afford to say twice over which side a turn is on.

Only this page. **Technical** and **Log file** are one flat block of text as they have always
been — a diagnostic feed and a file are not conversations between anybody, and the `> ` in front
of your own words there is how a flat page says who spoke.

**Technical** is the same with the diagnostics left in — the version banner, where things are
installed, whether the language model came up. This is what the panel used to show all the time.

It also carries **the speech loop, as it happens**: the microphone opening, what you said being
turned into words, the answer being worked out, the answer being spoken. Each stage is a line
that stays, so when something stops part-way, what it got as far as is still on the page above
it. That is the difference between this and the microphone indicator beside the ask box — the
indicator says what is true right now, and this says what happened.

```text
[21:04:07] Microphone open, listening.
[21:04:09] Turning what you said into words.
[21:04:10] Working on an answer.
[21:04:12] Speaking the answer.
```

**Errors from the speech path land here too**, with the cause attached rather than only the
sentence:

```text
[error] Could not start capture — device in use by another application
```

Errors only, and only from speech. Warnings and the rest of the running commentary stay in the
log file, because a page that repeats another page is one nobody reads.

**Log file** is today's log, read when you open the page rather than tailed continuously: a log
nobody is looking at is not worth a file read per tick, and one you *are* looking at is open
because something already went wrong. Switch away and back to re-read it.

One asymmetry between the three is kept rather than smoothed over: Conversation and Technical are
the same turns at two verbosities, and **Log file is a file read off disk** — which is why it,
alone of the three, carries a working indicator, and why this is three modes rather than a single
toggle.

Which mode you are on belongs to the surface you are looking at, not to the transcript. The
desktop window can sit on the log while the headset panel shows the conversation — same
transcript underneath, and each surface decides how much of it to show. The mini headset panel
has no bar at all, being the transcript's tail and the provenance line and nothing else.

#### Scrolling by saying so {#scrolling}

Four phrases, on **all three surfaces at once** — the window, the headset panel and the flat
overlay:

```text
page down       page forward      next page
page up         page back         previous page
scroll down     down a bit        scroll down a bit
scroll up       up a bit          scroll up a bit
```

A page is a screenful less one line, so the line you were reading when you said it is still there
when the page settles. A scroll is three lines, which is what one notch of a wheel does nearly
everywhere.

**It moves whichever page is showing**, not the transcript specifically — the checklist, the
engineers and the clocks scroll the same way. If a chooser is open, it moves the chooser.

**Why this exists, on each surface:**

```text
the window     the wheel already works; this is for hands on a stick
the headset    a ray on a twelve-pixel bar was the only way
the overlay    there was no way at all — the wheel goes straight through it
```

Dragging the scrollbar is unchanged and is not being replaced. The thumbsticks stay unbound.

**Nothing happens at the end, and that is deliberate.** Say "page down" at the bottom and Directive
47 declines rather than reporting a scroll it did not make, so the phrase falls through to be
answered instead of vanishing into a silence that looks like not being heard.

Scrolling up stops the transcript following the newest line, exactly as dragging it up does. Press
the jump-to-latest control, or scroll back to the bottom, to start following again.

**It is never a tool.** Like moving between tabs, this is matched before any provider is consulted
— so it works with no model configured, and nothing an in-game message says can move your page.

#### Following it, or not {#following}

The transcript follows its own newest line, which is what you want almost all of the time and
exactly what you do not want the moment you scroll up to read something — a busy session appends
several lines a second, and every one of them used to drag you back to the bottom.

Scrolling away from the end stops it following. A **↓ Newest** button appears over the bottom
right of the page while you are behind; pressing it goes to the newest line *and* starts
following again, and so does scrolling back to the bottom yourself. There is no mode to
remember: the lock is inferred from where you are looking.

Following belongs to the page, so arriving at a different tab opens it at its newest line rather
than carrying "I have scrolled up" across with you.

#### Selecting and copying it {#copying}

The transcript is selectable text. Drag across it and press <kbd>Ctrl</kbd>+<kbd>C</kbd>, exactly
as anywhere else — including across lines that arrive while you are selecting.

On **Conversation** a selection is made within one turn, which is what a page drawn as bubbles
can do and what every messaging application does with the same shape. On **Technical** and
**Log file** it runs across the whole page, as before. Either way, **Copy** takes the lot.

**Copy**, beside the search box, takes the whole page as it is currently shown: the conversation
without the diagnostics, or with them, or the log file. A search query highlights on these pages
rather than filtering, so it deliberately does not narrow what is copied — you asked for the log.
The button says **Copied** for a moment, and says so if it could not.

### The ship AI's face {#avatar}

Top left of the panel, and in the headset too. One look per stage of a turn, so you can tell what
Directive 47 is doing without reading anything — which in a cockpit is the point.

| Stage | What it shows |
|---|---|
| `idle` | A closed ring. Nothing in flight. |
| `listening` | An open mouth. The microphone is on. |
| `transcribing` | Three rising bars — sound becoming words. |
| `thinking` | Four blocks around a gap, the Guardian motif, working. |
| `speaking` | A widening wave. |
| `answered` | A tick. |
| `unsure` | A question mark, drawn rather than typed. |
| `failed` | A cross. |

They differ in shape as well as colour, so they read in greyscale — colour is never the only
signal. Colour comes from your theme, so switching theme repaints the face with everything else.

Each one breathes: a slow opacity cycle, faster while a turn is running and very slow at rest.
Nothing here jitters, because a companion's face that jitters is a companion that looks anxious.

#### Using your own

Drop image files into `data/avatar/<state>/` beside the executable — the same shape the audio
cues use, so if you have already customised those you know this one.

```text
data/avatar/thinking/01.png
data/avatar/thinking/02.png
data/avatar/thinking/03.png
```

- **One folder per state, and replacing one leaves the rest alone.** Your `thinking` frames do
  not turn off the drawn `idle`.
- **More than one file in a folder animates**, cycling in filename order at four frames a second.
  That is how an animated avatar is supported: as a sequence of stills.
- `.png`, `.jpg`, `.bmp` and `.webp`. **Not `.gif` or `.svg`** — Directive 47 decodes neither, and
  offering a format that silently never renders would be worse than not offering it.
- An empty, unreadable or corrupt file is skipped, and a state with nothing usable falls back to
  the drawn face rather than showing a broken box.

The shipped faces are drawn rather than supplied as images, and that is not only an aesthetic
choice: one widget tree renders to both the window and the headset, so an animation made of
decoded frames would need something advancing it on two surfaces — and it would put an image
decoder in a dependency graph that has stayed clear of them since the first release.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

This capability registers no tools. A descriptor declares a capability's whole surface, and this
one's surface is settings rows — giving the model a way to repaint the app or rebind a gesture
would be reach added for the sake of symmetry.

Colour lives in one resource dictionary per theme, keyed by role rather than by colour, so no
view hardcodes a literal and a sixth theme is a file rather than a sweep through every screen:

```text
D47.Background      the window behind everything
D47.Surface         cards, the transcript, raised areas
D47.SurfaceAlt      row striping, inset areas, the ship's side of the conversation
D47.Border          hairlines between things, and the chip behind a code span
D47.Text            body text
D47.TextMuted       help text, placeholders, provenance lines
D47.Accent          the theme's own colour: focus, headings, the ask button
D47.AccentMuted     the same colour with the volume down: your side of the conversation
D47.Danger          the error banner
D47.Info            the update banner
```

The HUD override file holds a 3x3 matrix — each output channel is the dot product of one row
with the source colour, which is how a Commander's HUD ends up teal without the game shipping a
teal palette:

```xml
<GraphicsConfig>
  <GUIColour>
    <Default>
      <LocalisationName>Standard</LocalisationName>
      <MatrixRed>   0.35, 0.00, 0.00 </MatrixRed>
      <MatrixGreen> 0.00, 0.80, 0.00 </MatrixGreen>
      <MatrixBlue>  0.00, 0.20, 1.00 </MatrixBlue>
    </Default>
  </GUIColour>
</GraphicsConfig>
```

Zoom is a layout transform rather than a font-size change: it re-runs measure and arrange at the
new scale, so text rewraps and padding grows with it. The zoomed content sits in a horizontally
scrolling viewport, and the content is constrained to that viewport's width divided by the scale
— which is what gives the layout a width to wrap against at all. A scroll viewer that may scroll
sideways measures its child with *infinite* width, and without the constraint nothing below it
ever wraps: the transcript became one line as long as the longest thing D47 had said, and the
window could be dragged wider forever without reaching the end of it.

Sideways scrolling is what is left for content that genuinely will not fit: the settings grid
holds a minimum of 700 points, because below about 420 a row cannot keep its caption beside its
control and the caption was squeezed to nothing while the control drew over it. A minimum beats
the viewport constraint, so those rows push the scrollbar back out; the transcript, which has no
minimum, simply rewraps.

The opening size is a proportion of the working area of the screen the window appears on — 55% of
its width and 75% of its height, floored so a small screen still gets a readable window — rather
than a fixed size written down once against somebody else's monitor. It is then clamped to 90% of
that working area —
90% rather than 100% because a window filling the work area exactly reads as maximised. Size and
position live in `view-state.json` beside the executable and fail quietly if unreadable.

</details>
