---
title: HOTAS switches
group: Acting on the game
nav_order: 136
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">A maintained toggle on your stick that means a <em>state</em> rather than a press.</p>
<section>
<h2><span class="num">1</span> The switch is a question, not a command.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Flipping a switch asks Elite whether it is already in that state, and presses a key only if it is not">
 <rect x="20" y="44" width="200" height="90" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="120" y="82" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOU FLIP IT</text>
 <text x="120" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">gear switch, down</text>
 <line x1="232" y1="88" x2="256" y2="88" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="270,88 254,80 254,96" fill="var(--accent-muted)"/>
 <rect x="284" y="44" width="250" height="90" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="409" y="82" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">IT ASKS ELITE</text>
 <text x="409" y="112" text-anchor="middle" font-size="14" fill="var(--text-muted)">“is the gear already down?”</text>
 <line x1="546" y1="76" x2="572" y2="58" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <line x1="546" y1="102" x2="572" y2="122" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <rect x="584" y="28" width="276" height="58" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="722" y="63" text-anchor="middle" font-size="15" fill="var(--text)">already down → nothing at all</text>
 <rect x="584" y="96" width="276" height="58" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="722" y="131" text-anchor="middle" font-size="15" fill="var(--text)">not down → one press, yours</text>
 <text x="440" y="198" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">Between flips it touches nothing.</text>
 <text x="440" y="230" text-anchor="middle" font-size="15" fill="var(--text-muted)">Every other way is edge-triggered: it sends a toggle on the flip and never learns what the game did.</text>
</svg>
<p class="body">Which is why an edge-triggered switch ends up upside down the first time the game changes its own mind — you dock, you relog, you lower the gear by voice — and stays that way until you notice.</p>
</section>
<section>
<h2><span class="num">2</span> Elite cannot be told what a position means.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="A maintained switch is held rather than pressed, so Elite reads it as a button held down forever">
 <rect x="20" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">A MAINTAINED SWITCH</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">is held, not pressed</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">sixteen were held on the bench</text>
 <line x1="432" y1="96" x2="448" y2="96" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="462,96 446,88 446,104" fill="var(--accent-muted)"/>
 <rect x="474" y="40" width="386" height="112" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="667" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--danger)">SO ELITE SEES</text>
 <text x="667" y="110" text-anchor="middle" font-size="15" fill="var(--text)">a button you are leaning on</text>
 <text x="667" y="134" text-anchor="middle" font-size="15" fill="var(--text)">forever</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">There is no way to say “this position means gear down” in a bindings file.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> It learns your switch by watching you walk it.</h2>
<svg viewBox="0 0 880 262" role="img" aria-label="Walking the switch through every position discovers four things that cannot be assumed">
 <text x="440" y="30" text-anchor="middle" font-size="15" fill="var(--text-muted)">“Move the switch to each position in turn, and pause at each one.”</text>
 <rect x="27" y="48" width="196" height="100" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="125" y="76" text-anchor="middle" font-size="14" font-weight="800" fill="var(--text)">HOW MANY POSITIONS</text>
 <text x="125" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">two, three and four</text>
 <text x="125" y="128" text-anchor="middle" font-size="14" fill="var(--text-muted)">all exist</text>
 <rect x="237" y="48" width="196" height="100" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="335" y="76" text-anchor="middle" font-size="14" font-weight="800" fill="var(--text)">WHICH BUTTON EACH</text>
 <text x="335" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">consecutive is a hint,</text>
 <text x="335" y="128" text-anchor="middle" font-size="14" fill="var(--text-muted)">not a rule</text>
 <rect x="447" y="48" width="196" height="100" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="545" y="76" text-anchor="middle" font-size="14" font-weight="800" fill="var(--text)">WHETHER EACH HOLDS</text>
 <text x="545" y="104" text-anchor="middle" font-size="14" font-weight="800" fill="var(--text)">ONE AT ALL</text>
 <text x="545" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">a dead detent is legal</text>
 <rect x="657" y="48" width="196" height="100" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="755" y="76" text-anchor="middle" font-size="14" font-weight="800" fill="var(--text)">WHETHER IT STAYS</text>
 <text x="755" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">a spring-return switch</text>
 <text x="755" y="128" text-anchor="middle" font-size="14" fill="var(--text-muted)">cannot mean a state</text>
 <text x="440" y="192" text-anchor="middle" font-size="16" fill="var(--text)">The pause is what tells a maintained switch from a spring-return one.</text>
 <text x="440" y="222" text-anchor="middle" font-size="15" fill="var(--text-muted)">Hold duration cannot — flips ran 407–1611 ms and deliberate presses 206–1751 ms, which overlap outright.</text>
 <text x="440" y="248" text-anchor="middle" font-size="15" fill="var(--text-muted)">After a second and a half, one is still held and the other has gone home. That is not a close call.</text>
