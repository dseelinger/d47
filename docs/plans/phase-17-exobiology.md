# Phase 17 — exobiology, from the Galaxy Map to the sample

## Context

`list.md` Phase 17 carried one line about exobiology: genus, spacing, and what has already been
scanned. That is the last thing a Commander does, and the item said nothing about the four hours
before it — finding somewhere worth going.

The scope grew on 2026-08-15, in conversation with the maintainer, and it grew in a direction that
changes the shape rather than the size. **The most valuable case is the one no index can serve.**
First footfall pays a multiple, and by definition it happens in systems nobody has scanned and
uploaded — so a search over crowd-fed data is exactly the wrong tool for the highest-value half of
the activity. What works there is reading the Galaxy Map, and a procedural system name turns out to
carry a **mass code**: the lone letter before the digits, `Dryafea PO-X `**`d`**`2-0`, running `a`
to `h` from least to most massive, boxel size doubling at each step. That is decodable from a string
with no network call, no upload and no index — which makes it the one part of this family that helps
in the black.

So the phase splits into four items where there was one, and they are not the same kind of work:

| Item | Machinery | Needs a source? |
|---|---|---|
| Spike | — | It *is* the sourcing |
| Read a system name | A rule and a string | The mass ladder, and provenance for it |
| Find the exobiology | `IRouteService`, `SAASignalsFound` | The planner's wire shape |
| Exobiology sampling | `Status.json`, per-body state | The genus spacing table |

**Nothing here reopens Phase 14.** Two of these lean on Phase 14's machinery — the route seam and
the galaxy search — but that phase is ticked and released as `v0.10.0`, and un-ticking a shipped
phase would make a published version number say something untrue. New work lands in the phase that
is open.

---

## The rule the whole plan runs on

**No invented game data, and the enforcement is structural rather than a good intention.** Three
mechanisms, in order of preference:

1. **The game's own answer wins.** `SAASignalsFound` names the genera on a body outright. Any
   prediction from `Scan` properties is a guess about something the game will simply tell you a
   minute later, so the prediction is for *before* the surface scan and is never allowed to
   contradict the event.
2. **A table, derived by a generator, with provenance recorded.** Same shape as `Materials.tsv` and
   `Engineers.tsv`. This is where the genus conditions and the species values belong, *if* a
   licence-clean machine-readable source exists.
3. **Web search, where what it finds stays a sentence.** Shipped in Phase 14. This is the honest
   home for terrain lore — "legend has it that this genus sits near that feature" — because it is
   written-up player experience rather than a dataset. Attributed and hedged is honest; the same
   sentence hand-copied into a shipped table is d47 laundering a forum post into its own voice, and
   it cannot be corrected without a new tag.

And where none of the three has an answer, the feature **declines by name**, as the material
sourcing item already does for Rhenium, Lead and Boron. A search that returns nothing reads as
"there is none here", which is a wrong answer that looks like a right one.

`Radicoida Unica` is the worked example of why volatile knowledge must not be a table row: one
species, one system (HIP 87621), attached to a live narrative and a community goal. A table
compiled into a signed binary cannot be corrected when the story moves; the community goals board,
shipped in `v0.10.0`, already names goals like that one straight from the journal.

---

# The spike — what can be known before you land

Throwaway probe under `spike/`, finding to `docs/spikes/`, per `spike/README.md`. Five questions.
**Each one is written with what a null result means**, because a spike that can only confirm is a
spike that will confirm.

## 1. Is there an exobiology route planner, and what does it send?

spansh has no published API. Every endpoint d47 uses was established against the live service and
recorded in `SpanshRequest`, and this is no different.

- **If yes:** a fifth plot type on `IRouteService`, beside neutron, Road to Riches, trade and
  mining. The submit-queue-poll protocol already exists; this is a request shape and a projection.
- **If no:** *Find the exobiology* falls back to a filtered body search, and the item has to say
  that it ranks what it fetched rather than the galaxy — the same wording the material sourcing item
  uses for a share it cannot sort on.

Watch for the group-filter trap. Four filters so far — materials, services, signals, state — take a
nested shape and **silently ignore** the obvious flat spelling, returning an unfiltered result that
looks like a working search. Anything new is assumed to be a fifth until proven otherwise.

## 2. Is there a licence-clean table of what conditions each genus needs?

Atmosphere, pressure, temperature, gravity, volcanism, and the star class that decides a variant's
colour and therefore its price.

- **First place to look:** EDDiscovery's `EliteDangerousCore`, Apache-2.0, already vetted and used
  for the engineer referral chain.
- **If it is prose in forum guides rather than data:** the prediction half moves to web search
  entirely and *Find the exobiology* ships only the post-DSS arithmetic. That is a smaller feature
  and an honest one.
