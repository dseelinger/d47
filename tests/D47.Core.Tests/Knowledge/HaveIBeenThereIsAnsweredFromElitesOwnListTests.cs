using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Journal;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// <c>system_visits</c> and <c>describe_system</c> answer from the current Commander's
/// <c>VisitedStarsCache.dat</c>, and a system the file does not list is no record, not never.
/// </summary>
[Trait("Category", "Integration")]
public sealed class HaveIBeenThereIsAnsweredFromElitesOwnListTests : IDisposable
{
    private const long Lave = VisitedStarsCacheIsReadFromItsBytesTests.Lave;

    private const long Sol = VisitedStarsCacheIsReadFromItsBytesTests.Sol;

    private const long Diaguandri = 670417429889;

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("d47-visits-");

    private readonly VisitedStarsBook _book;

    public HaveIBeenThereIsAnsweredFromElitesOwnListTests()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "735466"));
        File.WriteAllBytes(
            Path.Combine(_root.FullName, "735466", VisitedStarsCache.FileName),
            VisitedStarsCacheIsReadFromItsBytesTests.File(
                512,
                [
                    (Lave, 3, VisitedStarsCacheIsReadFromItsBytesTests.Second),
                    (Sol, 1, VisitedStarsCacheIsReadFromItsBytesTests.Fifth),
                ]));
        _book = new VisitedStarsBook(_root.FullName);
    }

    public void Dispose() => _root.Delete(recursive: true);

    private sealed class FakeSystems : IStarSystemService
    {
        public List<SystemNameMatch> Matches { get; } = [];

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = "Fixture", SystemAddress = systemAddress });

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>(Matches);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(
            string system,
            double lightYears,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private Task<ToolResult> CallAsync(
        string tool,
        Dictionary<string, string> arguments,
        bool lookups = true,
        string frontierId = "F735466",
        long here = Sol)
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, lookups ? "true" : "false", SettingsCaller.Panel);

        var systems = new FakeSystems();
        systems.Matches.Add(new SystemNameMatch("Lave", Lave, new StarPosition(0, 0, 0)));
        systems.Matches.Add(new SystemNameMatch("Diaguandri", Diaguandri, new StarPosition(0, 0, 0)));
        var state = new CommanderGameState(new CommanderIdentity(frontierId, "Fixture"));

        return GalaxyCapability.Create(
                null,
                () => here == Sol ? "Sol" : "Lave",
                settings,
                gameState: () => state,
                systems: systems,
                currentAddress: () => here,
                visits: _book)
            .Tools.Single(definition => definition.Name == tool)
            .Handler(new ToolArguments(arguments), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AVisitedSystemSaysHowManyTimesAndWhenLast()
    {
        var result = await CallAsync("system_visits", new() { ["system"] = "lave" });

        Assert.False(result.IsError);
        Assert.Equal("Yes. The Commander has been to Lave 3 times, last on 2 October 2026.", result.Content);
    }

    [Fact]
    public async Task OneVisitIsSaidAsOnce()
    {
        var result = await CallAsync("system_visits", []);

        Assert.Equal("Yes. The Commander has been to Sol once, on 5 January 2026.", result.Content);
    }

    [Fact]
    public async Task ASystemTheFileDoesNotListIsNoRecordNotNever()
    {
        var result = await CallAsync("system_visits", new() { ["system"] = "Diaguandri" });

        Assert.False(result.IsError);
        Assert.Equal(
            "Elite's list of visited systems on this PC has no record of a visit to Diaguandri. "
            + "That is not proof the Commander has never been there.",
            result.Content);
    }

    [Fact]
    public async Task AnotherCommandersFileIsNotRead()
    {
        var result = await CallAsync("system_visits", new() { ["system"] = "Lave" }, frontierId: "F14064573");

        Assert.True(result.IsError);
        Assert.Equal("I can't read Elite's list of visited systems for this Commander on this PC.", result.Content);
    }

    [Fact]
    public async Task WithLookupsOffOnlyTheCurrentSystemCanBeChecked()
    {
        var elsewhere = await CallAsync("system_visits", new() { ["system"] = "Lave" }, lookups: false);
        var here = await CallAsync("system_visits", new() { ["system"] = "Lave" }, lookups: false, here: Lave);

        Assert.True(elsewhere.IsError);
        Assert.Equal(
            "Galaxy lookups are off, so I can only check the system the Commander is in, Sol.",
            elsewhere.Content);
        Assert.Equal("Yes. The Commander has been to Lave 3 times, last on 2 October 2026.", here.Content);
    }

    [Fact]
    public async Task DescribeSystemAddsTheVisitSentenceForAVisitedSystem()
    {
        var result = await CallAsync("describe_system", new() { ["system"] = "Lave" });

        Assert.EndsWith(
            "No stations on record. The Commander has been here 3 times, last on 2 October 2026.",
            result.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DescribeSystemSaysNothingAboutVisitsForAnUnlistedSystem()
    {
        var result = await CallAsync("describe_system", new() { ["system"] = "Diaguandri" });

        Assert.EndsWith("No stations on record.", result.Content, StringComparison.Ordinal);
    }
}
