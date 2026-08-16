"""Does the mass code predict what pays, and what is the first-footfall multiplier?

The two questions in the spike that no external source can answer, plus the structural one
that decides whether the sampling item is reachable at all.

  Q3  Does the procedural mass code — the lone letter before the digits in "Swoilz AW-C d5-2"
      — correlate with what exobiology actually pays? "More mass, more Stratum" is folklore,
      and shipping folklore as a heuristic sends a Commander the wrong way with confidence.
  Q4  What is the first-footfall multiplier? `SellOrganicData` carries `Bonus` beside `Value`
      per row, so it is measured here rather than recited.
  Q6  What position does `ScanOrganic` actually carry? The sample spacing figure rests on it.

Reconnaissance only. Run where the journals are.
"""
import collections, glob, json, os, re, statistics

JOURNALS = os.path.join(
    os.environ["USERPROFILE"], "Saved Games", "Frontier Developments", "Elite Dangerous", "Journal.*.log")

# "Swoilz AW-C d5-2", "Hyades Sector ZU-Y d68 3 b", "Col 285 Sector SE-Q d5-63".
# The mass code is the single letter immediately before the first run of digits in the
# "<AA-A> <c><n>" tail. Anything without that tail is a named system and has no mass code.
PROCEDURAL = re.compile(r"\b[A-Z]{2}-[A-Z]\s+([a-h])\d", re.IGNORECASE)

scans = []          # (timestamp, SystemAddress, genus, species, scantype, keys)
sells = []          # BioData rows
addr_to_name = {}   # SystemAddress -> StarSystem
scan_keys = collections.Counter()
scan_types = collections.Counter()

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

            if kind in ("FSDJump", "Location", "CarrierJump", "SupercruiseExit", "Scan", "SAASignalsFound", "FSSBodySignals"):
                a, n = e.get("SystemAddress"), e.get("StarSystem")
                if a and n:
                    addr_to_name[a] = n
                # Body names carry the system name as a prefix, which fills gaps the
                # arrival events miss.
                if a and not addr_to_name.get(a) and e.get("BodyName"):
                    addr_to_name[a] = e["BodyName"]

            elif kind == "ScanOrganic":
                scan_keys.update(e.keys())
                scan_types[e.get("ScanType")] += 1
                scans.append(e)

            elif kind == "SellOrganicData":
                sells.extend(e.get("BioData", []))

print(f"{len(scans)} ScanOrganic, {len(sells)} sold BioData rows, {len(addr_to_name)} systems named")
print()

# ---- Q6: what does ScanOrganic actually carry? --------------------------------------
print("--- ScanOrganic fields, and how many events carry each ---")
for k, n in scan_keys.most_common():
    print(f"  {k:24s} {n:5d}{'' if n == len(scans) else '   <- not always'}")
print(f"  ScanType values: {dict(scan_types)}")
positional = [k for k in scan_keys if k.lower() in ("latitude", "longitude", "lat", "long", "altitude")]
print(f"  positional fields present: {positional or 'NONE'}")
print()

# ---- Q4: the first-footfall multiplier ----------------------------------------------
print("--- first-footfall bonus, measured from Bonus beside Value ---")
ratios = collections.Counter()
zero = 0
for row in sells:
    value, bonus = row.get("Value"), row.get("Bonus")
    if not value:
        continue
    if not bonus:
        zero += 1
        continue
    ratios[round(bonus / value, 4)] += 1

print(f"  {len(sells)} rows: {zero} with no bonus, {sum(ratios.values())} with one")
for ratio, n in ratios.most_common(8):
    print(f"    bonus / value = {ratio:g}  in {n} rows")
if ratios:
    only = list(ratios)
    print(f"  distinct ratios: {sorted(only)}")
    print(f"  => a first footfall pays value x {sorted(only)[0]:g} on top, "
          f"i.e. {1 + sorted(only)[0]:g}x total" if len(only) == 1 else "  => NOT a single constant")
print()

