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

    private static Board Open(bool suggest = true, bool enabled = true)
    {
        var asked = new List<string>();
        var picked = new List<string>();
        var on = new[] { enabled };
        var box = new TextBox();

        if (suggest)
        {
            _ = new SystemNameSuggestions(
                box,
                (typed, _) =>
                {
                    lock (asked)
                    {
                        asked.Add(typed);
                    }

                    return Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                        [.. Names
                            .Where(n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                            .Select(n => new SystemNameMatch(n, n.Length, new StarPosition(0, 0, 0)))]);
                },
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

    [AvaloniaFact]
    public void DeletingBelowThreeCharactersEmptiesTheRow()
    {
        var board = Open();

        Spell(board, "dia");
        board.Settle(() => board.Tiles.Length > 0);

        board.Press("delete");
        board.Settle(() => board.Tiles.Length == 0);

        Assert.Empty(board.Tiles);
    }

    [AvaloniaFact]
    public void ClearEmptiesTheRow()
    {
        var board = Open();

        Spell(board, "dia");
        board.Settle(() => board.Tiles.Length > 0);

        board.Press("clear");
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
}
