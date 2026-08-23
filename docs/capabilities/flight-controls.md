---
title: Flight and navigation
group: Acting on the game
nav_order: 129
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">The parts of your ship that are switches — gear, lights, scoop, hardpoints, the frame shift drive.</p>
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

</details>
