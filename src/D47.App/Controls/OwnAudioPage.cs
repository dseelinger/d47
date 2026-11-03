using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;

namespace D47.App.Controls;

/// <summary>Every folder of the Commander's own audio with its file count, and every file that was ignored or skipped.</summary>
public sealed class OwnAudioPage : DialogPage
{
    public override string Crumb => "Your own audio";

    private static readonly (string Group, string Title, string None)[] Groups =
    [
        (OwnAudioReport.Music, "Music", "No music of your own."),
        (OwnAudioReport.Cues, "Sound cues", "No cues of your own."),
        (OwnAudioReport.Alerts, "Alerts", "No alert sounds of your own."),
        (OwnAudioReport.Beds, "Thinking beds", "No thinking beds of your own."),
    ];

    private readonly Func<OwnAudioReport> _report;
    private readonly Func<Action, Action>? _watch;
    private readonly StackPanel _body = new() { Spacing = 20 };

    private Action? _unwatch;

    public OwnAudioPage(Func<OwnAudioReport> report, Action? openFolder, Func<Action, Action>? watch)
    {
        _report = report;
        _watch = watch;

        Title = "Your own audio";

        var buttons = new List<Control>();

        if (openFolder is not null)
        {
            var open = new Button { Name = "OpenAudioFolder", Content = "Open audio folder" };
            open.Click += (_, _) => openFolder();
            buttons.Add(open);
        }

        var back = new Button { Name = "Back", Content = "Back", MinWidth = 110 };
        back.Click += (_, _) => Close();
        buttons.Add(back);

        Modal.Apply(this, "Settings", Title, _body, buttons);

        Rebuild();

        AttachedToVisualTree += (_, _) =>
        {
            _unwatch?.Invoke();
            _unwatch = _watch?.Invoke(() => Dispatcher.UIThread.Post(Rebuild));
        };

        DetachedFromVisualTree += (_, _) =>
        {
            _unwatch?.Invoke();
            _unwatch = null;
        };
    }

    private void Rebuild()
    {
        var report = _report();

        _body.Children.Clear();

        foreach (var (group, title, none) in Groups)
        {
            var folders = report.In(group);
            var files = folders.Sum(folder => folder.Count);

            _body.Children.Add(Group(title, folders.Count == 0 ? none : $"{files} file{(files == 1 ? "" : "s")}, most files first.", folders));
        }

        if (report.HasWarning)
        {
            _body.Children.Add(Warnings(report));
        }
    }

    private static Control Group(string title, string help, IReadOnlyList<OwnAudioFolder> folders)
    {
        var reset = SettingsView.ResetGlyph("NoReset", $"Reset {title}");
        reset.IsVisible = false;

        var (head, _, _) = SettingsView.GroupHead(title, help, reset);
        var group = new StackPanel { Name = title.Replace(" ", string.Empty, StringComparison.Ordinal), Children = { head } };

        if (folders.Count > 0)
        {
            var grid = new UniformGrid { Columns = 3 };

            foreach (var folder in folders)
            {
                grid.Children.Add(Cell(folder));
            }

            group.Children.Add(grid);
        }

        return group;
    }

    private static Control Cell(OwnAudioFolder folder)
    {
        var name = Typeset.Mono(folder.Name, TypeScale.Small, ThemeManager.WhiteKey, trimming: Avalonia.Media.TextTrimming.CharacterEllipsis);
        var count = Typeset.Mono(folder.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), TypeScale.Small, ThemeManager.GreyKey);

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(count, Dock.Right);
        count.Margin = new Thickness(12, 0, 0, 0);
        dock.Children.Add(count);
        dock.Children.Add(name);

        var cell = new Border { Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 2, 2), Child = dock };
        ThemeInk.Bind(cell, Border.BackgroundProperty, ThemeManager.SlabKey);

        return cell;
    }

    private static Control Warnings(OwnAudioReport report)
    {
        var reset = SettingsView.ResetGlyph("NoReset", "Reset Files D47 did not use");
        reset.IsVisible = false;

        var (head, _, _) = SettingsView.GroupHead("Files D47 did not use", report.WarningLabel() + ".", reset);
        var group = new StackPanel { Name = "Warnings", Children = { head } };

        foreach (var line in report.Ignored.Select(reason => $"Ignored: {reason}").Concat(report.Skipped.Select(reason => $"Skipped: {reason}")))
        {
            var text = Typeset.Mono(line, TypeScale.Small, ThemeManager.WhiteKey, wrap: true);
            var row = new Border { Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 0, 2), Child = text };
            ThemeInk.Bind(row, Border.BackgroundProperty, ThemeManager.SlabKey);
            group.Children.Add(row);
        }

        return group;
    }
}
