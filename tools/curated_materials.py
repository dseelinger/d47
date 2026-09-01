"""Materials the game has and no id list names.

<https://github.com/dseelinger/d47/issues/127>

Read by `gen-materials.py` and by `gen-material-grades.py`, which build two different tables
from the same authority and are asserted against each other by
`MaterialCatalogueTests.EveryGradeAgreesWithTheTableTheRestOfCoreAlreadyUses`. **One list, for
the reason `edengineer_names.ALIASES` is one list**: a curated row repaired in one generator and
not the other is a disagreement between two shipped tables, found by a test failure rather than
by anybody noticing.

Why any of this exists
----------------------
`EDCD/FDevIDs` is the naming authority and carries no row for these three, by display name or by
symbol. Nor does EDEngineer, nor coriolis, nor 941 journals across three Commanders. They are the
ingredients of `GuardianModule_Sturdy` — Anti-Guardian Zone Resistance — which Frontier offers on
nine module types and which could therefore be costed by nothing at all, so the Ships page said
*"I have no recipe for this"* about a blueprint the Commander was standing in front of.

Added 2026-09-01 on the Commander's ruling: **"go with what EDSY and EDOMH agree on"**.

The warrant, column by column
-----------------------------
**symbol** — EDSY's `eddb.js` and ED Odyssey Materials Helper's
`locale/material/horizons/manufactured.csv` (MIT) agree on all three spellings, and neither is
downstream of the other. EDOMH's is load-bearing in its own app: it is the key it counts a
journal inventory by, so a wrong one would show a permanent zero to every user who gathered one.

**name** — both sources, taking EDSY's bare form. EDOMH suffixes "(Thargoid)" to several
materials FDevIDs names plainly — its "Weapon Parts (Thargoid)" is FDevIDs' "Weapon Parts" — so
the suffix is its own disambiguation rather than Frontier's name.

**category** — Manufactured. EDSY files all three `mattype:'mfc'`; EDOMH files them under
`material.manufactured.*`.

**grade** — from the capacity EDOMH shows against each one, through Frontier's published ladder
(300/250/200/150/100 = grades 1 to 5). **Validated rather than assumed**: the same screen carries
16 Thargoid materials that FDevIDs *does* key, and the capacity agrees with the published rarity
in all 16, across grades 2 to 5. This overrules EDSY on Tactical Core Chip, which it calls rarity
2 against a capacity of 100 — and EDSY's own row admits it does not know that material's group or
id.

**line** — none. Guardian and Thargoid materials are not traded, which is the same answer
FDevIDs' `category` gives its own 29 rows of them.

Each one retires itself
-----------------------
Both generators drop a curated row the day the authority names it, and say so on the run. A list
like this shadowing a source that has caught up is how a table goes quietly stale.
"""

# symbol, name, category, grade
CURATED = [
    ("tg_abrasion03", "Hardened Surface Fragments", "Manufactured", 1),
    ("tg_causticcrystal", "Caustic Crystal", "Manufactured", 4),
    ("unknowncorechip", "Tactical Core Chip", "Manufactured", 5),
]
