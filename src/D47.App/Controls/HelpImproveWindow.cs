using System.Globalization;
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
/// One window for sharing what the ship saw, in either of two shapes (#238): an incident excerpt, or —
/// with <c>Include journal history</c> on — the whole journal history.
/// </summary>
public sealed class HelpImproveWindow : Window
{
    private const string CopyLabel = "Copy for a bug report";
    private const string SendLabel = "Send it";

    private const string PrivacyUrl = "https://dseelinger.github.io/d47/donation-privacy.html";

    /// <summary>
    /// The point past which the excerpt consent wears thin, because the consent it asks for is read
    /// this and say yes to it (#173).
    /// </summary>
    private const int MostCharacters = 60_000;

    /// <summary>What a history reading produced: the document to show, and its own account of itself.</summary>
    public sealed record CorpusReading(CorpusSurvey Survey, string Report);

    /// <summary>Renders the excerpt and reports what it did, so the figures can show it live (#338).</summary>
    private readonly Func<ExcerptRequest, (string Text, ExcerptTally Tally)> _build;

    /// <summary>The sends, or null where nothing composed one.</summary>
    private readonly Func<string, CancellationToken, Task<DonationSent>>? _send;

    private readonly Func<string, IProgress<DonationStep>, CancellationToken, Task<DonationSent>>? _sendCorpus;

    /// <summary>The history half, or null where nothing composed one.</summary>
    private readonly Func<CorpusScope, IProgress<int>, CancellationToken, Task<CorpusReading>>? _read;

    private readonly Func<Stream, IProgress<int>, CancellationToken, Task>? _write;

    /// <summary>The withdrawal, or null where nothing composed one.</summary>
    private readonly Func<CancellationToken, Task<string>>? _forget;

    /// <summary>Whether a send has anywhere to go.</summary>
    private readonly string? _destination;
    private readonly DateTimeOffset _markedAt;
    private readonly CancellationTokenSource _sending = new();

    /// <summary>The toggle the merge exists for (#238). The leftmost cluster in its row, so the checkbox leads.</summary>
    private readonly CheckBox _includeHistory;

