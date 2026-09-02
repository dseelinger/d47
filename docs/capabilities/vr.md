---
title: Headset
group: Interface
nav_order: 126
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and that is load-bearing rather than cosmetic.
  HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under that class
  would silently become what the in-app panel draws on this page. The docs site styles the two
  identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. That separation is the
  whole point of there being two, and it is the thing that will erode first.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="lede">Three steps to D47 in the headset.</p>
<section>
<h2><span class="num">1</span> Start SteamVR first, then D47.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Headset">
 <rect x="20" y="16" width="840" height="212" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Headset</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Draw in the headset</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">on</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">SteamVR</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text)">running</text>
 <text x="44" y="222" font-size="15" fill="var(--text-muted)">D47 draws over Elite as an overlay. It is beside the game, never inside it.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Put the panel where you want it.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">put the panel down</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"lock it to my head" — "move it left" — "bring it nearer" — "tilt it up"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">World-locked stays where you put it. Head-locked follows you.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Set the size and the distance.</h2>
<svg viewBox="0 0 880 308" role="img" aria-label="Headset">
 <rect x="20" y="16" width="840" height="268" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Headset</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Distance</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">1.1 m</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Size</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text)">0.9 m wide</text>
 <rect x="44" y="182" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="210" font-size="16" fill="var(--text)">Opacity</text>
 <text x="812" y="210" text-anchor="end" font-size="16" fill="var(--text-muted)">85%</text>
 <text x="44" y="278" font-size="15" fill="var(--text-muted)">The tilt is worked out from these, so a panel below eye level faces you without being told.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="The desktop window can be minimised and the overlay stays.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">The desktop window can be minimised and the overlay stays.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">They are independent on purpose. Closing D47 is what takes the quad away, not minimising it.</text>
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
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Directive 47 drawn over Elite in your own cockpit — beside the game, never inside it.</p>
<section>
<h2><span class="num">1</span> It attaches. It never starts anything.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="Directive 47 waits for SteamVR and a headset to appear, and never launches SteamVR or hooks Elite">
 <rect x="20" y="44" width="250" height="100" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="145" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">IT ATTACHES</text>
 <text x="145" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">looks every few seconds</text>
 <rect x="310" y="44" width="250" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="435" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">NEVER STARTS</text>
 <text x="435" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">SteamVR, on your behalf</text>
 <rect x="600" y="44" width="260" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="730" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">NEVER HOOKS</text>
 <text x="730" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">Elite, or its frame</text>
 <text x="440" y="188" text-anchor="middle" font-size="16" fill="var(--text)">SteamVR first, Directive 47 first, SteamVR restarted halfway through — none of it matters.</text>
 <text x="440" y="220" text-anchor="middle" font-size="15" fill="var(--text-muted)">And it says which of the two it is waiting for: “SteamVR is not running”, or “no headset is switched on”.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> The panel is the same app, drawn a second time.</h2>
<svg viewBox="0 0 880 230" role="img" aria-label="The headset panel is a second instantiation of the same view, not a screenshot of the desktop window">
 <rect x="20" y="44" width="380" height="104" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="210" y="84" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">THE SAME APP</text>
 <text x="210" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">drawn a second time</text>
 <rect x="460" y="44" width="400" height="104" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">NOT A PICTURE OF IT</text>
 <text x="660" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">no screenshot of a window</text>
 <text x="440" y="198" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So the windowed version can never do something the headset version cannot.</text>
</svg>
<p class="body">Point a controller at it, pull the <strong>trigger</strong>, and it comes with you — position and angle together, so it feels attached rather than dragged. Nothing turns it to face you while you hold it: a panel forced upright and square cannot be tilted to read from below or angled to sit beside you, which is most of what moving one is for.</p>
</section>
<section>
<h2><span class="num">3</span> Three levers that all sound like “how big”.</h2>
<svg viewBox="0 0 880 258" role="img" aria-label="Resolution decides how much the image holds, size how big it looks, and scale how much layout the pixels carry">
 <rect x="20" y="36" width="270" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="76" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">RESOLUTION</text>
 <text x="155" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">how much the image can hold</text>
 <text x="155" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">more pixels, more rows</text>
 <rect x="305" y="36" width="270" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="76" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SIZE</text>
 <text x="440" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">how big it looks in the room</text>
 <text x="440" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">metres across</text>
 <rect x="590" y="36" width="270" height="124" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="725" y="76" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SCALE</text>
 <text x="725" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">how much layout those</text>
 <text x="725" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">pixels carry — a density</text>
 <text x="440" y="198" text-anchor="middle" font-size="16" fill="var(--text)">At 75% the panel presents 1365×853 worth of layout and a ship’s slots fit.</text>
 <text x="440" y="224" text-anchor="middle" font-size="16" fill="var(--text)">At 200% it presents 512×320 and you can read it from across the cockpit.</text>
 <text x="440" y="252" text-anchor="middle" font-size="15" fill="var(--text-muted)">More pixels cost more every frame — pick by looking at it, not by picking the biggest number.</text>
