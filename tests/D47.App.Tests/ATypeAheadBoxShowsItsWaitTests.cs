using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.NameSuggestionReading;

namespace D47.App.Tests;

/// <summary>
/// A type-ahead box opens with the wait mark as soon as the text qualifies, then shows the names, "Nothing
/// matches." or a failure notice, and closes under three characters and on Escape in every state (#2577).
/// </summary>
public sealed class ATypeAheadBoxShowsItsWaitTests
{
    private sealed class Lookup
    {
        public TaskCompletionSource<IReadOnlyList<string>> Answer { get; private set; } = new();

        public Task<IReadOnlyList<string>> Ask(string typed, CancellationToken token)
        {
            Answer = new TaskCompletionSource<IReadOnlyList<string>>();

            return Answer.Task;
        }
    }

    private sealed class Systems : StubStarSystemService, IStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Rig
    {
        public Rig()
        {
            new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

            Box = new TextBox();
            Suggestions = new SystemNameSuggestions<string>(Box, Lookup.Ask, name => name, () => true, _ => { }, TimeSpan.Zero);

            var root = new StackPanel { Children = { Box, Suggestions.List } };
            var window = new Window { Content = root, Width = 400, Height = 300 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public Lookup Lookup { get; } = new();

        public TextBox Box { get; }

        public SystemNameSuggestions<string> Suggestions { get; }

        public bool Open => Suggestions.List.IsVisible;

        public int Buttons => Suggestions.List.GetVisualDescendants().OfType<Button>().Count();

        public string[] Statuses() =>
            [.. Suggestions.List.GetVisualDescendants().OfType<Border>()
                .Where(row => row.Name == SystemNameSuggestions<string>.StatusName)
                .Select(row => string.Join(
                    "|",
                    row.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).Where(text => !string.IsNullOrEmpty(text))))];

        public void Type(string text)
        {
            Box.Text = text;
            Dispatcher.UIThread.RunJobs();
        }

        public void Settle()
        {
            Thread.Sleep(50);
            Dispatcher.UIThread.RunJobs();
        }

        public KeyEventArgs Press(Key key)
        {
            var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key };
            Box.RaiseEvent(args);
            Dispatcher.UIThread.RunJobs();

            return args;
        }
    }

    [AvaloniaFact]
    public void TheThirdCharacterOpensTheBoxWithOnlyTheWaitingRow()
    {
        var rig = new Rig();

        rig.Type("Mo");
        Assert.False(rig.Open);

        rig.Type("Mot");

        Assert.True(rig.Open);
        Assert.Equal(["Retrieving results…"], rig.Statuses());
        Assert.Single(rig.Suggestions.List.GetVisualDescendants().OfType<WaitingState>());
        Assert.Equal(0, rig.Buttons);
    }

    [AvaloniaFact]
    public void NamesReplaceTheWaitingRowAndTypingAgainBringsItBack()
    {
        var rig = new Rig();

        rig.Type("Mot");
        rig.Lookup.Answer.SetResult(["Mother Gaia", "Mother Earth"]);
        rig.Settle();

        Assert.Empty(rig.Statuses());
        Assert.Equal(2, rig.Buttons);

        rig.Type("Moth");

        Assert.Equal(["Retrieving results…"], rig.Statuses());
        Assert.Equal(0, rig.Buttons);
    }

    [AvaloniaFact]
    public void NoNamesShowOneGreyRowThatIsNotAButton()
    {
        var rig = new Rig();

        rig.Type("Zzz");
        rig.Lookup.Answer.SetResult([]);
        rig.Settle();

        Assert.True(rig.Open);
        Assert.Equal(["Nothing matches."], rig.Statuses());
        Assert.Equal(0, rig.Buttons);
        Assert.Empty(rig.Suggestions.Shown);

        var text = rig.Suggestions.List.GetVisualDescendants().OfType<TextBlock>().Single();

        Assert.True(Application.Current!.TryFindResource(ThemeManager.GreyKey, out var grey));
        Assert.Same(grey, text.Foreground);
    }

