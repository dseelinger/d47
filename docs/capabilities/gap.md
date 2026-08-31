---
title: The gap
group: Knowledge
nav_order: 112
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
<p class="lede">Two steps to one list of everything you are short of.</p>
<section>
<h2><span class="num">1</span> Plan something first. The Gap is the arithmetic on top.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what am I missing</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It reads your engineering plans, your builds and your goals together.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Nothing to configure — it is the sum of what you already asked for.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Read it ledger by ledger.</h2>
<svg viewBox="0 0 880 308" role="img" aria-label="The gap">
 <rect x="20" y="16" width="840" height="268" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">The gap</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Raw materials</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">14 short across 3 plans</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Commodities</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text)">3,200 t for the build</text>
 <rect x="44" y="182" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="210" font-size="16" fill="var(--text)">Credits</text>
 <text x="812" y="210" text-anchor="end" font-size="16" fill="var(--text-muted)">nothing outstanding</text>
 <text x="44" y="278" font-size="15" fill="var(--text-muted)">Each ledger says which plan it came from.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="An empty gap means nothing is planned.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">An empty gap means nothing is planned.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">The Gap has nothing of its own. With no plans, no builds and no goals, it is correctly blank.</text>
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
<p class="lede">What everything you have planned needs that you are not carrying, ledger by ledger.</p>
<section>
<h2><span class="num">1</span> Not a wishlist. The arithmetic between two of them.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="The gap is your plans minus what is in your hold">
 <rect x="20" y="44" width="250" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOUR PLANS</text>
 <text x="145" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">what your ships should be</text>
 <text x="300" y="102" text-anchor="middle" font-size="24" font-weight="800" fill="var(--accent-muted)">-</text>
 <rect x="330" y="44" width="250" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="455" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOUR HOLD</text>
 <text x="455" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">what you are carrying</text>
 <text x="610" y="102" text-anchor="middle" font-size="24" font-weight="800" fill="var(--accent-muted)">=</text>
 <rect x="640" y="44" width="220" height="96" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="750" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">THE GAP</text>
 <text x="750" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">what to go and get</text>
 <text x="440" y="182" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">A wishlist is a list of things you want. This is the arithmetic.</text>
 <text x="440" y="214" text-anchor="middle" font-size="15" fill="var(--text-muted)">Your ship builds and your suit plans are already the wishlist — this reads across both of them.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> The ledgers are never totalled together.</h2>
<svg viewBox="0 0 880 262" role="img" aria-label="Materials, the ship locker and the cargo hold have separate caps and no exchange between them">
 <rect x="20" y="40" width="270" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">MATERIALS</text>
 <text x="155" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">raw · manufactured · encoded</text>
 <text x="155" y="132" text-anchor="middle" font-size="14" fill="var(--text)">Zirconium: 8 short</text>
 <line x1="290" y1="86" x2="304" y2="104" stroke="var(--danger)" stroke-width="3" stroke-linecap="round"/>
 <line x1="304" y1="86" x2="290" y2="104" stroke="var(--danger)" stroke-width="3" stroke-linecap="round"/>
 <rect x="305" y="40" width="270" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SHIP LOCKER</text>
 <text x="440" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">Opinion Polls ×40 live here</text>
 <text x="440" y="132" text-anchor="middle" font-size="14" fill="var(--text)">Graphene: 2 short</text>
 <line x1="575" y1="86" x2="589" y2="104" stroke="var(--danger)" stroke-width="3" stroke-linecap="round"/>
 <line x1="589" y1="86" x2="575" y2="104" stroke="var(--danger)" stroke-width="3" stroke-linecap="round"/>
 <rect x="590" y="40" width="270" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="725" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">CARGO HOLD</text>
 <text x="725" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">Gold ×200 is 200 tonnes</text>
 <text x="725" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">a different thing entirely</text>
 <text x="440" y="192" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">Separate caps, and no exchange between them.</text>
 <text x="440" y="224" text-anchor="middle" font-size="16" fill="var(--text)">So the one figure that spans everything counts units still to find.</text>
 <text x="440" y="252" text-anchor="middle" font-size="15" fill="var(--text-muted)">A count of things to go and get is the same shape whatever ledger they are in. A sum of them is not.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Every shortfall names what wants it.</h2>
<svg viewBox="0 0 880 230" role="img" aria-label="A shortfall line names the ships and slots that asked for it, with any trade offered on a second line">
 <rect x="20" y="36" width="840" height="112" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="46" y="74" text-anchor="start" font-size="16" fill="var(--text)">Zirconium: 8 short (2 of 10) — for Bad Idea (Python) · MainEngines.</text>
 <text x="86" y="106" text-anchor="start" font-size="15" fill="var(--accent)">A material trader would take 24 Iron for 8</text>
 <text x="46" y="136" text-anchor="start" font-size="14" fill="var(--text-muted)">every line names the ships and the slots that asked for it</text>
 <text x="440" y="190" text-anchor="middle" font-size="16" fill="var(--text)">A figure you cannot trace is a figure you cannot act on.</text>
 <text x="440" y="220" text-anchor="middle" font-size="15" fill="var(--text-muted)">The trade is a second line beside the shortfall, never instead of it — the headline stays the raw number.</text>
