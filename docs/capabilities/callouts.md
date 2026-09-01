---
title: Callouts
group: Voice
nav_order: 124
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
<p class="lede">Three steps to being told what you want and nothing else.</p>
<section>
<h2><span class="num">1</span> Turn callouts on, then pick the ones you want.</h2>
<svg viewBox="0 0 880 254" role="img" aria-label="The master callouts toggle above a list of individual callout toggles">
 <rect x="20" y="20" width="840" height="60" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="46" y="57" font-size="17" font-weight="700" fill="var(--text)">Callouts</text>
 <rect x="746" y="35" width="68" height="30" rx="15" fill="var(--accent)"/>
 <circle cx="799" cy="50" r="11" fill="var(--background)"/>
 <rect x="60" y="96" width="800" height="48" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="86" y="126" font-size="16" fill="var(--text)">Arrival</text>
 <rect x="756" y="107" width="60" height="26" rx="13" fill="var(--accent)"/>
 <circle cx="803" cy="120" r="9" fill="var(--background)"/>
 <rect x="60" y="156" width="800" height="48" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="86" y="186" font-size="16" fill="var(--text)">Limpets</text>
 <rect x="756" y="167" width="60" height="26" rx="13" fill="var(--border)"/>
 <circle cx="769" cy="180" r="9" fill="var(--surface)"/>
 <text x="60" y="238" font-size="15" fill="var(--text-muted)">The individual rows are not there until the master one is on.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Or just say it.</h2>
<svg viewBox="0 0 880 168" role="img" aria-label="Spoken phrases that turn callouts on and off">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">stop telling me about limpets</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"be quiet about arrivals" — "tell me about limpets again" — "no more callouts"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Each one names a row. Nothing here needs the panel.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="A callout that never fires is usually a callout whose own row is off">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Silence usually means its own row is off.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">The master switch being on is not enough. Check the row for the one you are missing.</text>
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
<p class="lede">The things Directive 47 says without being asked, and what earns the right to interrupt.</p>
<section>
<h2><span class="num">1</span> NPCs announce themselves. It listens.</h2>
<svg viewBox="0 0 880 246" role="img" aria-label="An attack warning arrives a median of six seconds before the first shot and is right 88% of the time">
 <rect x="20" y="44" width="300" height="96" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="170" y="80" text-anchor="middle" font-size="16" fill="var(--text)">“Pirate lining up an</text>
 <text x="170" y="104" text-anchor="middle" font-size="16" fill="var(--text)">interdiction. Boost or</text>
 <text x="170" y="128" text-anchor="middle" font-size="16" fill="var(--text)">high-wake now.”</text>
 <text x="425" y="72" text-anchor="middle" font-size="16" font-weight="700" fill="var(--accent)">a median of six seconds</text>
 <line x1="336" y1="96" x2="500" y2="96" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="514,96 498,88 498,104" fill="var(--accent-muted)"/>
 <rect x="530" y="44" width="330" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="695" y="88" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">THE FIRST SHOT</text>
 <text x="695" y="118" text-anchor="middle" font-size="14" fill="var(--text-muted)">and the line was right 88% of the time</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">They say what they are about to do before they do it, and there is still time to act.</text>
 <text x="440" y="226" text-anchor="middle" font-size="15" fill="var(--text-muted)">A cargo demand gives eight seconds and is right 67% — each situation gets its own sentence and sound.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> It matches on the message id, never on the words.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Matching on hostile-sounding text fires thousands of false alarms, while matching on Elite's message ids does not">
 <rect x="20" y="40" width="400" height="124" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--danger)">MATCHING ON THE WORDS</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text)">2,399 firings to catch</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text)">30 real attacks</text>
 <rect x="460" y="40" width="400" height="124" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">MATCHING ON THE ID</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text)">a fixed list, NPC chatter only</text>
 <text x="660" y="134" text-anchor="middle" font-size="14" fill="var(--text-muted)">and nothing the message says is</text>
 <text x="660" y="156" text-anchor="middle" font-size="14" fill="var(--text-muted)">repeated, shown, or sent to a model</text>
 <text x="440" y="210" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">A hundred false alarms per real event is a warning you switch off within the hour — and then do not have when it matters.</text>
 <text x="440" y="242" text-anchor="middle" font-size="15" fill="var(--text-muted)">In-game chat is written by anyone in range, so matching on the text is matching on a string somebody else chose.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Urgent cuts in. Everything else waits its turn.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="Danger and fuel speak over whatever is being said; routine lines like a core asteroid wait">
 <rect x="20" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">URGENT CUTS IN</text>
 <text x="220" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">danger and fuel</text>
 <text x="220" y="136" text-anchor="middle" font-size="14" fill="var(--text-muted)">over whatever is being said</text>
 <rect x="460" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="80" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">ROUTINE WAITS ITS TURN</text>
 <text x="660" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">a core asteroid is exciting</text>
 <text x="660" y="136" text-anchor="middle" font-size="14" fill="var(--text-muted)">and is not a safety matter</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">Announcing a core across a hull warning would be the priority exactly backwards.</text>