# ---- species values, from the corpus's own sales ------------------------------------
#
# `Value` is NOT a unit price. Radicoida Unica sold at the same station as the same variant
# for 119,037 / 476,148 / 7,618,368 / 14,284,440 / 33,330,360 — every one an exact multiple
# of the smallest, and there is no count field on the row. So a row is the *total* for however
# many specimens of that species went in the transaction, and the unit price can only be
# recovered as the GCD of what was observed. That is exact when some sale was of one specimen
# and an over-estimate otherwise, so it is labelled as a floor rather than as the price.
values = {}
for row in sells:
    if row.get("Species_Localised") and row.get("Value"):
        values.setdefault(row["Species_Localised"], []).append(row["Value"])

print(f"--- {len(values)} species sold; is every Value a multiple of that species' GCD? ---")
units, breaks = {}, 0
for name, vs in sorted(values.items()):
    unit = vs[0]
    for v in vs[1:]:
        while v:
            unit, v = v, unit % v
    units[name] = unit
    multiples = [v // unit for v in vs]
    if any(v % unit for v in vs):
        breaks += 1
        print(f"  BREAKS: {name}: {sorted(vs)}")
    elif len(set(multiples)) > 1:
        print(f"  {name:28s} unit {unit:>9,}  x {sorted(multiples)}")
print(f"  {breaks} species break the multiple rule "
      f"({'the row-total reading holds' if breaks == 0 else 'the reading is wrong'})")
print()

# ---- Q3: mass code against what was scanned -----------------------------------------
print("--- mass code vs genus, over every scan whose system could be named ---")
by_code = collections.defaultdict(collections.Counter)
unnamed = named = nonprocedural = 0
for e in scans:
    name = addr_to_name.get(e.get("SystemAddress"))
    if not name:
        unnamed += 1
        continue
    m = PROCEDURAL.search(name)
    if not m:
        nonprocedural += 1
        continue
    named += 1
    by_code[m.group(1).lower()][e.get("Genus_Localised") or "?"] += 1

print(f"  {named} scans in procedural systems, {nonprocedural} in named systems, {unnamed} unresolved")
print()
# Per-genus unit price, from the GCD-derived per-species units rather than from raw row
# totals. Using the totals here was the first draft's mistake and it inflated whichever mass
# code happened to contain a bulk sale — which is precisely the folklore this question exists
# to test, arrived at by accident.
genus_value = {}
for row in sells:
    g, s_name = row.get("Genus_Localised"), row.get("Species_Localised")
    if g and s_name in units:
        genus_value.setdefault(g, []).append(units[s_name])

header = f"  {'code':5s} {'scans':>6s} {'genera':>7s}  {'mean value/scan':>16s}  top genera"
print(header)
for code in sorted(by_code):
    counter = by_code[code]
    total = sum(counter.values())
    priced = [(n, statistics.mean(genus_value[g])) for g, n in counter.items() if g in genus_value]
    mean = sum(n * v for n, v in priced) / sum(n for n, _ in priced) if priced else float("nan")
    top = ", ".join(f"{g} {n}" for g, n in counter.most_common(3))
    print(f"  {code:5s} {total:6d} {len(counter):7d}  {mean:16,.0f}  {top}")

print()
print("  Genus prices used above come from this corpus's own sales, so a genus that was")
print("  scanned but never sold contributes nothing to the mean rather than a guessed price.")
print(f"  priced genera: {sorted(genus_value)}")


# ---- what one organism's scan sequence looks like -----------------------------------
print()
print("--- ScanType sequence per organism, on (SystemAddress, Body, Species) ---")
seq = collections.defaultdict(list)
for e in scans:
    seq[(e.get("SystemAddress"), e.get("Body"), e.get("Species"))].append((e["timestamp"], e.get("ScanType")))
shapes = collections.Counter()
gaps = []
for key, rows in seq.items():
    rows.sort()
    shapes[tuple(t for _, t in rows)] += 1
for shape, n in shapes.most_common(6):
    print(f"  {n:4d}  {' -> '.join(shape)}")
print(f"  {len(seq)} organisms scanned, {len(shapes)} distinct sequences")
