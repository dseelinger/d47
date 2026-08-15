"""What events exist at all, and which of them could record an Operations purchase.

Reconnaissance only. The item names four questions; this one answers "what event records
the purchase" by refusing to guess the name and counting every event instead.
"""
import collections, glob, json, os, sys

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")

counts = collections.Counter()
files = sorted(glob.glob(JOURNALS))
bad = 0
for path in files:
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            try:
                counts[json.loads(line)["event"]] += 1
            except Exception:
                bad += 1

print(f"{len(files)} journals, {sum(counts.values())} events, {len(counts)} distinct, {bad} unparsable")
print()
needles = ("merc", "coin", "front", "oper", "micro", "suit", "weapon", "module", "broker", "redeem", "power")
print("--- events whose name mentions anything the item names ---")
for name, n in sorted(counts.items()):
    if any(needle in name.lower() for needle in needles):
        print(f"{n:8d}  {name}")