    /// <summary>How far back an excerpt reaches, in spans a person can name (#173).</summary>
    private readonly Segment _span = new()
    {
        Name = "Span",
        ItemsSource = [.. ExcerptSpan.All.Select(span => span.Name)],
        SelectedIndex = 0,
        MinWidth = 190,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>How much history goes, gentlest first (#241).</summary>
    private readonly Segment _scope = new()
    {
        Name = "Scope",
        ItemsSource = [.. CorpusScope.All.Select(scope => scope.Name)],
        SelectedIndex = 0,
        MinWidth = 190,
        VerticalAlignment = VerticalAlignment.Center,
    };

    // Named, like the choosers and the panes, because these are what a test drives to assert that the
    // text on screen is the text on the clipboard.
    private readonly CheckBox _mySpeech;

    private readonly Button _read_ = new() { Name = "ReadJournals", Content = "Read my journals", MinWidth = 160 };

    private readonly SelectableTextBlock _preview = new()
    {
        Name = "Excerpt",
        FontFamily = new FontFamily(Fonts.MonoFamily),
        FontSize = TypeScale.Small,

        // **Wrapped, though a payload reads better as the lines it is.** The paragraphs above the payload —
        // what was replaced, what was withheld, what is being agreed to — must not run off the right edge,
        // and a consent notice you have to scroll sideways to find is worse than ugly.
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly SelectableTextBlock _corpusPreview = new()
    {
        Name = "CorpusReport",
        FontFamily = new FontFamily(Fonts.MonoFamily),
        FontSize = TypeScale.Small,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>The one sentence a Commander reads before deciding anything else (#338).</summary>
    private readonly TextBlock _intro = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = TypeScale.Secondary,
        Margin = new Thickness(0, 0, 0, 12),
    };

    /// <summary>Where a send goes and how long it is kept — the one consent line that changes with the
    /// mode and the destination (#338).</summary>
    private readonly TextBlock _consentDestination;

    /// <summary>The way to the full legal text, which lives on the site rather than in the app (#338).</summary>
    private readonly Button _privacyLink = new()
    {
        Name = "DonationPrivacyLink",
        Content = "Read the full privacy note",
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    /// <summary>What leaves, in four figures a Commander can read at a glance (#338).</summary>
    private readonly TextBlock _figureEvents;
    private readonly TextBlock _figureNames;
    private readonly TextBlock _figureChars;

    /// <summary>The fourth figure, whose caption changes with the mode: log entries for an excerpt,
    /// journal files for a history — a corpus has no log half to count.</summary>
    private readonly TextBlock _figureFourthValue;
    private readonly TextBlock _figureFourthCaption;

    /// <summary>Collapsed until pressed, so the window opens on the consent rather than the payload.</summary>
    private readonly Button _disclosureToggle = new()
    {
        Name = "DisclosureToggle",
        Content = "Show the exact text",
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0, 0, 0, 6),
    };

    /// <summary>What the disclosure toggle shows and hides.</summary>
    private readonly Border _disclosurePane;

    /// <summary>
    /// Collapsed through <c>Height</c> rather than <c>IsVisible</c>: a hidden control never applies its
    /// template, so <see cref="_preview"/> and <see cref="_corpusPreview"/> — inside the scroller inside
    /// this border — would never join the visual tree at all while collapsed.
    /// </summary>
    private bool _disclosureExpanded;

    // Wrapped, because these carry whole sentences — the no-address explanation clipped mid-word under the
    // Cancel button before this said so (seen 2026-08-31).
    private readonly TextBlock _size = new()
    {
        FontSize = TypeScale.Small,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly TextBlock _status = new()
    {
        // Named, like the buttons and the choosers, because what this line says during a send is now a claim
        // a test has to be able to read (#212).
        Name = "SendStatus",
        FontSize = TypeScale.Small,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>How far the upload has got (#212).</summary>
    private readonly ProgressBar _bar = new()
    {
        Name = "SendProgress",
        Height = 3,
        Minimum = 0,
        Maximum = 1,
        IsVisible = false,
        Margin = new Thickness(0, 4, 16, 0),
    };

    private readonly Button _copy = new()
    {
        Name = "CopyExcerpt", Content = CopyLabel, MinWidth = 190,
    };

    private readonly Button _saveExcerpt = new()
    {
        Content = "Save a file instead…", MinWidth = 160,
    };

    private readonly Button _stop = new() { Name = "StopCorpus", Content = "Cancel", MinWidth = 110 };

    private readonly Button _saveCorpus = new()
    {
        Name = "SaveCorpus", Content = "Save it instead…", MinWidth = 160, IsEnabled = false,
    };

    // Named, like the controls above, because a test drives these to assert that what is sent is the artefact
    // that was on screen.
    /// <summary>The way back out, on the page that sent it (#295).</summary>
    private readonly Button _forgetButton = new()
    {
        Name = "ForgetDonations", Content = "Forget", MinWidth = 110, Classes = { "destructive" },
    };

    private readonly Button _sendButton = new() { Name = "SendExcerpt", Content = SendLabel, MinWidth = 190 };
    private readonly Button _sendCorpusButton = new()
    {
        Name = "SendCorpus", Content = SendLabel, MinWidth = 150, IsEnabled = false,
    };

    private string _text = string.Empty;
    private IDisposable? _sizeColour;
    private CancellationTokenSource? _running;
    private CorpusReading? _reading;

    /// <param name="markedAt">The bookmark — when the Commander said this was the moment.</param>
    /// <param name="build">Cuts an excerpt window, renders it, and reports what it did.</param>
    /// <param name="send">
    /// Sends the rendered excerpt — the text that is on screen, never a rebuild — or null where there
    /// is nowhere to send.
    /// </param>
    /// <param name="destination">
    /// Where a send would go, named on screen before it happens.
    /// </param>
    /// <param name="read">
    /// Surveys the history and renders its report, or null where this surface does not offer the
    /// history half.
    /// </param>
    /// <param name="write">Writes the history payload to a stream the Commander chose.</param>
    /// <param name="sendCorpus">
    /// Sends the history — takes the report that was read and said yes to — or null where there is
    /// nowhere to send.
    /// </param>
    /// <param name="forget">
    /// Asks the store to delete everything this installation ever sent, and answers with the sentence
    /// to show — the same call the <c>Privacy and egress</c> row makes (#295).
    /// </param>
    public HelpImproveWindow(
        DateTimeOffset markedAt,
        Func<ExcerptRequest, (string Text, ExcerptTally Tally)> build,
        Func<string, CancellationToken, Task<DonationSent>>? send = null,
        string? destination = null,
        Func<CorpusScope, IProgress<int>, CancellationToken, Task<CorpusReading>>? read = null,
        Func<Stream, IProgress<int>, CancellationToken, Task>? write = null,
        Func<string, IProgress<DonationStep>, CancellationToken, Task<DonationSent>>? sendCorpus = null,
        Func<CancellationToken, Task<string>>? forget = null)
    {
        _markedAt = markedAt;
        _build = build;
        _send = send;
        _destination = destination;
        _read = read;
        _write = write;
        _sendCorpus = sendCorpus;
        _forget = forget;

        (_includeHistory, _) = LabeledCheckBox.Build("Include journal history", labelFirst: false);
        _includeHistory.Name = "IncludeHistory";

        (_mySpeech, _) = LabeledCheckBox.Build("Include what I said out loud");
        _mySpeech.Name = "IncludeMySpeech";

        Title = "Help improve D47";
        Width = 900;
        Height = 720;
        MinWidth = 560;
        MinHeight = 420;
        CanResize = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Themed(_preview, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        Themed(_corpusPreview, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        Themed(_intro, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        Themed(_size, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        Themed(_status, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        // The history half exists where both of its delegates do — which is every reading the button appears
        // on, since the page it was pressed on stopped deciding that.
        _includeHistory.IsVisible = HistoryOffered;
        _includeHistory.IsChecked = false;

        // "Instead" needs a send to be instead of (asked 2026-08-31, from a window with no address set and a
        // button that dangled).
        _saveCorpus.Content = sendCorpus is null ? "Save it to a file…" : "Save it instead…";

        var root = new DockPanel();

        var options = Options();

        var (consent, consentDestination) = Consent();
        _consentDestination = consentDestination;

        var (figures, figureEvents, figureNames, figureChars, figureFourthValue, figureFourthCaption) = Figures();
        _figureEvents = figureEvents;
        _figureNames = figureNames;
        _figureChars = figureChars;
        _figureFourthValue = figureFourthValue;
        _figureFourthCaption = figureFourthCaption;

        var (buttons, progress) = Footer();

        var disclosureSection = new DockPanel();
        DockPanel.SetDock(_disclosureToggle, Dock.Top);
        disclosureSection.Children.Add(_disclosureToggle);
        disclosureSection.Children.Add(DisclosurePane(out _disclosurePane));

        _disclosureToggle.Click += (_, _) =>
        {
            _disclosureExpanded = !_disclosureExpanded;
            _disclosurePane.Height = _disclosureExpanded ? double.NaN : 0;
            _disclosureToggle.Content = _disclosureExpanded ? "Hide the exact text" : "Show the exact text";
        };

        // Help leads the footer, apart from the buttons that act (#252); the reasoning behind the window is in
        // the intro's tooltip (#269).
        var mark = SiteHelpMark.For(DocsSite.Page(HelpPage), "HelpImproveHelp");

        ToolTip.SetTip(_intro, Reasoning);
        _intro.Margin = new Thickness(0, 0, 0, 12);

        DockPanel.SetDock(_intro, Dock.Top);
        DockPanel.SetDock(options, Dock.Top);
        DockPanel.SetDock(consent, Dock.Top);
        DockPanel.SetDock(figures, Dock.Top);
        DockPanel.SetDock(progress, Dock.Bottom);

        root.Children.Add(_intro);
        root.Children.Add(options);
        root.Children.Add(consent);
        root.Children.Add(figures);
        root.Children.Add(progress);
        root.Children.Add(disclosureSection);

        // The exact text takes the height left, so the body does not scroll as a whole.
        Modal.Apply(this, "Transcript", Title, root, [mark, .. buttons], scrolls: false);

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
        _forgetButton.Click += async (_, _) => await ForgetAsync();
        _stop.Click += (_, _) => Stop();
        _privacyLink.Click += (_, _) => SiteHelpMark.Open(PrivacyUrl);

        // A send in flight is a request against a daily ceiling and a payload half written at the store.
        Closed += (_, _) =>
        {
            _sending.Cancel();
            _running?.Cancel();
        };

        ApplyMode();
    }

    /// <summary>The rendered excerpt as it stands.</summary>
    internal string Text => _text;

    private bool HistoryOffered => _read is not null && _write is not null;

    private bool History => HistoryOffered && _includeHistory.IsChecked == true;

    /// <summary>
    /// Everything the toggle decides, in one place: which chooser, which pane, which buttons — and a
    /// fresh start for the flow being entered, because a consent begun under one mode must not be spent
    /// under the other.
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
        _bar.IsVisible = false;

        _copy.IsVisible = !history;
        _saveExcerpt.IsVisible = !history;
        _sendButton.IsVisible = !history && _send is not null;

        _saveCorpus.IsVisible = history;
        _sendCorpusButton.IsVisible = history && _sendCorpus is not null;

        _intro.Text = Sentence(history);
        _consentDestination.Text = DestinationText(history);
        _figureFourthCaption.Text = history ? "JOURNAL FILES" : "LOG ENTRIES";

        // Collapsed again on every fresh entry to a mode, the same rule the send buttons follow: a payload
        // shown open under one mode is not consent given under the other.
        _disclosureExpanded = false;
        _disclosurePane.Height = 0;
        _disclosureToggle.Content = "Show the exact text";

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

    /// <summary>The page the mark opens (#252).</summary>
    public const string HelpPage = D47.Core.Help.HelpLibrary.GeneralPrefix + "help-improve";

    /// <summary>The one sentence a Commander reads before anything else (#338).</summary>
    private static string Sentence(bool history) =>
        history
            ? "Send the developer your whole journal history, so a fix can be proved against it."
            : "Send the developer a slice of what just happened, so a fix can be proved against it.";

    /// <summary>Where a send goes and how long it is kept, or the fact that nothing can be sent (#269, #338).</summary>
    private string DestinationText(bool history) =>
        _destination is not null
            ? "Sent, it goes to Directive 47. "
              + (history
                  ? "It is kept until you press Forget."
                  : "It is deleted after 30 days, or sooner when you press Forget.")
            : history
                ? "No send address is set, so nothing here goes to a network. Save it, and where the "
                  + "file goes afterwards is yours."
                : "No send address is set, so nothing can be sent from here. Copy or save it, and "
                  + "where it goes afterwards is yours — anything posted publicly can be archived "
                  + "beyond anyone's reach.";

    /// <summary>The intro's hover (#269): the arguments for pressing.</summary>
    internal const string Reasoning =
        "Why real journals\n"
        + "A bug is nearly always about a situation — a callout that fires when it should not, "
        + "a ship state nobody anticipated, an event Frontier added that the app had never "
        + "seen. Reproducing one from a description means guessing at the situation; from the "
        + "journal that produced it, the replay harness runs the same events in the same order.\n"
        + "\n"
        + "What the scrub does\n"
        + "It works from a list of fields it keeps, rather than a list of fields to remove. Other "
        + "fields are removed by default.";

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

        // One visible label over whichever chooser the mode shows: the two lists answer two different
        // questions — a window around an incident, and a reach of history — so they stay two controls.
        // "Include" rather than "Scale" (#295): the label says what the chooser decides, in the word the
        // toggle beside it already uses.
        var scale = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _span, _scope },
        };

        row.Children.Add(Labelled("Include", scale));
        row.Children.Add(new Border { Padding = new Thickness(8, 0, 0, 0), Child = _mySpeech });
        row.Children.Add(_read_);

        return row;
    }

    /// <summary>Three consent lines in the report treatment (#335), and the way to the full legal text.</summary>
    private (Control Block, TextBlock Destination) Consent()
    {
        var (scrubRow, scrubText) = ConsentLine();
        scrubText.Text =
            "Names and IDs are replaced with stand-ins, and other people's words are stripped, "
            + "before anything leaves this machine.";

        var (destinationRow, destinationText) = ConsentLine();

        var (standingRow, standingText) = ConsentLine();
        standingText.Text =
            "Nothing is saved or sent until you press " + SendLabel + " — every time, with no "
            + "standing consent and nothing remembered.";

        var block = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12),
            Children = { scrubRow, destinationRow, standingRow, _privacyLink },
        };

        return (block, destinationText);
    }

    /// <summary>One line, drawn with no box — a rule on the left edge, the same treatment a read-only
    /// settings row draws (#335) — rather than the bordered rectangle a field draws.</summary>
    private (Border Row, TextBlock Text) ConsentLine()
    {
        var row = ConsentLine(out var text);
        return (row, text);
    }

    private Border ConsentLine(out TextBlock text)
    {
        var block = new TextBlock { FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var row = new Border
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(14, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Child = block,
        };

        Themed(row, Border.BorderBrushProperty, ThemeManager.Line2Key);

        text = block;
        return row;
    }

    /// <summary>
    /// What will leave, in four stat tiles (#338). <see cref="Render"/>, <see cref="Discard"/> and <see cref="ReadAsync"/> keep them
    /// current; nothing here computes a count itself.
    /// </summary>
    private (Control Block, TextBlock Events, TextBlock Names, TextBlock Chars, TextBlock FourthValue, TextBlock FourthCaption) Figures()
    {
        var fourth = Figure("log entries", out var fourthValue, out var fourthCaption);
        var events = Figure("journal events", out var eventsValue);
        var names = Figure("names replaced", out var namesValue);
        var chars = Figure("characters", out var charsValue);

        var block = StatTile.Grid([fourth, events, names, chars]);
        block.Margin = new Thickness(0, 0, 0, 12);

        return (block, eventsValue, namesValue, charsValue, fourthValue, fourthCaption);
    }

    private Border Figure(string caption, out TextBlock value) => Figure(caption, out value, out _);

    private static Border Figure(string caption, out TextBlock value, out TextBlock captionBlock)
    {
        var tile = StatTile.Build(caption, "—");
        var lines = (StackPanel)tile.Child!;

        captionBlock = (TextBlock)lines.Children[0];
        value = (TextBlock)lines.Children[1];
        return tile;
    }

    /// <summary>The exact text, collapsed until <see cref="_disclosureToggle"/> is pressed (#338).</summary>
    private Control DisclosurePane(out Border pane)
    {
        // Vertical only, for the reason recorded on SpendWindow (#87): a ScrollViewer that may scroll
        // horizontally measures its content with unconstrained width, which makes the wrapping above a no-op.
        pane = new Border
        {
            Name = "DisclosurePane",
            Padding = new Thickness(14, 10),
            Height = 0,
            Child = new ScrollViewer
            {
                Name = "ExcerptScroller",
                Content = new StackPanel { Children = { _preview, _corpusPreview } },
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
        };

        Themed(pane, Border.BackgroundProperty, ThemeManager.SlabKey);

        return pane;
    }

    /// <summary>The footer's buttons, and the size, status and progress lines above them.</summary>
    private (List<Control> Buttons, Control Progress) Footer()
    {
        var primary = new List<Control>();

        // Send leads the primary cluster, with the alternates beside it rather than instead of it — the
        // upload is the default action, not the only one.
        if (_send is not null)
        {
            primary.Add(_sendButton);
        }

        primary.Add(_saveExcerpt);
        primary.Add(_copy);
        primary.Add(_saveCorpus);

        if (_sendCorpus is not null)
        {
            primary.Add(_sendCorpusButton);
        }

        // A fixed gap between the actions and Cancel and Forget.
        primary.Add(new Border { Width = 24 });

        primary.Add(_stop);

        // Beside Send, on whichever page is showing (#295): the retention line above names this button, and a
        // Commander who reads it there should not have to go looking for it.
        if (_forget is not null)
        {
            primary.Add(_forgetButton);
        }

        // The bar under the sentence rather than in place of it (#212): "Nothing else is being sent, and
        // nothing is being kept anywhere else" is doing work about scope that a percentage cannot do.
        var progress = new StackPanel { Margin = new Thickness(0, 12, 0, 0), Children = { _size, _status, _bar } };

        return (primary, progress);
    }

    /// <summary>Rebuilds the excerpt, shows it and refreshes the figures.</summary>
    private void Render()
    {
        if (History)
        {
            return;
        }

        var span = _span.SelectedIndex >= 0 ? ExcerptSpan.All[_span.SelectedIndex] : ExcerptSpan.Default;

        var (text, tally) = _build(span.Around(_markedAt, _mySpeech.IsChecked == true));

        _text = text;
        _preview.Text = _text;

        _figureEvents.Text = Number(tally.JournalEvents);
        _figureNames.Text = Number(tally.NamesReplaced);
        _figureFourthValue.Text = Number(tally.LogEntries);
        _figureChars.Text = Number(_text.Length);

        // **A changed payload is a fresh decision.** The same rule the history flow enforces by throwing its
        // report away: a button reading "Sent" above an excerpt that is no longer the one that was sent is
        // the one failure a consent step must not have.
        _sendButton.Content = SendLabel;
        _sendButton.IsEnabled = true;

        var long_ = _text.Length > MostCharacters;

        // Names the real problem and no transport (#165): the yes this window asks for is a yes to something
        // read, and that is what stops being true at this size.
        _size.Text = long_
            ? $"{_text.Length:N0} characters — more than a person reads, so a yes to it would not "
              + "be a yes to something you read. Choose a shorter span."
            : $"{_text.Length:N0} characters";

        // Disposed before rebinding, or the subscriptions stack up and the last to fire decides.
        _sizeColour?.Dispose();
        _sizeColour = Themed(
            _size,
            TextBlock.ForegroundProperty,
            long_ ? ThemeManager.RedKey : ThemeManager.GreyKey);
    }

    /// <summary>Throws away a history reading, and resets the figures to unknown.</summary>
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

        _figureEvents.Text = "—";
        _figureNames.Text = "—";
        _figureFourthValue.Text = "—";
        _figureChars.Text = "—";

        _corpusPreview.Text =
            "Nothing has been read yet.\n\n"
            + "Choose how much of your history to include, then press Read my journals. "
            + "Reading a full history takes a few seconds and happens entirely on this machine.";

        // The question a Commander actually asked at this window (2026-08-31): which button sends?
        _status.Text = _sendCorpus is null
            ? "No send button: this window was opened with nowhere to send. Save writes the "
              + "scrubbed file, and where it goes is yours."
            : string.Empty;
    }

    private async Task ReadAsync()
    {
        if (_read is not { } read)
        {
            return;
        }

        var scope = _scope.SelectedIndex >= 0 ? CorpusScope.All[_scope.SelectedIndex] : CorpusScope.Default;

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

            _figureEvents.Text = Number(survey.Tally.Events);
            _figureNames.Text = Number(survey.Tally.NamesReplaced);
            _figureFourthValue.Text = Number(survey.Files);
            _figureChars.Text = Number(_reading.Report.Length);

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

    /// <summary>The excerpt yes.</summary>
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
            // Said on the button rather than in a banner: a fault here — another application holding the
            // clipboard — is otherwise indistinguishable from having worked.
            _copy.Content = "Could not copy";
            return;
        }

        // Where a copied excerpt goes is the Commander's (#165); this says only what happened.
        _copy.Content = "Copied";
        await Task.Delay(TimeSpan.FromSeconds(1.6));
        Close();
    }

    /// <summary>The excerpt send (#175).</summary>
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
        // The window closed under it.
        }
        finally
        {
            // Rebinding rather than leaving the colour on whatever the last render chose: this line is now an
            // outcome rather than a character count.
            _sizeColour?.Dispose();
            _sizeColour = Themed(_size, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        }
    }

    /// <summary>
    /// The excerpt as a file, through the Commander's own picker, so nothing is written anywhere they
    /// did not choose.
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

    /// <summary>The history yes.</summary>
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

    /// <summary>The history send (#181).</summary>
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

        _bar.Value = 0;
        _bar.IsVisible = false;

        // **A report that arrives after the outcome is dropped.** Progress<T> posts, the send may complete
        // without ever yielding, and the losing order puts "Sending — 30.5 MB of 30.5 MB" and a full bar on
        // top of "the endpoint refused it" — which is the one thing this window exists not to say.
        var reporting = true;

        var progress = new Progress<DonationStep>(step =>
        {
            if (!reporting)
            {
                return;
            }

            _status.Text = step switch
            {
                { Sending: false } =>
                    $"Preparing what you are sharing — {step.Files:N0} journal files so far",

                // **"Compressed" is not decoration.** The report above states the history's own size — 383 MB
                // — and what goes on the wire is a twelfth of it, so a number counting to 32.5 MB with
                // nothing to explain it reads as most of it missing.
                { Total: > 0 } =>
                    $"Sending — {Size(step.Sent)} of {Size(step.Total)} compressed. Nothing else "
                    + "is being sent, and nothing is being kept anywhere else.",

                _ => "Sending. Nothing else is being sent, and nothing is being kept anywhere else.",
            };

            // Absent where there is nothing to measure rather than sitting at nought, which is what the
            // preparing step is — it has a rising file count and no denominator.
            _bar.IsVisible = step.Fraction is not null;
            _bar.Value = step.Fraction ?? 0;
        });

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
            reporting = false;

            Busy(false);
            _saveCorpus.IsEnabled = _reading is not null;

            // Gone the moment the bytes stop moving, whichever way it went.
            _bar.IsVisible = false;

            // Offered again only where it did not land.
            _sendCorpusButton.IsEnabled = _reading is not null && !landed;
        }
    }

    /// <summary>The withdrawal (#295).</summary>
    private async Task ForgetAsync()
    {
        if (_forget is not { } forget)
        {
            return;
        }

        var history = History;

        _forgetButton.IsEnabled = false;

        try
        {
            var said = await forget(_sending.Token);

            if (history)
            {
                _status.Text = said;
            }
            else
            {
                _size.Text = said;

                // This line is an outcome now rather than a character count, so it is not left in whatever
                // colour the last render chose for it.
                _sizeColour?.Dispose();
                _sizeColour = Themed(_size, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            }
        }
        catch (OperationCanceledException)
        {
        // The window closed under it.
        }
        finally
        {
            // Offered again either way: a refused erasure keeps the identifier precisely so the press can be
            // made a second time.
            _forgetButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Cancels what is running, or closes where nothing is — the one button a Commander reaches for
    /// when they have changed their mind, whichever of the two things they meant.
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

    /// <summary>
    /// Megabytes for a payload and kilobytes for a small one — the same shape, to one decimal, that
    /// <c>CorpusReport</c> states the history's own size in, so the two numbers a Commander has on
    /// screen at once are read in the same units (#212).
    /// </summary>
    private static string Size(long bytes) =>
        bytes >= 1024L * 1024L
            ? $"{(bytes / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.InvariantCulture)} MB"
            : $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB";

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>A caption in front of a control.</summary>
    private Control Labelled(string caption, Control control)
    {
        var label = new TextBlock
        {
            Text = caption,
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };

        Themed(label, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

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
