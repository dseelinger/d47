# Phase 50 — Colonisation trade routes

**Status: built 2026-08-25.** `list.md` Phase 50 was flagged by the Commander as *not thought
through yet*, and its last item existed to be struck out by this document: *"Open, and to be settled
in a plan before anything is built. Named here so they are not decided by accident."*

This is that plan, and it is kept as written rather than rewritten into a description of what
shipped — a plan that is edited to match the code stops being evidence of what was decided before
it. **Three of the four recommendations shipped as recommended. The third was overturned by the
Commander on the day, and §"Where does the carrier fit?" below is the reasoning it overturned.**

> **Overturned 2026-08-25: the carrier is entered by the Commander and *is* subtracted.** The
> measurement holding it out of the arithmetic — wrong 679 times against right 347 — is about
> *deriving* a figure from `CargoTransfer`, and that stands and is unchanged. What the ruling adds
> is the route this section itself left open: *"a Commander-entered figure, not a derived one"*. A
> Commander can see the carrier's inventory screen; d47 cannot. So a typed figure is a statement of
> fact rather than an inference, it comes off the shopping list, and it is named and **dated** on
> every answer that used it — which is what d47 owes in return for using a number it cannot check.
> `CarrierManifest` is the store, the Checklist tab's Sourcing page is where it is said, and the
> site's own outstanding figures are untouched by it.

It answers the four open questions with a recommendation each, and it separates what the earlier
phases already settle from what is genuinely a choice.

---

## What is already settled, and is not up for discussion here

Three things arrive from other phases and this plan must not re-open them.

**The shopping list is exact and needs no source.** `ColonisationConstructionDepot` is a snapshot
rather than a delta — measured over 6,330 events and 120,208 resource rows, `RequiredAmount` never
moving mid-build and `ProvidedAmount` never decreasing across 119,887 consecutive comparisons. So
`ConstructionSite.Outstanding` is a fact off the Commander's own disk. **Nothing in this phase may
recompute it.** That is the trap which caught `EngineerProgressState` and `ModuleStore`, silently
both times.

**A site that does not exist cannot be costed.** Every machine-readable rendering of Frontier's
facility figures sits inside GPL-3.0 source, EDSC publishes nothing, EDCD has no colonisation
repository, and Frontier's own guide states every mechanic and publishes not one number.
`ColonisationPlan.WhatThisCannotBe` is already the sentence and this phase reuses it verbatim
rather than learning to cost a plan by the back door of a sourcing feature.

**Three things d47 cannot see, and says so rather than estimating.** What is on the carrier is not
derivable — accumulating `CargoTransfer` against `CarrierStats` came out wrong 679 times against
right 347, driving 11 commodities negative. The site is only as fresh as the last docking, since
6,307 of 6,330 events fired while docked at that very site. And a network supply figure ages
fastest precisely during a rush.

---

## What Phase 49 already built that this stands on

Phase 49 shipped on 2026-08-25 and it is more than half of the machinery.

- `CommodityMarketSearch.Rank` — the local ranking over `MarketSnapshot`, pure, no clock, no socket.
- `ITradePlanService.FindCommodityAsync` — the sweep, the cache, the `MarketBook` merge and the
  ageing-out, for **one** commodity.
- `CommodityBoard` — the last answer, so a spoken answer and a drawn one are the same answer.

**The gap is exactly the arithmetic**, and the checklist says what kind: *"It is a covering problem
and not the trade planner."* Phase 49 answers *where do I buy tritium*; a build asks about twenty
commodities at once, and that is a different question rather than twenty of the same one.

---

## The join, which is the thing most likely to ship broken

The depot's rows carry a **folded internal symbol**; both market sources are keyed by the **display
name** — `Market.json` by `Name_Localised`, spansh by its `commodity` field. So *Low Temperature
Diamonds* has to meet `$lowtemperaturediamond_name;`.

