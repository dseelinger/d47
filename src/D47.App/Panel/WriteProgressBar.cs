using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Adventures;

namespace D47.App.Panel;

/// <summary>The stage words, time so far, gauge and hint for a story write in progress; refreshes from its source while attached.</summary>
internal sealed class WriteProgressBar : StackPanel
{
    private readonly Grid _track = Gauge.Track(0, ThemeManager.AKey);
    private readonly TextBlock _hint = AdventuresPage.Muted("");
    private readonly Func<StoryProgress?> _source;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public WriteProgressBar(string automationName, Func<StoryProgress?> source)
    {
        _source = source;
        _timer.Tick += (_, _) => Refresh();
        Spacing = 6;
        Margin = new Thickness(0, 8, 0, 0);
        Children.Add(new Border());
        Children.Add(_track);
        Children.Add(_hint);
        AutomationProperties.SetName(_track, automationName);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    /// <summary>Shows what the source returns now; keeps the last reading when it returns null.</summary>
    internal void Refresh()
    {
        if (_source() is { } progress)
        {
            Show(progress);
        }
    }

    public void Show(StoryProgress progress)
    {
        Children[0] = Gauge.Heading(AdventuresPage.Doing(progress.Stage), Clock(progress.Elapsed), ThemeManager.AKey);
        Gauge.Fill(_track, progress.Fill);
        _hint.Text = progress.Hint;
        _hint.IsVisible = _hint.Text.Length > 0;
    }

    private static string Clock(TimeSpan elapsed) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}");
}
