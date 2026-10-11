"""Hand-kept maps the mission table generator reads.

`gen-missions.py` exits, naming the mission name and the word, on a family or a name-ending
word that is not mapped here. Keys are lower case; a family is matched as a case-insensitive
prefix of the name and the longest match wins.
"""

import re

COMBAT = "Combat"
TRANSPORTATION = "Transportation"
FREELANCE = "Freelance"
OPERATIONS = "Operations"
SUPPORT = "Support"
PASSENGER_LOUNGE = "Passenger Lounge"

# Family (as the journal spells it) to group, spoken words and the station state the family
# itself encodes. On-foot families are placed by what they ask; NEWP lists none of them.
FAMILIES = {
    "Mission_Altruism": (SUPPORT, "donation of goods", ""),
    "Mission_AltruismCredits": (SUPPORT, "donation of credits", ""),
    "Mission_Assassinate": (COMBAT, "assassination", ""),
    "Mission_Collect": (TRANSPORTATION, "commodity collection", ""),
    "Mission_Courier": (TRANSPORTATION, "courier", ""),
    "Mission_Delivery": (TRANSPORTATION, "delivery", ""),
    "Mission_Disable": (OPERATIONS, "disable", ""),
    "Mission_DisableMegaship": (OPERATIONS, "megaship turret disable", ""),
    "Mission_DS_Collect": (TRANSPORTATION, "commodity collection", "DS"),
    "Mission_DS_PassengerBulk": (PASSENGER_LOUNGE, "group passengers", "DS"),
    "Mission_Hack": (OPERATIONS, "data hack", ""),
    "Mission_HackMegaship": (OPERATIONS, "megaship data link", ""),
    "Mission_LongDistanceExpedition": (PASSENGER_LOUNGE, "long distance expedition", ""),
    "Mission_Massacre": (COMBAT, "pirate massacre", ""),
    "Mission_Massacre_Skimmer": (COMBAT, "skimmer massacre", ""),
    "Mission_MassacreThargoid": (COMBAT, "Thargoid massacre", ""),
    "Mission_Mining": (TRANSPORTATION, "mining", ""),
    "Mission_OnFoot_Assassination": (COMBAT, "on foot assassination", ""),
    "Mission_OnFoot_AssassinationIllegal": (COMBAT, "on foot illegal assassination", ""),
    "Mission_OnFoot_Collect": (TRANSPORTATION, "on foot collection", ""),
    "Mission_OnFoot_Defence": (OPERATIONS, "on foot defence", ""),
    "Mission_OnFoot_Delivery": (TRANSPORTATION, "on foot delivery", ""),
    "Mission_OnFoot_Hack": (OPERATIONS, "on foot data hack", ""),
    "Mission_OnFoot_Heist": (FREELANCE, "on foot heist", ""),
    "Mission_OnFoot_Massacre": (COMBAT, "on foot massacre", ""),
    "Mission_OnFoot_MassacreIllegal": (COMBAT, "on foot illegal massacre", ""),
    "Mission_OnFoot_Onslaught": (COMBAT, "on foot onslaught", ""),
    "Mission_OnFoot_OnslaughtIllegal": (COMBAT, "on foot illegal onslaught", ""),
    "Mission_OnFoot_ProductionHeist": (FREELANCE, "on foot production heist", ""),
    "Mission_OnFoot_Reboot": (OPERATIONS, "on foot reboot", ""),
    "Mission_OnFoot_RebootRestore": (OPERATIONS, "on foot reboot and restore", ""),
    "Mission_OnFoot_Sabotage": (FREELANCE, "on foot sabotage", ""),
    "Mission_OnFoot_Salvage": (TRANSPORTATION, "on foot salvage", ""),
    "Mission_OnFoot_SalvageIllegal": (TRANSPORTATION, "on foot illegal salvage", ""),
    "Mission_OnFoot_Smuggle": (FREELANCE, "on foot smuggling", ""),
    "Mission_PassengerBulk": (PASSENGER_LOUNGE, "group passengers", ""),
    "Mission_PassengerVIP": (PASSENGER_LOUNGE, "VIP passenger", ""),
    "Mission_Piracy": (FREELANCE, "piracy", ""),
    "Mission_Rescue": (PASSENGER_LOUNGE, "rescue", ""),
    "Mission_RS_Massacre": (COMBAT, "pirate massacre", "RS"),
    "Mission_RS_PassengerBulk": (PASSENGER_LOUNGE, "group passengers", "RS"),
    "Mission_Salvage": (TRANSPORTATION, "salvage", ""),
    "Mission_Scan": (OPERATIONS, "planetary scan", ""),
    "Mission_Sightseeing": (PASSENGER_LOUNGE, "sightseeing", ""),
    "Mission_Smuggle": (FREELANCE, "smuggling", ""),
}

