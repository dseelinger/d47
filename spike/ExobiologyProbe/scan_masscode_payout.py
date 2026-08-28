"""Does a system's mass code predict its exobiology, measured with a denominator under it?

Phase 18, *Spike: does the mass code predict the biology, with a denominator*.

Phase 16's spike asked this of one Commander's own scan history and could not answer it — the fatal
reason being that a flight log is a record of where somebody chose to fly, so a Commander who
already believes the folklore produces data that agrees with it. This probe fixes that by drawing
the sample from a property of the *bodies* rather than from anybody's route.

**The design, and the one thing that makes it valid.**

  numerator    landable bodies that carry a high-value landmark
  denominator  ALL landable bodies in the same volume

P(mass code | good biology) is not P(good biology | mass code). Counting only the good ones would
"discover" that the common mass codes are the best ones, because they are common. So both come out
of one query stream: every landable body in a ball, with its landmarks attached, so numerator and
denominator cannot disagree about volume.

**Volume is controlled by distance shells around several references**, because the galaxy is not
uniform — density, and the mix of mass codes with it, varies by region. Each reference is measured
separately as well as pooled, so a result that only holds near Sol is visible as one that only holds
near Sol.

**The spansh contract, established live 2026-08-16** (the way this folder establishes every
contract — by sending things and reading what comes home):

    POST api/bodies/search   {filters, sort, size, page, reference_system}

  - `count` is REAL below 10,000 and CAPPED at exactly 10,000 above it. A nonsense request also
    answers 10,000, so the number is not evidence of anything until it is under the cap. Every shell
    here is sized until it is.
  - `size` maxes at 500. **`size: 1000` silently returns 25** rather than erroring, which would
    quietly shrink a sample by 95% while looking like it worked.
  - Deep paging fails rather than returning empty, so a shell must fit inside its own count.
  - Default ordering is NOT by distance, and must not be used: Elite's `id64` packs the boxel
    coordinates and the mass code into its bits, so paging in natural order samples non-randomly on
    the exact variable under test. Everything here sorts by distance explicitly.
  - A body with no landmarks carries `landmarks: null` or `landmark_value: 0`. Both are handled.

**Landmarks are not all biology.** `landmark_subtype` mixes organics with geology and points of
interest — "Aleoida Arcus" beside "Ammonia Ice Fumarole" and "Abandoned Base". Rather than
hand-writing a list of organic genera, which would be invented game data, this reports by spansh's
own `value` and prints the genus distribution so the classification stays visible.

Polite by construction: one request a second, and a hard cap on pages per reference.

    python spike/ExobiologyProbe/scan_masscode_payout.py
"""
import json, re, sys, time, urllib.request, collections

API = "https://spansh.co.uk/api/bodies/search"

# Spread across the galaxy on purpose. A result that holds in the bubble and nowhere else is a
# result about the bubble, and pooling would hide that.
REFERENCES = [
    ("Sol", 30),
    ("Colonia", 60),
    ("Sagittarius A*", 40),
    ("Beagle Point", 120),
    ("Merope", 40),
    ("Diaguandri", 40),
]

PAGE = 500
MAX_PAGES = 20          # 10,000 bodies per reference, which is also the count cap
COUNT_CAP = 10_000
TARGET = 9_000          # shrink a shell until its count is comfortably under the cap
PAUSE = 1.0

# The mass code lives in the name. Hand-named systems have none and drop out by not matching, which
# is the same grammar d47 ships in SystemName.cs.
PROC = re.compile(r"^(?P<sector>.+?) [A-Z][A-Z]-[A-Z] (?P<mass>[a-h])(?:\d+-)?\d+$")

# What counts as worth flying for. Reported at several thresholds rather than one, because a single
# cut-off chosen after seeing the data is a way of choosing the answer.
THRESHOLDS = [1, 1_000_000, 5_000_000, 10_000_000]


