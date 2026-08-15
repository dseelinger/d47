"""Is a blueprint that appears with the sentinel ever also rolled by a named engineer?

Decides whether 399999 marks the blueprint or only the module.
"""
import collections, glob, json, os

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")
SENTINEL = 399999
WATCH = ("CargoRack_IncreasedCapacity", "PowerDistributor_PrioritySystems")

by_blueprint = collections.defaultdict(collections.Counter)
distributor_modules = collections.Counter()
craft_blueprints = collections.Counter()

for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            entry = json.loads(line)
            name = entry.get("event")
            if name == "Loadout":
                for module in entry.get("Modules", []):
                    engineering = module.get("Engineering")
                    if not isinstance(engineering, dict):
                        continue
                    blueprint = engineering.get("BlueprintName")
                    who = "399999" if engineering.get("EngineerID") == SENTINEL else engineering.get("Engineer", "?")
                    by_blueprint[blueprint][who] += 1
                    if blueprint == "PowerDistributor_PrioritySystems":
                        distributor_modules[(module.get("Item"), who)] += 1
            elif name == "EngineerCraft":
                craft_blueprints[entry.get("BlueprintName")] += 1

for blueprint in WATCH:
    print(f"--- {blueprint} in Loadout, by engineer ---")
    for who, n in by_blueprint[blueprint].most_common():
        print(f"    {n:6d}  {who}")
    print(f"    EngineerCraft events for it: {craft_blueprints.get(blueprint, 0)}")
    print()

print("--- which modules carry PowerDistributor_PrioritySystems ---")
for (item, who), n in distributor_modules.most_common():
    print(f"    {n:6d}  {item}  (engineer {who})")
