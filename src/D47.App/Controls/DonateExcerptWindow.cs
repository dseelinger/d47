using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Donation;
using D47.App.Theming;
using D47.Core.Diagnostics.Donation;

namespace D47.App.Controls;

/// <summary>
/// The show-then-attach step: <b>the Commander reads exactly what would leave, and then says yes
/// to that</b> (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// <para>
/// <b>The text on screen is the text on the clipboard.</b> One rendering — <see
/// cref="ExcerptReport.Render"/> — fills the pane and fills the clipboard, because a preview
/// assembled by one code path and a payload assembled by another are two artefacts and the
/// Commander only ever read one of them. Changing any control here re-renders, so what is shown is
/// never a stale answer to an older question.
/// </para>
/// <para>
/// <b>One act per donation, and the window closes on it.</b> There is no standing consent, no
/// remembered choice and no auto-send: a consent given once that uploads forever afterwards is
/// telemetry wearing a consent form. Closing on the copy is what makes the next donation a fresh
/// decision rather than a repeat of this one.
/// </para>
/// <para>
/// <b>Since <a href="https://github.com/dseelinger/d47/issues/175">#175</a> it can also send.</b>
/// The clipboard and the file are still here — an upload became the default action, not the only
/// one — and the property that keeps this on the right side of no-telemetry is unchanged, because
/// it was never "nothing reaches the network". It was that a Commander reads the whole payload and
/// presses once, every time, with no standing consent and nothing remembered. A send is disclosed
/// as <see cref="D47.Core.Configuration.EgressDisclosure.Donation"/> and is impossible until an
/// address is configured.
/// </para>
/// <para>
/// <b>What a send costs, and what buys it back.</b> "What is shown is what leaves" was observable
/// while the human was the transport; an upload turns it into a claim about code. So d47 writes
/// its own copy of exactly the bytes it sent, with their hash, into <c>data\donations</c> — see
/// <see cref="DonationReceipt"/>. The Commander's evidence, and their deletion request's.
/// </para>
/// <para>
/// Built in code rather than as an axaml pair, like <see cref="SpendWindow"/> and
/// <see cref="ChangelogWindow"/> beside it.
/// </para>
/// </summary>
public sealed class DonateExcerptWindow : Window
{
    private const string CopyLabel = "Copy it for the report";
    private const string SendLabel = "Send it";

    /// <summary>
    /// The point past which the claim this window makes starts to wear thin, because the consent
    /// it asks for is <i>read this and say yes to it</i>
    /// (<a href="https://github.com/dseelinger/d47/issues/173">#173</a>).
    /// <para>
    /// <b>It used to be GitHub's limit for one comment, and it is not that any more</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/165">#165</a>). That number was a
    /// transport detail of a destination the erasure ruling removed, and the store behind
    /// <see cref="D47.App.Donation.DonationUpload"/> has a ceiling four megabytes away from here.
    /// What it measured was never really the transport: it is that nobody reads sixty thousand
    /// characters, and the yes this window asks for is a yes to something read. So the number
    /// stayed and the reason for it is now stated as the reason it always was.
    /// </para>
    /// </summary>
    private const int MostCharacters = 60_000;

    private readonly Func<ExcerptRequest, string> _build;

    /// <summary>
    /// The send, or null where nothing composed one. <b>Null is a real state and not a test
    /// convenience</b>: with no donation address configured there is nowhere to send, and the
    /// window then offers exactly what it offered before #175 rather than a button that explains
    /// itself.
    /// </summary>
    private readonly Func<string, CancellationToken, Task<DonationSent>>? _send;

    private readonly string? _destination;
    private readonly DateTimeOffset _markedAt;
    private readonly CancellationTokenSource _sending = new();

