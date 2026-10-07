# Spike: what Frontier's Companion API offers

The desk half of #615. Nobody has called the API for this page. It decides whether the live probe
(#616) is worth doing and lists what that probe has to check.

## Sources

There is no official endpoint specification from Frontier. The community documentation below is
what the page relies on. It describes itself as the last known information, subject to change
([EP:127-129][EP127]). All of it was read on 2026-10-07, and line numbers refer to that day's copies.

| Tag | Source |
| --- | --- |
| EP | [Athanasius/fd-api, `docs/FrontierDevelopments-CAPI-endpoints.md`][EP]. The canonical copy is [EDCD/FDevIDs, `Frontier API/`][FDEV]. The two differ only in the wording of the SRV and fighter `launchBays` entry. |
| OA | [Athanasius/fd-api, `docs/FrontierDevelopments-oAuth2-notes.md`][OA] |
| EDMC | [EDCD/EDMarketConnector][EDMC] `companion.py`, `ChangeLog.md`, `constants.py`, `protocol.py` on `main` |
| FAQ | [Frontier's OAuth2 instructions, hosting.zaonce.net][FAQ]. This is the only Frontier-written page that could be fetched: the Frontier forum and elitedangerous.com returned 403. |
| Disk | The Commander's journal folder, `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\`, which holds 1,014 journal files plus `Market.json`, `Shipyard.json`, `Outfitting.json`, `ModulesInfo.json`, `Cargo.json`, `ShipLocker.json`, `Backpack.json`, `FCMaterials.json`, `NavRoute.json` and `Status.json`. |

## Endpoints

The CAPI root lists seven endpoints: `/profile`, `/market`, `/shipyard`, `/communitygoals`,
`/journal`, `/fleetcarrier` and `/visitedstars` ([EP:71-122][EP71]). No source lists a squadron
endpoint, and EDMC's endpoint enum has none either ([companion.py:535-541][EDMC535]). Every call
carries `Authorization: Bearer <access token>` ([EP:7-14][EP7]).

All event names below appear in `HandledEvents` (`src/D47.Core/Journal/HandledEvents.cs`).

| Endpoint | What it returns | What d47 already reads | What it adds | Serves |
| --- | --- | --- | --- | --- |
| `/profile` | `commander` (name, id, docked, alive, credits, debt, current ship id, ranks), `lastStarport`, `lastSystem`, `ship` (value, health, modules, engineering, launch bays) and `ships`, "all ships the Commander owns" in the same format as `ship` ([EP:124-236][EP124]). | `LoadGame`, `Rank`, `Progress`, `Reputation`, `Statistics`, `Location` and `Docked` are in `ActedOn`. `Loadout` gives the current ship's modules and engineering. `StoredShips` gives each stored ship's type, name, value and location, but no modules (read on disk). `LoadoutStore` keeps the last `Loadout` of every ship flown since d47 was installed. | The modules of stored ships that have not been flown since d47 was installed, if `ships` fills them in. The documentation implies it does but gives no example; this is probe question 5. | The Fleet loadouts that `LoadoutStore` keeps. No open issue. |
| `/market` | The last docked station's `id`, `name`, `outpostType`, `imported`, `exported`, `services`, `economies` and `prohibited`, plus `commodities[]` with prices, stock, demand, brackets, `legality` and `statusFlags` ([EP:547-596][EP547]). | `Market` (`MarketBook` reads `Market.json`). On disk, `Market.json` has prices, stock, demand, brackets and the `Consumer`, `Producer` and `Rare` flags. `Docked` has `StationEconomies` and `StationServices`. | The station's `prohibited` list and each commodity's `legality`. Market.json and `Docked` carry neither. | None. |
| `/shipyard` | The last visited shipyard's station header, `modules` (id, category, name, cost, sku, stock) and `ships.shipyard_list` (id, name, basevalue, sku, stock) ([EP:240-289][EP240]). Either list can be missing or empty at a fleet carrier or a damaged station ([companion.py:100-129][EDMC100]). | Nothing. `Shipyard` and `Outfitting` are in `NarratedOnly`, and no class reads `Shipyard.json` or `Outfitting.json`. On disk, `Shipyard.json` has `ShipType` and `ShipPrice` per ship, and `Outfitting.json` has `Name` and `BuyPrice` per module. | Stock counts and `sku`. Everything else is in companion files that d47 already has on disk and does not read. | None. |
| `/fleetcarrier` | `name` (callsign, vanity name), `currentStarSystem`, `balance`, `fuel`, `state`, `theme`, `dockingAccess`, `notoriousAccess`, `capacity`, `itinerary`, `marketFinances`, `blackmarketFinances`, `finance`, `servicesCrew`, `cargo[]`, `orders`, `carrierLocker`, `reputation`, `market`, `ships` and `modules` ([EP:306-543][EP306]). Returns 204 when the Commander owns no carrier ([EP:299-304][EP299]). | `CarrierStats` (name, callsign, docking access, fuel, jump range, `SpaceUsage`, `Finance`, `Crew` services, `ShipPacks`, `ModulePacks`), `CarrierJumpRequest`, `CarrierJump`, `CarrierLocation` and `CarrierTradeOrder`, all folded into `CarrierState`. `CarrierState` counts the carrier's hold from cargo movements (#799). | `theme`, the carrier's Livery theme (see below). `cargo[]` as Frontier lists it, where d47 counts it. `itinerary.completed`, the carrier's past jumps. `servicesCrew` salaries and factions, `carrierLocker`, `marketFinances` and `reputation`. | #308 (`theme`). The counted hold in `CarrierState` (#799, which `cargo[]` could check or replace). |
| `/communitygoals` | "Details on all currently active Community Goals and any contributions from this Commander" ([EP:97-101][EP97]). No source documents its fields. | `CommunityGoal`, `CommunityGoalJoin`, `CommunityGoalReward` and `CommunityGoalDiscard` (`CommunityGoalBoard`) cover goals the Commander has seen. When an Inara key is stored, `InaraCommunityGoalService` implements `ICommunityGoalService` for goals the Commander has not seen. | Possibly every active goal without an Inara key, which would make `/communitygoals` a second implementation of `ICommunityGoalService`. Whether it carries what `CommunityGoalListing` holds is probe question 6. | `CommunityGoalCapability`. No open issue. |
| `/journal` | The journal for today or a given date, with all of that day's sessions in one file. Returns 204 when the Commander did not play that day, and 206 for a partial journal: keep asking until it returns 200 ([EP:600-634][EP600]). It is not real time: query it once a session is over ([EP:43-45][EP43]). | The journal files themselves, which `JournalReader` reads on the game PC. | A journal written on another machine. | None: d47 runs on the PC that writes the journal. |
| `/visitedstars` | "A zip archive containing the player's VisitedStarsCache.dat"; status 102 means the file is still being generated ([EP:115-118][EP115]). No section documents it further. | Arrival events in the journal. | Every system the Commander has visited, including visits from before the oldest journal. | None. |

## Does `/fleetcarrier` describe the carrier's Livery (#308)?

**Partly.** The only documented field is a single theme:

> `theme`: Livery theme for the carrier.

([EP:322][EP322]). Its known values are `SearchAndRescue`, `Mining`, `Trader`, `Explorer`,
`AntiXeno` and `BountyHunter` ([EP:323-328][EP322]). No documented field names individual Livery
parts, paint or decals. The sources do not say whether the in-game Livery screen sets anything
beyond the theme, or whether CAPI reports it if it does. That goes to the probe as question 1.

The journals carry nothing that `theme` could be checked against. All 3,351 occurrences of
"livery" on disk are a station service named in `Docked`, `Location`, `ApproachSettlement`,
`CarrierJump` and related events, and none describes the Commander's carrier. `CarrierStats` carries
services (`Crew`) and packs, and `ShipPacks` and `ModulePacks` are empty arrays in the latest event.

So CAPI can give #308 the carrier's theme, and might give more. Nothing else can give it either.

## Cost

### A client ID

- A developer applies at `https://user.frontierstore.net/`. Approval is not instant. Once
  approved, the Developer Zone lists the developer's clients with their `AUTH` and `CAPI` scopes,
  and **View** shows the client ID and shared key, either of which can be regenerated
  ([OA:3-28][OA3]). The author of those notes could not document the application itself
  ([OA:6][OA3]). The sources do not say what the form asks for or how applications are reviewed.
  EDMC names `https://auth.frontierstore.net/client/signup` as the place to obtain one
  ([companion.py:271-274][EDMC271]).
- The application name matters later: Frontier asks that the `User-Agent` match
  `EDCD-[A-Za-z]+-[.0-9]+`, with the middle part aligned to the name given when applying
  ([FDevIDs README:9-19][FDEVREADME]).
- **The open-source conflict.** Frontier's FAQ says whoever obtains an API key is responsible for
  accepting Frontier's Terms and Conditions, that anyone else running an instance must obtain
  their own key, and that the key "SHOULD NOT be included in your open source code"
  ([FAQ][FAQ]). EDMC, which is open source, hardcodes its client ID and lets an environment
  variable override it ([companion.py:271-274][EDMC271], [Releasing.md:90-91][EDMCREL]). d47 is
  open source and ships one binary to every Commander. The sources do not say whether a PKCE
  client ID, used without a shared key, counts as the "API key" the FAQ means. The maintainer has
  to settle this with Frontier before #616, because the alternative is asking every
  Commander to apply for a client ID of their own.

### OAuth and token lifetimes

- An authorisation-code flow with PKCE (S256, 32-byte verifier). The authorisation endpoint is
  `https://auth.frontierstore.net/auth` with `audience`, `scope`, `client_id`, `code_challenge` and
  `redirect_uri` ([OA:53-100][OA53]), and the token endpoint is `https://auth.frontierstore.net/token`
  ([OA:143-154][OA143]). The FAQ confirms PKCE is supported ([FAQ][FAQ]). With PKCE, no client
  secret is needed ([OA:236][OA236]).
- The scope has to be `auth capi`. A `capi`-only token is refused, with a response that lists
  `email`, `firstname` and `lastname` as missing ([fd-api README:13-24][FDREADME]). So the token d47
  would hold carries the Commander's real name and email address.
- `audience`: EDMC sends `frontier,steam,epic` ([companion.py:378-381][EDMC378]). The FAQ lists
  `xbox`, `psn`, `steam`, `frontier` and `all` ([FAQ][FAQ]), and not `epic`.
- The redirect URI can be a custom scheme or a web URL, provided the user's browser can reach it
  ([OA:113-122][OA113]). EDMC registers `edmc://auth` on Windows ([constants.py:15][EDMCCONST]) and
  uses `http://localhost:<port>/auth` elsewhere ([protocol.py:339-340][EDMCPROTO]). A loopback
  listener needs no registry change. A custom scheme needs a protocol handler, which the per-user
  installer (`installer/d47.iss`, `PrivilegesRequired=lowest`) would have to write to `HKCU`.
- An access token lasts 14,400 seconds, which is four hours ([OA:246-250][OA246]). Each refresh
  returns a new refresh token ([OA:250][OA246]). No refresh token works more than **25 days after
  the first authorisation**, after which the Commander has to sign in again in a browser
  ([OA:216-220][OA216]). So d47 would send the Commander to a browser about once a month.
- If the Commander revokes d47's access, the next refresh fails with 401, but an access token that
  has not expired keeps working until it does ([OA:252-254][OA246], [OA:275-278][OA258]).

### Where tokens live

The refresh token and the current access token would go in `SecretStore` under two new names, read
through `TryGet` behind a lazy accessor, like the Inara key that `InaraCommunityGoalService` reads on
every call. Both tokens rotate, so unlike every secret stored today, they are written by d47 rather
than pasted in by the Commander.

### The egress disclosure

This would add a new entry to `EgressDisclosure` with two destinations: `auth.frontierstore.net`
for the sign-in and token refresh, and `companion.orerve.net` (or `legacy-companion.orerve.net`) for
the queries. The entry has to say:

- what is sent: the bearer token, which identifies the Frontier account;
- what comes back: the Commander's name, credits, ranks and carrier finances;
- that the token itself holds the Commander's real name and email address.

The FAQ says personal data needs a separate consent step and that players must be told what is
used and why ([FAQ][FAQ]). The disclosure and the sign-in screen have to carry that.

### Live and Legacy

The data comes from three hosts: `companion.orerve.net` (Live), `legacy-companion.orerve.net`
(Legacy) and `pts-companion.orerve.net` (beta) ([companion.py:64-66][EDMC64]).
Since Update 14, the Live host returns only Live-galaxy data ([EP:47-66][EP47]). EDMC shipped
5.6.0 for the split ([ChangeLog.md:1343-1345][EDMCCL1343]) and stopped sending Legacy data to
Inara from 2022-11-29 ([ChangeLog.md:1407-1408][EDMCCL1407]). EDMC still sends non-Live play to the
Legacy host ([companion.py:1129-1134][EDMC1129]). Whether that host still answers is not settled;
it is probe question 9. A d47 client would have to pick the host from the galaxy the journal says
the Commander is playing in.

### Rate limits and outages

- No published limit. The guidance is no more than one query a minute. Frontier has said rate
  limiting may start above two queries a second ([EP:31-45][EP31]).
- EDMC waits 60 seconds between sets of queries, and 15 minutes between `/fleetcarrier` queries,
  with a 60-second timeout on the latter ([companion.py:55-58][EDMC55]). It queries `/fleetcarrier`
  only on `CarrierBuy` and `CarrierStats`, that is when the carrier management screen opens, and
  only if the Commander has switched that on ([ChangeLog.md:977-989][EDMCCL977]).
- Status 418 means maintenance ([EP:647-651][EP640]). An expired token returns 401 since
  2019-09-17 ([EP:640-646][EP640]), but the OAuth notes still say 422 ([OA:211-214][OA211]).
- No systematic outage history was found. One example: an EDMC "Misc. Error" at fleet carriers on
  5.8.1 and later ([EDMC#2048][EDMC2048]).

A d47 client would query on `CarrierStats` with the same 15-minute floor, never more often.

### Frontier's terms

The only Frontier-written terms reached are the FAQ's: the key holder accepts the Terms and
Conditions, the key is not to go in open-source code, personal data needs a separate consent step,
and players are told how their data is used ([FAQ][FAQ]). The Terms and Conditions themselves
were not reached. The sources say nothing about commercial use, redistribution or revocation. The
maintainer reads them when applying for the client ID.

## Where a client would sit

A CAPI client is a web client like `InaraCommunityGoalService` and the Spansh services, so it
belongs in **`D47.Knowledge`**, which references only Core.

The proposed seam is **a new Core seam per need, not one for the whole API**, following
`ICommunityGoalService`:

- `IFleetCarrierSource` in `D47.Core.Knowledge`: asks for the carrier snapshot (`theme`, `cargo`,
  `itinerary`) and returns a Core record. `CarrierState` takes from it what the journal lacks.
- `/communitygoals`, if probe question 6 says it carries enough, would be a second implementation of
  the existing `ICommunityGoalService`, with no new seam.
- Signing in needs a browser and a redirect listener, so it is the App's job. `D47.App` would open
  the browser and hand the returned code to the Knowledge client through a delegate, the way
  `InaraCommunityGoalService` takes its key as a `Func<string?>`. No spoke references another.
- Every request runs on the thread pool, queued from the tick, as the Inara request does. Nothing on
  the tick waits for it.

This page proposes the seam. It does not build one.

## Questions only a live call can answer

These are for #616, using the Commander's own carrier (callsign BNH-T2F).

1. **Livery.** Call `/fleetcarrier` and record every top-level and nested key. Does any key other
   than `theme` describe Livery parts, paint or decals? Compare `theme` with the theme shown on the
   in-game Livery screen.
2. **Livery changes.** Change the theme in game, open the carrier management screen, wait out the
   cooldown and call `/fleetcarrier` again. Did `theme` change, and how long after the change?
3. **Hold.** Compare `/fleetcarrier` `cargo[]` with the hold that `CarrierState` has counted (#799)
   at the same moment. Do they match per commodity?
4. **Orders.** Compare `/fleetcarrier` `orders.commodities` with the last `CarrierTradeOrder` events.
5. **Stored ships.** Call `/profile`. Does `ships` carry `modules` for ships that are stored rather
   than flown, and do they match `LoadoutStore` for a ship that is in both?
6. **Community goals.** Call `/communitygoals` and record its fields. Does it carry each field of
   `CommunityGoalListing` (system, station, expiry, tiers, contributors, objective, reward)?
7. **Audience.** Which `audience` value does the Commander's account need: `frontier`, `steam`, or
   EDMC's `frontier,steam,epic`?
8. **Redirect.** Does a `http://localhost:<port>` redirect work without being registered against
   the client ID?
9. **Legacy host.** Does `legacy-companion.orerve.net` still answer?
10. **Expired token.** Let an access token expire and call once. Is the status 401 or 422?
11. **Token contents.** Decode the access token with `/decode` ([OA:258-283][OA258]). Which personal
    fields does it carry, so the egress entry can name them exactly?

Before any call, the client ID application has to settle the open-source question under **A client
ID** above. The probe records what the form asked for and what terms were accepted.

## Verdict

**Worth probing for `/fleetcarrier` only.** It is the one endpoint that may hold state no local
source has, and that an open issue needs: the carrier's Livery theme for #308, and a listed hold to
check #799's counted one. `/market`, `/shipyard` and `/journal` add little beyond the companion files
d47 already has on disk. `/profile` and `/communitygoals` can be checked with the same token during
the probe (questions 5 and 6), but neither justifies the client ID, the monthly browser sign-in or the
new disclosure on its own.

[EP]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md
[EP7]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L7-L14
[EP31]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L31-L45
[EP43]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L43-L45
[EP47]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L47-L66
[EP71]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L71-L122
[EP97]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L97-L101
[EP115]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L115-L118
[EP124]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L124-L236
[EP127]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L127-L129
[EP240]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L240-L289
[EP299]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L299-L304
[EP306]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L306-L543
[EP322]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L322-L328
[EP547]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L547-L596
[EP600]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L600-L634
[EP640]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-CAPI-endpoints.md#L640-L651
[OA]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-oAuth2-notes.md
[OA3]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-oAuth2-notes.md#L3-L28
[OA53]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-oAuth2-notes.md#L53-L100
[OA113]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-oAuth2-notes.md#L113-L122
[OA143]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-oAuth2-notes.md#L143-L154
[OA211]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-oAuth2-notes.md#L211-L214
[OA216]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-oAuth2-notes.md#L216-L220
[OA236]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-oAuth2-notes.md#L236
[OA246]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-oAuth2-notes.md#L246-L254
[OA258]: https://github.com/Athanasius/fd-api/blob/main/docs/FrontierDevelopments-oAuth2-notes.md#L258-L283
[FDEV]: https://github.com/EDCD/FDevIDs/tree/master/Frontier%20API
[FDEVREADME]: https://github.com/EDCD/FDevIDs/blob/master/Frontier%20API/README.md#L9-L19
[FDREADME]: https://github.com/Athanasius/fd-api/blob/main/README.md#L13-L24
[FAQ]: https://hosting.zaonce.net/docs/oauth2/instructions.html
[EDMC]: https://github.com/EDCD/EDMarketConnector
[EDMC55]: https://github.com/EDCD/EDMarketConnector/blob/main/companion.py#L55-L58
[EDMC64]: https://github.com/EDCD/EDMarketConnector/blob/main/companion.py#L64-L66
[EDMC100]: https://github.com/EDCD/EDMarketConnector/blob/main/companion.py#L100-L129
[EDMC271]: https://github.com/EDCD/EDMarketConnector/blob/main/companion.py#L271-L274
[EDMC378]: https://github.com/EDCD/EDMarketConnector/blob/main/companion.py#L378-L381
[EDMC535]: https://github.com/EDCD/EDMarketConnector/blob/main/companion.py#L535-L541
[EDMC1129]: https://github.com/EDCD/EDMarketConnector/blob/main/companion.py#L1129-L1134
[EDMCCONST]: https://github.com/EDCD/EDMarketConnector/blob/main/constants.py#L15
[EDMCPROTO]: https://github.com/EDCD/EDMarketConnector/blob/main/protocol.py#L339-L340
[EDMCREL]: https://github.com/EDCD/EDMarketConnector/blob/main/docs/Releasing.md#L90-L91
[EDMCCL977]: https://github.com/EDCD/EDMarketConnector/blob/main/ChangeLog.md#L977-L989
[EDMCCL1343]: https://github.com/EDCD/EDMarketConnector/blob/main/ChangeLog.md#L1343-L1345
[EDMCCL1407]: https://github.com/EDCD/EDMarketConnector/blob/main/ChangeLog.md#L1407-L1408
[EDMC2048]: https://github.com/EDCD/EDMarketConnector/issues/2048
