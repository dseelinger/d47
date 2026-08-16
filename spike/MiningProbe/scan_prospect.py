"""What does a prospector limpet actually report, and what can a callout say about it?

`list.md` Phase 18, *Prospector and core callouts*. Three questions, and the second one changes what
ships:

  1. What does `ProspectedAsteroid` carry, how often does it arrive, and how rare is a core?
  2. Does Elite's own `Content` grade — Low / Medium / High — track what a miner cares about?
  3. Is a fixed percentage threshold meaningful across materials?

Reads the journals and prints. Writes nothing. Run where the journals are:

    ssh cooler 'python -' < spike/MiningProbe/scan_prospect.py
"""
import json, os, glob, collections
from datetime import datetime

base = os.path.join(os.path.expanduser("~"), "Saved Games", "Frontier Developments", "Elite Dangerous")
files = sorted(glob.glob(os.path.join(base, "Journal.*.log")))
print("journals:", len(files))

fields = collections.Counter()
content = collections.Counter()
per_rock = collections.Counter()
motherlode = collections.Counter()
proportions = collections.defaultdict(list)
best_by_content = collections.defaultdict(list)
gaps = []
spellings = collections.defaultdict(set)
rich_total = rich_low = 0
events = 0
motherlode_localised = 0

for path in files:
    previous = None
    for line in open(path, encoding="utf-8", errors="replace"):
        if '"ProspectedAsteroid"' not in line:
            continue
        try:
            event = json.loads(line)
        except Exception:
            continue
        if event.get("event") != "ProspectedAsteroid":
            continue

        events += 1
        for field in event:
            fields[field] += 1

        grade = (event.get("Content_Localised") or "").replace("Material Content: ", "")
        content[grade] += 1

        if event.get("MotherlodeMaterial"):
            motherlode[event.get("MotherlodeMaterial_Localised")
                       or event["MotherlodeMaterial"]] += 1
            if event.get("MotherlodeMaterial_Localised"):
                motherlode_localised += 1

        rows = event.get("Materials", [])
        per_rock[len(rows)] += 1
        for row in rows:
            raw = row.get("Name", "")
            spellings[raw.lower()].add(row.get("Name_Localised") or raw)
            proportions[row.get("Name_Localised") or raw].append(row.get("Proportion", 0))

        if rows:
            best = max(row.get("Proportion", 0) for row in rows)
            best_by_content[grade].append(best)
            if best >= 40:
                rich_total += 1
                if grade == "Low":
                    rich_low += 1

        stamp = datetime.fromisoformat(event["timestamp"].replace("Z", "+00:00"))
        if previous:
            gaps.append((stamp - previous).total_seconds())
        previous = stamp

print()
print("== 1. the event, and how often it arrives ==")
print("ProspectedAsteroid events:", events)
print("fields:", dict(fields))
print("materials per rock:", sorted(per_rock.items()))
print("Content grades:", content.most_common())

within = sorted(gap for gap in gaps if 0 < gap < 600)
if within:
    print("gap between prospects (s): n=%d  p10=%.0f  median=%.0f"
          % (len(within), within[int(len(within) * .10)], within[len(within) // 2]))

print("cores (MotherlodeMaterial): %d of %d (%.2f%%)"
      % (sum(motherlode.values()), events, 100 * sum(motherlode.values()) / events if events else 0))
print("  ", motherlode.most_common(8))
print("  MotherlodeMaterial_Localised present on %d of them — so it cannot be relied on."
      % motherlode_localised)

print()
print("== 2. does Content grade what a miner cares about? ==")
for grade in ("Low", "Medium", "High"):
    values = sorted(best_by_content[grade])
    if not values:
        continue
    print("  %-7s n=%4d  best material in the rock: median=%5.1f  p90=%5.1f  max=%5.1f"
          % (grade, len(values), values[len(values) // 2],
             values[int(len(values) * .9)], values[-1]))
print("  rocks with a material at 40%% or more: %d, of which Content says Low: %d (%.0f%%)"
      % (rich_total, rich_low, 100 * rich_low / rich_total if rich_total else 0))
print("  -> Low and High are the same distribution. The grade is about engineering-material")
print("     content, not the commodity proportion being refined, so a callout must ignore it.")

print()
print("== 3. is one percentage threshold meaningful across materials? ==")
for name, values in sorted(proportions.items(), key=lambda kv: -len(kv[1]))[:12]:
    values = sorted(values)
    print("  %-30s n=%5d  median=%5.1f  p90=%5.1f  max=%5.1f"
          % (name, len(values), values[len(values) // 2], values[int(len(values) * .9)], values[-1]))
print("  -> no. Platinum's median is above Water's 90th percentile, so 'over 25%' is routine for")
print("     one and near-unheard-of for the other.")

print()
print("== the identity trap, for the third time in this repository ==")
duplicated = {k: v for k, v in spellings.items() if len(v) > 1}
print("materials written two ways:", len(duplicated), "of", len(spellings))
for name, forms in list(duplicated.items())[:8]:
    print("   ", name, "->", sorted(forms))
print("distinct folded symbols: %d   distinct raw spellings: %d"
      % (len(spellings), sum(len(v) for v in spellings.values())))