</svg>
<p class="body">A warning arriving after Directive 47 has finished reading you a commodity list has arrived too late to be one. And a thing is said <em>once</em> as it happens: your shields going down is news; your shields still being down half a second later is not.</p>
</section>
<section>
<h2><span class="num">4</span> The model may ask what it is watching for. It may not switch one off.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="Warnings are readable by the model but only changeable from the panel, a key or a spoken phrase">
 <rect x="20" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">IT CAN BE ASKED</text>
 <text x="220" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">what it is watching for</text>
 <rect x="460" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="660" y="80" text-anchor="middle" font-size="15" font-weight="800" fill="var(--danger)">IT CANNOT SWITCH ONE OFF</text>
 <text x="660" y="112" text-anchor="middle" font-size="14" fill="var(--text-muted)">not by anything the model calls</text>
 <text x="440" y="192" text-anchor="middle" font-size="16" fill="var(--text)">Directive 47 reads in-game messages from anyone in range.</text>
 <text x="440" y="220" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">A model that could disable the interdiction warning could be told to by the pirate.</text>
</svg>
<p class="body">One measured refusal worth knowing: Directive 47 never repeats Elite's own <code>Material Content</code> grade while prospecting. Across 1,633 prospects, <code>Low</code> and <code>High</code> rocks have the same distribution — and <strong>45% of the rocks holding a material at 40% or more are graded <code>Low</code></strong>. Passing that on would fly you past the best rock in the cluster.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="audio.html"><span class="ct">Audio →</span><span class="cd">The alert category these ride, and what everything else ducks under.</span></a>
<a class="card" href="settings.html"><span class="ct">Settings →</span><span class="cd">Every switch on this page, and what protected means for them.</span></a>
</div>
</div>
</div></div>

## The details

What Directive 47 says without being asked: danger, fuel, route progress, arrivals, material
milestones, and an attack somebody has announced but not yet made.

All of it comes from the files Elite already writes, as they change. Nothing here waits on the
language model, so a warning arrives when the thing happens rather than after something has
finished thinking about it — which for an interdiction is after it is over.

### Ask for it

> "what are you watching for"
> "stop calling things out"
> "start calling things out"

```text
I speak up about:
  danger: on
  announced-attack: on
  fuel: on
  route: on
  long-jump: on
  arrival: on
  materials: on
  rival-territory: on
Route progress every 3 jumps.
Home system is Shinrarta Dezhra.
```

### What it speaks up about

#### Danger {#danger}

Interdiction, shields down, hull damage, overheating, and a full cargo hold.

Said once as it happens rather than repeatedly while it lasts — your shields going down is news;
your shields still being down half a second later is not. Submitting to an interdiction was your
decision and is not read back to you as an emergency.

An urgent warning cuts in rather than waiting its turn. A warning that arrives after Directive 47
has finished reading you a commodity list has arrived too late to be one.

**Every urgent one is preceded by an alarm**, so you know what kind of trouble it is before the
sentence has finished arriving. There are two, and which you hear tells you where to look:

| Sound | What it means | The answer |
|---|---|---|
| Falling three-note tumble | Somebody is hurting you — shot at, shields gone, hull opened | Fight, run, or high-wake |
| Three pulses on one note | The ship is cooking itself | The throttle, not the trigger |

Being *pulled out of supercruise* uses the same sound as the warning that one was coming, because
it is the same situation a moment later.

The lines that are not emergencies stay quiet. A full cargo hold and the rebuy screen are worth
saying and are not worth an alarm, so neither gets one.

**Two alarms of the same kind seconds apart sound once.** A pirate announcing an interdiction and
then interdicting you is two warnings, both worth saying — but only one of them is worth a noise,
because by the second one you are already looking. The words still come; the sound does not repeat.