</svg>
<p class="body">Scale and <em>mini</em> are different things, and the difference is the point: scale changes how big everything on the panel is, mini changes how much of it there is. Zooming a panel you cannot read makes it readable; switching to mini gives you less to read.</p>
</section>
<section>
<h2><span class="num">4</span> The captions follow the broadcast standard, not a preference.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="Caption limits: 42 characters a line, two lines per utterance, 20 characters a second, timed from when the speech ends">
 <rect x="20" y="34" width="840" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="60" y="68" text-anchor="start" font-size="16" font-weight="700" fill="var(--text)">42 characters</text>
 <text x="270" y="68" text-anchor="start" font-size="15" fill="var(--text-muted)">the most on one line</text>
 <text x="60" y="96" text-anchor="start" font-size="16" font-weight="700" fill="var(--text)">2 lines</text>
 <text x="270" y="96" text-anchor="start" font-size="15" fill="var(--text-muted)">the most for one thing said</text>
 <text x="60" y="124" text-anchor="start" font-size="16" font-weight="700" fill="var(--text)">20 a second</text>
 <text x="270" y="124" text-anchor="start" font-size="15" fill="var(--text-muted)">reading speed, which sets how long one stays</text>
 <text x="60" y="150" text-anchor="start" font-size="16" font-weight="700" fill="var(--accent)">timed from the end</text>
 <text x="270" y="150" text-anchor="start" font-size="15" fill="var(--text-muted)">not from when the speech starts</text>
 <text x="440" y="202" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">Nobody reads along with a voice they can hear.</text>
 <text x="440" y="234" text-anchor="middle" font-size="15" fill="var(--text-muted)">They catch the last line after it has gone — and if you say “stop”, the captions stop with the voice.</text>
</svg>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="interface.html"><span class="ct">The window →</span><span class="cd">The same panel on the surface you can point a mouse at.</span></a>
<a class="card" href="settings.html"><span class="ct">Settings →</span><span class="cd">Where every row on this page lives, and why Settings is desktop-only.</span></a>
</div>
</div>
</div></div>

## The details

Directive 47 in your headset, drawn over Elite in your own cockpit.

It sits alongside the game rather than replacing it. It never hooks Elite, never touches its
process, and never takes over the frame — it is an overlay, the same way SteamVR's own dashboard
is.

### Ask for it

> "is the headset working"
> "headset overlay off"
> "mini panel"
> "full panel"

### The order does not matter

SteamVR first, Directive 47 first, SteamVR restarted halfway through the evening — none of that
is something you should have to think about. Directive 47 looks for a headset every few seconds
and builds everything the moment one appears.

It **attaches** to SteamVR rather than starting it. If SteamVR is not running, or your headset is
switched off, Directive 47 waits and tells you which of the two it is waiting for:

```text
SteamVR is not running. D47 will attach when you start it.
```

```text
No headset is switched on. D47 will attach when one appears.
```

Nothing here starts SteamVR on your behalf. Launching it uninvited on a machine whose headset is
off takes over your desktop for no reason.

### What you get

Two things float in front of you.

**The panel** — the same panel as on the desktop, not a picture of it. It is the same app drawn a
second time, so the windowed version can never do something the headset version cannot.

**Captions** — everything Directive 47 says, written underneath. They place themselves, clear
themselves, and cannot be moved or dragged somewhere you would not see them.

### Moving it about

**Say where you want it.** The panel moves a step at a time, in whichever direction you name:

> "move the panel left" / "move the panel right" / "move the panel up" / "move the panel down"
> "move the panel closer" / "move the panel further away"
> "turn the panel left" / "turn the panel right"
> "tilt the panel up" / "tilt the panel down"

