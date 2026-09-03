---
title: Ships
group: Knowledge
nav_order: 111
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
<p class="intro">Three steps to a build you are working towards.</p>
<section>
<h2><span class="num">1</span> Open the Ships tab. Your fleet is already there.</h2>
<svg viewBox="0 0 880 300" role="img" aria-label="The Ships tab">
 <rect x="20" y="16" width="840" height="264" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <rect x="20" y="16" width="840" height="42" rx="8" fill="var(--surface)"/>
 <text x="44" y="44" font-size="16" font-weight="700" fill="var(--accent)">Ships</text>
 <text x="836" y="44" text-anchor="end" font-size="14" fill="var(--text-muted)">Drawings</text>

 <rect x="44" y="78" width="256" height="152" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="3"/>
 <path d="M78 130 L214 120 L266 148 L214 166 L112 168 Z" fill="var(--text-muted)"/>
 <path d="M112 168 L214 166 L196 186 L128 184 Z" fill="var(--border)"/>
 <text x="60" y="206" font-size="15" fill="var(--text)">Ptarmigan (Anaconda)</text>
 <text x="60" y="224" font-size="13" fill="var(--text-muted)">flying now</text>

 <rect x="312" y="78" width="256" height="152" rx="6" fill="var(--surface)"/>
 <path d="M346 134 L462 122 L512 146 L462 164 L378 168 Z" fill="var(--text-muted)"/>
 <text x="328" y="206" font-size="15" fill="var(--text)">Sparrow (Krait Mk II)</text>
 <text x="328" y="224" font-size="13" fill="var(--text-muted)">Jameson Memorial</text>

 <rect x="580" y="78" width="256" height="152" rx="6" fill="var(--surface)" stroke="var(--text-muted)" stroke-width="2" opacity="0.55"/>
 <path d="M614 132 L730 122 L778 146 L730 164 L646 168 Z" fill="var(--text-muted)" opacity="0.55"/>
 <text x="596" y="206" font-size="15" fill="var(--text-muted)">Python</text>
 <text x="596" y="224" font-size="13" fill="var(--text-muted)">wanted</text>

 <text x="44" y="266" font-size="15" fill="var(--text-muted)">Read out of the journal. The one you are flying is outlined; a ship you only mean to buy is faded.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Open one and change a module.</h2>
<svg viewBox="0 0 880 308" role="img" aria-label="Ptarmigan">
 <rect x="20" y="16" width="840" height="268" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Ptarmigan</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Power plant</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">7A Guardian Hybrid</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">FSD</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text)">6A — dirty drive grade 5</text>
 <rect x="44" y="182" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="210" font-size="16" fill="var(--text)">Jump range</text>
 <text x="812" y="210" text-anchor="end" font-size="16" fill="var(--text-muted)">62.4 ly</text>
 <text x="44" y="278" font-size="15" fill="var(--text-muted)">The gauges move as you change things, so you see the cost before you commit.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Accept the plan, and it lands on your checklist.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what do I still need for the Ptarmigan</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The shortfall goes to the Gap page with everything else.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">One build per ship, kept per Commander.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="Elite never writes a loadout after engineering.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Elite never writes a loadout after engineering.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">D47 works your modifications out from the modules themselves, because the game does not tell it.</text>
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
<p class="intro">Your fleet, the hulls you mean to buy, and one build per ship.</p>
<section>
<h2><span class="num">1</span> The build owns what. The checklist owns when.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="A build and a checklist are separate, and nothing crosses between them unasked">
 <rect x="20" y="20" width="300" height="120" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="170" y="62" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">THE BUILD</text>
 <text x="170" y="92" text-anchor="middle" font-size="16" fill="var(--text-muted)">what a ship should be</text>
 <text x="170" y="120" text-anchor="middle" font-size="15" fill="var(--text-muted)">one entry per slot</text>
 <text x="440" y="62" text-anchor="middle" font-size="15" fill="var(--text-muted)">you promote it</text>
 <line x1="336" y1="80" x2="530" y2="80" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="544,80 528,72 528,88" fill="var(--accent-muted)"/>
 <text x="440" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">and accept it</text>
 <rect x="560" y="20" width="300" height="120" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="710" y="62" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">THE CHECKLIST</text>
 <text x="710" y="92" text-anchor="middle" font-size="16" fill="var(--text-muted)">what you are doing next</text>
 <text x="710" y="120" text-anchor="middle" font-size="15" fill="var(--text-muted)">in the order you put it in</text>
 <text x="440" y="180" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">Nothing crosses between them unasked.</text>
 <text x="440" y="212" text-anchor="middle" font-size="16" fill="var(--text-muted)">So a build can be rearranged without your checklist reordering itself underneath you.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Changing your mind about a slot is an edit.</h2>
