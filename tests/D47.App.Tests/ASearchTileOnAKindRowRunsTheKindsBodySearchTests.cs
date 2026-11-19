using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>A star or planet kind's Search tile on the Codex catalogue runs the body search on the kind's fields and the region (#2587).</summary>
[Trait("Category", "Integration")]
public sealed class ASearchTileOnAKindRowRunsTheKindsBodySearchTests
{
    private const int Region = 18;

    private static string Kind(string key, string name, string family, string parent, string subType, string band = "", string terraformable = "", string atmosphere = "", string rings = "", string regions = "18:40") =>
        string.Join('\t', key, name, family, parent, subType, band, terraformable, atmosphere, rings, string.Empty, regions);

    internal static readonly CodexTable Table = CodexTable.Parse(string.Join(
        '\n',
        Kind("star.k_yellow_orange_giant_star.giant", "K (Yellow-Orange giant) Star, giant", "Stars", "star.k_yellow_orange_giant_star", "K (Yellow-Orange giant) Star", band: "giant"),
        Kind("planet.rocky_body.not_terraformable.no_atmosphere", "Rocky body, not terraformable, No atmosphere", "Terrestrial planets", "planet.rocky_body.not_terraformable", "Rocky body", terraformable: "no", atmosphere: "No atmosphere"),
        Kind("gas.class_i_gas_giant.icy_rocky", "Class I gas giant, Icy and Rocky rings", "Gas giant planets", "gas.class_i_gas_giant", "Class I gas giant", rings: "Icy,Rocky"),
        Kind("gas.class_i_gas_giant.no_rings", "Class I gas giant, no rings", "Gas giant planets", "gas.class_i_gas_giant", "Class I gas giant", rings: "None"),
        "1200502\tGreen Class I Gas Giant\tAstronomical Bodies\tGas giant planets\t18:4",
        "1200902\tGreen Class V Gas Giant\tAstronomical Bodies\tGas giant planets\t18:1",
        "2310102\tAleoida Arcus - Green\tBiological and Geological\tOrganic structures\t18:352"));

    private sealed class Galaxy : StubGalaxyService
    {
        public List<BodyQuery> Asked { get; } = [];

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);

            return Task.FromResult(new BodySearchResult("Sol", 0, []));
        }
    }

    private static (Window Window, CodexCataloguePage Page, List<IReadOnlyDictionary<string, string>> Asked) Shown(string key)
    {
        var asked = new List<IReadOnlyDictionary<string, string>>();
        var state = new CommanderGameState(new CommanderIdentity("F735466", "Test Commander"));
        var tree = CodexTree.Of(Table);
        var page = new CodexCataloguePage(
            () => state, () => HistoryState.Done, new CodexCatalogueChoice { Other = Region }, tree.Node(key)!.Parent!, tree: tree, bodySearch: () => new BodySearchLink(asked.Add));
        var window = new Window { Content = page, Width = 1280, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page, asked);
    }

    private static Button? Find(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().SingleOrDefault(button => button.Name == name);

    /// <summary>The query <c>find_body</c> gives the service for what the tile asks.</summary>
    private static async Task<BodyQuery> Request(IReadOnlyDictionary<string, string> arguments)
    {
        var galaxy = new Galaxy();
        var tool = GalaxyCapability.Create(galaxy, () => "Sol", TestSurface.Settings()).Tools.Single(t => t.Name == "find_body");

        var result = await tool.Handler(new ToolArguments(arguments), CancellationToken.None);

        Assert.False(result.IsError, result.Content);

        return Assert.Single(galaxy.Asked);
    }

    private static async Task<(IReadOnlyDictionary<string, string> Sent, BodyQuery Query)> Press(string key)
    {
        var (window, page, asked) = Shown(key);

        Assert.IsType<Button>(Find(page, "CodexSearch." + key)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Close();

        var sent = Assert.Single(asked);

        return (sent, await Request(sent));
    }

    [AvaloniaFact]
    public async Task AGiantRowSendsItsSubtypeAndTheGiantClasses()
    {
        var (sent, query) = await Press("star.k_yellow_orange_giant_star.giant");

        Assert.False(sent.ContainsKey("codex_entry"));
        Assert.Null(query.CodexEntry);
        Assert.Equal("K (Yellow-Orange giant) Star", query.Subtype);
        Assert.Equal(["III", "IIIa", "IIIab", "IIIb"], query.LuminosityClasses);
        Assert.Equal(GalacticRegions.NameOf(Region), query.Region);
        Assert.True(query.NoDistanceLimit);
    }

    [AvaloniaFact]
    public async Task ARockyBodyRowSendsNotTerraformableAndNoAtmosphere()
    {
        var (_, query) = await Press("planet.rocky_body.not_terraformable.no_atmosphere");

        Assert.Null(query.CodexEntry);
        Assert.Equal("Rocky body", query.Subtype);
        Assert.False(query.Terraformable);
        Assert.Equal("No atmosphere", query.Atmosphere);
    }

    [AvaloniaFact]
    public async Task ARingedGasGiantRowSendsBothRingTypes()
    {
        var (_, query) = await Press("gas.class_i_gas_giant.icy_rocky");

        Assert.Null(query.CodexEntry);
        Assert.Equal("Class I gas giant", query.Subtype);
        Assert.Equal(["Icy", "Rocky"], query.RingTypes);
        Assert.Equal(["Icy", "Rocky"], query.ExactRings!.Order(StringComparer.Ordinal));
    }

    [AvaloniaFact]
    public async Task AnUnringedGasGiantRowKeepsOnlyUnringedBodies()
    {
        var (sent, query) = await Press("gas.class_i_gas_giant.no_rings");

        Assert.Equal("None", sent["ring_type"]);
        Assert.Empty(query.RingTypes);
        Assert.Empty(query.ExactRings!);
    }

    [AvaloniaFact]
    public async Task ACanonnRowStillSendsItsCodexEntry()
    {
        var (sent, query) = await Press("2310102");

        Assert.Equal("Aleoida Arcus - Green", sent["codex_entry"]);
        Assert.Equal("Aleoida Arcus", query.CodexEntry);
    }

    [AvaloniaFact]
    public void AGreenGasGiantTheServiceDoesNotRecordHasNoTile()
    {
        var (window, page, _) = Shown("1200502");

        Assert.NotNull(Find(page, "CodexSearch.1200502"));
        Assert.Null(Find(page, "CodexSearch.1200902"));

        window.Close();
    }
}
