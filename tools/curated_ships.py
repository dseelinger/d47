"""Hand-kept hull sets that `gen-elite-specs.py` reads."""

# Hull symbols as the journal writes them, sold for ARX only. Remove a symbol when the hull
# reaches the shipyard for credits.
ARX_EARLY_ACCESS: set[str] = {"anacondamkii"}

# Manufacturers for hulls coriolis-data does not carry, keyed by journal symbol. coriolis-data
# wins where it has one.
MANUFACTURERS: dict[str, str] = {"corsair": "Gutamaya"}
