using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Listening;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Transcript's footer, search field and turn heads, restyled to the reference (#430).</summary>
public class TheTranscriptFooterLinesUpWithTheTurnsTests
{
    private static PanelViewModel Conversation()
    {
        var model = new PanelViewModel { Microphone = MicrophoneState.Idle };

        model.Append("where is the nearest scoopable star", voice: TranscriptVoice.Commander);
        model.Append("[calm] Arque is two jumps out, a K class, scoopable.");
        model.Append("Holding at Fixture Anchorage.", sourceKey: "docking.granted");

        return model;
    }

    private static PanelView Laid(
        PanelViewModel model,
        double width = 1280,
        double height = 860,
        string? commander = null,
        Func<decimal>? session = null)
    {
        var panel = new PanelView { DataContext = model };

        panel.EnableSearch();
        panel.EnableTurnDetails(() => Task.CompletedTask, session ?? (() => 0.0231m));
        panel.EnableCommanderName(() => commander);

        var window = new Window { Width = width, Height = height, Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static IReadOnlyList<Border> Turns(PanelView panel) =>
        [.. panel.GetControl<StackPanel>("Bubbles").Children.OfType<Border>()];

    private static Grid Head(Border turn) => (Grid)((StackPanel)turn.Child!).Children[0];

    private static double LeftOf(Control control, Visual root) => control.TranslatePoint(default, root)!.Value.X;

    private static Color Resource(Control on, string key) =>
        ((ISolidColorBrush)on.FindResource(key)!).Color;

    [AvaloniaFact]
    public void TheFooterStartsAtTheTurnsLeftEdge()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite);
        var panel = Laid(Conversation());

        var ship = LeftOf(Turns(panel)[1], panel);
        var dot = LeftOf(panel.GetControl<Avalonia.Controls.Shapes.Ellipse>("MicrophoneGlyph"), panel);
        var ask = LeftOf(panel.GetControl<TextBox>("AskBox"), panel);
        var bar = LeftOf(panel.GetControl<DockPanel>("PageBar"), panel);

        Assert.InRange(dot - ship, -1, 1);
        Assert.InRange(ask - ship, -1, 1);
        Assert.InRange(bar - ship, -1, 1);
    }

    [AvaloniaTheory]
    [InlineData(TranscriptVoice.Commander)]
    [InlineData(TranscriptVoice.Ship)]
    public void TheTimeFollowsTheLastTagAtAFixedGap(TranscriptVoice voice)
    {
        using var look = AppLook.Put(ThemeCatalog.Elite);
        var panel = Laid(Conversation(), width: 1600);

        var turn = Turns(panel).First(border =>
            voice == TranscriptVoice.Commander
                ? border.HorizontalAlignment == Avalonia.Layout.HorizontalAlignment.Right
                : border.HorizontalAlignment == Avalonia.Layout.HorizontalAlignment.Left);
        var head = Head(turn);
        var last = ((WrapPanel)head.Children[0]).Children[^1];
        var time = head.Children[1];

        var gap = LeftOf(time, head) - (LeftOf(last, head) + last.Bounds.Width);

        Assert.InRange(gap, PanelView.HeadTimeGap - 1, PanelView.HeadTimeGap + 1);
    }

    [AvaloniaFact]
    public void ACommandersTurnCarriesTheirNameAndCopyDoesNot()
    {
        var model = Conversation();
        var panel = Laid(model, commander: "John Deparagon");

        var name = (TextBlock)((WrapPanel)Head(Turns(panel)[0]).Children[0]).Children[0];

        Assert.Equal("CMDR JOHN DEPARAGON", name.Text);
        Assert.Equal("CMDR", model.Segments(TranscriptPage.Conversation)[0].Speaker);
        Assert.DoesNotContain("Deparagon", model.TranscriptText, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void ACommanderWithNoNameKnownIsCMDR()
    {
        var panel = Laid(Conversation());

        var name = (TextBlock)((WrapPanel)Head(Turns(panel)[0]).Children[0]).Children[0];

        Assert.Equal("CMDR", name.Text);
    }

    [AvaloniaFact]
    public void TheSessionFigureSitsBesideSpendAndFollowsEachTurn()
    {
        var spent = 0.0231m;
        var model = Conversation();
        var panel = Laid(model, session: () => spent);

        var figure = panel.GetControl<TextBlock>("SessionFigure");
        var spend = panel.GetControl<Button>("TurnDetails");

        Assert.Equal(0.0231m.ToString("C4", System.Globalization.CultureInfo.CurrentCulture), figure.Text);
        Assert.True(LeftOf(figure, panel) < LeftOf(spend, panel), "the figure is not left of SPEND");
        Assert.Equal(28, spend.Bounds.Height, 0.5);

        spent = 0.0415m;
        model.TurnStatus = "routed: Model";
        model.TurnStatus = string.Empty;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0.0415m.ToString("C4", System.Globalization.CultureInfo.CurrentCulture), figure.Text);
    }

    [AvaloniaFact]
    public void TheSearchFieldIsLineAtRest()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite);
        var panel = Laid(Conversation());

        var search = panel.GetControl<TextBox>("SearchInput");
        var ask = panel.GetControl<TextBox>("AskBox");

        Assert.Equal(Resource(panel, ThemeManager.LineKey), ((ISolidColorBrush)search.BorderBrush!).Color);
        Assert.Equal(PanelView.TranscriptSearchWidth, search.Bounds.Width, 0.5);
        Assert.True(
            search.Bounds.Height <= ask.Bounds.Height + 0.5,
            $"the search field is {search.Bounds.Height} tall and the ask box {ask.Bounds.Height}");
    }

    /// <summary>One CMDR turn and two D47 turns at 1280×860 on Elite, saved for comparison with the reference.</summary>
    [AvaloniaFact]
    public void TheTranscriptIsCaptured()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite);

        var model = Conversation();
        model.AttachProvenance(0, new TurnProvenance("ANSWERED VIA CLAUDE-SONNET-5 · EFFORT MEDIUM", "$0.0690"));

        var panel = Laid(model, commander: "John Deparagon");
        var window = (Window)TopLevel.GetTopLevel(panel)!;
        var path = Path.Combine(TestSurface.CaptureDirectory, "transcript-footer.png");

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        window.Close();

        Assert.True(File.Exists(path));
    }
}
