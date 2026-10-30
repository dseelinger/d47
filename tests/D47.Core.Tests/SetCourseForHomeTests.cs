using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests;

public class SetCourseForHomeTests
{
    private const string Home = "Kestrel Reach";

    private static DynamicCommand[] Commands(string? home) => [.. HomeCourse.Phrases(() => home)];

    [Theory]
    [InlineData("set course for home")]
    [InlineData("set course home")]
    [InlineData("set a course for home")]
    [InlineData("plot a course home")]
    [InlineData("plot a course to home")]
    [InlineData("take me home")]
    [InlineData("take us home")]
    [InlineData("navigate home")]
    [InlineData("route home")]
    [InlineData("plot a route home")]
    public void TheRouterPlotsTheHomeSystemWithoutTheModel(string phrase)
    {
        var registry = CapabilityRegistry.Build([NavigationCapability.Create(Surface(Home, new RecordingClipboard(), new RecordingGameInput()))]);
        var router = new KeywordRouter(registry, () => Commands(Home));

        var match = router.MatchToolCommand(phrase);

        Assert.NotNull(match);
        Assert.Equal("plot_course", match.ToolName);
        Assert.True(match.Arguments.TryGetString("system", out var system));
        Assert.Equal(Home, system);
    }

    [Theory]
    [InlineData("home")]
    [InlineData("  HOME ")]
    [InlineData("Home")]
    public async Task PlotCourseReadsTheWordHomeAsTheHomeSystem(string spoken)
    {
        var clipboard = new RecordingClipboard();
        var result = await Plot(spoken, Surface(Home, clipboard, new RecordingGameInput()));

        Assert.Equal(Home, clipboard.Last);
        Assert.Contains(Home, result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task WithNoHomeSystemNothingIsPlottedAndTheCommanderIsToldSo(string? home)
    {
        var clipboard = new RecordingClipboard();
        var input = new RecordingGameInput();

        var result = await Plot("home", Surface(home, clipboard, input));

        Assert.True(result.IsError);
        Assert.Equal(HomeCourse.NotSet, result.Content);
        Assert.Empty(clipboard.Written);
        Assert.Empty(input.Steps);
    }

    [Fact]
    public async Task ASystemNamedSomethingElseIsLeftAlone()
    {
        var clipboard = new RecordingClipboard();

        await Plot("Sol", Surface(Home, clipboard, new RecordingGameInput()));

        Assert.Equal("Sol", clipboard.Last);
    }

    [Fact]
    public void SwitchingCommandersChangesWhichSystemThePhrasesPlot()
    {
        var current = "Kestrel Reach";

        Assert.All(HomeCourse.Phrases(() => current), command => Assert.Equal("Kestrel Reach", command.Arguments["system"]));

        current = "Deciat";

        Assert.All(HomeCourse.Phrases(() => current), command => Assert.Equal("Deciat", command.Arguments["system"]));
    }

    [Theory]
    [InlineData(Home)]
    [InlineData(null)]
    public void ABookmarkCannotBeCalledHome(string? home)
    {
        var taken = Commands(home).Select(command => command.Phrase).ToArray();

        Assert.NotNull(BookmarkValidation.Problem("home", [], taken));
        Assert.NotNull(BookmarkValidation.Problem(" Home ", [], []));
    }

    private static NavigationSurface Surface(string? home, RecordingClipboard clipboard, RecordingGameInput input) => new()
    {
        Clipboard = clipboard,
        Actions = new ActionSurface
        {
            Binds = () => new EliteBinds { PresetName = "Test", SourceFile = "Test.binds", Bindings = [] },
            Status = () => new D47.Core.Journal.GameStatus { ReadAt = DateTimeOffset.UnixEpoch },
            Input = input,
            Enabled = () => false,
        },
        AutoPlotEnabled = () => false,
        WatchRoute = () => new FixedPlotWatch(null),
        AwaitGalaxyMap = (_, _) => Task.FromResult<bool?>(null),
        HomeSystem = () => home,
    };

    private static Task<ToolResult> Plot(string system, NavigationSurface surface) =>
        NavigationCapability.Plot(
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = system }),
            surface,
            TestContext.Current.CancellationToken);
}
