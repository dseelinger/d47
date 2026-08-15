"""What module storage says about engineering, since that is where these modules came from."""
import collections, glob, json, os

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")

keys_on_stored = collections.Counter()
retrieve_keys = collections.Counter()
stored_hits = collections.Counter()
samples = {}

for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            entry = json.loads(line)
            name = entry.get("event")
            if name == "StoredModules":
                for item in entry.get("Items", []):
                    for key in item:
                        keys_on_stored[key] += 1
                    if "EngineerModifications" in item:
                        stored_hits[item["EngineerModifications"]] += 1
                        samples.setdefault(item["EngineerModifications"], json.dumps(item))
            elif name in ("ModuleRetrieve", "ModuleStore", "ModuleSell", "ModuleSellRemote"):
                for key in entry:
                    retrieve_keys[f"{name}.{key}"] += 1

print("--- fields on a StoredModules item ---")
for key, n in keys_on_stored.most_common():
    print(f"{n:8d}  {key}")

print()
print("--- engineering named in storage: the pre-engineered ones ---")
for blueprint, n in stored_hits.most_common():
    flag = "  <-- pre-engineered" if blueprint in ("CargoRack_IncreasedCapacity", "PowerDistributor_PrioritySystems") else ""
    print(f"{n:8d}  {blueprint}{flag}")

print()
print("--- a stored pre-engineered rack, verbatim ---")
for blueprint in ("CargoRack_IncreasedCapacity", "PowerDistributor_PrioritySystems"):
    if blueprint in samples:
        print(samples[blueprint][:400])

print()
print("--- engineering-related fields on the move events ---")
for key, n in retrieve_keys.most_common():
    if "Engineer" in key or "Level" in key or "Quality" in key:
        print(f"{n:8d}  {key}")
