using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Conversation;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Each answer carries its own provenance line, and the turn in flight shows in the microphone row (#431).</summary>
public class TheCostLineIsInsideEachAnswerTests
{
    private static readonly TurnCost Priced = new(LlmUsage.None, 0.069m, Priced: true);

    private static TurnResult ModelTurn(TurnCost? cost = null) => new(
        TurnOutcome.Answered, TurnRoute.Model, "Arque is two jumps out.", ThinkingEffort.Medium, cost ?? Priced,
        "claude-sonnet-5");

    private static PanelView Laid(PanelViewModel model)
    {
        var panel = new PanelView { DataContext = model };

        panel.EnableTurnDetails(() => Task.CompletedTask, () => 0.069m);

        var window = new Window { Width = 1280, Height = 860, Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static IReadOnlyList<Border> Turns(PanelView panel) =>
        [.. panel.GetControl<StackPanel>("Bubbles").Children.OfType<Border>()];

    private static string? LineIn(Border turn) =>
        turn.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => block.Name == "ProvenanceLine")
            ?.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Aggregate(string.Empty, (line, run) => line + run.Text);

    private static void Ask(PanelViewModel model, string asked, params TurnEvent[] events)
    {
        model.Append(asked, voice: TranscriptVoice.Commander);

        var presenter = new TurnPresenter(model);

        foreach (var turnEvent in events)
        {
            presenter.On(turnEvent);
        }
    }

    [Fact]
    public void AModelTurnNamesTheModelTheEffortAndTheCost()
    {
        var line = TurnProvenance.For(ModelTurn());

        Assert.Equal(
            $"ANSWERED VIA CLAUDE-SONNET-5 · EFFORT MEDIUM · {0.069m.ToString("C4", CultureInfo.CurrentCulture)}",
            line.Text);
        Assert.Equal(0.069m.ToString("C4", CultureInfo.CurrentCulture), line.Cost);
    }

    [Fact]
    public void AnUnpricedModelSaysSoInPlaceOfTheCost()
    {
        var line = TurnProvenance.For(ModelTurn(TurnCost.Unpriced(LlmUsage.None)));

        Assert.Equal("ANSWERED VIA CLAUDE-SONNET-5 · EFFORT MEDIUM · UNPRICED MODEL", line.Text);
        Assert.Null(line.Cost);
    }

    [Fact]
    public void AKeywordRoutedTurnShowsItsRouteWithNoCost()
    {
        var line = TurnProvenance.For(
            new TurnResult(TurnOutcome.Answered, TurnRoute.KeywordRouter, "Docked.", Effort: null, Cost: null));

        Assert.Equal("ANSWERED VIA KEYWORD ROUTER", line.Text);
        Assert.Null(line.Cost);
    }

    [AvaloniaFact]
    public void EarlierTurnsKeepTheirOwnLines()
    {
        var model = new PanelViewModel();

        Ask(model, "where is the nearest scoopable star",
            new TurnEvent.TextDelta("Arque is two jumps out."),
            new TurnEvent.Completed(ModelTurn()));

        Ask(model, "what time is it",
            new TurnEvent.TextDelta("Fourteen twenty."),
            new TurnEvent.Completed(
                new TurnResult(TurnOutcome.Answered, TurnRoute.KeywordRouter, "Fourteen twenty.", null, null)));

        var turns = Turns(Laid(model));

        Assert.Equal(4, turns.Count);
        Assert.Null(LineIn(turns[0]));
        Assert.StartsWith("ANSWERED VIA CLAUDE-SONNET-5", LineIn(turns[1]), StringComparison.Ordinal);
        Assert.Null(LineIn(turns[2]));
        Assert.Equal("ANSWERED VIA KEYWORD ROUTER", LineIn(turns[3]));
    }

    [AvaloniaFact]
    public void TheLineGoesOnAReplyUnderAnotherNameAndNotOnACalloutAfterIt()
    {
        var model = new PanelViewModel();

        model.Append("ask the captain", voice: TranscriptVoice.Commander);
        var presenter = new TurnPresenter(model);

        presenter.On(new TurnEvent.Addressed(D47.Core.Audio.VoiceRole.CarrierCaptain, "Captain Reyes", 1));
        presenter.On(new TurnEvent.TextDelta("Jump is plotted."));
        model.Append("Docking granted.", sourceKey: "docking.granted");
        presenter.On(new TurnEvent.Completed(ModelTurn()));

        var turns = Turns(Laid(model));

        Assert.Equal(3, turns.Count);
        Assert.NotNull(LineIn(turns[1]));
        Assert.Null(LineIn(turns[2]));
    }

    [AvaloniaFact]
    public void ACopyOfTheTranscriptCarriesNoProvenance()
    {
        var model = new PanelViewModel();

        Ask(model, "where is the nearest scoopable star",
            new TurnEvent.TextDelta("Arque is two jumps out."),
            new TurnEvent.Completed(ModelTurn()));

        Assert.DoesNotContain("ANSWERED", model.TranscriptText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ARetryShowsInTheMicrophoneRowUntilTheTurnCompletes()
    {
        var model = new PanelViewModel();
        var panel = Laid(model);

        model.Append("where is the nearest scoopable star", voice: TranscriptVoice.Commander);
        var presenter = new TurnPresenter(model);

        presenter.On(new TurnEvent.Routed(TurnRoute.Model, ThinkingEffort.Medium));
        presenter.On(new TurnEvent.Retrying(1, 3, TimeSpan.FromSeconds(2), "overloaded"));
        Dispatcher.UIThread.RunJobs();

        var status = panel.GetControl<TextBlock>("TurnStatus");

        Assert.Same(panel.GetControl<Border>("MicrophoneRow"), status.FindAncestorOfType<Border>());
        Assert.StartsWith("retrying (1/3) in 2s", status.Text, StringComparison.Ordinal);

        presenter.On(new TurnEvent.TextDelta("Arque is two jumps out."));
        presenter.On(new TurnEvent.Completed(ModelTurn()));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, status.Text);
    }

    [AvaloniaFact]
    public void NothingIsDrawnBetweenTheListAndTheFooter()
    {
        var model = new PanelViewModel();
        model.Append("Holding at Fixture Anchorage.");

        var panel = Laid(model);

        var pane = panel.GetControl<Border>("TranscriptPane");
        var footer = panel.GetControl<Border>("Footer");

        var paneBottom = pane.TranslatePoint(new Point(0, pane.Bounds.Height), panel)!.Value.Y;
        var footerTop = footer.TranslatePoint(default, panel)!.Value.Y;

        Assert.Equal(footer.Margin.Top, footerTop - paneBottom, 0.5);
        Assert.Null(panel.FindControl<Control>("StatusRow"));
    }

    /// <summary>Two answered turns on Elite, each with its own line, for a human to look at.</summary>
    [AvaloniaFact]
    public void TheLinesAreCaptured()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite);

        var model = new PanelViewModel();

        Ask(model, "where is the nearest scoopable star",
            new TurnEvent.TextDelta("Arque is two jumps out, a K class, scoopable."),
            new TurnEvent.Completed(ModelTurn()));

        model.Append("what time is it", voice: TranscriptVoice.Commander);
        var second = new TurnPresenter(model);

        second.On(new TurnEvent.Routed(TurnRoute.Model, ThinkingEffort.Medium));
        second.On(new TurnEvent.Retrying(1, 3, TimeSpan.FromSeconds(2), "the provider is overloaded"));
        second.On(new TurnEvent.TextDelta("Fourteen twenty, galactic standard."));

        var panel = Laid(model);
        var window = (Window)TopLevel.GetTopLevel(panel)!;

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(Path.Combine(TestSurface.CaptureDirectory, "turn-provenance-live.png"), new PngBitmapEncoderOptions());
        }

        second.On(new TurnEvent.Completed(
            new TurnResult(TurnOutcome.Answered, TurnRoute.KeywordRouter, "Fourteen twenty.", null, null)));
        Dispatcher.UIThread.RunJobs();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(Path.Combine(TestSurface.CaptureDirectory, "turn-provenance-done.png"), new PngBitmapEncoderOptions());
        }

        window.Close();
    }
}