# Names left out of the table because the mission board does not list them.
NOT_ON_BOARD = [
    "Chain_",
    "Mission_GenericPermit",
    "Mission_StartZone_",
    "Mission_TheDead",
    "Mission_TW_",
    "Mission_Welcome_",
]

# Name-ending word to (kind, value). A kind is state, government, economy, rank, engineer or
# variant; a variant encodes no circumstance and its value is the word spoken.
WORDS = {
    "agricultural": ("economy", "Agriculture"),
    "agriculture": ("economy", "Agriculture"),
    "anarchy": ("government", "Anarchy"),
    "arriving": ("variant", "arriving"),
    "blops": ("variant", "black ops"),
    "boom": ("state", "Boom"),
    "bs": ("variant", "bs"),
    "bust": ("state", "Bust"),
    "civilliberty": ("state", "Civil Liberty"),
    "civilunrest": ("state", "Civil Unrest"),
    "civilwar": ("state", "Civil War"),
    "communism": ("government", "Communism"),
    "confederacy": ("government", "Confederacy"),
    "conflict": ("variant", "conflict"),
    "contact": ("variant", "contact"),
    "cooperative": ("government", "Cooperative"),
    "corporate": ("government", "Corporate"),
    "corporation": ("government", "Corporate"),
    "covert": ("variant", "covert"),
    "democracy": ("government", "Democracy"),
    "download": ("variant", "download"),
    "election": ("state", "Election"),
    "elections": ("state", "Election"),
    "engineer": ("engineer", "invitation"),
    "expansion": ("state", "Expansion"),
    "extraction": ("economy", "Extraction"),
    "famine": ("state", "Famine"),
    "feudal": ("government", "Feudal"),
    "founder": ("variant", "founder"),
    "hard": ("variant", "hard"),
    "hightech": ("economy", "High Tech"),
    "illegal": ("variant", "illegal"),
    "industrial": ("economy", "Industrial"),
    "investment": ("state", "Investment"),
    "leaving": ("variant", "leaving"),
    "legal": ("variant", "legal"),
    "liquidcanister": ("variant", "liquid canister"),
    "lockdown": ("state", "Lockdown"),
    "macguffin": ("variant", "macguffin"),
    "mb": ("variant", "mission board"),
    "megaship": ("variant", "megaship"),
    "military": ("economy", "Military"),
    "ncd": ("variant", "ncd"),
    "nr": ("variant", "nr"),
    "offline": ("variant", "offline"),
    "outbreak": ("state", "Outbreak"),
    "palin": ("variant", "palin"),
    "penal": ("government", "Prison Colony"),
    "planet": ("variant", "planetary"),
    "planetary": ("variant", "planetary"),
    "poi": ("variant", "point of interest"),
    "power": ("variant", "power"),
    "prisoncol": ("government", "Prison Colony"),
    "prisoncolony": ("government", "Prison Colony"),
    "production": ("variant", "production"),
    "rankemp": ("rank", "Empire"),
    "rankfed": ("rank", "Federation"),
    "refinery": ("economy", "Refinery"),
    "retreat": ("state", "Retreat"),
    "service": ("economy", "Service"),
    "terraforming": ("economy", "Terraforming"),
    "theocracy": ("government", "Theocracy"),
    "upload": ("variant", "upload"),
    "war": ("state", "War"),
    "wing": ("variant", "wing"),
}

# Passenger and sightseeing names carry who is aboard.
for _who in (
    "aidworker business medical politician polprisoner prisonerofwar protestor rebel refugee "
    "scientist security celebrity ceo criminal doctor explorer general headofstate terrorist "
    "tourist whistleblower"
).split():
    WORDS[_who] = ("variant", _who)

# Numbered variants of an on-foot scenario ("001", "013").
NUMBER = re.compile(r"^\d{3}$")
