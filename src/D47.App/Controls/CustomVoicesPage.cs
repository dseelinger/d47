using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using D47.App.Settings;
using D47.App.Theming;
using D47.App.Timing;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Speech;

namespace D47.App.Controls;

/// <summary>Your voice and the Commander's custom voices: the list, and Add a voice, recorded or imported.</summary>
public sealed class CustomVoicesPage : DialogPage
{
    public override string Crumb => "Custom voices";

    /// <summary>The page suggests it; any words will do.</summary>
    public const string SuggestedSentence = "Fuel is good, the route is plotted, and the station knows we are coming.";

    /// <summary>The formats Choose a file offers.</summary>
    public static readonly string[] Formats = ["wav", "mp3", "m4a", "wma", "flac"];

    private const double PageMargin = 28;

    private const double ScrollBarWidth = 16;

    private static readonly string[] Genders = ["female", "male", string.Empty];
    private static readonly string[] Pitches = ["low", "mid", "high"];
    private static readonly string[] Paces = ["slow", "even", "brisk"];

    private readonly CustomVoicesEditing _editing;

    private readonly Notice _chatterbox = new(NoticeLevel.Warning) { Name = "Chatterbox", Label = "Chatterbox not downloaded" };
    private readonly Button _download = new() { Name = "DownloadChatterbox", Content = "Download" };
    private readonly StackPanel _list = new();

    private readonly Notice _refused = new(NoticeLevel.Error) { Name = "NotKept", Label = "Not kept", Margin = new Thickness(0, 12, 0, 4) };
    private readonly TextBox _name = new() { Name = "VoiceName", PlaceholderText = "A name for this voice" };
    private readonly Segment _gender = Choice("Gender", "Female", "Male", "Unlabelled");
    private readonly Segment _pitch = Choice("Pitch", "Low", "Mid", "High");
    private readonly Segment _pace = Choice("Pace", "Slow", "Even", "Brisk");

    private readonly Button _record = new() { Name = "RecordTake", Content = "Record" };
    private readonly Button _choose = new() { Name = "ChooseFile", Content = "Choose a file" };
    private readonly TextBlock _formats = Mono(string.Join(" · ", Formats.Select(format => format.ToUpperInvariant())), ThemeManager.GreyKey);
    private readonly TextBlock _source = Mono(string.Empty, ThemeManager.WhiteKey);
    private readonly TextBlock _recordingWord = Chrome("Recording", ThemeManager.CyanKey);
    private readonly TextBlock _elapsed = Mono(string.Empty, ThemeManager.WhiteKey);
    private readonly TextBlock _sentenceNote = Prose(string.Empty, TypeScale.Secondary, ThemeManager.GreyKey);

    private readonly Button _play = new() { Name = "PlayTake", Content = "Play" };
    private readonly Button _save = new() { Name = "SaveTake", Content = "Save" };
    private readonly TextBlock _kept = Mono(string.Empty, ThemeManager.WhiteKey);
    private readonly TextBlock _takeNote = Prose(string.Empty, TypeScale.Secondary, ThemeManager.GreyKey);

    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly List<IDisposable> _recordInk = [];
    private readonly List<IDisposable> _ownInk = [];

    private string? _renaming;
    private string _renameText = string.Empty;
    private string? _saveRefusal;
    private bool _downloading;

