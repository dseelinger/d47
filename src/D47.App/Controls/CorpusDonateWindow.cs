using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using D47.App.Donation;
using D47.App.Theming;
using D47.Core.Diagnostics.Donation;

namespace D47.App.Controls;

/// <summary>
/// The consent step for a whole journal history
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// <para>
/// <b>Deliberately not <see cref="DonateExcerptWindow"/> with a wider dial.</b> That window shows
/// the payload and asks the Commander to say yes to what they read, and it stops at half a day
/// because reading stops there. A corpus is 356 MB and 712,000 events; the same window pointed at
/// it would ask for a yes to something nobody could have read, which is the consent form this
/// feature exists not to be.
/// </para>
/// <para>
/// <b>So what is shown is <see cref="CorpusReport"/>, and the payload is never shown at all.</b>
/// The report is sized by the number of distinct event <i>kinds</i> — a couple of hundred — and
/// carries one real scrubbed instance of each, lifted from the payload rather than rebuilt for
/// display. Every kind is seen; most of the volume is not; the document says so in those words.
/// </para>
/// <para>
/// <b>Changing the scope throws the report away.</b> A report describing one range beside a Save
/// that would write another is the one failure this window must not have — it would be a yes to a
/// document that does not describe what left.
/// </para>
/// <para>
/// <b>Since <a href="https://github.com/dseelinger/d47/issues/181">#181</a> it can also send.</b>
/// <a href="https://github.com/dseelinger/d47/issues/174">#174</a> shipped this consent flow before
/// the transport existed and #175 built the transport without reaching into this window, so the
/// endpoint road stopped one call short of the payload the whole thing was built to carry. The
/// button is the one the excerpt window already has, on the same rules and in the same words: it
/// appears only when an address is set, it sends only on the press, and it sends every time.
/// </para>
/// <para>
/// <b>The Save button is not replaced.</b> Upload became the default action, not the only one —
/// and the two share one code path, so the file a Commander could have saved and the bytes that
/// leave are the same bytes rather than two renderings of the same intent.
/// </para>
/// <para>
/// With no address set this window is the window that shipped before #181: it writes a file the
/// Commander picked, names no destination, and says nothing reaches a network — which is then
/// true.
/// </para>
/// </summary>
public sealed class CorpusDonateWindow : Window
{
    private const string SendLabel = "Send it";

    private readonly Func<CorpusScope, IProgress<int>, CancellationToken, Task<CorpusReading>> _read;
    private readonly Func<Stream, IProgress<int>, CancellationToken, Task> _write;

    /// <summary>
    /// The send, or null where nothing composed one. <b>Null is a real state and not a test
    /// convenience</b>, exactly as it is in <see cref="DonateExcerptWindow"/>: with no address
    /// configured there is nowhere to send, and a button that explained why it could not work
    /// would be worse than no button.
    /// </summary>
    private readonly Func<string, IProgress<DonationStep>, CancellationToken, Task<DonationSent>>? _send;

    private readonly string? _destination;

    private readonly ComboBox _scope = new()
    {
        Name = "Scope",
        ItemsSource = CorpusScope.All,
        SelectedIndex = 0,
        MinWidth = 190,
        VerticalAlignment = VerticalAlignment.Center,
    };

    // Named, like the excerpt window's controls beside them, because these are what a test drives
    // to assert that the report on screen describes the range the Save would write.
    private readonly Button _read_ = new() { Name = "ReadJournals", Content = "Read my journals", MinWidth = 160 };
    private readonly Button _save = new() { Name = "SaveCorpus", Content = "Save the donation…", MinWidth = 170, IsEnabled = false };
    private readonly Button _stop = new() { Name = "StopCorpus", Content = "Cancel", MinWidth = 110 };

    // Named like the two above it, because a test drives this one to assert that the bytes on the
    // wire come out of the same writer the Save button uses.
    private readonly Button _sendButton = new() { Name = "SendCorpus", Content = SendLabel, MinWidth = 150, IsEnabled = false };

