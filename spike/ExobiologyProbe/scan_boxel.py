"""Derive the boxel ladder from the game's own coordinates, rather than reciting it.

Phase 18's *Read a system name* requires the a-to-h mass ladder to come from a source with
its provenance recorded, never from memory. This is that source, and it is the game itself.

A procedural name encodes a boxel index in its three letters plus the boxel number. If a mass code's
boxels are S ly on a side then a sector — 1,280 ly — holds N = 1280/S of them per axis, and the
index decomposes as i = idx % N, j = (idx // N) % N, k = idx // N**2.

So for each candidate N, decode i/j/k and regress them against the real `StarPos`, with each
sector's own mean removed so that no sector grid origin has to be assumed. The correct N fits; every
other N scrambles the mapping and fits nothing. **The slope of the fit is the box size**, and it is
recovered independently of the candidate, which is what makes this a measurement rather than a
curve-fit to an assumption.

Also prints the grammar survey the parser's tests cite: how many names are procedural, how many are
hand-named, and — the number that matters — how many look procedural and fail to parse.

Reads the journals and prints. Writes nothing. Run where the journals are:

    ssh cooler 'python -' < spike/ExobiologyProbe/scan_boxel.py
"""
import json, os, glob, collections, re

home = os.path.expanduser("~")
base = os.path.join(home, "Saved Games", "Frontier Developments", "Elite Dangerous")
files = sorted(glob.glob(os.path.join(base, "Journal.*.log")))
print("journals:", len(files))

PROC = re.compile(
    r"^(?P<sector>.+?) (?P<l1>[A-Z])(?P<l2>[A-Z])-(?P<l3>[A-Z]) "
    r"(?P<mass>[a-h])(?:(?P<n1>\d+)-)?(?P<n2>\d+)$")

# Anything with the shape of a boxel designator in it. A name matching this and not PROC is a hole
# in the grammar, and is the one count worth watching.
LOOSE = re.compile(r"[A-Z][A-Z]-[A-Z] [a-h]")

systems = {}
for path in files:
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                event = json.loads(line)
            except Exception:
                continue
            position = event.get("StarPos")
            if event.get("StarSystem") and isinstance(position, list) and len(position) == 3:
                systems[event["StarSystem"]] = tuple(position)

parsed = collections.defaultdict(list)
handnamed = []
for name, (x, y, z) in systems.items():
    match = PROC.match(name)
    if not match:
        handnamed.append(name)
        continue
    idx = ((ord(match.group("l1")) - 65)
           + (ord(match.group("l2")) - 65) * 26
           + (ord(match.group("l3")) - 65) * 26 * 26
           + int(match.group("n1") or 0) * 26 * 26 * 26)
    parsed[match.group("mass")].append((match.group("sector"), idx, x, y, z))

print()
print("== the grammar survey the parser's tests cite ==")
print("distinct systems with coordinates:", len(systems))
print("procedurally named:", sum(len(v) for v in parsed.values()))
print("hand named:", len(handnamed))
print("hand named yet containing a boxel-shaped designator (must be 0):",
      sum(1 for name in handnamed if LOOSE.search(name)))


def fit(pairs):
    n = len(pairs)
    if n < 8:
        return None
    mx = sum(p[0] for p in pairs) / n
    my = sum(p[1] for p in pairs) / n
    sxx = sum((p[0] - mx) ** 2 for p in pairs)
    sxy = sum((p[0] - mx) * (p[1] - my) for p in pairs)
    syy = sum((p[1] - my) ** 2 for p in pairs)
    if sxx == 0 or syy == 0:
        return None
    return sxy / sxx, (sxy * sxy) / (sxx * syy), n


def evaluate(rows, N):
    by_sector = collections.defaultdict(list)
    for sector, idx, x, y, z in rows:
        by_sector[sector].append((idx % N, (idx // N) % N, idx // (N * N), x, y, z))

    centred = collections.defaultdict(list)
    for entries in by_sector.values():
        if len(entries) < 2:
            continue
        means = [sum(e[c] for e in entries) / len(entries) for c in range(6)]
        for e in entries:
            for comp in range(3):
                for axis in range(3):
                    centred[(comp, axis)].append(
                        (e[comp] - means[comp], e[3 + axis] - means[3 + axis]))

    best = {}
    for comp in range(3):
        scored = [(f[1], axis, f[0], f[2])
                  for axis in range(3)
                  if (f := fit(centred[(comp, axis)]))]
        if scored:
            best[comp] = max(scored)
    return best


print()
print("== the ladder, derived ==")
print("The i->x slope is the box size. It is stable across candidate N, which is the sign that it")
print("is being measured rather than assumed.")
print()

for mass in "abcdefgh":
    rows = parsed[mass]
    if len(rows) < 20:
        print(f"mass code {mass}: {len(rows)} systems — too few to derive anything.")
        continue

    results = []
    for N in (128, 64, 32, 16, 8, 4, 2, 1):
        best = evaluate(rows, N)
        if len(best) == 3:
            results.append((sum(b[0] for b in best.values()) / 3, N, best))

    results.sort(reverse=True)
    print(f"mass code {mass} — {len(rows)} systems")
    for mean_r2, N, best in results[:2]:
        detail = "  ".join(
            f"{'ijk'[c]}->{'xyz'[best[c][1]]} r2={best[c][0]:.3f} slope={best[c][2]:7.2f}"
            for c in range(3))
        print(f"   N={N:4} mean r2={mean_r2:.3f}   {detail}")
