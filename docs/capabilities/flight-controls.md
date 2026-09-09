---
title: Flight and navigation
group: Acting on the game
nav_order: 128
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and the class decides behaviour, not just
  appearance. HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under
  that class would silently become what the in-app panel draws on this page. The docs site styles
  the two identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to flying the ship by voice.</p>
<section>
<h2><span class="num">1</span> Turn on the master switch.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Flight and navigation">
 <rect x="20" y="16" width="840" height="212" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Flight and navigation</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Let D47 press keys in Elite</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">on</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Let a HOTAS switch operate the ship</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text-muted)">off</text>
 <text x="44" y="222" font-size="15" fill="var(--text-muted)">Off out of the box. The AI cannot turn this on for you.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Say what you want the ship to do.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">gear up</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"lights on" — "hardpoints out" — "scoop away" — "silent running"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">D47 presses your own Elite binding, once.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="Elite must be the window in front.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Elite must be the window in front.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Keys are never sent into another window. Alt-tab away and nothing is pressed at all.</text>
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
<p class="intro">The parts of your ship that are switches — gear, lights, scoop, hardpoints, the frame shift drive.</p>
<section>
<h2><span class="num">1</span> It presses your keys, not its own.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="Directive 47 reads the bindings you already use and sends those keys to Elite">
 <rect x="20" y="44" width="250" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOUR BINDINGS</text>
 <text x="145" y="114" text-anchor="middle" font-size="15" fill="var(--text-muted)">the keys you already use</text>
 <line x1="282" y1="94" x2="318" y2="94" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="332,94 316,86 316,102" fill="var(--accent-muted)"/>
 <rect x="345" y="44" width="250" height="100" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="470" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">IT SENDS THOSE</text>
 <text x="470" y="114" text-anchor="middle" font-size="15" fill="var(--text-muted)">“gear down” presses yours</text>
 <line x1="607" y1="94" x2="643" y2="94" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="657,94 641,86 641,102" fill="var(--accent-muted)"/>
 <rect x="670" y="44" width="190" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="765" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">ELITE</text>
 <text x="765" y="114" text-anchor="middle" font-size="15" fill="var(--text-muted)">does the thing</text>
 <text x="440" y="190" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">Directive 47 has no keys of its own.</text>
 <text x="440" y="222" text-anchor="middle" font-size="15" fill="var(--text-muted)">Change your controls in the game and nothing here needs changing. Your bindings are only ever read.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Two switches stand between it and your keyboard.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="Key pressing is off by default and unreachable by the AI, and keys go out only while Elite is in front">
 <rect x="20" y="36" width="410" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="225" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">LET DIRECTIVE 47 PRESS KEYS</text>
 <text x="225" y="102" text-anchor="middle" font-size="15" fill="var(--text-muted)">off until you switch it on</text>
 <text x="225" y="128" text-anchor="middle" font-size="14" fill="var(--text-muted)">panel, hotkey or voice — never the AI</text>
 <rect x="460" y="36" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="72" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">AND ONLY WHILE ELITE IS IN FRONT</text>
 <text x="660" y="102" text-anchor="middle" font-size="15" fill="var(--text-muted)">alt-tab mid-command and the rest</text>
 <text x="660" y="128" text-anchor="middle" font-size="15" fill="var(--text-muted)">is dropped, not typed into a browser</text>
 <text x="440" y="192" text-anchor="middle" font-size="16" fill="var(--text)">A companion that could grant itself your keyboard on the strength of a message</text>
 <text x="440" y="220" text-anchor="middle" font-size="16" fill="var(--text)">another Commander sent you is not a companion.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> When it says no, it says which no.</h2>
<svg viewBox="0 0 880 256" role="img" aria-label="An action can be inert in the current mode, or bound to a joystick with no key to press">
 <rect x="20" y="40" width="410" height="124" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="225" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text-muted)">IT DOES NOTHING IN THIS MODE</text>
 <text x="225" y="110" text-anchor="middle" font-size="15" fill="var(--text)">“Landing gear does nothing</text>
 <text x="225" y="134" text-anchor="middle" font-size="15" fill="var(--text)">while you are in supercruise.”</text>
 <rect x="460" y="40" width="400" height="124" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">IT IS ON YOUR JOYSTICK</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text)">“Bind it to a key or a mouse</text>
 <text x="660" y="134" text-anchor="middle" font-size="15" fill="var(--text)">button and I can.”</text>
 <text x="440" y="206" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">It would rather refuse than say “done” over a key that did nothing.</text>
 <text x="440" y="238" text-anchor="middle" font-size="15" fill="var(--text-muted)">The joystick one is the common case, not the rare one — Elite gives every action a second slot.</text>
