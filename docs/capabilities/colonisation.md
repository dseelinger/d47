# Construction tracking

What your construction sites still need, what you are already carrying towards them, and what is
left to haul.

> "what does my construction site still need"
> "what is left to deliver"
> "how far along is the construction"
> "what am I carrying for the build"

This needs no key, no account and no network. It is subtraction over data Elite has already written
to your disk — the shortest supply chain of anything d47 ships.

## The arithmetic you would otherwise be doing on paper

```text
Ratraii Construction Site, Ratraii — 25.7% built, seen 2026-08-16 10:00 game time.

3 commodities outstanding, 687 tonnes in all:
  Aluminium — 400 tonnes left, 300 tonnes in the hold, 100 of 500 delivered.
  Ceramic Composites — 278 tonnes left, 112 of 390 delivered.
  Food Cartridges — 9 tonnes left, all of it in the hold, 231 of 240 delivered.

309 tonnes of that is already in your hold, leaving 378 tonnes to find.
At 720 tonnes a run, that is 1 more full load.
Your carrier was holding 656 tonnes of cargo as of 2026-08-16 09:45 game time. Elite does not
write what those tonnes are, so I cannot tell you how much of it belongs on this manifest.
You have delivered 100 tonnes here since I started reading this session, across 1 commodity. The
delivered figures above are everybody's.

A site reports only while you are docked at it, so these are its figures from your last visit
rather than live ones. Others may have delivered since.
```

Every number there comes off one of three places: the site's own manifest, `Cargo.json`, and your
ship's cargo capacity from its `Loadout`.

## The one thing to know: these figures are from your last visit

A construction site reports its manifest **only while you are docked at it** — 6,307 of 6,330 events
measured across a 912-journal corpus. So what d47 holds is a record of where you have been, not a
live feed. Somebody else may have delivered ten minutes ago and nothing on your disk says so.

That caveat is on the end of every answer rather than buried in a settings page, because this is the
one way this capability can be wrong while looking right: the arithmetic is exact, and the moment it
describes has passed.

The good news is the manifest itself has no traps in it. `ColonisationConstructionDepot` is a
**snapshot, not a delta** — measured over 6,330 events and 120,208 resource rows, with
`RequiredAmount` never moving mid-build and `ProvidedAmount` never once decreasing across 119,887
consecutive comparisons. So "what is left" is one subtraction over the latest event, with no history
to keep:

```json
{ "timestamp":"2025-12-17T22:23:00Z", "event":"ColonisationConstructionDepot",
  "MarketID":3960809986, "ConstructionProgress":0.056927,
  "ConstructionComplete":false, "ConstructionFailed":false,
  "ResourcesRequired":[
    { "Name":"$aluminium_name;", "Name_Localised":"Aluminium",
      "RequiredAmount":500, "ProvidedAmount":0, "Payment":3239 } ] }
```

`Name_Localised` is on every one of those 120,208 rows, which is why this needs no commodity table
at all — not even the one d47 already ships for other things.

## Several sites at once

Three were open simultaneously in the corpus, so this is a list rather than a "current site". With
one under construction, asking what is needed just answers; with several, d47 names them and asks
which. You can also name one directly — by its station or its system, whichever you would say out
loud.

A finished site keeps reporting for a while — 2 to 60 more events after it completes — so
**completion is the `ConstructionComplete` flag and never "the events stopped"**. Complete and failed
sites are out of the way unless you ask for them.

## What is in your hold

This comes from `Cargo.json` rather than from the journal, and that is not a preference. The `Cargo`
event carries its full `Inventory` on only **1,151 of 13,762** occurrences measured — essentially the
first one in each session — and the other 12,611 carry a bare tonnage. A journal-only reader would
have a correct manifest for the first minute of a session and a stale one for the rest of it. Elite
rewrites the file on every change, which is the same reason your backpack and ship locker are read
the same way.

Two things d47 is careful about here:

- **Carrying more than is wanted counts only what is wanted.** Nine hundred tonnes of aluminium
  against six outstanding is six tonnes of progress, and a line reading "900 in the hold" beside "6
  left" would invite a trip that buys nothing.
- **The SRV has its own hold**, and Elite rewrites the same file for it. So the vessel is carried
  rather than assumed — eight tonnes of scoopings reported as your ship's four hundred would be a
  wrong answer nobody could see was wrong.

## What is on your carrier: a tonnage, and no manifest

d47 will tell you how many tonnes your carrier is holding, and will not tell you what they are. That
is a measured refusal rather than a gap.

`CarrierStats.SpaceUsage.Cargo` is a real number Elite publishes. Nothing it writes says what those
tonnes consist of. The only per-commodity signal is `CargoTransfer`, and building a stock model from
it was tried against the corpus and reconciled against the game's own total:

| Check | Result |
|---|---|
| Derived stock matched the reported tonnage | **347 times** |
| Derived stock was wrong | **679 times** |
| Commodities driven *negative* | 11 — transfers out of stock d47 never saw arrive |

Cargo reaches a carrier by routes the journal never itemises: its own commodity market, another
Commander's delivery, anything loaded before the file d47 is reading. So an itemised carrier
manifest would be wrong twice as often as right, and it would look authoritative every time. The
tonnage is the honest half.

## Your deliveries, apart from everybody else's

`ProvidedAmount` on the manifest is what *everybody* has handed in. On a build several Commanders are
hauling for, "did my run land" is a different question, and `ColonisationContribution` answers it.
Summed across the session d47 has been reading — so it is your deliveries since d47 started, not
your career's, and the answer says so.

This is where the commodity names nearly went wrong. Elite spells the same commodity three ways:

| Where | How it is written |
|---|---|
| `ColonisationConstructionDepot` | `$aluminium_name;` |
| `Cargo.json` | `aluminium` |
| `ColonisationContribution` | `$ComputerComponents_name;` |

Mixed case on **30 of 30** distinct symbols the contribution event writes, against 0 of 31 for the
depot and 0 of 64 for the hold. A normaliser that stripped the `$` and the `_name;` and stopped there
would join the depot to the hold perfectly and match **no contribution against anything** — reporting
a delivery you had just completed as never having happened. Names are folded to lowercase for exactly
that reason.

## What this does not do

**Planning** — an objective, which facilities to build and in what order — is not here. It lives on
the [checklist](checklists.md), beside your ship builds, because a colonisation build is the same
shape of long-lived intent as an engineering one. There is also no costed facility table anywhere in
d47: the figures exist only inside GPL-3.0 source or an unlicensed community spreadsheet, so there is
no licence-clean route to them. See
[the colonisation spike](https://github.com/dseelinger/d47/blob/main/docs/spikes/colonisation-sources.md).

**Predicting a site's manifest before you have seen it** is the same problem, and for the same
reason. A site tells d47 what it wants; d47 does not guess.

## Tools

### `get_construction_sites`

Every site your journal has reported: where it is, how far along it is, how many commodities are
outstanding, and when you last saw it.

```json
{"type":"object","properties":{"include_finished":{"type":"boolean","description":"Also list sites that are complete or have failed. Default false \u2014 a finished site cannot be hauled to."}},"required":[],"additionalProperties":false}
```

### `get_construction_needs`

The hauling list for one site: every commodity still outstanding, how much is left of each, how much
is already aboard, and how many runs the ship's capacity implies.

```json
{"type":"object","properties":{"site":{"type":"string","description":"The station or system name of the site. Leave out when only one is under construction."}},"required":[],"additionalProperties":false}
```
