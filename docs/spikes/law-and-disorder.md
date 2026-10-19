# Pending: Frontier's Law and Disorder update

**Status: announced, not shipped.** Frontier plans this for December 2026. Their dates often move,
and the details below may change before release. Everything here was read on 2026-10-19 from the
sources below. When the update ships, check each item against the release notes and the journal
documentation before building on it.

## Sources

| Tag | Source |
| --- | --- |
| GNU | Galnet News Update, "19 October 3312", YouTube. The maintainer supplied the transcript on 2026-10-19. |
| MOP | [Massively Overpowered, 2026-10-15: "Elite Dangerous wants crime to actually pay for players with its December Law and Disorder update"][MOP], which cites Frontier's September developer log. |

Neither is a Frontier document. No journal event names or fields have been announced.

## What is announced

### Evidence and the Unconfirmed Bounty

- A crime counts only if someone witnesses it. A criminal record needs an active scan of the
  person who committed it.
- A witnessed crime gives a temporary local **Unconfirmed Bounty (UCB)**. Witnesses may attack the
  offender and are paid a bounty if they kill them.
- The UCB becomes permanent only when the authorities scan the offender. Leaving before the scan,
  or leaving no living witnesses, means no record.
- The offender is told when they carry a UCB.

### Scanning in space

- The **Kill Warrant Scanner is removed** from all ships.
- On arrival at a location, a ship passively scans every ship nearby and learns its name, ID,
  faction, power and any UCB. A ship with a UCB may be attacked without an active scan.
- An active scan adds the pilot's name, legal status and aggregated bounties, and is shared with
  every ship nearby.
- A crime in space alerts the system authorities, whose response time and numbers vary.

### Settlements

- An unobserved kill or theft gives no bounty, even after a scan. Goods identifiable as the
  settlement's (research samples are the example) are the exception.
- An observed crime leaves the choice of taking the bounty or killing every witness before it is
  reported. Frontier has not given the details.

### Starports

- **Anonymous Access is removed.** Known criminals cannot dock.
- A pilot who gets docking permission and reaches the pad without being scanned has every
  station service.
- A scan that finds a bounty, or a crime inside the no-fire zone, cancels docking permission.

### Five tiers of criminality

- Criminals are sorted into five tiers by the accumulated size of their crimes.
- Small crimes are small fines, cleared by paying on the spot or when asked. An unpaid small fine
  no longer becomes a bounty.
- **Loitering is no longer lethal.** Traffic control offers to fly the ship out of the station.
- Deployed weapons or accidental fire in a no-fire zone get a warning first, then a fine.
- Higher tiers: the authorities demand immediate payment and turn hostile on refusal. The top
  tier is shot on sight.

### Paying fines

- **Interstellar Factors is abolished.** Most fines are paid at any starport with the right
  facility, to any faction.
- **Notoriety is abolished for Commanders**, so there is no wait before paying.
- The top two tiers cannot pay this way. They pay a premium to a **Record Cleaner** in a
  criminal-controlled port.
- **Hot ships and hot modules are removed**, and existing records are deleted. Crimes belong to
  the Commander, not the ship.

### Piracy and smuggling

- A ship used for successful piracy gains **notoriety as a pirate ship**. It makes later piracy
  easier and makes the authorities more likely to scan it. A clean ship gets the benefit of the
  doubt.
- A new comms option lets a pirate demand cargo, alongside hatch breaker limpets. The victim
  decides from both loadouts, the pirate ship's notoriety and the pirate's honour: a pirate who
  takes the cargo and kills the victim anyway finds the next victim less willing.
- Pirated cargo still sells at black markets. In criminal-controlled systems it also sells on the
  open market at a much better price.
- Smuggling gains from changes to scanning and silent running. No details yet.

### Crimes between Commanders

- The Pilots' Federation runs its own tiers for Commander-on-Commander violence. The bounty scales
  with the gap in skill and in ship lethality: a Sidewinder destroying a Corvette earns far less
  than the reverse.
- Turning off crime reporting for consensual combat matters more.

### Powerplay

- Legitimate Powerplay activity is not a crime. Attacking another Power's ships is warfare and
  draws no bounty, though the authorities may still treat the attacker as hostile. Firing on the
  authorities is a crime.
- Powerplay assignments to commit crimes must target civilians, not Powerplay opponents, and earn
  ordinary bounties.
- A Power can limit where warrants against its own pilots apply. As described, a crime committed
  in an enemy Power's territory is not held against the pilot in their own Power's systems, even
  against the same faction. Frontier has not given the details.

### Anarchy becomes Criminal

- No inhabited system is labelled **Anarchy** any more. The label is kept for uninhabited systems.
- Inhabited anarchies become **Criminal** systems with security forces and authority vessels that
  work for the gangs running the system. They issue bounties, and handing those in supports the
  criminal faction.
