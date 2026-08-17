"""Does the same symbol join across the events d47 reads it from?

scan_case.py asks whether one event spells a field two ways. This asks the harder question:
whether event A's spelling of a thing equals event B's spelling of the same thing — which is
what every join in d47 actually depends on.

Usage: python scan_join.py [journal-directory]
"""
import collections
import glob
import json
import os
import sys

HOME = os.path.expanduser("~")
DEFAULT = os.path.join(HOME, "Saved Games", "Frontier Developments", "Elite Dangerous")

# family -> list of (event, dotted path). A path segment of "[]" means "each element of".
FAMILIES = {
    "ship-type": [
        ("Loadout", "Ship"),
        ("LoadGame", "Ship"),
        ("ShipyardSwap", "ShipType"),
        ("ShipyardNew", "ShipType"),
        ("ShipyardBuy", "ShipType"),
        ("ShipyardSell", "ShipType"),
        ("StoredShips", "ShipsHere[].ShipType"),
        ("StoredShips", "ShipsRemote[].ShipType"),
        ("SetUserShipName", "Ship"),
    ],
    "module": [
        ("Loadout", "Modules[].Item"),
        ("ModuleBuy", "BuyItem"),
        ("ModuleBuy", "SellItem"),
        ("ModuleSell", "SellItem"),
        ("ModuleStore", "StoredItem"),
        ("ModuleRetrieve", "RetrievedItem"),
        ("ModuleSwap", "FromItem"),
        ("ModuleSwap", "ToItem"),
        ("StoredModules", "Items[].Name"),
        ("EngineerCraft", "Module"),
    ],
    "engineering-material": [
        ("EngineerCraft", "Ingredients[].Name"),
        ("MaterialCollected", "Name"),
        ("MaterialDiscarded", "Name"),
        ("MaterialTrade", "Paid.Material"),
        ("MaterialTrade", "Received.Material"),
        ("Materials", "Raw[].Name"),
        ("Materials", "Manufactured[].Name"),
        ("Materials", "Encoded[].Name"),
        ("Synthesis", "Materials[].Name"),
        ("TechnologyBroker", "Materials[].Name"),
    ],
    "commodity": [
        ("ColonisationConstructionDepot", "ResourcesRequired[].Name"),
        ("ColonisationContribution", "Contributions[].Name"),
        ("MarketBuy", "Type"),
        ("MarketSell", "Type"),
        ("CargoDepot", "CargoType"),
        ("CollectCargo", "Type"),
        ("EjectCargo", "Type"),
        ("Cargo", "Inventory[].Name"),
        ("MiningRefined", "Type"),
        ("ProspectedAsteroid", "Materials[].Name"),
        ("BuyDrones", "Type"),
    ],
    "blueprint": [
        ("EngineerCraft", "BlueprintName"),
        ("Loadout", "Modules[].Engineering.BlueprintName"),
    ],
    "experimental": [
        ("EngineerCraft", "ExperimentalEffect"),
        ("Loadout", "Modules[].Engineering.ExperimentalEffect"),
    ],
    "engineer": [
        ("EngineerCraft", "Engineer"),
        ("EngineerProgress", "Engineers[].Engineer"),
        ("EngineerProgress", "Engineer"),
        ("EngineerContribution", "Engineer"),
    ],
    "genus": [
        ("ScanOrganic", "Genus"),
        ("SAASignalsFound", "Genuses[].Genus"),
        ("CodexEntry", "Name"),
    ],
    "suit": [
        ("SuitLoadout", "SuitName"),
        ("BuySuit", "Name"),
        ("UpgradeSuit", "Name"),
        ("SwitchSuitLoadout", "SuitName"),
    ],
    "hand-weapon": [
        ("SuitLoadout", "Modules[].ModuleName"),
        ("BuyWeapon", "Name"),
        ("UpgradeWeapon", "Name"),
    ],
    "micro-resource": [
        ("Backpack", "Items[].Name"),
        ("ShipLocker", "Items[].Name"),
        ("TradeMicroResources", "Offered[].Name"),
        ("BuyMicroResources", "Name"),
        ("CollectItems", "Name"),
    ],
    "slot": [
        ("Loadout", "Modules[].Slot"),
        ("ModuleBuy", "Slot"),
        ("ModuleSell", "Slot"),
        ("ModuleRetrieve", "Slot"),
        ("ModuleStore", "Slot"),
    ],
}


def fold(value):
    """The same normalisation D47.Core.Journal.JournalJson.Symbol applies."""
    if not value:
        return None
    s = value.strip()
    if s.startswith("$"):
        s = s[1:]
    if s.endswith(";"):
        s = s[:-1]
    if s.lower().endswith("_name"):
        s = s[: -len("_name")]
    return s.lower() or None


def dig(node, path):
    """Yields every string at a dotted path, expanding [] over arrays."""
    if not path:
        if isinstance(node, str):
            yield node
        return
    head, _, rest = path.partition(".")
    if head.endswith("[]"):
        key = head[:-2]
        arr = node.get(key) if isinstance(node, dict) else None
        if isinstance(arr, list):
            for item in arr:
                yield from dig(item, rest)
    else:
        if isinstance(node, dict) and head in node:
            yield from dig(node[head], rest)


def main():
    directory = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    files = sorted(glob.glob(os.path.join(directory, "Journal.*.log")))
    print(f"{len(files)} journals in {directory}\n")

    wanted_events = set()
    for sources in FAMILIES.values():
        for ev, _ in sources:
            wanted_events.add(ev)

    # family -> raw spelling -> set of "event.path" that wrote it
    seen = collections.defaultdict(lambda: collections.defaultdict(set))

    for f in files:
        with open(f, encoding="utf-8", errors="replace") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    e = json.loads(line)
                except Exception:
                    continue
                kind = e.get("event")
                if kind not in wanted_events:
                    continue
                for family, sources in FAMILIES.items():
                    for ev, path in sources:
                        if ev != kind:
                            continue
                        for value in dig(e, path):
                            seen[family][value].add(f"{ev}.{path}")

    for family in FAMILIES:
        raw = seen[family]
        if not raw:
            print(f"### {family}: NOTHING IN CORPUS\n")
            continue

        folded = collections.defaultdict(set)
        for spelling in raw:
            f = fold(spelling)
            if f:
                folded[f].add(spelling)

        clashes = {k: v for k, v in folded.items() if len(v) > 1}
        # A clash matters most when the two spellings come from different events, because that
        # is a join d47 makes rather than a field it merely reads.
        cross = {}
        for k, spellings in clashes.items():
            sources = {}
            for s in spellings:
                sources[s] = sorted(seen[family][s])
            if len({tuple(v) for v in sources.values()}) > 1:
                cross[k] = sources

        print(f"### {family}: {len(raw)} raw spellings -> {len(folded)} folded; "
              f"{len(clashes)} folded values with >1 spelling, {len(cross)} of them cross-event")
        for k, sources in sorted(cross.items())[:10]:
            print(f"    {k}")
            for s, evs in sorted(sources.items()):
                print(f"        {s!r:45s} {evs}")
        if len(cross) > 10:
            print(f"    … and {len(cross) - 10} more")
        print()


if __name__ == "__main__":
    main()
