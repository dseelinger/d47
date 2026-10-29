using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Characters;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>
/// Asset Mgmt › Characters: the carrier's captain and tower, the running story's cast and the Commander's own
/// characters, each with a picture, name, voice, personality notes and a place.
/// </summary>
public sealed class CharactersPage : UserControl, IPageSummary
{
    public const string RootKey = "loadout.characters";

    public const string NameBoxName = "CharacterName";

    public const string NotesBoxName = "CharacterNotes";

    public const string SystemBoxName = "CharacterSystem";

    public const string AboardName = "CharacterAboard";

    public const string VoiceName = "CharacterVoice";

    public const string DistanceName = "CharacterDistance";

    public const string AddName = "CharacterAdd";

    public const string SaveName = "CharacterSave";

    public const string CancelName = "CharacterCancel";

    public const string RemoveName = "CharacterRemove";

    public const string RemoveConfirmName = "CharacterRemoveConfirm";

    public const string RemoveCancelName = "CharacterRemoveCancel";

    public const string ResetName = "CharacterReset";

    public const string SidebarName = "CharactersSidebar";

    public const string CaptainKey = "captain";

    public const string TowerKey = "tower";

    public const string DraftKey = "new";

    public const string NoVoice = "Same as the ship";

    public const string NoStory = "No story is running.";

    private const string StoryPrefix = "story:";

    private const string CustomPrefix = "custom:";

    private const double WindowSidebar = 280;

    private const double BigSidebar = 220;

    private const double DetailMax = 900;

    private readonly CharactersHost _host;
    private readonly Func<CommanderGameState?> _state;
    private readonly PanelPrompts _prompts;
    private readonly SpeakerPortraits? _portraits;
    private readonly Func<VoiceRole, string?>? _carrierPicture;
    private readonly bool _big;
    private readonly ContentControl _sidebar = new();
    private readonly StackPanel _detail = new() { Spacing = 14, MaxWidth = DetailMax, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Dictionary<CharacterField, string> _errors = [];

    private string? _selected;
    private Draft? _draft;
    private bool _removing;
    private bool _editing;
    private object? _seen;
    private TextBlock? _distanceText;
    private (string? Here, string? There)? _distanceFor;
    private double? _distance;
    private bool _distanceKnown;
    private CancellationTokenSource? _lookup;

    public CharactersPage(
        CharactersHost host,
        Func<CommanderGameState?> state,
        PanelPrompts prompts,
        SpeakerPortraits? portraits = null,
        Func<VoiceRole, string?>? carrierPicture = null,
        bool big = false)
    {
        _host = host;
        _state = state;
        _prompts = prompts;
        _portraits = portraits;
        _carrierPicture = carrierPicture;
        _big = big;

        var layout = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(_sidebar, Dock.Left);
        layout.Children.Add(_sidebar);
        layout.Children.Add(new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(0, 0, 14, 0), Child = _detail },
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });

