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
/// The review surface for the audio recorder
/// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>): every utterance that
/// crossed the audio boundary this recording, what d47 made of it, and the button that turns one
/// into a regression test.
/// <para>
/// A window rather than more of the Privacy card, for the reason the coverage list is one: a
/// card taller than every other card stops the settings surface navigating by scroll position.
/// Desktop-only workbench furniture, like Settings — parity with the headset is optional here
/// and is not sought.
/// </para>
/// <para>
/// <b>Keeping is the half that earns the feature, and nothing is kept without the Commander's
/// hand on it.</b> A recording says what happened; it cannot say what should have happened, and
/// that is exactly the half a test case needs. So the expected value is typed — the words that
/// were actually said, or the phonemes the line should have been given — and the button will not
/// act until it is. That is <a href="https://github.com/dseelinger/d47/issues/162">#162</a>'s
/// adoption gate applied to test cases: d47 does not get to grade its own homework.
/// </para>
/// </summary>
public sealed class AudioRecorderWindow : Window
{
    private readonly RecordingLog _log;
    private readonly Func<DateTimeOffset> _now;
    private readonly StackPanel _list = new() { Spacing = 1 };
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

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        _summary.Name = "RecordingSummary";
        _summary.FontSize = TypeScale.Body;
        _summary.TextWrapping = TextWrapping.Wrap;
        Themed(_summary, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var folder = new Button
        {
            Name = "RecordingFolder",
            Content = "Open the folder",
            FontSize = TypeScale.Body,
            Padding = new Thickness(10, 4),
        };

        folder.Click += (_, _) => Launch(_log.Folder);

        var close = new Button { Name = "RecordingClose", Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        Content = new DockPanel
        {
            Margin = new Thickness(24),
            Children =
            {
                new StackPanel
                {
                    [DockPanel.DockProperty] = Dock.Top,
                    Spacing = 4,
                    Margin = new Thickness(0, 0, 0, 16),
                    Children = { Heading("Audio recorder"), _summary },
                },
                new StackPanel
                {
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 16, 0, 0),
                    Children = { folder, close },
                },
                new Border
                {
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Margin = new Thickness(0, 16, 0, 0),
                    Padding = new Thickness(14, 12),
                    CornerRadius = new CornerRadius(3),
                    Child = _detail,
                    [!BackgroundProperty] = new DynamicResourceExtension(ThemeManager.SurfaceAltKey),
                },
                new ScrollViewer
                {
                    Name = "RecordingScroller",
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = _list,
                },
            },
        };

        Refresh();

        Opened += (_, _) => close.Focus();
    }

    /// <summary>
    /// Redraws the list and the detail pane from the record as it now stands.
    /// <para>
    /// Whole rather than incremental. Keeping a row rewrites the corpus and the index, and a
    /// surface that patched one row after that would be a second description of the record with
    /// its own way of being wrong.
    /// </para>
    /// </summary>
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

        ShowDetail();
    }

    private Control Row(RecordingRow row)
    {
        var mark = new TextBlock
        {
            Text = row.Direction == RecordingDirection.Heard ? "heard" : "said",
            FontSize = TypeScale.Small,
            Width = 52,
            VerticalAlignment = VerticalAlignment.Center,

            // Weight as well as colour, for the reason the coverage list carries it: two of the
            // five themes take their palette from the Commander's own file, so no pair of
            // brushes can be guaranteed to read as different.
            FontWeight = row.Kept is null ? FontWeight.Normal : FontWeight.Bold,
        };

        Themed(
            mark,
            TextBlock.ForegroundProperty,
            row.Direction == RecordingDirection.Heard ? ThemeManager.AccentKey : ThemeManager.TextMutedKey);

        var text = new TextBlock
        {
            Text = row.Text is { Length: > 0 } said ? said : "(nothing intelligible)",
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        Themed(text, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var when = new TextBlock
        {
            Text = row.Kept is null
                ? $"{row.When:HH:mm:ss}  {row.Duration.TotalSeconds:0.0}s"
                : $"kept  {row.When:HH:mm:ss}  {row.Duration.TotalSeconds:0.0}s",
            FontSize = TypeScale.Small,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };

        Themed(when, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var button = new Button
        {
            Name = "RecordingRow",
            Tag = row.Id,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 5),
            FontSize = TypeScale.Body,
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

        button.Click += (_, _) =>
        {
            _selected = row;
            ShowDetail();
        };

        return button;
    }

    /// <summary>
    /// Everything about the selected row that a list cannot hold, and the two things that can be
    /// done with it: hear it, and keep it.
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
            // The column that turns a mispronunciation from an anecdote into a diagnosis. It
            // is the whole reason a said row is worth keeping at all, so it is stated in full
            // and selectable rather than trimmed to the width of the pane.
            _detail.Children.Add(Muted($"Phonemes  {phonemes}", selectable: true));
        }

        _detail.Children.Add(Muted(Provenance(row)));

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
            FontSize = TypeScale.Body,
            Padding = new Thickness(10, 4),
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
            FontSize = TypeScale.Body,
            Padding = new Thickness(10, 4),
        };

        var complaint = new TextBlock
        {
            Name = "RecordingKept",
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(complaint, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        keep.Click += (_, _) =>
        {
            if (expected.Text is not { Length: > 0 } wanted || string.IsNullOrWhiteSpace(wanted))
            {
                // Said rather than silently ignored. An empty expectation is the one thing that
                // would make a kept case worthless, and a dead button says nothing about why.
                complaint.Text = mishear
                    ? "Type what you actually said first — that is the half the recording cannot supply."
                    : "Type the phonemes it should have been said as first.";

                return;
            }

            var kind = mishear ? RecordingKeepKind.Mishear : RecordingKeepKind.Pronunciation;

            _selected = _log.Keep(row.Id, kind, wanted.Trim(), _now());
            Refresh();
        };

        _detail.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { play, expected, keep },
        });

        _detail.Children.Add(complaint);
    }

    /// <summary>
    /// Where the row came from, in one line. Every field it has something to say about, and
    /// nothing for the ones it does not — a said row has no transcription model and a heard row
    /// has no voice, and printing "(none)" for either is noise in a line meant to be scanned.
    /// </summary>
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

    /// <summary>
    /// Hands a path to the shell. A clip is a plain WAV so that whatever the Commander already
    /// plays audio with is what opens it — a second audio path inside d47, drawing its own
    /// transport, would be a way for the review surface to lie about what is in the file.
    /// </summary>
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

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        return block;
    }

    private static Control Muted(string text, bool selectable = false)
    {
        if (!selectable)
        {
            var block = new TextBlock
            {
                Text = text,
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
            };

            Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

            return block;
        }

        var selectableBlock = new SelectableTextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(selectableBlock, SelectableTextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        return selectableBlock;
    }

    private static TextBlock Heading(string text)
    {
        var heading = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Heading,
            FontWeight = FontWeight.Medium,
        };

        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        return heading;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
