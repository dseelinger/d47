"""What the galaxy index can and cannot be asked about a colonisation candidate.

Answers, against the live service: which system filters are honoured and which are
silently dropped, what the two colonisation flags mean, how a candidate's body shape
is reached, and where a plausible spelling costs 46 seconds. Prints; writes nothing.
"""
import json
import time
import urllib.request
from collections import Counter

HOST = "https://spansh.co.uk/"
UA = {"Content-Type": "application/json", "User-Agent": "d47-spike/0.1 (+https://github.com/dseelinger/d47)"}


def post(path, body, timeout=120):
    request = urllib.request.Request(HOST + path, data=json.dumps(body).encode(), headers=UA)
    started = time.time()
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            raw = response.read()
            return json.loads(raw), len(raw), time.time() - started
    except Exception as error:
        return {"ERROR": str(error), "count": None, "results": []}, 0, time.time() - started


def systems(filters, reference="Sol", size=1, sort=None, distance=15):
    body = {
        "filters": dict({"distance": {"min": "0", "max": str(distance)}}, **filters),
        "sort": sort or [{"distance": {"direction": "asc"}}],
        "size": size,
        "page": 0,
        "reference_system": reference,
    }
    return post("api/systems/search", body)


def bodies(filters, reference="Sol", size=5, distance=15):
    body = {
        "filters": dict({"distance": {"min": "0", "max": str(distance)}}, **filters),
        "sort": [{"distance": {"direction": "asc"}}],
        "size": size,
        "page": 0,
        "reference_system": reference,
    }
    return post("api/bodies/search", body)


def rule(title):
    print()
    print("=" * 78)
    print(title)
    print("=" * 78)


rule("1. Ground truth: every system within 15 ly of Sol, counted here rather than asked for")
truth, size_bytes, seconds = systems({}, size=500)
rows = truth["results"]
print("   {} systems, {:,} bytes, {:.1f}s".format(truth["count"], size_bytes, seconds))
print("   population 0: {}".format(sum(1 for s in rows if not s.get("population"))))
print("   populated:    {}".format(sum(1 for s in rows if s.get("population"))))
print("   is_colonised:       {}".format(Counter(str(s.get("is_colonised")) for s in rows)))
print("   is_being_colonised: {}".format(Counter(str(s.get("is_being_colonised")) for s in rows)))

rule("2. Which system filters are honoured. A bogus key is the control: it returns the unfiltered count")
for label, filters in [
    ("(unfiltered)", {}),
    ("bogus key", {"zzznotafilter": {"value": ["true"]}}),
    ("population 0-0", {"population": {"min": "0", "max": "0"}}),
    ("population 1-1e12", {"population": {"min": "1", "max": "1000000000000"}}),
    ("population 1-1e12 numeric", {"population": {"min": 1, "max": 1000000000000}}),
    ("population 100000-1e12", {"population": {"min": "100000", "max": "1000000000000"}}),
    ("population as a choice, 0", {"population": {"value": ["0"]}}),
    ("population as a choice, 19160", {"population": {"value": ["19160"]}}),
    ("is_colonised true", {"is_colonised": {"value": ["true"]}}),
    ("is_colonised false", {"is_colonised": {"value": ["false"]}}),
    ("is_colonised banana", {"is_colonised": {"value": ["banana"]}}),
    ("is_being_colonised false", {"is_being_colonised": {"value": ["false"]}}),
    ("body_count 0-0", {"body_count": {"min": "0", "max": "0"}}),
]:
    result, _, _ = systems(filters)
    print("   {:30s} -> {:>6s}  {}".format(label, str(result.get("count")), result.get("ERROR", "")))

rule("3. What cannot be filtered can be sorted. population ascending, then distance")
result, _, _ = systems({}, size=6, sort=[{"population": {"direction": "asc"}}, {"distance": {"direction": "asc"}}])
for system in result["results"]:
    print("   {:24s} population {:>12s}  {:5.1f} ly".format(
        system["name"], str(system.get("population")), system.get("distance", 0)))

