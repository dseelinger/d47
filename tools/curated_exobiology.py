"""Species spansh's own landmark value calls zero, and the value they actually sell for.

<https://github.com/dseelinger/d47/issues/528>

`gen-exobiology.py` treats a zero landmark value as "not a species" (`sample()`, "Not a
species — a point of interest"), which is right for the wrecks and settlements that share the
`landmarks` array. Radicoida Unica fails that test and is still a species: it is also missing
from spansh's `landmark_subtype` field-values list, the enumeration `candidate_names()` checks
against, so the generator never asked spansh about it at all. Its four surveyed bodies, all in
HIP 87621, carry the landmark with `value: 0` when queried directly by name — Frontier's
Community Goal reward pricing is not what spansh's zero-value filter was built to separate from
a point of interest. The value below, 119,037, comes from the Commander's own `SellOrganicData`
entries.

Each one retires itself: a name here is dropped from the generator's own candidate list the day
spansh reports a non-zero landmark value for it.
"""

# species -> value, for a landmark spansh finds but prices at zero.
CURATED_VALUES = {
    "Radicoida Unica": 119_037,
}
