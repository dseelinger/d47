#!/usr/bin/env python3
"""Print the Commander's game state for sizing a sample chapter.

    python .claude/skills/new-story/game-state.py [journal folder]

Reads the journals newest first and takes the last LoadGame, Loadout, Rank, Statistics and
StoredShips. The thresholds and the comfort-zone pick are the ones in #711. Writes nothing.
"""
import glob
import json
import os
import pathlib
import sys

FOLDER = pathlib.Path(os.environ.get("USERPROFILE", "~")).expanduser() / "Saved Games" / "Frontier Developments" / "Elite Dangerous"
WANTED = ("LoadGame", "Loadout", "Rank", "Statistics", "StoredShips")

AX_PREFIXES = ("hpt_atmulticannon", "hpt_atdumbfiremissile", "hpt_atventdisruptorpylon", "hpt_guardian_", "hpt_flakmortar")
AX_SCOUTS, AX_INTERCEPTORS, LONG_HAUL = 25_000_000, 150_000_000, 250_000_000

LADDERS = {
    "Combat": ["Harmless", "Mostly Harmless", "Novice", "Competent", "Expert", "Master", "Dangerous", "Deadly"],
    "Trade": ["Penniless", "Mostly Penniless", "Peddler", "Dealer", "Merchant", "Broker", "Entrepreneur", "Tycoon"],
    "Explore": ["Aimless", "Mostly Aimless", "Scout", "Surveyor", "Trailblazer", "Pathfinder", "Ranger", "Pioneer"],
    "Soldier": ["Defenceless", "Mostly Defenceless", "Rookie", "Soldier", "Gunslinger", "Warrior", "Gladiator", "Deadeye"],
    "Exobiologist": ["Directionless", "Mostly Directionless", "Compiler", "Collector", "Cataloguer", "Taxonomist", "Ecologist", "Geneticist"],
}
ELITE = ["Elite", "Elite I", "Elite II", "Elite III", "Elite IV", "Elite V"]

COMFORT = [
    ("bounty", "Combat", "Bounties_Claimed"),
    ("bond", "Combat", "Combat_Bonds"),
    ("mine", "Mining", "Quantity_Mined"),
    ("organic", "Exobiology", "Organic_Data"),
    ("rescue", "Search_And_Rescue", "SearchRescue_Count"),
    ("mission Mission_Passenger", "Passengers", "Passengers_Missions_Delivered"),
]


def latest(folder: pathlib.Path) -> dict:
    found = {}
    for path in sorted(glob.glob(str(folder / "Journal.*.log")), key=os.path.getmtime, reverse=True):
        for line in reversed(pathlib.Path(path).read_text(encoding="utf-8", errors="replace").splitlines()):
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                continue
            name = event.get("event")
            if name in WANTED and name not in found:
                found[name] = event
        if len(found) == len(WANTED):
            break
    return found


def rank_name(career: str, rank: int) -> str:
    if rank >= 8:
        return ELITE[min(rank - 8, len(ELITE) - 1)]
    return LADDERS[career][rank]


def main() -> None:
    folder = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else FOLDER
    state = latest(folder)
    missing = [name for name in WANTED if name not in state]
    if missing:
        print(f"Not found in {folder}: {', '.join(missing)}")

    credits = state.get("LoadGame", {}).get("Credits")
    loadout = state.get("Loadout", {})
    items = [module.get("Item", "").lower() for module in loadout.get("Modules", [])]
    ax_fitted = any(item.startswith(AX_PREFIXES) for item in items)
    stored = state.get("StoredShips", {})
    hulls = {ship.get("ShipType", "").lower() for ship in stored.get("ShipsHere", []) + stored.get("ShipsRemote", [])}
    caspian = loadout.get("Ship", "").lower() == "explorer_nx" or "explorer_nx" in hulls

    print(f"Credits at last load: {credits:,}" if credits is not None else "Credits at last load: unknown")
    if loadout:
        print(f"Ship: {loadout.get('Ship')} \"{loadout.get('ShipName', '')}\", jump range {loadout.get('MaxJumpRange', 0):.1f} ly, "
              f"cargo {loadout.get('CargoCapacity', 0)} t")
    print(f"AX or Guardian weapon fitted: {'yes' if ax_fitted else 'no'}")
    print(f"Caspian Explorer owned: {'yes' if caspian else 'no'}")

    rank = state.get("Rank", {})
    if rank:
        print("Ranks: " + ", ".join(f"{career} {rank_name(career, rank[career])}" for career in LADDERS if career in rank))

    credits = credits or 0
    scouts = credits >= AX_SCOUTS or ax_fitted
    print("Thresholds reached:")
    print(f"  AX scouts (25,000,000 or AX weapon fitted): {'yes' if scouts else 'no'}")
    print(f"  AX interceptors (150,000,000): {'yes' if credits >= AX_INTERCEPTORS else 'no'}")
    print(f"  Long haul (250,000,000 or Caspian owned): {'yes' if credits >= LONG_HAUL or caspian else 'no'}")

    statistics = state.get("Statistics")
    if not statistics:
        print("Comfort zone: no Statistics event, so no activity is picked")
        return
    rows = [(kind, statistics.get(section, {}).get(key, 0)) for kind, section, key in COMFORT]
    if scouts:
        figures = [v for v in statistics.get("TG_ENCOUNTERS", {}).values() if isinstance(v, (int, float))]
        rows.append(("bond thargoid: true", max(figures, default=0)))
    print("Comfort-zone figures:")
    for kind, figure in rows:
        print(f"  {kind}: {figure:,}")
    pick = min(rows, key=lambda row: row[1])
    print(f"Comfort zone pick: {pick[0]}")


if __name__ == "__main__":
    main()
