"""Do community goal events exist here, and do they name a module?

Written after being told these modules are community goal rewards — which the phase item
itself said the wiki claims, and which the first pass never searched for.
"""
import collections, glob, json, os

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")

events = collections.Counter()
samples = collections.defaultdict(list)
titles = collections.Counter()
reward_blobs = []

for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            entry = json.loads(line)
            name = entry.get("event", "")
            if "CommunityGoal" not in name:
                continue
            events[name] += 1
            if len(samples[name]) < 2:
                samples[name].append(json.dumps(entry)[:700])
            for goal in entry.get("CurrentGoals", []) or []:
                titles[goal.get("Title", "?")] += 1
            if name == "CommunityGoalReward":
                reward_blobs.append(json.dumps(entry))

print("--- community goal events in the corpus ---")
if not events:
    print("none at all")
for name, n in events.most_common():
    print(f"{n:6d}  {name}")
    for sample in samples[name]:
        print(f"        {sample[:400]}")

print()
print(f"--- distinct goals seen: {len(titles)} ---")
for title, n in titles.most_common(20):
    print(f"{n:6d}  {title}")

print()
print("--- does any reward name a module? ---")
for blob in reward_blobs[:10]:
    print(f"    {blob[:300]}")
