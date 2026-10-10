using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Input;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Listening;

namespace D47.App.Settings;

/// <summary>A local voice model the setup wizard fetches: its name, size and the download itself.</summary>
public sealed record LocalVoiceDownload(string Name, double Megabytes, LongPress Fetch);

/// <summary>
/// The first-run setup: welcome, three provider choices, the keys they need, the talk button, and a
/// summary. Nothing is saved until START; SKIP SETUP and Esc close it with every setting unchanged.
/// </summary>
public sealed class SetupWizard : ModalDialog
{
    public enum Step
    {
        Welcome,
        Conversation,
        Voice,
        Listening,
        Keys,
        TalkButton,
        Ready,
    }

    public const int StepCount = 7;

    /// <summary>The talk modes, in the order the segment shows them.</summary>
    public static IReadOnlyList<(string Mode, string Label)> TalkModes { get; } =
    [
        (ListeningCapability.HoldMode, "Hold"),
        (ListeningCapability.ToggleMode, "Toggle"),
        (ListeningCapability.ContinuousMode, "Always on"),
        (ListeningCapability.WakeMode, "Wake word"),
    ];

    private static readonly D47Settings Defaults = new();

    private readonly SettingsService _settings;
    private readonly Func<EliteBinds> _binds;
    private readonly SwitchEditing? _switches;
    private readonly Action? _openPrivacy;
    private readonly Func<string, LocalVoiceDownload?>? _localVoice;

    /// <summary>Kept across redraws, because a stick capture reports into it while it runs.</summary>
    private readonly StatusLine _message = new();

    private CancellationTokenSource? _capture;
    private string? _capturing;

    /// <summary>What START could not save, shown on the Ready step.</summary>
    private IReadOnlyList<SettingApplyResult> _refused = [];

    private CancellationTokenSource? _download;
    private LocalVoiceDownload? _fetching;
    private string? _downloadFailure;
    private double _fraction;
    private ProgressBar? _bar;
    private TextBlock? _fetched;

    public SetupWizard(
        SettingsService settings,
        Func<EliteBinds>? binds = null,
        SwitchEditing? switches = null,
        Action? openPrivacy = null,
        Func<string, LocalVoiceDownload?>? localVoice = null)
    {
        _settings = settings;
        _binds = binds ?? (() => EliteBinds.None);
        _switches = switches;
        _openPrivacy = openPrivacy;
        _localVoice = localVoice;

        Title = "Set up Directive 47";
        Width = Modal.Width;
        MaxHeight = 720;

        Choices = SetupChoices.From(settings.Current);
        Render();
    }

    public Step Current { get; private set; }

    /// <summary>What has been chosen so far; saved only by START.</summary>
    public SetupChoices Choices { get; private set; }

    /// <summary>Whether START saved the choices.</summary>
    public bool Started { get; private set; }

    /// <summary>Whether the download step is showing, after START saved a local voice that is not installed.</summary>
    public bool IsDownloading { get; private set; }

    /// <summary>The keys the current choices need.</summary>
    public IReadOnlyList<SetupKey> Keys => FirstRun.Keys(_settings, Choices);

    /// <summary>Shows a step.</summary>
    public void Go(Step step)
    {
        CancelCapture();

        if (step != Step.Ready)
        {
            _refused = [];
        }

        Current = step;
        Render();
    }