#### Announced attacks {#announced-attack}

NPCs say what they are about to do before they do it. Directive 47 listens for that and tells you
while there is still time to act:

```text
Pirate lining up an interdiction. Boost or high-wake now.
```

Across 912 real journals, that line came a **median of six seconds** before the first shot and was
right **88%** of the time. A pirate demanding cargo gives eight seconds and is right 67%. Each
situation gets its own sentence and its own sound, so you can tell an interdiction from a cargo
demand from somebody who is here for you before the sentence has finished.

**A hitman who has found you warns too**, and shares the bounty hunter's sound — because the answer
is the same one, and cargo will not buy either of them off. That was measured the same way as the
rest: the family that fires when a hunter spots you is followed by an attack **7 times out of 7**,
the strongest signal in the whole corpus.

##### Being hunted, which is not an emergency {#hunted}

Two other hitman families are a hunter *talking about you* rather than closing on you — the one that
produced *"the eagle is in the nest"* is followed by an attack only **15%** of the time. Warning you
about those would be crying wolf six times in seven, so Directive 47 does not.

It does not ignore them either. Being hunted is a situation rather than an event, so you get a
remark on it in your core's own voice — no alarm, never interrupting, and once every ten minutes
however much chatter arrives. It will not tell you why somebody is hunting you: the journal does not
say, and inventing a reason is the one thing it must not do.

**Directive 47 matches on Elite's message ids, never on the words.** That is not tidiness — it is
the difference between a useful warning and an unusable one. The obvious approach, warning about
anything that *sounds* hostile, was measured: one such message fires 2,399 times to catch 30 real
attacks, and another is wrong 48 times out of 48. A hundred false alarms per real event is a
warning you switch off within the hour and then do not have when it matters.

It is also what keeps it safe. In-game chat can be written by anyone in range, and matching on the
text would mean matching on a string somebody else chose. The ids come from a fixed list, only NPC
chatter is considered, and **nothing the message says is repeated, shown, or passed to the language
model** — the spoken line is chosen by which id arrived and is otherwise a constant.

#### Fuel and range {#fuel}

Low fuel is the easy half. The half that matters is this one:

```text
Route warning. Hyades Sector DB-X d1-112 is class T and cannot be scooped, and the jump beyond
it is 61.2 light years against a maximum range of 52.3. Replot before you jump.
```

That is not a low-fuel warning — your tank can be nearly full when it becomes true — and without
it the first you know is when you are parked at a brown dwarf with no way out.

Every number in it came from your own game: the route and star classes from the route file, your
jump range from your ship's loadout, your fuel from the status file, and how much you actually
burn per jump averaged from the jumps you have already made this session. Nothing is looked up
anywhere.

#### Route progress {#route}

Jumps remaining, what is next, and what is coming:

```text
14 jumps remaining. Next is Wredguia WD-K d8-30, scoopable.
Ahead on the route: Praea Euq QI-T d3-3 (a neutron star).
```

A hazard on the *very next* jump is always mentioned, whether or not it was a reporting jump.
Arriving unprepared at a neutron star is the thing this exists to prevent, and every-five-jumps
would miss it four times out of five.

Whether a star can be scooped is worked out properly rather than from the first letter of its
class. The KGBFOAM rule gets two cases wrong in opposite directions, and both matter: Herbig AeBe
stars start with A and cannot be scooped, while orange giants carry a suffix and can. A class
Directive 47 does not recognise is called unknown rather than unscoopable — routing you around a
star that would have refuelled you is its own kind of harm.

#### Long jumps {#long-jump}

A little conversation during a longer-than-usual jump. It starts once you are actually in
hyperspace, not when the drive begins charging — throttle up and cancel and you will hear
nothing.

#### Arrivals {#arrival}

Your home system, where your carrier is, ships you have stored where you have just arrived, and
stations that offer engineering.

**There is no built-in list of which engineer lives where.** That kind of list goes stale every
game update, and inventing one is exactly the confident wrong answer Directive 47 is built not to
give. Engineering is recognised from what the station itself advertises when you dock — which
also means it keeps working when a new engineer is added.

#### Material milestones {#materials}

Your first unit of a material, then a quarter, half and three-quarters full, a running count
after that, and full.

Start Directive 47 after Elite and it catches up on the session so far first, so materials you
already had do not all announce themselves as firsts.

