using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Modules;

namespace D47.App.Settings;

/// <summary>What the Modules place reads and does: the installed modules, their status, and who a module may speak as.</summary>
public sealed class ModulesSource(
    Func<IReadOnlyList<InstalledModule>> installed,
    Func<IReadOnlyList<ModuleStatusReport>> statuses,
    Action<string> restart,
    Func<IReadOnlyList<(string Id, string Name)>> cores,
    Func<IReadOnlyList<(string Id, string Name)>> yourCores,
    Func<IReadOnlyList<(string Id, string Name)>> characters,
    Action<Action>? watch = null,
    Action<Action>? unwatch = null)
{
    private Action? _changed;
    private bool _watching;

    public IReadOnlyList<InstalledModule> Installed => installed();

    public IReadOnlyList<ModuleStatusReport> Statuses => statuses();

    public IReadOnlyList<(string Id, string Name)> Cores => cores();

    public IReadOnlyList<(string Id, string Name)> YourCores => yourCores();

    public IReadOnlyList<(string Id, string Name)> Characters => characters();

    public void Restart(string id) => restart(id);

    /// <summary>Raised, on any thread, when the installed list or a status changes; the source listens only while someone does.</summary>
    public event Action? Changed
    {
        add
        {
            _changed += value;

            if (!_watching)
            {
                _watching = true;
                watch?.Invoke(Raise);
            }
        }

        remove
        {
            _changed -= value;

            if (_changed is null && _watching)
            {
                _watching = false;
                unwatch?.Invoke(Raise);
            }
        }
    }

    public void Raise() => _changed?.Invoke();
}

/// <summary>The Modules place: one group per installed module, drawn by the App because the groups follow what is installed.</summary>
internal sealed class ModulesPlaceView : StackPanel
{
    public const string GroupName = "ModuleGroup";
    public const string TurnedOnName = "ModuleTurnedOn";
    public const string RestartName = "ModuleRestart";
    public const string SpeaksAsName = "ModuleSpeaksAs";
    public const string MuteName = "ModuleMute";
    public const string IntervalName = "ModuleInterval";
    public const string LeavesName = "ModuleLeaves";
    public const string StatusName = "ModuleStatus";
    public const string NoticeName = "ModuleNotice";

    private readonly SettingsService _settings;
    private readonly ModulesSource _source;
    private string? _built;

    public ModulesPlaceView(SettingsService settings, ModulesSource source)
    {
        _settings = settings;
        _source = source;
        Spacing = 28;
        Rebuild();
    }

    /// <summary>Whether any module is installed, refused ones included.</summary>
    public bool HasModules => _source.Installed.Count > 0;

    /// <summary>Whether any group draws a protected row.</summary>
    public bool HasProtectedRow => HasModules;

