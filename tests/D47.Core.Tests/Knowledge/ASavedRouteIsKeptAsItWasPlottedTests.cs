using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ASavedRouteIsKeptAsItWasPlottedTests
{
    private const string Path = "saved-routes.json";

    private static readonly DateTimeOffset At = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    private static SavedRouteBook Book(MemoryFileSystem files, Func<string?>? shown = null) =>
        new(Path, files, NullLogger<SavedRouteBook>.Instance, shown);

    private static PlottedRoute Jump(string to) =>
        new("Sol", to, 100, 5, [new RouteWaypoint("Wolf 359", 8, 50, true), new RouteWaypoint(to, 8, 0, false)]);

    private static JournalEvent Arrival(string system) =>
        JournalEvent.TryParse(
            $$"""{"timestamp":"{{At.AddMinutes(5):O}}","event":"FSDJump","StarSystem":"{{system}}"}""",
            NullLogger.Instance,
            out var parsed)
            ? parsed!
            : throw new InvalidOperationException("bad journal fixture");

    private static StoredRoutePlan Plan(string to = "Colonia", int? reached = null) =>
        new()
        {
            Kind = RoutePlanKind.Jump,
            FrontierId = "F1",
            PlottedAt = At,
            Headline = $"Sol to {to}",
            Jump = Jump(to),
            Reached = reached,
        };

    [Fact]
    public void ASavedRouteTakesTheNameGiven()
    {
        var book = Book(new MemoryFileSystem(), () => "F1");

        var saved = book.Save(Plan(), "  Colonia run  ", At);

        Assert.Equal("Colonia run", saved.Name);
        Assert.Equal(RoutePlanKind.Jump, saved.Kind);
        Assert.Equal([saved.Id], book.For().Select(route => route.Id));
    }

    [Fact]
    public void ABlankNameIsTheHeadline()
    {
        var book = Book(new MemoryFileSystem(), () => "F1");

        Assert.Equal("Sol to Colonia", book.Save(Plan(), "   ", At).Name);
    }

    [Fact]
    public void ASavedRouteKeepsNoReachedStopAndNothingBought()
    {
        var book = Book(new MemoryFileSystem(), () => "F1");

        var saved = book.Save(Plan(reached: 1) with { Bought = ["Fuel Scoop"] }, "Run", At);

        Assert.Null(saved.Plan.Reached);
        Assert.Empty(saved.Plan.Bought);
    }

    [Fact]
    public void PlottingAgainOrArrivingLeavesTheSavedCopyAsItWas()
    {
        var files = new MemoryFileSystem();
        var plans = new RoutePlanBook("route-plans.json", files, NullLogger<RoutePlanBook>.Instance, () => "F1");
        var book = Book(files, () => "F1");

        plans.Record(Jump("Colonia"), "Sol to Colonia", At);
        var saved = book.Save(plans.Last(RoutePlanKind.Jump)!, "Run", At);

        plans.Record(Jump("Beagle Point"), "Sol to Beagle Point", At.AddHours(1));
        plans.Apply([Arrival("Wolf 359")], "F1");

        var kept = book.Get(saved.Id)!;

        Assert.Equal("Sol to Colonia", kept.Plan.Headline);
        Assert.Equal("Colonia", kept.Plan.Jump!.Destination);
        Assert.Null(kept.Plan.Reached);
    }

    [Fact]
    public void SavedRoutesAreNotOfferedAsSystemSuggestions()
    {
        var files = new MemoryFileSystem();
        var plans = new RoutePlanBook("route-plans.json", files, NullLogger<RoutePlanBook>.Instance, () => "F1");
        var book = Book(files, () => "F1");

        book.Save(Plan("Colonia"), "Run", At);

        Assert.DoesNotContain("Colonia", plans.Systems());
    }

    [Fact]
    public void SavedRoutesSurviveARestart()
    {
        var files = new MemoryFileSystem();
        var first = Book(files, () => "F1");
        var saved = first.Save(Plan(), "Run", At);

        var second = Book(files, () => "F1");
        second.Load();

        var again = Assert.Single(second.For());

        Assert.Equal(saved.Id, again.Id);
        Assert.Equal("Run", again.Name);
        Assert.Equal("Colonia", again.Plan.Jump!.Destination);
    }

    [Fact]
    public void EachCommanderSeesOnlyTheirOwnSavedRoutes()
    {
        var files = new MemoryFileSystem();
        var shown = "F1";
        var book = Book(files, () => shown);

        var mine = book.Save(Plan(), "Mine", At);
        shown = "F2";
        var theirs = book.Save(Plan("Beagle Point"), "Theirs", At);

        Assert.Equal([theirs.Id], book.For().Select(route => route.Id));
        Assert.Null(book.Get(mine.Id));
        Assert.False(book.Delete(mine.Id));

        shown = "F1";

        Assert.Equal([mine.Id], book.For().Select(route => route.Id));
    }

    [Fact]
    public void ADeletedRouteIsGoneAfterARestart()
    {
        var files = new MemoryFileSystem();
        var book = Book(files, () => "F1");
        var saved = book.Save(Plan(), "Run", At);
        var other = book.Save(Plan("Beagle Point"), "Other", At.AddHours(1));

        Assert.True(book.Delete(saved.Id));

        var again = Book(files, () => "F1");
        again.Load();

        Assert.Equal([other.Id], again.For().Select(route => route.Id));
    }

    [Fact]
    public void TheNewestPlottedRouteIsListedFirst()
    {
        var book = Book(new MemoryFileSystem(), () => "F1");

        var older = book.Save(Plan(), "Older", At);
        var newer = book.Save(Plan("Beagle Point") with { PlottedAt = At.AddDays(1) }, "Newer", At);

        Assert.Equal([newer.Id, older.Id], book.For().Select(route => route.Id));
    }

    [Fact]
    public void SavingAndDeletingRaiseChanged()
    {
        var book = Book(new MemoryFileSystem(), () => "F1");
        var raised = 0;
        book.Changed += () => raised++;

        var saved = book.Save(Plan(), "Run", At);
        book.Delete(saved.Id);

        Assert.Equal(2, raised);
    }

    [Fact]
    public void AnUnreadableFileIsStartedAgain()
    {
        var files = new MemoryFileSystem();
        files.WriteText(Path, "{ not json");
        var book = Book(files, () => "F1");

        book.Load();

        Assert.Empty(book.For());
    }
}