Phase 17 measured this seam going wrong. `ColonisationContribution` is mixed case on 30 of 30
symbols against 0 of 31 for the depot, and a fold that stripped `$` and `_name;` without lowercasing
joined the depot to the hold perfectly and matched no contribution at all — right-looking and wrong.

**A sourcing plan that quietly omits a commodity because its name did not join is that failure with
a Commander's week on it.** So the acceptance is blunt and is not negotiable:

> Every outstanding row either resolves to a market row, or is named in the answer as one d47 could
> not price. Nothing is dropped in silence.

That is a test before it is a feature.

### Corrected 2026-08-25, by writing it wrong first

The paragraph above states the seam correctly and then invites exactly the wrong fix, which is what
happened on the first attempt: the market's display name was folded with `JournalJson.Symbol` and
compared against the depot's symbol. It matched nothing, and the acceptance test caught it on the
first run.

**Elite's symbol is not derivable from the spelling.** `Low Temperature Diamonds` folds to
`low temperature diamonds`; the symbol is `lowtemperaturediamond`, which has lost the spaces *and*
the plural. No fold gets from one to the other and a lookup table would be needed to try.

**There is no need for one, and the repository already said so.** `ColonisationSites` carries the
comment: *"Name_Localised is on every row of all 120,208 measured, which is why this needs no
commodity table at all."* `ConstructionResource.Name` is therefore already the display name, and
both market sources are keyed by that same display name. **The join is name to name.**

The symbol is not retired — it is what joins the depot to the **hold**, which is Phase 17's business
and carries symbols rather than spellings. **Two joins for two purposes**, and reaching for the
wrong one is the failure Phase 17 measured going the other way. The symbol fold survives in the
sourcing join only as a fallback for a row where Elite omitted `_Localised` and both sides are
holding a raw symbol.

`FoldingTheDisplayNameDoesNotProduceElitesSymbol` asserts the fold's actual output, so nobody puts
this back.

---

## The four open questions

### 1. What shape is the answer?

**Recommendation: a shopping list per station, ordered by how much of the build each clears.**

Not a plotted course, and not a checklist project.

- **Not a course.** The Commander is flying a loop they will repeat a dozen times; the value is
  knowing *which four stations between them carry the whole list*, not the order to visit them in.
  Ordering is the easy part and they can see it on the Routing tab.
- **Not a checklist project, yet.** Phase 17's substrate would make it survive a session, which is
  attractive — but a sourcing answer is built on network prices that age in hours, and Phase 49's
  whole argument is that a saved price looks current because it was saved. A checklist project full
  of month-old supply figures is the trap wearing a friendlier face. `CommodityBoard` is in memory
  for exactly this reason and this should match it.
- **So: a board like Phase 49's**, one entry per station, each carrying which outstanding
  commodities it covers, at what price, in what quantity, and how old the figures are.

The unit is *station covers these six of your twenty*, because that is the sentence a Commander
acts on.

### 2. How many sites at once?

**Recommendation: one, chosen the way the checklist already chooses.**

Three were open simultaneously in the corpus, and `ConstructionSite` is deliberately a collection
with a selection rather than a "current site" field. But sourcing two builds at once is not two
answers side by side — it is a different and much harder problem (a station covering four
commodities for site A and two for site B is not comparable to one covering six for A), and nobody
has asked for it.

So: the selected site, named in the answer, with the same *"you have three open; this is Hutton
Orbital's"* disambiguation `get_construction_needs` already does. If a Commander wants the other,
they select it and ask again.

### 3. Where does the carrier fit?

**Recommendation: nowhere in the arithmetic, and explicitly named in the answer.**

This is the hardest one to be disciplined about, because the carrier is the obvious staging post
and it is exactly the thing a real colonisation Commander is using.