    private readonly SelectableTextBlock _preview = new()
    {
        Name = "CorpusReport",
        FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
        FontSize = TypeScale.Small,

        // Wrapped, for the reason DonateExcerptWindow records: the paragraphs a Commander has to
        // read before saying yes must not run off the right edge, whatever that does to a JSON
        // sample below them.
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly TextBlock _status = new() { FontSize = TypeScale.Small, VerticalAlignment = VerticalAlignment.Center };

    private CancellationTokenSource? _running;
    private CorpusReading? _reading;

    /// <param name="read">
    /// Surveys the history and renders the report. Runs off the UI thread — a real corpus is 936
    /// files and takes about fifteen seconds.
    /// </param>
    /// <param name="write">
    /// Writes the payload to a stream the Commander chose. <b>A second pass over the same files
    /// with the same stand-ins</b>, which is what makes the samples in the report the lines in the
    /// payload — see <see cref="CorpusDonation"/>.
    /// </param>
    /// <param name="send">
    /// Sends the payload, or null where there is nowhere to send it. Takes the report that is on
    /// screen — the artefact the Commander actually read and said yes to — and does its own
    /// writing through the same delegate as <paramref name="write"/>, which is what makes the file
    /// this window could have saved and the bytes that leave one code path rather than two.
    /// </param>
    /// <param name="destination">Where a send would go, named on screen before it happens.</param>
    public CorpusDonateWindow(
        Func<CorpusScope, IProgress<int>, CancellationToken, Task<CorpusReading>> read,
        Func<Stream, IProgress<int>, CancellationToken, Task> write,
        Func<string, IProgress<DonationStep>, CancellationToken, Task<DonationSent>>? send = null,
        string? destination = null)
    {
        _read = read;
        _write = write;
        _send = send;
        _destination = destination;

        Title = "Donate your journal history";
        Width = 900;
        Height = 720;
        MinWidth = 560;
        MinHeight = 420;
        CanResize = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);
        Themed(_preview, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        Themed(_status, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var root = new DockPanel { Margin = new Thickness(20) };

        var lede = Lede();
        var options = Options();
        var footer = Footer();

        DockPanel.SetDock(lede, Dock.Top);
        DockPanel.SetDock(options, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);

        var pane = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = new ScrollViewer
            {
                Name = "CorpusScroller",
                Content = _preview,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
        };

        Themed(pane, Border.BorderBrushProperty, ThemeManager.BorderKey);

        root.Children.Add(lede);
        root.Children.Add(options);
        root.Children.Add(footer);
        root.Children.Add(pane);

        Content = root;

        _scope.SelectionChanged += (_, _) => Discard();
        _read_.Click += async (_, _) => await ReadAsync();
        _save.Click += async (_, _) => await SaveAsync();
        _sendButton.Click += async (_, _) => await SendAsync();
        _stop.Click += (_, _) => Stop();

        // A send in flight is a request against a daily ceiling and tens of megabytes half written
        // at the store. Closing the window is the Commander saying they are done with it, not a
        // reason to leave one running with nowhere to report back to.
        Closed += (_, _) => _running?.Cancel();

        Discard();
    }

    /// <summary>What a reading produced: the document to show, and its own account of itself.</summary>
    public sealed record CorpusReading(CorpusSurvey Survey, string Report);

    /// <summary>
    /// What a Commander reads before they press anything.
    /// <para>
    /// <b>The last sentence used to be "nothing here goes to a network", and it stopped being true
    /// the moment an address could be set</b> (<a href="https://github.com/dseelinger/d47/issues/181">#181</a>).
    /// It is still said, word for word, when there is nowhere to send — because it is still true
    /// then, and a window that hedged about a network it cannot reach would be a worse disclosure
    /// than one that states the fact. With an address set it says where instead, and what the
    /// donor can do about it afterwards.
    /// </para>
    /// </summary>
    private Control Lede()
    {
        var where = _destination is { } destination
            ? "Nothing is written or sent until you press. Sending it puts it in Directive 47's "
              + "own store at " + destination + " — one press, nothing standing, nothing "
              + "remembered. A journal history is kept indefinitely, because a regression case "
              + "that expires stops being one; asking for it back is one press of Forget in "
              + "Privacy and egress, and it does not need you to post anywhere. d47 keeps the "
              + "report you are reading, and the hash of what it sent, in data\\donations."
            : "Nothing is written or sent until you save it, and nothing here goes to a network: "
              + "no donation address is set, so where the file goes afterwards is yours.";

        var lede = new TextBlock
        {
            Text = "This reads every Elite journal on disk, scrubs it the same way an incident "
                   + "excerpt is scrubbed, and then shows you a report about it rather than the "
                   + "thing itself — because a full history runs to hundreds of megabytes and nobody "
                   + "reads that. The report names every kind of event in the donation and shows a "
                   + "real scrubbed line of each.\n\n"
                   + where
                   + (_destination is null
                       ? string.Empty
                       : "\n\nA random number identifying this installation — not you, and not "
                         + "derived from your Commander name — goes with a send, so a history you "
                         + "add to is recognisable as the same history. Deleting "
                         + "data\\donor-token.txt stops that."),
            TextWrapping = TextWrapping.Wrap,
            FontSize = TypeScale.Secondary,
            Margin = new Thickness(0, 0, 0, 12),
        };

        Themed(lede, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        return lede;
    }

    private Control Options()
    {
        var row = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12),
        };

        row.Children.Add(Labelled("How much", _scope));
        row.Children.Add(_read_);

        return row;
    }

    private Control Footer()
    {
        // Send last, where the default action sits, and the save beside it rather than instead of
        // it. With nowhere to send, this row is exactly the row that shipped before #181.
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _stop, _save },
        };

