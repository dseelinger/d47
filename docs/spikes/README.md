# docs/spikes/

Findings. Each page answers questions that were measured rather than recalled, and each exists
because getting the answer wrong would have shipped game data d47 invented.

| Finding | Question |
|---|---|
| [vr-texture.md](vr-texture.md) | Can Avalonia reach a SteamVR overlay through a shared D3D11 texture? |
| [hotas-switch-read.md](hotas-switch-read.md) | Can a desktop process read HOTAS switch positions, and what identifies a device? |
| [engineering-data-sources.md](engineering-data-sources.md) | What do the ship engineering sources contain, and what does a roll actually cost? |
| [on-foot-engineering-sources.md](on-foot-engineering-sources.md) | Same question for suits and handheld weapons, which turn out to be a different game. |

The two engineering pages back Phase 14 `#102`, Phase 16 and Phase 19 in
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
| **Fandom wiki** | — | read-only reference | The most productive seam by far. Needs a browser (402). |
| **Frontier update notes and forums** | — | read-only reference | Authoritative and needs a browser (403). The only source that tracks patches. |
| **Inara** | — | read-only reference | Fetches cleanly. Per-engineer and per-item pages, mod effects, stat ladders. |
| **spansh.co.uk** | — | live service | The galaxy index. Wire shapes are measured, not published. |
| **jixxed/ed-odyssey-materials-helper** | source MIT, binaries EULA | **no** | Does exactly Phase 19's job, but its game data lives in a closed `ed-data-impl` artifact, not in the source tree. |
| **taleden/edsy** | **CC BY-NC** | **no** | Fails the permissive-only rule, and has no on-foot data anyway. |

## The licensing question nobody has answered

EDSY's data header says the game data *"remains the property of Frontier Developments plc, and is
used here as authorized by Frontier Customer Services"*, linking to Frontier's **Elite Dangerous
media usage rules** (`forums.frontier.co.uk/threads/elite-dangerous-media-usage-rules.510879/`).

**That is the ground every generated table in this repo already stands on, and it has not been
read.** `MaterialGrades.g.cs`, `EliteSpecifications.tsv` and `Engineers.tsv` all descend from
community files whose own terms say the game data is Frontier's. The generator docstrings reason
carefully about the *community repositories'* licences and are silent about Frontier's, which is the
one that actually governs. Reading that thread either confirms the position or says what attribution
is owed. It is a forum URL, so it needs the browser.

## The one blocker three spikes share

**A journal corpus with real engineering in it.** The development machine has four journals and
none of them carry `EngineerCraft`, `MaterialTrade`, `SuitLoadout`, `UpgradeSuit` or
`UpgradeWeapon`. That blocks, all at once:

- what `Engineering.Quality` means — a per-roll draw or a cumulative fill
- whether observed trades match the published exchange table
- what `SuitLoadout` actually carries, and whether `SuitName` encodes the grade

It is the cheapest thing on the board: one archived journal folder unblocks all three. Note that
`JournalSpine` tails only the newest file, so anything historical needs a deliberate backfill.

## What is still open

Kept per page rather than duplicated — see the "what is still unknown" section of
[engineering-data-sources.md](engineering-data-sources.md) and "what has not been found yet" in
[on-foot-engineering-sources.md](on-foot-engineering-sources.md). The largest remaining items:

- **The referral graph is found but not transcribed.** It is a collapsed wiki table expressing a
  tree through row and column spans, and the flattened text is ambiguous about Marco Qwent.
  Deliberately left for a careful read, because its failure mode is grinding the wrong engineer.
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