    /// <summary>
    /// How far back, in spans a person can name (#173). It replaced a pair of minute steppers that
    /// implied a reach the sources did not have — see <see cref="ExcerptSpan"/> for why the list
    /// stops where it does.
    /// </summary>
    private readonly ComboBox _span = new()
    {
        Name = "Span",
        ItemsSource = ExcerptSpan.All,
        SelectedIndex = 0,
        MinWidth = 190,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly CheckBox _mySpeech = new()
    {
        // Named, like the two steppers and the pane, because these four are what a test drives to
        // assert that the text on screen is the text on the clipboard.
        Name = "IncludeMySpeech",
        Content = "Include what I said out loud",
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly SelectableTextBlock _preview = new()
    {
        Name = "Excerpt",
        FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
        FontSize = TypeScale.Small,

        // **Wrapped, though a payload reads better as the lines it is.** Unwrapped with a
        // horizontal scrollbar was the first cut, and rendering it against a real session showed
        // what is wrong with that: the paragraphs above the payload — what was replaced, what was
        // withheld, what the Commander is agreeing to — all ran off the right edge, and the one
        // thing this window exists to do is put those in front of somebody before they say yes.
        // A wrapped journal line is ugly. A consent notice you have to scroll sideways to find is
        // worse than ugly.
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly TextBlock _size = new() { FontSize = TypeScale.Small };
    private readonly Button _copy = new() { Name = "CopyExcerpt", Content = CopyLabel, MinWidth = 190 };

    // Named, like the controls above it, because a test drives this one to assert that what is
    // sent is the string that was on screen.
    private readonly Button _sendButton = new() { Name = "SendExcerpt", Content = SendLabel, MinWidth = 190 };

    private string _text = string.Empty;
    private IDisposable? _sizeColour;

    /// <param name="markedAt">
    /// The bookmark — when the Commander said this was the moment. The outburst that marked it, if
    /// there was one, is already gone: only the instant travels.
    /// </param>
    /// <param name="build">Cuts a window and renders it. Called again on every change here.</param>
    /// <param name="send">
    /// Sends what is on screen, or null where there is nowhere to send it. Takes the rendered text
    /// rather than the request that produced it, which is the same "one rendering, used twice"
    /// rule the clipboard already follows: a payload assembled by a second code path is a second
    /// artefact, and the Commander only ever read one of them.
    /// </param>
    /// <param name="destination">Where a send would go, named on screen before it happens.</param>
    public DonateExcerptWindow(
        DateTimeOffset markedAt,
        Func<ExcerptRequest, string> build,
        Func<string, CancellationToken, Task<DonationSent>>? send = null,
        string? destination = null)
    {
        _markedAt = markedAt;
        _build = build;
        _send = send;
        _destination = destination;

        Title = "Donate an incident excerpt";
        Width = 900;
        Height = 720;
        MinWidth = 560;
        MinHeight = 420;
        CanResize = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);
        Themed(_preview, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        Themed(_size, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var root = new DockPanel { Margin = new Thickness(20) };

        var lede = Lede();
        var options = Options();
        var footer = Footer();

        DockPanel.SetDock(lede, Dock.Top);
        DockPanel.SetDock(options, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);

        // Vertical only, like every other window in this folder and for the reason recorded on
        // SpendWindow (#87): a ScrollViewer that may scroll horizontally measures its content with
        // unconstrained width, which makes the wrapping above a no-op.
        var pane = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = new ScrollViewer
            {
                Name = "ExcerptScroller",
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

        _span.SelectionChanged += (_, _) => Render();
        _mySpeech.IsCheckedChanged += (_, _) => Render();

        // A send in flight is a request against a daily ceiling and a payload half written at the
        // store. Closing the window is the Commander saying they are done with it, not a reason to
        // leave one running with nowhere to report back to.
        Closed += (_, _) => _sending.Cancel();

        Render();
    }

    /// <summary>The rendered excerpt as it stands. Public so a test can read what would be copied.</summary>
    internal string Text => _text;

    /// <summary>
    /// What a Commander reads before they press anything.
    /// <para>
    /// <b>It no longer says "paste it into the issue".</b> That named a destination the erasure
    /// ruling removed (<a href="https://github.com/dseelinger/d47/issues/165">#165</a>): a public
    /// repository's comments are mirrored by third-party archives within the hour and mailed whole
    /// to every watcher, so "ask and it is deleted" is a promise no public transport can keep.
    /// </para>
    /// <para>
    /// <b>And it states the weaker linkage claim, in front of the Commander, before the first
    /// donation</b> (<a href="https://github.com/dseelinger/d47/issues/176">#176</a>). d47 used to
    /// claim two donations from one Commander could not be joined. They can now, on a random
    /// installation token, which is what lets a journal history be added to — materially weaker,
    /// still worth stating, and read here rather than discovered afterwards by somebody who
    /// consented to the older claim.
    /// </para>
    /// </summary>
    private Control Lede()
    {
        var where = _destination is { } destination
            ? "Sending it puts it in Directive 47's own store at " + destination + " — one press, "
              + "nothing standing, nothing remembered. A rule on the store deletes it after "
              + "thirty days without anybody having to remember to, and asking for it sooner is "
              + "one press in Privacy and egress. d47 keeps its own copy of exactly what it sent, "
              + "and the hash, in data\\donations."
            : "Nothing can be sent from here: no donation address is set. Copy it or save it, and "
              + "where it goes after that is yours — anything posted publicly can be archived "
              + "beyond anyone's reach.";

        var lede = new TextBlock
        {
            Text = "Everything below is what would go into the report — nothing else, and nothing "
                   + "leaves until you press. Names and Frontier IDs are replaced before you see "
                   + "them, and other people's in-game messages are dropped. Reach back as far as "
                   + "the defect needs, and read it.\n\n"
                   + where + "\n\n"
                   + "A random number identifying this installation — not you, and not derived from "
                   + "your Commander name — goes with a send, so donations you make can be grouped "
                   + "into one history. Deleting data\\donor-token.txt stops that.",
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

        row.Children.Add(Labelled("How far back", _span));
        row.Children.Add(new Border { Padding = new Thickness(8, 0, 0, 0), Child = _mySpeech });

        return row;
    }

    private Control Footer()
    {
        var save = new Button { Content = "Save a file instead…", MinWidth = 160 };
        var cancel = new Button { Content = "Cancel", MinWidth = 110 };

        _copy.Click += async (_, _) => await CopyAsync();
        _sendButton.Click += async (_, _) => await SendAsync();
        save.Click += async (_, _) => await SaveAsync(save);
        cancel.Click += (_, _) => Close();

        // Send last, which is where this window's default action has always sat, and the copy
        // beside it rather than instead of it: the upload became the default action, not the only
        // one. With nowhere to send, the row is exactly the row that shipped before #175.
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, save, _copy },
        };

        if (_send is not null)
        {
            buttons.Children.Add(_sendButton);
        }

        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };

        DockPanel.SetDock(buttons, Dock.Right);

        _size.VerticalAlignment = VerticalAlignment.Center;

        footer.Children.Add(buttons);
        footer.Children.Add(_size);

        return footer;
    }

    /// <summary>
    /// Rebuilds the excerpt and shows it. Everything the Commander can change here changes what
    /// would leave, so everything here comes through this one call.
    /// </summary>
    private void Render()
    {
        var span = _span.SelectedItem as ExcerptSpan ?? ExcerptSpan.Default;

        _text = _build(span.Around(_markedAt, _mySpeech.IsChecked == true));
        _preview.Text = _text;

        // **A changed payload is a fresh decision.** The same rule the corpus window enforces by
        // throwing its report away: a button reading "Sent" above an excerpt that is no longer the
        // one that was sent is the one failure a consent step must not have.
        _sendButton.Content = SendLabel;
        _sendButton.IsEnabled = true;

        var long_ = _text.Length > MostCharacters;

        // **Names the real problem, and no longer names a transport at all** (#165). It used to
        // add "more than one GitHub comment holds", which was a detail of the destination the
        // erasure ruling removed — and it was never the thing that mattered here. What matters is
        // that the yes this window asks for is a yes to something read, and that is what stops
        // being true at this size whichever route the excerpt takes.
        _size.Text = long_
            ? $"{_text.Length:N0} characters — more than a person reads, so a yes to it would not "
              + "be a yes to something you read. Choose a shorter span."
            : $"{_text.Length:N0} characters";

        // Disposed before rebinding. A binding per keystroke on the steppers would stack up
        // subscriptions on one TextBlock, and the last one to fire would decide the colour.
        _sizeColour?.Dispose();
        _sizeColour = Themed(
            _size,
            TextBlock.ForegroundProperty,
            long_ ? ThemeManager.DangerKey : ThemeManager.TextMutedKey);
    }

    /// <summary>
    /// The yes. One act, and then the window is done — the next donation is a fresh decision about
    /// a fresh excerpt rather than this consent being spent twice.
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
            // Said on the button rather than in a banner, like the panel's own Copy: a fault here
            // — another application holding the clipboard — is otherwise indistinguishable from
            // having worked, and this is the one press where that matters.
            _copy.Content = "Could not copy";
            return;
        }

        // **No longer "paste it into the issue".** That named the destination the erasure ruling
        // removed (#165), and it named it at the one moment the Commander is acting on what it
        // says. Where a copied excerpt goes is now theirs, and this says only what happened.
        _copy.Content = "Copied";
        await Task.Delay(TimeSpan.FromSeconds(1.6));
        Close();
    }

    /// <summary>
    /// The other yes, and the one #175 made the default. <b>Sends the string that is on screen</b>
    /// rather than rebuilding it from the controls — one rendering, used three ways now, for the
    /// reason the clipboard already had: a payload assembled by a second code path is a second
    /// artefact and the Commander only ever read one of them.
    /// <para>
    /// <b>The window does not close on it.</b> The copy closes because a clipboard either has the
    /// text or does not; a send has an outcome that the Commander has to be able to read, and one
    /// that names a receipt they may want to go and find. Closing over the top of it would make a
    /// refused donation indistinguishable from a stored one.
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
            // Rebinding rather than leaving the size colour on whatever the last render chose:
            // this line is now an outcome rather than a character count.
            _sizeColour?.Dispose();
            _sizeColour = Themed(_size, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        }
    }

    /// <summary>
    /// The same excerpt as a file, for a window too long to paste. Through the Commander's own
    /// picker, so nothing is written anywhere they did not choose — this window has no business
    /// leaving a file with an incident in it beside the executable.
    /// </summary>
    private async Task SaveAsync(Button save)
    {
        try
        {
            if (StorageProvider is not { CanSave: true } storage)
            {
                save.Content = "No file picker here";
                return;
            }

            var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save the excerpt",
                SuggestedFileName = $"d47-excerpt-{_markedAt.ToUniversalTime():yyyy-MM-dd-HHmmss}.md",
                DefaultExtension = "md",
                FileTypeChoices =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Markdown")
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