def post(body):
    request = urllib.request.Request(
        API,
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json", "User-Agent": "d47-spike/1.0"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.load(response)


def query(reference, radius, size, page):
    return post({
        "filters": {
            "is_landable": {"value": True},
            "distance": {"min": "0", "max": str(radius)},
        },
        "sort": [{"distance": {"direction": "asc"}}],
        "size": size,
        "page": page,
        "reference_system": reference,
    })


def shell_for(reference, radius):
    """Shrink until the count is real rather than capped, so the shell can be paged in full."""
    while radius > 1:
        try:
            count = query(reference, radius, 1, 0).get("count", 0)
        except Exception as ex:
            print(f"  {reference} at {radius} ly: {ex}", file=sys.stderr)
            return None, 0
        time.sleep(PAUSE)
        if count == 0:
            return None, 0
        if count < TARGET:
            return radius, count
        print(f"  {reference}: {radius} ly gives "
              f"{'capped at ' if count >= COUNT_CAP else ''}{count}, halving")
        radius = radius // 2
    return None, 0


def collect(reference, radius, count):
    bodies = []
    for page in range(min(MAX_PAGES, (count + PAGE - 1) // PAGE)):
        try:
            batch = query(reference, radius, PAGE, page).get("results", [])
        except Exception as ex:
            print(f"  {reference} page {page}: {ex}", file=sys.stderr)
            break
        if not batch:
            break
        bodies.extend(batch)
        time.sleep(PAUSE)
    return bodies


def mass_of(system_name):
    match = PROC.match(system_name or "")
    return match.group("mass") if match else None


def tally(bodies, per_mass, per_system, genus_types, subtypes):
    for body in bodies:
        mass = mass_of(body.get("system_name"))
        if mass is None:
            per_mass["hand-named"]["bodies"] += 1
            continue

        value = body.get("landmark_value") or 0
        landmarks = body.get("landmarks") or []

        bucket = per_mass[mass]
        bucket["bodies"] += 1
        bucket["value"] += value
        for threshold in THRESHOLDS:
            if value >= threshold:
                bucket[f"ge{threshold}"] += 1
        if value > bucket["best"]:
            bucket["best"] = value

        for landmark in landmarks:
            genus_types[landmark.get("type")] += 1
            if (landmark.get("value") or 0) >= 1_000_000:
                subtypes[mass][landmark.get("subtype")] += 1

        system = per_system[(mass, body.get("system_name"))]
        system["bodies"] += 1
        system["value"] += value


def report(title, per_mass, per_system):
    print()
    print(f"=== {title} ===")
    print(f"{'mass':>5} {'bodies':>8} {'with bio':>9} {'P(bio)':>8} "
          f"{'P(>=1M)':>8} {'P(>=5M)':>8} {'mean cr/body':>13} {'best':>12}")
    for mass in "abcdefgh":
        bucket = per_mass.get(mass)
        if not bucket or bucket["bodies"] == 0:
            continue
        n = bucket["bodies"]
        print(f"{mass:>5} {n:>8} {bucket['ge1']:>9} {bucket['ge1'] / n:>7.1%} "
              f"{bucket['ge1000000'] / n:>7.2%} {bucket['ge5000000'] / n:>7.2%} "
              f"{bucket['value'] / n:>13,.0f} {bucket['best']:>12,}")

    if per_mass.get("hand-named", {}).get("bodies"):
        print(f"{'named':>5} {per_mass['hand-named']['bodies']:>8}   "
              f"(no mass code — excluded from the comparison)")

    systems = collections.defaultdict(lambda: {"n": 0, "bio": 0})
    for (mass, _), entry in per_system.items():
        systems[mass]["n"] += 1
        if entry["value"] > 0:
            systems[mass]["bio"] += 1
    if systems:
        print()
        print("  per system (a shell is complete, so every landable body of every system is in it):")
        for mass in "abcdefgh":
            entry = systems.get(mass)
            if entry and entry["n"]:
                print(f"    {mass}: {entry['n']:>6} systems, "
                      f"{entry['bio'] / entry['n']:>6.1%} hold at least one landmark")


def bucket():
    keys = {"bodies": 0, "value": 0, "best": 0}
    keys.update({f"ge{t}": 0 for t in THRESHOLDS})
    return keys


def main():
    pooled = collections.defaultdict(bucket)
    pooled_systems = collections.defaultdict(lambda: {"bodies": 0, "value": 0})
    genus_types = collections.Counter()
    subtypes = collections.defaultdict(collections.Counter)

    for reference, radius in REFERENCES:
        print(f"\n--- {reference} ---")
        shell, count = shell_for(reference, radius)
        if shell is None:
            print(f"  no usable shell; skipped")
            continue
        print(f"  shell {shell} ly, {count} landable bodies")

        bodies = collect(reference, shell, count)
        print(f"  collected {len(bodies)}")

        local = collections.defaultdict(bucket)
        local_systems = collections.defaultdict(lambda: {"bodies": 0, "value": 0})
        tally(bodies, local, local_systems, genus_types, subtypes)
        tally(bodies, pooled, pooled_systems, collections.Counter(), collections.defaultdict(collections.Counter))
        report(f"{reference} — {shell} ly", local, local_systems)

    report("POOLED across every reference", pooled, pooled_systems)

    print()
    print("=== what the landmarks actually are (organics are not the only kind) ===")
    print(genus_types.most_common(25))

    print()
    print("=== top >=1M genera per mass code ===")
    for mass in "abcdefgh":
        if subtypes[mass]:
            print(f"  {mass}: {subtypes[mass].most_common(5)}")


if __name__ == "__main__":
    main()
