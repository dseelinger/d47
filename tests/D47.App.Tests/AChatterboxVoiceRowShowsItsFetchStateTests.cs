using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core.Audio;
using D47.Core.Capabilities;
using Xunit;

namespace D47.App.Tests;

public class AChatterboxVoiceRowShowsItsFetchStateTests
{
    private sealed class Clips
    {
        public Dictionary<string, ChatterboxClipState> State { get; } = new()
        {
            ["marlow"] = new(ChatterboxClip.Shipped, 0),
            ["alder"] = new(ChatterboxClip.NotFetched, 247 * 1024),
            ["lucan"] = new(ChatterboxClip.Failed, 300 * 1024),
        };

        public Action<string>? Refresh { get; private set; }

        public List<string> Fetches { get; } = [];

        public SettingChoiceClips Make(string? suggested = null) => new()
        {
            Clip = id => State.TryGetValue(id, out var state) ? ChoiceClip.Of(state) : null,
            Detail = id => id == "marlow" ? "Female · mid pitch · even pace" : null,
            Watch = refresh =>
            {
                Refresh = refresh;
                return () => Refresh = null;
            },
            Fetch = Fetches.Add,
            Suggested = suggested is null ? null : (suggested, "Ship AI"),
        };
    }

    private static (PickerPage Picker, Window Window) Open(
        Clips clips, string? suggested = null, string current = "alder", string[]? choices = null)
    {
        var picker = PickerPage.For(new PickerRequest
        {
            Prompt = "Voice",
            Choices = choices ?? ["alder", "lucan", "marlow"],
            Current = current,
            Clips = clips.Make(suggested),
        });
        var window = new Window { Content = picker, Width = 900, Height = 520 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (picker, window);
    }

    private static string[] Lines(PickerPage picker) =>
        [.. picker.GetVisualDescendants().OfType<ListBoxItem>().Select(item =>
            string.Join(" | ", item.GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.IsVisible && !string.IsNullOrEmpty(text.Text))
                .Select(text => text.Text)))];

    /// <summary>The row whose words include all of these, in any order.</summary>
    private static string? Row(PickerPage picker, params string[] words) =>
        Lines(picker).FirstOrDefault(line => words.All(word => line.Split(" | ").Contains(word)));

    [AvaloniaFact]
    public void TheListIsSplitUnderTwoHeadsWithTheirCounts()
    {
        var (picker, _) = Open(new Clips());
        var lines = Lines(picker);

        Assert.NotNull(Row(picker, "ON THIS PC", "1", "marlow"));
        Assert.NotNull(Row(picker, "FETCHED WHEN PLAYED OR CHOSEN", "2", "alder"));
        Assert.Contains("marlow", lines[0]);
        Assert.Contains("alder", lines[1]);
    }

    [AvaloniaFact]
    public void EachStateDrawsItsWordAndSize()
    {
        var (picker, _) = Open(new Clips());

        Assert.NotNull(Row(picker, "marlow", "Female · mid pitch · even pace", "SHIPPED"));
        Assert.NotNull(Row(picker, "alder", "NOT FETCHED", "247 KB"));
        Assert.NotNull(Row(picker, "lucan", "FETCH FAILED", "300 KB", "Retry"));
    }

    [AvaloniaFact]
    public void TheVoiceInUseIsTagged()
    {
        var (picker, _) = Open(new Clips(), current: "alder");

        Assert.NotNull(Row(picker, "alder", "IN USE"));
        Assert.Null(Row(picker, "lucan", "IN USE"));
    }

    [AvaloniaFact]
    public void TheSuggestedVoiceLeadsWhatIsOnThisPc()
    {
        var clips = new Clips();
        clips.State["isolde"] = new(ChatterboxClip.Shipped, 0);

        var (picker, _) = Open(clips, suggested: "marlow", choices: ["isolde", "marlow", "alder"]);

        Assert.NotNull(Row(picker, "marlow", "SUGGESTED FOR SHIP AI", "ON THIS PC"));
        Assert.Contains("marlow", Lines(picker)[0]);
    }

    [AvaloniaFact]
    public void AVoiceHeldAtFetchingMovesToOnThisPcWhenTheFetchSucceeds()
    {
        var clips = new Clips();
        clips.State["alder"] = new(ChatterboxClip.Fetching, 247 * 1024);

        var (picker, _) = Open(clips);

        Assert.NotNull(Row(picker, "alder", "FETCHING", "247 KB"));
        Assert.Null(Row(picker, "alder", "ON THIS PC"));

        clips.State["alder"] = new(ChatterboxClip.Fetched, 247 * 1024);
        clips.Refresh!("alder");
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(Row(picker, "alder", "FETCHED"));
        Assert.Null(Row(picker, "alder", "FETCHING"));
        Assert.NotNull(Row(picker, "ON THIS PC", "2"));
    }

    [AvaloniaFact]
    public void RetryOnAFailedRowStartsOneFetch()
    {
        var clips = new Clips();
        var (picker, _) = Open(clips);

        var retry = picker.GetVisualDescendants().OfType<Button>().Single(button => button.IsVisible && button.Content as string == "Retry");

        retry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["lucan"], clips.Fetches);
    }

    [AvaloniaFact]
    public void AListWithoutClipsDrawsNoStateNoSizeAndNoSplit()
    {
        var picker = PickerPage.For(new PickerRequest { Prompt = "Voice", Choices = ["af_heart", "am_michael"] });
        var window = new Window { Content = picker, Width = 900, Height = 520 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["af_heart", "am_michael"], Lines(picker));
    }

    [AvaloniaFact]
    public void ALongChatterboxListStaysVirtualised()
    {
        var clips = new Clips();
        var ids = Enumerable.Range(0, 847).Select(i => $"voice-{i}").ToArray();

        foreach (var id in ids)
        {
            clips.State[id] = new(ChatterboxClip.NotFetched, 100 * 1024);
        }

        var (picker, window) = Open(clips, current: ids[0], choices: ids);

        Assert.InRange(picker.GetVisualDescendants().OfType<ListBoxItem>().Count(), 1, 60);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("voice-picker-fetch-state-long.png");
    }

    [AvaloniaFact]
    public void ThePickerDrawsTheStatesForACapture()
    {
        var (_, window) = Open(new Clips(), suggested: "marlow");

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("voice-picker-fetch-state.png");
    }
}
