"""Can one be bought? The two states the ship's AI can actually meet one in.

The symbol is shared with an ordinary rack, so "is this module purchasable" and "is this
module-with-its-engineering purchasable" are different questions with different answers.
"""
import collections, glob, json, os

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")
TARGETS = {"int_cargorack_size5_class1", "int_cargorack_size6_class1"}

offered = collections.Counter()
offered_with_engineering = 0
bought = collections.Counter()
fitted = collections.Counter()
stored = collections.Counter()

for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            entry = json.loads(line)
            name = entry.get("event")

            if name == "Outfitting":
                for item in entry.get("Items", []):
                    symbol = str(item.get("Name", "")).lower()
                    if symbol in TARGETS:
                        offered[symbol] += 1
                        if any(key.lower().startswith("engineer") or key == "Engineering" for key in item):
                            offered_with_engineering += 1
            elif name == "ModuleBuy":
                symbol = str(entry.get("BuyItem", "")).strip("$").replace("_name;", "").lower()
                if symbol in TARGETS:
                    bought[symbol] += 1
            elif name == "Loadout":
                for module in entry.get("Modules", []):
                    if str(module.get("Item", "")).lower() in TARGETS:
                        pre = isinstance(module.get("Engineering"), dict)
                        fitted[(module["Item"].lower(), pre)] += 1
            elif name == "StoredModules":
                for item in entry.get("Items", []):
                    symbol = str(item.get("Name", "")).strip("$").replace("_name;", "").lower()
                    if symbol in TARGETS:
                        stored[(symbol, item.get("EngineerModifications") is not None)] += 1

print("--- offered for sale in Outfitting ---")
for symbol, n in offered.most_common():
    print(f"{n:8d}  {symbol}")
print(f"    of those, listings carrying any engineering field: {offered_with_engineering}")

print()
print("--- bought over the counter ---")
for symbol, n in bought.most_common():
    print(f"{n:8d}  {symbol}")

print()
print("--- fitted to a ship (engineered vs not) ---")
for (symbol, pre), n in sorted(fitted.items()):
    print(f"{n:8d}  {symbol}  {'pre-engineered' if pre else 'plain'}")

print()
print("--- sitting in module storage (engineered vs not) ---")
for (symbol, pre), n in sorted(stored.items()):
    print(f"{n:8d}  {symbol}  {'engineered' if pre else 'plain'}")