        if (_send is not null)
        {
            buttons.Children.Add(_sendButton);
        }

        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };

        DockPanel.SetDock(buttons, Dock.Right);

        footer.Children.Add(buttons);
        footer.Children.Add(_status);

        return footer;
    }

    /// <summary>
    /// Throws away a reading. <b>Called whenever the scope changes</b>, because a report describing
    /// twelve months beside a Save that would write everything is a yes to the wrong document.
    /// </summary>
    private void Discard()
    {
        _reading = null;
        _save.IsEnabled = false;

        // **A changed scope is a fresh decision about the send too**, and the button says so
        // rather than keeping the word "Sent" over a report that no longer describes what left.
        _sendButton.IsEnabled = false;
        _sendButton.Content = SendLabel;

        _preview.Text =
            "Nothing has been read yet.\n\n"
            + "Choose how much of your history to include, then press Read my journals. "
            + "Reading a full history takes a few seconds and happens entirely on this machine.";

        _status.Text = string.Empty;
    }

    private async Task ReadAsync()
    {
        var scope = _scope.SelectedItem as CorpusScope ?? CorpusScope.Default;

        _running?.Cancel();
        _running = new CancellationTokenSource();

        var token = _running.Token;

        Busy(true);
        _status.Text = "Reading…";

        var progress = new Progress<int>(files => _status.Text = $"Reading — {files:N0} journal files so far");

        try
        {
            _reading = await _read(scope, progress, token);
            _preview.Text = _reading.Report;
            _save.IsEnabled = true;
            _sendButton.IsEnabled = true;

            var survey = _reading.Survey;

            _status.Text =
                $"{survey.Tally.Events:N0} events across {survey.Files:N0} files, "
                + $"{survey.Kinds.Count:N0} kinds — the report above is what you are agreeing to";
        }
        catch (OperationCanceledException)
        {
            Discard();
            _status.Text = "Stopped. Nothing was written.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Discard();
            _status.Text = "Could not read the journals.";
        }
        finally
        {
            Busy(false);
        }
    }

    /// <summary>
    /// The yes. <b>The second pass runs here</b> rather than at read time, so nothing exists on
    /// disk until the Commander has chosen where — the survey holds counts and one line per kind,
    /// never the payload.
    /// </summary>
    private async Task SaveAsync()
    {
        if (_reading is not { } reading)
        {
            return;
        }

        if (StorageProvider is not { CanSave: true } storage)
        {
            _status.Text = "No file picker here.";
            return;
        }

        var stamp = reading.Survey.Last ?? DateTimeOffset.UtcNow;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save your journal history",
            SuggestedFileName = $"d47-journal-history-{stamp.ToUniversalTime():yyyy-MM-dd}.jsonl",
            DefaultExtension = "jsonl",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON Lines") { Patterns = ["*.jsonl"] },
            ],
        });

        if (file is null)
        {
            return;
        }

        _running?.Cancel();
        _running = new CancellationTokenSource();

        var token = _running.Token;

        Busy(true);
        _save.IsEnabled = false;
        _sendButton.IsEnabled = false;

        var progress = new Progress<int>(files => _status.Text = $"Writing — {files:N0} journal files so far");

        try
        {
            await using var stream = await file.OpenWriteAsync();

            await _write(stream, progress, token);

            _status.Text = $"Saved to {file.Name}. It is on your machine and nowhere else.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Stopped part way. The file it was writing is incomplete.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = "Could not write it.";
        }
        finally
        {
            Busy(false);
            _save.IsEnabled = _reading is not null;
            _sendButton.IsEnabled = _reading is not null;
        }
    }

    /// <summary>
    /// The other yes, and the one #181 made the default. <b>Sends what the report on screen
    /// describes</b>, assembled by the same writer the Save button uses — the corpus form of "one
    /// rendering, used twice", which here means one payload writer used two ways rather than a
    /// second code path producing something the Commander never read a report about.
    /// <para>
    /// <b>The window does not close on it.</b> A send has an outcome the Commander has to be able
    /// to read, and one that names a receipt they may want to go and find. Closing over the top of
    /// it would make a refused donation indistinguishable from a stored one.
    /// </para>
    /// </summary>
    private async Task SendAsync()
    {
        if (_send is not { } send || _reading is not { } reading)
        {
            return;
        }

        _running?.Cancel();
        _running = new CancellationTokenSource();

        var token = _running.Token;

        Busy(true);
        _sendButton.IsEnabled = false;
        _sendButton.Content = "Sending…";
        _save.IsEnabled = false;

        var progress = new Progress<DonationStep>(step => _status.Text = step.Sending
            ? "Sending. Nothing else is being sent, and nothing is being kept anywhere else."
            : $"Preparing the donation — {step.Files:N0} journal files so far");

        var landed = false;

        try
        {
            var sent = await send(reading.Report, progress, token);

            landed = sent.Outcome.Sent;
            _sendButton.Content = landed ? "Sent" : SendLabel;

            _status.Text = sent.Receipt is { } receipt
                ? $"{sent.Outcome.Said} Your own copy of what you agreed to is in {receipt}."
                : $"{sent.Outcome.Said} d47 could not write its own copy of it.";
        }
        catch (OperationCanceledException)
        {
            _sendButton.Content = SendLabel;
            _status.Text = "Stopped. Nothing was confirmed as stored.";
        }
        finally
        {
            Busy(false);
            _save.IsEnabled = _reading is not null;

            // Offered again only where it did not land. A "Sent" button that invites a second
            // press is a second thirty-megabyte donation nobody asked for.
            _sendButton.IsEnabled = _reading is not null && !landed;
        }
    }

    /// <summary>
    /// Cancels what is running, or closes where nothing is — the one button a Commander reaches
    /// for when they have changed their mind, whichever of the two things they meant.
    /// </summary>
    private void Stop()
    {
        if (_running is { IsCancellationRequested: false } running)
        {
            running.Cancel();
            return;
        }

        Close();
    }

    private void Busy(bool busy)
    {
        _scope.IsEnabled = !busy;
        _read_.IsEnabled = !busy;
        _stop.Content = busy ? "Stop" : "Cancel";
    }

    private Control Labelled(string caption, Control control)
    {
        var label = new TextBlock
        {
            Text = caption,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };

        Themed(label, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 16, 0),
            Children = { label, control },
        };
    }

    private IDisposable Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));
}
