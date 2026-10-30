using System.Reflection;
using System.Text.Json;
using D47.Core.Activities;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// A per-Commander history in <see cref="HistoryBackfill"/> holds nothing from before a <c>NewCommander</c>
/// for that Frontier id (#1804).
/// </summary>
[Trait("Category", "Gate")]
public sealed class NoHistoryKeepsWhatAnEraseRemovedGateTests
{
    private const string F = "F1";

    private const string Name = "Jameson";

    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true, MaxDepth = 64 };

    /// <summary><see cref="HistoryBackfill"/> properties that are neither a history nor a ledger.</summary>
    private static readonly string[] Plumbing =
    [
        nameof(HistoryBackfill.Directory),
        nameof(HistoryBackfill.FileSystem),
        nameof(HistoryBackfill.Loggers),
        nameof(HistoryBackfill.LoadoutFile),
        nameof(HistoryBackfill.WantedShips),
        nameof(HistoryBackfill.KitFile),
        nameof(HistoryBackfill.NameFile),
        nameof(HistoryBackfill.Step),
        nameof(HistoryBackfill.Now),
    ];

    /// <summary>Properties that keep their value across an erase, with the reason.</summary>
    private static readonly Dictionary<string, string> Exempt = new()
    {
        [nameof(HistoryBackfill.Commanders)] = "Who exists, which an erase does not change.",
        [nameof(HistoryBackfill.Names)] = "How names are said, which an erase does not change.",
        [nameof(HistoryBackfill.Signals)] = "What is in a system, which is not the Commander's.",
        [nameof(HistoryBackfill.Erases)] = "The record of the erase itself.",
        [nameof(HistoryBackfill.Credits)] = "Replaced by the LoadGame that follows the erase.",
    };

    private static string Line(string time, string body) => "{\"timestamp\":\"2026-10-" + time + "Z\"," + body + "}";

    private static string Login(string day) =>
        Line(day + "T09:00:00", "\"event\":\"LoadGame\",\"FID\":\"" + F + "\",\"Commander\":\"" + Name + "\",\"Ship\":\"Python\",\"ShipID\":2,\"Credits\":5000");

    private static string Erase(string day) =>
        Line(day + "T10:00:00", "\"event\":\"NewCommander\",\"FID\":\"" + F + "\",\"Name\":\"" + Name + "\",\"Package\":\"Default\"");

    private static string[] Played(string day) =>
    [
        Login(day),
        Line(day + "T09:01:00", "\"event\":\"Loadout\",\"Ship\":\"sidewinder\",\"ShipID\":1,\"ShipName\":\"Old\",\"ShipIdent\":\"AB-01\",\"Modules\":[{\"Slot\":\"Armour\",\"Item\":\"sidewinder_armour_grade1\",\"On\":true,\"Priority\":1}]"),
        Line(day + "T09:02:00", "\"event\":\"StoredShips\",\"StarSystem\":\"Somewhere\",\"StationName\":\"Somewhere Station\",\"ShipsHere\":[{\"ShipID\":51,\"ShipType\":\"Anaconda\",\"Value\":1}],\"ShipsRemote\":[]"),
        Line(day + "T09:03:00", "\"event\":\"BuySuit\",\"Name\":\"UtilitySuit_Class1\",\"Price\":1000,\"SuitID\":1"),
        Line(day + "T09:04:00", "\"event\":\"BuyWeapon\",\"Name\":\"Wingman_Rifle\",\"Class\":1,\"Price\":1000,\"SuitModuleID\":1"),
        Line(day + "T09:05:00", "\"event\":\"CarrierStats\",\"CarrierID\":1,\"Callsign\":\"BNH-T2F\",\"Name\":\"Sacred Fire\",\"DockingAccess\":\"all\",\"FuelLevel\":900"),
        Line(day + "T09:06:00", "\"event\":\"CarrierLocation\",\"CarrierID\":1,\"CarrierType\":\"FleetCarrier\",\"StarSystem\":\"Meene\""),
        Line(day + "T09:07:00", "\"event\":\"MissionAccepted\",\"Faction\":\"Party\",\"Name\":\"Mission_Courier\",\"LocalisedName\":\"Courier\",\"DestinationSystem\":\"Wadjuk\",\"DestinationStation\":\"Crown\",\"Expiry\":\"2026-10-30T00:00:00Z\",\"Reward\":1000,\"MissionID\":1"),
        Line(day + "T09:08:00", "\"event\":\"Docked\",\"StationName\":\"$EXT_PANEL_ColonisationShip; Old\",\"StationType\":\"SurfaceStation\",\"StarSystem\":\"Col 285\",\"SystemAddress\":123,\"MarketID\":100,\"StationFaction\":{\"Name\":\"Alpha Mining\"}"),
        Line(day + "T09:09:00", "\"event\":\"ColonisationConstructionDepot\",\"MarketID\":100,\"ConstructionProgress\":0.25,\"ConstructionComplete\":false,\"ConstructionFailed\":false,\"ResourcesRequired\":[{\"Name\":\"$steel_name;\",\"Name_Localised\":\"Steel\",\"RequiredAmount\":500,\"ProvidedAmount\":80,\"Payment\":3239}]"),
        Line(day + "T09:10:00", "\"event\":\"TechnologyBroker\",\"BrokerType\":\"guardian\",\"ItemsUnlocked\":[{\"Name\":\"Int_GuardianFSDBooster_Size1\"}]"),
        Line(day + "T09:11:00", "\"event\":\"Reputation\",\"Federation\":10.0,\"Empire\":5.0,\"Alliance\":1.0"),
        Line(day + "T09:12:00", "\"event\":\"EngineerProgress\",\"Engineers\":[{\"Engineer\":\"Felicity Farseer\",\"EngineerID\":300100,\"Progress\":\"Unlocked\",\"RankProgress\":0,\"Rank\":1}]"),
        Line(day + "T09:13:00", "\"event\":\"Powerplay\",\"Power\":\"Li Yong-Rui\",\"Rank\":8,\"Merits\":44000"),
        Line(day + "T09:14:00", "\"event\":\"PowerplayMerits\",\"Power\":\"Li Yong-Rui\",\"MeritsGained\":900,\"TotalMerits\":45000"),
        Line(day + "T09:15:00", "\"event\":\"LaunchDrone\",\"Type\":\"Prospector\""),
        Line(day + "T09:16:00", "\"event\":\"MiningRefined\",\"Type\":\"Gold\""),
        Line(day + "T09:16:30", "\"event\":\"Missions\",\"Active\":[{\"MissionID\":1,\"Name\":\"Mission_Courier\",\"PassengerMission\":false,\"Expires\":60}],\"Failed\":[],\"Complete\":[]"),
        Line(day + "T09:16:40", "\"event\":\"Docked\",\"StationName\":\"Hutton\",\"MarketID\":100"),
        Line(day + "T09:17:00", "\"event\":\"MarketSell\",\"MarketID\":100,\"Type\":\"gold\",\"Count\":10,\"SellPrice\":50000,\"TotalSale\":500000,\"AvgPricePaid\":40000"),
        Line(day + "T09:18:00", "\"event\":\"Bounty\",\"Target\":\"empire_trader\",\"TotalReward\":1000,\"VictimFaction\":\"Empire\",\"Rewards\":[{\"Faction\":\"Party\",\"Reward\":1000}]"),
        Line(day + "T09:19:00", "\"event\":\"CodexEntry\",\"EntryID\":1,\"Name\":\"$Codex_Ent_Cactoid_01_Name;\",\"Name_Localised\":\"Cactoida\",\"SubCategory\":\"$Codex_SubCategory_Organic_Structures;\",\"Category\":\"$Codex_Category_Biology;\",\"Region\":\"$Codex_RegionName_1;\",\"System\":\"Sol\",\"SystemAddress\":1"),
        Line(day + "T09:20:00", "\"event\":\"ScanOrganic\",\"ScanType\":\"Analyse\",\"Genus\":\"$Codex_Ent_Cactoid_Genus_Name;\",\"Genus_Localised\":\"Cactoida\",\"Species\":\"$Codex_Ent_Cactoid_01_Name;\",\"Species_Localised\":\"Cactoida Cortexum\",\"SystemAddress\":1,\"Body\":1"),
        Line(day + "T09:21:00", "\"event\":\"Scan\",\"ScanType\":\"Detailed\",\"BodyName\":\"Body 1\",\"BodyID\":1,\"StarSystem\":\"Sol\",\"SystemAddress\":1,\"PlanetClass\":\"Earthlike body\",\"TerraformState\":\"\",\"MassEM\":1.0,\"WasDiscovered\":false,\"WasMapped\":false"),
        Line(day + "T09:22:00", "\"event\":\"SAAScanComplete\",\"BodyName\":\"Body 1\",\"BodyID\":1,\"SystemAddress\":1,\"ProbesUsed\":4,\"EfficiencyTarget\":6"),
        Line(day + "T09:23:00", "\"event\":\"CommitCrime\",\"CrimeType\":\"assault\",\"Faction\":\"Alliance\",\"Fine\":400"),
        Line(day + "T09:24:00", "\"event\":\"WingAdd\",\"Name\":\"CMDR Past\""),
        Line(day + "T09:25:00", "\"event\":\"MissionCompleted\",\"Faction\":\"Party\",\"Name\":\"Mission_Courier\",\"MissionID\":1,\"Reward\":1000"),
        Line(day + "T09:25:10", "\"event\":\"ApproachBody\",\"StarSystem\":\"Sol\",\"SystemAddress\":1,\"Body\":\"Body 1\",\"BodyID\":1"),
        Line(day + "T09:25:20", "\"event\":\"LaunchSRV\",\"SRVType\":\"mev_rhino\",\"PlayerControlled\":true"),
        Line(day + "T09:25:30", "\"event\":\"MiningRefined\",\"Type\":\"$platinum_name;\",\"Type_Localised\":\"Platinum\""),
        Line(day + "T09:25:40", "\"event\":\"DockSRV\",\"SRVType\":\"mev_rhino\""),
        Line(day + "T09:25:50", "\"event\":\"FSSAllBodiesFound\",\"SystemAddress\":1,\"SystemName\":\"Sol\",\"Count\":8"),
        Line(day + "T09:26:00", "\"event\":\"FSDJump\",\"StarSystem\":\"Sol\",\"SystemAddress\":1,\"StarPos\":[0.0,0.0,0.0],\"JumpDist\":5.0"),
    ];

    private sealed record Run(HistoryBackfill Backfill, Dictionary<string, Func<string>> Ledgers);

    private static string Snapshot(object? value) => JsonSerializer.Serialize(value, Json);

    private static Run Walk(params string[][] files)
    {
        var fileSystem = new MemoryFileSystem();

        for (var i = 0; i < files.Length; i++)
        {
            fileSystem.WriteLines($"j/Journal.2026-10-{10 + i:00}T000000.01.log", files[i]);
        }

        var logger = NullLogger.Instance;
        var exobiology = new ExobiologyLedger(null, fileSystem, logger);
        var cartography = new CartographyLedger(null, fileSystem, logger);
        var crimes = new OutstandingCrimes(fileSystem, logger);
        var activities = new ActivityLedger(null, fileSystem, logger);
        var mail = new MailLedger("mail.json", fileSystem, logger);
        var signals = new SignalSightings(fileSystem, logger);
        var met = new CommanderLog(fileSystem, logger);
        var travel = new TravelLog(fileSystem, logger);
        var fullyFound = new FullyFoundSystems(fileSystem, logger);

        var backfill = new HistoryBackfill
        {
            Directory = "j",
            FileSystem = fileSystem,
            Loggers = NullLoggerFactory.Instance,
            Exobiology = exobiology,
            Cartography = cartography,
            Crimes = crimes,
            Activities = activities,
            Mail = mail,
            Signals = signals,
            MetCommanders = met,
            Travel = travel,
            FullyFound = fullyFound,
        };

        backfill.Run(TestContext.Current.CancellationToken);
        Assert.Equal(HistoryState.Done, backfill.State);

        var ledgers = new Dictionary<string, Func<string>>
        {
            [nameof(HistoryBackfill.Exobiology)] = () => Snapshot(exobiology.Unsold(F).Held),
            [nameof(HistoryBackfill.Cartography)] = () => Snapshot(cartography.Unsold(F).Held),
            [nameof(HistoryBackfill.Crimes)] = () => Snapshot(crimes.Owed(F, 1)),
            [nameof(HistoryBackfill.Activities)] = () => Snapshot(activities.Stalest(100, F)) + Snapshot(activities.LastDone("mining", F)),
            [nameof(HistoryBackfill.Mail)] = () => mail.Compose(F),
            [nameof(HistoryBackfill.MetCommanders)] = () => Snapshot(met.Met(F)),
            [nameof(HistoryBackfill.Travel)] = () => Snapshot(travel.Steps(F)),
            [nameof(HistoryBackfill.FullyFound)] = () => fullyFound.Count(F).ToString(),
        };

        return new Run(backfill, ledgers);
    }

    private static IEnumerable<PropertyInfo> Histories() =>
        typeof(HistoryBackfill).GetProperties()
            .Where(property => property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)
                && property.PropertyType.GetGenericArguments()[0] == typeof(string)
                && !Exempt.ContainsKey(property.Name));

    /// <summary>The Commander's entry as JSON; "empty" when it is absent or equals its type's Empty or None.</summary>
    private static string DictionaryValue(Run run, PropertyInfo property)
    {
        var value = property.GetValue(run.Backfill);
        Assert.NotNull(value);

        var arguments = new object?[] { F, null };

        if (value.GetType().GetMethod("TryGetValue")!.Invoke(value, arguments) is not true)
        {
            return "empty";
        }

        var held = arguments[1];
        var empty = held!.GetType().GetField("Empty") ?? held.GetType().GetField("None");

        return empty is not null && Snapshot(held) == Snapshot(empty.GetValue(null)) ? "empty" : Snapshot(held);
    }

    private static string Relogin() => Line("10T11:00:00", "\"event\":\"LoadGame\",\"FID\":\"" + F + "\",\"Commander\":\"" + Name + "\",\"Ship\":\"Python\",\"ShipID\":2,\"Credits\":5000");

    private static string[] Erased() => [Login("10"), Erase("10"), Relogin()];

    [Fact]
    public void EveryLedgerPropertyIsCheckedOrExempt()
    {
        var ledgers = typeof(HistoryBackfill).GetProperties()
            .Where(property => property.SetMethod is { } setter
                && setter.ReturnParameter.GetRequiredCustomModifiers().Any(modifier => modifier.Name == "IsExternalInit")
                && !Plumbing.Contains(property.Name))
            .Select(property => property.Name);

        var checkedLedgers = Walk(Erased()).Ledgers.Keys;

        var missing = ledgers.Where(name => !checkedLedgers.Contains(name) && !Exempt.ContainsKey(name)).ToList();

        Assert.True(missing.Count == 0, "Not checked and not exempt: " + string.Join(", ", missing));
    }

    [Fact]
    public void TheJournalTouchesEveryHistoryItChecks()
    {
        var control = Walk(Erased());
        var played = Walk([.. Played("10")]);

        var untouched = new List<string>();

        foreach (var property in Histories())
        {
            if (DictionaryValue(played, property) == DictionaryValue(control, property))
            {
                untouched.Add(property.Name);
            }
        }

        foreach (var (name, read) in played.Ledgers)
        {
            if (read() == control.Ledgers[name]())
            {
                untouched.Add(name);
            }
        }

        Assert.True(untouched.Count == 0, "The played journal leaves these as the control has them: " + string.Join(", ", untouched));
    }

    [Fact]
    public void NoHistoryKeepsWhatAnEraseRemoved()
    {
        var control = Walk(Erased());
        var erased = Walk([.. Played("10"), Erase("10"), Relogin()]);

        var kept = new List<string>();

        foreach (var property in Histories())
        {
            if (DictionaryValue(erased, property) != DictionaryValue(control, property))
            {
                kept.Add(property.Name);
            }
        }

        foreach (var (name, read) in erased.Ledgers)
        {
            if (read() != control.Ledgers[name]())
            {
                kept.Add(name);
            }
        }

        Assert.True(kept.Count == 0, "Still holding data from before the erase: " + string.Join(", ", kept));
    }
}