</svg>
<p class="body">There is no list of sticks to pick from, and there never can be: Windows reports every controller as <code>HID-compliant game controller</code>, so a WinWing throttle, a Virpil base and a twenty-year-old Saitek are all the same string.</p>
</section>
<section>
<h2><span class="num">4</span> A switch that disagrees is announced, not corrected.</h2>
<svg viewBox="0 0 880 230" role="img" aria-label="The panel shows which assigned switches currently disagree with the game's state">
 <rect x="20" y="36" width="840" height="64" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="76" text-anchor="middle" font-size="18" fill="var(--text)">“gear switch: the landing gear is up”</text>
 <text x="440" y="140" text-anchor="middle" font-size="16" fill="var(--text)">A stale switch costs one extra flip — but only if you can see it coming.</text>
 <text x="440" y="176" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So the panel shows which switches disagree with the game.</text>
 <text x="440" y="210" text-anchor="middle" font-size="15" fill="var(--text-muted)">The answer real aviation reached with annunciator lights — showing the state is cheap, moving the switch is not.</text>
</svg>
<p class="body">A spare three-position toggle can name <em>pages of Directive 47's own panel</em> instead of ship actions — three detents, three pages. That one presses nothing, reads no binds and checks no foreground, so it works whether the key-injection rows are on or off.</p>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="flight-controls.html"><span class="ct">Flight and navigation →</span><span class="cd">The master switch underneath this one, and the actions it can reach.</span></a>
<a class="card" href="macros.html"><span class="ct">Macros →</span><span class="cd">The other way to author something once and reach it without a sentence.</span></a>
<a class="card" href="settings.html"><span class="ct">Settings →</span><span class="cd">Where Assign lives, and why the AI cannot reach it.</span></a>
</div>
</div>
</div></div>

## The details

A maintained toggle on your stick or throttle that means a **state** rather than a press.

Flip the gear switch down and Directive 47 asks Elite whether the gear is already down. If it is,
nothing happens at all. If it is not, it presses your own landing-gear binding once. Between
flips it touches nothing.

That one difference is the whole feature. Every other way of getting a switch into Elite is
edge-triggered: it sends a toggle on the flip and has no idea what the game was doing. So the
first time the game changes its own mind — you dock, you relog, you lower the gear by voice —
the switch is upside down and stays that way until you notice.

### Why your switches cannot just be bound in Elite

Because a maintained switch is *held*, not pressed. On the bench here, **sixteen buttons were
held down with nothing being touched**. Elite sees a switch left in the "on" position as a button
you are leaning on forever, and there is no way to express "this position means gear down" in a
bindings file.

### Turning it on

Two rows, and the second only appears once the first is on:

- **Let D47 press keys in Elite** — the master switch for all key injection.
- **Let a HOTAS switch operate the ship** — this feature.

Both are off until you turn them on, and neither can be turned on by the AI. They are reachable
from the settings panel, from the settings hotkey, and by saying one of the fixed phrases:

> "let my switches operate the ship"
> "stop reading my switches"

Keys are only ever sent while Elite is the window in front.

### Assigning a switch