</svg>
<p class="body">It knows which mode you are in because the game says so with every question you ask: the system, the ship, and whether you are in normal space, in supercruise, in hyperspace, docked, landed or on foot. When you are not docked it says that outright, so a station you left a minute ago cannot be answered about as though you were still on the pad. Before the game has said anything at all it claims neither, because a guess about where you are is worse than a shrug.</p>
</section>
<section>
<h2><span class="num">4</span> Your weapons are deliberately not on the list.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="Everything reachable is listed, and firing weapons is excluded on purpose">
 <rect x="20" y="40" width="520" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="280" y="76" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">WHAT IT CAN REACH</text>
 <text x="280" y="106" text-anchor="middle" font-size="15" fill="var(--text-muted)">gear · lights · scoop · hardpoints · FSD</text>
 <text x="280" y="132" text-anchor="middle" font-size="15" fill="var(--text-muted)">supercruise · jump · flight assist · boost</text>
 <rect x="580" y="40" width="280" height="110" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="720" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--danger)">NOT YOUR WEAPONS</text>
 <text x="720" y="116" text-anchor="middle" font-size="14" fill="var(--text-muted)">deliberately, and for good</text>
 <text x="440" y="192" text-anchor="middle" font-size="16" fill="var(--text)">Directive 47 reads text from the galaxy that anyone can write.</text>
 <text x="440" y="220" text-anchor="middle" font-size="16" fill="var(--text-muted)">One that can be talked into opening fire is a different kind of problem.</text>
</svg>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="ship-systems.html"><span class="ct">Ship systems →</span><span class="cd">Pips, heat sinks, and the one thing here that is not a switch at all.</span></a>
<a class="card" href="panels.html"><span class="ct">Panels →</span><span class="cd">Walking the cockpit panels, and the two maps Elite ships unbound.</span></a>
<a class="card" href="switches.html"><span class="ct">HOTAS switches →</span><span class="cd">Driving all of this from the stick instead of from your voice.</span></a>
</div>
</div>
</div></div>

## The details

Flies the parts of your ship that are switches: gear, lights, scoop, hardpoints and the frame
shift drive.

### Ask for it

> "gear down"
> "retract the hardpoints"
> "lights off"
> "take us to supercruise"

### It presses your keys, not its own

Directive 47 has no keys of its own. It reads the bindings you already use and sends those, so
"gear down" presses whatever *you* have landing gear bound to. Change your controls in the game
and nothing here needs changing.

Your bindings are only ever read. Directive 47 never writes to them.

### You have to turn it on

**Let Directive 47 press keys in Elite** is off until you switch it on, and it is one of the
settings the model cannot reach — it can be changed from the panel, a hotkey, or by voice
through the keyword router, but never by asking the AI nicely. A companion that could grant
itself your keyboard on the strength of a message another Commander sent you is not a companion.

Keys go out only while Elite is the window in front. Alt-tab to a browser mid-command and the
rest of the sequence is dropped rather than typed into it.

And only while you are actually **in** the game. Elite sitting at its main menu is running and in
front, but a keystroke aimed at a ship would land in a menu — so everything that presses keys
stays off until you go online, says so if asked, and comes alive the moment you load in. Quitting
to the menu mid-sequence stops the sequence the same way alt-tabbing does.

### Why it sometimes says no

Two things stop an action, and it tells you which:

```text
Landing gear does nothing while you are in supercruise.
```

The cockpit is not one mode. Gear, scoop and hardpoints are inert in supercruise, so Directive 47
would rather refuse than say "done" over a key that did nothing.

```text
Hardpoints is on your joystick, which I have no way to press. Bind it to a key or a mouse
button and I can.
```

This is the common one, not the rare one. If you fly on a stick, most of your ship is on the
stick, and there is no key there to press. Bind the handful you want by voice to a key as well —
Elite gives every action a second slot for exactly this — and they start working.

An action you have left unbound entirely says so too, rather than failing as silence.

### What it can reach

Landing gear, ship lights, cargo scoop, hardpoints, the frame shift drive, supercruise, the
hyperspace jump, flight assist, throttle to zero, and boost.

Firing your weapons is deliberately not on this list. Directive 47 reads text from the galaxy
that anyone can write, and a companion that can be talked into opening fire is a different kind
of problem from one that can be talked into turning the lights on.

