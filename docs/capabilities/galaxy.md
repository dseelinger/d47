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

The filters are `distance`, `population`, `allegiance`, `government`, `primary_economy` and
`security`. Ranges take one number for an upper bound (`30` means within 30) or two separated by
a dash (`10-50`, `1000000-`).

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
{"type":"object","properties":{"allegiance":{"type":"string","description":"Superpower allegiance.","enum":["Alliance","Empire","Federation","Guardian","Independent","Pilots Federation","Thargoid"]},"distance":{"type":"string","description":"How far to look, in light years. For example \u002230\u0022 or \u002210-50\u0022."},"government":{"type":"string","description":"Form of government.","enum":["Anarchy","Communism","Confederacy","Cooperative","Corporate","Democracy","Dictatorship","Feudal","None","Patronage","Prison","Prison Colony","Theocracy"]},"limit":{"type":"integer","description":"How many to return, 1 to 20. Defaults to 5."},"near":{"type":"string","description":"The system to measure distances from. Defaults to where the Commander is now."},"population":{"type":"string","description":"Population, as a range. For example \u00221000000-\u0022 for a million or more."},"primary_economy":{"type":"string","description":"The system\u0027s main economy.","enum":["Agriculture","Colony","Extraction","High Tech","Industrial","Military","None","Refinery","Service","Terraforming","Tourism"]},"security":{"type":"string","description":"Security level.","enum":["Anarchy","High","Low","Medium"]}},"required":[],"additionalProperties":false}
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
{"type":"object","properties":{"large_pad":{"type":"boolean","description":"Only stations with a large landing pad."},"limit":{"type":"integer","description":"How many to return, 1 to 20. Defaults to 5."},"max_distance":{"type":"number","description":"How far to look, in light years. Defaults to 50."},"module":{"type":"string","description":"A module to be sold there, by name \u2014 for example \u0022Frame Shift Drive\u0022 or \u0022Bi-Weave Shield Generator\u0022."},"module_class":{"type":"string","description":"Module size, 0 to 8.","enum":["0","1","2","3","4","5","6","7","8"]},"module_rating":{"type":"string","description":"Module rating, A to I.","enum":["A","B","C","D","E","F","G","H","I"]},"near":{"type":"string","description":"The system to search out from. Defaults to where the Commander is now."},"ship":{"type":"string","description":"A ship to be sold there, by name \u2014 for example \u0022Krait MkII\u0022."}},"required":[],"additionalProperties":false}
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

## Notes for anyone reading the code

There is no published API for this service; the endpoints are reverse-engineered by every third
party that uses them, and the shapes d47 relies on were established against the live service on
2026-08-14. Two that matter:

- a choice filter is `{"value":["Federation"]}` — the bare string is a 400
- a range filter is `{"min":"0","max":"20"}`, with the bounds as **strings**

`search_reference` in a response is a GUID identifying the search, not the system searched from.
The system's name is `reference.name`. Reading the wrong one tells the Commander their distances
were measured from `4FF6E786-9829-11F1-A270-E7F8D53241C7`.