    /// <summary>Draws the groups again when what they show has changed.</summary>
    public void Rebuild()
    {
        var signature = Signature();

        if (signature == _built)
        {
            return;
        }

        _built = signature;

        var focused = (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control)?.Name;

        Children.Clear();

        foreach (var module in _source.Installed)
        {
            Children.Add(module switch
            {
                ReadyModule ready => ReadyGroup(ready),
                RefusedModule refused => RefusedGroup(refused),
                _ => new Border(),
            });
        }

        if (focused is not null)
        {
            this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == focused)?.Focus();
        }
    }

    private string Signature()
    {
        var current = _settings.Current;
        var statuses = _source.Statuses;

        return string.Join(
            '\n',
            _source.Installed.Select(module => module switch
            {
                ReadyModule ready => string.Join(
                    '|',
                    ready.Folder,
                    ready.Manifest.Id,
                    StatusOf(ready, statuses),
                    current.ModuleFor(ready.Manifest.Id),
                    string.Join(',', _source.Cores.Concat(_source.YourCores).Concat(_source.Characters).Select(c => c.Id + c.Name))),
                _ => module.Folder,
            }));
    }

    private static ModuleStatus StatusOf(ReadyModule module, IReadOnlyList<ModuleStatusReport> statuses) =>
        statuses.FirstOrDefault(s => string.Equals(s.Folder, module.Folder, StringComparison.OrdinalIgnoreCase))?.Status
        ?? ModuleStatus.Off;

    private Control ReadyGroup(ReadyModule module)
    {
        var manifest = module.Manifest;
        var chosen = _settings.Current.ModuleFor(manifest.Id);
        var status = StatusOf(module, _source.Statuses);

        var group = Group(
            manifest.Name,
            string.Create(CultureInfo.InvariantCulture, $"A module by {manifest.Author} · version {manifest.Version}"));

        group.Name = GroupName;

        if (status == ModuleStatus.StoppedAfterRepeatedCrashes)
        {
            group.Children.Add(Banner($"{manifest.Name} stopped", ModulePlace.StoppedNotice));
        }

        group.Children.Add(TurnedOn(manifest.Name, manifest.Id, chosen.Enabled, enabled: true));
        group.Children.Add(StatusRow(manifest, status));

        var speakers = ModulePlace.Speakers(
            manifest,
            _source.Cores,
            _source.YourCores,
            _source.Characters);

        group.Children.Add(SpeaksAs(module, chosen, speakers));

        if (chosen.Enabled)
        {
            group.Children.Add(Mute(manifest, chosen));
            group.Children.Add(Interval(manifest, chosen));
        }

        group.Children.Add(Leaves(module));

        return group;
    }

    private Control RefusedGroup(RefusedModule module)
    {
        var name = Path.GetFileName(module.Folder.TrimEnd('\\', '/'));

        var group = Group(name, "A module D47 will not start.");
        group.Name = GroupName;

        group.Children.Add(Banner($"{name} was refused", ModulePlace.RefusalNotice(module.Reason)));
        group.Children.Add(TurnedOn(name, name, isOn: false, enabled: false));

        var line = ModulePlace.StatusLine(ModuleStatus.Refused, hasPages: false);

        group.Children.Add(Row("Status", Data(line, StatusName + "." + name), protectedRow: false));

        return group;
    }

    private static StackPanel Group(string title, string description)
    {
        var group = new StackPanel { Spacing = 2 };

        var reset = SettingsView.ResetGlyph("ModuleGroupReset", $"Reset {title}");
        reset.IsVisible = false;

        var (head, _, _) = SettingsView.GroupHead(title, description, reset);

        group.Children.Add(head);

        return group;
    }

    private static Control Banner(string label, string text)
    {
        var notice = new Notice(NoticeLevel.Error, inline: true) { Name = NoticeName, Label = label.ToUpperInvariant(), Text = text };
        notice.Margin = new Thickness(0, 12, 0, 2);

        return notice;
    }

    private static Control Row(string label, Control control, bool protectedRow)
    {
        var caption = SettingsView.RowLabel(label);

        return SettingsView.RowFrame(SettingsView.RowColumns(caption, control, reset: null), protectedRow);
    }

    private Control TurnedOn(string name, string id, bool isOn, bool enabled)
    {
        var (box, label) = LabeledCheckBox.Build($"Run {name} with D47");
        label.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        box.Name = TurnedOnName + "." + id;
        box.IsChecked = isOn;
        box.IsEnabled = enabled;
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(box, $"Run {name} with D47");

        box.IsCheckedChanged += (_, _) =>
        {
            var on = box.IsChecked == true;

            if (on != isOn)
            {
                ModulePlace.Set(_settings, id, $"module {id} turned {(on ? "on" : "off")}", m => m with { Enabled = on });
            }
        };

        return Row("Turned on", box, protectedRow: true);
    }

    private Control StatusRow(ModuleManifest manifest, ModuleStatus status)
    {
        var line = ModulePlace.StatusLine(status, manifest.Pages.Count > 0);

        var data = Data(line, StatusName + "." + manifest.Id);

        if (status == ModuleStatus.Off)
        {
            return Row("Status", data, protectedRow: false);
        }

        var restart = new Button
        {
            Name = RestartName + "." + manifest.Id,
            Content = "Restart",
            IsEnabled = status != ModuleStatus.Starting,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        AutomationProperties.SetName(restart, $"Restart {manifest.Name}");
        restart.Click += (_, _) => _source.Restart(manifest.Id);

        var dock = new DockPanel();
        DockPanel.SetDock(restart, Dock.Right);
        dock.Children.Add(restart);
        dock.Children.Add(data);

        return Row("Status", dock, protectedRow: false);
    }

    private static Control Data(ModuleStatusLine line, string name)
    {
        var (inset, text) = SettingsView.Report();
        inset.Name = name;

        if (line.Lead.Length > 0)
        {
            var lead = new Avalonia.Controls.Documents.Run { Text = line.Lead };
            text.Inlines!.Add(lead);
            lead.Bind(Avalonia.Controls.Documents.TextElement.ForegroundProperty, Stepper.Ink(line.Tone));
        }

        text.Inlines!.Add(new Avalonia.Controls.Documents.Run { Text = line.Rest });

        return inset;
    }

    private Control SpeaksAs(ReadyModule module, ModuleSettings chosen, IReadOnlyList<ModuleSpeaker> speakers)
    {
        var manifest = module.Manifest;
        var now = ModulePlace.Chosen(manifest, chosen, speakers);

        var (tile, value, _) = SettingsView.DropdownTile($"Speaks as, {manifest.Name}");
        tile.Name = SpeaksAsName + "." + manifest.Id;
        value.Text = now.Label;

        tile.Click += (_, _) =>
        {
            if (tile.FindAncestorOfType<PanelView>()?.Prompts is not { } prompts)
            {
                return;
            }

            var labels = speakers.ToDictionary(s => s.Value, s => s.Label, StringComparer.Ordinal);

            prompts.Pick(
                $"module-speaks-as:{manifest.Id}",
                "Speaks as",
                new PickerRequest
                {
                    Prompt = $"{manifest.Name} speaks as",
                    Help = ModulePlace.SpeaksAsHelp,
                    Choices = [.. speakers.Select(s => s.Value)],
                    Describe = v => labels.GetValueOrDefault(v, v),
                    Current = now.Value,
                    ChoiceNoun = "voices",
                },
                result =>
                {
                    ModulePlace.Set(_settings, manifest.Id, $"module {manifest.Id} speaks as", m => m with { SpeaksAs = result.Value });
                    tile.Focus();
                });
        };

        var help = new TextBlock
        {
            Text = ModulePlace.SpeaksAsHelp,
            FontSize = TypeScale.Secondary,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        };
        ThemeInk.Bind(help, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        return Row("Speaks as", new StackPanel { Children = { tile, help } }, protectedRow: false);
    }

    private Control Mute(ModuleManifest manifest, ModuleSettings chosen)
    {
        var (box, label) = LabeledCheckBox.Build($"Mute {manifest.Name}");
        label.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        box.Name = MuteName + "." + manifest.Id;
        box.IsChecked = chosen.Muted;
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(box, $"Mute {manifest.Name}");

        box.IsCheckedChanged += (_, _) =>
        {
            var muted = box.IsChecked == true;

            if (muted != chosen.Muted)
            {
                ModulePlace.Set(_settings, manifest.Id, $"module {manifest.Id} muted", m => m with { Muted = muted });
            }
        };

        return Row("Mute", box, protectedRow: false);
    }

    private Control Interval(ModuleManifest manifest, ModuleSettings chosen)
    {
        var intervals = ModulePlace.Intervals;

        var statuses = intervals
            .Select(i => i == ModuleInterval.Own
                ? new ChoiceStatus(ModulePlace.Apart(manifest.MinimumIntervalSeconds), ChoiceTone.Grey)
                : null)
            .ToList();

        var (view, choice) = Choice.Build(
            [.. intervals.Select(ModulePlace.IntervalLabel)],
            selectedIndex: ModulePlace.IntervalAllowed(chosen.Interval, manifest) ? Math.Max(0, intervals.ToList().IndexOf(chosen.Interval)) : 0,
            statuses: statuses);

        view.Name = IntervalName + "." + manifest.Id;
        view.HorizontalAlignment = HorizontalAlignment.Stretch;
        SettingsView.DressAsAChoice(view);
        AutomationProperties.SetName(view, $"How often {manifest.Name} may speak");

        if (view is Segment segment)
        {
            for (var i = 0; i < intervals.Count; i++)
            {
                segment.SetOptionEnabled(i, ModulePlace.IntervalAllowed(intervals[i], manifest));
            }
        }

        choice.SelectionChanged += (_, _) =>
        {
            if (choice.SelectedIndex is >= 0 && choice.SelectedIndex < intervals.Count)
            {
                var picked = intervals[choice.SelectedIndex];

                if (picked != chosen.Interval && ModulePlace.IntervalAllowed(picked, manifest))
                {
                    ModulePlace.Set(_settings, manifest.Id, $"module {manifest.Id} interval", m => m with { Interval = picked });
                }
            }
        };

        return Row("How often it may speak", view, protectedRow: false);
    }

    private Control Leaves(ReadyModule module)
    {
        var (inset, text) = SettingsView.Report();
        inset.Name = LeavesName + "." + module.Manifest.Id;
        text.Text = ModulePlace.Leaves(_settings.Current, module);

        return Row("What leaves this machine", inset, protectedRow: false);
    }
}
