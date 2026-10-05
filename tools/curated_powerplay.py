"""Hand-kept maps the Powerplay rank generator reads.

`gen-powerplay.py` fails, naming the cell, on a module name or perk header that is not a key
here. Keys are normalised: lower case, whitespace collapsed, hyphens read as spaces.
"""

# The Powers as the journal spells them, with the tab each one is on.
POWERS = {
    "Zemina Torval": 540247093,
    "Felicia Winters": 781786024,
    "Li Yong-Rui": 1395805772,
    "Archon Delaine": 764722830,
    "Nakato Kaine": 297898880,
    "Jerome Archer": 1291717331,
    "Denton Patreus": 1682885056,
    "Pranav Antal": 286905514,
    "Yuri Grom": 1437755647,
    "A. Lavigny-Duval": 868561511,
    "Edmund Mahon": 1878511070,
    "Aisling Duval": 1807225310,
}

# Module name on the sheet to the entitlement symbol in EliteSpecifications.tsv. "Plasma
# Accelerator" and "Imperial Rail Gun" are the sheet's names for the Powerplay modules, not the
# generic ones.
MODULES = {
    "imperial hammer rail gun": "ELITE_SPECIFIC_V_POWER_200010",
    "imperial rail gun": "ELITE_SPECIFIC_V_POWER_200010",
    "prismatic shield generator": "ELITE_SPECIFIC_V_POWER_200020",
    "prismatic shield generators": "ELITE_SPECIFIC_V_POWER_200020",
    "cytoscrambler burst laser": "ELITE_SPECIFIC_V_POWER_200030",
    "advanced plasma accelerator": "ELITE_SPECIFIC_V_POWER_200050",
    "plasma accelerator": "ELITE_SPECIFIC_V_POWER_200050",
    "retributor beam laser": "ELITE_SPECIFIC_V_POWER_200060",
    "retributuor beam laser": "ELITE_SPECIFIC_V_POWER_200060",
    "pulse disruptor laser": "ELITE_SPECIFIC_V_POWER_200070",
    "pack hound missile rack": "ELITE_SPECIFIC_V_POWER_200080",
    "enforcer cannon": "ELITE_SPECIFIC_V_POWER_200090",
    "rocket propelled containment missile": "ELITE_SPECIFIC_V_POWER_200100",
    "rocket propelled fsd disruptor": "ELITE_SPECIFIC_V_POWER_200100",
    "mining lance beam laser": "ELITE_SPECIFIC_V_POWER_200120",
    "miing lance beam laser": "ELITE_SPECIFIC_V_POWER_200120",
    "concord cannon": "ELITE_SPECIFIC_V_POWER_200130",
    "pacifier frag cannon": "ELITE_SPECIFIC_V_POWER_200140",
    "pacfier frag cannon": "ELITE_SPECIFIC_V_POWER_200140",
    "pacifer frag cannon": "ELITE_SPECIFIC_V_POWER_200140",
}

# Perk column header on the sheet to perk key. Different headers may share a key.
PERKS = {
    "trade profits on sales": "trade_sales",
    "trade bond of +x% of trade profit on sales": "trade_sales",
    "trade bond on sales in power territory": "trade_sales",
    "trade profits on rare goods sales": "rare_goods_sales",
    "minor faction reputation gain": "faction_reputation",
    "increase in minor faction reputation gain": "faction_reputation",
    "increase in minor faction reputation gain in power territory": "faction_reputation",
    "bounty payouts": "bounty_payouts",
    "bounty payout": "bounty_payouts",
    "weapon module cost": "weapon_module_cost",
    "rearm prices": "rearm_prices",
    "rearm refuel repair prices": "rearm_refuel_repair_prices",
    "exploration data sales": "exploration_data_sales",
    "organics data sales": "organics_data_sales",
    "technology commodity profits": "technology_profits",
    "mining commodity profits": "mining_profits",
    "mining commodity profits in power territory": "mining_profits",
    "imperial slaves commodity profits in power territory": "imperial_slaves_profits",
    "increase on salvage profits in power territory": "salvage_profits",
    "increase on food and medicne profits in power territory": "food_medicine_profits",
    "search and rescue payout": "search_rescue_payouts",
    "search and rescue payouts": "search_rescue_payouts",
    "black market profits on sales": "black_market_profits",
    "on bounties placed on you": "bounties_on_you",
}
