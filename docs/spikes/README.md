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

## The source ledger

| Source | Licence | Usable? | What it is good for |
|---|---|---|---|
| **EDCD/FDevIDs** | no licence file | yes, by precedent | The ids and symbols the journal writes. Ships, modules, materials, micro-resources, engineers. **No suit or weapon list exists.** |
| **EDCD/coriolis-data** | code MIT; **JSON declared Frontier's IP** | yes, by precedent | Ship and module figures. **Ships only** — no suits, weapons or micro-resources. |
| **msarilar/EDEngineer** | **MIT** | yes | The whole on-foot vocabulary: mods, upgrade recipes, ingredients, engineer attribution, micro-resource origins. **Its Odyssey unlock quantities are a patch stale.** |
| **EDDiscovery/EliteDangerousCore** | **Apache-2.0** | yes | Found 2026-08-15 through [edcodex.info](https://edcodex.info). The things nothing else had: the **engineer referral graph** with the grade needed at the referrer, the **suit list** keyed on what `SuitLoadout` writes, the **hand-weapon list**, and a `MaterialGroupType` that is the material **line** the trade rate depends on. Data is **C# source rather than data files**, and carries per-figure provenance including visible guesses. |
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

**Decision, 2026-08-15: the maintainer accepts these terms**, and Phase 14 `#102` proceeds on that
basis rather than waiting. The link is recorded here rather than summarised so that any attribution
wording owed is checked against the source instead of recalled — and `NOTICE` remains the single
place that wording lives, per CLAUDE.md. The thread is a forum URL and answers **403** to an
automated fetch, so it needs a browser.

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
