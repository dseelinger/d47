using D47.Core.Adventures;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using D47.Core.Journal;
using D47.Core.Storage;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public sealed class ABenchStepSurvivesReplayTests
{
    private const string Key = "the-lantern-route";

    private static readonly string Folder = Path.Combine(MemoryInstall.FakeRoot, "bench-step");
    private static readonly string AdventuresFile = Path.Combine(Folder, "adventures.json");

    private readonly MemoryFileSystem _files = new();
    private readonly AdventureStore _store;
    private readonly AdventureBook _book;

    public ABenchStepSurvivesReplayTests()
    {
        _store = new AdventureStore(AdventuresFile, _files, NullLogger<AdventureStore>.Instance);
        _book = new AdventureBook(_store, NullLogger<AdventureBook>.Instance);
    }

    private static DateTimeOffset At(int minutes) => Accepted.AddMinutes(minutes);

    private AdventureStanding Begun(Adventure? adventure = null)
    {
        _book.Write("F1", adventure ?? LanternRoute());
        Assert.Null(_book.Begin("F1", Key, Accepted));
        _book.Drain();
        _book.Told("F1", Key, new AdventureTold { Kind = AdventureToldKind.Beat, Text = "Opening.", At = Accepted });
        _book.CatchUp([]);
        return _book.Standing("F1", Key)!;
    }

    private string Journal(params JournalEvent[] events)
    {
        var path = Path.Combine(Folder, "Journal.2026-08-22T190000.01.log");
        _files.WriteText(path, string.Join('\n', events.Select(e => e.Raw.GetRawText())) + "\n");
        return path;
    }

    [Fact]
    public void SteppingTheFirstBeatFiresItAsTheBench()
    {
        Begun();

        Assert.Null(_book.Step("F1", Key, At(5)));

        var standing = _book.Standing("F1", Key)!;
        Assert.Equal(1, standing.Current);
        Assert.Equal("Bench", standing.FiredBy[0]);
        Assert.Equal(At(5), standing.Fired[0]);

        var moment = Assert.Single(_book.Drain());
        Assert.Equal(0, moment.Beat);
        Assert.True(_book.IsStirring("F1", Key));
    }

    [Fact]
    public void AStepSurvivesAWalkOverJournalsWithNoMatchingEvent()
    {
        Begun();
        Assert.Null(_book.Step("F1", Key, At(5)));

        _book.CatchUp([Journal(Commander("F1", Accepted.AddMinutes(-1)), Jump(Home, At(1)), Jump(Home, At(9)))]);

        var standing = _book.Standing("F1", Key)!;
        Assert.Equal(1, standing.Current);
        Assert.Equal("Bench", standing.FiredBy[0]);
        Assert.Equal(At(5), standing.Fired[0]);
    }

    [Fact]
    public void AStepSurvivesARestart()
    {
        Begun();
        Assert.Null(_book.Step("F1", Key, At(5)));

        var reopened = new AdventureStore(AdventuresFile, _files, NullLogger<AdventureStore>.Instance);
        reopened.Poll();

        var restarted = new AdventureBook(reopened, NullLogger<AdventureBook>.Instance);
        restarted.CatchUp([]);

        Assert.Equal(1, restarted.Standing("F1", Key)!.Current);
    }

    [Fact]
    public void AnEventBeforeTheStepDoesNotCountTowardTheNextBeatButOneAfterDoes()
    {
        Begun();
        Assert.Null(_book.Step("F1", Key, At(5)));

        _book.CatchUp([Journal(Commander("F1", Accepted.AddMinutes(-1)), Jump(QuietField, At(2)), Scan(QuietField, 6, At(3)))]);
        Assert.Equal(1, _book.Standing("F1", Key)!.Current);

        _book.CatchUp([Journal(Commander("F1", Accepted.AddMinutes(-1)), Jump(QuietField, At(2)), Scan(QuietField, 6, At(7)))]);
        Assert.Equal(2, _book.Standing("F1", Key)!.Current);
    }

    [Fact]
    public void AStepOnACountedBeatPartwayThroughResetsTheCount()
    {
        Begun(LanternRoute() with
        {
            Beats =
            [
                Beat("Bonds", "setup", new AdventureTrigger { Kind = TriggerKind.Bond, Count = 3, Faction = "Labour" }, "Three."),
                Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "Home."),
            ],
        });
        _book.Observe(Event($$"""{ "timestamp":"{{Stamp(At(1))}}", "event":"FactionKillBond", "Reward":80000, "AwardingFaction":"Labour", "VictimFaction":"X" }"""), "F1");
        Assert.Equal(1, _book.Standing("F1", Key)!.Counted);

        Assert.Null(_book.Step("F1", Key, At(2)));

        var standing = _book.Standing("F1", Key)!;
        Assert.Equal(1, standing.Current);
        Assert.Equal(0, standing.Counted);
    }

    [Fact]
    public void SteppingTheLastBeatLeavesTheStandingDone()
    {
        Begun(LanternRoute() with { Beats = [LanternRoute().Beats[0]] });

        Assert.Null(_book.Step("F1", Key, At(5)));

        Assert.True(_book.Standing("F1", Key)!.IsDone);
    }

    [Fact]
    public void AShortStoryStepsTheSameWay()
    {
        Begun(LanternRoute() with { StoryId = null });

        Assert.Null(_book.Step("F1", Key, At(5)));
        Assert.Equal(1, _book.Standing("F1", Key)!.Current);
    }

    [Fact]
    public void AStepIsRefusedWhereNothingCanBeStepped()
    {
        Assert.NotNull(_book.Step("F1", "missing", At(5)));

        _book.Write("F1", LanternRoute());
        Assert.NotNull(_book.Step("F1", Key, At(5)));
        Assert.Empty(_store.Find("F1", Key)!.Stepped);

        Begun();
        Assert.Null(_book.Step("F1", Key, At(5)));

        Assert.NotNull(_book.Step("F1", Key, At(6)));
        Assert.Equal(1, _book.Standing("F1", Key)!.Current);
        Assert.Single(_store.Find("F1", Key)!.Stepped);
    }

    [Fact]
    public void AnAbandonedStoryCannotBeStepped()
    {
        Begun();
        Assert.Null(_book.Abandon("F1", Key, At(1)));

        Assert.NotNull(_book.Step("F1", Key, At(2)));
        Assert.Empty(_store.Find("F1", Key)!.Stepped);
    }

    [Fact]
    public void ADoneChapterCannotBeStepped()
    {
        Begun(LanternRoute() with { Beats = [LanternRoute().Beats[0]] });
        Assert.Null(_book.Step("F1", Key, At(5)));
        _book.Drain();
        _book.Told("F1", Key, new AdventureTold { Kind = AdventureToldKind.Beat, Text = "Scoop here.", At = At(6), Beat = 0 });

        Assert.NotNull(_book.Step("F1", Key, At(7)));
        Assert.Single(_store.Find("F1", Key)!.Stepped);
    }

    [Fact]
    public void AnAdventureWithNoStepsIsWrittenWithoutThem()
    {
        Begun();

        Assert.DoesNotContain("stepped", _files.ReadText(AdventuresFile), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BeginClearsTheSteps()
    {
        Begun();
        Assert.Null(_book.Step("F1", Key, At(5)));
        Assert.Null(_book.Abandon("F1", Key, At(6)));

        Assert.Null(_book.Begin("F1", Key, At(10)));

        Assert.Empty(_store.Find("F1", Key)!.Stepped);
    }

    [Trait("Category", "Gate")]
    [Fact]
    public void NothingButTheBenchCallsTheBooksStep()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);

        while (root is not null && !File.Exists(Path.Combine(root.FullName, "d47.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);

        var callers = Directory.EnumerateFiles(Path.Combine(root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Step" } access && access.Expression.ToString().Contains("book", StringComparison.OrdinalIgnoreCase)))
            .Select(file => Path.GetFileName(file))
            .ToList();

        Assert.Equal(["StoryBench.cs"], callers);
    }
}
