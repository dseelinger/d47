using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using D47.App.Donation;
using D47.App.Theming;
using D47.Core.Diagnostics.Donation;

namespace D47.App.Controls;

/// <summary>
/// One window for sharing what the ship saw, in either of two shapes
/// (<a href="https://github.com/dseelinger/d47/issues/238">#238</a>): an incident excerpt, or —
/// with <c>Include journal history</c> on — the whole journal history. Named for what it is for
/// (<a href="https://github.com/dseelinger/d47/issues/239">#239</a>): "Donate" read as a request
/// for money or a kidney, and what this window asks for is help improving d47.
/// <para>
/// <b>One surface, two consents — the merge deliberately overturns only the first half.</b>
/// <c>CorpusDonateWindow</c> recorded that a history is "deliberately not the excerpt window
/// with a wider dial", and the reason stands: an excerpt is read whole and the yes is a yes to
/// something read (#160, #173); a history is hundreds of megabytes nobody could read, so what is
/// shown is a report about it and the payload is never shown at all (#174). The toggle decides
/// which of those two flows this window runs, and each keeps its own rules — the excerpt
/// re-renders on every change and closes on the copy; the history throws its reading away on a
/// scope change and arms nothing until the journals have been read.
/// </para>
/// <para>
/// <b>The lede leads with why, then the three promises</b>
/// (<a href="https://github.com/dseelinger/d47/issues/240">#240</a>): voluntary, scrubbed,
/// removable. They were all already true and already said; they were buried mid-paragraph, and
/// the first thing a Commander reads should be the reason anyone would do this at all.
/// </para>
/// <para>
/// <b>The scale opens on the journal scale</b>
/// (<a href="https://github.com/dseelinger/d47/issues/241">#241</a>): with the history half
/// available the toggle starts on and the chooser shows the corpus scopes, gentlest first. The
/// excerpt spans are one toggle away and unchanged.
/// </para>
/// <para>
/// Everything consequential is ported, not re-decided: one rendering fills the pane and the
/// clipboard; a send is the string or the writer that was on screen; no standing consent, no
/// remembered choice, no auto-send; the window closes on a copy and stays open on a send so a
/// refused one is distinguishable from a stored one. Built in code rather than as an axaml pair,
/// like <see cref="SpendWindow"/> beside it.
/// </para>
/// </summary>
public sealed class HelpImproveWindow : Window
{
    private const string CopyLabel = "Copy it for the report";
    private const string SendLabel = "Send it";

    /// <summary>
    /// The point past which the excerpt consent wears thin, because the consent it asks for is
    /// <i>read this and say yes to it</i> (#173). Nobody reads sixty thousand characters, so a
    /// yes past this size would not be a yes to something read.
    /// </summary>
    private const int MostCharacters = 60_000;

    /// <summary>What a history reading produced: the document to show, and its own account of itself.</summary>
    public sealed record CorpusReading(CorpusSurvey Survey, string Report);

    private readonly Func<ExcerptRequest, string> _build;

    /// <summary>
    /// The sends, or null where nothing composed one. <b>Null is a real state and not a test
    /// convenience</b>: with no address configured there is nowhere to send, and the window
    /// offers the copy and the file exactly as it did before #175.
    /// </summary>
    private readonly Func<string, CancellationToken, Task<DonationSent>>? _send;

    private readonly Func<string, IProgress<DonationStep>, CancellationToken, Task<DonationSent>>? _sendCorpus;

    /// <summary>
    /// The history half, or null where this surface does not offer it — the Log page cuts an
    /// excerpt from a source a history does not read (#174), so there the toggle is absent and
    /// this window is the excerpt window it always was.
    /// </summary>
    private readonly Func<CorpusScope, IProgress<int>, CancellationToken, Task<CorpusReading>>? _read;

    private readonly Func<Stream, IProgress<int>, CancellationToken, Task>? _write;

    private readonly string? _destination;
    private readonly DateTimeOffset _markedAt;
    private readonly CancellationTokenSource _sending = new();

