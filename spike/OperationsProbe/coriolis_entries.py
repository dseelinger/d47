import json, urllib.request

url = "https://raw.githubusercontent.com/EDCD/coriolis-data/master/modifications/blueprints.json"
request = urllib.request.Request(url, headers={"User-Agent": "d47-operations-spike"})
with urllib.request.urlopen(request, timeout=60) as response:
    data = json.loads(response.read().decode("utf-8-sig"))

for key, value in data.items():
    blob = json.dumps(value)
    if "IncreasedCapacity" in key or "PrioritySystems" in key or "IncreasedCapacity" in blob or "PrioritySystems" in blob:
        print(f"=== {key} ===")
        print(json.dumps(value, indent=2)[:1600])
        print()
