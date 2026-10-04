"""Species spansh's own landmark value calls zero, and the value they actually sell for.

<https://github.com/dseelinger/d47/issues/528>

`gen-exobiology.py` treats a zero landmark value as "not a species" (`sample()`, "Not a
species — a point of interest"), which is right for the wrecks and settlements that share the
`landmarks` array. Radicoida Unica fails that test and is still a species: it is also missing
from spansh's `landmark_subtype` field-values list, the enumeration `candidate_names()` checks
against, so the generator never asked spansh about it at all. Its four surveyed bodies, all in
HIP 87621, carry the landmark with `value: 0` when queried directly by name. The value below,
119,037, comes from the Commander's own `SellOrganicData` entries.

`gen-exobiology.py` adds every name here to its candidates, uses the curated value where spansh's
is zero, and prints a notice to drop the entry once spansh reports a non-zero value for it.
"""

# species -> value, for a landmark spansh finds but prices at zero.
CURATED_VALUES = {
    "Radicoida Unica": 119_037,
}

# genus -> metres a sample must be from the last one of the same species. Frontier publishes no
# table, so these are community figures: ArtemisScannerTracker's organicinfo.py
# (https://github.com/Balvald/ArtemisScannerTracker), HerzbubeWiki's EDExobiology page
# (https://wiki.herzbube.ch/wiki/EDExobiology) and EDCoPilot's spoken callouts. Only the numbers
# are taken. A genus not listed has no value.
COLONY_DISTANCES = {
    "Aleoida": 150,
    "Bacterium": 500,
    "Cactoida": 300,
    "Clypeus": 150,
    "Concha": 150,
    "Electricae": 1000,
    "Fonticulua": 500,
    "Frutexa": 150,
    "Fumerola": 100,
    "Fungoida": 300,
    "Osseus": 800,
    "Recepta": 150,
    "Stratum": 500,
    "Tubus": 800,
    "Tussock": 200,
    "Amphora Plant": 100,
    "Anemone": 100,
    "Bark Mounds": 100,
    "Brain Tree": 100,
    "Radicoida": 15,
    "Shards": 100,
    "Tubers": 100,
}
