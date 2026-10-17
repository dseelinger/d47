using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Adventures;

namespace D47.App.Panel;

/// <summary>The stage words, time so far, gauge and hint for a story write in progress.</summary>
internal sealed class WriteProgressBar : StackPanel
{
    private readonly Grid _track = Gauge.Track(0, ThemeManager.AKey);
    private readonly TextBlock _hint = AdventuresPage.Muted("");

    public WriteProgressBar(string automationName)
    {
        Spacing = 6;
        Margin = new Thickness(0, 8, 0, 0);
        Children.Add(new Border());
        Children.Add(_track);
        Children.Add(_hint);
        AutomationProperties.SetName(_track, automationName);
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
