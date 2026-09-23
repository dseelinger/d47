using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Diagnostics.Recording;

namespace D47.App.Controls;

/// <summary>
/// The review surface for the audio recorder (#164): every utterance that crossed the audio boundary
/// this recording, what d47 made of it, and the button that turns one into a regression test.
/// </summary>
public sealed class AudioRecorderWindow : Window
{
    private readonly RecordingLog _log;
    private readonly Func<DateTimeOffset> _now;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly StackPanel _detail = new() { Spacing = 6 };
    private readonly TextBlock _summary = new();

    private RecordingRow? _selected;

    public AudioRecorderWindow(RecordingLog log, Func<DateTimeOffset> now)
    {
        ArgumentNullException.ThrowIfNull(log);

        _log = log;
        _now = now;

        Title = "Audio recorder";
        Width = 860;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _summary.Name = "RecordingSummary";
        _summary.FontSize = TypeScale.Body;
        _summary.TextWrapping = TextWrapping.Wrap;
        Themed(_summary, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var folder = new Button
        {
            Name = "RecordingFolder",
            Content = "Open the folder",
        };

        folder.Click += (_, _) => Launch(_log.Folder);

        var close = new Button { Name = "RecordingClose", Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        var detailBox = new Border
        {
            [DockPanel.DockProperty] = Dock.Bottom,
            Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(14, 12),
            Child = _detail,
        };

        Themed(detailBox, Border.BackgroundProperty, ThemeManager.SlabKey);

        _summary.Margin = new Thickness(0, 0, 0, 16);
        DockPanel.SetDock(_summary, Dock.Top);

        var body = new DockPanel
        {
            Children =
            {
                _summary,
                detailBox,
                new ScrollViewer
                {
                    Name = "RecordingScroller",
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = _list,
                },
            },
        };

        Modal.Apply(this, "Recorder", Title, body, [folder, close], scrolls: false);

        Refresh();

        Opened += (_, _) => close.Focus();
    }

    /// <summary>Redraws the list and the detail pane from the record as it now stands.</summary>
    private void Refresh()
    {
        var rows = _log.Rows;

        _summary.Text = _log.Summary();
        _list.Children.Clear();

        if (rows.Count == 0)
        {
            _list.Children.Add(Muted(
                "Nothing yet. What the transcriber was given and what left the speakers both land "
                + "here, as they happen."));
        }

        foreach (var row in rows)
        {
            _list.Children.Add(Row(row));
        }

        if (_selected is { } was)
        {
            _selected = rows.FirstOrDefault(row =>
                string.Equals(row.Id, was.Id, StringComparison.Ordinal));
        }

        MarkSelected();
        ShowDetail();
    }

    private Control Row(RecordingRow row)
    {
        var mark = ListRow.Secondary(new TextBlock
        {
            Text = row.Direction == RecordingDirection.Heard ? "HEARD" : "SAID",
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            Width = 56,
            VerticalAlignment = VerticalAlignment.Center,

            // Weight as well as colour, for the reason the coverage list carries it: two of the five themes
            // take their palette from the Commander's own file, so no pair of brushes can be guaranteed to
            // read as different.
            FontWeight = row.Kept is null ? FontWeight.Normal : FontWeight.Bold,
        });

        var text = ListRow.Name(new TextBlock
        {
            Text = row.Text is { Length: > 0 } said ? said : "(nothing intelligible)",
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var when = ListRow.Secondary(new TextBlock
        {
            Text = row.Kept is null
                ? $"{row.When:HH:mm:ss}  {row.Duration.TotalSeconds:0.0}s"
                : $"kept  {row.When:HH:mm:ss}  {row.Duration.TotalSeconds:0.0}s",
            FontSize = TypeScale.Small,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        });

        var button = new Button
        {
            Name = "RecordingRow",
            Tag = row.Id,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new DockPanel
            {
                Children =
                {
                    mark,
                    new StackPanel
                    {
                        [DockPanel.DockProperty] = Dock.Right,
                        Orientation = Orientation.Horizontal,
                        Children = { when },
                    },
                    text,
                },
            },
        };

        ListRow.Dress(button, string.Equals(_selected?.Id, row.Id, StringComparison.Ordinal));

        button.Click += (_, _) =>
        {
            _selected = row;
            MarkSelected();
            ShowDetail();
        };

        return button;
    }

    /// <summary>Moves the selected fill to the row <see cref="_selected"/> names.</summary>
    private void MarkSelected()
    {
        foreach (var button in _list.Children.OfType<Button>())
        {
            button.Classes.Set(
                ListRow.SelectedClass,
                string.Equals(button.Tag as string, _selected?.Id, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Everything about the selected row that a list cannot hold, and the two things that can be done
    /// with it: hear it, and keep it.
    /// </summary>
    private void ShowDetail()
    {
        _detail.Children.Clear();

        if (_selected is not { } row)
        {
            _detail.Children.Add(Muted("Pick a row to see what d47 made of it."));
            return;
        }

        _detail.Children.Add(Label(row.Text is { Length: > 0 } said ? said : "(nothing intelligible)"));

        if (row.Phonemes is { Length: > 0 } phonemes)
        {
            // The column that turns a mispronunciation from an anecdote into a diagnosis.
            _detail.Children.Add(Muted($"Phonemes  {phonemes}", selectable: true, key: ThemeManager.AKey));
        }

        _detail.Children.Add(Muted(Provenance(row), key: ThemeManager.Grey2Key));

        if (row.Kept is { } kept)
        {
            _detail.Children.Add(Muted(
                $"Kept as a {(kept.Kind == RecordingKeepKind.Mishear ? "mishear" : "pronunciation")} case "
                + $"on {kept.When:yyyy-MM-dd HH:mm} — expected: {kept.Expected}"));
        }

        var play = new Button
        {
            Name = "RecordingPlay",
            Content = "Play it",
        };

        play.Click += (_, _) => Launch(Path.Combine(_log.Folder, row.Clip));

        var mishear = row.Direction == RecordingDirection.Heard;

        var expected = new TextBox
        {
            Name = "RecordingExpected",
            PlaceholderText = mishear
                ? "What you actually said"
                : "The phonemes it should have been said as",
            FontSize = TypeScale.Body,
            MinWidth = 380,
            Text = row.Kept?.Expected ?? string.Empty,
        };

        var keep = new Button
        {
            Name = "RecordingKeep",
            Content = mishear ? "Keep as a mishear case" : "Keep as a pronunciation case",
        };

        var complaint = new TextBlock
        {
            Name = "RecordingKept",
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(complaint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        keep.Click += (_, _) =>
        {
            if (expected.Text is not { Length: > 0 } wanted || string.IsNullOrWhiteSpace(wanted))
            {
                // Said rather than silently ignored.
                complaint.Text = mishear
                    ? "Type what you actually said first — that is the half the recording cannot supply."
                    : "Type the phonemes it should have been said as first.";

                return;
            }

            var kind = mishear ? RecordingKeepKind.Mishear : RecordingKeepKind.Pronunciation;

            _selected = _log.Keep(row.Id, kind, wanted.Trim(), _now());
            Refresh();
        };

        _detail.Children.Add(new WrapPanel
        {
            ItemSpacing = 8,
            LineSpacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { play, expected, keep },
        });

        _detail.Children.Add(complaint);
    }

    /// <summary>Where the row came from, in one line.</summary>
    private static string Provenance(RecordingRow row)
    {
        var parts = new List<string> { $"{row.When:yyyy-MM-dd HH:mm:ss}", $"{row.Duration.TotalSeconds:0.0}s" };

        if (row.Provider is { Length: > 0 } provider)
        {
            parts.Add(provider);
        }

        if (row.Voice is { Length: > 0 } voice)
        {
            parts.Add(voice);
        }

        if (row.Model is { Length: > 0 } model)
        {
            parts.Add(model);
        }

        if (row.Elapsed > TimeSpan.Zero)
        {
            parts.Add($"{row.Elapsed.TotalMilliseconds:0} ms to render");
        }

        parts.Add(row.Clip);

        return string.Join("  ·  ", parts);
    }

    /// <summary>Hands a path to the shell.</summary>
    private void Launch(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            _summary.Text = $"Could not open {path} — {ex.Message}";
        }
    }

    private static TextBlock Label(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Subheading,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return block;
    }

    private static Control Muted(string text, bool selectable = false, string key = ThemeManager.GreyKey)
    {
        if (!selectable)
        {
            var block = new TextBlock
            {
                Text = text,
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
            };

            Themed(block, TextBlock.ForegroundProperty, key);

            return block;
        }

        var selectableBlock = new SelectableTextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(selectableBlock, SelectableTextBlock.ForegroundProperty, key);

        return selectableBlock;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
