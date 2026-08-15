# docs/spikes/

Findings. Each page answers questions that were measured rather than recalled, and each exists
because getting the answer wrong would have shipped game data d47 invented.

| Finding | Question |
|---|---|
| [vr-texture.md](vr-texture.md) | Can Avalonia reach a SteamVR overlay through a shared D3D11 texture? |
| [hotas-switch-read.md](hotas-switch-read.md) | Can a desktop process read HOTAS switch positions, and what identifies a device? |
| [engineering-data-sources.md](engineering-data-sources.md) | What do the ship engineering sources contain, and what does a roll actually cost? |
| [on-foot-engineering-sources.md](on-foot-engineering-sources.md) | Same question for suits and handheld weapons, which turn out to be a different game. |
| [journal-corpus-engineering.md](journal-corpus-engineering.md) | What do 6,272 real engineering rolls say — about `Quality`, the roll table, the trade rate, and a source conflict? |
| [community-goals.md](community-goals.md) | What does the journal already know about community goals, and where does an external source actually start? |
| [material-lines.md](material-lines.md) | Can d47 derive the material trader's 32 lines, the thing the trade rate depends on and the journal cannot supply? |
| [operations-pre-engineered.md](operations-pre-engineered.md) | What does a module that arrives already engineered look like, and do the blueprint sources know it? |

The three engineering pages back Phase 14 `#102`, Phase 16 and Phase 19 in
[list.md](../../list.md). The rest of this page is what a fresh pair of hands needs to carry that
research on.

---

## The method, and the mistake it exists to stop

**A failed fetch is a fact about the fetcher, never about the galaxy.**

Three separate passes over the engineering sources concluded "no source has this", and were wrong
each time. `elite-dangerous.fandom.com` answers **402** to an automated fetch;
`forums.frontier.co.uk` and `elitedangerous.com` answer **403**. All three render perfectly in a
browser. Behind them sat the complete engineering roll table, the engineer referral graph, the
reputation price list, the material trader exchange rates and the full on-foot mod map — every one
of which had been written down as unobtainable.

A related trap, hit three times: **a search-engine excerpt is not the page.** Each time an excerpt
was checked against the page it came from, it differed — most sharply on a suit mod's figure, where
the excerpt said 33% and the page said 25%.

So the order of attack is: **browser first, then the file, then the excerpt, and never conclude
absence from a fetch failure.** If it still cannot be found, say where the search stopped rather
than that the thing does not exist.

**And read the file you already have open.** On 2026-08-15 the material trader's *lines* were
written down across three documents as existing in exactly one permissive source, parsed out of C#,
and were called this plan's single point of failure on that basis. They were also in FDevIDs
`material.csv` — in a `category` column immediately beside the `rarity` column this repo has read
since `MaterialGrades.g.cs` was first generated, added in January 2021 and never since touched. See
[material-lines.md](material-lines.md). That makes three: a user-agent check, one machine's play
history, and a column nobody looked at. Each time the conclusion was *no source has this* and each
time the honest sentence was **where the looking stopped**.

[operations-pre-engineered.md](operations-pre-engineered.md) supplies a sixth, and it is the
sharpest: the acquisition of a pre-engineered module was written down as "predates the corpus" after
a sweep of every module and purchase event in 912 journals. It was 32 seconds after a
`CommunityGoalReward`, in the same files. The sweep never searched for community goals — **and the
`list.md` item being answered says in its own text that the wiki attributes these modules to
community goals.** The looking stopped inside the document that named where to look.

That page collected a seventh on the way out, and this one was caught before it was written down:
"no outfitting listing offers an engineered module, 0 of them" — measured against the `Outfitting`
*event*, which carries no stock list at all. The stock is in `Outfitting.json` beside the journal.
The honest version of that finding is structural rather than counted, and better for it: a listing
is `Name`, `BuyPrice` and `id`, so there is no field in which engineering could be expressed.

That page is also the same lesson from the other end.
Searching EDEngineer for `PowerDistributor_PrioritySystems` returns nothing — but EDEngineer carries
no blueprint symbols at all, so that search returns nothing whatever the truth is, and the absence
is a property of the query. The cargo rack in the same table really is missing, and the way to tell
the two apart was to ask a question symbols are not involved in: EDEngineer lists **no cargo rack
blueprint under any name**. A negative result is only evidence when the search could have succeeded.

## The source ledger

