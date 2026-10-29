using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Adventures;
using D47.Core.Stories;
using Xunit;

namespace D47.App.Tests;

/// <summary>The next-objective strip says the sentence, the system and the story, in one line, whatever the width.</summary>
public class TheNextObjectiveStripIsOneLineTests
{
    private const string Key = "the-lantern-route";

    private static readonly DateTimeOffset Accepted = new(2026, 8, 22, 19, 0, 0, TimeSpan.Zero);

    private static ShownObjective Shown(AdventureTrigger trigger, bool paused = false)
    {
        var surface = AdventureFixture.Surface();

        Assert.Null(surface.Book.Write("F1", new Adventure
        {
            Key = Key,
            Name = "The Salt Road",
            Source = AdventureSource.Generated,
            AcceptedAt = Accepted,
            Beats = [new AdventureBeat { Title = "Beat", Trigger = trigger, Line = "Done." }],
        }));
        surface.Book.CatchUp([]);

        return new ShownObjective(surface.Book.Standing("F1", Key)!, "The Salt Road", paused);
    }

    private static AdventureTrigger Arrive(string system) =>
        new() { Kind = TriggerKind.Arrive, SystemAddress = 1, System = system };

    [Fact]
    public void TheSentenceIsSplitAroundItsSystemWithNoClosingStop()
    {
        var text = ObjectiveStrip.Describe(Shown(Arrive("Shinrarta Dezhra")), "Sol", rewriting: false)!;

        Assert.Equal("NEXT", text.Label);
        Assert.Equal("Shinrarta Dezhra", text.System);
        Assert.Equal("Shinrarta Dezhra", text.CopyValue);
        Assert.False(text.InSystem);
        Assert.False(text.Grey);
        Assert.Equal("THE SALT ROAD", text.StoryName);
        Assert.EndsWith(" ", text.Before);
        Assert.Null(text.After);
    }

    [Fact]
    public void TheSystemTurnsCyanWhereTheCommanderIs()
    {
        var text = ObjectiveStrip.Describe(Shown(Arrive("Shinrarta Dezhra")), "shinrarta dezhra", rewriting: false)!;

        Assert.True(text.InSystem);
    }

    [Fact]
    public void ARankObjectiveHasNoSystemAndNoGlyph()
    {
        var text = ObjectiveStrip.Describe(
            Shown(new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Explore", Rank = 6 }), "Sol", rewriting: false)!;

        Assert.Null(text.System);
        Assert.Null(text.CopyValue);
    }

    [Fact]
    public void APausedStoryHasNoStrip()
    {
        var paused = Shown(Arrive("Shinrarta Dezhra"), paused: true);

        Assert.Null(ObjectiveStrip.Describe(paused, "Sol", rewriting: false));
        Assert.Null(ObjectiveStrip.Describe(paused, "Sol", rewriting: true));
    }

    [Fact]
    public void WhileTheObjectiveIsRewrittenTheStripSaysSoInGreyWithNoSystem()
    {
        var text = ObjectiveStrip.Describe(Shown(Arrive("Shinrarta Dezhra")), "Sol", rewriting: true)!;

        Assert.Equal("Writing a different objective…", text.Before);
        Assert.True(text.Grey);
        Assert.Null(text.System);
    }

    [Fact]
    public void WithNoObjectiveThereIsNoStrip()
    {
        Assert.Null(ObjectiveStrip.Describe(null, "Sol", rewriting: false));
    }

    [AvaloniaFact]
    public void ALongSentenceIsCutWhileTheSystemAndGlyphStayWhole()
    {
        var strip = new ObjectiveStrip(AdventureFixture.Surface(), () => (_ => Task.FromResult(true)));
        var text = new ObjectiveStripText(
            "NEXT",
            "Analyse four organic samples of Bacterium Informem on Pipe (stem) Sector ZE-A d89 A 2 in ",
            "Pipe (stem) Sector ZE-A d89",
            "Pipe (stem) Sector ZE-A d89",
            null,
            false,
            false,
            "THE SALT ROAD");

        var window = new Window { Width = 820, Height = 200, Content = strip.Build(text) };
        window.Show();

        var blocks = window.GetVisualDescendants().OfType<TextBlock>().ToList();
        var sentence = blocks.Single(block => block.Text!.StartsWith("Analyse", StringComparison.Ordinal));
        var system = blocks.Single(block => block.Text == "Pipe (stem) Sector ZE-A d89");
        var glyph = Assert.Single(window.GetVisualDescendants().OfType<Button>());
        var right = glyph.TranslatePoint(new Point(glyph.Bounds.Width, 0), window)!.Value.X;

        Assert.True(sentence.Bounds.Width <= 820 - system.Bounds.Width - glyph.Bounds.Width);
        Assert.True(system.Bounds.Width >= system.DesiredSize.Width);
        Assert.Equal(44, glyph.Bounds.Height);
        Assert.InRange(right, 0, 820);
    }
}