    private void Render()
    {
        var step = Current;

        if (IsDownloading)
        {
            RenderDownload();
            return;
        }

        var (title, body) = step switch
        {
            Step.Welcome => ("Welcome, Commander", Welcome()),
            Step.Conversation => ("Choose an AI for conversation", Providers(SetupSlot.Conversation)),
            Step.Voice => ("Choose a voice", Providers(SetupSlot.Voice)),
            Step.Listening => ("Choose how D47 hears you", Providers(SetupSlot.Listening)),
            Step.Keys => ("Add your keys", KeysBody()),
            Step.TalkButton => ("Choose a talk button", TalkButtonBody()),
            _ => ("Ready, Commander", ReadyBody()),
        };

        var number = (int)step + 1;
        var context = step == Step.Welcome
            ? $"Set up · Step {number} of {StepCount}"
            : $"Set up · Step {number} of {StepCount} · {Label(step)}";

        var back = new Button { Name = "SetupBack", Content = "BACK", IsEnabled = step != Step.Welcome };
        back.Click += (_, _) => Go(Previous(step));

        var next = new Button
        {
            Name = "SetupNext",
            Content = step switch
            {
                Step.Welcome => "BEGIN",
                Step.Ready => "START",
                _ => "NEXT",
            },
        };

        next.Click += (_, _) =>
        {
            if (step == Step.Ready)
            {
                Start();
            }
            else
            {
                Go(Following(step));
            }
        };

        Button? skip = null;

        if (step != Step.Ready)
        {
            skip = new Button { Name = "SetupSkip", Content = "SKIP SETUP" };
            skip.Click += (_, _) => Close();
        }

        this[!TemplatedControl.BackgroundProperty] = new DynamicResourceExtension(ThemeManager.BarKey);
        Content = Modal.Build(context, title, body, [back, next], below: Progress(step), trailing: skip);
    }

    /// <summary>Saves the choices and closes, or stays on Ready saying what was refused.</summary>
    public void Start()
    {
        CancelCapture();

        _refused = FirstRun.Apply(_settings, Choices);

        if (_refused.Count > 0)
        {
            Go(Step.Ready);
            return;
        }

        Started = true;

        if (_localVoice?.Invoke(Choices.Voice) is { } voice)
        {
            BeginDownload(voice);
            return;
        }

        Close();
    }

    private void BeginDownload(LocalVoiceDownload voice)
    {
        CancelDownload();
        _fetching = voice;
        _downloadFailure = null;
        _fraction = 0;
        IsDownloading = true;
        Go(Step.Ready);

        var source = new CancellationTokenSource();
        _download = source;
        _ = RunDownloadAsync(voice, source);
    }