rule("4. The page cap, and what asking for more than it does")
for size in [100, 500, 501, 600]:
    result, size_bytes, seconds = systems({}, size=size)
    print("   size={:<4d} -> {:3d} results, {:9,d} bytes, {:.1f}s".format(
        size, len(result["results"]), size_bytes, seconds))

rule("5. Reaching one candidate's bodies. system_name on the body search")
for label, filters in [
    ("system_name Sol", {"system_name": {"value": ["Sol"]}}),
    ("system_name sol (lowercase)", {"system_name": {"value": ["sol"]}}),
    ("system_name three at once", {"system_name": {"value": ["Sol", "Alpha Centauri", "Barnard" + chr(39) + "s Star"]}}),
    ("system_name as a group", {"system_name": {"name": {"value": ["Sol"]}}}),
    ("system (the plausible key)", {"system": {"value": ["Sol"]}}),
]:
    result, size_bytes, seconds = bodies(filters, size=500)
    names = Counter(b.get("system_name") for b in result.get("results", []))
    print("   {:30s} -> count {:>6s}  {}  {:.1f}s {}".format(
        label, str(result.get("count")), dict(names), seconds, result.get("ERROR", "")))

rule("6. The 46-second spelling: a zero-width distance range is not 'this system only'")
result, size_bytes, seconds = bodies({"distance": {"min": "0", "max": "0"}}, size=3)
print("   distance 0-0 -> count {}, {:.1f}s, systems {}".format(
    result.get("count"), seconds, set(b.get("system_name") for b in result.get("results", []))))

rule("7. What the systems search's embedded bodies carry, and what only the body search has")
embedded = set()
for system in rows:
    for body in system.get("bodies", []):
        embedded |= set(body)
detail, _, _ = bodies({"system_name": {"value": ["Sol"]}}, size=100)
full = set()
for body in detail["results"]:
    full |= set(body)
print("   embedded body keys ({}): {}".format(len(embedded), sorted(embedded)))
print("   only on the body search: {}".format(sorted(full - embedded)))

rule("8. Do the flags and the population agree, across five regions?")
sample = []
for reference in ["Sol", "Deciat", "HIP 22460", "Synuefe EN-H d11-96", "Colonia"]:
    result, size_bytes, seconds = systems(
        {}, reference=reference, size=60,
        sort=[{"population": {"direction": "asc"}}, {"distance": {"direction": "asc"}}])
    print("   {:22s} {:4d} within 15 ly, pulled {:3d}, {:9,d} bytes, {:.1f}s".format(
        reference, result["count"], len(result["results"]), size_bytes, seconds))
    sample += result["results"]
unpopulated = [s for s in sample if not s.get("population")]
print("   sampled {} systems, {} with population 0".format(len(sample), len(unpopulated)))
print("     is_colonised:       {}".format(Counter(str(s.get("is_colonised")) for s in unpopulated)))
print("     is_being_colonised: {}".format(Counter(str(s.get("is_being_colonised")) for s in unpopulated)))
print("     with a 'stations' entry: {}".format(sum(1 for s in unpopulated if s.get("stations"))))
print("     station types there: {}".format(
    Counter(t.get("type") for s in unpopulated for t in s.get("stations", []))))
print("     with no bodies at all: {}".format(sum(1 for s in unpopulated if not s.get("body_count"))))
populated = [s for s in sample if s.get("population")]
print("   {} populated, of which is_colonised true: {}".format(
    len(populated), sum(1 for s in populated if s.get("is_colonised"))))

rule("9. The body vocabulary the embedded records actually use")
print("   type:               {}".format(Counter(b.get("type") for s in sample for b in s.get("bodies", []))))
print("   terraforming_state: {}".format(
    Counter(b.get("terraforming_state") for s in sample for b in s.get("bodies", []))))
subtypes = Counter(b.get("subtype") for s in sample for b in s.get("bodies", []))
print("   subtypes ({}): {}".format(len(subtypes), json.dumps(dict(subtypes.most_common()))))