    public CustomVoicesPage(CustomVoicesEditing editing)
    {
        _editing = editing;
        Title = "Custom voices";

        _chatterbox.Text =
            "Custom voices are spoken by Chatterbox. You can add and keep voices now, and D47 speaks in them once "
            + "Chatterbox is downloaded.";
        _chatterbox.Detail = $"About {ChatterboxAssets.TotalMegabytes:0} MB · fetched once from huggingface.co";
        _chatterbox.Actions.Add(_download);
        _download.Click += async (_, _) => await DownloadAsync();

        _refused.IsVisible = false;
        _name.MaxLength = CustomVoices.MaxNameLength;
        AutomationProperties.SetName(_name, "Name");

        _record.Click += (_, _) => Record();
        _choose.Click += async (_, _) => await ChooseAsync();
        _play.Click += (_, _) => editing.Draft.Play();
        _save.Click += (_, _) => Save();

        _ticker.Tick += (_, _) => ShowTake();

        var column = new StackPanel
        {
            Name = "CustomVoices",
            HorizontalAlignment = HorizontalAlignment.Left,
            Spacing = 12,
            Children =
            {
                _chatterbox,
                Group(
                    "Your custom voices",
                    "Kept on this PC. Pick one for a core under the voice picker's Custom filter.",
                    _list),
                Group(
                    "Add a voice",
                    "Seven seconds of a voice, recorded here or taken from a file. Pitch and pace are optional.",
                    _refused,
                    Row("Name", _name),
                    Row("Gender", _gender),
                    Row("Pitch", _pitch),
                    Row("Pace", _pace),
                    Row("Sound", Line(_record, _choose, Gap(_formats, 16), Gap(_source, 16), Gap(_recordingWord, 16), Gap(_elapsed, 12))),
                    Sentence(),
                    Row("Take", Line(_play, _save, Gap(_kept, 16), Gap(_takeNote, 12)))),
            },
        };

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(PageMargin, 20, PageMargin, 28), Child = column },
        };

        // The settings page's column: as wide as the pane allows, up to 900.
        scroller.SizeChanged += (_, e) =>
            column.Width = Math.Clamp(e.NewSize.Width - (2 * PageMargin) - ScrollBarWidth, 0, SettingsView.RowsMaxWidth);

        Content = scroller;

        ResetChoices();
        ShowList();
        ShowTake();
        ShowChatterbox();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _editing.Store.Changed += OnListChanged;
        _editing.Own.Changed += OnOwnChanged;
        _editing.Draft.Changed += OnTakeChanged;

        ShowList();
        ShowTake();
        ShowChatterbox();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _editing.Store.Changed -= OnListChanged;
        _editing.Own.Changed -= OnOwnChanged;
        _editing.Draft.Changed -= OnTakeChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        _ticker.Stop();

        if (_editing.Own.Recording)
        {
            _editing.Own.Stop();
        }

        _editing.Draft.Clear();
        base.OnClosed(e);
    }

    private void OnListChanged() => Dispatcher.UIThread.Post(ShowList);

    private void OnOwnChanged() => Dispatcher.UIThread.Post(() =>
    {
        ShowList();
        ShowTake();
    });

    private void OnTakeChanged() => Dispatcher.UIThread.Post(ShowTake);

    // -- The list --

    private void ShowList()
    {
        Release(_ownInk);
        _list.Children.Clear();
        _list.Children.Add(YourVoice());

        var voices = _editing.Store.List();

        if (voices.Count == 0)
        {
            var empty = new Border
            {
                Name = "NoCustomVoices",
                Padding = new Thickness(16),
                Margin = new Thickness(0, 8, 0, 0),
                Child = Prose("No other custom voices yet. Add one below.", TypeScale.Secondary, ThemeManager.GreyKey),
            };
            Ink(empty, Border.BackgroundProperty, ThemeManager.SlabKey);
            _list.Children.Add(empty);
            return;
        }

        foreach (var voice in voices)
        {
            _list.Children.Add(voice.Id == _renaming ? Renaming(voice) : Listed(voice));
        }
    }

    private Control YourVoice()
    {
        var own = _editing.Own;
        var label = SettingsView.RowLabel("Your voice");
        Ink(label, TextBlock.ForegroundProperty, ThemeManager.CyanKey);

        var says = Prose(own.State(), TypeScale.Secondary, ThemeManager.GreyKey);
        says.VerticalAlignment = VerticalAlignment.Center;

        var buttons = new List<Control>();

        if (own.Recording && !own.RecordingCustom)
        {
            var stop = new Button { Name = "StopOwnVoice", Content = "Stop" };
            Selected(stop, _ownInk);
            stop.Click += (_, _) => own.Toggle();
            buttons.Add(stop);
        }
        else if (own.Recorded)
        {
            var play = new Button { Name = "PlayOwnVoice", Content = "Play" };
            play.Click += (_, _) => own.Play();

            var again = new Button { Name = "RecordOwnVoice", Content = "Record again", IsEnabled = !own.Recording };
            again.Click += (_, _) => own.Toggle();

            var delete = Destructive("DeleteOwnVoice", own.Delete);

            buttons.AddRange([play, again, delete]);
        }
        else
        {
            var record = new Button { Name = "RecordOwnVoice", Content = "Record", IsEnabled = !own.Recording };
            record.Click += (_, _) => own.Toggle();
            buttons.Add(record);
        }

        return SettingsView.RowFrame(SettingsView.RowColumns(label, Spread(says, buttons), null), protectedRow: false);
    }

    private Control Listed(CustomVoice voice)
    {
        var label = SettingsView.RowLabel(voice.Name);

        var details = Prose(Describe(voice), TypeScale.Secondary, ThemeManager.GreyKey);
        details.VerticalAlignment = VerticalAlignment.Center;

        var play = new Button { Content = "Play" };
        AutomationProperties.SetName(play, $"Play {voice.Name}");
        play.Click += (_, _) =>
        {
            if (_editing.Store.Clip(voice.Id) is { } clip)
            {
                _editing.Play(clip);
            }
        };

        var rename = new Button { Content = "Rename" };
        AutomationProperties.SetName(rename, $"Rename {voice.Name}");
        rename.Click += (_, _) =>
        {
            _renaming = voice.Id;
            _renameText = voice.Name;
            ShowList();
        };

        var delete = Destructive(null, () => _editing.Store.Delete(voice.Id));
        AutomationProperties.SetName(delete, $"Delete {voice.Name}");

        return SettingsView.RowFrame(SettingsView.RowColumns(label, Spread(details, [play, rename, delete]), null), protectedRow: false);
    }

    private Control Renaming(CustomVoice voice)
    {
        var label = SettingsView.RowLabel(voice.Name);

        var box = new TextBox
        {
            Name = "RenameVoice",
            Text = _renameText,
            MaxLength = CustomVoices.MaxNameLength,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(box, $"New name for {voice.Name}");
        box.TextChanged += (_, _) => _renameText = box.Text ?? string.Empty;

        var save = new Button { Name = "SaveRename", Content = "Save" };
        save.Click += (_, _) =>
        {
            if (_editing.Store.Rename(voice.Id, _renameText) is { } problem)
            {
                FieldMessage.ShowError(box, problem);
                return;
            }

            _renaming = null;
            ShowList();
        };

        var cancel = new Button { Name = "CancelRename", Content = "Cancel" };
        cancel.Click += (_, _) =>
        {
            _renaming = null;
            ShowList();
        };

        Dispatcher.UIThread.Post(() => box.Focus());

        return SettingsView.RowFrame(SettingsView.RowColumns(label, Spread(box, [save, cancel]), null), protectedRow: false);
    }

    /// <summary><c>Gender · Pitch · Pace</c>.</summary>
    internal static string Describe(CustomVoice voice)
    {
        var gender = voice.Gender switch
        {
            "female" => "Female",
            "male" => "Male",
            _ => "Unlabelled",
        };

        return $"{gender} · {Capital(voice.Pitch)} pitch · {Capital(voice.Pace)} pace";
    }

    // -- Add a voice --

    private void Record()
    {
        _saveRefusal = null;

        if (_editing.Draft.Record() is { } failure)
        {
            _saveRefusal = failure;
        }

        ShowTake();
    }

    private async Task ChooseAsync()
    {
        if (StorageProvider is not { } storage)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a recording",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Audio") { Patterns = [.. Formats.Select(format => $"*.{format}")] },
            ],
        });

        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        _saveRefusal = null;
        await _editing.Draft.ImportAsync(path);
    }

    private void Save()
    {
        var name = _name.Text ?? string.Empty;

        if (_editing.Store.NameProblem(name) is { } problem)
        {
            FieldMessage.ShowError(_name, problem);
            return;
        }

        var refused = _editing.Draft.Save(name, Genders[_gender.SelectedIndex], Pitches[_pitch.SelectedIndex], Paces[_pace.SelectedIndex]);

        if (refused is not null && _editing.Store.NameProblem(name) is { } taken)
        {
            FieldMessage.ShowError(_name, taken);
            return;
        }

        _saveRefusal = refused;

        if (refused is null)
        {
            _name.Text = string.Empty;
            FieldMessage.Clear(_name);
            ResetChoices();
        }

        ShowTake();
    }

    private void ResetChoices()
    {
        _gender.SelectedIndex = 2;
        _pitch.SelectedIndex = 1;
        _pace.SelectedIndex = 1;
    }

    private void ShowTake()
    {
        var draft = _editing.Draft;
        var own = _editing.Own;
        var recording = own.RecordingCustom;
        var busy = own.Recording || draft.Decoding;

        var refusal = _saveRefusal ?? draft.Refusal;
        _refused.IsVisible = refusal is not null;
        _refused.Text = refusal;
        _refused.Detail = _saveRefusal is null ? draft.RefusalDetail : null;

        _record.Content = recording ? "Stop" : "Record";
        _record.IsEnabled = recording || !busy;

        Release(_recordInk);
        _record.ClearValue(BackgroundProperty);
        _record.ClearValue(ForegroundProperty);

        if (recording)
        {
            Selected(_record, _recordInk);
        }

        _choose.IsEnabled = !busy;

        var source = draft.Source;
        _formats.IsVisible = !recording && source is null;
        _source.IsVisible = !recording && source is not null;
        _source.Text = source ?? string.Empty;
        _recordingWord.IsVisible = recording;
        _elapsed.IsVisible = recording;
        _elapsed.Text = $"{CustomVoiceDraft.Clock(Min(own.Elapsed, OwnVoice.MaxLength))} / {CustomVoiceDraft.Clock(OwnVoice.MaxLength)}";

        if (recording)
        {
            _ticker.Start();
        }
        else
        {
            _ticker.Stop();
        }

        _sentenceNote.Text = recording
            ? $"Speech recognition is paused while D47 records. It stops by itself at {OwnVoice.MaxLength.TotalSeconds:0} s."
            : $"Any words will do. D47 records from the microphone chosen under Voice input and stops after {OwnVoice.MaxLength.TotalSeconds:0} s.";

        var kept = draft.Kept;
        _play.IsEnabled = kept is not null && !busy;
        _save.IsEnabled = kept is not null && !busy;
        _kept.IsVisible = kept is not null;

        if (kept is { } span)
        {
            var length = span.To - span.From;
            _kept.Text = $"{CustomVoiceDraft.Clock(span.From)} – {CustomVoiceDraft.Clock(span.To)}";
            _takeNote.Text = span.From > TimeSpan.FromSeconds(0.5)
                ? $"The {length.TotalSeconds:0} s kept, after the silence at the start."
                : $"The {length.TotalSeconds:0} s kept.";
        }
        else
        {
            _takeNote.Text = recording
                ? "Ready when the recording stops."
                : draft.Decoding ? "Reading the file." : "Record or choose a file first.";
        }
    }

    // -- Chatterbox --

    private void ShowChatterbox() => _chatterbox.IsVisible = !_editing.ChatterboxInstalled();

    private async Task DownloadAsync()
    {
        if (_downloading)
        {
            return;
        }

        _downloading = true;
        _download.IsEnabled = false;

        var progress = new Progress<double>(fraction => _download.Content = $"{fraction:P0}");

        try
        {
            if (await _editing.DownloadChatterbox(progress, CancellationToken.None) is { Length: > 0 } said)
            {
                _chatterbox.Text = said;
            }
        }
        catch (Exception ex) when (ex is TtsException or HttpRequestException or IOException or TaskCanceledException)
        {
            _chatterbox.Text = $"Chatterbox could not be downloaded: {ex.Message}";
        }
        finally
        {
            _downloading = false;
            _download.IsEnabled = true;
            _download.Content = "Download";
            ShowChatterbox();
        }
    }

    // -- Dress --

    private static Segment Choice(string name, params string[] options)
    {
        var segment = new Segment { Name = name, ItemsSource = options, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(segment, name);
        SettingsView.DressAsAChoice(segment);
        return segment;
    }

    private static Control Group(string title, string help, params Control[] rows)
    {
        var reset = SettingsView.ResetGlyph("NoReset", $"Reset {title}");
        reset.IsVisible = false;

        var (head, _, _) = SettingsView.GroupHead(title, help, reset);
        var group = new StackPanel { Children = { head } };

        foreach (var row in rows)
        {
            group.Children.Add(row);
        }

        return group;
    }

    private static Control Row(string label, Control control)
    {
        control.VerticalAlignment = VerticalAlignment.Center;

        return SettingsView.RowFrame(SettingsView.RowColumns(SettingsView.RowLabel(label), control, null), protectedRow: false);
    }

    private Control Sentence()
    {
        var slab = new Border
        {
            Padding = new Thickness(16, 14),
            Margin = new Thickness(0, 8),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    Chrome("A sentence to read", ThemeManager.GreyKey),
                    Prose($"“{SuggestedSentence}”", TypeScale.Body, ThemeManager.WhiteKey),
                    _sentenceNote,
                },
            },
        };
        Ink(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    /// <summary>Text taking the free width, then the tiles 2px apart.</summary>
    private static Control Spread(Control text, IReadOnlyList<Control> buttons)
    {
        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, VerticalAlignment = VerticalAlignment.Center };

        foreach (var button in buttons)
        {
            tiles.Children.Add(button);
        }

        var line = new DockPanel();
        DockPanel.SetDock(tiles, Dock.Right);
        text.Margin = new Thickness(0, 0, 12, 0);
        line.Children.Add(tiles);
        line.Children.Add(text);

        return line;
    }

    private static Control Line(params Control[] parts)
    {
        var line = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = Gaps.Tile, VerticalAlignment = VerticalAlignment.Center };

        foreach (var part in parts)
        {
            part.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(part);
        }

        return line;
    }

    private static Control Gap(Control part, double left)
    {
        part.Margin = new Thickness(left, 0, 0, 0);
        return part;
    }

    /// <summary>A destructive tile that asks "Press again to confirm" before it acts.</summary>
    private static Button Destructive(string? name, Action act)
    {
        var button = new Button { Content = "Delete", Classes = { "destructive" } };

        if (name is not null)
        {
            button.Name = name;
        }

        var armed = false;
        IDisposable? lapse = null;

        void Disarm()
        {
            armed = false;
            button.Content = "Delete";
            lapse?.Dispose();
        }

        button.Click += (_, _) =>
        {
            if (!armed)
            {
                armed = true;
                button.Content = "Press again to confirm";
                lapse?.Dispose();
                lapse = OneShot.Dispatcher(SettingsView.ConfirmPressWindow, Disarm);
                return;
            }

            Disarm();
            act();
        };

        return button;
    }

    /// <summary>A tile drawn as the selected one: solid A with Knock ink, held in <paramref name="inks"/>.</summary>
    private static void Selected(Button button, List<IDisposable> inks)
    {
        inks.Add(Ink(button, BackgroundProperty, ThemeManager.AKey));
        inks.Add(Ink(button, ForegroundProperty, ThemeManager.KnockKey));
    }

    private static void Release(List<IDisposable> inks)
    {
        foreach (var ink in inks)
        {
            ink.Dispose();
        }

        inks.Clear();
    }

    private static TextBlock Prose(string text, double size, string ink)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.ProseFamily),
            FontSize = size,
            LineHeight = size * 1.5,
            TextWrapping = TextWrapping.Wrap,
        };
        Ink(block, TextBlock.ForegroundProperty, ink);
        return block;
    }

    private static TextBlock Mono(string text, string ink)
    {
        var block = new TextBlock { Text = text, FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Small };
        Ink(block, TextBlock.ForegroundProperty, ink);
        return block;
    }

    private static TextBlock Chrome(string text, string ink)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Caption,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Caption * Fonts.ChromeTracking,
        };
        Ink(block, TextBlock.ForegroundProperty, ink);
        return block;
    }

    private static string Capital(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static IDisposable Ink(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.GetResourceObservable(key));
}