    [AvaloniaFact]
    public void AThrownLookupShowsTheInlineNoticeAndNothingElse()
    {
        var rig = new Rig();

        rig.Type("Mot");
        rig.Lookup.Answer.SetException(new GalaxyUnavailableException("Spansh refused."));
        rig.Settle();

        Assert.True(rig.Open);
        Assert.Equal("Could not retrieve results.", rig.Suggestions.List.GetVisualDescendants().OfType<Notice>().Single().Text);
        Assert.Empty(rig.Suggestions.List.GetVisualDescendants().OfType<WaitingState>());
    }

    [AvaloniaFact]
    public void ACancelledLookupShowsNothingNew()
    {
        var rig = new Rig();

        rig.Type("Mot");
        rig.Lookup.Answer.SetCanceled();
        rig.Settle();

        Assert.Equal(["Retrieving results…"], rig.Statuses());
    }

    [AvaloniaFact]
    public void DeletingBackToTwoCharactersClosesTheBoxInEveryState()
    {
        var rig = new Rig();

        rig.Type("Mot");
        rig.Type("Mo");
        Assert.False(rig.Open);

        rig.Type("Mot");
        rig.Lookup.Answer.SetResult([]);
        rig.Settle();
        rig.Type("Mo");
        Assert.False(rig.Open);

        rig.Type("Mot");
        rig.Lookup.Answer.SetException(new GalaxyUnavailableException("Spansh refused."));
        rig.Settle();
        rig.Type("Mo");
        Assert.False(rig.Open);
    }

    [AvaloniaFact]
    public void EscapeAndCloseAndSetTextCloseTheBoxInEveryState()
    {
        var rig = new Rig();
        var closers = new Action[] { () => rig.Press(Key.Escape), rig.Suggestions.Close, () => rig.Suggestions.SetText("x") };
        var texts = new[] { "Mot", "Mou", "Mov", "Mow" };

        foreach (var close in closers)
        {
            for (var state = 0; state < texts.Length; state++)
            {
                rig.Type(texts[state]);

                switch (state)
                {
                    case 1:
                        rig.Lookup.Answer.SetResult([]);
                        break;
                    case 2:
                        rig.Lookup.Answer.SetException(new GalaxyUnavailableException("Spansh refused."));
                        break;
                    case 3:
                        rig.Lookup.Answer.SetResult(["Mowgli"]);
                        break;
                }

                rig.Settle();
                Assert.True(rig.Open, $"state {state} was not open");

                close();

                Assert.False(rig.Open, $"state {state} did not close");
            }
        }
    }

    [AvaloniaFact]
    public void EnterOverTheEmptyRowIsLeftToTheField()
    {
        var rig = new Rig();

        rig.Type("Zzz");
        rig.Lookup.Answer.SetResult([]);
        rig.Settle();

        Assert.False(rig.Press(Key.Enter).Handled);
    }

    [AvaloniaFact]
    public void TheDrawnKeyboardTilesGetNoNamesWhileWaiting()
    {
        var rig = new Rig();
        var tiles = rig.Suggestions.NewTiles();
        var answers = new List<string[]>();
        tiles.Answered += names => answers.Add([.. names]);

        _ = tiles.Typed("Mot");
        Assert.Empty(answers);

        _ = tiles.Typed("Mo");
        Assert.Single(answers);
        Assert.Empty(answers[0]);
    }

    [AvaloniaFact]
    public void TheWaitMarkStepsOnTheSystemSearchPage()
    {
        var lookup = new Lookup();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableStarSystem(new StarSystemSurface(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null))
        {
            Searches = new GalaxySearchSurface(CapabilityRegistry.Build([]), new GalaxySearchBoard(), null, () => string.Empty, () => [])
            {
                PrefixNames = (_, _) => Task.FromResult<IReadOnlyList<SystemNameMatch>>([]),
                FactionNames = lookup.Ask,
                SuggestionPause = TimeSpan.Zero,
            },
        });

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(SystemSearchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        panel.Box("controlling faction").Text = "Mot";
        Dispatcher.UIThread.RunJobs();

        var mark = panel.GetVisualDescendants().OfType<WaitingState>().Single(m => m.IsEffectivelyVisible);

        Assert.Contains(mark, WaitingState.ShownUnder(panel));

        var moved = false;

        for (var beat = 0; beat < WaitingState.BeatsPerStep * 2 && !moved; beat++)
        {
            moved = panel.TickWaiting();
        }

        Assert.True(moved, "the mark did not step on the panel tick");
        window.Close();
    }
}
