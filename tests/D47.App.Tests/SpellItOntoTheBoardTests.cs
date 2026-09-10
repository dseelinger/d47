using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>A value said onto a drawn keyboard, key by key, in one breath (#51).</summary>
public class SpellItOntoTheBoardTests
{
    private static readonly PixelSize Quad = new(1024, 640);

    /// <summary>The headset's board, opened the way a ray press on a text box opens it.</summary>
    private static (OffscreenSurface Surface, TextBox Box) Headset(string initial = "")
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        var surface = new OffscreenSurface(view, Quad);
        var box = new TextBox { Text = initial };

        surface.Type(box);
        surface.Render(view.KeepUp);

        return (surface, box);
    }

    /// <summary>What the board is showing, which is not the box it will write into.</summary>
    private static string Showing(OffscreenSurface surface) =>
        surface.View.GetVisualDescendants().OfType<TextBox>().First(box => box.IsReadOnly).Text
        ?? string.Empty;

    private static IEnumerable<string> Lines(Visual from) =>
        from.GetVisualDescendants().OfType<TextBlock>().Select(line => line.Text ?? string.Empty);

    /// <summary>The board hears nothing until it is up, and stops when it goes away.</summary>
    [AvaloniaFact]
    public void TheHeadsetsBoardListensOnlyWhileItIsUp()
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, Quad);

        Assert.False(surface.IsListening);

        surface.Type(new TextBox());
        Assert.True(surface.IsListening);

        surface.Dismiss();
        Assert.False(surface.IsListening);
    }

    /// <summary>Spelled and committed in one breath, and the box is written once.</summary>
    [AvaloniaFact]
    public void AlphaBravoSevenDoneWritesTheValueOnceOnDone()
    {
        var (surface, box) = Headset();

        using (surface)
        {
            surface.Hear(new Heard("alpha bravo seven", 1, Final: true));

            // Pressed onto the board, and nowhere near the box it will write into.
            Assert.Equal("ab7", Showing(surface));
            Assert.Equal(string.Empty, box.Text);

            surface.Hear(new Heard("done", 1, Final: true));

            Assert.Equal("ab7", box.Text);
            Assert.False(surface.IsListening);
        }
    }

    /// <summary>And the whole of it in one utterance does the same thing.</summary>
    [AvaloniaFact]
    public void TheWholeThingInOneBreathDoesTheSame()
    {
        var (surface, box) = Headset();

        using (surface)
        {
            surface.Hear(new Heard("Alpha bravo seven done.", 1, Final: true));

            Assert.Equal("ab7", box.Text);
            Assert.False(surface.IsListening);
        }
    }

    /// <summary>Anything that is not spelling is the value, which is what gives this board dictation.</summary>
    [AvaloniaFact]
    public void AlphaCentauriLandsInTheFieldWhole()
    {
        var (surface, box) = Headset();

        using (surface)
        {
            surface.Hear(new Heard("Alpha Centauri", 1, Final: true));

            Assert.Equal("Alpha Centauri", Showing(surface));

            // Still nothing committed: the board writes on Done and at no other time.
            Assert.Equal(string.Empty, box.Text);
            Assert.Contains(Lines(surface.View), line => line.Contains("Centauri", StringComparison.Ordinal));
        }
    }

    /// <summary>One word off the table presses nothing at all — not the words in front of it either.</summary>
    [AvaloniaFact]
    public void AnUtteranceWithOneUnknownWordPressesNothingAndNamesTheWord()
    {
        var (surface, _) = Headset();

        using (surface)
        {
            surface.Hear(new Heard("alpha bravo zog", 1, Final: true));

            Assert.Equal("alpha bravo zog", Showing(surface));
            Assert.Contains(Lines(surface.View), line => line.Contains("“zog”", StringComparison.Ordinal));
        }
    }

    /// <summary>Below the bar nothing is pressed and the board says so.</summary>
    [AvaloniaFact]
    public void AnUtteranceHeardBadlyPressesNothing()
    {
        var (surface, box) = Headset();

        using (surface)
        {
            surface.Hear(new Heard("alpha bravo", TextEntryLoop.ConfidentEnough - 0.01, Final: true));

            Assert.Equal(string.Empty, Showing(surface));
            Assert.Equal(string.Empty, box.Text);
            Assert.Contains(
                Lines(surface.View),
                line => line == TextEntryLoop.Explain(EntryFallback.LowConfidence, null));
        }
    }

    /// <summary>Delete and clear are keys like any other.</summary>
    [AvaloniaFact]
    public void DeleteAndClearAreSaidLikeAnyOtherKey()
    {
        var (surface, _) = Headset();

        using (surface)
        {
            surface.Hear(new Heard("alpha bravo charlie delete", 1, Final: true));
            Assert.Equal("ab", Showing(surface));

            surface.Hear(new Heard("clear delta", 1, Final: true));
            Assert.Equal("d", Showing(surface));
        }
    }

    /// <summary>Cancel commits nothing, which is the whole of what it is for.</summary>
    [AvaloniaFact]
    public void CancelPutsTheBoardAwayWithoutWritingAnything()
    {
        var (surface, box) = Headset("deciat");

        using (surface)
        {
            surface.Hear(new Heard("alpha cancel", 1, Final: true));

            Assert.Equal("deciat", box.Text);
            Assert.False(surface.IsListening);
        }
    }

    /// <summary>The prompt-layer board, drawn by the panel rather than by the headset.</summary>
    private static (PanelPrompts Prompts, Control Page, List<string> Committed) Prompted()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("root", "Root"));

        var layer = new Avalonia.Controls.Panel();
        var prompts = new PanelPrompts(nav, layer);
        var committed = new List<string>();

        prompts.Enter(
            new EntryRequest(
                "entry",
                "Entry",
                "System name",
                Context: null,
                Initial: string.Empty,

                // The keys drawn, which is the state a failed hearing leaves the page in.
                EntrySurface.Keyboard),
            committed.Add);

        var page = prompts.Build(nav.Trail[^1])!;

        new Window
        {
            Content = new Avalonia.Controls.Panel { Children = { page, layer } },
            Width = 1400,
            Height = 900,
        }.Show();

        Dispatcher.UIThread.RunJobs();

        return (prompts, page, committed);
    }

    /// <summary>With the keys drawn, the panel's own board takes speech too.</summary>
    [AvaloniaFact]
    public void ThePromptLayerBoardListensWhileItsKeysAreDrawn()
    {
        var (prompts, _, _) = Prompted();

        Assert.True(prompts.IsListening);
    }

    /// <summary>The same two utterances, on the other board.</summary>
    [AvaloniaFact]
    public void ThePromptLayerBoardSpellsAndCommitsInOneBreath()
    {
        var (prompts, page, committed) = Prompted();

        prompts.Hear(new Heard("alpha bravo seven", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(committed);
        Assert.Equal("ab7", page.GetVisualDescendants().OfType<TextBox>().First().Text);

        prompts.Hear(new Heard("done", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["ab7"], committed);
        Assert.False(prompts.IsListening);
    }

    /// <summary>And a value that is not spelling arrives whole, uncommitted.</summary>
    [AvaloniaFact]
    public void ThePromptLayerBoardTakesAlphaCentauriWhole()
    {
        var (prompts, page, committed) = Prompted();

        prompts.Hear(new Heard("Alpha Centauri", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(committed);
        Assert.Equal("Alpha Centauri", page.GetVisualDescendants().OfType<TextBox>().First().Text);
        Assert.Contains(Lines(page), line => line.Contains("“Centauri”", StringComparison.Ordinal));
    }

    /// <summary>
    /// The invariant: a spelled key reaches a drawn board and nothing else. The day this list grows a
    /// file that also sends keys to Elite, spelling has become a macro path.
    /// </summary>
    [Fact]
    public void SpelledPressesReachDrawnBoardsAndNothingElse()
    {
        var readers = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("Spelling.Hear", StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["OffscreenSurface.cs", "PanelPrompts.cs"], readers);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }

    /// <summary>The vocabulary and the drawn key table are one thing (#51).</summary>
    [Fact]
    public void EveryDrawnKeyHasAWordAndEveryWordHasADrawnKey()
    {
        var drawn = string.Concat(PanelPrompts.Keys).ToHashSet();

        Assert.Equal(drawn, Spelling.Characters.ToHashSet());

        foreach (var key in drawn)
        {
            Assert.Equal([SpelledKey.Types(key)], Spelling.Parse(
                Spelling.Alphabet.GetValueOrDefault(key)
                ?? Spelling.Figures.GetValueOrDefault(key)
                ?? Spelling.Marks[key]).Keys);
        }
    }
}