| Source | Licence | Usable? | What it is good for |
|---|---|---|---|
| **EDCD/FDevIDs** | no licence file | yes, by precedent | The ids and symbols the journal writes. Ships, modules, materials, micro-resources, engineers. `material.csv` also carries the **grade** and the **trader line**, the latter overlooked until 2026-08-15. **No suit or weapon list exists.** |
| **EDCD/coriolis-data** | code MIT; **JSON declared Frontier's IP** | yes, by precedent | Ship and module figures. **Ships only** — no suits, weapons or micro-resources. |
| **msarilar/EDEngineer** | **MIT** | yes | The whole on-foot vocabulary: mods, upgrade recipes, ingredients, engineer attribution, micro-resource origins. **Its Odyssey unlock quantities are a patch stale.** |
| **EDDiscovery/EliteDangerousCore** | **Apache-2.0** | yes | Found 2026-08-15 through [edcodex.info](https://edcodex.info). The things nothing else had: the **engineer referral graph** with the grade needed at the referrer, the **suit list** keyed on what `SuitLoadout` writes, and the **hand-weapon list**. Its `MaterialGroupType` is the material **line**, but is the *cross-check* rather than the source — FDevIDs had it too. Data is **C# source rather than data files**, and carries per-figure provenance including visible guesses. |
| **Fandom wiki** | — | read-only reference | The most productive seam by far. Needs a browser (402). |
| **Frontier update notes and forums** | — | read-only reference | Authoritative and needs a browser (403). The only source that tracks patches. |
| **Inara** | — | read-only reference | Fetches cleanly. Per-engineer and per-item pages, mod effects, stat ladders. |
| **spansh.co.uk** | — | live service | The galaxy index. Wire shapes are measured, not published. |
| **jixxed/ed-odyssey-materials-helper** | source MIT, binaries EULA | **no** | Does exactly Phase 19's job, but its game data lives in a closed `ed-data-impl` artifact, not in the source tree. |
| **taleden/edsy** | **CC BY-NC** | **no** | Fails the permissive-only rule, and has no on-foot data anyway. |

## The licensing question, and the decision taken on it

EDSY's data header says the game data *"remains the property of Frontier Developments plc, and is
used here as authorized by Frontier Customer Services"*, linking to Frontier's **Elite Dangerous
media usage rules**:

> https://forums.frontier.co.uk/threads/elite-dangerous-media-usage-rules.510879/

**That is the ground every generated table in this repo stands on.** `MaterialGrades.g.cs`,
`EliteSpecifications.tsv` and `Engineers.tsv` all descend from community files whose own terms say
the game data is Frontier's, and the generator docstrings reason carefully about the *community
repositories'* licences while being silent about Frontier's — which is the one that actually
governs.

**Decision, 2026-08-15: the maintainer has read Frontier's rules and accepts them**, and the
engineering work proceeds on that basis. This closes the longest-standing open question in these
pages — and note *read*, not merely accepted: the thread answers **403** to an automated fetch, so
it took a person with a browser, which is the whole point §"The method" keeps making.

The link is recorded rather than summarised, so any attribution wording owed is checked against the
source instead of recalled — and `NOTICE` remains the single place that wording lives, per CLAUDE.md.

What the decision does not change: the use stays **non-commercial**, the data is not d47's to
relicense, and tables stay **derived by a generator with its provenance recorded** rather than
copied.

## The one blocker three spikes shared — cleared 2026-08-15

**A journal corpus with real engineering in it.** The development laptop has four journals and none
of them carry `EngineerCraft`, `MaterialTrade`, `SuitLoadout`, `UpgradeSuit` or `UpgradeWeapon`, and
all three spikes were recorded as waiting on that.

**They were waiting on one laptop, not on the world.** A second machine holds 912 journals covering
thirteen months and nine Commanders — the same corpus list.md Phase 15 already used for the NPC-comms
measurement, which nobody had connected to this. All three questions are answered in
[journal-corpus-engineering.md](journal-corpus-engineering.md): `Quality` is a cumulative fill that
completes at 0.85 rather than 1.0, the published trade rate holds exactly across 1,096 trades, and
`SuitName` does encode the grade.

**The habit worth taking from it** is the same one §"The method" states about fetching. *No data
here* was recorded as *no data anywhere*, twice, for two different reasons — a user agent check the
first time and a single machine's play history the second. Before writing that something cannot be
measured, say **where** it was looked for.

Still true and still worth knowing: `JournalSpine` tails only the newest file, so anything historical
needs a deliberate backfill rather than a folder to point at.

## What is still open

Kept per page rather than duplicated — see the "what is still unknown" section of
[engineering-data-sources.md](engineering-data-sources.md) and "what has not been found yet" in
[on-foot-engineering-sources.md](on-foot-engineering-sources.md). The largest remaining items:

- ~~**The referral graph is found but not transcribed.**~~ **Settled 2026-08-15.** It is
  machine-readable in EDDiscovery's `Items/Engineers.cs`, naming the referring engineer and the grade
  needed with them, and it agrees with the wiki's rendered per-engineer table on **37 of 38**. The
  Marco Qwent ambiguity was a scraping artefact of the collapsed table — both sources say he is
  reached through Elvira Martuuk. The one conflict, Bill Turner, is decided by journal in
  [journal-corpus-engineering.md](journal-corpus-engineering.md) §4 in the wiki's favour. The
  threshold is stated by both: **grade 3 access plus roughly half the bar to grade 4** with the
  referrer.
- **Weapon mod effects**, and a *current* source for the suit ones — those come from a 2022
  compilation that predates a recipe-changing patch.
- **Suit and weapon base stats and grade ladders.** Inara has them; not harvested.
- **The ship-locker cap** — per category or per item type. Sources conflict.

## A habit worth keeping

Community data files lag patches, and the lag is invisible. EDEngineer's unlock quantities were out
by a factor of eight because a patch changed them and nobody updated the file. The tool that solves
this problem commercially versions its game data separately from its code — `ed-data-impl` at 1.36
against an API at 1.7 — which is a fair measure of how often this data moves.

**So a generated table should carry a visible version and the date it was built**, and anything
derived from a source that does not track patches should say when it was last checked.