</svg>
<p class="body">Whether hulls and suits you have not bought yet are counted is a switch on the page, not a decision taken once on your behalf. Counting them is honest about the whole ambition; excluding them answers what can be finished tonight. Both are real questions, and which one you are asking changes through the evening.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="ships.html"><span class="ct">Ships →</span><span class="cd">The builds this reads across, one entry per slot.</span></a>
<a class="card" href="engineering.html"><span class="ct">Engineering →</span><span class="cd">Where a slot’s material bill comes from in the first place.</span></a>
<a class="card" href="checklists.html"><span class="ct">Checklists →</span><span class="cd">The other shortfall — what you have actually accepted, rather than merely planned.</span></a>
</div>
</div>
</div></div>

## The details

What everything you have planned needs that you are not carrying, ledger by ledger.

> "what do my plans still need"
> "what am I short of"
> "what do I still have to find"

Nothing here touches the network. The first two phrases need no AI configured at all.

### It is not a wishlist

A wishlist is a list of things you want. That is what your ship builds and your suit plans already
are. **This is the arithmetic between them and what is in your hold** — the third mode of the
Loadout tab, reading across both the others, because a Commander gathering materials does not care
which ship wanted them.

### The ledgers are never totalled together

Raw, manufactured and encoded materials, the ship locker and the cargo hold have separate caps and
no exchange between them. Meta-alloys are a material, Gold ×200 is two hundred **tonnes of cargo**,
and Opinion Polls ×40 are ship locker. Adding those up produces a feasibility verdict that is
nonsense delivered confidently, so d47 does not:

```text
14 units still to find, across 3 plans.

Materials — 12 to find:
  Zirconium: 8 short (2 of 10) — for Bad Idea (Python) · MainEngines. A material trader would take 24 Iron for 8.
  Chromium: 4 short (0 of 4) — for Bad Idea (Python) · PowerPlant.

Ship locker — 2 to find:
  Graphene: 2 short (6 of 8) — for Maverick Suit · Mod 1.
```

**The one figure that spans everything counts units still to find**, and that is a shopping list
rather than a balance — a count of things to go and get is the same shape whatever ledger they are
in, where a sum of them is not a number about anything.

### A shortfall reads back to what wants it

Every line names the ships and slots that asked for it. That is what makes the roll-up navigable
instead of merely a total: a figure you cannot trace is a figure you cannot act on, and "8 short"
means something different when it is one slot than when it is four.

### Trading is included, and stays secondary

The trader's rate is exact — one grade down returns 3 for 1, one grade up costs 6 for 1, and **a
different line costs a further 6×**, confirmed across all 1,096 trades in the corpus, of which 560
were cross-line. That last multiplier is why the line matters: the material trader's grid column is
**not** the Raw/Manufactured/Encoded category the journal writes, and treating it as one prices the
commonest trade there is at a sixth of what it costs.

So a trade appears as a second line beside the shortfall and never instead of it. The headline stays
the honest raw number, and only a trade you can actually make out of a genuine surplus is offered —
one that leaves you short of what you traded away has moved the problem rather than solved it.

### Counting what you do not own yet is a filter

Whether hulls and suits you have not bought are included is a switch on the page, not a decision
taken once on your behalf. Counting them is honest about the whole ambition; excluding them answers
what can be finished now. **Both are real questions**, and which one you are asking changes through
the evening.

### Asking about one material names the blueprint

*"What is Conductive Polymers for?"* used to get an apology: d47 said it could not tell which
blueprint ate them, because the shortfall is netted across every plan at once.

**It knew more than it admitted.** The demands under a shortfall have always named the ship and the
slot — *for Bad Idea (Python) · MainEngines* — and only the blueprint was missing. It is recorded
now, at the point the plan is costed, where the slot has it in hand.

**The fleet-wide list does not show it, and that is deliberate.** A shortfall can name a dozen
demands, and hanging *· Dirty Drive Tuning 3* off each would double a line that is read aloud as
often as it is drawn. Ask about one material and there is room:

```
Conductive Polymers: 14 short (6 of 20).
  Bad Idea (Python) · MainEngines · Dirty Drive Tuning 3 — 8
  Flamebrand (Anaconda) · PowerPlant · Armoured 4 — 12
```

Say *"what is Conductive Polymers for"*, *"what do I need Conductive Polymers for"* or *"what wants
Conductive Polymers"*. Those phrases exist only for materials something you have planned actually
wants — the vocabulary follows your plans, and asking about anything else falls through to an
ordinary answer.

#### `get_build_gap`

```json
{"type":"object","properties":{"include_unowned":{"type":"boolean","description":"Whether hulls and items they do not own yet are counted. True is the whole ambition; false is what can be finished now. Defaults to true."},"material":{"type":"string","description":"One material, by name or symbol. Narrows the answer to what wants that one and names the blueprint each demand is for. Omit for everything."}},"required":[],"additionalProperties":false}
```

### Not the same set as `get_plan_shortfall`

Two tools that sound alike and read different things:

- **`get_plan_shortfall`** nets what is on your **checklist** — work you have accepted, plus what
  a construction site still wants delivered.
- **`get_build_gap`** nets what is **planned**, including builds that have never been promoted —
  which is most of them while a build is still being decided.

Both are true at once. A build you are still arguing with yourself about costs materials whether or
not you have put it on a list.

### What it cannot say

A plan with no grade named has no total: which grade decides the multiplication, and you have not
said. A grade your rank cannot reach with the named engineer is a gate rather than a shortfall, and
it is stated first — listing materials under a gate nobody can pass is listing work nobody can
start. And a blueprint no shipped table covers is **kept and marked, never refused**: a checklist
line presses nothing, so the honest move is to carry it and say what is not known about it.
