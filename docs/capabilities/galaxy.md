# Galaxy search

Look up star systems, and work out how far apart two of them are.

> "how far is Colonia"
> "find a high tech system within 30 light years"
> "what's the nearest Federation system"

This is the first thing d47 does that needs the internet. It asks
[spansh.co.uk](https://spansh.co.uk), which indexes the galaxy from data Commanders share, and
it is **off until you turn it on**.

## Look things up in the galaxy

The one setting. Off by default.

When it is off, asking about a system gets you "Galaxy search is switched off, so I can't look
that up" — a capability that is off, not an error, so the conversation carries on. When it is
on, two things leave this machine:

- the system name you asked about, and any filters — an allegiance, a distance, an economy
- **where you are**, whenever the question is relative to you, because "the nearest high tech
  system" cannot be asked without saying where from

No key, no identifier, nothing else from your journal. The Privacy section computes this same
list rather than repeating it by hand, so it cannot go stale.

Turning it on takes effect immediately. There is no restart.

## Why the filters are checked here rather than there

The search service **ignores filter keys it does not recognise**. It does not reject them or
warn about them. Measured against the live service on 2026-08-14, within 20 light years of Sol:

| Request | Result |
|---|---|
| `allegiance: ["Federation"]` | 60 systems |
| `allegience: ["Federation"]` (misspelled) | 108 systems |
| `not_a_real_filter: ["Federation"]` | 108 systems |
| no allegiance filter at all | 108 systems |

The misspelled row is the dangerous one. Nothing fails, nothing warns, and the answer that comes
back is a perfectly good answer to a question nobody asked — you asked for Federation systems
and got every system, presented as though the filter had applied.

So d47 keeps its own closed list of filters and refuses one it does not know **before** building
a request. If you ask for something that is not on the list, you get told what is, rather than a
confident wrong answer. This is the first guardrail — never invent game data — applied to a
service that will happily let you invent it.

The filters are `distance`, `population`, `allegiance`, `government`, `primary_economy`,
`security` and `state`. Ranges take one number for an upper bound (`30` means within 30) or two
separated by a dash (`10-50`, `1000000-`).

### A worse trap than the misspelling: a real filter that matches nothing

`state` is not sent under the name `state`, and finding that out cost a measurement rather than a
guess. There **is** a field called `state`. It has its own published value list carrying exactly the
twenty-one words this filter accepts, and it is honoured rather than dropped. It also matches
nothing — zero systems for every value, including `None`, where a bogus key returns the unfiltered
count. No result row carries a `state` field at all.

| Request, within 200 light years of Sol | Result |
|---|---|
| no state filter | 10,000 (the cap) |
| `state: ["Boom"]` | **0** |
| `state: ["None"]` | **0** |
| `controlling_minor_faction_state: ["Boom"]` | 1,286 |
| `controlling_minor_faction_state: ["Outbreak"]` | 68 |

Measured 2026-08-15. This fails as an *empty* answer rather than a wrong one, which is worse than
the misspelling above: "there are no systems in Boom near you" reads as a fact about the galaxy, and
a Commander has no way to tell it from one. So d47 offers the short word and sends the long key.

The state itself is **crowd-reported and turns over on the background simulation's tick**, so the
answer says systems *reported* in Boom — the same framing the station stock carries.

## What comes back

One system record from the service is **268 KB** — every body, every station, every market. A
search returning three systems is 44 KB. None of that reaches the model: d47 reads six fields
per system and drops the rest, so the answer is a handful of facts that can be said out loud in
a cockpit rather than a database dump billed by the token.

The station list is read for its length and then discarded. How many stations a system has is
worth saying; which ones they are is the next question.

Results always say how many matched in total, not just how many were read out. "412 systems
matched; here are the nearest 5" is a different answer from "there are 5".

## When it cannot answer

Everything that can go wrong becomes a sentence rather than an error: the service being
unreachable, rate limiting you, taking too long, or having no record of the system you named.
A system the service does not know produces "I couldn't find one of those systems", not a
failed turn.

## Tools

### `search_systems`

Find star systems matching some criteria, nearest first.

```json
{"type":"object","properties":{"allegiance":{"type":"string","description":"Superpower allegiance.","enum":["Alliance","Empire","Federation","Guardian","Independent","Pilots Federation","Thargoid"]},"distance":{"type":"string","description":"How far to look, in light years. For example \u002230\u0022 or \u002210-50\u0022."},"government":{"type":"string","description":"Form of government.","enum":["Anarchy","Communism","Confederacy","Cooperative","Corporate","Democracy","Dictatorship","Feudal","None","Patronage","Prison","Prison Colony","Theocracy"]},"limit":{"type":"integer","description":"How many to return, 1 to 20. Default 5."},"near":{"type":"string","description":"Measure from this system. Defaults to the Commander\u0027s own."},"population":{"type":"string","description":"Population, as a range. For example \u00221000000-\u0022 for a million or more."},"primary_economy":{"type":"string","description":"The system\u0027s main economy.","enum":["Agriculture","Colony","Extraction","High Tech","Industrial","Military","None","Refinery","Service","Terraforming","Tourism"]},"security":{"type":"string","description":"Security level.","enum":["Anarchy","High","Low","Medium"]},"state":{"type":"string","description":"What the controlling faction is going through. Crowd-reported, so this finds systems reported in that state.","enum":["Blight","Boom","Bust","Civil Liberty","Civil Unrest","Civil War","Drought","Election","Expansion","Famine","Infrastructure Failure","Investment","Lockdown","Natural Disaster","None","Outbreak","Pirate Attack","Public Holiday","Retreat","Terrorist Attack","War"]}},"required":[],"additionalProperties":false}
```

A search with no filters is refused rather than run — it would match the whole galaxy.

### `distance_between`

The straight-line distance in light years between two star systems.

```json
{"type":"object","properties":{"from":{"type":"string","description":"The system to measure from. Defaults to the Commander\u0027s current system."},"to":{"type":"string","description":"The system to measure to."}},"required":["to"],"additionalProperties":false}
```

The arithmetic is d47's own. The service returns positions and d47 computes the distance from
the coordinates, so "how far" has the same answer wherever it is asked from.

### `find_nearest_station`

Where to buy a named module or ship, nearest first.

```json
{"type":"object","properties":{"large_pad":{"type":"boolean","description":"Only stations with a large landing pad."},"limit":{"type":"integer","description":"How many to return, 1 to 20. Default 5."},"max_distance":{"type":"number","description":"How far to look, in light years. Default 50."},"module":{"type":"string","description":"A module to be sold there, by name \u2014 for example \u0022Frame Shift Drive\u0022 or \u0022Bi-Weave Shield Generator\u0022."},"module_class":{"type":"string","description":"Module size, 0 to 8.","enum":["0","1","2","3","4","5","6","7","8"]},"module_rating":{"type":"string","description":"Module rating, A to I.","enum":["A","B","C","D","E","F","G","H","I"]},"near":{"type":"string","description":"Search out from this system. Defaults to the Commander\u0027s own."},"ship":{"type":"string","description":"A ship to be sold there, by name \u2014 for example \u0022Krait MkII\u0022."}},"required":[],"additionalProperties":false}
```

Module and ship names are **not** in the schema. There are 132 modules and 48 ships, and an
`enum` that large sits in prompt position 1 where one changed byte invalidates the whole cached
prefix — several kilobytes paid for on every turn to answer a question asked once a session. So
the parameter is free text and the name is matched against the real catalogue here, before a
request is built.

The matcher handles the two ways a spoken name arrives wrong. A **fragment** — "shield
generator" for "Bi-Weave Shield Generator" — is what a Commander says. A **misspelling** —
"Frame Shift Drve" — is what the transcriber does to them. Fragments are offered first.

This matters less than the system filters did and still matters. The service *does* honour a
module name it has never heard of: it returns nothing. So a typo produces "nowhere within 100
light years sells that", which is a false statement about the galaxy rather than a wrong filter.
Catching it here turns it into "I don't know a module called that; did you mean…".

**Stock is crowd-reported**, so every result says when it was last seen. It is not what is
there, it is what somebody last reported was there — and a live search on 2026-08-14 returned a
carrier whose outfitting was last reported in October 2024 alongside two stations reported that
morning. Reading the stale one out as current is how a Commander flies a long way for an empty
shelf.

Getting a position out of a name takes one search: there is no lookup-by-name endpoint —
`api/system/name/Colonia` is a 404, and the by-id endpoint wants an id64 nobody says out loud —
so d47 names the system as a *search reference* and reads back the coordinates the service
resolved it to.

### `find_body`

The nearest planets, moons and stars matching some criteria.

```json
{"type":"object","properties":{"body_type":{"type":"string","description":"The kind of body, by name \u2014 for example \u0022Earth-like world\u0022, \u0022Neutron Star\u0022, \u0022Water world\u0022 or \u0022Class I gas giant\u0022."},"hotspot":{"type":"string","description":"A mining hotspot material in one of the body\u0027s rings \u2014 for example \u0022Painite\u0022, \u0022Low Temperature Diamonds\u0022 or \u0022Void Opal\u0022."},"hotspot_count":{"type":"integer","description":"Exactly how many overlapping hotspots of that material \u2014 not a minimum. A double or triple hotspot is 2 or 3."},"landable":{"type":"boolean","description":"Only bodies that can be landed on."},"limit":{"type":"integer","description":"How many to return, 1 to 20. Default 5."},"max_distance":{"type":"number","description":"How far to look, in light years. Default 50."},"near":{"type":"string","description":"Search out from this system. Defaults to the Commander\u0027s own."},"reserve_level":{"type":"string","description":"How rich the rings are.","enum":["Common","Depleted","Low","Major","Pristine"]},"ring_type":{"type":"string","description":"Ring composition.","enum":["Icy","Metal Rich","Metallic","Rocky"]},"signal":{"type":"string","description":"A signal on the body\u0027s surface: \u0022Biological\u0022, \u0022Geological\u0022, \u0022Human\u0022, \u0022Guardian\u0022 or \u0022Thargoid\u0022."},"signal_count":{"type":"integer","description":"Exactly how many of that signal \u2014 not a minimum. Leave it out unless the Commander asked for a specific number."},"terraformable":{"type":"boolean","description":"Only terraforming candidates, which are worth far more to map."}},"required":[],"additionalProperties":false}
```

One index answers three questions that sound unrelated:

- **where the nearest Earth-like world is** — or neutron star, or water world, for a jump or a
  payday
- **where there is something on a surface** — biological and geological signals, and how many
- **which ring to mine** — a hotspot material, how many overlap, the ring's composition and how
  rich its reserves are

Body types are matched, not listed. There are 61 of them, so the same reasoning applies as for
module names — and the matcher takes a **unique fragment** as well as a spelling: "earth-like"
names exactly one subtype and is what a person says. "gas giant" names six, so it is refused with
the candidates rather than answered about whichever one came first.

Two things about this search were measured and are worth knowing before changing it.

**The signal filter has a third wire shape**, different from both the system filters and the
station module group. It is `{"name":{"value":["Biological"]},"count":2}` — a choice member beside
a **bare number**. The obvious spelling, `{"Biological":{"min":"1","max":"40"}}`, is accepted and
ignored: within 20 light years of Sol it returned the unfiltered 1,315 bodies, exactly as a
misspelled key did.

**A signal count is exact, not a minimum.** Asking for 1 returned 41 bodies, 2 returned 14, 3
returned none and 4 returned 2 — not a decreasing series, and every result carried precisely the
number asked for. So the schema says "exactly how many" rather than "at least", because a "three
or more" that quietly meant "exactly three" would be a wrong answer that reads like a right one.

`distance_to_arrival` is **not** offered as a filter: the service ignores it. Setting it to 0-10
light seconds returned the same 1,315 bodies as no filter at all. It is read off each result and
reported, because how far in-system a body sits is half of how far away it is — but it cannot
narrow a search, and offering it as though it could would be the silent-ignore failure with d47's
name on it.

Hotspots carry the date they were reported, for the same reason outfitting stock does. They are
crowd-sourced, and only the rings holding what you asked for are listed — a metal-rich ring with
no Painite in it is not part of the answer to "where is Painite", and naming it invites a trip to
the wrong one of two rings around the same planet.

## Notes for anyone reading the code

There is no published API for this service; the endpoints are reverse-engineered by every third
party that uses them, and the shapes d47 relies on were established against the live service on
2026-08-14. Two that matter:

- a choice filter is `{"value":["Federation"]}` — the bare string is a 400
- a range filter is `{"min":"0","max":"20"}`, with the bounds as **strings**

`search_reference` in a response is a GUID identifying the search, not the system searched from.
The system's name is `reference.name`. Reading the wrong one tells the Commander their distances
were measured from `4FF6E786-9829-11F1-A270-E7F8D53241C7`.
