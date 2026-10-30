using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Help;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The help page draws How to use it, then How it works, then Where to go next (#1890).</summary>
public class TheHowToBandLeadsTheHelpPageTests
{
    private static (Window Window, Control Page) Open(string id, PanelNavigator? nav = null, double height = 900)
    {
        var page = HelpPageView.Build(HelpLevel.For(id), nav ?? new PanelNavigator(), _ => { });
        var window = new Window { Content = page, Width = 1280, Height = height };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static List<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void TheStepsComeBeforeThePlates()
    {
        var (window, page) = Open("engineers");
        var shown = Text(page);

        var howTo = HelpLibrary.HowTo("engineers")!;
        var works = HelpLibrary.For("engineers")!;

        var heads = new[] { "HOW TO USE IT", "HOW IT WORKS", "WHERE TO GO NEXT" }.Select(head => shown.IndexOf(head)).ToArray();
        Assert.All(heads, at => Assert.True(at >= 0));
        Assert.Equal(heads.Order(), heads);

        var lastStep = shown.IndexOf(howTo.Sections[^1].Heading);
        var firstPlate = shown.IndexOf(works.Sections[0].Heading);

        Assert.InRange(lastStep, heads[0], heads[1]);
        Assert.InRange(firstPlate, heads[1], heads[2]);

        window.Close();
    }

    [AvaloniaFact]
    public void EveryStepShowsItsSayAndExpectLines()
    {
        var (window, page) = Open("engineers");
        var shown = Text(page);

        var steps = HelpLibrary.HowTo("engineers")!.Sections;
        Assert.Contains(steps, step => step.Say is { Length: > 0 });

        foreach (var step in steps)
        {
            if (step.Say is { Length: > 0 } say)
            {
                Assert.Contains(say, shown);
            }

            if (step.Expect is { Length: > 0 } expect)
            {
                Assert.Contains(expect, shown);
            }
        }

        Assert.Contains("YOU", shown);
        Assert.Contains("THEN", shown);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("about")]
    [InlineData("bookmarks")]
    [InlineData("learned-phrases")]
    public void APageWithOnlyTheHowToBandDrawsOnlyThatHead(string id)
    {
        var (window, page) = Open(id);
        var shown = Text(page);

        Assert.Contains("HOW TO USE IT", shown);
        Assert.DoesNotContain("HOW IT WORKS", shown);
        Assert.DoesNotContain(shown, line => line.StartsWith("There is no short-form help", StringComparison.Ordinal));
        Assert.Contains(HelpLibrary.HowTo(id)!.Sections[0].Heading, shown);

        window.Close();
    }

    [AvaloniaFact]
    public void TheIndexListsTheHowToPagesByTheirHowToIntro()
    {
        var nav = new PanelNavigator();
        var (window, page) = Open(HelpLevel.Index, nav);
        var shown = Text(page);

        foreach (var id in new[] { "about", "bookmarks", "learned-phrases", "mining", "privacy" })
        {
            var howTo = HelpLibrary.HowTo(id)!;

            Assert.Contains(howTo.Title.ToUpperInvariant(), shown);
            Assert.Contains(howTo.Intro, shown);
        }

        window.Close();
    }

    /// <summary>A two-band page and a how-to-only page, in each theme, for checking against the canvas.</summary>
    [Trait("Category", "Integration")]
    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void TheHelpPagesAreCaptured(string themeId)
    {
        var blue = new GuiColourMatrix(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);
        using var look = AppLook.Put(themeId, themeId == ThemeCatalog.ElitePaletteId ? blue : null);

        foreach (var (id, height) in new[] { ("engineers", 4600.0), ("about", 2400.0) })
        {
            var (window, _) = Open(id, height: height);

            using (var frame = window.CaptureRenderedFrame()!)
            {
                frame.SaveCapture($"help-howto-first-{id}-{themeId}.png");
            }

            window.Close();
        }
    }
}
