"""EDEngineer's display names, and the five places they drift from Frontier's.

Shared by `gen-materials.py` and `gen-blueprints.py` because both join EDEngineer to FDevIDs
on the display name, and a drift fixed in one and not the other loses ingredient references
silently — which is the specific failure the whole join is arranged to avoid. One definition,
so a new drift is repaired once.

**Why the join is on display name at all.** EDEngineer's own `FormattedName` is not Frontier's
symbol: it keeps the leading adjective Frontier drops, so `anomalousbulkscandata` against
`bulkscandata`, and it differs for 22 materials — every one of them Encoded. Keying on it
throws nothing and silently loses 22 of the 45 Encoded materials.

**Why these five and not a similarity threshold.** Each is asserted below to be *drift* rather
than *absence*: the name must appear in exactly one source and its counterpart in exactly the
other. Three real near-misses fail that test — `Geographical Data` is not `Geological Data`,
`Mineral Analytics` is not `Mining Analytics`, `Security Plans` is not `Settlement Defence
Plans`. Both spellings of each exist in EDEngineer and only one in FDevIDs, so those are
distinct items with no symbol, and aliasing them would hang one item's data on a different
real item. Quieter than having none, and worse.
"""

ALIASES = {
    "Abnormal Compact Emission Data": "Abnormal Compact Emissions Data",
    "Ballistic Data": "Ballistics Data",
    "Guardian Wreckage Components": "Guardian Sentinel Wreckage Components",
    "Ship System Data": "Ship Systems Data",
    "Xihe Companions": "Xihe Biomorphic Companions",
}


def canonical(name: str) -> str:
    """The FDevIDs spelling of an EDEngineer display name."""
    return ALIASES.get(name.strip(), name.strip())


def check(frontier_names, edengineer_names) -> None:
    """Stop the run unless every alias is still drift rather than two separate items.

    An alias whose target has vanished means the drift moved. An alias where either source now
    carries both spellings means they were never one thing, and the alias is actively wrong.
    """
    theirs = set(edengineer_names)
    ours = set(frontier_names)

    for source, target in ALIASES.items():
        if target not in ours:
            raise SystemExit(
                f"alias {source!r} -> {target!r}: the target is no longer in FDevIDs, so the "
                "drift has moved and the pair needs looking at again"
            )

        if source in ours:
            raise SystemExit(
                f"alias {source!r} -> {target!r}: FDevIDs now carries both spellings, so these "
                "are two items rather than one and the alias is wrong"
            )

        if target in theirs:
            raise SystemExit(
                f"alias {source!r} -> {target!r}: EDEngineer now carries both spellings, so "
                "these are two items rather than one and the alias is wrong"
            )