- Licence is checked on the **transitive graph**, not the direct reference, and the data underneath
  a permissive licence is checked separately — a source's own licence is never mistaken for
  permission over Frontier's figures.

## 3. Does the mass code correlate with what actually pays?

**The question no source can answer, and the reason this spike is worth running.** The maintainer's
own journals hold `ScanOrganic` alongside the system name at the time, so the claim "more system
mass, more of the big-payout species" can be turned into a number.

- Join each `ScanOrganic` to the mass code parsed out of the system name, and to the species value.
- Report the distribution per mass code with its sample size.
- **If the correlation is not there, say so in the item.** *Read a system name* then explains what
  the letter means and stops. Shipping folklore as a heuristic sends a Commander a long way in the
  wrong direction with d47 sounding certain, which is worse than saying nothing.

The corpus is a Commander's own play history and **stays out of the repository**; the recipe in
`journal-corpus-engineering.md` §7 applies unchanged.

## 4. What is the first-footfall multiplier?

It lands directly in a payout figure. The maintainer said 4× and the assistant did not know; neither
is a source. Read it at Frontier's own wording where they state it, and record where it came from.

## 5. Two smaller ones that the sampling item rests on

- **Does `Status.json`'s `GuiFocus` distinguish the DSS panel**, and what are the values? `GameStatus`
  parses no `GuiFocus` today.
- **How closely does a `ScanOrganic` line track the position `Status.json` held at that moment?**
  `ScanOrganic` carries no latitude or longitude, so d47 must stamp one itself, and the spacing
  figure is only as good as that correlation. Measure the write-to-write gap across the corpus; if
  it is wide, the item quotes a distance **with its uncertainty** rather than a bare number.

---

# The items

## Read a system name

A rule and a string. No network, no index, no upload — which is what makes it work in the black,
where the first-footfall money is.

- Decode the mass code from any system name: the current one from the journal, or one the Commander
  reads off the Galaxy Map and says out loud.
- The `a`–`h` ordering and the ladder of boxel sizes come from a **recorded source**, not from
  memory. `a` = 10 ly and `c` = 40 ly are confirmed; the rest is sourced or omitted.
- **What the code means and what it is worth are two claims.** Only the first is settled today.
  Question 3 decides whether the second is said at all.
- Star class rides along: a variant's colour follows the star, and the variant sets the price.

## Find the exobiology

- The planner, if question 1 says there is one: a circuit or the next few hops.
- The arithmetic after a DSS scan, which needs no prediction at all — `SAASignalsFound` names the
  genera, so it is a lookup and a sum.
- Everything from a crowd-fed index carries its age and its origin, as `StockLastSeen` does.

## Exobiology sampling

The original item, with three things that were not in it:

- **d47 reads no position.** `GameStatus` names `HasLatLong` and parses fuel, cargo, heat, body and
  balance — no latitude, no longitude. That plumbing comes first, with the body radius off `Scan`,
  and the distance is a great circle rather than a straight line.
- **`ScanOrganic` carries no position either**, so d47 stamps one from `Status.json` as the event
  lands. See question 5.
- **Per-body state that outlives a session** — the first of its kind here. What has already been
  sampled on this body, keyed by body and Commander.

**What this cannot be:** reacting to the signal filter selected in orbit. `GuiFocus` says which
panel is open and `SAASignalsFound` says what the body holds; nothing says which entry is
highlighted, and in VR the panel is in world space, so there is no fixed place on the screen to
look. The reachable half is naming the genera and asking which is being hunted — one question,
remembered, working identically in the headset. The screen-reading branch is Phase 21 and is
deliberately not a dependency of anything here.

---

## Verification

- `dotnet build` (warnings are errors) and `dotnet test`, including `CoreDependencyTests` and
  `DocumentationGateTests`.
- **Generators are run, not mocked**, and print join failures. A run that resolves everything
  silently is the suspicious one.
- Any figure quoted in an answer traces to a table with a generator behind it, a live-service
  response recorded in the spike, or a web search made at the time — never to prose in this file.
- The mass-code decoder is tested against real names including the ones that break naive parsing:
  hand-named systems with no code at all, and permit-locked oddities.

## Order and dependencies

The spike gates everything except the position plumbing, which can start immediately and is the
longest pole in *Exobiology sampling*. *Read a system name* needs only question 3 and is otherwise
free-standing — it is the shortest path to something useful, and the only item that works with no
network at all. *Find the exobiology* needs question 1 for its route half and nothing for its
arithmetic half, so it can ship in two pieces. Nothing here needs Phase 21.

**Release:** these four items are part of Phase 17, not all of it — colonisation and the prospector
callouts are still open, so finishing them is a patch and the minor waits for the phase. The two
warnings that used to sit here moved to Phase 15, so they no longer gate this one and it no longer
gates them. The maintainer has flagged colonisation as closer to engineering in complexity and it
gets its own plan.
