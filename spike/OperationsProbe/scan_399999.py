"""What a module with EngineerID 399999 actually is, and whether it can be worked on.

Answers the item's four questions in order: what event records the purchase, whether such a
module carries an Engineering block and which BlueprintName it names, and whether it is ever
engineered further on top.
"""
import collections, glob, json, os

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")

SENTINEL = 399999

shapes = collections.Counter()        # (module, blueprint, level, quality, experimental)
modifiers_seen = collections.Counter()
first_sample = {}
sentinel_modules = set()
crafted_modules = collections.Counter()
crafted_blueprints = collections.defaultdict(collections.Counter)
buys = collections.Counter()
first_seen = {}
buy_events = collections.defaultdict(list)

WATCH_BUY = {"ModuleBuy", "ModuleBuyAndStore", "ModuleRetrieve", "FetchRemoteModule",
             "ModuleStore", "StoredModules", "ShipRedeemed", "ShipyardRedeem",
             "TechnologyBroker", "CarrierModulePack", "LoadoutEquipModule"}

for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            entry = json.loads(line)
            name = entry.get("event")
            stamp = entry.get("timestamp", "")

            if name == "Loadout":
                for module in entry.get("Modules", []):
                    engineering = module.get("Engineering")
                    if not isinstance(engineering, dict):
                        continue
                    item = module.get("Item", "?")
                    if engineering.get("EngineerID") == SENTINEL:
                        sentinel_modules.add(item)
                        key = (item,
                               engineering.get("BlueprintName"),
                               engineering.get("Level"),
                               round(float(engineering.get("Quality", 0)), 4),
                               engineering.get("ExperimentalEffect", "(none)"))
                        shapes[key] += 1
                        first_seen.setdefault(key, stamp)
                        first_sample.setdefault(key, json.dumps(module)[:600])
                        for modifier in engineering.get("Modifiers", []):
                            modifiers_seen[(item, modifier.get("Label"))] += 1

            elif name == "EngineerCraft":
                crafted_modules[entry.get("Module", "?")] += 1
                crafted_blueprints[entry.get("Module", "?")][entry.get("BlueprintName")] += 1

            elif name in WATCH_BUY:
                blob = json.dumps(entry)
                if "cargorack" in blob.lower():
                    buys[name] += 1
                    if len(buy_events[name]) < 3:
                        buy_events[name].append(blob[:320])

print("--- every distinct shape carrying EngineerID 399999 ---")
print(f"{'module':46} {'blueprint':34} {'lvl':>3} {'quality':>8} {'seen':>6}  first")
for key, n in sorted(shapes.items(), key=lambda kv: -kv[1]):
    item, blueprint, level, quality, experimental = key
    print(f"{item:46} {str(blueprint):34} {str(level):>3} {quality:>8} {n:>6}  {first_seen[key][:10]}  exp={experimental}")

print()
print("--- one whole module, verbatim ---")
for key in list(first_sample)[:1]:
    print(first_sample[key])

print()
print("--- modifiers these carry ---")
for (item, label), n in modifiers_seen.most_common():
    print(f"{n:6d}  {item}  {label}")

print()
print("--- was a module of this kind ever engineered by hand? ---")
for item in sorted(sentinel_modules):
    n = crafted_modules.get(item, 0)
    detail = ", ".join(f"{b} x{c}" for b, c in crafted_blueprints[item].most_common()) or "never"
    print(f"{item}: {n} EngineerCraft events — {detail}")

print()
print("--- purchase-shaped events mentioning a cargo rack ---")
for name, n in buys.most_common():
    print(f"{n:6d}  {name}")
    for sample in buy_events[name]:
        print(f"        {sample}")