Elite never reports how much of a material you can carry — the game simply stops accepting more —
so the percentages come from a table of material grades built from the community-maintained id
list that Coriolis and EDEngineer use, rather than from anyone's memory. A material too new for
that table still announces your first unit; the percentages stay quiet rather than being counted
against a number nobody checked.

#### Rival Power territory {#rival-territory}

If you fly for a Power, Directive 47 tells you when you drop into normal space somewhere another
Power controls:

```text
Yuri Grom controls this system, and you fly for Edmund Mahon. You are exposed here.
```

Said once as you enter the condition, then silent for as long as it lasts. And **explained once
per day, full stop** — across restarts and whichever core is aboard, remembered in
`view-state.json`. Every exposure after the first that day, whoever controls the space, is four
words rather than the lecture:

```text
Hostile territory. Be on guard.
```

It began as once per Power per session, and a day of re-launching proved the fourth hearing was
not information either. One name is trimmed for the ear: the journal writes *A. Lavigny-Duval*,
and a voice reading "A." is reading punctuation, so she is spoken as *Lavigny-Duval*.

It waits until you are
in normal space rather than saying it as you arrive, because arriving happens in supercruise and
nothing can reach you there — that is measured, not assumed: across every Power security contact in
912 journals, none of them happened in supercruise and two thirds happened in normal space.

It also **stands down for anything dangerous**. A remark about enemy territory arriving as somebody
opens fire is worse than silence, so if you are already being shot at, interdicted, or have just
been told an attack is coming, this one is dropped rather than delivered late.

Your pledge is followed as it changes rather than read once at startup, so defecting does not leave
Directive 47 calling your new Power's space hostile.

**What this is not.** What you actually want is a warning when a Power Security ship shows up in
your contacts panel — and no third-party app can see that. There is no event for a ship appearing,
no file listing your contacts, and every signal about another ship requires it to have already done
something to you. The message a Power's security sends does exist and does name the Power, but it
arrives about a second before the shot, which is a caption rather than a warning. So this says the
thing it actually knows — that where you are is hostile — instead of implying somebody is close.

#### Checklist changes {#checklist}

Two moments from your checklist, both of which happen while you are doing something else.

**A plan item the journal has changed its mind about.** Selling the engineered multi-cannon
un-completes the item that was tracking it, and Directive 47 says so — *once*, because the new
verdict is written down as it is announced:

```text
"Grade 5 Overcharged on Hardpoint 1" is no longer done. Nothing is fitted in Hardpoint 1.
```

A computed tick going backwards is information rather than a glitch to hide. You would otherwise
find out by reading a list that had quietly changed under you.

**The last unit a plan needed.** Netted across every live plan, because storage caps are shared and
a shopping trip is a trip for everything:

```text
That is the last Cadmium your plans needed. 12 of 12.
```

Switching this off silences both. It does not freeze the list — the verdicts stay up to date and the
panel keeps showing them; a setting that did two things, one of them invisible, would be the wrong
setting.

#### High grade emissions {#emissions}

When you arrive somewhere that could be running High Grade Emissions, which grade 5 materials would
be in them:

```
Shinrarta Dezhra could be running high grade emissions for Core Dynamics Composites,
Proprietary Composites and Pharmaceutical Isolators.
```

**Every row is the system's allegiance plus a population over a million**, and four of them add a
state — which is the **controlling faction's**, not any faction's:

| The system | Emissions hold |
|---|---|
| Federal | Core Dynamics Composites, Proprietary Composites |
| Imperial | Imperial Shielding |
| Independent, controlling faction in Civil Unrest | Improvised Components |
| Independent, controlling faction in War or Civil War | Military Grade Alloys, Military Supercapacitors |
| Independent, controlling faction in Boom or Expansion | Proto Heat Radiators, Proto Light Alloys, Proto Radiolic Alloys |
| Independent, controlling faction in Outbreak | Pharmaceutical Isolators |

**Alliance systems yield nothing**, and systems under a million people are not mentioned at all.
A minority Federal faction in an Independent system does not make it Federal — that reading once
had Core Dynamics Composites announced in Oppi.

A system can still offer two unrelated groups, when its controlling faction is in two states at
once. Civil Unrest *and* Expansion gives you Improvised Components and all three Proto materials.

**Nothing is said about a material you are already full of**, and a system whose materials are all
full says nothing. So once you have finished gathering, this goes quiet on its own without your
switching it off. Where D47 does not know a material's cap it says it anyway rather than guessing
you are full.

