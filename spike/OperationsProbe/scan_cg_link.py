"""Did the pre-engineered racks appear in storage because of a community goal reward?

Two independent checks: the timeline around the reward, and whether BuyPrice tells a granted
module from a bought one.
"""
import collections, glob, json, os

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")
PRE = {"CargoRack_IncreasedCapacity", "PowerDistributor_PrioritySystems"}

first_in_storage = {}
buyprice = collections.defaultdict(collections.Counter)
where = collections.Counter()
timeline = []

for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            entry = json.loads(line)
            name = entry.get("event", "")
            stamp = entry.get("timestamp", "")

            if name == "StoredModules":
                for item in entry.get("Items", []):
                    blueprint = item.get("EngineerModifications")
                    label = blueprint if blueprint in PRE else (
                        "other engineered" if blueprint else "not engineered")
                    buyprice[label][item.get("BuyPrice", "?") == 0] += 1
                    if blueprint in PRE:
                        where[(blueprint, item.get("StarSystem"))] += 1
                        key = (blueprint, item.get("Name"), item.get("StorageSlot"))
                        first_in_storage.setdefault(key, (stamp, json.dumps(item)))

            if name.startswith("CommunityGoal") and "Reward" in name:
                timeline.append((stamp, f"REWARD {entry.get('Reward'):,} Cr — {entry.get('Name')} ({entry.get('System')})"))

print("--- BuyPrice of stored modules: is zero peculiar to the pre-engineered ones? ---")
print(f"{'group':36} {'BuyPrice == 0':>14} {'BuyPrice > 0':>13}")
for label, counts in buyprice.items():
    print(f"{label:36} {counts[True]:>14,} {counts[False]:>13,}")

print()
print("--- where the pre-engineered modules sit in storage ---")
for (blueprint, system), n in where.most_common(10):
    print(f"{n:6d}  {blueprint} @ {system}")

print()
print("--- first time each pre-engineered module is seen in storage ---")
for (blueprint, module, slot), (stamp, blob) in sorted(first_in_storage.items(), key=lambda kv: kv[1][0]):
    print(f"{stamp}  slot {slot:>4}  {blueprint}")
    print(f"        {blob[:230]}")

print()
print("--- community goal rewards, in order ---")
for stamp, text in timeline:
    print(f"{stamp}  {text}")
