using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Web;
using Xunit;

namespace D47.Core.Tests.Web;

/// <summary>
/// The snapshot document the web app reads, built from the web fixture, is pinned: renaming or removing a
/// field fails here.
/// </summary>
public sealed class TheSnapshotDocumentKeepsItsShapeTests
{
    /// <summary>
    /// Three engineers and two goals, because the rest of each list comes from
    /// shipped tables that change when the game data is regenerated.
    /// </summary>
    private static readonly string[] Pinned = ["Felicity Farseer", "The Dweller", "Juri Ishmaak", "Elite V in Combat", "Federal Navy"];

    private static WebSnapshot Built()
    {
        var bench = new SnapshotBench();

        bench.Note(SnapshotBench.Fid, "Ada Quillon", "Buy limpets", "Fit a fuel scoop");
        bench.Watch();
        bench.Plot(SnapshotBench.Fid, "Colonia");

        return bench.Build();
    }

    [Fact]
    public void TheDocumentBuiltFromTheFixtureIsTheOnePinned()
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(Built(), WebSnapshot.Json))!.AsObject();
        var engineers = json["engineers"]!.AsArray();

        Assert.Contains(engineers, engineer => (string?)engineer!["name"] == "Marco Qwent");

        json["engineers"] = Only(engineers);
        json["checklist"]!["goals"] = Only(json["checklist"]!["goals"]!.AsArray());

        var actual = json.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        Assert.Equal(Expected.ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));
    }

    private static JsonArray Only(JsonArray entries) => new(
    [
        .. entries
            .Where(entry => Pinned.Contains((string?)entry!["name"]))
            .Select(entry => entry!.DeepClone()),
    ]);

    [Fact]
    public void EveryFieldIsTheOnePinned()
    {
        var paths = new List<string>();

        Walk(typeof(WebSnapshot), string.Empty, paths);
        paths.Sort(StringComparer.Ordinal);

        Assert.Equal(Fields.ReplaceLineEndings("\n").Split('\n'), paths);
    }

    /// <summary>Each property's JSON path, with <c>[]</c> after a list, into every document type it holds.</summary>
    private static void Walk(Type type, string prefix, List<string> paths)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var path = prefix + WebSnapshot.Json.PropertyNamingPolicy!.ConvertName(property.Name);
            var held = property.PropertyType;

            if (held.IsGenericType && held.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                path += "[]";
                held = held.GetGenericArguments()[0];
            }

            paths.Add(path);

            if (held.Namespace == typeof(WebSnapshot).Namespace)
            {
                Walk(held, path + ".", paths);
            }
        }
    }

    [Fact]
    public void TheDocumentReadsBackEqual()
    {
        var built = Built();
        var json = JsonSerializer.Serialize(built, WebSnapshot.Json);
        var read = JsonSerializer.Deserialize<WebSnapshot>(json, WebSnapshot.Json);

        Assert.NotNull(read);
        Assert.Equivalent(built, read, strict: true);
        Assert.Equal(json, JsonSerializer.Serialize(read, WebSnapshot.Json));
    }

    [Fact]
    public void TheFormatStartsAtOne()
    {
        Assert.Equal(1, WebSnapshot.CurrentFormat);
        Assert.Equal(1, Built().Format);
    }

    private const string Fields = """
        carrier
        carrier.balance
        carrier.callSign
        carrier.capacity
        carrier.cargoTonnes
        carrier.departureTime
        carrier.destinationSystem
        carrier.dockingAccess
        carrier.freeSpace
        carrier.fuelLevel
        carrier.jumpRange
        carrier.maxJumpRange
        carrier.name
        carrier.services[]
        carrier.services[].activated
        carrier.services[].enabled
        carrier.services[].name
        carrier.services[].role
        carrier.starSystem
        carrier.tritiumInHold
        carrier.weeklyUpkeep
        checklist
        checklist.goals[]
        checklist.goals[].fraction
        checklist.goals[].have
        checklist.goals[].isDone
        checklist.goals[].name
        checklist.goals[].need
        checklist.goals[].nextRank
        checklist.goals[].rung
        checklist.lists[]
        checklist.lists[].done
        checklist.lists[].home
        checklist.lists[].isHere
        checklist.lists[].items[]
        checklist.lists[].items[].state
        checklist.lists[].items[].text
        checklist.lists[].name
        checklist.lists[].next
        checklist.lists[].open
        engineers[]
        engineers[].jumps
        engineers[].lightYears
        engineers[].name
        engineers[].prerequisites[]
        engineers[].prerequisites[].percent
        engineers[].prerequisites[].state
        engineers[].prerequisites[].text
        engineers[].reach
        engineers[].referredBy[]
        engineers[].specialities[]
        engineers[].specialities[].grade
        engineers[].specialities[].kind
        engineers[].standing
        engineers[].standing.progress
        engineers[].standing.rank
        engineers[].standing.rankProgress
        engineers[].station
        engineers[].system
        engineers[].unlockCost
        format
        frontierId
        hudMatrix[]
        materials[]
        materials[].category
        materials[].count
        materials[].displayName
        materials[].grade
        missions[]
        missions[].cargo
        missions[].cargo.collected
        missions[].cargo.delivered
        missions[].cargo.total
        missions[].destinationStation
        missions[].destinationSystem
        missions[].expiry
        missions[].faction
        missions[].passengerCount
        missions[].reward
        missions[].targetFaction
        missions[].title
        name
        progress
        progress.balance
        progress.location
        progress.location.body
        progress.location.controllingPower
        progress.location.mode
        progress.location.powerplayState
        progress.location.starSystem
        progress.location.stationName
        progress.location.systemFaction
        progress.navy[]
        progress.navy[].career
        progress.navy[].percent
        progress.navy[].rank
        progress.ranks[]
        progress.ranks[].career
        progress.ranks[].percent
        progress.ranks[].rank
        progress.reputation[]
        progress.reputation[].reputation
        progress.reputation[].superpower
        routePlans[]
        routePlans[].headline
        routePlans[].jump
        routePlans[].jump.destination
        routePlans[].jump.origin
        routePlans[].jump.totalDistance
        routePlans[].jump.totalJumps
        routePlans[].jump.waypoints[]
        routePlans[].jump.waypoints[].distanceLeft
        routePlans[].jump.waypoints[].isNeutron
        routePlans[].jump.waypoints[].jumps
        routePlans[].jump.waypoints[].system
        routePlans[].kind
        routePlans[].plottedAt
        routePlans[].reached
        ships
        ships.fleet[]
        ships.fleet[].inTransit
        ships.fleet[].name
        ships.fleet[].starSystem
        ships.fleet[].stationName
        ships.fleet[].type
        ships.fleet[].value
        ships.flown
        ships.flown.cargoCapacity
        ships.flown.fuelCapacity
        ships.flown.hullHealth
        ships.flown.ident
        ships.flown.maxJumpRange
        ships.flown.modules[]
        ships.flown.modules[].blueprint
        ships.flown.modules[].blueprintLevel
        ships.flown.modules[].experimental
        ships.flown.modules[].health
        ships.flown.modules[].item
        ships.flown.modules[].slot
        ships.flown.name
        ships.flown.rebuy
        ships.flown.totalValue
        ships.flown.typeName
        ships.flown.unladenMass
        takenAt
        theme
        watches[]
        watches[].commodity
        watches[].comparison
        watches[].enabled
        watches[].fired
        watches[].firedAt
        watches[].kind
        watches[].minPad
        watches[].name
        watches[].service
        watches[].serviceKind
        watches[].station
        watches[].system
        watches[].threshold
        """;

    private const string Expected = """
        {
          "format": 1,
          "frontierId": "F9900417",
          "name": "Ada Quillon",
          "takenAt": "2026-11-12T10:00:00\u002B00:00",
          "theme": "elite",
          "progress": {
            "balance": 48250000,
            "location": {
              "starSystem": "Shinrarta Dezhra",
              "stationName": "Jameson Memorial",
              "mode": "Docked",
              "body": "Jameson Memorial",
              "systemFaction": "Pilots\u0027 Federation Local Branch",
              "controllingPower": "Zemina Torval",
              "powerplayState": "Fortified"
            },
            "ranks": [
              {
                "career": "Combat",
                "rank": 3,
                "percent": 41
              },
              {
                "career": "Trade",
                "rank": 6,
                "percent": 12
              },
              {
                "career": "Explore",
                "rank": 5,
                "percent": 88
              },
              {
                "career": "Soldier",
                "rank": 0,
                "percent": 0
              },
              {
                "career": "Exobiologist",
                "rank": 2,
                "percent": 30
              },
              {
                "career": "CQC",
                "rank": 0,
                "percent": 0
              }
            ],
            "navy": [
              {
                "career": "Empire",
                "rank": 4,
                "percent": 57
              },
              {
                "career": "Federation",
                "rank": 1,
                "percent": 5
              }
            ],
            "reputation": [
              {
                "superpower": "Empire",
                "reputation": 75
              },
              {
                "superpower": "Federation",
                "reputation": -12.5
              },
              {
                "superpower": "Alliance",
                "reputation": 30
              },
              {
                "superpower": "Independent",
                "reputation": 0
              }
            ]
          },
          "ships": {
            "fleet": [
              {
                "type": "Cobra Mk III",
                "name": "Small Hours",
                "starSystem": "Shinrarta Dezhra",
                "stationName": "Jameson Memorial",
                "inTransit": false,
                "value": 1200000
              },
              {
                "type": "Asp Explorer",
                "starSystem": "Deciat",
                "inTransit": false,
                "value": 6600000
              },
              {
                "type": "Type-9 Heavy",
                "inTransit": true,
                "value": 76000000
              }
            ],
            "flown": {
              "typeName": "python",
              "name": "Long Errand",
              "ident": "AQ-07",
              "rebuy": 3800000,
              "maxJumpRange": 24.75,
              "cargoCapacity": 64,
              "fuelCapacity": 32,
              "unladenMass": 410.5,
              "totalValue": 76000000,
              "hullHealth": 94,
              "modules": [
                {
                  "slot": "FrameShiftDrive",
                  "item": "int_hyperdrive_size5_class5",
                  "blueprint": "FSD_LongRange",
                  "blueprintLevel": 5,
                  "experimental": "Mass Manager",
                  "health": 100
                },
                {
                  "slot": "MainEngines",
                  "item": "int_engine_size6_class2",
                  "health": 97
                }
              ]
            }
          },
          "carrier": {
            "callSign": "QX7-41Z",
            "name": "QUIET ORBIT",
            "balance": 2100000000,
            "starSystem": "Shinrarta Dezhra",
            "destinationSystem": "Deciat",
            "departureTime": "2026-11-12T09:15:15\u002B00:00",
            "jumpRange": 500,
            "maxJumpRange": 500,
            "capacity": 25000,
            "freeSpace": 17630,
            "cargoTonnes": 1200,
            "dockingAccess": "squadronfriends",
            "fuelLevel": 640,
            "services": [
              {
                "role": "Refuel",
                "name": "Ilse Marrow",
                "activated": true,
                "enabled": true
              },
              {
                "role": "Shipyard",
                "name": "Tovin Haddick",
                "activated": true,
                "enabled": false
              }
            ]
          },
          "engineers": [
            {
              "name": "The Dweller",
              "station": "Black Hide",
              "system": "Wyrd",
              "specialities": [
                {
                  "kind": "Power Distributor",
                  "grade": 5
                },
                {
                  "kind": "Pulse Laser",
                  "grade": 4
                },
                {
                  "kind": "Beam Laser",
                  "grade": 3
                },
                {
                  "kind": "Burst Laser",
                  "grade": 3
                }
              ],
              "reach": "WithinReach",
              "standing": {
                "progress": "Invited"
              },
              "lightYears": 75.4735441023873,
              "jumps": 4,
              "prerequisites": [
                {
                  "text": "Deal with at least 5 black markets.",
                  "state": "Met"
                },
                {
                  "text": "Pay 500,000 credits.",
                  "state": "Unknown"
                }
              ]
            },
            {
              "name": "Juri Ishmaak",
              "station": "Pater\u0027s Memorial",
              "system": "Giryak",
              "specialities": [
                {
                  "kind": "Mine Launcher",
                  "grade": 5
                },
                {
                  "kind": "Sensors",
                  "grade": 5
                },
                {
                  "kind": "Surface Scanner",
                  "grade": 5
                },
                {
                  "kind": "Missile Rack",
                  "grade": 3
                },
                {
                  "kind": "Torpedo Pylon",
                  "grade": 3
                }
              ],
              "referredBy": [
                "Felicity Farseer"
              ],
              "reach": "WithinReach",
              "lightYears": 91.79905981442566,
              "jumps": 4,
              "prerequisites": [
                {
                  "text": "Grade 3 with Felicity Farseer.",
                  "state": "Met"
                },
                {
                  "text": "Earn more than 50 combat bonds.",
                  "state": "Unknown"
                },
                {
                  "text": "Provide 100,000 or 1,000,000 credits worth of combat bonds.",
                  "state": "Unknown"
                }
              ]
            },
            {
              "name": "Felicity Farseer",
              "station": "Farseer Inc",
              "system": "Deciat",
              "specialities": [
                {
                  "kind": "Frame Shift Drive",
                  "grade": 5
                },
                {
                  "kind": "Sensors",
                  "grade": 3
                },
                {
                  "kind": "Surface Scanner",
                  "grade": 3
                },
                {
                  "kind": "Thrusters",
                  "grade": 3
                },
                {
                  "kind": "Frame Shift Drive Interdictor",
                  "grade": 1
                },
                {
                  "kind": "Power Plant",
                  "grade": 1
                },
                {
                  "kind": "Shield Booster",
                  "grade": 1
                }
              ],
              "unlockCost": "Meta-alloys \u00D71",
              "reach": "Unlocked",
              "standing": {
                "progress": "Unlocked",
                "rank": 5,
                "rankProgress": 0
              },
              "lightYears": 101.7653071256359,
              "jumps": 5,
              "prerequisites": [
                {
                  "text": "Gain exploration rank Scout or higher.",
                  "state": "Met"
                },
                {
                  "text": "Provide 1 unit of Meta Alloys.",
                  "state": "Met"
                }
              ]
            }
          ],
          "materials": [
            {
              "displayName": "carbon",
              "category": "Raw",
              "count": 120,
              "grade": 1
            },
            {
              "displayName": "polonium",
              "category": "Raw",
              "count": 7,
              "grade": 4
            },
            {
              "displayName": "Heat Conduction Wiring",
              "category": "Manufactured",
              "count": 44,
              "grade": 1
            },
            {
              "displayName": "Classified Scan Databanks",
              "category": "Encoded",
              "count": 12,
              "grade": 3
            }
          ],
          "missions": [
            {
              "title": "Deliver 20 units of Gold",
              "faction": "Pilots\u0027 Federation Local Branch",
              "targetFaction": "Ruling Crow Syndicate",
              "destinationStation": "Garay Terminal",
              "destinationSystem": "Deciat",
              "expiry": "2026-11-13T09:00:10\u002B00:00",
              "reward": 1500000,
              "cargo": {
                "collected": 10,
                "delivered": 0,
                "total": 20
              }
            },
            {
              "title": "Take 4 tourists to Sol",
              "faction": "Pilots\u0027 Federation Local Branch",
              "destinationStation": "Abraham Lincoln",
              "destinationSystem": "Sol",
              "expiry": "2026-11-14T09:00:12\u002B00:00",
              "reward": 800000,
              "passengerCount": 4
            }
          ],
          "checklist": {
            "lists": [
              {
                "name": "Your notes",
                "home": "Yours",
                "open": 1,
                "done": 0,
                "next": "Buy limpets",
                "isHere": false,
                "items": [
                  {
                    "text": "Buy limpets",
                    "state": "Open"
                  }
                ]
              },
              {
                "name": "Long Errand",
                "home": "Ships",
                "open": 0,
                "done": 1,
                "isHere": true,
                "items": [
                  {
                    "text": "Fit a fuel scoop",
                    "state": "Done"
                  }
                ]
              }
            ],
            "goals": [
              {
                "name": "Elite V in Combat",
                "have": 3,
                "need": 13,
                "fraction": 0.2623076923076923,
                "rung": 0.41,
                "nextRank": "Expert",
                "isDone": false
              },
              {
                "name": "Federal Navy",
                "have": 1,
                "need": 14,
                "fraction": 0.075,
                "rung": 0.05,
                "nextRank": "Cadet",
                "isDone": false
              }
            ]
          },
          "watches": [
            {
              "name": "Gold at Jameson",
              "kind": "Stock",
              "enabled": true,
              "station": "Jameson Memorial",
              "system": "Shinrarta Dezhra",
              "commodity": "Gold",
              "comparison": "Above",
              "threshold": 500
            },
            {
              "name": "Encoded trader",
              "kind": "NextSystem",
              "enabled": true,
              "firedAt": "2026-11-11T20:31:00\u002B00:00",
              "fired": "Deciat \u00B7 11 Nov 20:31",
              "service": "Material Trader",
              "serviceKind": "Encoded",
              "minPad": "Large"
            }
          ],
          "routePlans": [
            {
              "kind": "Jump",
              "headline": "Shinrarta Dezhra to Colonia",
              "plottedAt": "2026-11-12T09:30:00\u002B00:00",
              "jump": {
                "origin": "Shinrarta Dezhra",
                "destination": "Colonia",
                "totalDistance": 812.4,
                "totalJumps": 9,
                "waypoints": [
                  {
                    "system": "Wregoe AC-D d12-0",
                    "jumps": 1,
                    "distanceLeft": 640.2,
                    "isNeutron": true
                  },
                  {
                    "system": "Colonia",
                    "jumps": 8,
                    "distanceLeft": 0,
                    "isNeutron": false
                  }
                ]
              }
            }
          ]
        }
        """;
}
