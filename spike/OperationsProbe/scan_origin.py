"""Where a 399999 module comes from, and whether Quality 1.0 is peculiar to it.

The purchase question cannot be answered by looking for a purchase event that carries an
Engineering block, because none does. So instead: find the first moment each pre-engineered
module is on a ship, and read what happened immediately before it.
"""
import collections, glob, json, os

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")
SENTINEL = 399999
TARGETS = {"int_cargorack_size5_class1", "int_cargorack_size6_class1", "int_powerdistributor_size4_class5"}

quality_by_source = collections.Counter()   # (is_sentinel, quality==1.0)
exact_one_by_engineer = collections.Counter()
history = collections.deque(maxlen=40)
first_hit = {}
seen_targets = set()

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
                    sentinel = engineering.get("EngineerID") == SENTINEL
                    quality = float(engineering.get("Quality", 0))
                    quality_by_source[(sentinel, quality == 1.0)] += 1
                    if quality == 1.0:
                        exact_one_by_engineer[engineering.get("Engineer", f"id {engineering.get('EngineerID')}")] += 1

                    item = module.get("Item")
                    if sentinel and item in TARGETS and item not in seen_targets:
                        seen_targets.add(item)
                        first_hit[item] = (entry.get("timestamp"), os.path.basename(path), list(history))

            history.append((entry.get("timestamp"), name, json.dumps(entry)[:200]))

print("--- Quality exactly 1.0, by whether the engineer is the sentinel ---")
for (sentinel, exact), n in sorted(quality_by_source.items()):
    who = "EngineerID 399999" if sentinel else "a named engineer"
    print(f"{n:8d}  {who}, quality {'== 1.0' if exact else '!= 1.0'}")

print()
print("--- who produces a quality of exactly 1.0 ---")
for who, n in exact_one_by_engineer.most_common(12):
    print(f"{n:8d}  {who}")

for item, (stamp, journal, before) in first_hit.items():
    print()
    print(f"=== first sighting of {item} at {stamp} ({journal}) ===")
    for i, (ts, name, blob) in enumerate(before[-18:]):
        print(f"  {ts}  {name}")