Said once per system, and never for the backlog D47 reads at startup — the only jump in that
backlog you could still act on is the last one.

#### Limpet reminders {#limpets}

**Off by default** — this is for Commanders who fly limpets, and one who never does should not have
to switch it off.

When you dock somewhere that sells limpets, with a big enough hold and few enough aboard:

```
No limpets aboard, and this station sells them. You have 256 tonnes to fill.
```

Limpets are bought through **Advanced Maintenance**, not the commodity market — which is why D47
looks for the station's re-arm service rather than reading its market. Most stations have it; the
ones that do not are on-foot settlements, small outposts, and carriers whose owner has not fitted
it. **Your own carrier may not be recognised**, which is a known and accepted gap rather than a
fault: of 136 limpet purchases measured, three were at a carrier that reported no re-arm service.

No price is quoted. Limpets are nearly always 101 credits and occasionally not, and D47 has no
reading of this station's price until after you have bought some.

##### Only remind me about limpets above {#limpet-floor}

Cargo capacity, in tonnes. Default **64**. Below it you are not running a limpet operation and the
reminder is noise.

##### Remind me when limpets are under {#limpet-percent}

A percentage **of your cargo capacity**. Default **5**, so twelve limpets in a 256 tonne hold is low
and thirteen is not.

#### Prospector results {#prospector}

What a prospector limpet found, spoken in the ring so you can keep your eyes on the rock instead of
the panel. Richest material first:

```text
Platinum, 58.3%.
Tritium, 23.5%, plus Liquid oxygen at 5.1%.
Platinum, 66.7%. Best you have found this session.
```

**Directive 47 never repeats Elite's own `Material Content` grade, and that is deliberate.** Measured
across 1,633 prospects, `Low` and `High` rocks have the same distribution of best-material proportion
— medians of 19.9% and 20.3% — and **45% of the rocks holding a material at 40% or more are graded
`Low`**. The example above, at 58.3% Platinum, is one of them. That grade is about the
engineering-material fragments a collector limpet picks up, not the commodity you are refining: two
different questions sharing a word. Passing it on would fly you past the best rock in the cluster.

**"Best you have found this session" is relative, and says so.** There is no fixed percentage that
means "good" — Platinum's median of 26.6% is above Water's 90th percentile — so a single threshold
would be wrong per material and silently so. What you have actually seen this session needs no table
and adapts to whatever you are mining. The first rock is never announced as a best, because it is the
only one.

A prospect arrives every 48 seconds at the median, which is about seventy-five lines in an hour of
mining. That is why this has its own switch, separate from the one below.

#### Core asteroids {#core-asteroid}

```text
Core asteroid. Alexandrite.
```

**Its own setting, because a core is 3 in 1,633 prospects.** Turning the running commentary off must
not cost you the one announcement you are actually mining for.

It is spoken as a routine line rather than an urgent one — urgent speaks over the top of whatever is
being said and is reserved for danger and fuel. A core is exciting; it is not a safety matter, and
announcing one across a hull warning would be the priority exactly backwards.

#### Sampling progress {#sampling}

Each organic specimen as it lands, with the distance you covered to get it:

```text
Stratum Paleas, 2 of 3. 556 metres from the last one. 1 to go.
Stratum Paleas analysed. That run is complete.
```

The distance is the whole point — you can see the genus and you can count to three, but nobody can
judge four hundred metres across a ridge, and getting it wrong wastes the sample. Directive 47 says
how far you moved and **never whether it was far enough**: that figure is in the species' Codex entry
in-game, and no table of it ships here. See [Exobiology](exobiology.md) for why.

#### Picking up where you left off {#continuity}

One line at the start of a session — a greeting on your own clock, and a readiness:

```text
Good evening, Commander. Ready to go.
```

That is the whole of it, since 2026-08-21. It used to carry how long you had been away, the
engineer in the system you were sitting in, and the top three items of your checklist read out in
full — *"Top of your list: Grade 5 Efficient Weapon on 2F Pulse Laser on Hammer (Type-11
Prospector); then…"* — which is long, and arrives while you are still putting the headset on. If
you want to know about your [checklist](checklists.md), ask; it is all still there.

It waits a few seconds after launch so the journal backlog has been folded, says its line once, and
does not speak again for the life of the process.