    /// <summary>
    /// The toggle the merge exists for (#238). Checked out of the box where the history is
    /// offered (#241): the journal scale is the opening offer, and the excerpt is one press away.
    /// </summary>
    private readonly CheckBox _includeHistory = new()
    {
        Name = "IncludeHistory",
        Content = "Include journal history",
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>How far back an excerpt reaches, in spans a person can name (#173).</summary>
    private readonly ComboBox _span = new()
    {
        Name = "Span",
        ItemsSource = ExcerptSpan.All,
        SelectedIndex = 0,
        MinWidth = 190,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>How much history goes, gentlest first (#241).</summary>
    private readonly ComboBox _scope = new()
    {
        Name = "Scope",
        ItemsSource = CorpusScope.All,
        SelectedIndex = 0,
        MinWidth = 190,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly CheckBox _mySpeech = new()
    {
        // Named, like the choosers and the panes, because these are what a test drives to
        // assert that the text on screen is the text on the clipboard.
        Name = "IncludeMySpeech",
        Content = "Include what I said out loud",
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly Button _read_ = new() { Name = "ReadJournals", Content = "Read my journals", MinWidth = 160 };

    private readonly SelectableTextBlock _preview = new()
    {
        Name = "Excerpt",
        FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
        FontSize = TypeScale.Small,

        // **Wrapped, though a payload reads better as the lines it is.** The paragraphs above
        // the payload — what was replaced, what was withheld, what is being agreed to — must
        // not run off the right edge, and a consent notice you have to scroll sideways to find
        // is worse than ugly.
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly SelectableTextBlock _corpusPreview = new()
    {
        Name = "CorpusReport",
        FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
        FontSize = TypeScale.Small,
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly TextBlock _lede = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = TypeScale.Secondary,
        Margin = new Thickness(0, 0, 0, 12),
    };

    private readonly TextBlock _size = new() { FontSize = TypeScale.Small, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { FontSize = TypeScale.Small, VerticalAlignment = VerticalAlignment.Center };

    private readonly Button _copy = new() { Name = "CopyExcerpt", Content = CopyLabel, MinWidth = 190 };
    private readonly Button _saveExcerpt = new() { Content = "Save a file instead…", MinWidth = 160 };
    private readonly Button _stop = new() { Name = "StopCorpus", Content = "Cancel", MinWidth = 110 };
    private readonly Button _saveCorpus = new() { Name = "SaveCorpus", Content = "Save it instead…", MinWidth = 160, IsEnabled = false };

    // Named, like the controls above, because a test drives these to assert that what is sent
    // is the artefact that was on screen.
    private readonly Button _sendButton = new() { Name = "SendExcerpt", Content = SendLabel, MinWidth = 190 };
    private readonly Button _sendCorpusButton = new() { Name = "SendCorpus", Content = SendLabel, MinWidth = 150, IsEnabled = false };

    private string _text = string.Empty;
    private IDisposable? _sizeColour;
    private CancellationTokenSource? _running;
    private CorpusReading? _reading;

    /// <param name="markedAt">
    /// The bookmark — when the Commander said this was the moment. Only the instant travels.
    /// </param>
    /// <param name="build">Cuts an excerpt window and renders it. Called again on every change here.</param>
    /// <param name="send">
    /// Sends the rendered excerpt — the text that is on screen, never a rebuild — or null where
    /// there is nowhere to send.
    /// </param>
    /// <param name="destination">Where a send would go, named on screen before it happens.</param>
    /// <param name="read">
    /// Surveys the history and renders its report, or null where this surface does not offer the
    /// history half. Runs off the UI thread — a real corpus is hundreds of files.
    /// </param>
    /// <param name="write">
    /// Writes the history payload to a stream the Commander chose. The same writer serves the
    /// Save button and the send (#181), which is what makes the file that could have been saved
    /// and the bytes that leave the same bytes.
    /// </param>
    /// <param name="sendCorpus">
    /// Sends the history — takes the report that was read and said yes to — or null where there
    /// is nowhere to send.
    /// </param>
    public HelpImproveWindow(
        DateTimeOffset markedAt,
        Func<ExcerptRequest, string> build,
        Func<string, CancellationToken, Task<DonationSent>>? send = null,
        string? destination = null,
        Func<CorpusScope, IProgress<int>, CancellationToken, Task<CorpusReading>>? read = null,
        Func<Stream, IProgress<int>, CancellationToken, Task>? write = null,
        Func<string, IProgress<DonationStep>, CancellationToken, Task<DonationSent>>? sendCorpus = null)
    {
        _markedAt = markedAt;
        _build = build;
        _send = send;
        _destination = destination;
        _read = read;
        _write = write;
        _sendCorpus = sendCorpus;

        Title = "Help improve D47";
        Width = 900;
        Height = 720;
        MinWidth = 560;
        MinHeight = 420;
        CanResize = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);
        Themed(_preview, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        Themed(_corpusPreview, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        Themed(_lede, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        Themed(_size, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        Themed(_status, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        // The history half exists only where both of its delegates do, and opens on when it
        // exists at all (#241): the journal scale is the default view, not the small print.
        _includeHistory.IsVisible = HistoryOffered;
        _includeHistory.IsChecked = HistoryOffered;

        var root = new DockPanel { Margin = new Thickness(20) };

        var options = Options();
        var footer = Footer();

        DockPanel.SetDock(_lede, Dock.Top);
        DockPanel.SetDock(options, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);

        // Vertical only, for the reason recorded on SpendWindow (#87): a ScrollViewer that may
        // scroll horizontally measures its content with unconstrained width, which makes the
        // wrapping above a no-op.
        var pane = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = new ScrollViewer
            {
                Name = "ExcerptScroller",
                Content = new StackPanel { Children = { _preview, _corpusPreview } },
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
        };

        Themed(pane, Border.BorderBrushProperty, ThemeManager.BorderKey);

        root.Children.Add(_lede);
        root.Children.Add(options);
        root.Children.Add(footer);
        root.Children.Add(pane);

        Content = root;

        _includeHistory.IsCheckedChanged += (_, _) => ApplyMode();
        _span.SelectionChanged += (_, _) => Render();
        _mySpeech.IsCheckedChanged += (_, _) => Render();
        _scope.SelectionChanged += (_, _) => Discard();
        _read_.Click += async (_, _) => await ReadAsync();
        _copy.Click += async (_, _) => await CopyAsync();
        _sendButton.Click += async (_, _) => await SendAsync();
        _saveExcerpt.Click += async (_, _) => await SaveExcerptAsync(_saveExcerpt);
        _saveCorpus.Click += async (_, _) => await SaveCorpusAsync();
        _sendCorpusButton.Click += async (_, _) => await SendCorpusAsync();
        _stop.Click += (_, _) => Stop();

        // A send in flight is a request against a daily ceiling and a payload half written at
        // the store. Closing the window is the Commander saying they are done with it.
        Closed += (_, _) =>
        {
            _sending.Cancel();
            _running?.Cancel();
        };

        ApplyMode();
    }

    /// <summary>The rendered excerpt as it stands. Public so a test can read what would be copied.</summary>
    internal string Text => _text;

    private bool HistoryOffered => _read is not null && _write is not null;

    private bool History => HistoryOffered && _includeHistory.IsChecked == true;

    /// <summary>
    /// Everything the toggle decides, in one place: which chooser, which pane, which buttons —
    /// and a fresh start for the flow being entered, because a consent begun under one mode must
    /// not be spent under the other.
    /// </summary>
    private void ApplyMode()
    {
        var history = History;

        _span.IsVisible = !history;
        _mySpeech.IsVisible = !history;
        _scope.IsVisible = history;
        _read_.IsVisible = history;

        _preview.IsVisible = !history;
        _corpusPreview.IsVisible = history;

        _size.IsVisible = !history;
        _status.IsVisible = history;

        _copy.IsVisible = !history;
        _saveExcerpt.IsVisible = !history;
        _sendButton.IsVisible = !history && _send is not null;

        _saveCorpus.IsVisible = history;
        _sendCorpusButton.IsVisible = history && _sendCorpus is not null;

        _lede.Text = LedeText(history);

        if (history)
        {
            Discard();
        }
        else
        {
            _running?.Cancel();
            Render();
        }
    }

    /// <summary>
    /// Why first, then the three promises, then the mechanics of the shape being offered
    /// (#240). The promises are the same three whichever way the toggle sits, because they were
    /// already true of both flows — the wording just stopped hiding them.
    /// </summary>
    private string LedeText(bool history)
    {
        var promises =
            "Real journals are how defects get found and fixed — sharing what your ship actually "
            + "saw is the most useful thing you can hand this project. Three things hold, "
            + "whichever way you share:\n"
            + "  •  Entirely voluntary — nothing is read, written or sent until you press, every "
            + "time. There is no standing consent and nothing is remembered.\n"
            + "  •  Scrubbed — your name and IDs are replaced, and other people's words are "
            + "dropped, before you ever see the result.\n"
            + "  •  Removable — if you change your mind, taking it back is one press in Privacy "
            + "and egress.\n\n";

        if (history)
        {
            var where = _destination is { } destination
                ? "Nothing is written or sent until you press. Sending it puts it in Directive 47's "
                  + "own store at " + destination + " — one press, nothing standing. A journal "
                  + "history is kept until you ask for it back, because a regression case that "
                  + "expires stops being one; asking is one press of Forget in Privacy and egress, "
                  + "and it does not need you to post anywhere. d47 keeps the report you are "
                  + "reading, and the hash of what it sent, in data\\donations."
                : "Nothing is written or sent until you save it, and nothing here goes to a "
                  + "network: no send address is set, so where the file goes afterwards is yours.";

            return promises
                   + "This reads your Elite journals as far back as the scale says, scrubs them, "
                   + "and then shows you a report about them rather than the thing itself — a "
                   + "history runs to hundreds of megabytes and nobody reads that. The report "
                   + "names every kind of event included and shows a real scrubbed line of each.\n\n"
                   + where
                   + (_destination is null
                       ? string.Empty
                       : "\n\nA random number identifying this installation — not you, and not "
                         + "derived from your Commander name — goes with a send, so a history you "
                         + "add to is recognisable as the same history. Deleting "
                         + "data\\donor-token.txt stops that.");
        }

        var excerptWhere = _destination is { } excerptDestination
            ? "Sending it puts it in Directive 47's own store at " + excerptDestination + " — one "
              + "press, nothing standing, nothing remembered. A rule on the store deletes it "
              + "after thirty days without anybody having to remember to, and asking for it "
              + "sooner is one press in Privacy and egress. d47 keeps its own copy of exactly "
              + "what it sent, and the hash, in data\\donations."
            : "Nothing can be sent from here: no send address is set. Copy it or save it, and "
              + "where it goes after that is yours — anything posted publicly can be archived "
              + "beyond anyone's reach.";

        return promises
               + "Everything below is what would go into the report — nothing else, and nothing "
               + "leaves until you press. Reach back as far as the defect needs, and read it.\n\n"
               + excerptWhere
               + "\n\n"
               + "A random number identifying this installation — not you, and not derived from "
               + "your Commander name — goes with a send, so what you share can be grouped into "
               + "one history. Deleting data\\donor-token.txt stops that.";
    }

    private Control Options()
    {
        var row = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12),
        };

        if (HistoryOffered)
        {
            row.Children.Add(new Border { Padding = new Thickness(0, 0, 16, 0), Child = _includeHistory });
        }

        // One visible label over whichever chooser the mode shows: the Commander's word for
        // this control is the scale (#241), and the two lists answer two different questions —
        // a window around an incident, and a reach of history — so they stay two controls.
        var scale = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _span, _scope },
        };

        row.Children.Add(Labelled("Scale", scale));
        row.Children.Add(new Border { Padding = new Thickness(8, 0, 0, 0), Child = _mySpeech });
        row.Children.Add(_read_);

        return row;
    }

    private Control Footer()
    {
        // Send last, where the default action sits, and the copy or save beside it rather than
        // instead of it: the upload became the default action, not the only one.
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _stop, _saveExcerpt, _copy, _saveCorpus },
        };

        if (_send is not null)
        {
            buttons.Children.Add(_sendButton);
        }

        if (_sendCorpus is not null)
        {
            buttons.Children.Add(_sendCorpusButton);
        }

        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };

        DockPanel.SetDock(buttons, Dock.Right);

        footer.Children.Add(buttons);
        footer.Children.Add(new StackPanel { Children = { _size, _status } });

        return footer;
    }

    /// <summary>
    /// Rebuilds the excerpt and shows it. Everything the Commander can change in the excerpt
    /// flow changes what would leave, so everything comes through this one call.
    /// </summary>
    private void Render()
    {
        if (History)
        {
            return;
        }

        var span = _span.SelectedItem as ExcerptSpan ?? ExcerptSpan.Default;

        _text = _build(span.Around(_markedAt, _mySpeech.IsChecked == true));
        _preview.Text = _text;

        // **A changed payload is a fresh decision.** The same rule the history flow enforces by
        // throwing its report away: a button reading "Sent" above an excerpt that is no longer
        // the one that was sent is the one failure a consent step must not have.
        _sendButton.Content = SendLabel;
        _sendButton.IsEnabled = true;

        var long_ = _text.Length > MostCharacters;

        // Names the real problem and no transport (#165): the yes this window asks for is a yes
        // to something read, and that is what stops being true at this size.
        _size.Text = long_
            ? $"{_text.Length:N0} characters — more than a person reads, so a yes to it would not "
              + "be a yes to something you read. Choose a shorter span."
            : $"{_text.Length:N0} characters";

        // Disposed before rebinding, or the subscriptions stack up and the last to fire decides.
        _sizeColour?.Dispose();
        _sizeColour = Themed(
            _size,
            TextBlock.ForegroundProperty,
            long_ ? ThemeManager.DangerKey : ThemeManager.TextMutedKey);
    }

    /// <summary>
    /// Throws away a history reading. <b>Called whenever the scope changes</b> — and on entering
    /// the history mode — because a report describing one range beside a Save that would write
    /// another is a yes to the wrong document.
    /// </summary>
    private void Discard()
    {
        if (!History)
        {
            return;
        }

        _reading = null;
        _saveCorpus.IsEnabled = false;

        // **A changed scope is a fresh decision about the send too**, and the button says so.
        _sendCorpusButton.IsEnabled = false;
        _sendCorpusButton.Content = SendLabel;

        _corpusPreview.Text =
            "Nothing has been read yet.\n\n"
            + "Choose how much of your history to include, then press Read my journals. "
            + "Reading a full history takes a few seconds and happens entirely on this machine.";

        _status.Text = string.Empty;
    }

    private async Task ReadAsync()
    {
        if (_read is not { } read)
        {
            return;
        }

        var scope = _scope.SelectedItem as CorpusScope ?? CorpusScope.Default;

        _running?.Cancel();
        _running = new CancellationTokenSource();

        var token = _running.Token;

        Busy(true);
        _status.Text = "Reading…";

        var progress = new Progress<int>(files => _status.Text = $"Reading — {files:N0} journal files so far");

        try
        {
            _reading = await read(scope, progress, token);
            _corpusPreview.Text = _reading.Report;
            _saveCorpus.IsEnabled = true;
            _sendCorpusButton.IsEnabled = true;

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
    /// The excerpt yes. One act, and then the window is done — the next time you share is a
    /// fresh decision about a fresh excerpt rather than this consent being spent twice.
    /// </summary>
    private async Task CopyAsync()
    {
        if (Clipboard is not { } clipboard)
        {
            _copy.Content = "No clipboard here";
            return;
        }

        try
        {
            await clipboard.SetTextAsync(_text);
        }
        catch (Exception)
        {
            // Said on the button rather than in a banner: a fault here — another application
            // holding the clipboard — is otherwise indistinguishable from having worked.
            _copy.Content = "Could not copy";
            return;
        }

        // Where a copied excerpt goes is the Commander's (#165); this says only what happened.
        _copy.Content = "Copied";
        await Task.Delay(TimeSpan.FromSeconds(1.6));
        Close();
    }

    /// <summary>
    /// The excerpt send (#175). <b>Sends the string that is on screen</b> rather than rebuilding
    /// it from the controls — one rendering, used three ways.
    /// <para>
    /// <b>The window does not close on it.</b> A send has an outcome the Commander has to be
    /// able to read; closing over the top of it would make a refused send indistinguishable
    /// from a stored one.
    /// </para>
    /// </summary>
    private async Task SendAsync()
    {
        if (_send is not { } send)
        {
            return;
        }

        _sendButton.IsEnabled = false;
        _sendButton.Content = "Sending…";
        _size.Text = "Sending. Nothing else is being sent, and nothing is being kept anywhere else.";

        try
        {
            var sent = await send(_text, _sending.Token);

            _sendButton.Content = sent.Outcome.Sent ? "Sent" : SendLabel;
            _sendButton.IsEnabled = !sent.Outcome.Sent;

            _size.Text = sent.Receipt is { } receipt
                ? $"{sent.Outcome.Said} Your own copy of it is in {receipt}."
                : $"{sent.Outcome.Said} d47 could not write its own copy of it.";
        }
        catch (OperationCanceledException)
        {
            // The window closed under it. There is nowhere left to say anything.
        }
        finally
        {
            // Rebinding rather than leaving the colour on whatever the last render chose: this
            // line is now an outcome rather than a character count.
            _sizeColour?.Dispose();
            _sizeColour = Themed(_size, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        }
    }

    /// <summary>
    /// The excerpt as a file, through the Commander's own picker, so nothing is written
    /// anywhere they did not choose.
    /// </summary>
    private async Task SaveExcerptAsync(Button save)
    {
        try
        {
            if (StorageProvider is not { CanSave: true } storage)
            {
                save.Content = "No file picker here";
                return;
            }

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save the excerpt",
                SuggestedFileName = $"d47-excerpt-{_markedAt.ToUniversalTime():yyyy-MM-dd-HHmmss}.md",
                DefaultExtension = "md",
                FileTypeChoices =
                [
                    new FilePickerFileType("Markdown")
                    {
                        Patterns = ["*.md"],
                    },
                ],
            });

            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(_text);

            save.Content = $"Saved to {file.Name}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            save.Content = "Could not write it";
        }
    }

    /// <summary>
    /// The history yes. <b>The second pass runs here</b> rather than at read time, so nothing
    /// exists on disk until the Commander has chosen where — the survey holds counts and one
    /// line per kind, never the payload.
    /// </summary>
    private async Task SaveCorpusAsync()
    {
        if (_reading is not { } reading || _write is not { } write)
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
        _saveCorpus.IsEnabled = false;
        _sendCorpusButton.IsEnabled = false;

        var progress = new Progress<int>(files => _status.Text = $"Writing — {files:N0} journal files so far");

        try
        {
            await using var stream = await file.OpenWriteAsync();

            await write(stream, progress, token);

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
            _saveCorpus.IsEnabled = _reading is not null;
            _sendCorpusButton.IsEnabled = _reading is not null;
        }
    }

    /// <summary>
    /// The history send (#181). <b>Sends what the report on screen describes</b>, assembled by
    /// the same writer the Save button uses. The window does not close on it, for the excerpt
    /// send's reason.
    /// </summary>
    private async Task SendCorpusAsync()
    {
        if (_sendCorpus is not { } send || _reading is not { } reading)
        {
            return;
        }

        _running?.Cancel();
        _running = new CancellationTokenSource();

        var token = _running.Token;

        Busy(true);
        _sendCorpusButton.IsEnabled = false;
        _sendCorpusButton.Content = "Sending…";
        _saveCorpus.IsEnabled = false;

        var progress = new Progress<DonationStep>(step => _status.Text = step.Sending
            ? "Sending. Nothing else is being sent, and nothing is being kept anywhere else."
            : $"Preparing what you are sharing — {step.Files:N0} journal files so far");

        var landed = false;

        try
        {
            var sent = await send(reading.Report, progress, token);

            landed = sent.Outcome.Sent;
            _sendCorpusButton.Content = landed ? "Sent" : SendLabel;

            _status.Text = sent.Receipt is { } receipt
                ? $"{sent.Outcome.Said} Your own copy of what you agreed to is in {receipt}."
                : $"{sent.Outcome.Said} d47 could not write its own copy of it.";
        }
        catch (OperationCanceledException)
        {
            _sendCorpusButton.Content = SendLabel;
            _status.Text = "Stopped. Nothing was confirmed as stored.";
        }
        finally
        {
            Busy(false);
            _saveCorpus.IsEnabled = _reading is not null;

            // Offered again only where it did not land. A "Sent" button that invites a second
            // press is a second thirty-megabyte upload nobody asked for.
            _sendCorpusButton.IsEnabled = _reading is not null && !landed;
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
        _includeHistory.IsEnabled = !busy;
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
