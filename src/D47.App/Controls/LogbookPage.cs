using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Theming;
using static D47.App.Theming.ThemedText;
using D47.Core.Capabilities;
using D47.Core.Logbook;

namespace D47.App.Controls;

/// <summary>Writing up a session, and reading back what has been written (Phase 33).</summary>
public sealed class LogbookPage : DialogPage
{
    public override string Crumb => "Logbook";

    private readonly LogbookBook _book;
    private readonly Segment _span;
    private readonly Button _from;
    private readonly Button _to;
    private DateOnly? _fromDay;
    private DateOnly? _toDay;
    private readonly CheckBox _exact;
    private readonly Button _estimate;
    private readonly Button _write;
    private readonly TextBlock _quote;
    private readonly StackPanel _entries = new() { Spacing = Gaps.Tile };

    private CancellationTokenSource? _running;

    // The page's own dispatcher, taken on the UI thread at construction.
    private readonly Avalonia.Threading.Dispatcher _dispatcher = Avalonia.Threading.Dispatcher.UIThread;

    public LogbookPage(LogbookBook book)
    {
        ArgumentNullException.ThrowIfNull(book);

        _book = book;

        Title = "Your Commander's log";

        _span = new Segment
        {
            Name = "LogSpan",
            ItemsSource = [.. LogRanges.Ids.Select(LogRanges.LabelOf)],
            SelectedIndex = 0,
            MinWidth = 220,
        };

        (_exact, _) = LabeledCheckBox.Build("Between two dates instead");
        _exact.Name = "LogExactDates";
        _from = new Button { Name = "LogFrom", Content = "FROM", IsEnabled = false, MinWidth = 200, MinHeight = 44 };
        _to = new Button { Name = "LogTo", Content = "TO", IsEnabled = false, MinWidth = 200, MinHeight = 44 };
        _from.Click += (_, _) => ChooseDay(from: true);
        _to.Click += (_, _) => ChooseDay(from: false);

        _exact.IsCheckedChanged += (_, _) =>
        {
            var exact = _exact.IsChecked == true;
            _span.IsEnabled = !exact;
            _from.IsEnabled = exact;
            _to.IsEnabled = exact;
            _book.Disarm();
        };

        _span.SelectionChanged += (_, _) => _book.Disarm();

        _quote = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = TypeScale.Secondary,
            Text =
                "Nothing is written until you ask. Working out the cost first is optional: it reads your "
                + "journals on this machine and spends nothing.",
        };

        Themed(_quote, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _estimate = new Button { Name = "EstimateLog", Content = "Work out what this would cost", MinWidth = 230 };
        _write = new Button { Name = "WriteLog", Content = "Write it", MinWidth = 130 };

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
                new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile, Children = { _from, _to } },
                new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile, Children = { _estimate, _write } },
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

        var (span, from, to) = Window();

        // Off the UI thread, because a month of journals is a real read and Core is synchronous by design —
        // the App is what decides where a long fold runs, exactly as habit mining does.
        var message = await Task.Run(() =>
        {
            try
            {
                _book.Estimate(span, from, to, out var described);

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

    private void ChooseDay(bool from)
    {
        if (this.FindAncestorOfType<Panel.PanelView>()?.Prompts is not { } prompts)
        {
            return;
        }

        var word = from ? "From" : "To";
        var current = from ? _fromDay : _toDay;
        var button = from ? _from : _to;

        prompts.Pick(
            $"logbook-day:{word}",
            word,
            new PickerRequest
            {
                Prompt = word,
                Choices = [.. _book.JournalDays().Select(LogDays.Write)],
                Describe = value => LogDays.Parse(value) is { } day ? $"{value}, {LogDays.Weekday(day)}" : value,
                Current = current is { } c ? LogDays.Write(c) : null,
                AllowsFreeText = true,
                WhyEmpty = "There are no journals for this Commander to take a day from. You can still type one, like 2026-09-14.",
            },
            result => TakeDay(from, result.Value));

        button.Focus();
    }

    /// <summary>Sets one end of the range from a chosen or typed day, or says the form when it is not one.</summary>
    private void TakeDay(bool from, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (LogDays.Parse(value) is not { } day)
        {
            _quote.Text = LogDays.NotADay;
            return;
        }

        if (from)
        {
            _fromDay = day;
            _from.Content = $"FROM {LogDays.Write(day)}";
        }
        else
        {
            _toDay = day;
            _to.Content = $"TO {LogDays.Write(day)}";
        }

        _book.Disarm();
    }

    /// <summary>The window the page is set to: a span, or two exact days.</summary>
    private (string? Span, DateTimeOffset? From, DateTimeOffset? To) Window()
    {
        if (_exact.IsChecked != true)
        {
            return (LogRanges.Ids[Math.Max(0, _span.SelectedIndex)], null, null);
        }

        var zone = TimeZoneInfo.Local;

        return (
            null,
            _fromDay is { } start ? LogDays.Start(start, zone) : null,
            _toDay is { } end ? LogDays.End(end, zone) : null);
    }

    private async Task WriteAsync()
    {
        _write.IsEnabled = false;
        _estimate.IsEnabled = false;
        _quote.Text = "Writing. This is the largest thing D47 asks a model to do, so it takes a moment.";

        _running?.Cancel();
        _running = new CancellationTokenSource();

        var token = _running.Token;
        var (span, from, to) = Window();

        // Off the UI thread, because an unquoted write reads the journals before it sends anything.
        var message = await Task.Run(async () =>
        {
            try
            {
                return (await _book.WriteAsync(span, from, to, token)).Message;
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

    private void OpenFolder()
    {
        try
        {
            _book.Folder.Files.CreateFolder(_book.Folder.Folder);

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
        _write.IsEnabled = _estimate.IsEnabled;

        if (_book.Armed is { } armed)
        {
            _write.Content = new TextBlock { Inlines = [new Run("WRITE IT — "), Fonts.Mono(armed.Price.ToUpperInvariant())] };
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
                $"Nothing written yet. Logs go to {_book.Folder.WriteFolder} as plain markdown, and D47 never "
                + "overwrites one."));

            return;
        }

        foreach (var entry in entries.Take(20))
        {
            _entries.Children.Add(Row(entry));
        }

        if (entries.Count > 20)
        {
            var more = Muted($"and {entries.Count - 20} more in the folder");
            more.Margin = new Thickness(0, 4, 0, 0);
            _entries.Children.Add(more);
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

        var written = ListRow.Aside(new TextBlock
        {
            Inlines = [Fonts.Mono($"{entry.Written.ToLocalTime():d MMM yyyy HH:mm}")],
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

        return ListRow.Dress(new Border { Child = row });
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