**But what is on the carrier is not derivable.** The reconciliation came out wrong 679 times against
right 347 and drove 11 commodities negative. A plan that subtracts a wrong number from the shopping
list is worse than one that subtracts nothing: it sends the Commander to buy 200 tonnes they already
have, or — far worse — *not* to buy 200 they do not.

So the plan sources the **whole** outstanding list, and says in one sentence that it has not counted
the carrier. The Commander knows what is in their own carrier; d47 does not, and saying so is the
honest version.

*If* this is later worth improving, the route is a Commander-entered figure ("I have 300 tritium on
the carrier"), not a derived one — but that is a separate ask and is not in this phase.

### 4. Where is it asked from?

**Recommendation: a panel page, and one existing tool widened — no new tool.**

The surface ceiling makes this nearly decided already. After Phase 49 the SRV profile sits at 39,864
bytes against a `ComfortableBytes` of 40,000: **136 bytes spare.** A new tool costs hundreds, and
overrunning does not fail loudly — it drops the Commander's action tools.

So:

- **Panel**: a page on the **Checklist** tab rather than Routing. The Commander is looking at what
  they owe, and *where to get it* belongs beside *what is left* rather than beside route plotting.
  Costs no tool-surface bytes at all, which is the lever Phase 47 used and Phase 49 used again.
- **Voice**: widen `get_construction_needs` with a boolean like `where_to_buy`, which is tens of
  bytes on a tool whose sentence already covers the subject — the same trade Phase 49 made, and for
  the same reason.

If even that does not fit, the panel page ships alone and the voice half waits. The panel is where
a twenty-row answer belongs anyway.

---

## What to build, in order

1. **`ColonisationSourcing` in Core.** Pure: handed the outstanding list and a list of
   `MarketSnapshot`, returns a covering plan. Reads no clock, opens no socket, knows nothing about
   Spansh — the rule that made Phase 36 testable, and the same seam `CommodityMarketSearch` already
   sits behind. This is the whole of the arithmetic and all of it is assertable.
2. **The join, with the acceptance above as a test first.** Symbol to display name, and every
   unresolved row named in the answer rather than dropped.
3. **The service method**, beside `FindCommodityAsync` on `ITradePlanService`, reusing the same
   sweep and the same cache. Nothing new is fetched here either.
4. **The panel page.**
5. **The voice half**, if the bytes are there when the time comes. Measure, do not estimate — the
   probe that measured Phase 49's cost took one test run and turned a 615-byte guess into a fact.

**All five shipped on 2026-08-25**, and the fifth was measured rather than estimated: the widening
put the SRV profile at **40,027 against a ceiling of 40,000**, which is a profile that degrades and
drops the Commander's action tools rather than failing loudly. It was paid for the way Phase 49 paid
for its own — 136 bytes of redundancy trimmed inside this same capability, four descriptions that
each said something twice — leaving **39,897, with 103 spare**.

### The objective, stated

`TradePlanner` maximises credits over hops and carries credits and cargo between them. **None of
that is this.** The cargo is decided before the Commander leaves — it is the depot's list — the
objective is **trips and time** rather than profit, and the binding constraint is **supply where you
buy** rather than demand where you sell.

Concretely: minimise the number of stations that between them cover the outstanding list, subject to
each station actually holding the tonnage, and break ties by distance. That is a set-cover, which is
NP-hard in general and entirely untroubled by it at this size — twenty commodities against a few
hundred markets is a greedy pass with an exact check, and the greedy answer is within a log factor
of optimal on a problem where the Commander cannot tell the difference.

---

## What would make this wrong while looking right

Worth writing down before anybody starts, because all three have precedent in this repository.

- **A commodity silently missing from the plan because its name did not join.** Covered above; it is
  the acceptance test.
- **Supply figures quoted as though current during exactly the rush that invalidates them.** Phase
  49's caveat arriving here with more tonnage behind it. Every figure carries its age, same as there.
- **Subtracting a carrier's contents that d47 cannot actually see.** Question 3.
