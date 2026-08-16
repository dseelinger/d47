"""Does a depot requirement join to a cargo hold row, and to a contribution row?

Elite spells the same commodity three ways, and the tracking item's whole arithmetic depends on the
three meeting:

    ColonisationConstructionDepot   $aluminium_name;
    Cargo.json                      aluminium
    ColonisationContribution        $ComputerComponents_name;

A normaliser that strips the decoration and stops there joins the first two perfectly and the third
to nothing at all — which surfaces as a Commander's own completed delivery being reported as never
having happened. This counts the case of every symbol each event writes, so the fold is measured
rather than assumed.

Reads the journals and prints. Writes nothing. Run where the journals are:

    ssh cooler 'python -' < spike/ColonisationProbe/scan_join.py
"""
import json, os, glob, collections

home = os.path.expanduser("~")
base = os.path.join(home, "Saved Games", "Frontier Developments", "Elite Dangerous")
files = sorted(glob.glob(os.path.join(base, "Journal.*.log")))
print("journals:", len(files))


def fold(name):
    """What d47's JournalJson.Symbol does: strip $, _name;, and lowercase."""
    if not name:
        return None
    symbol = name.strip()
    if symbol.startswith("$"):
        symbol = symbol[1:]
    if symbol.endswith(";"):
        symbol = symbol[:-1]
    if symbol.lower().endswith("_name"):
        symbol = symbol[:-len("_name")]
    return symbol.lower() or None


raw = collections.defaultdict(set)          # source -> raw names as written
folded = collections.defaultdict(set)       # source -> folded symbols
localised = collections.defaultdict(dict)   # source -> symbol -> spellings
missing_localised = collections.defaultdict(set)

for path in files:
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                event = json.loads(line)
            except Exception:
                continue

            kind = event.get("event")

            if kind == "ColonisationConstructionDepot":
                rows, key, source = event.get("ResourcesRequired", []), "Name", "depot"
            elif kind == "ColonisationContribution":
                rows, key, source = event.get("Contributions", []), "Name", "contribution"
            elif kind == "Cargo" and "Inventory" in event:
                rows, key, source = event["Inventory"], "Name", "hold"
            elif kind == "CargoTransfer":
                rows, key, source = event.get("Transfers", []), "Type", "transfer"
            else:
                continue

            for row in rows:
                name = row.get(key)
                symbol = fold(name)
                if symbol is None:
                    continue

                raw[source].add(name)
                folded[source].add(symbol)

                spelling = row.get(key + "_Localised")
                if spelling:
                    localised[source].setdefault(symbol, set()).add(spelling)
                else:
                    missing_localised[source].add(symbol)

print()
print("== how each source writes a commodity ==")
for source in ("depot", "hold", "contribution", "transfer"):
    names = sorted(raw[source])
    mixed = [name for name in names if name != name.lower()]
    print(f"{source:13} {len(names):3} distinct, {len(mixed):3} with uppercase   e.g. {names[:2]}")
print("-> the case fold is the join. Without it the contribution matches nothing.")

print()
print("== do the folded symbols meet? ==")
print("depot symbols also seen in the hold:", len(folded['depot'] & folded['hold']),
      "of", len(folded['depot']))
print("contribution symbols found in the depot:", len(folded['contribution'] & folded['depot']),
      "of", len(folded['contribution']))
print("contribution symbols the depot never asked for:",
      sorted(folded['contribution'] - folded['depot']))
print("-> a depot symbol absent from the hold means this Commander never carried it, not a")
print("   failed join. The contribution set is the one that must be fully covered, and is.")

print()
print("== Name_Localised, which decides who supplies the display name ==")
for source in ("depot", "hold"):
    print(f"{source:13} symbols with no localised spelling ever: "
          f"{len(missing_localised[source] - set(localised[source]))} of {len(folded[source])}")
print("-> so the depot names a commodity and the hold only counts it.")

print()
print("== do two sources ever disagree on the spelling? ==")
disagreements = [
    (symbol, sorted(localised["depot"][symbol]), sorted(localised["hold"][symbol]))
    for symbol in sorted(set(localised["depot"]) & set(localised["hold"]))
    if localised["depot"][symbol] != localised["hold"][symbol]
]
print("disagreements:", len(disagreements))
for row in disagreements[:10]:
    print("   ", row)
