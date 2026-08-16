# What an NPC says before it shoots

**Measured 2026-08-15** against the same corpus as
[journal-corpus-engineering.md](journal-corpus-engineering.md): **912 journals, 373 MB, 3 July 2025
to 11 August 2026**, nine Commanders, read over SSH from a second machine. Everything below is
counted from those files.

This page is the evidence behind list.md **Phase 15 — Warnings that arrive in time**. Both of that
phase's items turn on a measurement rather than on a design, and both of those measurements are
reproduced here so the numbers in the code comments have somewhere to point.

**What it settles:** which NPC comms ids actually precede an attack and by how long, which
plausible-sounding ones do not, and what the journal does and does not report about Powerplay
territory. **What it does not settle** is anything about who is *near* you — see §4, which is a
null result and the reason the second item is written the way it is.

---

## 1. The allowlist, and the false positives it exists to exclude

Every NPC line Elite writes arrives as a localisation id — `$Pirate_StartInterdiction07;` — with
the prose in a separate `Message_Localised` field. The ids are stable, enumerable and not written
by another player, which is what makes an allowlist possible at all.

Follow-through is counted as **any of `UnderAttack`, `HullDamage`, `ShieldState` with
`ShieldsUp:false`, `Interdicted` or `Died` inside 60 seconds** of the line; the lead time is to the
first of them. `within30` is the subset that also landed inside 30 seconds, which is the window in
which a warning is still worth having.

| Id group | Seen | Followed by an attack | Inside 30 s | Median lead |
|---|---|---|---|---|
| `$Pirate_StartInterdiction*` | 42 | **37 (88.1%)** | 37 | 6 s |
| `$Pirate_OnDeclarePiracyAttack*` | 398 | **266 (66.8%)** | 205 | 8 s |
| `$BountyHunter_StartInterdiction*` | 1 | **1 (100%)** | 1 | 5 s |
| `$Trader_OnEnemyShipDetection*` | 2,399 | 30 (1.3%) | 25 | 12 s |
| `$HostileScan*` | 48 | 0 (0%) | 0 | — |

**The bottom two rows are the whole argument for an allowlist.** Anything matching on "this id
sounds hostile" fires 2,399 times to catch 30 real events — a hundred false alarms per true one,
which is a warning a Commander switches off within an hour and then does not have when it matters.
`$HostileScan*` is worse: it is 48 for 48 wrong.

`$Trader_OnEnemyShipDetection*` is not even about the Commander. A trader NPC says it when it spots
something *it* does not like, which is usually the Commander's own combat ship.

### Groups measured and deliberately not shipped

Three more cleared 40%, and none of them is in the allowlist:

| Id group | Seen | Followed | Median lead | Why not |
|---|---|---|---|---|
| `$Military_StartInterdiction*` | 4 | 4 (100%) | 8 s | 4 samples. A perfect rate over four events is not a rate. |
| `$Pirate_UnprovokedAttack*` | 14 | 11 (78.6%) | 3 s | Three seconds is not a warning, it is a caption. |
| `$PirateLord_OnDeclarePiracyAttack*` | 14 | 6 (42.9%) | 6 s | Below both shipped pirate groups, on a tenth of the evidence. |

They are recorded rather than discarded because the next corpus may have enough of them to decide,
and a group that was measured and rejected is cheaper to revisit than one nobody looked at.

Note that `$PirateLord_*` is **not** matched by `$Pirate_*`: the ids are compared as whole prefixes
after the `$`, so `Pirate_` matching `PirateLord_` would be a bug rather than a bonus.

---

## 2. The lines are ids, and the ids are what is matched

`Message` carries the id; `Message_Localised` carries prose somebody at Frontier wrote. The
allowlist compares against `Message` only.

```json
{ "timestamp":"2026-02-14T20:11:03Z", "event":"ReceiveText",
  "From":"$npc_name_decorate:#name=Kaiser Grendel;", "From_Localised":"Kaiser Grendel",
  "Message":"$Pirate_OnDeclarePiracyAttack04;", "Message_Localised":"...", "Channel":"npc" }
```

This matters beyond tidiness. In-game comms are untrusted input (architecture.md §7), and the
attacker is any player in range. A match on the *text* is a match on a string an attacker chooses;
a match on the id is a comparison against a fixed set of constants that no message can add to. It
is also why the message text never reaches the model: the warning is assembled from the id group
alone.

---

## 3. Powerplay: what the journal reports

| Event | Count | Carries |
|---|---|---|
| `Powerplay` | 677 | `Power`, `Rank`, `Merits`, `TimePledged` |
| `PowerplayMerits` | 2,115 | `Power`, `MeritsGained`, `TotalMerits` |
| `PowerplayRank` | 86 | `Power`, `Rank` |
| `PowerplayCollect` / `PowerplayDeliver` | 9 / 13 | `Power`, `Type`, `Count` |
| `PowerplayJoin` | 4 | `Power` |
| `PowerplayLeave` | 1 | `Power` |

`Powerplay` is a session snapshot; `PowerplayJoin` and `PowerplayLeave` are the transitions. **The
pledge is history, not a constant** — one Commander in this corpus left Pranav Antal in January
2026 — so reading it once at startup and holding it is how d47 ends up warning about the territory
of a Power the Commander now flies for.

The system's own Power arrives on the arrival events:

| Field | `FSDJump` | `Location` | `CarrierJump` |
|---|---|---|---|
| `PowerplayState` | 4,891 | 713 | 54 |
| `Powers` | 4,891 | 713 | 54 |
| `ControllingPower` | 2,959 | 521 | 46 |

`ControllingPower` is a plain string. **It is absent rather than null when the system is
unoccupied**, which is the 1,932-event gap between the first two rows and the third — so a missing
field means nobody controls this system, not that the field was not read.

It appears on `FSDJump`, `Location` and `CarrierJump`, and on nothing else. There is no
`SupercruiseExit` copy of it, which is why the condition is evaluated from the location the
Commander already has rather than from the event that drops them into normal space.

---

## 4. The null result: nothing reports a ship being *near* you

The thing actually wanted for the second item is a warning when a Power Security ship appears in
the contacts panel. **No third-party app can see that.** Across the 221 event types Elite has ever
written there is no contact, spawn or proximity event; no state file holds a contacts list; and
every signal about another ship — `ShipTargeted`, `ReceiveText`, `Scanned`, `UnderAttack` —
requires it to have already acted.

`$PowersSecurity_OnAttackStart*` is real and names the Power in `#targetPowerName`:

```json
"Message":"$PowersSecurity_OnAttackStart02:#targetPowerName=Edmund Mahon;"
```

It was seen 15 times, 11 of them followed by an attack, at a **median lead of 1 second**. That is
confirmation, not warning, and it is why the shipped item announces the standing condition — *you
are exposed* — rather than implying something is near.

The measured asymmetry that decides where the announcement fires: across every Power security
contact in this corpus, **0% happened in supercruise and 67% in normal space**. Arriving is a
supercruise state and nothing can reach you there, so announcing on arrival is announcing at the
one moment the condition cannot hurt.

---

## Reproducing this

The corpus is not in the repo and must not be — it is one person's play history. The two scripts
that produced the tables above are short enough to restate rather than store: an id histogram over
`"Message":"$..."` with trailing digits stripped, and a per-file pass pairing each allowlisted line
against the next attack event. Both ran over SSH against the second machine.

One trap worth writing down, because it cost three silent failures: the remote default shell reads
piped input **line by line**, so a pipeline split across lines with a trailing `|` is swallowed with
exit code 0 and no output. Send the script as a single `-EncodedCommand` instead.
