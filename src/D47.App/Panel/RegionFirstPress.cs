using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Voice;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>The REPORT TO CANONN press on the codex pages: calls <c>report_region_first</c> as the Commander and keeps each record's report state.</summary>
public sealed class RegionFirstPress(
    Func<string, ToolArguments, CancellationToken, Task<ToolResult>> invoke,
    RegionFirstBook book,
    Func<CommanderGameState?> state,
    WaitSound? waitSound = null)
{
    public const string TileLabel = "Report to Canonn";

    public const string RetryLabel = "Retry";

    public const string SentText = "✓ Sent to Canonn.";

    public const string FailedLabel = "Report not sent";

    private readonly HashSet<(long Entry, int Region)> _sending = [];
    private readonly HashSet<(long Entry, int Region)> _failed = [];
    private readonly HashSet<(long Entry, int Region)> _sent = [];
    private readonly Dictionary<(long Entry, int Region), WaitingState> _marks = [];

    private long _version;

    /// <summary>Changes whenever a press starts or ends; a page that draws the press redraws on it.</summary>
    public long Version => _version;

    /// <summary>Whether the record can be offered: unreported, with its journal line and position.</summary>
    public static bool Offers(RegionFirstRecord record) =>
        record is { Reported: null, Journal.Length: > 0, StarPos: not null };

    /// <summary>The band's tile slot: the tile, the disabled tile with the wait mark, or the sent line. Null when nothing is offered.</summary>
    public Control? Slot(RegionFirstRecord record)
    {
        var key = KeyOf(record);

        if (_sent.Contains(key))
        {
            var sent = CreditsKit.Line(SentText, Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.BlueKey);
            sent.FontWeight = FontWeight.SemiBold;
            sent.VerticalAlignment = VerticalAlignment.Center;
            sent.Name = "CodexReportSent";

            return sent;
        }

        return Offers(record) ? Tile(record) : null;
    }

    /// <summary>The red Notice for a record whose last press failed, or null.</summary>
    public Notice? Failure(RegionFirstRecord record, bool inline)
    {
        if (!_failed.Contains(KeyOf(record)) || !Offers(record))
        {
            return null;
        }

        return new Notice(NoticeLevel.Error, inline)
        {
            Name = "CodexReportFailed",
            Label = FailedLabel,
            Text = RegionFirstReporting.Unreachable,
        };
    }

    /// <summary>The tile, or the retry tile after a failure, with the wait mark beside it while a press runs.</summary>
    public Control Tile(RegionFirstRecord record)
    {
        var key = KeyOf(record);
        var sending = _sending.Contains(key);

        var button = new Button
        {
            Name = "CodexReport",
            Content = _failed.Contains(key) ? RetryLabel : TileLabel,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(18, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = !sending,
        };
        button.Click += (_, _) => _ = PressAsync(record);

        var line = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { button },
        };

        if (sending && _marks.TryGetValue(key, out var mark))
        {
            (mark.Parent as Avalonia.Controls.Panel)?.Children.Remove(mark);
            mark.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(mark);
        }

        return line;
    }

    /// <summary>Calls the tool off the UI thread, then reads the store to tell sent from failed.</summary>
    public async Task PressAsync(RegionFirstRecord record)
    {
        var key = KeyOf(record);

        if (state()?.Identity.FrontierId is not { Length: > 0 } frontierId || !_sending.Add(key))
        {
            return;
        }

        _failed.Remove(key);

        if (!_marks.TryGetValue(key, out var mark))
        {
            _marks[key] = mark = new WaitingState(WaitForm.Inline) { IsVisible = false };
        }

        _version++;

        var arguments = new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["entry_id"] = record.EntryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["region_id"] = record.RegionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

        try
        {
            await Waiting.While(mark, waitSound, () => Task.Run(() => invoke(RegionFirstReporting.Tool, arguments, CancellationToken.None))).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A tool that throws is a send that failed; the store decides below.
        }

        _sending.Remove(key);

        if (book.Find(frontierId, record.EntryId, record.RegionId)?.Reported is not null)
        {
            _sent.Add(key);
        }
        else
        {
            _failed.Add(key);
        }

        _version++;
    }

    private static (long, int) KeyOf(RegionFirstRecord record) => (record.EntryId, record.RegionId);
}
