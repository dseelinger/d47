using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The drawn keyboard lists system names for a box that carries suggestions (#1088).</summary>
[Trait("Category", "Integration")]
public sealed class TheHeadsetKeyboardSuggestsSystemNamesTests
{
    private static readonly PixelSize Quad = new(1024, 900);

    private static readonly string[] Names = ["Diaguandri", "Dian", "Diabozo", "Diadem"];

    private sealed class Board
    {
        public required OffscreenSurface Surface { get; init; }
        public required TextBox Box { get; init; }
        public required List<string> Asked { get; init; }
        public required List<string> Picked { get; init; }
        public required bool[] Enabled { get; init; }

        public IEnumerable<Button> Buttons => Surface.View.GetVisualDescendants().OfType<Button>();

        public string[] Tiles =>
            [.. Buttons.Where(b => b.Content is string name && Names.Contains(name)).Select(b => (string)b.Content!)];

        public void Press(string label)
        {
            Buttons.First(b => Equals(b.Content, label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        public void Settle(Func<bool> done)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);

            while (!done())
            {
                Assert.True(DateTime.UtcNow < deadline, "the board did not settle in time");
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
        }
    }

    private static Board Open(
        bool suggest = true,
        bool enabled = true,
        Func<string, CancellationToken, Task<IReadOnlyList<SystemNameMatch>>>? lookup = null)
    {
        var asked = new List<string>();
        var picked = new List<string>();
        var on = new[] { enabled };
        var box = new TextBox();

        if (suggest)
        {
            _ = new SystemNameSuggestions<SystemNameMatch>(
                box,
                lookup ?? ((typed, _) =>
                {
                    lock (asked)
                    {
                        asked.Add(typed);
                    }

                    return Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                        [.. Names
                            .Where(n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                            .Select(n => new SystemNameMatch(n, n.Length, new StarPosition(0, 0, 0)))]);
                }),
                match => match.Name,
                () => on[0],
                match =>
                {
                    picked.Add(match.Name);
                    box.Text = match.Name;
                },
                TimeSpan.Zero);
        }

        var view = new PanelView { DataContext = new PanelViewModel() };
        var surface = new OffscreenSurface(view, Quad);

        surface.Type(box);
        surface.Render(view.KeepUp);

        return new Board { Surface = surface, Box = box, Asked = asked, Picked = picked, Enabled = on };
    }

    private static void Spell(Board board, string text)
    {
        foreach (var key in text)
        {
            board.Press(key.ToString());
        }
    }

    [AvaloniaFact]
    public void TypingDiaOnTheDrawnKeyboardShowsTilesForDiaguandriAndDian()
    {
        var board = Open();

        Spell(board, "dia");
        board.Settle(() => board.Tiles.Length > 0);

        Assert.Contains("Diaguandri", board.Tiles);
        Assert.Contains("Dian", board.Tiles);
    }

    [AvaloniaFact]
    public void EachTileIsAtLeastAsTallAsTheMinimumTarget()
    {
        var board = Open();

        Spell(board, "dia");
        board.Settle(() => board.Tiles.Length > 0);

        var tiles = board.Buttons.Where(b => b.Content is string name && Names.Contains(name));

        Assert.All(tiles, tile => Assert.True(tile.Height >= TypeScale.MinimumTarget));
    }

    [AvaloniaTheory]
    [InlineData("delete")]
    [InlineData("clear")]
    public void TheRowEmptiesWhenAKeyLeavesUnderThreeCharacters(string key)
    {
        var board = Open();

        Spell(board, "dia");
        board.Settle(() => board.Tiles.Length > 0);

        board.Press(key);
        board.Settle(() => board.Tiles.Length == 0);

        Assert.Empty(board.Tiles);
    }

    [AvaloniaFact]
    public void ADictatedWordRefreshesTheRow()
    {
        var board = Open();

        board.Surface.Hear(new Heard("diaguandri", 1, Final: true));
        board.Settle(() => board.Tiles.Length > 0);

        Assert.Contains("Diaguandri", board.Tiles);
    }

    [AvaloniaFact]
    public void PressingATileWritesTheNameClosesTheKeyboardAndRunsThePick()
    {
        var board = Open();

        Spell(board, "dia");
        board.Settle(() => board.Tiles.Length > 0);

        board.Press("Diaguandri");

        Assert.Equal("Diaguandri", board.Box.Text);
        Assert.Equal(["Diaguandri"], board.Picked);
        Assert.False(board.Surface.IsListening);
    }

    [AvaloniaFact]
    public void ABoxWithoutSuggestionsDrawsNoTilesAndAsksNothing()
    {
        var board = Open(suggest: false);

        Spell(board, "dia");
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(board.Tiles);
        Assert.Empty(board.Asked);
    }

    [AvaloniaFact]
    public void WithGalaxySearchOffNoRowIsDrawnAndNothingIsSent()
    {
        var board = Open(enabled: false);

        Spell(board, "dia");
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(board.Tiles);
        Assert.Empty(board.Asked);
    }

    private static Func<string, CancellationToken, Task<IReadOnlyList<SystemNameMatch>>> Gated(
        TaskCompletionSource<IReadOnlyList<SystemNameMatch>> gate) => (_, _) => gate.Task;

    // The surface's window is never shown, so IsEffectivelyVisible is false everywhere; walk IsVisible below it instead.
    private static bool IsShown(Visual visual) =>
        visual.GetSelfAndVisualAncestors().OfType<Control>().TakeWhile(c => c is not Window).All(c => c.IsVisible);

    private static bool Shows(Board board, string text) =>
        board.Surface.View.GetVisualDescendants().OfType<TextBlock>().Any(t => IsShown(t) && t.Text == text);

    private static bool MarkShown(Board board) =>
        board.Surface.View.GetVisualDescendants().OfType<WaitingState>().Any(m => IsShown(m));

    [AvaloniaFact]
    public void ATooShortTextShowsNoStrip()
    {
        var board = Open(lookup: Gated(new()));

        Spell(board, "di");

        Assert.False(MarkShown(board));
        Assert.False(Shows(board, "Retrieving results…"));
    }

    [AvaloniaFact]
    public void TheThirdCharacterShowsTheWaitMarkWithRetrievingResults()
    {
        var board = Open(lookup: Gated(new()));

        Spell(board, "dia");

        Assert.True(MarkShown(board));
        Assert.True(Shows(board, "Retrieving results…"));
        Assert.Empty(board.Tiles);
    }

    [AvaloniaFact]
    public void TheWaitMarkStepsOnTheDrawnKeyboardsTick()
    {
        var board = Open(lookup: Gated(new()));

        Spell(board, "dia");

        var moved = false;

        for (var beat = 0; beat < WaitingState.BeatsPerStep * 2 && !moved; beat++)
        {
            moved = board.Surface.TickWaiting();
        }

        Assert.True(moved, "the mark did not step on the keyboard's tick");
    }

    [AvaloniaFact]
    public void AStepOfTheWaitMarkMarksTheHeadsetPanelSurfaceDirty()
    {
        var (settings, _, _) = TestSurface.Create();
        var surface = new D47.App.Headset.VrPanelSurface(new PanelViewModel(), settings, _ => null);
        var box = new TextBox();

        _ = new SystemNameSuggestions<SystemNameMatch>(
            box, Gated(new()), match => match.Name, () => true, _ => { }, TimeSpan.Zero);

        surface.Board.Type(box);

        foreach (var key in "dia")
        {
            surface.Board.View.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, key.ToString()))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        Dispatcher.UIThread.RunJobs();

        var (width, height) = surface.Size;
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(width * height * 4);

        try
        {
            surface.Draw(buffer, width * 4);
            Assert.False(surface.IsDirty);

            for (var beat = 0; beat < WaitingState.BeatsPerStep * 2 && !surface.IsDirty; beat++)
            {
                surface.TickWaiting();
            }

            Assert.True(surface.IsDirty, "a step of the mark left the surface clean");
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }

    [AvaloniaFact]
    public void NamesReplaceTheWaitMarkWithTiles()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<SystemNameMatch>>();
        var board = Open(lookup: Gated(gate));

        Spell(board, "dia");
        gate.SetResult([new SystemNameMatch("Dian", 4, new StarPosition(0, 0, 0))]);
        board.Settle(() => board.Tiles.Length > 0);

        Assert.Equal(["Dian"], board.Tiles);
        Assert.False(MarkShown(board));
    }

    [AvaloniaFact]
    public void AnEmptyAnswerShowsNothingMatchesUntilTheTextChanges()
    {
        var board = Open(lookup: (_, _) => Task.FromResult<IReadOnlyList<SystemNameMatch>>([]));

        Spell(board, "zzz");
        board.Settle(() => Shows(board, "Nothing matches."));

        Assert.Empty(board.Tiles);

        board.Press("delete");
        board.Press("delete");
        board.Settle(() => !Shows(board, "Nothing matches."));
    }

    [AvaloniaFact]
    public void AFailedLookupShowsTheInlineNoticeUntilTheTextChanges()
    {
        var board = Open(lookup: (_, _) => Task.FromException<IReadOnlyList<SystemNameMatch>>(new InvalidOperationException()));

        Spell(board, "dia");
        board.Settle(() => Shows(board, "Could not retrieve results."));

        Assert.Contains(board.Surface.View.GetVisualDescendants().OfType<Notice>(), n => IsShown(n));
        Assert.Empty(board.Tiles);

        board.Press("delete");
        board.Press("delete");
        board.Settle(() => !Shows(board, "Could not retrieve results."));
    }
}
