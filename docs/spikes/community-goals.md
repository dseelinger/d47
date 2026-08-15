# What the journal already knows about community goals

**Measured 2026-08-15** against the same 912-journal corpus as
[journal-corpus-engineering.md](journal-corpus-engineering.md) — 3 July 2025 to 11 August 2026, nine
Commanders.

This exists because list.md Phase 14's *Know the current community goals* is written around a split
— goals the Commander has joined against goals they have not — and **the corpus says that is not the
split the journal makes**.

| Event | Count |
|---|---|
| `CommunityGoal` | 13,341 |
| `CommunityGoalJoin` | 13 |
| `CommunityGoalReward` | 11 |
| `CommunityGoalDiscard` | 1 |

## 1. The journal carries goals the Commander has *encountered*, not ones they joined

Across 16,999 goal entries inside those events, **952 carry `PlayerContribution: 0`** — so the list
is plainly not restricted to goals the Commander signed up for. Ten distinct goals appear in
thirteen months, and the ones with the most zero-contribution sightings are the ones the Commander
merely kept docking near:

```text
Protect Panther Clipper Mk II Shipments from Pirate Threats     307 sightings at zero
Support Panther Clipper Mk II Launch with Critical Deliveries   246
Zorgon Peterson Exobiology Initiative                           174
```

`CommunityGoal` fires off the **board at a station**, so it reports what is offered where the
Commander happens to be. The line an external source has to cover is therefore **"everywhere I have
not been"**, which is wider than "everything I have not joined" — and it is the wording the setting
row should use, because a Commander who has visited one bubble station has not thereby seen the
galaxy's community goals.

Ten goals in thirteen months is also a fair measure of how little the journal alone would surface.

## 2. One event carries the whole board

No key needed, and it is richer than the item assumes:

```json
{ "event":"CommunityGoal", "CurrentGoals":[ {
  "CGID":840, "Title":"Distant Worlds III Deep-Space Infrastructure Project",
  "SystemName":"Alrai", "MarketName":"Surveyor's Reach",
  "Expiry":"2026-01-17T16:00:00Z", "IsComplete":true,
  "CurrentTotal":313369977, "PlayerContribution":16064, "NumContributors":18516,
  "TopTier":{ "Name":"Tier 5", "Bonus":"" }, "TopRankSize":10,
  "PlayerInTopRank":false, "TierReached":"Tier 3",
  "PlayerPercentileBand":50, "Bonus":120000000 } ] }
```

`TierReached` and `TopTier` are the tiers list.md asks for; `PlayerPercentileBand` and
`PlayerInTopRank` are the Commander's standing; `NumContributors` and `CurrentTotal` are the shape of
the effort. All of it is already on disk.

## 3. The trap: it is a board snapshot, not a list of live goals

**The event quoted above is dated 2026-01-21 and the goal expired 2026-01-17.** Four days stale, and
carrying `IsComplete: true`.

So `CurrentGoals` must be **filtered on `Expiry` against injected time** rather than read back as
current. Announcing a finished community goal as something the Commander can still contribute to is
a wrong answer that reads exactly like the feature working — and given the event fires every time
they dock somewhere, the stale entry is the common case rather than the edge one.

The same reasoning that makes `StockLastSeen` carry its age applies here, and the wording already
exists in the repo for it.

## 4. What was still open, and how it was settled

Both of these were open when this was written and both were closed by the implementation on
2026-08-15. Kept here rather than deleted, because the *reasoning* is the finding.

- **Whether `CurrentGoals` replaces or merges.** It looks like a complete board per event, which
  would argue for replacing — but it is the board at *one station*, and no station in this corpus
  ran more than one goal at a time, so the two-stations case is untested. **It merges by `CGID`.**
  Replacing on an untested assumption loses a goal the Commander is actually running; merging keeps
  a goal that has ended, which the expiry check in §3 already has to handle because the snapshot
  goes stale regardless. Only one of those two failures is silent.

- **The INARA wire shape.** Read at source rather than measured, since the corpus has no key in it:
  `getCommunityGoalsRecent`, one endpoint, an envelope of a `header` and an `events` array. Three
  things in it are worth carrying:
  - **The response has no `CGID`.** `communitygoalName`, `starsystemName`, `stationName`,
    `goalExpiry`, `tierReached`, `tierMax`, `contributorsNum`, `contributionsTotal`, `isCompleted`,
    `lastUpdate`, `goalObjectiveText`, `goalRewardText`, `goalDescriptionText`, `inaraURL` — and no
    id. So the only field the journal and the listing can be matched on is the goal's *name*, which
    is exactly the field two sources spell differently. Merge on an exact match and let anything
    else appear twice: a duplicate is visible, a wrong merge is silent.
  - **`tierMax` is 0 when unknown**, because the journals carry no maximum tier and the site's own
    worked example shows a journal-sourced entry reading zero. Read literally, it announces a goal
    whose top tier is zero.
  - **HTTP 200 is not a success.** A rejected key, a malformed request and "no results" all arrive
    as 200 with the real status inside the body — `400` on the header cancels the batch, `204` on
    the event means there is nothing to report. Reading the transport code would report a bad key
    as an empty board, which is the one wrong answer here that looks exactly like a right one.

  The event is usable with the site's generic application key. d47 does not use one: it ships as a
  public binary with its source beside it, so a key baked in would be a published key.

## 5. How to re-measure

Filter journals to `"event":"CommunityGoal"` and read `CurrentGoals`. The corpus is a Commander's own
play history and is not in this repository; the recipe in
[journal-corpus-engineering.md](journal-corpus-engineering.md) §7 applies unchanged.
