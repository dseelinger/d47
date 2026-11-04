using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Windowing;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Find lists local results at once, asks the galaxy after a pause, and drops answers that arrive late (#1973).</summary>
public sealed class FindAsksTheGalaxyAsYouTypeTests
{
    private sealed class Asked
    {
        public List<(string Typed, TaskCompletionSource<IReadOnlyList<PanelFindName>> Answer, CancellationToken Token)> Calls { get; } = [];

    }

    private static void Jobs()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static PanelFindResult Local(string title) => new(PanelFindKind.Page, title, "Search", "key:" + title, 2);

    private static (FindDialog Dialog, Asked Asked) Shown()
    {
        var asked = new Asked();
        var window = new Window { Content = new Grid(), Width = 900, Height = 700 };
        window.Show();

        var galaxy = new GalaxyFind(
            (typed, token) =>
            {
                var answer = new TaskCompletionSource<IReadOnlyList<PanelFindName>>();
                asked.Calls.Add((typed, answer, token));
                return answer.Task;
            },
            (typed, names) =>
            [
                Local("Local " + typed),
                .. names.Select(name => new PanelFindResult(name.Kind, name.Title, name.Path, name.Target, 2, false, name.SystemAddress, name.LightYears)),
            ])
        {
            Pause = TimeSpan.Zero,
        };

        var dialog = new FindDialog(typed => typed.Length == 0 ? [] : [Local("Local " + typed)], _ => { }, "F", galaxy);
        _ = dialog.Over(window);
        Jobs();

        return (dialog, asked);
    }

    private static IReadOnlyList<PanelFindName> Names(params string[] titles) =>
        [.. titles.Select(title => new PanelFindName(PanelFindKind.Faction, title, "Minor faction", title))];

    [AvaloniaFact]
    public void TwoCharactersSendNothing()
    {
        var (dialog, asked) = Shown();

        dialog.Query = "da";
        Jobs();

        Assert.Empty(asked.Calls);
        Assert.Equal("1 RESULT", dialog.Count);
    }

    [AvaloniaFact]
    public void TheCountSaysAskingWhileARequestIsOutAndNotAfterItIsAnswered()
    {
        var (dialog, asked) = Shown();

        dialog.Query = "dark";
        Jobs();

        Assert.Equal("1 RESULT · ASKING SPANSH", dialog.Count);

        asked.Calls[0].Answer.SetResult(Names("Dark Echo"));
        Jobs();

        Assert.Equal("2 RESULTS", dialog.Count);
        Assert.Contains(dialog.Shown, r => r.Title == "Dark Echo");
    }

    [AvaloniaFact]
    public void ANewKeystrokeCancelsTheRequestAndALateAnswerIsDropped()
    {
        var (dialog, asked) = Shown();

        dialog.Query = "dar";
        Jobs();
        dialog.Query = "dark";
        Jobs();

        Assert.True(asked.Calls[0].Token.IsCancellationRequested);

        asked.Calls[1].Answer.SetResult(Names("Dark Echo"));
        Jobs();
        asked.Calls[0].Answer.SetResult(Names("Darwin Union"));
        Jobs();

        Assert.Contains(dialog.Shown, r => r.Title == "Dark Echo");
        Assert.DoesNotContain(dialog.Shown, r => r.Title == "Darwin Union");
    }

    [AvaloniaFact]
    public void AFailedAnswerLeavesTheLocalResultsAndNoAskingMark()
    {
        var (dialog, asked) = Shown();

        dialog.Query = "dark";
        Jobs();
        asked.Calls[0].Answer.SetException(new HttpRequestException("down"));
        Jobs();

        Assert.Equal("1 RESULT", dialog.Count);
        Assert.Single(dialog.Shown);
    }

    [AvaloniaFact]
    public void NoMatchIsNotSaidWhileARequestIsOut()
    {
        var asked = new Asked();
        var window = new Window { Content = new Grid(), Width = 900, Height = 700 };
        window.Show();
        var galaxy = new GalaxyFind(
            (typed, token) =>
            {
                var answer = new TaskCompletionSource<IReadOnlyList<PanelFindName>>();
                asked.Calls.Add((typed, answer, token));
                return answer.Task;
            },
            (_, names) => [.. names.Select(name => new PanelFindResult(name.Kind, name.Title, name.Path, name.Target, 2))])
        {
            Pause = TimeSpan.Zero,
        };
        var dialog = new FindDialog(_ => [], _ => { }, "F", galaxy);
        _ = dialog.Over(window);
        Jobs();

        dialog.Query = "zzzz";
        Jobs();

        Assert.False(dialog.HintShown);

        asked.Calls[0].Answer.SetResult([]);
        Jobs();

        Assert.True(dialog.HintShown);
    }

    [AvaloniaFact]
    public void TheHighlightedResultStaysHighlightedWhenTheListGrows()
    {
        var (dialog, asked) = Shown();

        dialog.Query = "dark";
        Jobs();
        asked.Calls[0].Answer.SetResult(Names("Dark Echo"));
        Jobs();

        Assert.Equal("Local dark", dialog.Shown[dialog.SelectedIndex].Title);
    }

    [AvaloniaFact]
    public void AStationPathHoldsTheWordASeparatorAndTheDistanceInMono()
    {
        var window = new Window { Content = new Grid(), Width = 900, Height = 700 };
        window.Show();
        var station = new PanelFindResult(PanelFindKind.Station, "Dark Wheel", "Station", "Dark Wheel", 2, false, 11, 64.31);
        var plain = new PanelFindResult(PanelFindKind.Station, "Dark Port", "Station", "Dark Port", 2, false, 12);
        var dialog = new FindDialog(typed => typed.Length == 0 ? [] : [station, plain], _ => { }, "F");
        _ = dialog.Over(window);
        Jobs();

        dialog.Query = "dark";
        Jobs();

        var runs = dialog.PathRuns(0);
        Assert.Equal(["Station", " · ", "64.3 LY"], runs.Select(run => run.Text));
        Assert.NotEqual(runs[0].FontFamily, runs[2].FontFamily);
        Assert.Equal(["Station"], dialog.PathRuns(1).Select(run => run.Text));
    }
}
