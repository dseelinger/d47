using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Logbook;

namespace D47.App.Controls;

/// <summary>Writing up a session, and reading back what has been written (Phase 33).</summary>
public sealed class LogbookWindow : Window
{
    private readonly LogbookBook _book;
    private readonly Segment _span;
    private readonly DatePicker _from;
    private readonly DatePicker _to;
    private readonly CheckBox _exact;
    private readonly Button _estimate;
    private readonly Button _write;
    private readonly TextBlock _quote;
    private readonly StackPanel _entries = new() { Spacing = 6 };

    private CancellationTokenSource? _running;

    // The window's own dispatcher, taken on the UI thread at construction.
    private readonly Avalonia.Threading.Dispatcher _dispatcher = Avalonia.Threading.Dispatcher.UIThread;

    public LogbookWindow(LogbookBook book)
    {
        ArgumentNullException.ThrowIfNull(book);

        _book = book;

        Title = "Your Commander's log";
        Width = 660;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _span = new Segment
        {
            Name = "LogSpan",
            ItemsSource = [.. LogRanges.Ids.Select(LogRanges.LabelOf)],
            SelectedIndex = 0,
            MinWidth = 220,
        };

        (_exact, _) = LabeledCheckBox.Build("Between two dates instead");
        _exact.Name = "LogExactDates";
        _from = new DatePicker { IsEnabled = false, MinWidth = 200 };
        _to = new DatePicker { IsEnabled = false, MinWidth = 200 };

        _exact.IsCheckedChanged += (_, _) =>
        {
            var exact = _exact.IsChecked == true;
            _span.IsEnabled = !exact;
            _from.IsEnabled = exact;
            _to.IsEnabled = exact;
        };

        _quote = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = TypeScale.Secondary,
            Text =
                "Nothing is written until you ask, and nothing is sent until you have seen what it costs. "
                + "Working it out reads your journals on this machine and spends nothing.",
        };

        Themed(_quote, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _estimate = new Button { Name = "EstimateLog", Content = "Work out what this would cost", MinWidth = 230 };
        _write = new Button { Name = "WriteLog", Content = "Write it", MinWidth = 130, IsEnabled = false };

        _estimate.Click += async (_, _) => await EstimateAsync();
        _write.Click += async (_, _) => await WriteAsync();

        var open = new Button { Name = "OpenLogFolder", Content = "Open the folder", MinWidth = 150 };
        open.Click += (_, _) => OpenFolder();

        var close = new Button { Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        var body = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                Modal.Section("Write up a session"),
                Muted(
                    "D47 reads your flight journal, works out what it can account for, and hands a language "
                    + "model the facts — never the journal itself. Every sentence it writes has to point at "
                    + "one of those facts, and the ones that do not are marked in the file."),
                _span,
                _exact,
                new WrapPanel { ItemSpacing = 10, LineSpacing = 10, Children = { _from, _to } },
                new WrapPanel { ItemSpacing = 10, LineSpacing = 10, Children = { _estimate, _write } },
                _quote,
                Modal.Section("What you have written"),
                _entries,
            },
        };

        Modal.Apply(this, "Logbook", Title, body, [open, close]);

        book.Changed += OnChanged;
        Closed += (_, _) =>
        {
            book.Changed -= OnChanged;
            _running?.Cancel();
        };

        Refresh();
    }

    private void OnChanged() => _dispatcher.Post(Refresh);

    private async Task EstimateAsync()
    {
        _estimate.IsEnabled = false;
        _quote.Text = "Reading your journals…";

        var exact = _exact.IsChecked == true;
        var span = LogRanges.Ids[Math.Max(0, _span.SelectedIndex)];
        var from = exact ? _from.SelectedDate : null;
        var to = exact ? _to.SelectedDate : null;

        // Off the UI thread, because a month of journals is a real read and Core is synchronous by design —
        // the App is what decides where a long fold runs, exactly as habit mining does.
        var message = await Task.Run(() =>
        {
            try
            {
                _book.Estimate(
                    exact ? null : span,
                    from is { } start ? new DateTimeOffset(start.Date, start.Offset) : null,
                    to is { } end ? new DateTimeOffset(end.Date.AddDays(1).AddSeconds(-1), end.Offset) : null,
                    out var described);

                return described;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"I could not read your journals: {ex.Message}";
            }
        });

        _quote.Text = message;
        _estimate.IsEnabled = true;
        Refresh();
    }

    private async Task WriteAsync()
    {
        if (_book.Armed is null)
        {
            return;
        }

        _write.IsEnabled = false;
        _estimate.IsEnabled = false;
        _quote.Text = "Writing. This is the largest thing D47 asks a model to do, so it takes a moment.";

        _running?.Cancel();
        _running = new CancellationTokenSource();

        var outcome = await _book.WriteAsync(_running.Token);

        _quote.Text = outcome.Message;
        _estimate.IsEnabled = true;
        Refresh();
    }

    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(_book.Folder.Folder);

            Process.Start(new ProcessStartInfo
            {
                FileName = _book.Folder.Folder,
                UseShellExecute = true,
            })?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _quote.Text = $"I could not open {_book.Folder.Folder}: {ex.Message}";
        }
    }

    private void Refresh()
    {
        _write.IsEnabled = _book.Armed is not null;

        if (_book.Armed is { } armed)
        {
            _write.Content = $"Write it — {armed.Price}";
        }
        else
        {
            _write.Content = "Write it";
        }

        _entries.Children.Clear();

        var entries = _book.Folder.Entries();

        if (entries.Count == 0)
        {
            _entries.Children.Add(Muted(
                $"Nothing written yet. Logs go to {_book.Folder.Folder} as plain markdown, and D47 never "
                + "overwrites one."));

            return;
        }

        foreach (var entry in entries.Take(20))
        {
            _entries.Children.Add(Row(entry));
        }

        if (entries.Count > 20)
        {
            _entries.Children.Add(Muted($"and {entries.Count - 20} more in the folder"));
        }
    }

    private Control Row(LogEntry entry)
    {
        var open = new Button { Content = "Open", MinWidth = 90 };

        open.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = entry.Path, UseShellExecute = true })?.Dispose();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                _quote.Text = $"I could not open {entry.Name}: {ex.Message}";
            }
        };

        var written = ListRow.Secondary(new TextBlock
        {
            Text = $"{entry.Written.ToLocalTime():d MMM yyyy HH:mm}",
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0),
        });

        var name = ListRow.Name(new SelectableTextBlock
        {
            Text = entry.Name,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var row = new DockPanel();
        DockPanel.SetDock(open, Dock.Right);
        DockPanel.SetDock(written, Dock.Right);
        row.Children.Add(open);
        row.Children.Add(written);
        row.Children.Add(name);

        return ListRow.Dress(new Border { Padding = new Thickness(12, 6), Child = row });
    }

    private static TextBlock Muted(string text, string key = ThemeManager.GreyKey)
    {
        var block = new TextBlock { Text = text, FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