There is no list of sticks to pick from, and there never can be. Windows reports **every**
controller as `HID-compliant game controller` — a WinWing throttle, a Virpil base and a
twenty-year-old Saitek are all the same string. Nothing exists for a device profile to be keyed
to, so Directive 47 learns your switch by watching you use it.

Press **Assign** and you are asked to:

> Move the switch to each position in turn, and pause at each one.

Walk it all the way through. That instruction is doing four jobs at once:

| What the walk discovers | Why it cannot be assumed |
|---|---|
| How many positions the switch has | Two, three and four-position switches all exist |
| Which button index each position holds | Indices were consecutive on the bench; that is a hint, not a rule |
| Whether every position holds a button *at all* | A centre detent that holds nothing is a legitimate design |
| Whether the switch stays where you put it | A spring-return switch cannot mean a state |

The pause is what tells a maintained switch from a spring-return one. Hold duration cannot: the
measured ranges overlap outright — switch flips ran 407–1611 ms and deliberate button presses ran
206–1751 ms. But after a second and a half, a maintained switch is still held and a spring-return
one has gone home, and that is not a close call.

#### When a walk is declined

Directive 47 declines rather than guessing, and says which of these it was:

- **"That is a hat, not a switch."** A hat returns to centre, so it can only mean a press.
- **"That control went back on its own."** Spring-return, or a push button.
- **"Button 5 moved but was never held still long enough to be a position."** You walked past a
  position. Try again, pausing at each.
- **"Buttons 8 and 9 are held together at one position."** Not a switch this can read.

Every walk — including a declined one — can be exported as a capture report. Most of the devices
this has to work on are ones nobody here will ever hold, so if your switch is declined and you
think it should not have been, that file is the whole of the evidence.

#### Assigning what each position means

Each captured position gets an action and a state. Only the actions Elite **reports the state
of** can be assigned, because those are the only ones that can be asked *are you already there*:

landing gear, ship lights, cargo scoop, hardpoints, flight assist, silent running, analysis mode,
SRV turret view, SRV handbrake, SRV drive assist.

A position may also mean nothing, which is what the centre of a three-position switch usually is.

#### A position may name a page of Directive 47's own panel instead

A spare three-position toggle with nothing bound to it can flip the Transcript between
Conversation, Technical and the log file — three detents, three pages. Each position gets a
**page** rather than an action, chosen from the same list the spoken route reads: every page any
surface shows, so there is nothing here that cannot also be said.

The same rule applies, and it fits better here than where it was invented. The flip is the
question, *are you already there* is asked first — and for Directive 47's own panel the answer is
exact rather than read out of `Status.json` — and between flips nothing is touched. Flip to
Technical while the panel is already on Technical and nothing happens at all. Flip to it while
the panel is on the checklist and the panel goes to Technical, on both surfaces.

Two things are different, because the target is:

- **It is not behind *Let a HOTAS switch operate the ship*, nor behind key injection.** Those
  rows exist because a switch that reconciles the ship reaches the keyboard. A switch that
  changes which page is drawn presses nothing, reads no binds and checks no foreground, so it
  works whether those rows are on or off.
- **It is never paused.** Nothing else drives the panel behind Directive 47's back, so there is
  nothing to desync with and nothing to stop reconciling for.

A position names an action *or* a page, never both. A page the panel does not have — a key
mistyped in the file, say — is reported on the row by name and never moved to. And a switch
sitting on a page the panel is not showing is annunciated like any other stale switch:

> transcript switch: the panel is on Conversation

**Spring-return controls and hats are still declined**, even for a page. The walk cannot know
what a switch will be assigned to, a momentary control can only ever mean a press, and saying a
page out loud already covers the press.

### When a mapping stops fitting

A mapping is stored against the device's `NonRoamableId` — never against its vendor and product
ids. This matters more than it sounds.

Turning **4x32 mode** on or off on a WinWing throttle renumbers every button on it, while leaving
the vendor and product ids completely unchanged. A mapping keyed on those would survive the
change and quietly press button 15 of a block that is no longer the same block. The non-roamable
id changes, so instead the switch **fails closed**: the row says it needs reassigning, and
nothing is pressed.

