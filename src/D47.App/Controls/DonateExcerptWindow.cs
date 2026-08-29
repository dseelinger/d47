using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
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
/// <b>Nothing here reaches the network.</b> It fills a clipboard, or writes a file the Commander
/// picked. Where the excerpt goes after that is a paste they perform, into an issue they can see —
/// which is the property that keeps this on the right side of no-telemetry rather than moving the
/// line.
/// </para>
/// <para>
/// Built in code rather than as an axaml pair, like <see cref="SpendWindow"/> and
/// <see cref="ChangelogWindow"/> beside it.
/// </para>
/// </summary>
public sealed class DonateExcerptWindow : Window
{
    /// <summary>
    /// How far back the window may reach. An hour is already more than any incident needs and is
    /// several thousand journal events; the point of a cap is that an excerpt stays something a
    /// person can actually read before consenting to it, which a whole session is not.
    /// </summary>
    private const int MostMinutesBefore = 60;

    /// <summary>
    /// And forward. Short, because the mark is placed at or just after the symptom — a long tail
    /// is a Commander donating the flying they did afterwards.
    /// </summary>
    private const int MostMinutesAfter = 15;

    private const string CopyLabel = "Copy it for the report";

    /// <summary>
    /// GitHub's own limit for one comment. Said here rather than found in a browser with the issue
    /// half written.
    /// </summary>
    private const int MostCharacters = 60_000;

    private readonly Func<ExcerptRequest, string> _build;
    private readonly DateTimeOffset _markedAt;

    private readonly NumericUpDown _before = Minutes("MinutesBefore", 5, MostMinutesBefore, least: 1);
    private readonly NumericUpDown _after = Minutes("MinutesAfter", 1, MostMinutesAfter, least: 0);

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
    private readonly Button _copy = new() { Content = CopyLabel, MinWidth = 190 };

    private string _text = string.Empty;
    private IDisposable? _sizeColour;

    /// <param name="markedAt">
    /// The bookmark — when the Commander said this was the moment. The outburst that marked it, if
    /// there was one, is already gone: only the instant travels.
    /// </param>
    /// <param name="build">Cuts a window and renders it. Called again on every change here.</param>
    public DonateExcerptWindow(DateTimeOffset markedAt, Func<ExcerptRequest, string> build)
    {
        _markedAt = markedAt;
        _build = build;

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

        _before.ValueChanged += (_, _) => Render();
        _after.ValueChanged += (_, _) => Render();
        _mySpeech.IsCheckedChanged += (_, _) => Render();

        Render();
    }

    /// <summary>The rendered excerpt as it stands. Public so a test can read what would be copied.</summary>
    internal string Text => _text;

    private Control Lede()
    {
        var lede = new TextBlock
        {
            Text = "Everything below is what would go into the report — nothing else, and nothing "
                   + "is sent from here. Names and Frontier IDs are replaced before you see them, "
                   + "and other people's in-game messages are dropped. Read it, then copy it and "
                   + "paste it into the issue yourself.",
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

        row.Children.Add(Labelled("Minutes before", _before));
        row.Children.Add(Labelled("Minutes after", _after));
        row.Children.Add(new Border { Padding = new Thickness(8, 0, 0, 0), Child = _mySpeech });

        return row;
    }

    private Control Footer()
    {
        var save = new Button { Content = "Save a file instead…", MinWidth = 160 };
        var cancel = new Button { Content = "Cancel", MinWidth = 110 };

        _copy.Click += async (_, _) => await CopyAsync();
        save.Click += async (_, _) => await SaveAsync(save);
        cancel.Click += (_, _) => Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, save, _copy },
        };

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
        var request = new ExcerptRequest(
            _markedAt,
            TimeSpan.FromMinutes((double)(_before.Value ?? 5)),
            TimeSpan.FromMinutes((double)(_after.Value ?? 1)),
            _mySpeech.IsChecked == true);

        _text = _build(request);
        _preview.Text = _text;

        // Said because GitHub's own limit is 65,536 characters for one comment, and an excerpt
        // that will not paste is better found here than in a browser with the issue half written.
        var long_ = _text.Length > MostCharacters;

        _size.Text = long_
            ? $"{_text.Length:N0} characters — too long for one GitHub comment; shorten the window "
              + "or save a file and attach it"
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

        _copy.Content = "Copied — paste it into the issue";
        await Task.Delay(TimeSpan.FromSeconds(1.6));
        Close();
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

    private static NumericUpDown Minutes(string name, int start, int most, int least) => new()
    {
        Name = name,
        Value = start,
        Minimum = least,
        Maximum = most,
        Increment = 1,
        FormatString = "0",
        Width = 96,
        VerticalAlignment = VerticalAlignment.Center,
    };

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
