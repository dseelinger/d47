"""Where a pre-engineered module could come from, and what one looks like.

Three passes over the same read: any payload that mentions a Merc-Coin-shaped currency;
every event that carries an Engineering block at a moment other than a craft; and every
distinct EngineerID seen anywhere, so an id that is not a person stands out.
"""
import collections, glob, json, os

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")

CURRENCY_WORDS = ("merc", "coin", "frontline", "operation", "prize", "reward token")

currency_hits = collections.Counter()
currency_samples = {}
engineering_by_event = collections.Counter()
engineer_ids = collections.Counter()
id_to_names = collections.defaultdict(collections.Counter)
sample_by_id = {}

def walk_engineering(event_name, obj):
    """Engineering blocks appear on a module, or on each item of a Modules list."""
    found = []
    if isinstance(obj, dict):
        if "Engineering" in obj and isinstance(obj["Engineering"], dict):
            found.append(obj["Engineering"])
        for value in obj.values():
            found.extend(walk_engineering(event_name, value))
    elif isinstance(obj, list):
        for value in obj:
            found.extend(walk_engineering(event_name, value))
    return found

for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            raw = line
            entry = json.loads(line)
            name = entry.get("event", "?")

            lowered = raw.lower()
            for word in CURRENCY_WORDS:
                if word in lowered:
                    currency_hits[(name, word)] += 1
                    currency_samples.setdefault((name, word), raw)

            for block in walk_engineering(name, entry):
                engineering_by_event[name] += 1
                engineer_id = block.get("EngineerID")
                engineer_ids[engineer_id] += 1
                id_to_names[engineer_id][block.get("Engineer", "(absent)")] += 1
                sample_by_id.setdefault(engineer_id, (name, block))

print("--- payloads mentioning a Merc-Coin-shaped currency ---")
if not currency_hits:
    print("none, in any of 692,631 events")
for (name, word), n in sorted(currency_hits.items()):
    print(f"{n:6d}  {name} (matched {word!r})")
    print(f"        {currency_samples[(name, word)][:220]}")

print()
print("--- events carrying an Engineering block ---")
for name, n in engineering_by_event.most_common():
    print(f"{n:8d}  {name}")

print()
print("--- every EngineerID seen, and the names it goes by ---")
for engineer_id, n in sorted(engineer_ids.items(), key=lambda kv: -kv[1]):
    names = ", ".join(f"{k} x{v}" for k, v in id_to_names[engineer_id].most_common())
    print(f"{n:8d}  {engineer_id}  {names}")
