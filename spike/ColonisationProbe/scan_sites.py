"""Concurrency, the other three colonisation events, and whether the manifest ever moves.

The rest of the spike's journal questions:

  - Can more than one construction site be active at once? The tracking item's whole shape
    depends on it: one active site is a field, several is a collection with a selection.
  - What do `ColonisationSystemClaim`, `ColonisationContribution` and
    `ColonisationBeaconDeployed` carry, and is a claim's 24-hour clock in the journal?
  - Is `ProvidedAmount` cumulative, and is `RequiredAmount` fixed for the life of a site?
    A snapshot is only usable if the numbers in it mean what they appear to.
  - Does the manifest size vary by facility, or is 19 rows universal?

Reconnaissance only. Run where the journals are.
"""
import collections, glob, json, os

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")

KINDS = ("ColonisationConstructionDepot", "ColonisationContribution",
         "ColonisationSystemClaim", "ColonisationBeaconDeployed")

seen = collections.defaultdict(list)
for path in sorted(glob.glob(JOURNALS)):
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line or "Colonisation" not in line:
                continue
            try:
                e = json.loads(line)
            except Exception:
                continue
            if e.get("event") in KINDS:
                seen[e["event"]].append(e)

for kind in KINDS[1:]:
    rows = seen.get(kind, [])
    print(f"--- {kind}: {len(rows)} events ---")
    keys = collections.Counter()
    for e in rows:
        keys.update(e.keys())
    for k, n in keys.most_common():
        print(f"    {k:30s} {n}")
    if rows:
        sample = dict(rows[0])
        for big in ("Contributions", "ResourcesRequired"):
            if big in sample and isinstance(sample[big], list):
                sample[big] = sample[big][:2] + ["…"]
        print("    sample: " + json.dumps(sample)[:520])
    print()

depots = seen.get("ColonisationConstructionDepot", [])

print("--- manifest size per site, and whether the requirement ever moves ---")
by_site = collections.defaultdict(list)
for e in depots:
    by_site[e["MarketID"]].append(e)

moved = 0
for market, seq in sorted(by_site.items()):
    seq.sort(key=lambda e: e["timestamp"])
    sizes = {len(e["ResourcesRequired"]) for e in seq}
    first = {r["Name"]: r["RequiredAmount"] for r in seq[0]["ResourcesRequired"]}
    last = {r["Name"]: r["RequiredAmount"] for r in seq[-1]["ResourcesRequired"]}
    changed = [n for n in first if n in last and first[n] != last[n]]
    moved += len(changed)
    done = seq[-1]["ConstructionComplete"]
    failed = any(e["ConstructionFailed"] for e in seq)
    print(f"  {market}  {len(seq):5d} events  rows={sorted(sizes)}  "
          f"progress {seq[0]['ConstructionProgress']:.3f}->{seq[-1]['ConstructionProgress']:.3f}  "
          f"complete={done}  everFailed={failed}  RequiredAmount changed for {len(changed)} commodities")

print(f"\n  RequiredAmount moved mid-build in {moved} commodity-site pairs")

print()
print("--- is ProvidedAmount cumulative (never decreasing within a site)? ---")
regressions = 0
pairs = 0
for market, seq in by_site.items():
    seq.sort(key=lambda e: e["timestamp"])
    prev = {}
    for e in seq:
        for r in e["ResourcesRequired"]:
            n = r["Name"]
            if n in prev:
                pairs += 1
                if r["ProvidedAmount"] < prev[n]:
                    regressions += 1
            prev[n] = r["ProvidedAmount"]
print(f"  {pairs} consecutive comparisons, {regressions} decreases "
      f"({'cumulative' if regressions == 0 else 'NOT cumulative'})")

print()
print("--- can two sites be active at the same time? ---")
# Active = between a site's first event and its last event with ConstructionComplete false.
spans = {}
for market, seq in by_site.items():
    seq.sort(key=lambda e: e["timestamp"])
    open_events = [e for e in seq if not e["ConstructionComplete"]]
    if open_events:
        spans[market] = (open_events[0]["timestamp"], open_events[-1]["timestamp"])

overlaps = []
items = sorted(spans.items(), key=lambda kv: kv[1][0])
for i, (a, (a0, a1)) in enumerate(items):
    for b, (b0, b1) in items[i + 1:]:
        if b0 <= a1 and a0 <= b1:
            overlaps.append((a, b, max(a0, b0), min(a1, b1)))

print(f"  {len(spans)} sites with an incomplete window; {len(overlaps)} overlapping pairs")
for a, b, s, e in overlaps[:8]:
    print(f"    {a} and {b} both open {s} .. {e}")

busiest = collections.Counter()
for market, (s, e) in spans.items():
    for other, (s2, e2) in spans.items():
        if other != market and s2 <= e and s <= e2:
            busiest[market] += 1
if busiest:
    print(f"  most simultaneous: one site overlapped {max(busiest.values())} others")

print()
print("--- does a completed site keep reporting? ---")
for market, seq in sorted(by_site.items(), key=lambda kv: -len(kv[1]))[:5]:
    seq.sort(key=lambda e: e["timestamp"])
    done = [i for i, e in enumerate(seq) if e["ConstructionComplete"]]
    if done:
        print(f"  {market}: first complete at event {done[0]+1}/{len(seq)}, "
              f"{len(seq)-done[0]-1} further events after it")
    else:
        print(f"  {market}: never completed in this corpus ({len(seq)} events)")
