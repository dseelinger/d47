# Spike: what Frontier's Companion API offers

Two halves. The desk half (#615) read the community documentation, compared each endpoint with the
journal events and companion files d47 already reads, and listed what a live call had to check. The
live half (#616) called every endpoint on 2026-10-07 with the maintainer's account and carrier, and
answers those checks under **The live probe** below. The verdict at the end is the live one.

## Sources

There is no official endpoint specification from Frontier. The community documentation below is what
the desk half relies on. It describes itself as the last known information, subject to change
([EP:127-129][EP127]). All of it was read on 2026-10-07, and line numbers refer to that day's
copies.

| Tag | Source |
| --- | --- |
| EP | [Athanasius/fd-api, `docs/FrontierDevelopments-CAPI-endpoints.md`][EP]. The canonical copy is [EDCD/FDevIDs, `Frontier API/`][FDEV]. The two differ only in the wording of the SRV and fighter `launchBays` entry. |
| OA | [Athanasius/fd-api, `docs/FrontierDevelopments-oAuth2-notes.md`][OA] |
| EDMC | [EDCD/EDMarketConnector][EDMC] `companion.py`, `ChangeLog.md`, `constants.py`, `protocol.py` on `main` |
| FAQ | [Frontier's OAuth2 instructions, hosting.zaonce.net][FAQ]. This is the only Frontier-written page that could be fetched: the Frontier forum and elitedangerous.com returned 403. |
| Disk | The Commander's journal folder, `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\`, which holds 1,014 journal files plus `Market.json`, `Shipyard.json`, `Outfitting.json`, `ModulesInfo.json`, `Cargo.json`, `ShipLocker.json`, `Backpack.json`, `FCMaterials.json`, `NavRoute.json` and `Status.json`. |
| Probe | The live calls of 2026-10-07, listed under **The live probe**. |

## Endpoints

The CAPI root lists seven endpoints: `/profile`, `/market`, `/shipyard`, `/communitygoals`,
`/journal`, `/fleetcarrier` and `/visitedstars` ([EP:71-122][EP71]). No source lists a squadron
endpoint, and EDMC's endpoint enum has none either ([companion.py:535-541][EDMC535]). Every call
carries `Authorization: Bearer <access token>` ([EP:7-14][EP7]).

All event names below appear in `HandledEvents` (`src/D47.Core/Journal/HandledEvents.cs`). The
"What it adds" column is the desk half's expectation; **The live probe** says which of it held.

| Endpoint | What it returns | What d47 already reads | What it adds | Open issue served |
| --- | --- | --- | --- | --- |
| `/profile` | `commander` (name, id, docked, alive, credits, debt, current ship id, ranks), `lastStarport`, `lastSystem`, `ship` (value, health, modules, engineering, launch bays) and `ships`, "all ships the Commander owns" in the same format as `ship` ([EP:124-236][EP124]). | `LoadGame`, `Rank`, `Progress`, `Reputation`, `Statistics`, `Location` and `Docked` are in `ActedOn`. `Loadout` gives the current ship's modules and engineering. `StoredShips` gives each stored ship's type, name, value and location, but no modules (read on disk). `LoadoutStore` keeps the last `Loadout` of every ship flown since d47 was installed. | Expected: the modules of stored ships. The probe found `ships` carries no modules (question 4). | None. |
| `/market` | The last docked station's `id`, `name`, `outpostType`, `imported`, `exported`, `services`, `economies` and `prohibited`, plus `commodities[]` with prices, stock, demand, brackets, `legality` and `statusFlags` ([EP:547-596][EP547]). | `Market` (`MarketBook` reads `Market.json`). On disk, `Market.json` has prices, stock, demand, brackets and the `Consumer`, `Producer` and `Rare` flags. `Docked` has `StationEconomies` and `StationServices`. | The station's `prohibited` list. The probe found `legality` empty and salvage prices that `Market.json` omits (question 5). | None. |
| `/shipyard` | The last visited shipyard's station header, `modules` (id, category, name, cost, sku, stock) and `ships.shipyard_list` (id, name, basevalue, sku, stock) ([EP:240-289][EP240]). Either list can be missing or empty at a fleet carrier or a damaged station ([companion.py:100-129][EDMC100]). | Nothing. `Shipyard` and `Outfitting` are in `NarratedOnly`, and no class reads `Shipyard.json` or `Outfitting.json`. | Expected: stock counts and `sku`. The probe found stock always `-1`, and rank-locked ships and pre-engineered modules instead (question 6). | None. |
| `/fleetcarrier` | `name` (callsign, vanity name), `currentStarSystem`, `balance`, `fuel`, `state`, `theme`, `dockingAccess`, `notoriousAccess`, `capacity`, `itinerary`, `marketFinances`, `blackmarketFinances`, `finance`, `servicesCrew`, `cargo[]`, `orders`, `carrierLocker`, `reputation`, `market`, `ships` and `modules` ([EP:306-543][EP306]). Returns 204 when the Commander owns no carrier ([EP:299-304][EP299]). | `CarrierStats` (name, callsign, docking access, fuel, jump range, `SpaceUsage`, `Finance`, `Crew` services, `ShipPacks`, `ModulePacks`), `CarrierJumpRequest`, `CarrierJump`, `CarrierLocation` and `CarrierTradeOrder`, all folded into `CarrierState`. `CarrierState` counts the carrier's hold from cargo movements (#799). | Expected: `theme` for #308, and upkeep costs. The probe found `theme` does not follow the Livery screen (questions 7 and 8), and the costs filled (question 9). | #308 (not served). |
| `/communitygoals` | "Details on all currently active Community Goals and any contributions from this Commander" ([EP:97-101][EP97]). No source documents its fields. | `CommunityGoal`, `CommunityGoalJoin`, `CommunityGoalReward` and `CommunityGoalDiscard` (`CommunityGoalBoard`) cover goals the Commander has seen. When an Inara key is stored, `InaraCommunityGoalService` implements `ICommunityGoalService` for goals the Commander has not seen. | Every active goal without an Inara key, with fewer fields than `CommunityGoalListing` (question 12). | None. |
| `/journal` | The journal for today or a given date, with all of that day's sessions in one file. Returns 204 when the Commander did not play that day, and 206 for a partial journal: keep asking until it returns 200 ([EP:600-634][EP600]). It is not real time: query it once a session is over ([EP:43-45][EP43]). | The journal files themselves, which `JournalReader` reads on the game PC. | A journal written on another machine, for roughly the last five to nine weeks (question 13). | None. |
| `/visitedstars` | "A zip archive containing the player's VisitedStarsCache.dat"; status 102 means the file is still being generated ([EP:115-118][EP115]). No section documents it further. | Arrival events in the journal, back to the oldest file on disk (2025-07-02). The game also writes a `VisitedStarsCache.dat` per account under `%LOCALAPPDATA%\Frontier Developments\Elite Dangerous\<account>\` (four copies on disk), which d47 does not read. | Nothing the local file does not already hold (question 14). | None. |

## The live probe

### How it was run

- **When and who.** 2026-10-07, 16:51 to 20:55 UTC, signed in to the Frontier account of the
  Commander who owns carrier BNH-T2F. A second Commander on another Frontier account played on the
  same PC that day; the probe covers only the account that signed in.
- **The client.** A client created that day in the Developer Zone with the `auth` and `capi` scopes.
  The **Create Client** form asks for an app name, a homepage URL, the scopes, a short description
  and agreement to the terms and conditions. It has no redirect URI field. The client was listed as
  **INACTIVE** for the whole probe, and the PKCE sign-in and every call worked regardless.
- **The flow.** Authorisation code with PKCE (S256), `scope=auth capi`,
  `audience=frontier,steam,epic`, redirect `http://localhost:8616/auth` to a loopback listener,
  `User-Agent: EDCD-Directive-0.110`. The client ID was used without the shared key.
- **The calls.** 28 calls to the Live host, one to the Legacy host, one to `/decode`, one token
  exchange and one refresh, at least a minute apart, and at least 15 minutes apart for
  `/fleetcarrier`. Between calls the maintainer docked, bought and sold, set a carrier buy order and
  changed the carrier's Livery, so freshness could be measured against the journal written at the
  same moments.
- **What was kept.** Raw responses stayed in the session's scratch folder. This page records one
  response per endpoint, trimmed and with identifying values replaced (**Recorded responses**).

### Endpoint by endpoint

Response time is the time to the full body. "Filled" means present with a value, "empty" means
present as an empty string, array or object, and "absent" means the key is missing.

| Endpoint | Status | Time | Size |
| --- | --- | --- | --- |
| `/profile` (Live) | 200 | 0.6–1.3 s | 36.5 KB |
| `/profile` (Legacy) | 200 | 6.7 s | 15.4 KB |
| `/market` | 200 | 0.5–0.6 s | 14.6 KB (an on-foot settlement), 42.0 KB (an Orbis) |
| `/shipyard` | 200 | 0.6–1.0 s | 1.8 KB (no shipyard), 152.6 KB (an Orbis) |
| `/fleetcarrier` | 200 | 0.5–0.8 s | 7.4 KB |
| `/communitygoals` | 200 | 0.5 s | 1.7 KB |
| `/journal`, `/journal/YYYY/MM/DD` | 200 or 204 | 0.5–1.0 s | 96–225 KB for a day played |
| `/visitedstars` | 200 | 0.8 s | 4.2 KB zip |

**`/profile`.** Every documented field under `commander`, `lastStarport`, `lastSystem` and `ship` is
filled, including `ship.modules[*].engineer`, `WorkInProgress_modifications` and
`specialModifications` (15 of 40 slots engineered) and `ship.launchBays`. `ships` is an object keyed
by ship id, and its entries are **not** in the same format as `ship`: each has only `id`, `name`,
`value`, `free`, `shipName`, `shipID`, `station` and `starsystem`, so `modules`, `health` and
`launchBays` are absent for every ship but the current one. Undocumented: `commander.onfoot`,
`commander.mercCoins`, the ranks `soldier`, `exobiologist`, `builder` and `learner`,
`capabilities.AllowCobraMkIV`, `Horizons` and `Odyssey`, cosmetic slots in `ship.modules`
(`PaintJob`, `Decal1`, `ShipName0`, `ShipID0`, `ShipKitSpoiler`, `WeaponColour`, `EngineColour`,
`StringLights`, `VesselVoice`), and five top-level objects: `suit` (the suit worn), `suits` (every
suit owned), `loadout` (the on-foot loadout in use, with each weapon's ammunition and every slot),
`loadouts` (every saved on-foot loadout) and `squadron` (`name`, `tag`, `rank`, `joined`). Weapon
`modifications` read `["NYI"]`.

**`/market`.** All documented fields are present. `prohibited` is an empty array at a station that
bans nothing and an object of id to name at one that does. Every commodity's `legality` is empty and
every `statusFlags` is an empty array, at both stations. The documented `categoryName` arrives as
`categoryname`. `services` carries four undocumented keys: `socialspace`, `powerplay`,
`registeringcolonisation` and `livery`.

**`/shipyard`.** At a station with no shipyard the response is the station header alone: `modules`
and `ships` are absent. At an Orbis all documented module fields are filled, and every module's
`stock` is `-1`. Ship entries in `shipyard_list` carry no `stock` (absent), and `shipyard_list` is
an object keyed by ship name. Undocumented: `ships.unavailable_list` (each with `unavailableReason`,
`factionId` and `requiredRank`), `ships.discount_list` (`{"elite": 0.975}`), and on 25 modules the
fields `nameEngineered`, `recipeName`, `recipeLevel`, `modifiers`, `additionalCost.mercCoins`,
`restrictions` and `tags`, under the category `mercgear`.

**`/fleetcarrier`.** Every documented top-level key is present. `theme` is the string `"None"`.
Filled: `name`, `balance`, `fuel`, `state`, `capacity`, `itinerary`, `marketFinances`, `finance`
with every cost field, `servicesCrew`, `cargo`, `reputation`, `market` and `modules`. Empty:
`orders.*`, `carrierLocker.*`, `ships.shipyard_list`, `blackmarketFinances` (all zero),
`itinerary.currentJump` (null). Absent: `servicesCrew.*.crewMember.avatarStr`. `servicesCrew` lists
three services (refuel, repair, rearm) although `finance.numServices` is 7. The commodity list
documented under `market` arrives as a top-level `commodities.commodities[]`. Undocumented:
`capacity.squadronBankTotal`, `crewMember.hiringPrice`, `modules.*.variant`, `restock` and `bundle`,
`ships.discount_list`, and six `market.services` keys (`socialspace`, `bartender`, `vistagenomics`,
`pioneersupplies`, `registeringcolonisation`, `livery`).

**`/communitygoals`.** One object, `activeCommunityGoals[]`, each with `id`, `title`, `expiry`,
`market_name`, `starsystem_name`, `activityType`, `target_qty`, `qty`, `objective`, `news` and
`bulletin`. Nothing in it is about the Commander.

**`/journal`.** Plain journal lines, one event per line, with no `Fileheader` or `Shutdown`. 204
with an empty body for a day the account did not play.

**`/visitedstars`.** `Content-Type: application/zip`, `Content-Disposition: attachment;
filename="VisitedStarsCache.zip"`, returned 200 on the first request with no 102. The zip holds
`readme.txt` (the Commander's name, the time it was generated, and instructions to back up and
replace the account's folder under `%LOCALAPPDATA%`) and `<account number>/VisitedStarsCache.dat`.

### Answers to the probe questions

1. **Shape.** Recorded per endpoint above.
2. **Capabilities.** Marked under **New capabilities each endpoint could support**.
3. **Freshness.** `/profile` and `/market` follow the game within a minute. The Commander docked at
   19:07:27 and bought fuel at 19:08:25; `/profile` at 19:09:28 had the new station and credits
   lower by 584, which is the 425 purchase plus the two `RefuelAll` events (149 and 10).
   `/market` at 19:10:36 matched `Market.json` written at 19:08:16 to the unit, except 5 fewer
   Hydrogen Fuel in stock, the 5 just bought. `/fleetcarrier` does not: a buy order set at 19:16 and
   filled by a sale at 19:21 had not appeared at 19:28 or 19:43, and the response was
   byte-for-byte the one from 16:54 apart from wages and upkeep that grow with time
   (`orders` empty, `cargo` 181 t, `bankBalance` unchanged), while `CarrierStats` at 19:16:33
   already showed 5 t reserved and 770 cr set aside. At 20:30 the sale had arrived: `cargo` held
   the 5 Hydrogen Fuel (186 t), and `bankBalance` was 770 lower. So `/fleetcarrier` caught up
   between 22 and 69 minutes after the sale, against a minute for `/profile` and `/market`.
4. **`/profile` ships.** No. `ships` has `value`, `station` and `starsystem` for every owned ship,
   and no `modules`, `engineer` or `health` for any ship but the current one, so there is nothing to
   compare with `LoadoutStore`.
5. **`/market`.** At Schrodinger Hub, `prohibited` named 11 commodities (Narcotics, Tobacco,
   Personal Weapons, Battle Weapons, Slaves, Imperial Slaves, Combat Stabilisers, Nerve Agents,
   Bootleg Liquor, Landmines, Onionhead Gamma Strain). `legality` was empty for every commodity.
   Prohibited goods appear in neither `/market`'s `commodities` nor `Market.json`, so on disk a
   banned good looks the same as one the station does not trade. `exported` equals the `Producer`
   flags exactly. `imported` is the `Consumer` flags plus 12 more: six prohibited goods and six
   salvage items. `/market` also lists seven commodities `Market.json` lacks: limpets and the
   salvage items (damaged escape pods, hostages, occupied escape pods, personal effects, black
   boxes, wreckage components) with their demand and sell prices.
6. **`/shipyard`.** Yes, more than `stock` and `sku` differ. `Outfitting.json` written at the same
   dock had 986 modules; `/shipyard` had 997, the extra 11 being armour for the `MediumTransport01`
   and `PantherMkII` hulls and a caustic missile launcher, with every shared price equal.
   `Shipyard.json` listed one ship (the Cobra Mk V); `/shipyard` listed all 34 for sale, with equal
   prices, plus four Federation ships in `unavailable_list` marked `Insufficient Rank` with the rank
   each needs (3, 5, 7 and 12), and an Elite discount of 2.5%. 25 pre-engineered modules carry their
   blueprint, grade, every modifier and a price in `mercCoins`. `stock` was `-1` on every module.
7. **Livery.** No. The in-game Livery screen shows a **Layout** (it read Victory-Class Carrier) and
   a **Concourse Theme** (it read Research). `/fleetcarrier` gave `theme: "None"`, and no key
   anywhere in the response names the layout, the concourse theme, paint or decals. "Research" is
   not one of the six documented `theme` values either.
8. **Livery changes.** At 18:56 the maintainer changed the layout to Nautilus and the concourse
   theme to Default, then reopened Carrier Management. `/fleetcarrier` at 18:57, 19:13, 19:28, 19:43
   and 20:30 still gave `theme: "None"` and no layout, though by 20:30 it had caught up with the
   carrier sale (question 3). The three `CarrierStats` events written around the change are
   identical apart from their timestamps and carry no layout or theme field.
9. **Carrier finances.** Filled: `coreCost` 5,000,000, `servicesCost` 4,486,600, `maintenance`
   9,486,600 a week, `jumpsCost`, `numJumps`, `maintenanceToDate`, `servicesCostToDate` and
   `debtThreshold` −300,000,000. `bankBalance` matched `CarrierStats.Finance.CarrierBalance` written
   at 18:48 to the credit. It did not follow the order's reservation (see question 3).
10. **Carrier hold and orders.** `cargo[]` matched the hold `CarrierState` counted from the journals
    (#799): 158 Tritium, 13 Modular Terminals, 9 Nanobreakers, 1 Meta-Alloys. After the Commander
    sold 5 Hydrogen Fuel into a buy order, `CarrierState` counted 186 t at once; `/fleetcarrier`
    said 181 t until somewhere between 19:43 and 20:30, then 186 t with the Hydrogen Fuel listed.
    The buy order filled and closed before `/fleetcarrier` ever listed it, so `outstanding` was not
    observed.
11. **Carrier locker and itinerary.** `carrierLocker` is present with `assets`, `goods` and `data`
    all empty. `itinerary.completed` lists four stops, Procyon (arrived 2026-09-13), Arque, Giryak
    and LTT 7786, which match the journal's `CarrierJumpRequest` and `CarrierLocation` events for
    those jumps. The journal holds 19 jump requests for this carrier since it was bought on
    2026-07-05, so the itinerary is a short recent window, not the carrier's history.
    `totalDistanceJumpedLY` was 246.
12. **Community goals.** Against `CommunityGoalListing`: `title` gives `Name`, `starsystem_name`
    `SystemName`, `market_name` `StationName`, `expiry` `Expiry`, `objective` `Objective`, and `qty`
    `ContributionsTotal`, with `target_qty` beside it. Absent: `TierReached`, `TopTier`,
    `Contributors` and `LastUpdate`. No field carries the Commander's contribution or percentile.
13. **Past journals.** `/journal/2026/10/02` returned the Commander's 255 lines for that day;
    against the local files it lacked only `Fileheader` and `Shutdown` and added four `Music` lines.
    `/journal/2026/09/01` matched the same way, and began at 2026-08-31T23:49Z, which is 00:49 UK
    time, so days appear to be counted in UK time. Days the Commander played returned 204 from
    2026-08-05 back: 2026-08-05, 07-01, 05-25, 03-01 and 01-02. So the window on 2026-10-07 reached
    back between 36 and 63 days. Undated `/journal` returned 204 because this account had not played
    that day.
14. **Visited stars.** A zip, served at once with status 200 and no 102. The file inside is the same
    format as the local `VisitedStarsCache.dat`, and not identical to it: it lists 855 systems to
    the local copy's 856, and every record has a visit count of 1 and the same day (2026-09-27),
    where the local copy holds real counts and days up to 2026-10-02. The format, as read here: a
    48-byte header that starts with the 12-byte signature `VisitedStars` and holds the record count
    at byte 24, then one 16-byte record per system: the system address (8 bytes), the visit count
    (4) and the day of the last visit counted from 1601-01-01 (4).
15. **Audience.** `frontier,steam,epic` worked for this account, whose token gives
    `usr.platform: frontier`. No other value was tried.
16. **Redirect.** Yes. `http://localhost:8616/auth` worked, and the Create Client form has no field
    in which to register one.
17. **Legacy host.** Yes. `legacy-companion.orerve.net/profile` answered 200 in 6.7 s, with the
    account's separate Legacy-galaxy Commander (different id, ships and credits).
18. **Expired token.** 401. The token issued at 16:51:21 with `expires_in: 14399` was used once at
    20:53:22, and `/profile` answered 401 with the body
    `{"status":401,"message":"JWT has incorrect\/unexpected fields","tag":"…"}`. The message
    does not say the token expired, so a client has to treat any 401 as "refresh, then retry once".
19. **Token contents.** `/decode` returned `iss`, `iat`, `exp` (`exp` − `iat` = 14,400 s), `scope`
    (`auth capi`), `2fa`, `crossplay` and `usr`, which holds `email`, `firstname`, `lastname`,
    `customer_id`, `platform`, `developer`, `roles`, `mfa` (`bypass_level`, `totp_enabled`,
    `passkey_enabled`, `email_enabled`) and `allowedDownloads` (48 entries). The egress entry has to
    name the email address, first and last name and customer id.

### Tokens, errors and rate limits

- The token response held `access_token`, `token_type` (`Bearer`), `expires_in` (14,399) and
  `refresh_token`, and no `scope`. A refresh at 20:53:23, after the token had expired, returned the
  same four fields with `expires_in: 14400` and a new refresh token, and `/profile` with the new
  access token answered 200. The 25-day limit on refresh was not tested.
- Apart from the expired token's 401, no call met a 418, a 429, a 5xx or a timeout, and no
  response carried a rate-limit header. Calls were one a minute at most. `/decode` set an `AWSALB`
  load-balancer cookie.

### Recorded responses

One response per endpoint. Each is trimmed: a long collection keeps its first entries and a `"…"`
entry, added here, says how many were left out, and a long text is cut with "…". These fields were
replaced with stand-ins of the same type and size, as the maintainer ruled: the Commander's name and
id, credits, the squadron's tag and joining date, every ship's name and ident, the on-foot loadout
names, the suit and weapon-rack ids, the carrier's name (`vanityName`, hex-encoded), the carrier's
balance and the journal's `FID`. The carrier's callsign, the squadron's name and rank title, and
every location are real. `/visitedstars` is described under question 14 rather than reproduced, and
`/decode` under question 19.

<details markdown="1">
<summary><code>/profile</code></summary>

```json
{
  "commander": {
    "id": 10483217,
    "name": "ARKON VALE",
    "credits": 10215047300,
    "debt": 0,
    "currentShipId": 37,
    "alive": true,
    "docked": false,
    "onfoot": false,
    "rank": {
      "combat": 1,
      "trade": 12,
      "explore": 5,
      "crime": 0,
      "service": 0,
      "empire": 2,
      "federation": 1,
      "power": 0,
      "cqc": 0,
      "soldier": 0,
      "exobiologist": 1,
      "builder": 0,
      "learner": 0
    },
    "capabilities": {"AllowCobraMkIV": false, "Horizons": false, "Odyssey": false},
    "mercCoins": 0
  },
  "ship": {
    "id": 37,
    "name": "CobraMkV",
    "value": {"hull": 1251787, "modules": 15852573, "cargo": 0, "total": 17104360, "unloaned": 66134},
    "free": false,
    "shipName": "Wayfarer",
    "shipID": "AV-08C",
    "station": {"id": 3715429376, "name": "BNH-T2F"},
    "starsystem": {"id": 50813, "name": "LTT 7786", "systemaddress": 633608311522},
    "alive": true,
    "health": {"hull": 1000000, "shield": 1000000, "shieldup": true, "integrity": 254, "paintwork": 254},
    "cockpitBreached": false,
    "oxygenRemaining": 450000,
    "modules": {
      "FrameShiftDrive": {
        "module": {
          "id": 129030588,
          "name": "Int_Hyperdrive_Overcharge_Size4_Class5",
          "locName": "FSD (SCO)",
          "locDescription": "Faster-than-light capable ship drive, featuring experimental overcharge capabilities, necessary for supercruise travel a…",
          "value": 1642282,
          "free": false,
          "health": 1000000,
          "on": true,
          "priority": 0
        },
        "engineer": {
          "engineerName": "Elvira Martuuk",
          "engineerId": 300160,
          "recipeName": "FSD_LongRange",
          "recipeLocName": "Increased FSD Range",
          "recipeLocDescription": "Longer range jumps are allowed with this modification, but at the cost of module integrity, power draw and higher mass.",
          "recipeLevel": 5
        },
        "WorkInProgress_modifications": {
          "OutfittingFieldType_PowerDraw": {"value": 1.15, "LessIsGood": true, "locName": "Power draw", "displayValue": "-15.00%", "dir": "˅"},
          "OutfittingFieldType_Mass": {"value": 1.3, "LessIsGood": true, "locName": "Mass", "displayValue": "-30.00%", "dir": "˅"},
          "…": "2 more entries"
        },
        "specialModifications": {"special_fsd_fuelcapacity": "special_fsd_fuelcapacity"}
      },
      "MediumHardpoint1": {
        "module": {
          "id": 128049460,
          "name": "Hpt_MultiCannon_Gimbal_Medium",
          "locName": "Multi-Cannon",
          "locDescription": "Rapid-fire, small-calibre projectile weapon on a gimballed mount with signature tracking assist.",
          "value": 48450,
          "free": false,
          "health": 1000000,
          "on": true,
          "priority": 0
        },
        "engineer": {
          "engineerName": "Tod 'The Blaster' McQuinn",
          "engineerId": 300260,
          "recipeName": "Weapon_Overcharged",
          "recipeLocName": "Overcharged Weapon",
          "recipeLocDescription": "This weapon modification increases power draw and heat to increase damage output.",
          "recipeLevel": 5
        },
        "WorkInProgress_modifications": {
          "OutfittingFieldType_DistributorDraw": {
            "value": 1.35,
            "LessIsGood": true,
            "locName": "Distributor draw",
            "displayValue": "-35.00%",
            "dir": "˅"
          },
          "OutfittingFieldType_ThermalLoad": {
            "value": 1.15,
            "LessIsGood": true,
            "locName": "Thermal load",
            "displayValue": "-15.00%",
            "dir": "˅"
          },
          "…": "3 more entries"
        },
        "specialModifications": {"special_incendiary_rounds": "special_incendiary_rounds"}
      },
      "…": "38 more entries"
    },
    "launchBays": {
      "Slot07_Size3": {
        "SubSlot0": {
          "name": "testbuggy",
          "locName": "SRV Scarab",
          "rebuilds": 1,
          "loadout": "starter",
          "loadoutName": "Starter"
        }
      }
    }
  },
  "suit": {
    "name": "UtilitySuit_Class2",
    "id": 128958396,
    "suitId": 7203071056372174,
    "locName": "Maverick Suit",
    "slots": [],
    "state": {"health": {"hull": 1000000}}
  },
  "lastSystem": {"id": 50813, "name": "LTT 7786", "faction": "independent"},
  "lastStarport": {
    "id": 3844488192,
    "services": {"dock": "ok", "contacts": "ok", "…": "8 more entries"},
    "name": "Archambeau's Serenity +",
    "faction": "independent",
    "minorfaction": "LTT 7786 Silver Partnership"
  },
  "squadron": {"name": "GREYBEARD DELTA", "tag": "NJ7K", "rank": "Greybeard", "joined": "2026-02-03 19:12:40"},
  "ships": {
    "37": {
      "id": 37,
      "name": "CobraMkV",
      "value": {"hull": 1251787, "modules": 15852573, "cargo": 0, "total": 17104360, "unloaned": 66134},
      "free": false,
      "shipName": "Wayfarer",
      "shipID": "AV-08C",
      "station": {"id": 3715429376, "name": "BNH-T2F"},
      "starsystem": {"id": 50813, "name": "LTT 7786", "systemaddress": 633608311522}
    },
    "49": {
      "id": 49,
      "name": "SmallCombat01_NX",
      "value": {"hull": 11709506, "modules": 16294418, "cargo": 0, "total": 28003924, "unloaned": 134493},
      "free": false,
      "shipName": "Lantern",
      "shipID": "AV-11S",
      "station": {"id": 3715429376, "name": "BNH-T2F"},
      "starsystem": {"id": 50813, "name": "LTT 7786", "systemaddress": 633608311522}
    },
    "…": "10 more entries"
  },
  "loadouts": {
    "0": {
      "loadoutSlotId": 0,
      "suit": {"name": "FlightSuit", "suitId": 5705056992307866, "locName": "Flight Suit"},
      "name": "<Default>",
      "slots": {
        "SecondaryWeapon": {
          "name": "Wpn_S_Pistol_Kinetic_SAuto",
          "id": 128937316,
          "weaponrackId": 5854660877198965,
          "locName": "Karma P-15",
          "locDescription": "A semi-automatic pistol with low recoil and a high fire rate, offset by comparatively low damage output."
        },
        "BaseSuit": {
          "name": "Humanoid_BaseSuit_FreePack01Black",
          "id": "128978336",
          "weaponrackId": 0,
          "locName": "Humanoid_BaseSuit_FreePack01Black_name",
          "locDescription": "Humanoid_BaseSuit_FreePack01Black_info"
        },
        "EVA_Helmet": {
          "name": "Humanoid_FlightSuit_Helmet",
          "id": "128982793",
          "weaponrackId": 0,
          "locName": "Humanoid_FlightSuit_Helmet_name",
          "locDescription": "Humanoid_FlightSuit_Helmet_info"
        }
      },
      "id": 128937264
    },
    "2": {
      "loadoutSlotId": 2,
      "suit": {"name": "UtilitySuit_Class2", "suitId": 7203071056372174, "locName": "Maverick Suit"},
      "name": "Quiet Approach",
      "slots": {
        "PrimaryWeapon1": {
          "name": "Wpn_M_Sniper_Plasma_Charged",
          "id": 128937319,
          "weaponrackId": 9507262999881741,
          "locName": "Manticore Executioner",
          "locDescription": "A semi-automatic, long-range plasma rifle with high damage output and a low rate of fire."
        },
        "SecondaryWeapon": {
          "name": "Wpn_S_Pistol_Plasma_Charged",
          "id": 128937281,
          "weaponrackId": 2790349105605807,
          "locName": "Manticore Tormentor",
          "locDescription": "This semi-automatic plasma pistol boasts high damage output, offset by high recoil and a low fire rate."
        }
      }
    },
    "…": "2 more entries"
  },
  "loadout": {
    "loadoutSlotId": 2,
    "suit": {"name": "UtilitySuit_Class2", "suitId": 7203071056372174, "locName": "Maverick Suit"},
    "name": "Quiet Approach",
    "slots": {
      "PrimaryWeapon1": {
        "name": "Wpn_M_Sniper_Plasma_Charged",
        "id": 128937319,
        "weaponrackId": 9507262999881741,
        "locName": "Manticore Executioner",
        "locDescription": "A semi-automatic, long-range plasma rifle with high damage output and a low rate of fire.",
        "health": 1000000,
        "value": 175000,
        "free": false,
        "slots": {
          "Optics": {
            "name": "Wpn_M_Sniper_Plasma_Charged_Default_Optic",
            "id": 128962677,
            "weaponrackId": 8216665535133086,
            "locName": "Wpn_M_Sniper_Plasma_Charged_Default_Optic_name",
            "locDescription": "Wpn_M_Sniper_Plasma_Charged_Default_Optic_info",
            "health": 1000000,
            "value": 1000,
            "free": false,
            "slots": [],
            "modifications": ["NYI"]
          },
          "PaintJob": {
            "name": "PaintJob_PlasmaSniper_Default_Paintjob_01",
            "id": 128978294,
            "weaponrackId": 0,
            "locName": "PaintJob_PlasmaSniper_Default_Paintjob_01_name",
            "locDescription": "PaintJob_PlasmaSniper_Default_Paintjob_01_info"
          }
        },
        "modifications": ["NYI"],
        "ammo": {"clip": 3, "hopper": 30}
      },
      "SecondaryWeapon": {
        "name": "Wpn_S_Pistol_Plasma_Charged",
        "id": 128937281,
        "weaponrackId": 2790349105605807,
        "locName": "Manticore Tormentor",
        "locDescription": "This semi-automatic plasma pistol boasts high damage output, offset by high recoil and a low fire rate.",
        "health": 1000000,
        "value": 50000,
        "free": false,
        "slots": {
          "Optics": {
            "name": "Wpn_S_Pistol_Plasma_Charged_Default_Optic",
            "id": 128962682,
            "weaponrackId": 7345181190717502,
            "locName": "Wpn_S_Pistol_Plasma_Charged_Default_Optic_name",
            "locDescription": "Wpn_S_Pistol_Plasma_Charged_Default_Optic_info",
            "health": 1000000,
            "value": 1000,
            "free": false,
            "slots": [],
            "modifications": ["NYI"]
          },
          "PaintJob": {
            "name": "PaintJob_PlasmaPistol_Default_Paintjob_01",
            "id": 128978299,
            "weaponrackId": 0,
            "locName": "PaintJob_PlasmaPistol_Default_Paintjob_01_name",
            "locDescription": "PaintJob_PlasmaPistol_Default_Paintjob_01_info"
          }
        },
        "modifications": ["NYI"],
        "ammo": {"clip": 6, "hopper": 72}
      }
    },
    "state": {"oxygenRemaining": 60000, "energy": 1}
  },
  "suits": {
    "5705056992307866": {"name": "FlightSuit", "id": 128937264, "suitId": 5705056992307866, "locName": "Flight Suit", "slots": []},
    "1328701204568546": {
      "name": "UtilitySuit_Class1",
      "id": 128958394,
      "suitId": 1328701204568546,
      "locName": "Maverick Suit",
      "slots": []
    },
    "…": "3 more entries"
  }
}
```

</details>

<details markdown="1">
<summary><code>/market</code></summary>

```json
{
  "id": 4252074499,
  "name": "Schrodinger Hub",
  "outpostType": "starport",
  "imported": {
    "128049226": "HazardousEnvironmentSuits",
    "128049191": "NaturalFabrics",
    "128049197": "Polymers",
    "…": "126 more entries"
  },
  "exported": {"128049202": "HydrogenFuel", "128049162": "Cobalt", "128049248": "Scrap", "…": "31 more entries"},
  "services": {"dock": "ok", "contacts": "ok", "exploration": "ok", "…": "17 more entries"},
  "economies": {
    "15": {"name": "Agri", "proportion": 0.1},
    "25": {"name": "HighTech", "proportion": 0.3},
    "35": {"name": "Industrial", "proportion": 0.3},
    "…": "3 more entries"
  },
  "prohibited": {
    "128049212": "BasicNarcotics",
    "128049213": "Tobacco",
    "128049233": "PersonalWeapons",
    "…": "8 more entries"
  },
  "commodities": [
    {
      "id": 128924334,
      "name": "AgronomicTreatment",
      "legality": "",
      "buyPrice": 2427,
      "sellPrice": 2344,
      "meanPrice": 3069,
      "demandBracket": 0,
      "stockBracket": 3,
      "stock": 14350,
      "demand": 1,
      "statusFlags": [],
      "categoryname": "Chemicals",
      "locName": "Agronomic Treatment"
    },
    {
      "id": 128049204,
      "name": "Explosives",
      "legality": "",
      "buyPrice": 0,
      "sellPrice": 622,
      "meanPrice": 483,
      "demandBracket": 2,
      "stockBracket": 0,
      "stock": 0,
      "demand": 148582,
      "statusFlags": [],
      "categoryname": "Chemicals",
      "locName": "Explosives"
    },
    {
      "id": 129046166,
      "name": "Helium",
      "legality": "",
      "buyPrice": 0,
      "sellPrice": 79573,
      "meanPrice": 73920,
      "demandBracket": 3,
      "stockBracket": 0,
      "stock": 0,
      "demand": 1159,
      "statusFlags": [],
      "categoryname": "Chemicals",
      "locName": "Helium"
    },
    "… 155 more entries"
  ]
}
```

</details>

<details markdown="1">
<summary><code>/shipyard</code></summary>

```json
{
  "id": 4252074499,
  "name": "Schrodinger Hub",
  "outpostType": "starport",
  "imported": {"128049226": "HazardousEnvironmentSuits", "128049191": "NaturalFabrics", "…": "127 more entries"},
  "exported": {"128049202": "HydrogenFuel", "128049162": "Cobalt", "…": "37 more entries"},
  "services": {"dock": "ok", "contacts": "ok", "…": "18 more entries"},
  "economies": {
    "15": {"name": "Agri", "proportion": 0.1},
    "25": {"name": "HighTech", "proportion": 0.3},
    "…": "4 more entries"
  },
  "ships": {
    "shipyard_list": {
      "SideWinder": {"id": 128049249, "name": "SideWinder", "basevalue": 31200, "sku": ""},
      "MediumTransport01": {
        "id": 129041442,
        "name": "MediumTransport01",
        "basevalue": 67557231,
        "sku": "ELITE_V_MEDIUM_TRANSPORT_01"
      },
      "…": "32 more entries"
    },
    "unavailable_list": [
      {
        "id": 128049321,
        "name": "Federation_Dropship",
        "basevalue": 14314205,
        "sku": "",
        "unavailableReason": "Insufficient Rank",
        "factionId": "3",
        "requiredRank": 3
      },
      {
        "id": 128672152,
        "name": "Federation_Gunship",
        "basevalue": 35814205,
        "sku": "",
        "unavailableReason": "Insufficient Rank",
        "factionId": "3",
        "requiredRank": 7
      },
      "… 2 more entries"
    ],
    "discount_list": {"elite": 0.975}
  },
  "modules": {
    "129044375": {
      "id": 129044375,
      "category": "mercgear",
      "name": "Hpt_Railgun_Fixed_Medium",
      "nameEngineered": "Enduring Feedback Rail Gun",
      "cost": 0,
      "additionalCost": {"mercCoins": 950},
      "sku": "",
      "stock": -1,
      "tags": "eTag_RailGun",
      "recipeName": "RailGun_LongShot",
      "recipeLevel": 1,
      "modifiers": {
        "modifiers": [
          {"name": "mod_mass", "value": 0.225, "type": 1},
          {"name": "mod_passive_power", "value": 0.0875, "type": 1},
          "… 8 more entries"
        ],
        "type": "merc",
        "progress": 1
      },
      "restrictions": {"removal": "preengineered-mercgear", "experimental": "preengineered-mercgear"}
    },
    "128049444": {
      "id": 128049444,
      "category": "weapon",
      "name": "Hpt_Cannon_Gimbal_Huge",
      "cost": 5266560,
      "sku": null,
      "stock": -1
    },
    "…": "995 more entries"
  }
}
```

</details>

<details markdown="1">
<summary><code>/fleetcarrier</code></summary>

```json
{
  "name": {
    "callsign": "BNH-T2F",
    "vanityName": "517569657420456d626572",
    "filteredVanityName": "517569657420456d626572"
  },
  "currentStarSystem": "LTT 7786",
  "balance": "958204117",
  "fuel": "952",
  "state": "normalOperation",
  "theme": "None",
  "dockingAccess": "all",
  "notoriousAccess": false,
  "capacity": {
    "shipPacks": 0,
    "modulePacks": 0,
    "cargoForSale": 0,
    "cargoNotForSale": 181,
    "cargoSpaceReserved": 0,
    "crew": 930,
    "freeSpace": 23889,
    "microresourceCapacityTotal": 1000,
    "microresourceCapacityFree": 1000,
    "microresourceCapacityUsed": 0,
    "microresourceCapacityReserved": 0,
    "squadronBankTotal": 0
  },
  "itinerary": {
    "completed": [
      {
        "departureTime": "2026-09-18 01:02:00",
        "arrivalTime": "2026-09-13 18:32:00",
        "state": "success",
        "visitDurationSeconds": 369000,
        "starsystem": "Procyon"
      },
      {
        "departureTime": "2026-09-22 00:57:00",
        "arrivalTime": "2026-09-18 01:02:00",
        "state": "success",
        "visitDurationSeconds": 345300,
        "starsystem": "Arque"
      },
      "… 2 more entries"
    ],
    "totalDistanceJumpedLY": 246,
    "currentJump": null
  },
  "marketFinances": {
    "cargoTotalValue": 7861053,
    "allTimeProfit": 19475,
    "numCommodsForSale": 0,
    "numCommodsPurchaseOrders": 0,
    "balanceAllocForPurchaseOrders": 0
  },
  "blackmarketFinances": {
    "cargoTotalValue": 0,
    "allTimeProfit": 0,
    "numCommodsForSale": 0,
    "numCommodsPurchaseOrders": 0,
    "balanceAllocForPurchaseOrders": 0
  },
  "finance": {
    "bankBalance": 958204117,
    "bankReservedBalance": 0,
    "taxation": 0,
    "service_taxation": {
      "bartender": 0,
      "pioneersupplies": 0,
      "rearm": 0,
      "refuel": 0,
      "repair": 0,
      "shipyard": 0,
      "outfitting": 0
    },
    "numServices": 7,
    "numOptionalServices": 0,
    "debtThreshold": -300000000,
    "maintenance": 9486600,
    "maintenanceToDate": 9109331,
    "coreCost": 5000000,
    "servicesCost": 4486600,
    "servicesCostToDate": 4109331,
    "jumpsCost": 0,
    "numJumps": 0,
    "bartender": {
      "microresourcesTotalValue": 0,
      "allTimeProfit": 0,
      "microresourcesForSale": 0,
      "microresourcesPurchaseOrders": 0,
      "balanceAllocForPurchaseOrders": 0,
      "profitHistory": [0, 0, "… 6 more entries"]
    }
  },
  "servicesCrew": {
    "refuel": {
      "crewMember": {
        "name": "Rosa Guthrie",
        "gender": "F",
        "enabled": "YES",
        "faction": "independent",
        "salary": 1500000,
        "hiringPrice": 40000000,
        "lastEdit": "2026-10-01 07:30:01"
      },
      "invoicesWeekToDate": [
        {"wages": 1369776, "from": "2026-10-01 07:30:01", "until": "2026-10-07 16:54:55", "type": "current"},
        {"wages": 1495533, "from": "2026-10-07 16:54:55", "until": "2026-10-08 07:00:00", "type": "expected"}
      ],
      "status": "ok"
    },
    "repair": {
      "crewMember": {
        "name": "Everleigh Chang",
        "gender": "F",
        "enabled": "YES",
        "faction": "independent",
        "salary": 1500000,
        "hiringPrice": 50000000,
        "lastEdit": "2026-10-01 07:30:01"
      },
      "invoicesWeekToDate": [
        {"wages": 1369776, "from": "2026-10-01 07:30:01", "until": "2026-10-07 16:54:55", "type": "current"},
        {"wages": 1495533, "from": "2026-10-07 16:54:55", "until": "2026-10-08 07:00:00", "type": "expected"}
      ],
      "status": "ok"
    },
    "rearm": {
      "crewMember": {
        "name": "Sanford Briggs",
        "gender": "M",
        "enabled": "YES",
        "faction": "independent",
        "salary": 1500000,
        "hiringPrice": 95000000,
        "lastEdit": "2026-10-01 07:30:01"
      },
      "invoicesWeekToDate": [
        {"wages": 1369776, "from": "2026-10-01 07:30:01", "until": "2026-10-07 16:54:55", "type": "current"},
        {"wages": 1495533, "from": "2026-10-07 16:54:55", "until": "2026-10-08 07:00:00", "type": "expected"}
      ],
      "status": "ok"
    }
  },
  "cargo": [
    {
      "commodity": "Modularterminals",
      "originSystem": null,
      "mission": false,
      "qty": 13,
      "value": 30901,
      "stolen": false,
      "locName": "Modular Terminals"
    },
    {
      "commodity": "Nanobreakers",
      "originSystem": null,
      "mission": false,
      "qty": 9,
      "value": 20475,
      "stolen": false,
      "locName": "Nanobreakers"
    },
    "… 2 more entries"
  ],
  "orders": {"commodities": {"sales": [], "purchases": []}, "onfootmicroresources": {"sales": [], "purchases": []}},
  "carrierLocker": {"assets": [], "goods": [], "data": []},
  "reputation": [{"majorFaction": "empire", "score": 100}, {"majorFaction": "federation", "score": 100}, "… 2 more entries"],
  "market": {
    "id": 3715429376,
    "name": "BNH-T2F",
    "outpostType": "fleetcarrier",
    "imported": [],
    "exported": [],
    "services": {"commodities": "ok", "carrierfuel": "ok", "…": "20 more entries"},
    "economies": {"136": {"name": "Carrier", "proportion": 1}},
    "prohibited": {"128667728": "ImperialSlaves"}
  },
  "commodities": {
    "commodities": [
      {
        "id": 128066403,
        "categoryname": "NonMarketable",
        "name": "Drones",
        "stock": 999999,
        "buyPrice": 95,
        "sellPrice": 95,
        "demand": 9999999,
        "legality": "",
        "meanPrice": 95,
        "demandBracket": 2,
        "stockBracket": 2,
        "locName": "Limpet"
      }
    ]
  },
  "ships": {"shipyard_list": [], "discount_list": []},
  "modules": {
    "129045677": {
      "id": 129045677,
      "category": "module",
      "name": "Int_FighterBayMk2_Size5_Class1_Free",
      "cost": 0,
      "sku": "ELITE_V_MKIIFIGHTERBAY_FREE",
      "stock": -1,
      "variant": null,
      "restock": 0,
      "bundle": true
    },
    "129045678": {
      "id": 129045678,
      "category": "module",
      "name": "Int_FighterBayMk2_Size6_Class1_Free",
      "cost": 0,
      "sku": "ELITE_V_MKIIFIGHTERBAY_FREE",
      "stock": -1,
      "variant": null,
      "restock": 0,
      "bundle": true
    },
    "…": "10 more entries"
  }
}
```

</details>

<details markdown="1">
<summary><code>/communitygoals</code></summary>

```json
{
  "activeCommunityGoals": [
    {
      "id": 860,
      "title": "Defeat Criminal Pilots in Redonesses",
      "expiry": "2026-10-08 10:00:00",
      "market_name": "Sword of Iustitia",
      "starsystem_name": "Redonesses",
      "activityType": "combatbond",
      "target_qty": 1000000000000,
      "qty": 102390474800,
      "objective": "Combat bonds",
      "news": "Defeat Criminal Pilots in Redonesses\n\n\nAuthorities in the Redonesses system have issued an urgent call for assistance fo…",
      "bulletin": "Authorities in the Redonesses system have issued an urgent call for assistance following a prison break aboard the EVE-5…"
    }
  ]
}
```

</details>

<details markdown="1">
<summary><code>/journal/2026/10/02</code></summary>

```json
{"timestamp": "2026-10-02T14:40:30Z", "event": "Commander", "FID": "F604218", "Name": "ARKON VALE"}
{"timestamp": "2026-10-02T14:40:30Z", "event": "Rank", "Combat": 1, "Trade": 12, "Explore": 5, "Soldier": 0, "Exobiologist": 1, "Empire": 2, "Federation": 1, "CQC": 0}
{"timestamp": "2026-10-02T14:40:32Z", "event": "CarrierLocation", "CarrierType": "FleetCarrier", "CarrierID": 3715429376, "StarSystem": "LTT 7786", "SystemAddress": 633608311522, "BodyID": 10}
{"timestamp": "2026-10-02T14:41:19Z", "event": "FSSSignalDiscovered", "SystemAddress": 633608311522, "SignalName": "$Warzone_PointRace_Low:#index=7;", "SignalName_Localised": "Conflict Zone [Low Intensity]", "SignalType": "Combat"}
{"timestamp": "2026-10-02T15:05:46Z", "event": "DockingRequested", "MarketID": 3844488192, "StationName": "Archambeau's Serenity", "StationType": "OnFootSettlement", "LandingPads": {"Small": 1, "Medium": 0, "Large": 1}}
{"timestamp": "2026-10-02T14:39:59Z", "event": "Music", "MusicTrack": "NoTrack"}
```

</details>

## New capabilities each endpoint could support

The table above measures CAPI against what d47 already does. This section lists what each endpoint
could let d47 do that it cannot do today, whether or not an issue asks for it, with the probe's
verdict on each: **supported**, **supported in part** (naming what is missing) or **not supported**.
The candidates marked *new* come from fields the documentation does not mention.

### `/profile`

- **The whole fleet's fittings from the first launch.** **Not supported.** `ships` carries no
  `modules` or `engineer` (question 4).
- **What each ship's value is made of.** **Supported.** `ships[*].value` has `hull`, `modules`,
  `cargo`, `total` and `unloaned`. `StoredShips` gives one total per ship.
- **Wear across the fleet.** **Not supported.** `health` is on the current ship only.
- **SRV bays.** **Supported in part.** `launchBays` is on the current ship only, where the journal's
  `Loadout` already shows the bay.
- *New:* **every saved on-foot loadout.** **Supported.** `loadouts` lists each loadout's suit and
  weapons, including loadouts not switched to since d47 was installed. Weapon modifications read
  `["NYI"]`, so the engineering on them is not there.
- *New:* **the Commander's `mercCoins`**, the currency pre-engineered modules are priced in.
  **Supported.**

### `/market`

- **Legality before a sale.** **Supported in part.** `prohibited` names the station's banned goods;
  `legality` is empty on every commodity (question 5). The last docked station only.
- **Imports and exports by name.** **Not supported** as anything new: they are the `Consumer` and
  `Producer` flags plus prohibited and salvage goods.
- *New:* **salvage prices.** **Supported.** What the last docked station pays for escape pods, black
  boxes, wreckage and hostages, which `Market.json` leaves out.

### `/shipyard`

- **What this station sells, with stock.** **Not supported.** `stock` is `-1` on every module and
  absent on ships (question 6). The lists without stock are already on disk in `Shipyard.json` and
  `Outfitting.json`, which d47 does not read.
- *New:* **ships locked by rank.** **Supported.** `unavailable_list` names each ship the Commander
  cannot buy and the rank it needs. The requirements do not change, so a table serves as well.
- *New:* **pre-engineered modules for sale.** **Supported.** Blueprint, grade, every modifier and
  the `mercCoins` price, at the last station visited.

### `/fleetcarrier`

- **How long the carrier can pay its way.** **Supported, and already in d47.** `finance` has the
  weekly cost, its parts and the debt threshold (question 9): 9,486,600 a week against a balance of
  971,002,653, about 102 weeks. `CarrierUpkeep` (#835) already works this out from the journal: run
  over the same journals it gave a weekly upkeep of 9,600,000, the last drop in `CarrierStats`
  balance across a weekly tick, and 101 weeks covered. CAPI would only make the weekly figure
  exact.
- **Buy orders still to fill.** **Not supported** in practice. `orders` stayed empty while an order
  was open (question 10), because the endpoint lagged the game by 22 to 69 minutes.
- **The hold as Frontier counts it.** **Supported, and stale.** `cargo[]` matched `CarrierState`'s
  count, and lagged it after a sale (question 10).
- **Where the carrier has been.** **Supported in part.** `itinerary.completed` covers the last four
  stops only (question 11).
- **What the carrier's market has earned.** **Supported.** `marketFinances.allTimeProfit` is filled.
- **On-foot materials stored on the carrier.** **Supported in part.** `carrierLocker` is present and
  was empty, so its contents were not seen.
- **The Livery theme** for #308. **Not supported** (questions 7 and 8).
- *New:* **the crew roster.** **Supported.** Each service's crew member, salary, hiring price and
  week-to-date wages.

### `/communitygoals`

- **Every active goal, without an Inara key.** **Supported in part.** Title, system, station,
  expiry, objective and progress against the target; no tiers and no contributor count
  (question 12).
- **The Commander's standing in a goal at any time.** **Not supported.** No field is about the
  Commander.

### `/journal`

- **History from days this PC has no file for.** **Supported in part.** The endpoint returns past
  days only for the last 36 to 63 days (question 13), too short to rebuild a Commander's history
  after a move to a new PC.

### `/visitedstars`

- **"Have I been here" for visits older than the journals.** **Supported, and better without CAPI.**
  The CAPI copy holds the same systems as the local `VisitedStarsCache.dat` with every count set to
  1 and every day the same (question 14). The local file, which the game already writes, has every
  system visited with real counts and days, and its format can be read.

## Does `/fleetcarrier` describe the carrier's Livery (#308)?

**No.** The documentation lists a single field:

> `theme`: Livery theme for the carrier.

([EP:322][EP322]), with the values `SearchAndRescue`, `Mining`, `Trader`, `Explorer`, `AntiXeno`
and `BountyHunter` ([EP:323-328][EP322]). The game's Livery screen sets a **Layout** (Victory-Class,
Nautilus and others) and a **Concourse Theme** (Research, Default and others). The probe changed
both, and `theme` read `"None"` before and after, with no other key naming either (questions 7 and
8). The journal carries nothing either: every occurrence of "livery" on disk is a station service,
and `CarrierStats` has no layout field.

So neither CAPI nor the journal can tell d47 which carrier layout the Commander has. Showing the
right model for #308 needs the Commander to say which layout it is.

## Cost

### A client ID

- A developer applies at `https://user.frontierstore.net/` under **Developer Zone**. The Create
  Client form asks for an app name, a homepage URL, the scopes, a short description and agreement to
  the terms and conditions. Once created, the client is listed with its scopes, and **View** shows
  the client ID and shared key ([OA:3-28][OA3]). The client created for the probe stayed
  **INACTIVE**, and PKCE sign-in with its ID worked all the same.
- The application name matters later: Frontier asks that the `User-Agent` match
  `EDCD-[A-Za-z]+-[.0-9]+`, with the middle part aligned to the name given when applying
  ([FDevIDs README:9-19][FDEVREADME]). "Directive 47" has digits, so the probe sent
  `EDCD-Directive-0.110`.
- **The open-source conflict.** Frontier's FAQ says whoever obtains an API key is responsible for
  accepting Frontier's Terms and Conditions, that anyone else running an instance must obtain
  their own key, and that the key "SHOULD NOT be included in your open source code"
  ([FAQ][FAQ]). EDMC, which is open source, hardcodes its client ID and lets an environment
  variable override it ([companion.py:271-274][EDMC271], [Releasing.md:90-91][EDMCREL]). d47 is
  open source and ships one binary to every Commander. The sources do not say whether a PKCE
  client ID, used without a shared key, counts as the "API key" the FAQ means. This is not settled,
  and has to be before anything ships with a client ID.

### OAuth and token lifetimes

- An authorisation-code flow with PKCE (S256, 32-byte verifier). The authorisation endpoint is
  `https://auth.frontierstore.net/auth` with `audience`, `scope`, `client_id`, `code_challenge` and
  `redirect_uri` ([OA:53-100][OA53]), and the token endpoint is
  `https://auth.frontierstore.net/token` ([OA:143-154][OA143]). With PKCE, no client secret is
  needed ([OA:236][OA236]); the probe used none.
- The scope has to be `auth capi`. A `capi`-only token is refused, with a response that lists
  `email`, `firstname` and `lastname` as missing ([fd-api README:13-24][FDREADME]). So the token d47
  would hold carries the Commander's real name and email address (question 19).
- `audience`: EDMC sends `frontier,steam,epic` ([companion.py:378-381][EDMC378]), which worked for
  the probe's account (question 15).
- The redirect URI can be a loopback URL that was never registered (question 16), so a loopback
  listener needs no registry change. A custom scheme would need a protocol handler, which the
  per-user installer (`installer/d47.iss`, `PrivilegesRequired=lowest`) would have to write to
  `HKCU`.
- An access token lasts 14,400 seconds, which is four hours ([OA:246-250][OA246]); the probe saw
  `expires_in: 14399`. No refresh token works more than **25 days after the first authorisation**,
  after which the Commander has to sign in again in a browser ([OA:216-220][OA216]).
- If the Commander revokes d47's access, the next refresh fails with 401, but an access token that
  has not expired keeps working until it does ([OA:252-254][OA246], [OA:275-278][OA258]).
- A token belongs to one Frontier account. A Commander who plays several accounts would sign in
  once per account, and d47 would have to pick the token by the journal's `FID`.

### Where tokens live

The refresh token and the current access token would go in `SecretStore` under two new names, read
through `TryGet` behind a lazy accessor, like the Inara key that `InaraCommunityGoalService` reads
on every call. Both tokens rotate, so unlike every secret stored today, they are written by d47
rather than pasted in by the Commander.

### The egress disclosure

This would add a new entry to `EgressDisclosure` with two destinations: `auth.frontierstore.net`
for the sign-in and token refresh, and `companion.orerve.net` for the queries. The entry has to say:

- what is sent: the bearer token, which identifies the Frontier account;
- what comes back: the Commander's name, credits, ranks and carrier finances;
- that the token itself holds the Commander's email address, first and last name and customer id.

The FAQ says personal data needs a separate consent step and that players must be told what is
used and why ([FAQ][FAQ]). The disclosure and the sign-in screen have to carry that.

### Live and Legacy

The data comes from three hosts: `companion.orerve.net` (Live), `legacy-companion.orerve.net`
(Legacy) and `pts-companion.orerve.net` (beta) ([companion.py:64-66][EDMC64]).
Since Update 14, the Live host returns only Live-galaxy data ([EP:47-66][EP47]). The Legacy host
still answers, with the account's Legacy Commander, in 6.7 s against the Live host's second
(question 17). A d47 client would pick the host from the galaxy the journal says the Commander is
playing in.

### Rate limits and outages

- No published limit. The guidance is no more than one query a minute. Frontier has said rate
  limiting may start above two queries a second ([EP:31-45][EP31]). The probe kept to one a minute
  and met no limit, error or outage.
- EDMC waits 60 seconds between sets of queries, and 15 minutes between `/fleetcarrier` queries,
  with a 60-second timeout on the latter ([companion.py:55-58][EDMC55]).
- Status 418 means maintenance ([EP:647-651][EP640]). An expired token returns 401 since
  2019-09-17 ([EP:640-646][EP640]), but the OAuth notes still say 422 ([OA:211-214][OA211]);
  question 18 has the probe's answer.

### Frontier's terms

The only Frontier-written terms reached are the FAQ's: the key holder accepts the Terms and
Conditions, the key is not to go in open-source code, personal data needs a separate consent step,
and players are told how their data is used ([FAQ][FAQ]). The probe's client was created by
accepting the Terms and Conditions on the Create Client form; their text was not recorded here.

## Where a client would sit

A CAPI client is a web client like `InaraCommunityGoalService` and the Spansh services, so it
belongs in **`D47.Knowledge`**, which references only Core.

The proposed seam is **a new Core seam per need, not one for the whole API**, following
`ICommunityGoalService`:

- `/communitygoals` would be a second implementation of the existing `ICommunityGoalService`, with
  no new seam, filling the fields question 12 found and leaving tiers and contributors empty.
- Any other endpoint would get a Core seam for the one need it serves, implemented in
  `D47.Knowledge`.
- Signing in needs a browser and a redirect listener, so it is the App's job. `D47.App` would open
  the browser and hand the returned code to the Knowledge client through a delegate, the way
  `InaraCommunityGoalService` takes its key as a `Func<string?>`. No spoke references another.
- Every request runs on the thread pool, queued from the tick, as the Inara request does. Nothing on
  the tick waits for it.

This page proposes the seam. It does not build one.

## Questions only a live call can answer

These were for #616 and are answered under **The live probe**, by number.

### Every endpoint

1. **Shape.** Call each of the seven endpoints once. For each, record the status, the response time
   and every top-level and nested key. Mark each documented field as present and filled, present and
   empty, or absent, and list every field the documentation does not mention.
2. **Capabilities.** For each item under **New capabilities each endpoint could support**, say
   whether the fields it needs are present and filled. Give each one of: supported, supported in
   part (naming the missing field), or not supported.
3. **Freshness.** For `/profile`, `/market` and `/fleetcarrier`, change something in game (credits,
   the docked station, a carrier order), then call again a minute later. Does the response show the
   change, and how long did it take?

### Per endpoint

4. **`/profile` ships.** Does `ships` carry `modules`, `engineer`, `value` and `health` for ships
   that are stored rather than flown? Do they match `LoadoutStore` for a ship that is in both?
5. **`/market`.** Dock at a station that prohibits a commodity. Are `prohibited` and `legality`
   filled? Do `imported` and `exported` say anything the `Consumer` and `Producer` flags in
   `Market.json` do not?
6. **`/shipyard`.** Compare `modules` and `ships.shipyard_list` with `Outfitting.json` and
   `Shipyard.json` written at the same dock. Is anything but `stock` and `sku` different?
7. **Livery.** Does any `/fleetcarrier` key other than `theme` describe Livery parts, paint or
   decals? Compare `theme` with the theme shown on the in-game Livery screen.
8. **Livery changes.** Change the theme in game, open the carrier management screen, wait out the
   cooldown and call `/fleetcarrier` again. Did `theme` change, and how long after the change?
9. **Carrier finances.** Are `coreCost`, `servicesCost`, `maintenance`, `jumpsCost` and
   `debtThreshold` filled? Does `bankBalance` match the latest
   `CarrierStats.Finance.CarrierBalance`?
10. **Carrier hold and orders.** Compare `cargo[]` with the hold `CarrierState` has counted (#799),
    per commodity. Compare `orders` with the last `CarrierTradeOrder` events, and record whether
    `outstanding` falls as a buy order is filled.
11. **Carrier locker and itinerary.** Is `carrierLocker` filled? Does `itinerary.completed` match
    the `CarrierJump` events in the journal, and how many past jumps does it list?
12. **Community goals.** Record the fields of `/communitygoals`. Does it carry each field of
    `CommunityGoalListing` (system, station, expiry, tiers, contributors, objective, reward), and
    the Commander's contribution and percentile for a goal they have joined?
13. **Past journals.** Ask `/journal` for a day that has a local journal file and compare the two.
    Then ask for days further back, a month at a time, until it returns nothing. How far back does
    it go?
14. **Visited stars.** Download `/visitedstars`. Is it a zip or a gzip archive, how long does the
    102 status last, and is the file inside identical to the local `VisitedStarsCache.dat` for the
    same account?

### Authentication

15. **Audience.** Which `audience` value does the Commander's account need: `frontier`, `steam`, or
    EDMC's `frontier,steam,epic`?
16. **Redirect.** Does a `http://localhost:<port>` redirect work without being registered against
    the client ID?
17. **Legacy host.** Does `legacy-companion.orerve.net` still answer?
18. **Expired token.** Let an access token expire and call once. Is the status 401 or 422?
19. **Token contents.** Decode the access token with `/decode` ([OA:258-283][OA258]). Which personal
    fields does it carry, so the egress entry can name them exactly?

## Verdict

**Not worth a CAPI client now.** What only CAPI gives is small: a station's prohibited goods and
salvage prices at the last dock, pre-engineered modules for sale, community goals without an Inara
key (and without tiers), and the carrier's exact upkeep. The costs are a client ID whose use in
open-source code Frontier's FAQ advises against, a browser sign-in renewed every 25 days, one token
per Frontier account, a token that carries the Commander's email address and real name, and a
`/fleetcarrier` that lagged the game by 22 to 69 minutes. The two expectations that made the probe
worth doing did not hold: `ships` has no fittings for stored ships, and `/fleetcarrier` has no
Livery layout (#308).

Worth a build issue, and needing no CAPI: **read the local `VisitedStarsCache.dat`** (#930). It
holds every system the account has visited, with visit counts and days, in a file the game already
writes, and answers "have I been here" for visits older than the journals on disk (question 14).
The carrier's upkeep runway was the other candidate; `CarrierUpkeep` (#835) already gives it.

If a CAPI client is built later, `/market`'s `prohibited` (a warning on docking with banned goods
aboard) and `/communitygoals` (behind `ICommunityGoalService`) are the first two worth building,
once the client ID question under **A client ID** is settled with Frontier.

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
[EDMCREL]: https://github.com/EDCD/EDMarketConnector/blob/main/docs/Releasing.md#L90-L91