One step is 5 cm, or 5 degrees for a turn or a tilt. Say it again for another step. **Turning**
swings the panel's face towards that side; **tilting up** leans it back towards your eyes, which is
what a panel sitting below them wants.

It acts on whichever panel is on screen — big or mini — and each keeps its own place. If the panel
was still riding your head when you asked, Directive 47 puts it down in front of you first and then
moves it, exactly as picking it up with a controller used to.

No model is needed for any of this. The phrases above are matched by name, so they work with no
provider configured.

**Motion controllers are off out of the box** — see [below](#controllers) for why, and for what
turning them back on gets you.

With them on, point a controller at the panel, pull the **trigger**, and it comes with you —
position and angle together, so it feels attached rather than dragged. Let go and it stays.

While a ray is on the panel, Directive 47 asks SteamVR for the trigger and grip of that controller
at overlay priority, and gives them back the moment the ray leaves. On a default SteamVR install
that changes nothing you would notice: taking inputs away from other applications needs SteamVR's
**Enable global input from overlays** developer setting, which is off unless you turned it on, and
without it the game, Virtual Desktop and the dashboard keep the trigger while Directive 47 reads it
too. With it on, the hand-back is what returns the trigger — 0.48.6 meant to do that on every path
and did not, because SteamVR refused the call it made and said so only in d47's log; 0.52.2 does,
and its log says when the controllers are claimed and given back, and **"headset overlay off"**
then **"headset overlay on"** still frees a controller on an older build. A controller that stops
answering everything some way into a session is a different fault, open as
[#18](https://github.com/dseelinger/d47/issues/18).

Nothing turns it to face you while you hold it. A panel forced upright and square cannot be
tilted to read from below or angled to sit beside you, which is most of what moving one is for.

Picking it up makes it world-locked, because picking something up and putting it down is what
world-locked means. Directive 47 does not then argue with you about the setting.

If Elite's own recenter moves your cockpit out from under a panel you had placed, put it back by
switching that panel's lock to **head**, which brings it to your face, and then nudging it — the
first nudge sets a head-locked panel down in front of you, at knee height, before it moves. Two
steps where re-anchoring was one; that command was retired in 0.94.0.

### Settings

#### Show Directive 47 in the headset {#enabled}

On by default, which costs nothing if you have no headset — it looks, finds nothing, and says so.
Turn it off if you use SteamVR for other things and would rather Directive 47 stayed out of it.

> "headset overlay on" / "headset overlay off"

#### Panel content {#mode}

**Full** or **mini**. Mini is the same panel showing less — the gear, the banners and the ask box
go, the transcript stays. It is also drawn smaller, so it can sit at the edge of vision.

**Mini out of the box.** The full panel is 1.1 m across — close to fifty degrees of view — which
is a lot to meet before you know it can be moved or shrunk. Switch to full whenever you want it.

Ask for either by name, in whichever words come out:

> "big panel" / "large panel" / "full panel"
> "small panel" / "little panel" / "minimal panel" / "mini panel"

The two keep their own placements, so parking mini out to one side while the full panel stays in
front of you works the way you would expect.

**Mini carries no controls, only what they were showing you.** It is 512 pixels wide, chosen so the
text is readable rather than so there is room — every button on it would be space taken from the
thing you opened it for. So a page's own bar goes: the checklist's filter, its ordering, its
import, its Goals tick. What stays is the list itself, **including the tick beside each line**, so
you can still mark work done from the headset. The big panel keeps the whole bar, and that is the
one headset surface where a controller can genuinely press it.

#### Motion controllers {#controllers}

**Off out of the box, and that is a withdrawal rather than a preference.** A controller put down
while Directive 47 was connected to SteamVR, that then went to standby, never woke up on its own —
every time. Put down while Directive 47 was *not* connected, it always did. That is
[#18](https://github.com/dseelinger/d47/issues/18), and it is not understood.

Directive 47 read your controllers' positions about ninety times a second for the whole session,
whether or not you were pointing at anything — around 350,000 times in one hour-long session in
which no ray ever touched the panel. A read like that running across the moment SteamVR puts a
controller to sleep is the one interaction the evidence points at, and turning it off is the only
way to test it.

**What you lose while it is off.** Nothing on the panel can be pressed in the headset: no buttons,
no toggles, no checklist ticks, no combo boxes, no on-panel keyboard, no scrollbar dragging, no
grip-to-go-back, and no Settings tab. You cannot grab the panel and carry it.

**What still works.** Everything by voice: moving between tabs, going back, scrolling, answering a
question the panel is already asking, and placing the panel — see
[Moving it about](#moving-it-about) above, which is the replacement for the carry.

**It is not forever.** Turn it back on and everything above returns, exactly as it was. A session
with it on and a session with it off are the experiment; if the fault comes back with it on, that
is the answer, and if it happens anyway with it off, the controller was never the cause.

**The row wears a warning badge, and the advice on it is deliberate**: when you finish trying it
on, turn it back off *and restart Directive 47*. Turning it off mid-session stops the pose reads,
but the session has already touched the controllers by then — the restart is what puts you back in
a session that never did, which is the state the evidence above calls safe.

> "motion controllers on" / "motion controllers off"

#### Placing a surface

Five settings each, and the mini panel has its own copies of all five. The big panel has a sixth.

**Ask for a change and it lands on the panel you are looking at.** The two surfaces keep their own
numbers — that is the point of mini, which exists to sit smaller and further out of the way — so
*"move it closer"* means the one currently on screen, and the other one does not move. Both are on
this page in full, so setting mini up while the big panel is in front of you is a thing you can do
here whenever you like.

**"Size" and "Scale" are different questions and it is worth knowing which you want.** Size is how
big the panel itself is, in metres across — the edges move. Scale is how big the writing and the
controls on it are drawn, without the edges moving. Wanting bigger text on a panel that is already
the right size is Scale.

| Setting | What it does |
|---|---|
| Locking {#panel-lock} | Head-locked follows you; world-locked stays where you put it |
| Distance {#panel-distance} | How far in front of you, in metres. Head-locked only — a surface you put down is where you put it |
| Size {#panel-size} | How wide, in metres. Height follows the panel's proportions, so there is nothing else to set |
| Curvature {#panel-curve} | 0 is flat, 1 is wrapped around you |
| Scale {#panel-scale} | How large the content is drawn, on the same steps the desktop window zooms with |
| Resolution {#panel-resolution} | How many pixels the big panel is rendered at. Big panel only |

##### Opacity is one knob for both {#opacity}

How solid the panel is, from 0.1 to 1 — and **one setting, not one per panel**. Everything in the
table above is genuinely per-surface: mini exists to be smaller and further out of the way, so its
distance, size and drop have to be able to differ. How see-through the glass is does not. It is one
preference about how much cockpit shows through Directive 47, and asking for it at half never means
*half, in one of the two modes*.

It used to be two, and that is exactly how it went wrong: *"set the opacity to 0.5"* landed on the
big panel's copy while the mini panel was the one in front of the Commander, so the number they
could see never moved and nothing was broken. A value already set on either panel is carried up to
the shared one the first time this version runs, so nobody has to set it again.

##### Three levers, and they do different things

Size, Scale and Resolution all sound like "how big", and keeping them apart is worth a minute.

- **Resolution decides how much the image can hold.** More pixels, more room for rows.
- **Size decides how big it looks in the room.** Metres across, at whatever distance you put it.
- **Scale decides how much layout those pixels carry.** It re-measures and rewraps rather than
  simply magnifying, so it is a *density* control: at 75% the default panel presents 1365x853
  worth of layout and a whole ship's slots fit; at 200% it presents 512x320 and you can read it
  from across the cockpit.

Every resolution is the same shape, so changing it never changes the proportions of the thing in
front of you — only how much detail is in it. **More pixels cost more to render, every frame**,
and past the point where the panel covers what your headset can actually resolve they buy you
nothing. Pick by looking at it, not by picking the biggest number.

Head-locked is what you want for something you glance at; world-locked for something that lives
in a particular corner of the cockpit.

**World-locked out of the box**, and put down for you the first time Directive 47 runs in your
headset: about a metre ahead of wherever you are facing, low enough that the top of the panel sits
around knee height, and tilted back so it faces you rather than the ceiling. A panel that follows
your gaze is in the way of whatever you turned to look at, which is the one thing a companion
beside a flight sim should never be.

That first position is worked out from where your head actually is — the floor comes from your
room setup, and the height of the panel from its width and proportions — so it is not a number
picked for somebody else's height or somebody else's panel size. Move it once and it is yours;
Directive 47 never places it again.

**Scale and mini are different things**, and the difference is the point: scale changes how big
everything on the panel is, mini changes how much of it there is. Zooming a panel you cannot read
makes it readable; switching to mini gives you less to read.

The mini rows work the same way: [locking](#mini-lock), [distance](#mini-distance),
[size](#mini-size), [curvature](#mini-curve) and [scale](#mini-scale) — but not opacity, which
[both panels share](#opacity).
The mini panel defaults to 0.34 m across — it is meant to sit at the edge of vision — against
1.1 m for the full one.

#### Captions {#captions}

They follow the broadcast closed-caption standard rather than anyone's preference:

```text
42 characters   the most on one line
 2 lines        the most for one thing said
 2 lines        the most on screen at once
20 per second   reading speed, which decides how long one stays up
5/6 s .. 7 s    the shortest and longest a caption lingers
```

A caption stays up timed from when the **speech ends**, not when it starts. Nobody reads along
with a voice they can hear; they catch the last line after it has gone.

**A long sentence arrives as several captions, not as its last two lines.** Anything that wraps
past two lines is shown two at a time, each staying up for as long as your reading speed says it
takes to read, until the sentence has been shown in full — which is what a caption track does with
a long line. If Directive 47 starts saying something new while an earlier sentence is still being
shown, the new one takes over: a caption still working through something the voice finished with
is no longer captioning.

**Sounds that mean something are captioned too.** The warning tone that plays a moment before an
urgent callout is written in brackets — `[interdiction alert]`, `[attack alarm]`, `[heat alarm]` —
so a Commander reading captions gets the same head start a Commander hearing them does. The loop
tones are not captioned, because the panel already shows the loop state; ambience, music and the
thinking bed are not, because they are continuous and say nothing.

**And a caption says who is talking when it is not Directive 47.** Your carrier's tower and its
captain are different people in different voices, and a crew member you address by name answers in
theirs — so those lines open with `[Tower]`, `[Carrier]` or the crew member's own name. Directive
47's own lines carry no name: it is the voice the caption band belongs to, and putting a label on
every line would be noise. In-game messages from other Commanders are not captioned at all — they
are already written out in full, with their sender, on the Transcript page's Journal File reading.

Captions follow what is actually audible rather than what was generated, so if you say "stop" the
captions stop with the voice. A caption still sitting there after a silence command is Directive
47 visibly not having stopped.

**They stay level with the horizon, not with your head.** Captions follow where you are looking —
turn or look down and they come with you — but they ignore how far your head is *tilted*, so the
text runs along the same line the cockpit does instead of along your eyeline. Tilt your head
twenty degrees and the captions stay put and level while your view rolls around them. There is no
setting for this and deliberately so: a caption is either level or it is wrong, and a tilt dial
you have to trim by hand is a way of shipping it wrong.

If your captions look level but the *cockpit* does not, that is Elite's own recenter having been
taken with your head tilted — recenter again with your head straight, and both agree.

#### Caption size {#size}

`small`, `medium` or `large`. Three sizes rather than a number, because a caption is either
legible at a glance or it is not, and there is nothing useful between two adjacent values.

#### Caption background {#background}

How solid the box behind the text is.

A caption sits over a starfield, a station's floodlights and your own instruments. Text with
nothing behind it is unreadable against half of those — which is why broadcast captions have
always sat on a box, and why the box is not fully solid, since one you cannot see through is a
hole cut in your cockpit. The text stays fully opaque whatever you set; only the box fades.

#### Reading speed {#speed}

Characters a second. `20` is the standard's adult rate, `17` its children's rate, and `12` is
there because reading speed is the one thing about a caption that is a property of the reader
rather than of the caption.

#### Headset {#state}

Not a setting but a state, shown next to the switch — because *switched off* and *SteamVR is not
running* look identical from the outside unless something says which.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `get_headset_status`

Reports whether D47 is showing in the headset, and if not, why not. Reports only. Takes no
arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `show_in_headset`

Shows D47 in the headset, or stops showing it — the same row as the switch above, reached by
voice. It answers with the *status* rather than an acknowledgement, because switching it on is
not the same as it appearing: with no runtime installed the setting takes and nothing shows.

```json
{"type":"object","properties":{"on":{"type":"boolean","description":"True to show D47 in the headset, false to leave SteamVR alone."}},"required":["on"],"additionalProperties":false}
```

#### `move_headset_panel`

Moves the panel that is on screen one step at a time. A world-locked panel's position is not in
`settings.json` at all — it is an anchor pose in view state, written until now only by a completed
carry — so this is a delta applied to that anchor rather than a settings row. The two placement
fields that look like the obvious answer, `Drop` and `Pitch`, are read only by the head-locked
path, and the default lock is world: rows for them would move nothing you could see.

```json
{"type":"object","properties":{"direction":{"type":"string","description":"Which way. turn-left and turn-right swing the face of the panel towards that side; tilt-up leans it back to face the Commander.","enum":["left","right","up","down","nearer","further","turn-left","turn-right","tilt-up","tilt-down"]},"steps":{"type":"integer","description":"How many steps, 1 to 20. One step is 5 cm or 5 degrees. Defaults to one."}},"required":["direction"],"additionalProperties":false}
```

Up and down are the room's vertical, not the panel's own, and nearer and further run along the
floor rather than along the tilted face — otherwise bringing a panel closer would raise it at the
same time, which is one gesture doing two things.

It exists because asking to see the panel used to reach `get_headset_status`, that being the only
headset-shaped thing on the surface — so *"show the VR panel"* was answered with *"the overlays
are dark, Commander"*, which is true and is not what was asked. `set_setting` could always have
done it; nothing pointed at that.

An *overlay* application, never a scene one — that is what keeps D47 clear of anything resembling
game injection. Elite renders through OpenVR, which is why this is OpenVR and not OpenXR.

Three states, and the difference between the first two matters. `Unavailable` is no runtime
installed; `Connecting` is a runtime present but no session; `Active` is up. `VR_Init` returns
`Init_HmdNotFound` while `vrserver.exe` is already running: *SteamVR is running* and *a headset
is present* are different facts. `VR_Init` also **starts SteamVR** when it is not running, so it
is called only once a session already exists and a headset is present — checked with the process
list and `VR_IsHmdPresent`, neither of which starts anything. Recovery rebuilds rather than
repairs: a session that has gone away leaves every overlay handle stale, and a stale handle fails
forever with a plausible error.

Two quads: `com.dseelinger.d47.panel` and `com.dseelinger.d47.captions`. Apparent text size is
the texture's pixel count and the quad's width in metres *together*, so mini is a genuinely
smaller image as well as less content — drawing the full panel and hanging it nearer gives text a
third of the size.

```text
full   1024 x 640 px   at 1.10 m wide
mini     512 x 280 px   at 0.34 m wide
```

1.4 m was tried first and read as enormous — close to fifty degrees of view, so the panel filled
the middle and the cockpit was behind it rather than around it.

The panel is a second instantiation of the same view definition bound to the same view model,
rasterised offscreen into a plain pixel buffer and submitted with `SetOverlayRaw`. About 0.75 ms
a frame measured live against SteamVR, of which the rasterise is 0.30 ms, and only when something
changed.

**The trigger only.** A controller reports its grip and other buttons through the same channel,
so treating them all as a press means the grip that grabs the panel also clicks whatever the ray
was over. And the pointer is the only controller input an overlay gets: SteamVR takes the
controllers for its own laser and hands back mouse events.
`IVRSystem.GetControllerState` returns false for controllers that are connected and tracking,
silently, and an `IVRInput` action manifest was built twice in two separate projects, accepted by
SteamVR, and never went live.

**The pixels go over as raw bytes rather than as a texture.** A shared Direct3D texture was the
original route, and it left the overlays invisible in the headset while SteamVR reported them
visible — with every call returning success, the copy proven correct by test, and the rasterise
proven correct by writing a live session's first frame to `data/vr-PanelFull.png` and looking at
it. `SetOverlayRaw` hands OpenVR a buffer and lets it do the upload, which removes the graphics
device, the adapter match and the share flag along with the fault. Two things it has to get
right, both of which come out as a wrong picture rather than an error: the buffer is RGBA where
Avalonia rasterises BGRA, and its rows carry no padding, because `SetOverlayRaw` derives the
stride from the width.

Caption line breaks go after punctuation, or before a conjunction or preposition, bottom-heavy
where nothing else decides it; when those compete the syntactic break wins. Captions are never
curved: two short lines in the middle of the view have no far edges to bring closer.

</details>