<svg viewBox="0 0 880 272" role="img" aria-label="Replacing a module in a slot edits that slot rather than deleting it and adding another">
 <rect x="20" y="40" width="300" height="92" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="170" y="78" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">HARDPOINT 3</text>
 <text x="170" y="108" text-anchor="middle" font-size="16" fill="var(--text-muted)">long range pulse laser</text>
 <text x="448" y="66" text-anchor="middle" font-size="15" fill="var(--text-muted)">you change your mind</text>
 <line x1="336" y1="86" x2="530" y2="86" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="544,86 528,78 528,94" fill="var(--accent-muted)"/>
 <rect x="560" y="40" width="300" height="92" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="710" y="78" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">HARDPOINT 3</text>
 <text x="710" y="108" text-anchor="middle" font-size="16" fill="var(--text-muted)">overcharged multi cannon</text>
 <rect x="20" y="164" width="410" height="76" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="225" y="196" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">The same slot, edited</text>
 <text x="225" y="222" text-anchor="middle" font-size="15" fill="var(--text-muted)">it keeps everything it had been through</text>
 <rect x="450" y="164" width="410" height="76" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="655" y="196" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text-muted)">Not a delete and an add</text>
 <text x="655" y="222" text-anchor="middle" font-size="15" fill="var(--text-muted)">which used to bury the history beside it</text>
 <text x="440" y="266" text-anchor="middle" font-size="16" fill="var(--text-muted)">A slot holds one plan, because a slot holds one module.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> You can plan a ship you have not bought.</h2>
<svg viewBox="0 0 880 250" role="img" aria-label="An intended hull becomes owned and the plan is pointed at it automatically">
 <text x="300" y="28" text-anchor="middle" font-size="15" fill="var(--text-muted)">you buy one</text>
 <text x="610" y="28" text-anchor="middle" font-size="15" fill="var(--text-muted)">D47 notices</text>
 <rect x="20" y="40" width="250" height="92" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="78" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">CORSAIR</text>
 <text x="145" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">intended, not bought yet</text>
 <line x1="282" y1="86" x2="304" y2="86" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="318,86 302,78 302,94" fill="var(--accent-muted)"/>
 <rect x="330" y="40" width="250" height="92" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="455" y="78" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">CORSAIR</text>
 <text x="455" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">yours now</text>
 <line x1="592" y1="86" x2="614" y2="86" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="628,86 612,78 612,94" fill="var(--accent-muted)"/>
 <rect x="640" y="40" width="220" height="92" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="750" y="78" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">THE PLAN</text>
 <text x="750" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">points at it</text>
 <rect x="20" y="158" width="840" height="56" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="193" text-anchor="middle" font-size="17" fill="var(--text)">“That Corsair is yours now, and the plan you had for one is pointed at it.”</text>
 <text x="440" y="244" text-anchor="middle" font-size="16" fill="var(--text-muted)">Only when exactly one intended build matches. Two planned and one bought is a question.</text>
</svg>
<p class="body">Buying the hull is the plan's <em>first step</em> rather than something you have to do before planning at all — which is also why a hull you own is a derived line and one you only intend is an authored one. That is the same rule the checklist draws, because it is the same rule.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="engineering.html"><span class="ct">Engineering →</span><span class="cd">What a planned roll costs, and how the one you already made went.</span></a>
<a class="card" href="checklists.html"><span class="ct">Checklists →</span><span class="cd">Where a promoted plan lands, and what it does once it is there.</span></a>
<a class="card" href="engineers.html"><span class="ct">Engineers →</span><span class="cd">Who can roll what a build asks for, and how far away they are.</span></a>
</div>
</div>
</div></div>

## The details

Your fleet, the hulls you intend to buy, and one build per ship.

### Ask for it

> "what have I planned"
> "plan grade 5 dirty drives on the thrusters"
> "plan an overcharged multi-cannon on the third hardpoint of my Corsair"
> "put that on my checklist"

The first and the last need no AI configured at all.

### The plan owns what. The checklist owns when.

These are two different questions and Directive 47 keeps them apart:

- **A build** is what a ship should be. It lives in `data/ships.json`, it has one entry per slot,
  and changing it disturbs nothing else.
- **Your checklist** is what you are working on next, in the order you put it in.

**Nothing crosses between them unasked.** Planning a slot writes the build and stops. It reaches
your checklist when you promote it — and even then it arrives as a proposal you accept, the same
way every other suggestion does.

That separation is what lets you rearrange a build without your checklist reordering itself under
you, and reorder your checklist without the build forgetting what you decided.

### What is fitted, remembered {#remembered}

**What is actually fitted is a third thing, in a third file.** `data/loadouts.json` holds what each
of your ships was last seen carrying, straight from the game — every ship you have sat in, not just
the one you are in now. Intent is authored and a loadout is derived, so the two never borrow from
each other.

**It is a cache and nothing depends on it surviving.** Delete it and Directive 47 rebuilds what it
can from your journals; it exists so that a ship you last flew a year ago is still answerable
today, which reading the newest journals alone could never manage. A ship you sell is dropped from
it at the sale — and dropped again if the game ever hands its id to something new, because Elite
reuses ship ids and an inherited module list would be worse than knowing nothing.

