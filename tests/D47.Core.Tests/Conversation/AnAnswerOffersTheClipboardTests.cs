using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The join between finding a place and being able to paste it (asked for 2026-08-21:
/// <i>"offer to put it on the clipboard … the asking part already works — what I need is the
/// prompting for the clipboard"</i>).
/// <para>
/// <b>Both halves were already here.</b> A search that finds the nearest system holding a material
/// has worked for phases, and so has a tool that puts a name on the clipboard. Nothing joined them,
/// so a Commander read a system name off a list and typed it into the galaxy map by hand — the
/// errand <c>copy_to_clipboard</c> exists to end.
/// </para>
/// </summary>
public class AnAnswerOffersTheClipboardTests
{
    private static ToolDefinition Tool(ClipboardOffer offer, IGalaxyService galaxy, string name) =>
        EngineeringCapability.Create(() => null, galaxy, offer).Tools.Single(tool => tool.Name == name);

    private static async Task<string> AskAsync(
        ClipboardOffer offer, IGalaxyService galaxy, string tool, params (string Key, string Value)[] arguments)
    {
        var result = await Tool(offer, galaxy, tool).Handler(
            new ToolArguments(arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)),
            CancellationToken.None);

        return result.Content;
    }

    [Fact]
    public async Task FindingAMaterialOffersTheNearestSystem()
    {
        var offer = new ClipboardOffer();

        var said = await AskAsync(
            offer,
            new StubGalaxy { Systems = ["Cubeo", "Quince"] },
            "find_material",
            ("material", "Imperial Shielding"),
            ("near", "Sol"));

        Assert.Contains("Say \"copy that\" and I will put Cubeo on your clipboard.", said, StringComparison.Ordinal);
        Assert.Equal("Cubeo", offer.Text);
    }

    [Fact]
    public async Task AndFindingATraderOffersItsSystem()
    {
        var offer = new ClipboardOffer();

        var said = await AskAsync(
            offer,
            new StubGalaxy { Stations = [("Jameson Memorial", "Shinrarta Dezhra")] },
            "find_material_trader",
            ("type", "Manufactured"));

        Assert.Contains("copy that", said, StringComparison.Ordinal);
        Assert.Equal("Shinrarta Dezhra", offer.Text);
    }

    /// <summary>
    /// <b>A search that found nothing clears the offer</b> rather than leaving the previous system
    /// answering to "that". Copying a system from two questions ago is worse than copying nothing:
    /// it is wrong and it looks like it worked.
    /// </summary>
    [Fact]
    public async Task ASearchThatFoundNothingLeavesNoOfferStanding()
    {
        var offer = new ClipboardOffer();

        await AskAsync(
            offer,
            new StubGalaxy { Systems = ["Cubeo"] },
            "find_material",
            ("material", "Imperial Shielding"));

        Assert.True(offer.IsStanding);

        var said = await AskAsync(
            offer,
            new StubGalaxy(),
            "find_material",
            ("material", "Imperial Shielding"));

        Assert.False(offer.IsStanding);
        Assert.DoesNotContain("copy that", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The phrases reach the clipboard tool carrying the system, which is the whole reason they
    /// have to be dynamic: a declared phrase carries fixed arguments and what is being copied
    /// changes with every answer.
    /// </summary>
    [Fact]
    public void TakingUpTheOfferCarriesTheSystemToTheClipboardTool()
    {
        var offer = new ClipboardOffer();

        Assert.Empty(offer.Phrases());

        offer.Offer("Deciat", "the system");

        var commands = offer.Phrases().ToList();

        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            Assert.Equal(NavigationCapability.Id, command.CapabilityId);
            Assert.Equal("copy_to_clipboard", command.ToolName);
            Assert.Equal("Deciat", command.Arguments["text"]);
        }

        Assert.Contains(commands, command => command.Phrase == "copy that");
    }

    /// <summary>
    /// <b>No bare "yes".</b> That word is spoken for while a checklist proposal is waiting, and a
    /// router phrase meaning two things depending on what else is going on is one nobody can
    /// predict. Every phrase here says copy or names the clipboard.
    /// </summary>
    [Fact]
    public void NoPhraseIsABareConfirmation()
    {
        var offer = new ClipboardOffer();
        offer.Offer("Deciat", "the system");

        foreach (var command in offer.Phrases())
        {
            Assert.True(
                command.Phrase.Contains("copy", StringComparison.OrdinalIgnoreCase)
                || command.Phrase.Contains("clipboard", StringComparison.OrdinalIgnoreCase),
                $"\"{command.Phrase}\" says neither copy nor clipboard");
        }
    }

    /// <summary>An offer stands until the next answer replaces it, so saying it twice works twice.</summary>
    [Fact]
    public void TheOfferIsNotSpentByBeingTakenUp()
    {
        var offer = new ClipboardOffer();
        offer.Offer("Deciat", "the system");

        Assert.NotEmpty(offer.Phrases());
        Assert.NotEmpty(offer.Phrases());
    }

    /// <summary>
    /// A galaxy service that answers with exactly what a test hands it, and nothing else. Only the
    /// two calls these two searches make are implemented.
    /// </summary>
    private sealed class StubGalaxy : IGalaxyService
    {
        public IReadOnlyList<string> Systems { get; init; } = [];

        public IReadOnlyList<(string Station, string System)> Stations { get; init; } = [];

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult(
                "Sol",
                Systems.Count,
                [.. Systems.Select((name, at) => new SystemSummary
                {
                    Name = name,
                    Distance = at + 1,
                    Population = 5_000_000,
                })]));

        public Task<StationSearchResult> FindStationsAsync(
            StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult(
                "Sol",
                Stations.Count,
                [.. Stations.Select((pair, at) => new StationSummary
                {
                    Name = pair.Station,
                    SystemName = pair.System,
                    Distance = at + 1,
                })]));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("Sol", 0, []));

        // Neither search under test calls these, and answering them with nothing is what makes
        // that visible rather than a stub quietly returning something plausible.
        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new ColonisationScan("Sol", 0, []));
    }
}