### Engage

Say **engage** and you jump. Say **supercruise** and you supercruise.

**The jump has two keys, and Directive 47 will use either.** Elite binds a dedicated `Hyperspace`
action and a combined frame shift drive key — the J key, on a stock keyboard layout — that jumps
when you have a system targeted and drops you to supercruise when you do not. If your dedicated
jump is on the stick where there is no key to press, Directive 47 presses the combined key
instead. It only does this with a route ahead of you: with nothing targeted that key would put you
in supercruise rather than jump, so it says *nothing is targeted* and asks you to set a course.
With neither key pressable it names both, so you know which one to bind. *Separate and
supercruise* never reaches for the combined key — with a route plotted it would jump, which is the
opposite of what you asked for.

These are whole sentences rather than keywords, which matters more than it sounds. The word
*engage* already sits inside three things you could say — *engage supercruise*, *engage boost*,
*engage the frame shift drive* — so a companion that reacted to the word anywhere in a sentence
would jump when you asked it to boost. Directive 47 matches what you said, all of it, or nothing.
"Should I engage?" is a question, and it is answered rather than obeyed.

### Take us out

Say **take us out** while docked and Directive 47 walks the left panel to the launch button.

**This one is a menu walk, not a key press, and it is the least reliable thing here.** Elite has
no launch binding at all — launching is a button on a menu, and Frontier ships no control for it —
so instead of pressing your own key, Directive 47 opens the left panel, presses **back** to leave
it, and then **down** and **select** on the station menu in the centre, which is where *Auto
Launch* lives. The left panel is only a way of getting to that menu.

It waits for the panel to actually close before pressing anything else, rather than counting on a
delay: those two keys sent while the panel is still going away arrive with no menu to receive them,
which is exactly how this failed the first time it was flown.

**You hear it accept before it starts.** Directive 47 says *"Taking us out."* the moment the
command is accepted — before the first key — and then tells you how it went when it knows:
*"We are away."* The two sentences do two different jobs. It used to say only the second, and it
used to say *"Taking us out"* for it, spoken after the ship had already left the pad; the whole of
the walk and up to thirty seconds of watching for the pad clamps to release happened in silence,
which is a long time to sit wondering whether you were heard.

A refusal is still one sentence and it comes straight away. Nothing is acknowledged on a road where
no work follows — being told *"Taking us out"* and then *"you are not docked"* would be worse than
either on its own.

Everything around the walk is checked against the game rather than assumed. It refuses unless you
are docked. It confirms the panel is actually open before sending a single direction key, because
those keys typed into a cockpit instead of a panel are steering inputs. And it watches for your
ship leaving the pad before it claims anything:

```
I walked the left panel and we are still docked, so assume it did not work.
The panel may not have been where I expected it.
```

It has its own switch, separate from the others, because it is the one here that works by
guesswork.

### Separate

Say **get clear and jump** and Directive 47 goes to full throttle, boosts until the mass lock
breaks, and jumps the moment it does. Any pairing works — *get clear*, *get us clear*, *separate* or
*boost*, ending in *jump*, *engage* or *hyperspace* — because the one word you happen to reach for
should not be the difference between the manoeuvre and a jump key pressed against a mass lock.
**Separate and supercruise** is the same thing ending in supercruise.

Both answer before the first key goes in — *"Acknowledged. Preparing for hyperspace jump."*, or
supercruise — for the reason *take us out* does: the loop below can run for a minute, and silence
for that long is indistinguishable from not having been heard. It names the manoeuvre you asked
for rather than the mechanism, and when it is done it says the drive engaged and leaves it there.
How many boosts it took to break the lock is Directive 47's business, not yours.

It watches the game rather than counting seconds. Elite reports mass lock continuously, so instead
of boosting a fixed number of times and hoping, Directive 47 boosts, looks, and stops the instant
the lock clears. If you were never mass locked it does not boost at all.

**The gear comes up first if it is down**, because deployed gear caps the throttle, stops the
boost and keeps the drive from engaging — every other key in the sequence would go in correctly
and the ship would stay where it was. If the gear is down and you have no binding to raise it, the
manoeuvre refuses before pressing anything rather than pretending.

**Then all four pips to engines**, which is the one thing it can do about the engine capacitor:
Elite does not publish it. The status file says how the distributor is *configured* and never how
much charge is *in* it, so the capacitor cannot be waited for. What it does instead is **press
boost once a second** — a boost with too little charge is a no-op, so an early press costs nothing
and a late one costs you a second beside a station. Pressing every second catches the charge as
soon as there is one.