**A sale you made while Directive 47 was closed still lands.** The file records how far through
your journals it has been read, so the next start picks up exactly where it left off — however
long that is. Come back after a year away and the first start takes a few seconds longer while it
reads the gap, and says so in the log.

**Not look right? Rescan.** The Ships card in Settings says how many ships are remembered and how
stale the oldest of them is, and offers **Rescan my journals**: it reads every journal on disk
again and rebuilds the lot from scratch. A ship nothing in your journals supports stops existing,
and one that has been sitting there wrong is put back the way the game described it. It takes a few
seconds — about three on a year of flying — and you can do it as often as you like.

Nothing else is touched. Your plans, your checklist and your settings are not read and not written,
and a rescan that finds no journals at all changes nothing rather than emptying the file: a journal
folder that has moved reads exactly like a fleet that has been sold, and only one of those should
be believed.

### One build per ship

Comparing a combat fit against an exploration fit for the same hull is a planner feature this
deliberately does not have. A slot holds one plan, because a slot holds one module.

**Changing your mind about a slot is an edit, not a delete and an add.** Swapping a long-range
pulse laser for an overcharged multi-cannon leaves you with the same third hardpoint on the same
hull — with whatever history it had. Before this, the first time you changed your mind about a
slot, everything that slot had been through was tombstoned and an identical-looking new item
opened beside the corpse.

### The fleet, and the fleet you intend

The Loadout tab opens on your fleet and answers where each ship is before you drill into anything.

**A hull you do not own is not in the fleet.** It is its own thing, with no ship id, because
Elite's id is what a ship list is keyed by and a Corsair nobody has bought has none. So
**acquiring the hull is the plan's first step** rather than a precondition sitting outside it:

```text
Corsair, intended — not bought yet
```

**Buying one adopts the plan rather than making you re-point it.** When the journal reports a new
hull of a type you had planned for, Directive 47 binds the plan to it and says so:

```text
That Corsair is yours now, and the plan you had for one is pointed at it.
```

Only when exactly one intended build matches the hull. Two Corsairs planned and one bought is a
question rather than a guess.

### Owned is derived. Intended is authored.

The same rule your checklist already draws between a line the journal settles and a line a person
does — so it looks like the same rule, because it is one.

### Dropping a build keeps what it already produced

Delete a plan and whatever it already put on your checklist **stays there**. You ordered your list
around those lines, and quietly removing them is a history that lies.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

**This capability advertises nothing, and the reason is cost as much as safety.** The advertised
tool surface is re-billed on every turn, and the largest profile — the SRV's, which carries that
vehicle's controls on top of everything else — measured **39,840 bytes against a 40,000 byte
ceiling** before this capability existed. `ToolProfiles.ComfortableBytes` says in as many words
that raising the number a third time is the wrong answer.

So the one route that genuinely needs a model to understand free English is
[`plan_ship_build`](checklists.md), which already existed and now writes to the build rather than
proposing straight to the checklist. Everything below is `Protected`: reachable from the panel and
from a phrase, and never from the model.

#### `get_ship_plans`

Every ship the Commander owns and every hull they intend to buy, with where each one is and how
many slots its build has an opinion about.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Not `get_fleet`, which `JournalCapability` already has and which answers a different question:
that one reports what the journal saw in the racks, and this one reports what the Commander means
to do about it.

#### `promote_ship_plan`

Offer a ship's build to the checklist. It is a proposal: the Commander accepts, and one planned
change produces the modification plus whatever unlocking and ranking it needs.

```json
{"type":"object","properties":{"ship":{"type":"string","description":"Which ship, by name or hull. Omit for the one the Commander is flying."}},"required":[],"additionalProperties":false}
```

**Promotion is one-to-many.** `EngineeringPlan` already emits an `EngineerAccess` step beside a
modification, so promoting one planned change produces several lines — and each carries the slot
that caused it in its intent, which is what lets a later revision find them again.

#### `drop_ship_plan`

Drop a ship's build. The Commander's own act: not offered to the model, and refused if it asks.
What the plan already put on the checklist is kept.

```json
{"type":"object","properties":{"ship":{"type":"string","description":"Which ship, by name or hull. Omit for the one the Commander is flying."}},"required":[],"additionalProperties":false}
```

#### The file

```json
{
  "ships": [
    {
      "id": "ship-1",
      "hull": "python",
      "shipId": 12,
      "name": "Bad Idea",
      "slots": [
        {
          "slot": "MainEngines",
          "blueprint": "Dirty Drive Tuning",
          "grade": 5,
          "engineer": "Felicity Farseer",
          "experimental": "Drag Drives"
        }
      ]
    }
  ]
}
```

`id` is the build's own identity and is **independent of `shipId` from the moment it is created** —
that independence is what there is to rebind when the hull is bought. A build with no `shipId` is
an intended one.

Hand-edited, it takes effect without a restart, and a line the file gets wrong is reported rather
than silently dropped.

</details>