The first sentence is written by Directive 47. Where a persona is on, the core finishes the second
— *"Ready to reconcile the ledger"*, *"Ready to go, for the last time, again"* — in a few words of
its own and changes nothing else; with personality off it is *"Ready to go."*

#### Adventure beats {#adventure}

A beat of the story you are following, said when you reach the place it waits for
(list.md Phase 47). An adventure is a story the ship's AI tells you across a flight — a spine,
a handful of beats, each anchored to a real place by the one thing the journal can prove: you
arrived in a system, docked at a station, landed on or scanned a body, or were promoted. The
Adventures tab is where you write one or ask for one; this is the voice it reaches you by.

**A short acknowledgement lands the moment the beat fires** — *"That's it."*, *"There it is."*,
one of ten — so you know at once that you did the thing, rather than sitting through the wait
below wondering. It is a stock line, never written by a model and never rewritten by one: a
round trip is the delay it exists to arrive ahead of. The beat itself follows when it is ready.

The beat waits twenty seconds after it fires, so the line is not read over the jump it arrived
on and three beats in a minute are not three model calls. A beat that comes due while you are
being interdicted or the game says you are in danger is **dropped rather than said late** — the
story is still in the conversation, so ask and the core picks it up. The opening, said when you
accept a story, does not wait: you just pressed Begin.

With a language model configured the core aboard says the beat in its own voice, keeping every
fact in it and adding none; without one, or with personality off, the authored line is read as
written. The core knows what you know: the story's premise and stake ride with every turn, the
turn and the ending arrive only when their beats fire, and the beats ahead never do — so it can
foreshadow, because the lines it was given do, and it cannot spoil what it has not been told.

```text
Ossen's Lantern. The nebula is what is left of the star this white dwarf used to be. Scoop here.
```

Off leaves the story in the conversation and stops it being read out — the acknowledgement with
it, since both belong to this callout.

While a beat is between firing and being said, the Adventures tab shows that the core is
composing, on the desktop window and in the headset alike.

### Settings

#### Speak without being asked {#enabled}

Off means Directive 47 only ever answers. Every warning above stops with it; everything else
keeps running.

> "stop calling things out"
> "start calling things out"

#### Route progress interval {#route-interval}

In jumps. The right answer depends entirely on the trip: every 3 jumps is reassuring over 20 and
unbearable over 300. Set it to `0` to silence the progress line while keeping the hazard warnings.

#### Long jump threshold {#long-jump-threshold}

In seconds, counted from entering hyperspace.

#### Home system {#home-system}

Where you consider home, for the arrival callout. There is no default — no journal event reports
that.

---

**The model can ask what Directive 47 is watching for; it cannot switch a warning off.** Every
toggle here is changeable from the panel, by a bound key, or by saying one of the phrases above —
and not by anything the model calls. Directive 47 reads in-game messages from anyone in range,
and a model that could disable the interdiction warning is one that could be told to by the
Commander doing the interdicting.

#### Ambient remarks {#ambient}

The occasional in-character line **to you**, said because *nothing* has happened. Everything else
on this page speaks because something did. Spoken to you rather than narrated, on purpose: the
brief tells the core it is talking to its Commander, not describing the view, so a remark is a
dry aside in the cockpit and never a scene from a novel.

Three rules keep it from being noise. It waits out an interval — forty-five seconds out of the box,
and in seconds rather than minutes because the interesting end of the range is finer than a minute.
It waits for the situation to have settled for ninety seconds rather than firing on the
transition, because Status.json flips several times a minute during an approach and a remark
about being docked that arrives as you are lifting off is worse than silence. And it never
remarks on the same situation twice running.

Seven situations are covered: docked, landed, supercruise, normal space, fuel scooping, in the
SRV, and on foot.

