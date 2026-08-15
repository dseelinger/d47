"""Q4: can a module that arrived pre-engineered be engineered further on top?

Traced per (ship id, slot) through time, because "the module type was crafted somewhere" is a
different claim from "this module was". Also lists what EDEngineer knows about the blueprint
family, so an absence can be told from a gap in the family.
"""
import collections, glob, json, os, urllib.request

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")
SENTINEL = 399999

# (ShipID, Slot) -> ordered list of (timestamp, engineer id, blueprint, level)
timeline = collections.defaultdict(list)
crafts = []

for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            entry = json.loads(line)
            name = entry.get("event")
            if name == "Loadout":
                ship = entry.get("ShipID")
                for module in entry.get("Modules", []):
                    engineering = module.get("Engineering")
                    if not isinstance(engineering, dict):
                        continue
                    key = (ship, module.get("Slot"), module.get("Item"))
                    state = (engineering.get("EngineerID"),
                             engineering.get("BlueprintName"),
                             engineering.get("Level"))
                    if not timeline[key] or timeline[key][-1][1:] != state:
                        timeline[key].append((entry.get("timestamp"), *state))
            elif name == "EngineerCraft":
                crafts.append((entry.get("timestamp"), entry.get("ShipID"), entry.get("Slot"),
                               entry.get("Module"), entry.get("BlueprintName"), entry.get("EngineerID")))

print("--- every slot that ever held a pre-engineered module, and what happened to it ---")
touched = 0
for (ship, slot, item), states in sorted(timeline.items(), key=lambda kv: str(kv[0])):
    if not any(state[1] == SENTINEL for state in states):
        continue
    changed = len(states) > 1
    touched += 1 if changed else 0
    print(f"ship {ship} {slot} {item}")
    for stamp, engineer_id, blueprint, level in states:
        who = "399999 (no engineer)" if engineer_id == SENTINEL else str(engineer_id)
        print(f"    {stamp[:10]}  engineer={who:22} {blueprint} G{level}")

print()
print(f"{touched} of those slots ever changed engineering state.")

print()
print("--- EngineerCraft against a slot that held a pre-engineered module ---")
sentinel_slots = {(ship, slot) for (ship, slot, item), states in timeline.items()
                  if any(state[1] == SENTINEL for state in states)}
hits = [c for c in crafts if (c[1], c[2]) in sentinel_slots]
print(f"{len(hits)} craft events landed on one of those {len(sentinel_slots)} slots")
for stamp, ship, slot, module, blueprint, engineer_id in hits[:10]:
    print(f"    {stamp[:10]}  ship {ship} {slot} {module} -> {blueprint} (engineer {engineer_id})")

url = ("https://raw.githubusercontent.com/msarilar/EDEngineer/master/"
       "EDEngineer/Resources/Data/blueprints.json")
request = urllib.request.Request(url, headers={"User-Agent": "d47-operations-spike"})
with urllib.request.urlopen(request, timeout=60) as response:
    edengineer = json.loads(response.read().decode("utf-8-sig"))

print()
print("--- what EDEngineer knows about power distributors and cargo racks ---")
names = sorted({b.get("Name", "?") for b in edengineer
                if "power distributor" in str(b.get("Type", "")).lower()})
print(f"Power Distributor blueprints in EDEngineer ({len(names)}): {', '.join(names)}")
racks = sorted({b.get("Name", "?") for b in edengineer
                if "cargo" in str(b.get("Type", "")).lower()})
print(f"Cargo Rack blueprints in EDEngineer ({len(racks)}): {racks or '(none)'}")
