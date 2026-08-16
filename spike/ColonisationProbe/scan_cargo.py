"""What can the journal say about what is in the hold, and what is on the carrier?

Answers the two questions Phase 18's tracking item rests on and that the first pass of this spike
did not ask:

  1. Does the `Cargo` event carry a usable manifest, or does that have to come from `Cargo.json`?
  2. Can a carrier's per-commodity stock be derived from `CargoTransfer`, and would d47 know when
     it had lost track?

The second is the one that matters. It reconciles a derived stock against `CarrierStats`'
`SpaceUsage.Cargo`, which is the game's own figure, so the failure is measured rather than argued.

Reads the journals and prints. Writes nothing. Run where the journals are:

    ssh cooler 'python -' < spike/ColonisationProbe/scan_cargo.py
"""
import json, os, glob, collections

home = os.path.expanduser("~")
base = os.path.join(home, "Saved Games", "Frontier Developments", "Elite Dangerous")
files = sorted(glob.glob(os.path.join(base, "Journal.*.log")))
print("journals:", len(files))

# --- 1. the Cargo event, and whether the manifest is ever in it -------------
first_has_inventory = collections.Counter()
later_has_inventory = collections.Counter()
count_vs_inventory = collections.Counter()
cargo_vessels = collections.Counter()

# --- 2. carrier stock, derived and reconciled ------------------------------
stock = collections.defaultdict(collections.Counter)
checks = collections.Counter()
negatives = collections.Counter()
drift_examples = []

transfer_directions = collections.Counter()
contributions = 0

for path in files:
    seen_first_cargo = False
    # Transfers name no carrier, so attribute to the only one this file has seen.
    carrier = None

    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                event = json.loads(line)
            except Exception:
                continue

            kind = event.get("event")

            if kind == "Cargo":
                inventory = event.get("Inventory")
                cargo_vessels[event.get("Vessel")] += 1

                if seen_first_cargo:
                    later_has_inventory[inventory is not None] += 1
                else:
                    first_has_inventory[inventory is not None] += 1
                    seen_first_cargo = True

                if inventory is not None:
                    total = sum(row.get("Count", 0) for row in inventory)
                    count_vs_inventory[
                        "Count == sum(Inventory)" if total == event.get("Count") else "MISMATCH"] += 1

            elif kind in ("CarrierStats", "CarrierBuy", "CarrierLocation", "CarrierJump"):
                carrier = event.get("CarrierID") or carrier

            elif kind == "ColonisationContribution":
                contributions += 1

            if kind == "CargoTransfer" and carrier:
                for row in event.get("Transfers", []):
                    commodity, count = row.get("Type"), row.get("Count", 0)
                    direction = row.get("Direction")
                    transfer_directions[direction] += 1

                    if direction == "tocarrier":
                        stock[carrier][commodity] += count
                    elif direction == "toship":
                        stock[carrier][commodity] -= count
                        if stock[carrier][commodity] < 0:
                            negatives[commodity] += 1

            if kind == "CarrierStats" and event.get("CarrierID"):
                reported = (event.get("SpaceUsage") or {}).get("Cargo")
                if reported is None:
                    continue

                derived = sum(stock[event["CarrierID"]].values())

                if derived == reported:
                    checks["exact"] += 1
                else:
                    checks["drift"] += 1
                    if len(drift_examples) < 10:
                        drift_examples.append(
                            (event["timestamp"], derived, reported, derived - reported))

print()
print("== 1. Does the Cargo event carry a manifest? ==")
print("first Cargo of a file, has Inventory:", dict(first_has_inventory))
print("later Cargo events, has Inventory:  ", dict(later_has_inventory))
print("vessels:", dict(cargo_vessels))
print("when present, Count vs sum(Inventory):", dict(count_vs_inventory))
print("-> a journal-only reader has a manifest for the first minute and a stale one after it.")

print()
print("== 2. Can carrier stock be derived from CargoTransfer? ==")
print("transfer directions:", dict(transfer_directions))
print("reconciled against SpaceUsage.Cargo:", dict(checks))
print("commodities driven negative:", sum(negatives.values()), "across", len(negatives))
print("worst:", negatives.most_common(8))
print("drift (timestamp, derived, reported, delta):")
for row in drift_examples:
    print("   ", row)
print("-> a negative stock is a transfer out of cargo the journal never saw arrive, which is the")
print("   proof that CargoTransfer is not the whole story rather than merely an imprecise one.")

print()
print("ColonisationContribution events:", contributions)
