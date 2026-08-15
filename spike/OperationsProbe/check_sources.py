"""Do the two pre-engineered blueprint names exist in the sources d47 generates from?

The item asks specifically about FDevIDs outfitting.csv and EDEngineer's list, so both are
fetched rather than inferred from the generated table.
"""
import io, json, urllib.request, csv, sys

NEEDLES = ["CargoRack_IncreasedCapacity", "PowerDistributor_PrioritySystems"]

SOURCES = {
    "EDEngineer blueprints.json":
        "https://raw.githubusercontent.com/msarilar/EDEngineer/master/EDEngineer/Resources/Data/blueprints.json",
    "FDevIDs outfitting.csv":
        "https://raw.githubusercontent.com/EDCD/FDevIDs/master/outfitting.csv",
    "coriolis modifications/blueprints.json":
        "https://raw.githubusercontent.com/EDCD/coriolis-data/master/modifications/blueprints.json",
}

def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "d47-operations-spike"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read().decode("utf-8-sig", errors="replace")

for label, url in SOURCES.items():
    try:
        body = fetch(url)
    except Exception as error:
        print(f"{label}: UNREACHABLE ({error})")
        continue

    print(f"--- {label}  ({len(body):,} bytes) ---")
    for needle in NEEDLES:
        hits = body.lower().count(needle.lower())
        print(f"    {needle}: {hits} occurrence(s)")

    # A weaker question too: does the source know about cargo rack engineering at all?
    for word in ("cargorack", "increasedcapacity", "prioritysystems"):
        print(f"    (any {word!r}: {body.lower().count(word)})")
    print()
