using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every bubble but the panel's own note heads itself with who spoke, a source tag when the line came
/// from a callout, and the time it was said (#276).
/// </summary>
public class TheBubbleHeadNamesWhoSpokeTests
{
    private static IReadOnlyList<Control> Turns(PanelView panel) =>
        [.. panel.GetControl<StackPanel>("Bubbles").Children];

    /// <summary>The head's parts in reading order: the name, any tags, then the time.</summary>
    private static IReadOnlyList<Control> Head(Control turn)
    {
        var head = (Grid)((StackPanel)((Border)turn).Child!).Children[0];

        return [.. ((WrapPanel)head.Children[0]).Children, head.Children[1]];
    }

    private static string? Said(Control chipOrTag) => chipOrTag switch
    {
        TextBlock block => block.Text,
        Border border => Said((Control)border.Child!),
        D47.App.Theming.BloomStack stack => Said(stack.Child!),
        _ => null,
    };

    private static PanelView Laid(PanelViewModel model)
    {
        var panel = new PanelView { DataContext = model };
        var window = new Window { Width = 900, Height = 560, Content = panel };
        window.Show();

        var bounds = new Rect(0, 0, 900, 560);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return panel;
    }

    [AvaloniaFact]
    public void ACalloutsHeadCarriesItsSpeakerTagAndTime()
    {
        var model = new PanelViewModel();
        model.Append("Sacred Fire clear. Safe flying, Commander.", speaker: "Tower", sourceKey: "carrier.departure");

        var head = Head(Turns(Laid(model))[0]);

        Assert.Equal(3, head.Count);
        Assert.Equal("TOWER", Said(head[0]));
        Assert.Equal("carrier.departure", Said(head[1]));
        Assert.Matches(@"^\d{2}:\d{2}$", Said(head[2]));
    }

    /// <summary>A line with no callout behind it carries no tag — the head has nothing to name.</summary>
    [AvaloniaFact]
    public void APlainShipLineNamesD47AndCarriesNoTag()
    {
        var model = new PanelViewModel();
        model.Append("Standing by, Commander.");

        var head = Head(Turns(Laid(model))[0]);

        Assert.Equal(2, head.Count);
        Assert.Equal("D47", Said(head[0]));
    }

    [AvaloniaFact]
    public void TheCommandersLineIsNamedCMDR()
    {
        var model = new PanelViewModel();
        model.Append("where am I", voice: TranscriptVoice.Commander);

        var head = Head(Turns(Laid(model))[0]);

        Assert.Equal("CMDR", Said(head[0]));
    }

    /// <summary>
    /// The merge key a run joins on has to include speaker and source, or two callouts spoken back to
    /// back in the ship's own voice would read as one bubble.
    /// </summary>
    [AvaloniaFact]
    public void TwoCalloutsInTheShipsVoiceStayTwoBubblesWhenTheirSourceDiffers()
    {
        var model = new PanelViewModel();
        model.Append("Fuel scoop advised.", sourceKey: "fuel.low");
        model.Append("Danger, shields down.", sourceKey: "danger.shields");

        Assert.Equal(2, Turns(Laid(model)).Count);
    }

    /// <summary>The persona's name heads the turn; the text itself carries no bracketed prefix.</summary>
    [AvaloniaFact]
    public void APersonasReplyIsHeadedWithTheirNameAndNoBracketedPrefix()
    {
        var model = new PanelViewModel();
        model.Append("On it, Commander.", speaker: "Cora");

        var turn = Turns(Laid(model))[0];
        var block = (SelectableTextBlock)((StackPanel)((Border)turn).Child!).Children[1];

        var said = string.Concat(
            block.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text));

        Assert.Equal("On it, Commander.", said);
        Assert.Equal("CORA", Said(Head(turn)[0]));
    }
}
