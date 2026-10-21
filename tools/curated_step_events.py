"""Hand-kept lists the step-event table generator reads.

`gen-step-events.py` exits, naming the field, when a whitelisted string field has no values
from any of its sources.
"""

import re

# Events the game writes and nobody performs.
SNAPSHOT_EVENTS = {
    "Fileheader", "LoadGame", "Commander", "Materials", "Cargo", "Statistics", "Market",
    "Outfitting", "Shipyard", "StoredModules", "StoredShips", "Missions", "Music", "Progress",
    "Rank", "Reputation", "NavRoute", "Friends", "Powerplay", "Shutdown", "Loadout",
    "SquadronStartup", "CarrierStats", "ShipLocker", "Backpack", "ModuleInfo", "Location",
}

# Events whose prerequisite (owning a fleet carrier, being in a squadron) has no ActivityGate.
NOT_DRAWABLE = re.compile(r"^(Carrier|Squadron)|Squadron")

# String fields whose values are names Frontier defines: types, classes, categories, symbols.
# Never a commander, carrier, ship, squadron, station, settlement, system or faction name, and
# never free text. "Event.Field".
WHITELIST = [
    "Docked.StationType",
    "Scan.PlanetClass",
    "Scan.StarType",
    "ScanOrganic.Genus",
    "ScanOrganic.Species",
    "ScanOrganic.ScanType",
    "StartJump.StarClass",
    "StartJump.JumpType",
    "MaterialCollected.Category",
    "MaterialCollected.Name",
    "CommitCrime.CrimeType",
    "USSDrop.USSType",
    "LaunchSRV.SRVType",
    "EngineerCraft.BlueprintName",
    "Synthesis.Name",
    "CodexEntry.Category",
    "CodexEntry.SubCategory",
    "MarketSell.Type",
    "MarketBuy.Type",
    "MiningRefined.Type",
    "CollectItems.Type",
    "CollectItems.Name",
]

# A committed Knowledge table column that already names a whitelisted field's values:
# "Event.Field" -> (file, column, lower-case the value).
KNOWLEDGE_COLUMNS = {
    "MaterialCollected.Name": ("Materials.tsv", "symbol", False),
}

# Gates a drawn step needs: ActivityGate names, comma separated.
NEEDS = {
    "LaunchSRV": "BuggyBay",
    "DockSRV": "BuggyBay",
    "SRVDestroyed": "BuggyBay",
    "ScanOrganic": "Odyssey",
    "SellOrganicData": "Odyssey",
    "CodexEntry": "SurfaceScanner",
    "MiningRefined": "MiningKit",
    "ProspectedAsteroid": "MiningKit",
    "AsteroidCracked": "MiningKit",
    "LaunchDrone": "MiningKit",
    "CollectCargo": "Cargo",
    "MarketSell": "Cargo",
    "MarketBuy": "Cargo",
    "EjectCargo": "Cargo",
    "CargoDepot": "Cargo",
    "BuyMicroResources": "Odyssey",
    "SellMicroResources": "Odyssey",
    "CollectItems": "Odyssey",
    "Disembark": "Odyssey",
    "Embark": "Odyssey",
    "SuitLoadout": "Odyssey",
    "UseConsumable": "Odyssey",
    "ShipTargeted": "Danger",
    "Interdicted": "Danger",
    "UnderAttack": "Danger",
    "Bounty": "Danger",
    "FactionKillBond": "Danger",
    "ShieldState": "Danger",
    "HullDamage": "Danger",
    "ShotsFired": "SoldierDanger",
    "Died": "Danger",
}

# Career an event belongs to; empty when it belongs to none.
CAREERS = {
    "Bounty": "Combat",
    "FactionKillBond": "Combat",
    "CapShipBond": "Combat",
    "Interdicted": "Combat",
    "MarketSell": "Trading",
    "MarketBuy": "Trading",
    "CollectCargo": "Trading",
    "MiningRefined": "Mining",
    "ProspectedAsteroid": "Mining",
    "AsteroidCracked": "Mining",
    "Scan": "Exploration",
    "FSSDiscoveryScan": "Exploration",
    "FSSAllBodiesFound": "Exploration",
    "SAAScanComplete": "Exploration",
    "MultiSellExplorationData": "Exploration",
    "SellExplorationData": "Exploration",
    "CodexEntry": "Exploration",
    "ScanOrganic": "Exobiology",
    "SellOrganicData": "Exobiology",
    "SearchAndRescue": "SearchAndRescue",
    "RescueShip": "SearchAndRescue",
    "CommitCrime": "Crime",
}

# Tool names a story may draw as a step.
DRAWABLE_TOOLS = {"add_log_entry", "bookmark_destination"}

# Checklist groups a story may draw as a step. None yet.
DRAWABLE_GROUPS: set[str] = set()

TOOL_RUN_DESCRIPTION = "A tool of the assistant ran for the Commander."
CHECKLIST_DESCRIPTION = "A line was added to a checklist."