- These authority vessels may demand part of a valuable cargo as a toll from a pilot with low
  reputation with the controlling faction.

## What d47 reads today that this touches

A sweep of the tree on 2026-10-19. Paths and symbols are as they were that day. "Wrong" means the
announcement contradicts it; "check" means it depends on what Frontier actually ships, most of all
on whether the crime journal events and `Status.json` fields change.

### Wrong once it ships

- **The Crime rating's note.** `AdventureCapability.cs` (the `crime` rating's note) and
  `docs/capabilities/adventures.md` (`#rating-crime`) say the rating has no effect until this
  update. `CareerWeights` pins Crime to 50 (#1309); `HomeSystemRanking` and `HomeSystemGatherer`
  give Crime no score (#1312). Deciding what Crime's rating does is the point of this update for
  d47.
- **Anarchy as the place for crime.** `AdventureGenerator` lists Anarchy systems for illegal beats,
  tells the model an Anarchy-run target reports no crime, and refuses illegal beats outside
  Anarchy (`IllegalOutsideAnarchyAsync`, `IsAnarchy`). `AdventureCallout` says an Anarchy-run
  settlement leaves you clean. Pinned by `IllegalMissionsAreSteeredToAnarchyTests` and
  `ACrimeIsWarnedOnceAtItsSettlementTests`. Docs: `adventures.md` (illegal families steered to
  Anarchy), `on-foot.md` (illegal goods refused outside Anarchy). Nothing handles a "Criminal"
  government or security yet.
- **Notoriety.** `JournalCapability.DescribeCrimeStatus` reads `Crime.Notoriety` from
  `Statistics` and speaks it; the `get_crime_status` description and `journal.md` name it.
- **Hot modules.** `ModuleStore` reads `Hot` from `StoredModules` and appends ", hot". A missing
  field reads as false, so this goes quiet rather than failing.
- **Kill Warrant Scanner.** Rows in the generated `Engineers.tsv` (unlock requirements),
  `Blueprints.tsv` (KWS modifications), `EliteSpecifications.tsv` (`hpt_crimescanner_*`), and the
  names in `OutfittingCatalogue` and `ShortNames`. Regenerate from the `tools/gen-*.py` scripts once
  Frontier's data changes; `gen-elite-specs.py` does not name these rows, so check how they arrive.
- **Interstellar Factors.** `docs/capabilities/callouts.md` says a fine is cleared at an
  Interstellar Factors contact or security office. `StationServices` maps `facilitator` to
  Interstellar Factors, and the station page, `StationQuery`, `QuickSearches` and `galaxy.md`
  offer it as a service to search for.
- **The fines and bounties ledger.** `OutstandingCrimes` tracks `CommitCrime` against
  `PayFines`/`PayBounties` per faction, and `OutstandingCrimesCallout` warns on arrival. Paying
  anywhere, the five tiers, unpaid fines no longer becoming bounties and the Record Cleaner all
  change what it should say. Pinned by `UnpaidFinesAreSaidOnArrivalTests`.

### Check once it ships

- `CommitCrime`, `CrimeVictim`, `PayFines`, `PayBounties`, `PayLegacyFines` and `Bounty` in
  `HandledEvents`, `JournalSentence`, `CreditLedger`, `HistoryBackfill`, `SceneTracker` and
  `JournalScrub`. A UCB, a tier or a Record Cleaner payment may arrive as new events or fields.
- `LegalState` in `Status.json` (`GameStatus`), `Wanted`/`ActiveFine` on `Docked` and
  `LegalStatus` on scans (`JournalFields`, `EventReadings`). The passive arrival scan may add events.
- `SystemSecurity` and `SystemGovernment` strings shown raw; the galaxy search choices in
  `GalaxyQuery` (government "Anarchy", security "Anarchy") and the "anarchy outbreak" quick search.
- Piracy: `AnnouncedAttackCallout` keys on the pirate's "demanding cargo" message; the new
  demand-cargo comms may change it. Hatch breaker rows in `OutfittingCatalogue` and `Engineers.tsv`.
- Missions: the illegal, Piracy and Smuggle rows of the generated `Missions.tsv`
  (`curated_missions.py`), `MissionFamilies.IsIllegal`, and the Crime row of `ChapterActivities`.
- Powerplay: `PowerplayRanks` and the generated `PowerplayRanks.tsv` "bounties placed on you" and
  "black market profits" rows.
- Station docking denial `Reason` values (`JournalSentence`, `DockingCallout`, `StoryDirector`).
- NPC message regexes in `NpcChatter` for "fined", "wanted" and "bounty on you".

Nothing in d47 reads Anonymous Access, loitering or Pilots' Federation bounties today.

[MOP]: https://massivelyop.com/2026/10/15/elite-dangerous-wants-crime-to-actually-pay-for-players-with-its-december-law-and-disorder-update/