### When Elite is bound to the same button

**The most common way for this to go wrong is the one you can see, so Directive 47 looks.** If a
switch position sits on a button Elite also binds, both act on every flip — and the results look
like Directive 47 misbehaving in two different ways at once:

- where Directive 47 correctly presses **nothing**, Elite's binding toggles, so the thing moves the
  wrong way;
- where Directive 47 correctly **presses**, the two toggles cancel and nothing happens at all.

That is one setup fault wearing two disguises, and it cost an afternoon before it was understood.
The switch now says so directly:

> Elite binds this button to LandingGearToggle as well, so both act on every flip. Unbind it in
> Elite, or move this switch to a button Elite does not use.

Directive 47 will not fix it for you: **your bindings file is read-only to it, always.** Unbind the
button in Elite, or move the switch.

### When something else is driving the same switch

A virtual device is the case that cannot be seen. If SimApp Pro or Joystick Gremlin is publishing a
vJoy device, Elite's binds name *that* device and nothing connects it back to your physical switch.

So the symptom is watched instead. One unexplained reversal and Directive 47 **stops reconciling
that switch** and says so, rather than fighting whatever else is there. The panel offers a
Resume button; a restart clears it too.

A mode change is not counted as unexplained. Hardpoints retract by themselves when you enter
supercruise, and that is the game doing its job.

**And a press that simply does not take is now reported too.** Watching for a reversal only catches
something that fights back *after* the state arrives; two toggles cancelling means the state never
arrives at all, and that used to be silent — the log said the key was sent, and nothing said the
thing had not moved. Now:

> I set the landing gear from LDG GEAR and it did not take. Something else may be bound to the same
> button.

### Switches that disagree with the game

A stale switch is harmless — it costs one extra flip — but only if you can see it coming. So the
panel and the VR surface show which assigned switches currently sit against the game's state:

> gear switch: the landing gear is up

This is the answer real aviation reached with annunciator lights rather than motorised switches,
for the same reason: showing the state is cheap and moving the switch is not.

You can also ask.

### Ask for it

> "which switches disagree"
> "check my switches"

#### `report_switches`

Reports only. It cannot assign, change, pause or clear a switch — assignment is reachable from
the panel and from nowhere else, because the AI reads untrusted text and a hostile in-game
message must not be able to remap your throttle.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Where it is stored

`data/switches.json`, beside the executable like everything else Directive 47 writes. It is
hand-editable, and it is re-read while running — a mapping edited in a text editor is live
without a restart. Anything in it that does not make sense is refused **by name, with the
reason**, and the rest of the file still loads.

```json
{
  "switches": [
    {
      "name": "gear switch",
      "deviceId": "{wgi/nrid/X8QoL-2>1[]-...-mA7IX7-Ac}",
      "device": "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 7 axes",
      "positions": [
        { "button": 8, "action": "landing_gear", "state": "on" },
        { "button": 9, "action": "landing_gear", "state": "off" }
      ]
    },
    {
      "name": "transcript switch",
      "deviceId": "{wgi/nrid/X8QoL-2>1[]-...-mA7IX7-Ac}",
      "device": "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 7 axes",
      "positions": [
        { "button": 4, "destination": "transcript.conversation" },
        { "button": 5, "destination": "transcript.technical" },
        { "button": 6, "destination": "transcript.log" }
      ]
    }
  ]
}
```

A page is its own `destination` field, never a prefix on `action` — a statement about what the
position means rather than a naming rule a rename would silently break. The keys are the ones the
panel registers: `transcript.conversation`, `transcript.technical`, `transcript.log`, and the
roots of every other tab.

### What it does not do

- **It does not read axes.** A throttle axis is not a switch and is not in scope.
- **It does not write your Elite bindings.** Those are read-only, always.
- **It does not move your switches.** Nothing can; they are lumps of metal.
- **It does not act on a switch it has just found.** On startup, and after a device reconnects,
  Directive 47 learns where the switch is sitting and presses nothing. The next flip is the next
  question.