**The pips stay in engines afterwards.** You asked to leave; what follows a separation is travel.

**If you were not mass locked to begin with, none of that happens** — no pips moved, no boosts.
There is nothing to fight, and where you keep your pips is not this command's to spend when it
does not need them. The gear still comes up, the throttle still goes forward, and the drive
engages.

If you have no binding for pips to engines it carries on without them and boosts anyway. That is
the only part of the sequence that is optional; every other key is resolved before anything moves,
and a missing one stops the attempt before the ship does.

**It gives up rather than boosting forever**, after a minute, and says so:

```
Still mass locked after 60 seconds; you may be too close to the station. I have not engaged.
```

Neither of those endings presses the engage key. Engaging while still mass locked is the thing the
limit exists to prevent.

The two have separate switches because they fail differently in the game: a jump needs a
destination locked in your nav panel and refuses without one, where supercruise needs nothing.

### Set a course and take us out

Say **set a course** after a nearest-first commodity search — the Community Goal search names one
station in full — and Directive 47 plots to the system it just named, the same way it would if you
had asked it to plot there by name. Say **set a course and take us out** and it plots and then
walks the left panel to the launch button, exactly as *take us out* does.

Both act on whatever the search actually found, never a name you say: there is nothing to
misrecognise, because nothing is spoken back into it. With nothing found yet, neither phrase has
anywhere to route, so the sentence is answered as an ordinary question instead of a course to
nowhere.

Plotting is best-effort by design — even on its own, "set a course" can put the system on the
clipboard and still not manage to drive the galaxy map itself, and it still answers as a plot
that was tried. The compound phrase goes on to launch on that same best-effort basis: only a
clipboard failure, which is the one thing `plot_course` treats as an actual error, stops it short
of the pad.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `control_flight`

Operate the landing gear, lights, cargo scoop, hardpoints and the frame shift drive. Only the
actions listed as reachable in the current game state will work; anything else comes back with
the reason it did not.

```json
{"type":"object","properties":{"action":{"type":"string","description":"Which action to perform.","enum":["landing_gear","lights","cargo_scoop","hardpoints","frame_shift_drive","supercruise","hyperspace","flight_assist","throttle_zero","boost"]},"state":{"type":"string","description":"What to leave it in. Elite binds a single toggle, so asking for \u0022on\u0022 or \u0022off\u0022 checks the game\u0027s own report first and does nothing if it is already there. Defaults to toggling.","enum":["on","off","toggle"]}},"required":["action"],"additionalProperties":false}
```

The enum is the whole group and never changes, which is what keeps the schema byte-identical
across turns and prompt caching alive. What is reachable *now* rides in the live game-state
block below the cache breakpoint, and the handler enforces it regardless of what the model asked
for.

That block states the flight mode in words — *in normal space*, *in supercruise*, *in hyperspace*,
*docked*, *landed*, *on foot* — and in the first three it adds *not docked*, because the mode
alone contradicts nothing an older turn in the transcript claimed. Its header says the same thing
about itself: it describes the situation now, and it beats anything said earlier in the
conversation. With the flight mode still unknown it makes neither claim.

#### `ship_command`

Compound ship commands: leave the pad, break a mass lock and engage, or set a course to what a
nearest-first search just found and leave the pad. Spoken only — the Commander reaches these by
voice or from the panel.

```json
{"type":"object","properties":{"command":{"type":"string","description":"Which command to run.","enum":["take_us_out","separate_and_engage","separate_and_supercruise","set_course_and_take_us_out"]}},"required":["command"],"additionalProperties":false}
```

**Protected, so the model never sees this one.** It is registered, and it is left out of the
advertisement entirely — which means it costs no tool-surface bytes, and that it is reachable
only from the model-free keyword router and the panel. Both facts are deliberate. A spoken command
that has to wait for a model round trip is a command given at the wrong moment, and the surface is
close enough to its ceiling that a tool nobody needs advertised should not be paying for a place
there.

Each of the first three has its own settings row, and every one of them is gated by *let D47 press
keys in Elite* as well, so a Commander who has not allowed key injection at all has not allowed
these. **Set a course and take us out** reads the switch that guards taking us out, since it wraps
that exact action — it is two tools run in order, `plot_course` and then the same launch, rather
than a key sequence of its own.

</details>
