using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>A mission on the board is not a checklist line, and a stored mission line is removed on load (#831).</summary>
[Trait("Category", "Integration")]
public sealed class AMissionIsNotOnTheChecklistTests : IDisposable
{
    private const string Haul = "Deliver 99 Low Temperature Diamonds to Crown Barracks, Wadjuk";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-mission-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private (GameStateStore Game, ChecklistService Service) Bench(bool storedMissionLine)
    {
        Directory.CreateDirectory(_folder);

        var game = new GameStateStore();
        game.Apply(Event("""{"timestamp":"2026-09-22T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        var store = new ChecklistStore(
            Path.Combine(_folder, "checklist.json"), NullLogger<ChecklistStore>.Instance);

        if (storedMissionLine)
        {
            store.Apply("F1", "Jameson", document => new ChecklistChange(
                document with
                {
                    Items =
                    [
                        new ChecklistItem
                        {
                            Key = "mission:1041612601",
                            Scope = ChecklistScope.Universal,
                            Kind = ChecklistItemKind.Derived,
                            Source = ChecklistSource.Mission,
                            Text = Haul,
                            Intent = new ChecklistIntent(ChecklistIntentKind.Commodity, "1041612601"),
                        },
                    ],
                },
                Changed: true,
                string.Empty));
        }

        var service = new ChecklistService(
            store,
            new ChecklistProposalStore(
                Path.Combine(_folder, "proposals.json"), NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        return (game, service);
    }

    private static IReadOnlyList<ChecklistItem> MissionLines(ChecklistService service) =>
        [.. service.Document.Items.Where(item => item.Source == ChecklistSource.Mission)];

    [Fact]
    public void ADeliveryMissionOnTheBoardAddsNoLine()
    {
        var (game, service) = Bench(storedMissionLine: false);

        game.Apply(Event(
            """
            {"timestamp":"2026-09-22T10:10:00Z","event":"MissionAccepted","Faction":"Party of Yoru","Name":"Mission_Delivery_Boom",
             "LocalisedName":"Boom time delivery of 99 units of Low Temperature Diamonds",
             "Commodity":"$LowTemperatureDiamond_Name;","Commodity_Localised":"Low Temperature Diamonds","Count":99,
             "DestinationSystem":"Wadjuk","DestinationStation":"Crown Barracks","Expiry":"2026-09-27T13:43:06Z",
             "Wing":false,"Reward":2799184,"MissionID":1041612601}
            """.ReplaceLineEndings(" ")));
        service.Poll();

        Assert.Single(game.Active!.Missions.Missions);
        Assert.Empty(MissionLines(service));
    }

    [Fact]
    public void AStoredMissionLineIsGoneAfterLoad()
    {
        var (_, service) = Bench(storedMissionLine: true);

        Assert.Single(MissionLines(service));

        service.Poll();

        Assert.Empty(MissionLines(service));
    }

    [Fact]
    public void TheChecklistReadoutListsNoMissionLine()
    {
        var (_, service) = Bench(storedMissionLine: true);

        service.Poll();

        Assert.DoesNotContain("Low Temperature Diamonds", service.Report(null, null, null, null, false));
    }
}