    private async Task RunDownloadAsync(LocalVoiceDownload voice, CancellationTokenSource source)
    {
        string? failure;

        try
        {
            var progress = new Progress<double>(fraction =>
            {
                if (_download == source)
                {
                    _fraction = fraction;
                    ShowFraction();
                }
            });

            failure = await voice.Fetch(progress, source.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_download != source)
        {
            return;
        }

        if (failure is null)
        {
            Close();
            return;
        }

        _downloadFailure = failure;
        Render();
    }

    private void CancelDownload()
    {
        if (_download is { } source)
        {
            _download = null;
            source.Cancel();
            source.Dispose();
        }
    }

    private void ShowFraction()
    {
        if (_bar is null || _fetched is null || _fetching is null)
        {
            return;
        }

        _bar.Value = _fraction;
        _fetched.Text = Fetched(_fetching, _fraction);
    }

    private static string Fetched(LocalVoiceDownload voice, double fraction) =>
        $"{Math.Clamp(fraction, 0, 1) * voice.Megabytes:0} of {voice.Megabytes:0} MB";

    private void RenderDownload()
    {
        var voice = _fetching!;
        var failed = _downloadFailure is not null;
        var stack = Stack();

        if (failed)
        {
            stack.Children.Add(new Notice(NoticeLevel.Error, inline: true)
            {
                Name = "SetupDownloadFailed",
                Text = _downloadFailure,
            });
        }
        else
        {
            _bar = new ProgressBar { Name = "SetupDownloadProgress", Height = 6, Minimum = 0, Maximum = 1, Value = _fraction };
            _fetched = Mono(Fetched(voice, _fraction));
            _fetched.Name = "SetupDownloadFetched";
            stack.Children.Add(_bar);
            stack.Children.Add(_fetched);
            stack.Children.Add(Help("Fetched once from huggingface.co."));
        }

        Button[] buttons;

        if (failed)
        {
            var retry = new Button { Name = "SetupDownloadRetry", Content = "RETRY" };
            retry.Click += (_, _) => BeginDownload(voice);

            var close = new Button { Name = "SetupDownloadClose", Content = "CLOSE" };
            close.Click += (_, _) => Close();

            buttons = [retry, close];
        }
        else
        {
            var cancel = new Button { Name = "SetupDownloadCancel", Content = "CANCEL" };
            cancel.Click += (_, _) => Close();

            buttons = [cancel];
        }

        this[!TemplatedControl.BackgroundProperty] = new DynamicResourceExtension(ThemeManager.BarKey);
        Content = Modal.Build("Set up · Downloading", $"Downloading {voice.Name}", stack, buttons);
    }

    private Step Following(Step step) =>
        step == Step.Listening && Keys.Count == 0 ? Step.TalkButton : step + 1;

    private Step Previous(Step step) =>
        step == Step.TalkButton && Keys.Count == 0 ? Step.Listening : step - 1;

    private static string Label(Step step) => step switch
    {
        Step.Conversation => "Conversation",
        Step.Voice => "Voice",
        Step.Listening => "Listening",
        Step.Keys => "Keys",
        Step.TalkButton => "Talk button",
        Step.Ready => "Ready",
        _ => "Welcome",
    };

    private static Control Progress(Step step)
    {
        var grid = new Grid { Name = "SetupProgress", ColumnSpacing = Gaps.Tile };

        for (var i = 0; i < StepCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var segment = new Border { Height = 4 };
            Themed(segment, Border.BackgroundProperty, i < (int)step
                ? ThemeManager.AKey
                : i == (int)step ? ThemeManager.CyanKey : ThemeManager.Line2Key);

            Grid.SetColumn(segment, i);
            grid.Children.Add(segment);
        }

        return grid;
    }

    private Control Welcome()
    {
        var stack = Stack();

        stack.Children.Add(Lead(
            "D47 reads the journal Elite writes and answers you out loud. Five short choices set it up."));

        var list = new StackPanel { Spacing = Gaps.Tile };

        (string Name, string Help)[] outline =
        [
            ("Conversation", "Which AI understands you and writes D47's replies."),
            ("Voice", "How D47 sounds when it speaks."),
            ("Listening", "How D47 turns your voice into text."),
            ("Keys", "API keys from your providers, if needed."),
            ("Talk button", "The button you hold to talk."),
        ];

        for (var i = 0; i < outline.Length; i++)
        {
            var number = new TextBlock
            {
                Text = $"{i + 1:00}",
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Control,
                Width = 28,
            };
            Themed(number, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

            var name = Chrome(outline[i].Name, ThemeManager.AKey);
            name.Width = 140;

            var help = Help(outline[i].Help);

            var row = new DockPanel { Children = { number, name, help } };
            DockPanel.SetDock(number, Dock.Left);
            DockPanel.SetDock(name, Dock.Left);

            var slab = new Border { Padding = new Thickness(14, 10), Child = row };
            Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
            list.Children.Add(slab);
        }

        stack.Children.Add(list);
        stack.Children.Add(Help(
            "Every step is optional. With nothing set up, D47 still reads your journal and answers what it "
            + "understands when you type."));

        var links = new UniformGrid { Columns = 2 };
        links.Children.Add(Link("SETUP GUIDE ›", "Opens the setup guide in your browser", Guide("first-run")));
        links.Children.Add(Link("COMPARE PROVIDERS ›", "Opens the provider comparison in your browser", Guide("choosing-providers")));
        stack.Children.Add(links);

        return stack;
    }

    /// <summary>A help page on the site, optionally at an anchor.</summary>
    public static string Guide(string page, string? anchor = null) =>
        $"{DocsSite.Root}{page}.html{(anchor is null ? string.Empty : $"#{anchor}")}";

    private Control Providers(SetupSlot slot)
    {
        var stack = Stack();

        stack.Children.Add(Lead(slot switch
        {
            SetupSlot.Conversation =>
                "This is the AI that understands what you ask and writes D47's replies. Commands D47 "
                + "recognises work without one.",
            SetupSlot.Voice =>
                "This turns D47's replies into speech. You can pick the voice itself later in Settings.",
            _ => "This turns what you say into text for D47. It hears you while you hold the talk button, or "
                 + "all the time in the hands-free modes.",
        }));

        var list = new StackPanel { Name = "SetupOptions", Spacing = Gaps.Tile };

        foreach (var option in Options(slot))
        {
            var selected = string.Equals(Choices.For(slot), option.Id, StringComparison.OrdinalIgnoreCase);
            list.Children.Add(OptionRow(slot, option, selected));
        }

        stack.Children.Add(list);

        var (words, anchor) = slot switch
        {
            SetupSlot.Conversation => ("COMPARE AI PROVIDERS ›", "conversation"),
            SetupSlot.Voice => ("COMPARE VOICES ›", "voice"),
            _ => ("COMPARE LISTENING ›", "listening"),
        };

        stack.Children.Add(Link(words, "Opens the provider comparison in your browser", Guide("choosing-providers", anchor)));

        return stack;
    }

    /// <summary>One catalog entry as the wizard offers it.</summary>
    public sealed record Option(string Id, string Name, string Summary, string Tag);

    /// <summary>Every entry of a slot's catalog, in the order offered.</summary>
    public static IReadOnlyList<Option> Options(SetupSlot slot) => slot switch
    {
        SetupSlot.Conversation =>
        [
            .. LlmProviderCatalog.All
                .OrderBy(provider => provider.Id == LlmProviderCatalog.NoneId)
                .Select(provider => new Option(
                    provider.Id,
                    provider.Name,
                    WithDefault(provider.Summary, provider.Id == Defaults.Llm.Provider),
                    provider.NeedsKey ? "Key · pay per use"
                    : provider.KeyOptional ? "Address · key optional"
                    : "Free · no key")),
        ],
        SetupSlot.Voice =>
        [
            .. TtsProviderCatalog.All.Select(provider => new Option(
                provider.Id,
                provider.Name,
                WithDefault(provider.Summary, provider.Id == Defaults.Speech.Provider),
                provider.NeedsKey ? (provider.Billed ? "Key · pay per use" : "Key")
                : TtsProviderCatalog.IsLocal(provider.Id) ? "Free · local"
                : "Free · no key")),
        ],
        _ =>
        [
            .. SttProviderCatalog.All.Select(provider => new Option(
                provider.Id,
                provider.Name,
                WithDefault(provider.Summary, provider.Id == Defaults.Listening.Provider),
                provider.Hosted ? "Key · pay per use" : "Free · local")),
        ],
    };

    private static string WithDefault(string summary, bool isDefault) =>
        isDefault ? $"{summary} The default." : summary;

    private Button OptionRow(SetupSlot slot, Option option, bool selected)
    {
        var box = new Border
        {
            Width = 16,
            Height = 16,
            BorderThickness = new Thickness(2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Border { Width = 8, Height = 8, IsVisible = selected },
        };

        // A selected row is filled A, so its words go Knock and its tag Brown.
        var ink = selected ? ThemeManager.KnockKey : ThemeManager.AKey;

        Themed(box, Border.BorderBrushProperty, ink);
        Themed(box.Child, Border.BackgroundProperty, ink);

        var name = Chrome(option.Name, ink);

        var summary = new TextBlock
        {
            Text = option.Summary,
            FontSize = TypeScale.Control,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0),
        };
        Themed(summary, TextBlock.ForegroundProperty, selected ? ThemeManager.KnockKey : ThemeManager.GreyKey);

        var tag = new TextBlock
        {
            Text = option.Tag.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.MetaSmall,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        Themed(tag, TextBlock.ForegroundProperty, selected ? ThemeManager.BrownKey : ThemeManager.GreyKey);

        var words = new StackPanel { Children = { name, summary }, Margin = new Thickness(12, 0, 0, 0) };

        var layout = new DockPanel { Children = { box, tag, words } };
        DockPanel.SetDock(box, Dock.Left);
        DockPanel.SetDock(tag, Dock.Right);

        var row = ListRow.Dress(
            new Button
            {
                Name = $"SetupOption-{option.Id}",
                Content = layout,
                MinHeight = 56,
                Padding = new Thickness(12, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            },
            selected);

        AutomationProperties.SetName(row, option.Name);

        row.Click += (_, _) => Choose(Choices.With(slot, option.Id));

        return row;
    }

    private Control KeysBody()
    {
        var stack = Stack();
        var keys = Keys;

        stack.Children.Add(Lead(
            (keys.Count == 1 ? "Your choices need one API key." : $"Your choices need {keys.Count} API keys, one from each provider.")
            + " Each is stored encrypted for this Windows account, and D47 never shows it back to you."));

        foreach (var key in keys)
        {
            var group = new StackPanel { Spacing = 10 };

            group.Children.Add(Head(key.Row.Label, $"For {Serves(key.Serves)}"));
            group.Children.Add(new SecretEditor(key.Row, _settings));

            var egress = Mono(Egress(key.Egress));
            egress.Name = "SetupKeyEgress";

            var line = new DockPanel();

            if (key.KeyPage is { } page)
            {
                var get = Link("GET A KEY ›", $"Opens {Host(page)} in your browser", page);
                DockPanel.SetDock(get, Dock.Right);
                line.Children.Add(get);
            }

            line.Children.Add(egress);
            group.Children.Add(line);

            stack.Children.Add(group);
        }

        stack.Children.Add(Help("Skip a key and that part uses the free choice until you add one in Settings."));
        stack.Children.Add(Link("HOW TO GET EACH KEY ›", "Opens the setup guide in your browser", Guide("first-run", "keys")));

        return stack;
    }

    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    /// <summary>One line for a key: where it sends, then what, from the disclosure of each slot it serves.</summary>
    private static string Egress(IReadOnlyList<EgressEntry> entries)
    {
        var destinations = string.Join(", ", entries.Select(entry => entry.Destination).Distinct(StringComparer.OrdinalIgnoreCase));
        var summaries = string.Join(" ", entries.Select(entry => entry.Summary).Distinct(StringComparer.Ordinal));

        return $"{destinations} · {summaries}";
    }

    private static string Serves(IReadOnlyList<SetupSlot> slots)
    {
        var words = slots.Select(slot => slot.ToString().ToLowerInvariant()).ToList();

        return words.Count switch
        {
            1 => words[0],
            2 => $"{words[0]} and {words[1]}",
            _ => $"{string.Join(", ", words.Take(words.Count - 1))} and {words[^1]}",
        };
    }

    private Control TalkButtonBody()
    {
        var stack = Stack();
        var binds = _binds();

        stack.Children.Add(Lead(
            "Hold it while you talk to D47. A spare button on your stick or throttle is easiest to reach in flight."));

        var message = _message;
        (message.Parent as Avalonia.Controls.Panel)?.Children.Remove(message);

        // The stick.
        var stick = new StackPanel { Spacing = 10 };
        stick.Children.Add(Head("Stick or throttle", "Recommended"));

        var button = HotasButton.Parse(Choices.TalkButton);
        var waitingForStick = _capturing == ListeningCapability.PushToTalkButtonKey;

        stick.Children.Add(BindLine(
            "SetupStick",
            waitingForStick
                ? "Press a button on your stick…"
                : _switches is null ? "No controllers" : button?.Describe() ?? "None",
            waitingForStick,
            canBind: _switches is not null,
            bound: button is not null,
            bind: () => _ = CaptureAsync(ListeningCapability.PushToTalkButtonKey, message),
            clear: () => Choose(Choices with { TalkButton = null })));

        if (button is { } held
            && binds.IsKnown
            && binds.SharingButton(held, _switches?.Reader.Poll() ?? []) is { Bindings.Count: > 0 } sharing)
        {
            var actions = Actions(sharing.Bindings);

            stick.Children.Add(Warning(sharing.Interfaces switch
            {
                0 => $"Elite ({binds.PresetName}) binds a button of that number to {actions}. "
                     + "If it is the same controller, one of the two will not work.",
                1 => $"Elite ({binds.PresetName}) also binds this button to {actions}. One of the two will not work.",
                var interfaces => $"Windows shows this controller as {interfaces} devices, and Elite "
                                  + $"({binds.PresetName}) binds a button of that number on one of them to {actions}. "
                                  + "If it is the device this button is on, one of the two will not work.",
            }));
        }

        stack.Children.Add(stick);

        // The keyboard.
        var keyboard = new StackPanel { Spacing = 10 };
        keyboard.Children.Add(Head("Keyboard", "Works with either"));

        var waitingForKey = _capturing == ListeningCapability.PushToTalkKeyKey;

        keyboard.Children.Add(BindLine(
            "SetupKey",
            waitingForKey
                ? "Press a key…"
                : Choices.TalkKey is { Length: > 0 } key ? Gestures.Describe(key) : "None",
            waitingForKey,
            canBind: true,
            bound: Choices.TalkKey is { Length: > 0 },
            bind: () => _ = CaptureAsync(ListeningCapability.PushToTalkKeyKey, message),
            clear: () => Choose(Choices with { TalkKey = null })));

        if (Choices.TalkKey is { Length: > 0 } gesture && binds.IsKnown && binds.Using(gesture) is { Count: > 0 } collisions)
        {
            keyboard.Children.Add(Warning(
                $"Elite ({binds.PresetName}) also binds {Gestures.Describe(gesture)} to {Actions(collisions)}. "
                + "One of the two will not work."));
        }

        keyboard.Children.Add(Help(
            "Elite's default keyboard layout and Windows leave Scroll Lock free. If your keyboard has none, "
            + "bind another key."));

        stack.Children.Add(keyboard);
        stack.Children.Add(message);

        // The mode.
        var mode = new StackPanel { Spacing = 10 };
        mode.Children.Add(Head("How it listens", null));

        var index = TalkModes.ToList().FindIndex(
            entry => string.Equals(entry.Mode, Choices.TalkMode, StringComparison.OrdinalIgnoreCase));

        var segment = new Segment
        {
            Name = "SetupMode",
            ItemsSource = [.. TalkModes.Select(entry => entry.Label)],
            SelectedIndex = index,
        };

        segment.SelectionChanged += (_, _) =>
        {
            if (segment.SelectedIndex >= 0)
            {
                Choose(Choices with { TalkMode = TalkModes[segment.SelectedIndex].Mode });
            }
        };

        mode.Children.Add(segment);
        stack.Children.Add(mode);

        if (Choices.TalkKey is { Length: > 0 } || button is not null || ListeningCapability.IsHandsFree(Choices.TalkMode))
        {
            stack.Children.Add(Ready());
        }

        return stack;
    }

    private static string Actions(IReadOnlyList<EliteBinding> bindings) =>
        string.Join(", ", bindings.Select(binding => binding.Action).Distinct(StringComparer.Ordinal));

    private Control Ready()
    {
        var dot = new Border { Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Center };
        Themed(dot, Border.BackgroundProperty, ThemeManager.CyanKey);

        var status = Chrome("PTT ready", ThemeManager.CyanKey);
        status.Margin = new Thickness(14, 0);

        var help = Help(Choices.TalkMode switch
        {
            ListeningCapability.ToggleMode => "Press your button, talk, and press it again.",
            ListeningCapability.ContinuousMode => "D47 listens whenever you speak.",
            ListeningCapability.WakeMode => "Say D47's name, then what you want.",
            _ => "Hold your button and say something.",
        });

        var row = new DockPanel { Name = "SetupPttReady", Children = { dot, status, help } };
        DockPanel.SetDock(dot, Dock.Left);
        DockPanel.SetDock(status, Dock.Left);

        var slab = new Border { Padding = new Thickness(14, 12), Child = row };
        Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    private void Choose(SetupChoices choices)
    {
        Choices = choices;
        Render();
    }

    private async Task CaptureAsync(string key, StatusLine message)
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        CancelCapture();

        var cancel = new CancellationTokenSource();
        _capture = cancel;
        _capturing = key;
        Render();

        var stick = key == ListeningCapability.PushToTalkButtonKey;

        var caught = await BindCapture.RunAsync(
            top,
            stick ? null : key,
            bare: _settings.Find(key) is { SystemWide: false },
            stick ? key : null,
            _switches,
            message,
            cancel.Token);

        if (!ReferenceEquals(_capture, cancel))
        {
            return;
        }

        _capture = null;
        _capturing = null;
        cancel.Dispose();

        if (caught is not { } pressed)
        {
            Render();
        }
        else if (stick)
        {
            Choose(Choices with { TalkButton = pressed.Value });
        }
        else
        {
            Choose(Choices with { TalkKey = pressed.Value });
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelCapture();
        CancelDownload();
        base.OnClosed(e);
    }

    private void CancelCapture()
    {
        if (_capture is { } capture)
        {
            _capture = null;
            _capturing = null;
            capture.Cancel();
            capture.Dispose();
        }
    }

    private Control BindLine(string name, string chip, bool waiting, bool canBind, bool bound, Action bind, Action clear)
    {
        var text = new TextBlock
        {
            Name = $"{name}Chip",
            Text = chip.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Tip,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(text, TextBlock.ForegroundProperty, waiting ? ThemeManager.CyanKey : bound ? ThemeManager.WhiteKey : ThemeManager.Grey2Key);

        var slab = new Border { Padding = new Thickness(14, 0), MinHeight = TypeScale.MinimumTarget, Child = text };
        Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        if (waiting)
        {
            slab.BorderThickness = new Thickness(1);
            Themed(slab, Border.BorderBrushProperty, ThemeManager.CyanKey);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Margin = Gaps.TileBefore };

        if (waiting)
        {
            var cancel = new Button { Name = $"{name}Cancel", Content = "CANCEL" };
            cancel.Click += (_, _) =>
            {
                CancelCapture();
                Render();
            };
            buttons.Children.Add(cancel);
        }
        else
        {
            var press = new Button { Name = $"{name}Bind", Content = "BIND", IsEnabled = canBind };
            press.Click += (_, _) => bind();

            var wipe = new Button { Name = $"{name}Clear", Content = "CLEAR", IsEnabled = bound };
            wipe.Click += (_, _) => clear();

            buttons.Children.Add(press);
            buttons.Children.Add(wipe);
        }

        var line = new DockPanel { Children = { buttons, slab } };
        DockPanel.SetDock(buttons, Dock.Right);

        return line;
    }

    private Control ReadyBody()
    {
        var stack = Stack();
        var hasSecret = (string name) => _settings.HasSecret(name);
        var effective = FirstRun.Effective(Choices, hasSecret);

        stack.Children.Add(Lead("Check your choices and what they send. Select one to change it."));

        if (_localVoice?.Invoke(Choices.Voice) is { } voice)
        {
            var note = Help($"START downloads about {voice.Megabytes:0} MB from huggingface.co, for the {voice.Name} voice.");
            note.Name = "SetupDownloadNote";
            stack.Children.Add(note);
        }

        if (_refused.Count > 0)
        {
            stack.Children.Add(new Notice(NoticeLevel.Error, inline: true)
            {
                Name = "SetupRefused",
                Text = string.Join(" ", _refused.Select(result => result.Message)),
            });
        }

        var grid = new UniformGrid { Name = "SetupSummary", Columns = 2 };

        foreach (var slot in FirstRun.Slots)
        {
            var chosen = Options(slot).FirstOrDefault(option => option.Id == Choices.For(slot));
            var saved = Options(slot).FirstOrDefault(option => option.Id == effective.For(slot));
            var note = chosen is not null && saved is not null && chosen.Id != saved.Id
                ? $"{chosen.Name} has no key"
                : null;

            grid.Children.Add(SummaryTile(
                slot.ToString(),
                saved?.Name ?? effective.For(slot),
                note,
                slot switch
                {
                    SetupSlot.Conversation => Step.Conversation,
                    SetupSlot.Voice => Step.Voice,
                    _ => Step.Listening,
                }));
        }

        grid.Children.Add(SummaryTile("Talk button", TalkSummary(), null, Step.TalkButton));
        stack.Children.Add(grid);

        var egress = new StackPanel { Name = "SetupEgress" };
        egress.Children.Add(Head("What leaves this machine", null));

        foreach (var entry in FirstRun.Destinations(_settings, Choices))
        {
            var name = Chrome(DestinationName(entry), ThemeManager.WhiteKey);
            var where = Mono(entry.Destination);
            where.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(where, Dock.Right);

            var top = new DockPanel { Children = { where, name } };

            var what = Help(entry.Summary);
            what.Margin = new Thickness(0, 4, 0, 0);

            var row = new Border
            {
                Padding = new Thickness(0, 10),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new StackPanel { Children = { top, what } },
            };
            Themed(row, Border.BorderBrushProperty, ThemeManager.Line2Key);

            egress.Children.Add(row);
        }

        if (_openPrivacy is { } open)
        {
            var privacy = new Button
            {
                Name = "SetupPrivacy",
                Content = "START, THEN OPEN PRIVACY AND EGRESS ›",
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            AutomationProperties.SetName(privacy, "Start, then open Settings, Privacy and egress");

            privacy.Click += (_, _) =>
            {
                Start();

                if (Started && !IsDownloading)
                {
                    open();
                }
            };

            egress.Children.Add(privacy);
        }

        stack.Children.Add(egress);
        stack.Children.Add(Help("Change any of this in Settings. Reopen this guide from About › Set up keys."));

        return stack;
    }

    /// <summary>The disclosure's own name, except where it would say "language model".</summary>
    private static string DestinationName(EgressEntry entry) =>
        entry.Id == EgressDisclosure.LanguageModel ? "Conversation AI" : entry.Name;

    private string TalkSummary()
    {
        var parts = new List<string>();

        if (HotasButton.Parse(Choices.TalkButton) is { } button)
        {
            parts.Add(button.Describe());
        }

        if (Choices.TalkKey is { Length: > 0 } key)
        {
            parts.Add(Gestures.Describe(key));
        }

        if (parts.Count == 0)
        {
            parts.Add("None");
        }

        var index = TalkModes.ToList().FindIndex(
            entry => string.Equals(entry.Mode, Choices.TalkMode, StringComparison.OrdinalIgnoreCase));
        var mode = index >= 0 ? TalkModes[index].Label : Choices.TalkMode;

        return $"{string.Join(" or ", parts)} · {mode}";
    }

    private Button SummaryTile(string label, string value, string? note, Step step)
    {
        var words = new StackPanel { Spacing = 4 };
        words.Children.Add(ListRow.Sub(new TextBlock { Text = label }));
        words.Children.Add(ListRow.Name(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap }));

        if (note is not null)
        {
            words.Children.Add(ListRow.SecondaryInk(new TextBlock { Text = note, FontSize = TypeScale.Control, TextWrapping = TextWrapping.Wrap }));
        }

        var tile = ListRow.Dress(new Button
        {
            Name = $"SetupGoTo{step}",
            Content = words,
            Margin = new Thickness(0, 0, Gaps.Tile, Gaps.Tile),
            Padding = new Thickness(14, 12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
        });

        AutomationProperties.SetName(tile, $"{label}: {value}. Change it.");
        tile.Click += (_, _) => Go(step);

        return tile;
    }

    private static StackPanel Stack() => new() { Spacing = Modal.BlockGap };

    private static Control Head(string text, string? aside)
    {
        var name = Chrome(text, ThemeManager.WhiteKey);
        name.FontSize = TypeScale.Section;

        var row = new DockPanel();

        if (aside is not null)
        {
            var right = Chrome(aside, ThemeManager.GreyKey);
            right.FontSize = TypeScale.Meta;
            right.FontWeight = FontWeight.Medium;
            right.VerticalAlignment = VerticalAlignment.Bottom;
            DockPanel.SetDock(right, Dock.Right);
            row.Children.Add(right);
        }

        row.Children.Add(name);

        var head = new Border { Padding = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(0, 0, 0, 1), Child = row };
        Themed(head, Border.BorderBrushProperty, ThemeManager.AKey);

        return head;
    }

    private static TextBlock Chrome(string text, string ink)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Control,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Control * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, ink);

        return block;
    }

    private static TextBlock Lead(string text)
    {
        var block = new TextBlock { Text = text, FontSize = TypeScale.Body, TextWrapping = TextWrapping.Wrap };
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return block;
    }

    private static TextBlock Help(string text)
    {
        var block = new TextBlock { Text = text, FontSize = TypeScale.Tip, TextWrapping = TextWrapping.Wrap };
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return block;
    }

    private static TextBlock Mono(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Meta,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return block;
    }

    private static Control Warning(string text) =>
        new Notice(NoticeLevel.Warning, inline: true) { Name = "SetupCollision", Text = text };

    private static Button Link(string words, string says, string url)
    {
        var link = Glyphs.Quiet(new Button { Tag = url, HorizontalAlignment = HorizontalAlignment.Left }, words, says);
        link.Click += (_, _) => SiteHelpMark.Open(url);

        return link;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
