using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

public class SpellingASystemToPlotTests
{
    private static NavigationSurface Surface(
        RecordingClipboard clipboard,
        bool? confirm,
        List<string>? spelled = null,
        List<string>? offered = null)
    {
        var binds = new EliteBinds
        {
            PresetName = "Test",
            SourceFile = "Test.binds",
            Bindings =
            [
                .. new[]
                {
                    ("GalaxyMapOpen", "Key_M"),
                    ("UI_Up", "Key_W"),
                    ("UI_Select", "Key_Space"),
                    ("UI_Down", "Key_S"),
                    ("CamTranslateRight", "Key_R"),
                    ("CamTranslateLeft", "Key_L"),
                }.Select(e => new EliteBinding(e.Item1, "Primary", "Keyboard", e.Item2)),
            ],
        };

        return new NavigationSurface
        {
            Clipboard = clipboard,
            Actions = new ActionSurface
            {
                Binds = () => binds,
                Status = () => new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = DateTimeOffset.UnixEpoch },
                Input = new RecordingGameInput(),
                Enabled = () => true,
            },
            AutoPlotEnabled = () => true,
            WatchRoute = () => new FixedPlotWatch(confirm),
            AwaitGalaxyMap = (_, _) => Task.FromResult<bool?>(true),
            SpellSystem = spelled is null ? null : spelled.Add,
            OfferSpelling = offered is null ? null : offered.Add,
        };
    }

    private static Task<ToolResult> Plot(NavigationSurface surface, string system) =>
        CapabilityRegistry.Build([NavigationCapability.Create(surface)]).InvokeAsync(
            "plot_course",
            new ToolArguments(new Dictionary<string, string> { ["system"] = system }),
            TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("spell a system")]
    [InlineData("spell the system")]
    [InlineData("spell a destination")]
    public async Task ThePhraseOpensAnEmptyKeyboardWithoutTheModel(string phrase)
    {
        var spelled = new List<string>();
        var registry = CapabilityRegistry.Build(
            [NavigationCapability.Create(Surface(new RecordingClipboard(), null, spelled))]);

        var match = new KeywordRouter(registry).MatchToolCommand(phrase);

        Assert.NotNull(match);
        Assert.Equal("spell_system", match.ToolName);

        await registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.Equal([string.Empty], spelled);
    }

    [Fact]
    public async Task TheModelCannotOpenTheKeyboard()
    {
        var spelled = new List<string>();
        var registry = CapabilityRegistry.Build(
            [NavigationCapability.Create(Surface(new RecordingClipboard(), null, spelled))]);

        var result = await registry.InvokeAsync(
            "spell_system",
            new ToolArguments(new Dictionary<string, string>()),
            TestContext.Current.CancellationToken,
            ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.Empty(spelled);
    }

    [Fact]
    public async Task ASpelledNameIsPlottedExactlyAsSpelled()
    {
        var clipboard = new RecordingClipboard();

        var result = await Plot(Surface(clipboard, confirm: true), "sol");

        Assert.Equal("sol", clipboard.Last);
        Assert.Contains("Course plotted to sol", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APlotWithNoRouteOffersToSpellIt()
    {
        var offered = new List<string>();

        var result = await Plot(Surface(new RecordingClipboard(), confirm: false, offered: offered), "Colonai");

        Assert.EndsWith("Spell it?", result.Content, StringComparison.Ordinal);
        Assert.Equal(["Colonai"], offered);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public async Task APlotThatWorkedOrCannotTellOffersNothing(bool? confirm)
    {
        var offered = new List<string>();

        var result = await Plot(Surface(new RecordingClipboard(), confirm, offered: offered), "Colonia");

        Assert.DoesNotContain("Spell it?", result.Content, StringComparison.Ordinal);
        Assert.Empty(offered);
    }

    [Fact]
    public async Task OpeningTheKeyboardWritesNothingToTheClipboard()
    {
        var clipboard = new RecordingClipboard();
        var registry = CapabilityRegistry.Build(
            [NavigationCapability.Create(Surface(clipboard, null, spelled: []))]);

        await registry.InvokeAsync(
            "spell_system", new ToolArguments(new Dictionary<string, string>()), TestContext.Current.CancellationToken);

        Assert.Empty(clipboard.Written);
    }
}
