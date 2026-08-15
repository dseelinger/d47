"""How much of what the journal names do the sources not know, and how much is not makeable?

Corrected after a first attempt joined journal blueprint SYMBOLS against d47's shipped table,
which is keyed on EDEngineer DISPLAY NAMES and carries no symbol column at all (the Step 6
finding). That join could only ever report "all 35 unknown", which is a fact about the join.
Coriolis is keyed on fdname, so it is the only source this question can be asked of.
"""
import collections, csv, glob, io, json, os, urllib.request

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")

def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "d47-operations-spike"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read().decode("utf-8-sig", errors="replace")

coriolis = json.loads(fetch(
    "https://raw.githubusercontent.com/EDCD/coriolis-data/master/modifications/blueprints.json"))
outfitting = {row["symbol"].lower()
              for row in csv.DictReader(io.StringIO(fetch(
                  "https://raw.githubusercontent.com/EDCD/FDevIDs/master/outfitting.csv")))
              if row.get("symbol")}

def recipeless(entry):
    """A blueprint with no ingredients at any grade is one nobody can gather for."""
    grades = entry.get("grades", {})
    return bool(grades) and all(not grade.get("components") for grade in grades.values())

blueprints = collections.Counter()
modules = collections.Counter()
sentinel = set()

for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            entry = json.loads(line)
            if entry.get("event") == "Loadout":
                for module in entry.get("Modules", []):
                    modules[module.get("Item", "?").lower()] += 1
                    engineering = module.get("Engineering")
                    if isinstance(engineering, dict):
                        blueprints[engineering.get("BlueprintName")] += 1
                        if engineering.get("EngineerID") == 399999:
                            sentinel.add(engineering.get("BlueprintName"))

unknown = sorted(b for b in blueprints if b not in coriolis)
print(f"journal blueprint symbols: {len(blueprints)}; coriolis knows {len(blueprints) - len(unknown)}")
print(f"unknown to coriolis: {unknown or 'none'}")

print()
print(f"--- coriolis blueprints with no ingredients at any grade ({len(coriolis)} total) ---")
for key, entry in sorted(coriolis.items()):
    if recipeless(entry):
        grades = ",".join(sorted(entry.get("grades", {})))
        seen = f"  seen {blueprints[key]}x here" if key in blueprints else ""
        mark = "  <-- sentinel in this corpus" if key in sentinel else ""
        print(f"    {key}  grades={grades}  module={entry.get('modulename')}{seen}{mark}")

real = [m for m in modules if m not in outfitting and (m.startswith("int_") or m.startswith("hpt_"))]
cosmetic = [m for m in modules if m not in outfitting and not (m.startswith("int_") or m.startswith("hpt_"))]
print()
print(f"--- module symbols FDevIDs has never heard of ---")
print(f"    real modules (int_/hpt_): {len(real)}  -> {sorted(real) or 'none'}")
print(f"    cosmetics and cockpits:   {len(cosmetic)}  (bobbles, ship kits, paint, cockpits)")
