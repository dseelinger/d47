using D47.Core.Campaigns;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>
/// The Chronicler writes a passage for each chronicle entry once, from the entry's facts only, and the passage is kept on
/// the entry across restarts and never asked for again.
/// </summary>
public sealed class TheChroniclerWritesEachEntryOnceTests
{
    private const string Squadron = "4401";

    private const string Id = "F1";

    private const string Path = "campaigns.json";

    private readonly MemoryFileSystem _files = new();

    private static ChronicleEntry Entry(string system = "Lave", string outcome = "Won") =>
        new("Conflict", system, "Lave Purple Gang", outcome, [Now.AddDays(-3), Now.AddDays(-2)], [new ChronicleWork(D47.Core.Journal.BgsWorkKind.Bounty, 1000)]);

    private CampaignStore Store(params ChronicleEntry[] entries)
    {
        var store = new CampaignStore(Path, _files, NullLogger<CampaignStore>.Instance);

        store.Load();
        store.SetFaction(Id, Us, clearIntents: false);

        foreach (var entry in entries)
        {
            store.Choose(Id, FrontKind.Conflict, entry.System, Now.AddDays(-5));
            Assert.True(store.Resolve(Id, FrontKind.Conflict, entry.System, entry));
        }

        return store;
    }

    private CampaignStore Reloaded()
    {
        var store = new CampaignStore(Path, _files, NullLogger<CampaignStore>.Instance);

        store.Load();

        return store;
    }

    private static Func<string, CancellationToken, Task<string?>> Writes(string passage, List<string> asked) =>
        (prompt, _) =>
        {
            asked.Add(prompt);
            return Task.FromResult<string?>(passage);
        };

    [Fact]
    public void OnlyEntriesWithNoPassageArePending()
    {
        var written = Entry() with { Legend = "Done." };

        Assert.Equal([1], CampaignLegend.Pending([written, Entry("Diso")]));
    }

    [Fact]
    public void TheChroniclerIsInventedFromTheSeedKeyAndIsTheSameEachTime()
    {
        var name = CampaignArcs.Chronicler(Squadron);

        Assert.StartsWith("Chronicler ", name, StringComparison.Ordinal);
        Assert.Equal(name, CampaignArcs.Chronicler(Squadron));
    }

    [Fact]
    public void TheLineCarriesTheEntrysFactsAndNothingElse()
    {
        var line = CampaignLegend.Line(Us, Squadron, Entry());

        Assert.Equal(ArcAct.Legend, line.Act);
        Assert.Equal(CampaignArcs.Chronicler(Squadron), line.Leader);
        Assert.Contains("The opponent was Lave Purple Gang.", line.Facts);
        Assert.Contains("It ended: Won.", line.Facts);
        Assert.Contains("It spanned 2 BGS ticks.", line.Facts);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("The Commander held Lave for 2 ticks.", true)]
    [InlineData("The Commander held Lave for 9 ticks.", false)]
    public void APassageIsKeptOnlyWhenItAddsNoNumberTheFactsDidNotGive(string answer, bool kept)
    {
        var line = CampaignLegend.Line(Us, Squadron, Entry());

        Assert.Equal(kept, CampaignLegend.Read(answer, line) is not null);
    }

    [Fact]
    public void ATooLongPassageIsRefused()
    {
        var line = CampaignLegend.Line(Us, Squadron, Entry());

        Assert.Null(CampaignLegend.Read(new string('a', CampaignLegend.MostCharacters + 1), line));
    }

    [Fact]
    public async Task EachEntryIsWrittenOnceAndSurvivesARestart()
    {
        var store = Store(Entry());
        var asked = new List<string>();

        var first = await CampaignLegend.RunAsync(store, Id, Us, Squadron, Writes("The Commander held Lave.", asked), null, CancellationToken.None);
        var second = await CampaignLegend.RunAsync(store, Id, Us, Squadron, Writes("Another telling.", asked), null, CancellationToken.None);

        Assert.Equal((1, 0), (first, second));
        Assert.Single(asked);
        Assert.Equal("The Commander held Lave.", Reloaded().For(Id).Chronicle[0].Legend);
    }

    [Fact]
    public async Task AFailedRequestLeavesTheEntryForTheNextRun()
    {
        var store = Store(Entry());

        var failed = await CampaignLegend.RunAsync(store, Id, Us, Squadron, (_, _) => Task.FromResult<string?>(null), null, CancellationToken.None);

        Assert.Equal(0, failed);
        Assert.Equal([0], CampaignLegend.Pending(store.For(Id).Chronicle));
    }

    [Fact]
    public void APassageIsNotKeptOverAnEntryThatIsNoLongerTheOneAskedAbout()
    {
        var store = Store(Entry());

        Assert.False(store.SetLegend(Id, 0, Entry(outcome: "Lost"), "Wrong."));
        Assert.True(store.SetLegend(Id, 0, Entry(), "Right."));
        Assert.False(store.SetLegend(Id, 0, Entry(), "Again."));
        Assert.Equal("Right.", store.For(Id).Chronicle[0].Legend);
    }

    [Fact]
    public void ResolvingAFrontRaisesTheChronicleGrew()
    {
        var store = Store();
        var raised = new List<string>();

        store.ChronicleGrew += raised.Add;
        store.Choose(Id, FrontKind.Conflict, "Lave", Now);
        store.Resolve(Id, FrontKind.Conflict, "Lave", Entry());

        Assert.Equal([Id], raised);
    }

    [Fact]
    public void OpenCrisesSurviveARestartAndAreClearedWithAnotherFaction()
    {
        var store = Store();

        store.SetCrises(Id, [new OpenCrisis("Lave", "Famine", "Lave Purple Gang", Now, true)]);

        Assert.Equal(new OpenCrisis("Lave", "Famine", "Lave Purple Gang", Now, true), Assert.Single(Reloaded().For(Id).Crises));

        Reloaded().SetFaction(Id, "Another Faction", clearIntents: false);

        Assert.Empty(Reloaded().For(Id).Crises);
    }
}