        Content = layout;
        Draw();
    }

    public string Summary => _big
        ? "Names, voices and notes."
        : "Everyone you can talk to on comms: their names, voices, pictures and notes.";

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>The sidebar key of the character showing.</summary>
    public string? Selected => _selected;

    /// <summary>Completes when the distance last asked for has been looked up.</summary>
    internal Task DistanceSettled { get; private set; } = Task.CompletedTask;

    /// <summary>Redraws when the characters, the settings, the story or the carrier have changed.</summary>
    public bool Tick()
    {
        if (_editing || Equals(Stamp(), _seen))
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>Shows the character at <paramref name="key"/>.</summary>
    public void Select(string key)
    {
        _selected = key;
        _removing = false;
        _errors.Clear();
        Draw();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _lookup?.Cancel();
    }

    private string? Fid => _state()?.Identity.FrontierId;

    private CharacterStore Store => _host.Store();

    private object? Stamp()
    {
        var speech = _host.Settings.Current.Speech;
        var story = _host.Story(Fid);

        return (
            Fid,
            Store.CharactersOf(Fid),
            speech.CarrierCaptainName,
            speech.CarrierCaptainVoice,
            speech.TowerName,
            speech.TowerVoice,
            story?.StoryId,
            story?.Members.Count,
            _state()?.Carrier,
            _state()?.Location.StarSystem);
    }

    private IReadOnlyList<Entry> Entries()
    {
        var speech = _host.Settings.Current.Speech;
        var entries = new List<Entry>
        {
            new(CaptainKey, Kind.Captain, Named(speech.CarrierCaptainName, "Captain")),
            new(TowerKey, Kind.Tower, Named(speech.TowerName, "Tower Control")),
        };

        if (_host.Story(Fid) is { } story)
        {
            entries.AddRange(story.Members.Select(member => new Entry(StoryPrefix + member.Shown.Id, Kind.Story, member.Shown.Name)
            {
                Member = member,
                Story = story,
            }));
        }

        entries.AddRange(Store.CharactersOf(Fid).Select(character => new Entry(CustomPrefix + character.Id, Kind.Custom, character.Name)
        {
            Character = character,
        }));

        if (_draft is not null)
        {
            entries.Add(new Entry(DraftKey, Kind.Draft, "New character"));
        }

        return entries;
    }

    private static string Named(string? name, string fallback) =>
        string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();

    private void Draw()
    {
        _seen = Stamp();
        _lookup?.Cancel();
        _distanceText = null;

        var entries = Entries();
        var entry = entries.FirstOrDefault(e => e.Key == _selected) ?? entries[0];

        _selected = entry.Key;

        _sidebar.Content = SidebarOf(entries, entry.Key);

        _detail.Children.Clear();
        _detail.Children.Add(Group(entry));

        if (entry.Kind == Kind.Draft)
        {
            _detail.Children.Add(DraftFoot());
        }
        else if (entry.Kind == Kind.Custom && !_removing)
        {
            _detail.Children.Add(Tile("REMOVE CHARACTER", RemoveName, () =>
            {
                _removing = true;
                Draw();
            }, destructive: true));
        }
    }

    private Control SidebarOf(IReadOnlyList<Entry> entries, string selected)
    {
        var story = _host.Story(Fid);
        var captain = _host.Settings.Current.Speech;

        SidebarItem Item(Entry e) => e.Kind switch
        {
            Kind.Captain when !string.IsNullOrWhiteSpace(captain.CarrierCaptainName) =>
                new SidebarItem(e.Key, e.Name, "CAPTAIN", ThemeManager.GreyKey),
            Kind.Custom => e.Character!.Aboard
                ? new SidebarItem(e.Key, e.Name, "ABOARD", ThemeManager.GreyKey)
                : new SidebarItem(e.Key, e.Name, (e.Character.System ?? string.Empty).ToUpperInvariant(), ThemeManager.AKey),
            _ => new SidebarItem(e.Key, e.Name),
        };

        var groups = new List<SidebarGroup>
        {
            new("Carrier", [.. entries.Where(e => e.Kind is Kind.Captain or Kind.Tower).Select(Item)]),
            story is null
                ? new SidebarGroup("Story", [], NoStory)
                : new SidebarGroup($"Story · {story.Title}", [.. entries.Where(e => e.Kind == Kind.Story).Select(Item)]),
            new("Yours", [.. entries.Where(e => e.Kind is Kind.Custom or Kind.Draft).Select(Item)]),
        };

        var sidebar = new Sidebar(groups, selected, Select, width: _big ? BigSidebar : WindowSidebar) { Name = SidebarName };

        var add = Tile("ADD A CHARACTER", AddName, StartDraft);
        add.Margin = new Thickness(0, 8, 0, 0);
        sidebar.Append(add);

        return new ScrollViewer
        {
            Content = sidebar,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    private void StartDraft()
    {
        _draft ??= new Draft();
        Select(DraftKey);
    }

    // ----- the detail pane

    private Control Group(Entry entry)
    {
        var group = new StackPanel();

        group.Children.Add(Head(entry));

        if (_removing && entry.Character is { } removing)
        {
            group.Children.Add(RemoveStrip(removing));
            return group;
        }

        group.Children.Add(PictureRow(entry));
        group.Children.Add(NameRow(entry));
        group.Children.Add(VoiceRow(entry));
        group.Children.Add(NotesRow(entry));
        group.Children.Add(WhereRow(entry));

        if (entry.Kind == Kind.Custom || entry.Kind == Kind.Draft)
        {
            AddSystemRow(group, entry);
        }

        return group;
    }

    private Control Head(Entry entry)
    {
        var line = entry.Kind switch
        {
            Kind.Captain => Channel("Your carrier's captain, on comms as ", "CARRIER", "CAPTAIN"),
            Kind.Tower => Channel("Your carrier's tower, on comms as ", "CARRIER", "TOWER"),
            Kind.Story => Channel($"From the story {entry.Story!.Title}, on comms as ", "STORY", entry.Name.ToUpperInvariant()),
            Kind.Draft => Plain("Joins the CHARACTERS channel once saved."),
            _ => _big
                ? Channel("On comms as ", "CHARACTERS", entry.Name.ToUpperInvariant())
                : Channel("Your character, on comms as ", "CHARACTERS", entry.Name.ToUpperInvariant()),
        };

        var name = TitleText.Build(entry.Name, TypeScale.Secondary, TitleRank.Group);
        var head = new StackPanel { Spacing = 2, Children = { name, line } };

        return TitleText.GroupRow(head);
    }

    private static TextBlock Plain(string text)
    {
        var block = new TextBlock { Text = text, FontSize = TypeScale.Small, TextWrapping = TextWrapping.Wrap };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    private static TextBlock Channel(string prefix, string channel, string name)
    {
        var block = new TextBlock { FontSize = TypeScale.Small, TextWrapping = TextWrapping.Wrap };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var channelRun = new Run(channel)
        {
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Small * Fonts.ChromeTracking,
        };
        ThemeInk.Bind(channelRun, Run.ForegroundProperty, ThemeManager.AKey);

        var nameRun = new Run(name) { FontFamily = new FontFamily(Fonts.ChromeFamily), FontWeight = FontWeight.Medium };
        ThemeInk.Bind(nameRun, Run.ForegroundProperty, ThemeManager.WhiteKey);

        block.Inlines!.Add(new Run(prefix));
        block.Inlines.Add(channelRun);
        block.Inlines.Add(new Run(" · "));
        block.Inlines.Add(nameRun);

        return block;
    }

    private double LabelWidth => _big ? 170 : 240;

    private double PictureSize => _big ? 88 : 120;

    private Border Row(string label, Control control, Control? trailing = null, bool top = false)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Center,
            Margin = top ? new Thickness(0, 12, 0, 0) : default,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},*,{TypeScale.MinimumTarget}") };
        grid.Children.Add(text);

        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        if (trailing is not null)
        {
            Grid.SetColumn(trailing, 2);
            grid.Children.Add(trailing);
        }

        var row = new Border
        {
            MinHeight = 56,
            Padding = new Thickness(12, 6),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid,
        };
        ThemeInk.Bind(row, Border.BorderBrushProperty, ThemeManager.LineKey);

        return row;
    }

    private static Border Inset(string text)
    {
        var line = Plain(text);
        var inset = new Border { Padding = new Thickness(12, 8), Margin = new Thickness(0, 6, 0, 0), Child = line };
        ThemeInk.Bind(inset, Border.BackgroundProperty, ThemeManager.SlabKey);
        return inset;
    }

    private Button Tile(string label, string name, Action pressed, bool destructive = false)
    {
        var button = new Button
        {
            Name = name,
            Content = label,
            Height = TypeScale.MinimumTarget,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (destructive)
        {
            button.Classes.Add(SettingsView.DestructiveClass);
        }

        button.Click += (_, _) => pressed();

        return button;
    }

    // ----- picture

    private Control PictureRow(Entry entry)
    {
        Control body;

        switch (entry.Kind)
        {
            case Kind.Captain or Kind.Tower when _portraits is { } portraits:
                var role = entry.Kind == Kind.Captain ? VoiceRole.CarrierCaptain : VoiceRole.TowerControl;
                body = new PictureChooser(
                    portraits.Pictures,
                    _carrierPicture?.Invoke(role),
                    PictureSize,
                    "Fly once with D47 running to set this picture.");
                break;
            case Kind.Custom when _portraits is { } portraits:
                body = new PictureChooser(portraits.Pictures, entry.Character!.Picture, PictureSize);
                break;
            case Kind.Story:
                body = StoryPicture(entry.Member!);
                break;
            case Kind.Draft:
                body = Plain("Save the character first, then choose a picture.");
                break;
            default:
                body = Plain(string.Empty);
                break;
        }

        return Row("Picture", body, top: true);
    }

    private Control StoryPicture(StoryCastMember member)
    {
        var holder = new Border { Width = PictureSize, Height = PictureSize, HorizontalAlignment = HorizontalAlignment.Left };
        ThemeInk.Bind(holder, Border.BackgroundProperty, ThemeManager.SlabKey);

        if (_portraits?.For(member.Shown.Picture, (int)PictureSize) is { } bitmap)
        {
            holder.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform };
        }

        return holder;
    }

    // ----- name

    private Control NameRow(Entry entry)
    {
        Control control;
        Control? reset = null;

        switch (entry.Kind)
        {
            case Kind.Captain or Kind.Tower:
                var key = NameKey(entry.Kind);
                var fallback = entry.Kind == Kind.Captain ? "Captain" : "Tower Control";

                control = TextField(
                    NameBoxName,
                    _host.Settings.Read(key) ?? string.Empty,
                    "Name",
                    fallback,
                    text => ApplySetting(key, text));
                reset = ResetFor(key);
                break;
            case Kind.Story:
                control = ReadOnly(entry.Name);
                break;
            case Kind.Draft:
                control = TextField(NameBoxName, _draft!.Name, "Name", "Name", text => _draft!.Name = text, CharacterField.Name);
                break;
            default:
                control = TextField(
                    NameBoxName,
                    entry.Character!.Name,
                    "Name",
                    "Name",
                    text => SaveCharacter(entry.Character, held => held with { Name = text }),
                    CharacterField.Name);
                break;
        }

        return Row("Name", control, reset);
    }

    private static string NameKey(Kind kind) =>
        kind == Kind.Captain
            ? D47.Core.Capabilities.Builtin.SpeechCapability.CarrierCaptainNameKey
            : D47.Core.Capabilities.Builtin.SpeechCapability.TowerNameKey;

    private static string VoiceKey(Kind kind) =>
        kind == Kind.Captain
            ? D47.Core.Capabilities.Builtin.SpeechCapability.CarrierCaptainVoiceKey
            : D47.Core.Capabilities.Builtin.SpeechCapability.TowerVoiceKey;

    private Button? ResetFor(string key)
    {
        if (!_host.Settings.IsChanged(key))
        {
            return null;
        }

        var reset = new Button
        {
            Name = ResetName,
            Content = "↺",
            Width = TypeScale.MinimumTarget,
            Height = TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Avalonia.Automation.AutomationProperties.SetName(reset, $"Reset {key}");

        reset.Click += (_, _) =>
        {
            _host.Settings.Reset(key, SettingsCaller.Panel);
            Draw();
        };

        return reset;
    }

    private Control ReadOnly(string text)
    {
        var block = new TextBlock { Text = text, FontSize = TypeScale.Body, VerticalAlignment = VerticalAlignment.Center };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var slab = new Border { MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(12, 0), Child = block };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
        return slab;
    }

    /// <summary>
    /// A text box that commits on Enter or when it loses focus, or on the headset a button that asks for the
    /// text on the entry page. <paramref name="commit"/> returns after storing the text.
    /// </summary>
    private Control TextField(
        string name, string value, string title, string placeholder, Action<string> commit, CharacterField? field = null)
    {
        var shell = new StackPanel { Spacing = 4 };

        if (_big)
        {
            var button = new Button
            {
                Name = name,
                Height = TypeScale.MinimumTarget,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = value.Length > 0 ? value : placeholder,
            };

            button.Click += (_, _) => Ask(title, value, text => commit(text.Trim()), field);
            shell.Children.Add(button);

            if (field is { } shown && _errors.GetValueOrDefault(shown) is { } message)
            {
                shell.Children.Add(ErrorLine(message));
            }

            return shell;
        }

        var box = new TextBox { Name = name, Text = value, PlaceholderText = placeholder };
        Watch(box, text => commit(text.Trim()), keepsReturn: false);
        shell.Children.Add(box);

        if (field is { } f && _errors.GetValueOrDefault(f) is { } error)
        {
            FieldMessage.ShowError(box, error);
        }

        return shell;
    }

    private static TextBlock ErrorLine(string message)
    {
        var line = new TextBlock { Text = message, FontSize = TypeScale.FieldMessage, TextWrapping = TextWrapping.Wrap };
        ThemeInk.Bind(line, TextBlock.ForegroundProperty, ThemeManager.RedKey);
        return line;
    }

    private void Watch(TextBox box, Action<string> commit, bool keepsReturn)
    {
        var committed = box.Text ?? string.Empty;

        void Commit()
        {
            var text = box.Text ?? string.Empty;

            if (text == committed)
            {
                return;
            }

            committed = text;
            commit(text);
        }

        box.GotFocus += (_, _) => _editing = true;
        box.LostFocus += (_, _) =>
        {
            _editing = false;
            Commit();
        };

        if (!keepsReturn)
        {
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    _editing = false;
                    Commit();
                }
            };
        }
    }

    private void Ask(string title, string initial, Action<string> done, CharacterField? field)
    {
        _prompts.Enter(
            new EntryRequest(
                $"character.{title.ToLowerInvariant()}",
                title,
                title,
                null,
                initial,
                EntrySurface.Voice,
                value => Verdict(field, value)),
            done);
    }

    private EntryVerdict Verdict(CharacterField? field, string value)
    {
        if (field is not { } f || _selected is not { } key)
        {
            return EntryVerdict.Ok;
        }

        CustomCharacter? probe = key == DraftKey
            ? _draft!.With(value, f)
            : Store.Find(Fid, key[CustomPrefix.Length..]) is { } held ? Replace(held, f, value) : null;

        return probe is not null && Store.Check(Fid, probe) is { } refusal && refusal.Field == f
            ? EntryVerdict.No(refusal.Reason)
            : EntryVerdict.Ok;
    }

    private static CustomCharacter Replace(CustomCharacter held, CharacterField field, string value) => field switch
    {
        CharacterField.Name => held with { Name = value.Trim() },
        CharacterField.System => held with { System = value.Trim() },
        _ => held with { Notes = value },
    };

    // ----- saving

    private void ApplySetting(string key, string text)
    {
        _host.Settings.Apply(key, text, SettingsCaller.Panel);
        Draw();
    }

    /// <summary>
    /// Saves <paramref name="change"/> applied to the character as the store holds it now, not as <paramref name="seen"/>
    /// was drawn: a control from an earlier draw can still fire after another field saved.
    /// </summary>
    private void SaveCharacter(CustomCharacter seen, Func<CustomCharacter, CustomCharacter> change)
    {
        _errors.Clear();

        if (Store.Set(Fid, change(Store.Find(Fid, seen.Id) ?? seen)) is { } refusal)
        {
            _errors[refusal.Field] = refusal.Reason;
        }

        Draw();
    }

    private Control DraftFoot()
    {
        var save = Tile("SAVE", SaveName, SaveDraft);
        var cancel = Tile("CANCEL", CancelName, () =>
        {
            _draft = null;
            Select(CaptainKey);
        });

        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { save, cancel } };
    }

    private void SaveDraft()
    {
        if (_draft is not { } draft)
        {
            return;
        }

        var character = draft.Build();

        _errors.Clear();

        if (Store.Set(Fid, character) is { } refusal)
        {
            _errors[refusal.Field] = refusal.Reason;
            Draw();
            return;
        }

        _draft = null;
        Select(CustomPrefix + character.Id);
    }

    // ----- voice

    private string ProviderId => VoiceGroups.ProviderFor(_host.Settings.Current.Speech, VoiceGroup.Carrier);

    private Control VoiceRow(Entry entry)
    {
        if (entry.Kind == Kind.Story)
        {
            var member = entry.Member!;
            var provider = TtsProviderCatalog.Selected(member.Speaks.ProviderId);
            var voice = member.SpeaksName
                ?? (member.Speaks.IsCrewVoice ? "The crew voice"
                    : provider.VoiceIdsAreOpaque ? $"A {provider.Name} voice" : member.Speaks.VoiceId);

            var stack = new StackPanel { Children = { Face(voice, provider.Name, readOnly: true), Inset($"The story sets {entry.Name}'s name, voice, picture and who they are. Your notes are added to that.") } };
            return Row("Voice", stack);
        }

        var settings = _host.Settings;
        var row = settings.Find(VoiceKey(entry.Kind == Kind.Tower ? Kind.Tower : Kind.Captain));
        Control? reset = null;
        string? current;
        Action<string?> chosen;

        if (entry.Kind is Kind.Captain or Kind.Tower)
        {
            var key = VoiceKey(entry.Kind);
            current = settings.Read(key);
            chosen = value =>
            {
                settings.Apply(key, value, SettingsCaller.Panel);
                Draw();
            };
            reset = ResetFor(key);
        }
        else if (entry.Kind == Kind.Draft)
        {
            current = _draft!.Voice?.VoiceId;
            chosen = value =>
            {
                _draft!.Voice = string.IsNullOrEmpty(value) ? null : new CharacterVoice(ProviderId, value);
                Draw();
            };
        }
        else
        {
            var held = entry.Character!;
            current = held.Voice is { } voice && SameProvider(voice.Provider) ? voice.VoiceId : null;
            chosen = value => SaveCharacter(held, stored => stored with { Voice = string.IsNullOrEmpty(value) ? null : new CharacterVoice(ProviderId, value) });
        }

        var label = current is { Length: > 0 } id
            ? (row?.DescriberFor(settings.Current)(id) ?? id)
            : NoVoice;
        var providerName = current is { Length: > 0 } ? TtsProviderCatalog.Selected(ProviderId).Name : null;

        var face = Face(label, providerName, readOnly: false);
        face.Name = VoiceName;
        face.Click += (_, _) => PickVoice(entry, row, current, chosen);

        Control body = face;

        if (entry.Kind is Kind.Captain or Kind.Tower)
        {
            var empty = entry.Kind == Kind.Captain ? "Captain" : "Tower Control";

            body = new StackPanel
            {
                Children =
                {
                    face,
                    Inset($"The name and voice are settings, the same for every Commander on this PC. The notes are yours alone. Leave the name empty and D47 says \"{empty}\"."),
                },
            };
        }

        return Row("Voice", body, reset);
    }

    private bool SameProvider(string provider) =>
        string.Equals(provider, ProviderId, StringComparison.OrdinalIgnoreCase);

    private Button Face(string voice, string? provider, bool readOnly)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };

        var value = new TextBlock { Text = voice, FontSize = TypeScale.Body, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ThemeInk.Bind(value, TextBlock.ForegroundProperty, voice == NoVoice ? ThemeManager.GreyKey : ThemeManager.WhiteKey);
        grid.Children.Add(value);

        if (provider is not null)
        {
            var tag = new TextBlock
            {
                Text = provider.ToUpperInvariant(),
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Meta,
                Margin = new Thickness(8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ThemeInk.Bind(tag, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            Grid.SetColumn(tag, 1);
            grid.Children.Add(tag);
        }

        if (!readOnly)
        {
            var caret = new TextBlock { Text = "›", FontSize = TypeScale.Body, VerticalAlignment = VerticalAlignment.Center };
            ThemeInk.Bind(caret, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            Grid.SetColumn(caret, 2);
            grid.Children.Add(caret);
        }

        return new Button
        {
            Content = grid,
            Height = TypeScale.MinimumTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsEnabled = !readOnly,
        };
    }

    private void PickVoice(Entry entry, SettingRow? row, string? current, Action<string?> chosen)
    {
        if (row is null)
        {
            return;
        }

        _prompts.Pick(
            $"character-voice:{entry.Key}",
            "Voice",
            SettingControls.PickerRequestFor(row, _host.Settings.Current, current),
            result => chosen(result.Value));
    }

    // ----- notes

    private Control NotesRow(Entry entry)
    {
        var notes = NotesOf(entry) ?? string.Empty;
        var hint = entry.Kind switch
        {
            Kind.Captain or Kind.Tower => "D47 adds these to the captain's own manner.",
            Kind.Story => "Kept for this story only.",
            _ => $"D47 uses these to shape what {HintName(entry)} says and how.",
        };

        var count = new TextBlock
        {
            Text = $"{notes.Length} / {CustomCharacter.MaxNotes}",
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Meta,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ThemeInk.Bind(count, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var foot = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(count, Dock.Right);
        foot.Children.Add(count);

        if (!_big)
        {
            var hintLine = Plain(hint);
            hintLine.Margin = new Thickness(0, 0, 12, 0);
            foot.Children.Add(hintLine);
        }

        var shell = new StackPanel { Spacing = 6 };
        Action<string> commit = text => SaveNotes(entry, text);

        if (_big)
        {
            var button = new Button
            {
                Name = NotesBoxName,
                MinHeight = TypeScale.MinimumTarget,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new TextBlock
                {
                    Text = notes.Length > 0 ? notes : "Add notes",
                    TextWrapping = TextWrapping.Wrap,
                    MaxLines = 3,
                    FontFamily = new FontFamily(Fonts.ProseFamily),
                    FontSize = TypeScale.Body,
                },
            };

            button.Click += (_, _) => Ask("Notes", notes, commit, CharacterField.Notes);
            shell.Children.Add(button);
        }
        else
        {
            var box = new TextBox
            {
                Name = NotesBoxName,
                Text = notes,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 4 * 24 + 20,
                MaxLength = CustomCharacter.MaxNotes,
                FontFamily = new FontFamily(Fonts.ProseFamily),
                FontSize = TypeScale.Body,
            };

            box.TextChanged += (_, _) => count.Text = $"{box.Text?.Length ?? 0} / {CustomCharacter.MaxNotes}";
            Watch(box, commit, keepsReturn: true);
            shell.Children.Add(box);

            if (_errors.GetValueOrDefault(CharacterField.Notes) is { } error)
            {
                FieldMessage.ShowError(box, error);
            }
        }

        shell.Children.Add(foot);

        return Row("Personality notes", shell, top: true);
    }

    private static string HintName(Entry entry) =>
        entry.Kind == Kind.Draft ? "this character" : entry.Name;

    private string? NotesOf(Entry entry) => entry.Kind switch
    {
        Kind.Captain => Store.NotesFor(Fid, CarrierCharacter.Captain),
        Kind.Tower => Store.NotesFor(Fid, CarrierCharacter.Tower),
        Kind.Story => Store.CastNotes(Fid, entry.Story!.StoryId, entry.Member!.Shown.Id),
        Kind.Draft => _draft!.Notes,
        _ => entry.Character!.Notes,
    };

    private void SaveNotes(Entry entry, string text)
    {
        _errors.Clear();
        CharacterRefusal? refusal = null;

        switch (entry.Kind)
        {
            case Kind.Captain:
                refusal = Store.SetNotes(Fid, CarrierCharacter.Captain, text);
                break;
            case Kind.Tower:
                refusal = Store.SetNotes(Fid, CarrierCharacter.Tower, text);
                break;
            case Kind.Story:
                refusal = Store.SetCastNotes(Fid, entry.Story!.StoryId, entry.Member!.Shown.Id, text);
                break;
            case Kind.Draft:
                _draft!.Notes = text;
                break;
            default:
                SaveCharacter(entry.Character!, held => held with { Notes = text });
                return;
        }

        if (refusal is not null)
        {
            _errors[refusal.Field] = refusal.Reason;
        }

        Draw();
    }

    // ----- where

    private Control WhereRow(Entry entry)
    {
        switch (entry.Kind)
        {
            case Kind.Captain or Kind.Tower:
                return Row("Where", CarrierWhere(), top: true);
            case Kind.Story:
                var story = LabeledCheckBox.Caps("Aboard");
                story.Name = AboardName;
                story.IsEnabled = false;
                return Row("Where", story, top: true);
        }

        var aboard = entry.Kind == Kind.Draft ? _draft!.Aboard : entry.Character!.Aboard;
        var box = LabeledCheckBox.Caps("Aboard");
        box.Name = AboardName;
        box.IsChecked = aboard;
        box.Width = 220;
        box.HorizontalAlignment = HorizontalAlignment.Left;
        box.IsCheckedChanged += (_, _) =>
        {
            var now = box.IsChecked == true;

            if (entry.Kind == Kind.Draft)
            {
                _draft!.Aboard = now;
                Draw();
                return;
            }

            SaveCharacter(entry.Character!, held => held with { Aboard = now });
        };

        var who = HintName(entry);
        var hint = Plain($"Aboard, {who} is heard on the intercom. Away, over the radio, fainter with distance.");

        return Row("Where", new StackPanel { Spacing = 6, Children = { box, hint } }, top: true);
    }

    private void AddSystemRow(StackPanel group, Entry entry)
    {
        var aboard = entry.Kind == Kind.Draft ? _draft!.Aboard : entry.Character!.Aboard;

        // Turning Aboard off with no system is refused, so the row is drawn to say why and to take the system.
        var asked = _errors.ContainsKey(CharacterField.System);

        if (aboard && !asked)
        {
            return;
        }

        var system = entry.Kind == Kind.Draft ? _draft!.System : entry.Character!.System ?? string.Empty;

        Action<string> commit = text =>
        {
            if (entry.Kind == Kind.Draft)
            {
                _draft!.System = text;
                Draw();
                return;
            }

            SaveCharacter(entry.Character!, held => held with { System = text, Aboard = held.Aboard && !asked });
        };

        var field = TextField(SystemBoxName, system, "System", "System", commit, CharacterField.System);
        var strip = DistanceStrip(system);
        var body = new StackPanel { Spacing = 6, Children = { field, strip } };

        group.Children.Add(Row("System", body, top: true));
    }

    private Control DistanceStrip(string system)
    {
        var label = new TextBlock
        {
            Text = "DISTANCE",
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(label, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _distanceText = new TextBlock
        {
            Name = DistanceName,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Description,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
        };
        ThemeInk.Bind(_distanceText, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var galaxy = _host.Settings.Current.Knowledge.GalaxySearch;
        var hint = Plain(galaxy
            ? $"Out of comms range beyond {CarrierComms.RangeLightYears:N0} ly."
            : "Distance unknown with Galaxy search off.");
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        hint.VerticalAlignment = VerticalAlignment.Center;

        var line = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(hint, Dock.Right);
        line.Children.Add(hint);
        line.Children.Add(label);
        line.Children.Add(_distanceText);

        var strip = new Border { MinHeight = 36, Padding = new Thickness(12, 4), Child = line };
        ThemeInk.Bind(strip, Border.BackgroundProperty, ThemeManager.SlabKey);

        LookUpDistance(_state()?.Location.StarSystem, system);

        return strip;
    }

    private Control CarrierWhere()
    {
        var carrier = _state()?.Carrier ?? CarrierState.None;

        if (!carrier.Owned || carrier.StarSystem is not { Length: > 0 } system)
        {
            return Plain("D47 has not seen your carrier yet.");
        }

        var place = new TextBlock
        {
            Text = $"{carrier.CallSign ?? carrier.Name ?? "Your carrier"} · {system} · ",
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(place, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        _distanceText = new TextBlock
        {
            Name = DistanceName,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(_distanceText, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        LookUpDistance(_state()?.Location.StarSystem, system);

        return new StackPanel { Orientation = Orientation.Horizontal, Children = { place, _distanceText } };
    }

    private void LookUpDistance(string? here, string? there)
    {
        var wanted = (here, there);

        if (_distanceFor == wanted)
        {
            ShowDistance();
            return;
        }

        _distanceFor = wanted;
        _distance = null;
        _distanceKnown = false;

        if (!_host.Settings.Current.Knowledge.GalaxySearch || string.IsNullOrWhiteSpace(there))
        {
            _distanceKnown = true;
            ShowDistance();
            DistanceSettled = Task.CompletedTask;
            return;
        }

        ShowDistance();

        var source = _lookup = new CancellationTokenSource();
        var distance = _host.Distance;

        DistanceSettled = Task.Run(async () =>
        {
            double? away = null;

            try
            {
                away = await CarrierComms.DistanceAsync(here, there!, distance, source.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_distanceFor == wanted)
                {
                    _distance = away;
                    _distanceKnown = true;
                    ShowDistance();
                }
            });
        });
    }

    private void ShowDistance()
    {
        if (_distanceText is null)
        {
            return;
        }

        _distanceText.Text = !_distanceKnown ? "…" : _distance is { } away ? $"{away:N1} ly" : "unknown";
    }

    // ----- remove

    private Control RemoveStrip(CustomCharacter character)
    {
        var head = new TextBlock
        {
            Text = "REMOVE CHARACTER",
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontWeight = FontWeight.SemiBold,
            FontSize = TypeScale.ControlLarge,
        };
        ThemeInk.Bind(head, TextBlock.ForegroundProperty, ThemeManager.RedKey);

        var words = new TextBlock
        {
            Text = $"D47 forgets {character.Name}'s name, voice, picture and notes, and they leave the CHARACTERS channel. This cannot be undone.",
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(words, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var confirm = Tile("REMOVE", RemoveConfirmName, () =>
        {
            Store.Remove(Fid, character.Id);
            _removing = false;
            Select(CaptainKey);
        }, destructive: true);
        var cancel = Tile("CANCEL", RemoveCancelName, () =>
        {
            _removing = false;
            Draw();
        });

        var inner = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(12, 10),
            Children =
            {
                head,
                words,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { confirm, cancel } },
            },
        };

        var bar = new Border { Width = 3 };
        ThemeInk.Bind(bar, Border.BackgroundProperty, ThemeManager.RedKey);

        var strip = new DockPanel { Children = { bar, inner } };
        DockPanel.SetDock(bar, Dock.Left);

        var shell = new Border { Margin = new Thickness(0, 14, 0, 0), Child = strip };
        ThemeInk.Bind(shell, Border.BackgroundProperty, ThemeManager.RedGroundKey);

        return shell;
    }

    private enum Kind
    {
        Captain,
        Tower,
        Story,
        Custom,
        Draft,
    }

    private sealed record Entry(string Key, Kind Kind, string Name)
    {
        public CustomCharacter? Character { get; init; }

        public StoryCastMember? Member { get; init; }

        public RunningCast? Story { get; init; }
    }

    private sealed class Draft
    {
        public string Name { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public CharacterVoice? Voice { get; set; }

        public bool Aboard { get; set; }

        public string System { get; set; } = string.Empty;

        public CustomCharacter Build() =>
            new(CustomCharacter.NewId(), Name.Trim(), Voice, Notes.Length > 0 ? Notes : null, Aboard, System.Trim().Length > 0 ? System.Trim() : null);

        public CustomCharacter With(string value, CharacterField field) =>
            field switch
            {
                CharacterField.Name => Build() with { Name = value.Trim() },
                CharacterField.System => Build() with { System = value.Trim() },
                _ => Build() with { Notes = value },
            };
    }
}
