"""What `ColonisationConstructionDepot` carries, and whether it is a snapshot or a delta.

Three of the spike's questions, answered by counting rather than by reading one event and
assuming the rest look like it:

  1. What is on the event, and what is on each `ResourcesRequired` entry.
  2. Is `ResourcesRequired` a **snapshot** of the whole manifest every time, or a **delta**
     carrying only what changed? This is the trap that has now caught `EngineerProgress`
     and `StoredModules`, and it is silent both times: a delta read as a snapshot produces
     a shortfall the Commander does not have, and it looks exactly like a correct answer.
  3. Does it fire only while docked at the site?

Reconnaissance only. Run where the journals are.
"""
import collections, glob, json, os

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")

EVENT = "ColonisationConstructionDepot"

top_keys = collections.Counter()
row_keys = collections.Counter()
events = []          # (path, timestamp, payload)
docked_spans = []    # (path, timestamp, event, MarketID)

for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line or '"event"' not in line:
                continue
            try:
                e = json.loads(line)
            except Exception:
                continue
            kind = e.get("event")
            if kind == EVENT:
                top_keys.update(e.keys())
                for row in e.get("ResourcesRequired", []):
                    row_keys.update(row.keys())
                events.append((os.path.basename(path), e.get("timestamp"), e))
            elif kind in ("Docked", "Undocked", "Location"):
                docked_spans.append((os.path.basename(path), e.get("timestamp"), kind, e.get("MarketID")))

print(f"{len(events)} {EVENT} events")
print()

print("--- top-level fields, and how many events carry each ---")
for k, n in top_keys.most_common():
    print(f"  {k:34s} {n:6d}  {'always' if n == len(events) else 'sometimes'}")

print()
print("--- ResourcesRequired entry fields ---")
total_rows = sum(row_keys.values()) // max(1, len(row_keys))
for k, n in row_keys.most_common():
    print(f"  {k:34s} {n:6d}")

print()
print("--- one event, trimmed to two resource rows ---")
if events:
    sample = dict(events[len(events) // 2][2])
    if "ResourcesRequired" in sample:
        sample["ResourcesRequired"] = sample["ResourcesRequired"][:2] + ["…"]
    print(json.dumps(sample, indent=2)[:1600])

# ---- snapshot or delta -------------------------------------------------------------
#
# The test is the row count. A snapshot repeats the whole manifest every time, so the row
# count is stable and equal to the number of distinct commodities the site ever asked for.
# A delta carries only what moved, so consecutive events differ in length and the union
# across a site is larger than any single event.
print()
print("--- snapshot or delta, per construction site ---")
by_site = collections.defaultdict(list)
for _, ts, e in events:
    by_site[e.get("MarketID")].append((ts, e))

print(f"{len(by_site)} distinct MarketIDs")
verdict = collections.Counter()
for market, seq in sorted(by_site.items(), key=lambda kv: -len(kv[1]))[:6]:
    seq.sort(key=lambda p: p[0] or "")
    names = [{r.get("Name") for r in e.get("ResourcesRequired", [])} for _, e in seq]
    sizes = [len(s) for s in names]
    union = set().union(*names) if names else set()
    stable = len(set(sizes)) == 1
    covers = all(s == union for s in names) if names else False
    verdict["snapshot" if covers else "delta-or-shrinking"] += 1
    print(f"  MarketID {market}: {len(seq):4d} events, row counts {min(sizes)}..{max(sizes)}, "
          f"union {len(union)}  -> {'SNAPSHOT (every event carries the full manifest)' if covers else 'not a constant full manifest'}"
          + ("" if stable else "  [row count varies]"))

# Does a commodity leave the list once satisfied? That is the other way a snapshot can
# shrink without being a delta, and it changes what "what is left" means.
print()
print("--- does a satisfied commodity drop out of the list? ---")
for market, seq in sorted(by_site.items(), key=lambda kv: -len(kv[1]))[:3]:
    seq.sort(key=lambda p: p[0] or "")
    first = {r.get("Name"): r for r in seq[0][1].get("ResourcesRequired", [])}
    last = {r.get("Name"): r for r in seq[-1][1].get("ResourcesRequired", [])}
    gone = set(first) - set(last)
    print(f"  MarketID {market}: first {len(first)} rows, last {len(last)} rows, dropped {len(gone)}")
    for name in list(gone)[:3]:
        r = first[name]
        print(f"      dropped {name}: required={r.get('RequiredAmount')} provided={r.get('ProvidedAmount')}")

# ---- is the event tied to being docked? --------------------------------------------
print()
print("--- is the event only written while docked at the site? ---")
docked_spans.sort(key=lambda t: (t[0], t[1] or ""))
per_file = collections.defaultdict(list)
for f, ts, kind, market in docked_spans:
    per_file[f].append((ts, kind, market))

matched = undocked = elsewhere = 0
for f, ts, e in events:
    prior = [x for x in per_file.get(f, []) if (x[0] or "") <= (ts or "")]
    if not prior:
        undocked += 1
        continue
    last_ts, last_kind, last_market = prior[-1]
    if last_kind == "Undocked":
        undocked += 1
    elif last_market == e.get("MarketID"):
        matched += 1
    else:
        elsewhere += 1

print(f"  docked at this very MarketID : {matched}")
print(f"  docked somewhere else        : {elsewhere}")
print(f"  not docked at all            : {undocked}")
