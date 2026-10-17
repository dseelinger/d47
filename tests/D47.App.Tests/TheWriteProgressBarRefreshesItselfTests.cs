using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Adventures;
using Xunit;

namespace D47.App.Tests;

/// <summary>The bar reads its source on refresh and keeps its last reading when the source has none.</summary>
[Trait("Category", "Integration")]
public sealed class TheWriteProgressBarRefreshesItselfTests
{
    [AvaloniaFact]
    public void ABarShowsALaterReadingAndKeepsItWhenTheSourceGoesEmpty()
    {
        var clock = new Clock();
        var progress = new StoryProgress(clock);
        progress.Enter(WriteStage.Objectives, StoryPace.For("claude-sonnet-5-5"));
        StoryProgress? source = progress;

        var bar = new WriteProgressBar("Writing the chapter", () => source);
        var window = new Window { Content = bar };
        window.Show();

        clock.Advance(TimeSpan.FromSeconds(12));
        bar.Refresh();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("0:12", Texts(bar));

        clock.Advance(TimeSpan.FromSeconds(10));
        bar.Refresh();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("0:22", Texts(bar));

        source = null;
        clock.Advance(TimeSpan.FromSeconds(10));
        bar.Refresh();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("0:22", Texts(bar));

        window.Close();
    }

    private static List<string?> Texts(Control control) =>
        [.. control.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text)];

    private sealed class Clock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }
}