The core aboard writes every remark itself and it is genuinely theirs — Chart will tell you about
the sky, the Quartermaster about what the run cost. It also knows who it is flying with: your
[character sheet](conversation.md#character-sheet) goes with every remark, and your
[About Me](conversation.md#about-me) story with about one in four, so the remarks differ by who is
flying and not just by wording.

**Chatter is model-written or it is nothing.** With no language model configured there are no
ambient remarks at all, and these rows are absent from this page — a switch governing something
that cannot happen would be a switch that does nothing. The ten authored lines per situation are
what the model is shown as a sample of the register, never a stand-in read out in its place: a
remark the model did not write for this moment is not spoken.

```text
The drive note has not changed in some minutes. That is what it sounds like when it is right.
```

#### Invented chatter {#npc-chatter}

Made-up radio traffic from people who do not exist: two crews on the local channel about their own
small business, the dock telling an invented pilot off when you are docked somewhere, and — about
one exchange in four — one line said to *you* over the open channel. Statements only, never a
question: **nothing here is ever answered**, by you or by the ship's AI, and none of it enters the
conversation or the comms record. It is theatre, heard once.

**This is not the game's own NPC traffic.** Elite's real messages — station chatter, pirates,
your wing — are re-voiced under Speech → *Speak incoming messages*, and they are somebody else's
words. Invented chatter is Directive 47's own fiction, on its own switch, so you can have either
without the other.

Each invented speaker gets their own voice from the NPC pool for as long as you stay in the
system, the same way the real traffic is cast. The exchange is written by the model against where
you actually are; with no language model configured there is no chatter and these rows are absent,
the same rule as the ambient remarks above — there are no canned conversations, on purpose.

The gap between exchanges is a range, not a tick: each wait lands somewhere between the least and
the most time rows — twenty to forty minutes out of the box — because overheard traffic on a
fixed cadence stops sounding overheard. Setting the two equal pins it; 0 on the least silences
them. An exchange is a scene rather than a sentence, and scenes wear out faster, which is why the
floor sits well above the ambient remarks'.

> "stop calling out invented chatter" / "start calling out invented chatter"

**Switching personality off silences these entirely**, which is the one place that switch
reaches a callout. It is in that item's own acceptance criteria: plain answers, no flavour, no
ambient remarks.

Set the interval to `0` if you want the switch without finding the switch.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `get_callouts`

Read-only. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Two sources feed the warnings, deliberately. The journal reports **transitions** — shields went
down, hull was hit — and `Status.json` reports **conditions** — shields are *still* down. A
warning built on events alone goes quiet the moment the game stops repeating itself; one built on
conditions alone cannot tell a new emergency from an ongoing one. Conditions announce on the edge
into the condition, because `Status.json` is rewritten several times a second and announcing on
the level would be a warning per tick.

The material grade table is **derived, not written**. `tools/gen-material-grades.py` generates
`MaterialGrades.g.cs` from [EDCD/FDevIDs](https://github.com/EDCD/FDevIDs) `material.csv`. The
generator is not part of the build and the app never runs it or reaches the network for it; its
output is committed and regenerated when Frontier adds or reclassifies a material. This is not
academic: the first draft of the tests asserted Antimony was grade 5, from memory. The derived
table said grade 4, and it was right — raw materials come in categories of four graded 1–4, so no
raw material is grade 5 at all. A hand-written table would have shipped that error, and it would
have surfaced as a milestone firing at the wrong count with nothing reporting a problem.

The grade-to-capacity rule — 300/250/200/150/100 for grades 1–5 — lives beside the generated
table rather than in it: five numbers that do not change when Frontier adds a material, so
regenerating cannot disturb them.

The tick loop is synchronous and must not block, so a callout does not speak. It returns an
`Announcement`, the engine queues it, and the app drains that queue onto the thread pool.
Announcements are spoken one at a time in the order queued — two synthesised concurrently would
arrive in whichever order the network returned them, and "shields are down" is not
interchangeable with "route complete". An urgent one silences the queue before speaking, on the
`Alert` channel above `Speech` in the one audio arbiter.

Condition-based warnings carry a cooldown keyed by what they are about, coarse enough that a
repeat is suppressed and specific enough that a different warning is not. A callout that throws
is logged and skipped and the rest still run: one broken callout must not take the danger
warnings down with it.

An announcement may also name an **alert cue** — a short sound played immediately ahead of it, on
the same channel, so the queue orders the two. Cue names are bound to the `AlertCue` members the
same way loop-state cues are bound to `LoopState`: the file is named for the member and the library
asserts the shipped set matches the enum, so a member added without a clip fails at startup rather
than going wrong as silence. That matters more here than it does for the loop states, because a
warning that did not fire and a warning whose cue would not load sound identical.

The measurements behind both Phase 15 warnings — every id group's follow-through rate and lead
time, the groups measured and deliberately not shipped, and what the journal does and does not
report about Powerplay — are in
[docs/spikes/journal-corpus-warnings.md](../spikes/journal-corpus-warnings.md).

</details>
