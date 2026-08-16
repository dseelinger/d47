"""Where does the current system's star class come from, and how often is it actually there?

`list.md` Phase 18's *Read a system name* says the star class rides along, because a variant's
colour follows the star and the variant is what sets the price. Two candidate sources, and the
obvious one is not the one that works:

    FSDTarget   fires before the jump and names the target system and its StarClass
    Scan        the arrival auto-scan of the main star, carrying StarType

Reads the journals and prints. Writes nothing. Run where the journals are:

    ssh cooler 'python -' < spike/ExobiologyProbe/scan_starclass.py
"""
import json, os, glob, collections

home = os.path.expanduser("~")
base = os.path.join(home, "Saved Games", "Frontier Developments", "Elite Dangerous")
files = sorted(glob.glob(os.path.join(base, "Journal.*.log")))
print("journals:", len(files))

jumps = 0
target_only = 0
scan_only = 0
both = 0
neither = 0
gaps = []
star_types = collections.Counter()


def close(open_jump):
    """Score the arrival that is ending."""
    global jumps, target_only, scan_only, both, neither
    _, had_target, _, found = open_jump
    jumps += 1
    if had_target and found:
        both += 1
    elif had_target:
        target_only += 1
    elif found:
        scan_only += 1
    else:
        neither += 1


for path in files:
    targeted = {}
    open_jump = None

    for line in open(path, encoding="utf-8", errors="replace"):
        line = line.strip()
        if not line:
            continue
        try:
            event = json.loads(line)
        except Exception:
            continue

        kind = event.get("event")

        if kind == "FSDTarget" and event.get("Name"):
            targeted[event["Name"]] = event.get("StarClass")

        elif kind == "FSDJump":
            if open_jump:
                close(open_jump)
            system = event.get("StarSystem")
            open_jump = (system, system in targeted, 0, False)
            continue

        elif kind == "Scan" and open_jump and event.get("StarType") and event.get("BodyID") == 0:
            system, had_target, count, found = open_jump
            star_types[event["StarType"]] += 1
            if not found:
                gaps.append(count)
            open_jump = (system, had_target, count, True)

        if open_jump:
            system, had_target, count, found = open_jump
            open_jump = (system, had_target, count + 1, found)

print()
print("arrivals measured:", jumps)
print("  FSDTarget named it, and a main-star Scan followed:", both)
print("  FSDTarget only:", target_only)
print("  main-star Scan only:", scan_only)
print("  neither:", neither)

if jumps:
    print()
    print(f"FSDTarget covers    {(both + target_only) / jumps:6.1%} of arrivals")
    print(f"main-star Scan covers {(both + scan_only) / jumps:6.1%} of arrivals")
    print("-> the source that looks obvious is the one that is usually not there.")

if gaps:
    gaps.sort()
    print()
    print("events between the jump and the main-star scan: median",
          gaps[len(gaps) // 2], " p95", gaps[int(len(gaps) * 0.95)])

print()
print("distinct main-star classes seen:", len(star_types), star_types.most_common(12))
