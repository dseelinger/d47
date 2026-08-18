---
title: The gap
group: Knowledge
nav_order: 112
---

What everything you have planned needs that you are not carrying, ledger by ledger.

> "what do my plans still need"
> "what am I short of"
> "what do I still have to find"

Nothing here touches the network. The first two phrases need no AI configured at all.

## It is not a wishlist

A wishlist is a list of things you want. That is what your ship builds and your suit plans already
are. **This is the arithmetic between them and what is in your hold** — the third mode of the
Loadout tab, reading across both the others, because a Commander gathering materials does not care
which ship wanted them.

## The ledgers are never totalled together

Raw, manufactured and encoded materials, the ship locker and the cargo hold have separate caps and
no exchange between them. Meta-alloys are a material, Gold ×200 is two hundred **tonnes of cargo**,
and Opinion Polls ×40 are ship locker. Adding those up produces a feasibility verdict that is
nonsense delivered confidently, so d47 does not:

```text
14 units still to find, across 3 plans.

Materials — 12 to find:
  Zirconium: 8 short (2 of 10) — for Bad Idea (Python) · MainEngines. or trade 24 Iron for 8.
  Chromium: 4 short (0 of 4) — for Bad Idea (Python) · PowerPlant.

Ship locker — 2 to find:
  Graphene: 2 short (6 of 8) — for Maverick Suit · Mod 1.
```

**The one figure that spans everything counts units still to find**, and that is a shopping list
rather than a balance — a count of things to go and get is the same shape whatever ledger they are
in, where a sum of them is not a number about anything.

## A shortfall reads back to what wants it

Every line names the ships and slots that asked for it. That is what makes the roll-up navigable
instead of merely a total: a figure you cannot trace is a figure you cannot act on, and "8 short"
means something different when it is one slot than when it is four.

## Trading is included, and stays secondary

The trader's rate is exact — one grade down returns 3 for 1, one grade up costs 6 for 1, and **a
different line costs a further 6×**, confirmed across all 1,096 trades in the corpus, of which 560
were cross-line. That last multiplier is why the line matters: the material trader's grid column is
**not** the Raw/Manufactured/Encoded category the journal writes, and treating it as one prices the
commonest trade there is at a sixth of what it costs.

So a trade appears as a second line beside the shortfall and never instead of it. The headline stays
the honest raw number, and only a trade you can actually make out of a genuine surplus is offered —
one that leaves you short of what you traded away has moved the problem rather than solved it.

## Counting what you do not own yet is a filter

Whether hulls and suits you have not bought are included is a switch on the page, not a decision
taken once on your behalf. Counting them is honest about the whole ambition; excluding them answers
what can be finished now. **Both are real questions**, and which one you are asking changes through
the evening.

### `get_build_gap`

```json
{"type":"object","properties":{"include_unowned":{"type":"boolean","description":"Whether hulls and items they do not own yet are counted. True is the whole ambition; false is what can be finished now. Defaults to true."}},"required":[],"additionalProperties":false}
```

## Not the same set as `get_plan_shortfall`

Two tools that sound alike and read different things:

- **`get_plan_shortfall`** nets what is on your **checklist** — work you have accepted, plus what
  a construction site still wants delivered.
- **`get_build_gap`** nets what is **planned**, including builds that have never been promoted —
  which is most of them while a build is still being decided.

Both are true at once. A build you are still arguing with yourself about costs materials whether or
not you have put it on a list.

## What it cannot say

A plan with no grade named has no total: which grade decides the multiplication, and you have not
said. A grade your rank cannot reach with the named engineer is a gate rather than a shortfall, and
it is stated first — listing materials under a gate nobody can pass is listing work nobody can
start. And a blueprint no shipped table covers is **kept and marked, never refused**: a checklist
line presses nothing, so the honest move is to carry it and say what is not known about it.
