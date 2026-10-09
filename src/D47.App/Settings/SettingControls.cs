using System.Diagnostics;
using System.Globalization;
using D47.Core.Listening;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Input;
using D47.App.Theming;
using D47.Core;
using D47.Core.Capabilities;
using D47.App.Windowing;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Coverage;
using D47.Core.Persona;

namespace D47.App.Settings;

internal sealed record SettingRowHost(
    SettingsService Settings,
    Control Resources,
    Func<bool> Refreshing,
    Func<string, string?, StatusLine, bool> Apply,
    Action Refresh);

internal sealed record SettingServices(
    Func<CoverageReport>? Coverage,
    D47.Core.Actions.MacroStore? Macros,
    D47.Core.Persona.OwnPersonaStore? OwnPersonas,
    D47.Core.Checklists.ChecklistService? Checklists,
    SwitchEditing? Switches,
    LoreEditing? Lore,
    (D47.Core.Memory.MemoryBook Book, Func<DateTimeOffset> Now)? Memories,
    (D47.Core.Debrief.DebriefBook Book, Func<DateTimeOffset> Now, Func<D47.Core.Persona.Persona> Core)? Debrief,
    D47.Core.Logbook.LogbookBook? Logbook,
    (D47.Core.Diagnostics.Recording.RecordingLog Log, Func<DateTimeOffset> Now)? Recording,
    D47.Core.Input.BindingProfiles? BindingProfiles,
    IReadOnlyList<string> Reserved,
    Func<WhisperModel, IProgress<ModelProgress>, Task<ModelInstallResult>>? DownloadModel);

internal readonly record struct SettingControl(Control Control, Action Refresh, (Control Block, string Words)? Under = null);

/// <summary>Builds the control for each settings row.</summary>
internal sealed partial class SettingControls(SettingRowHost host, SettingServices services)
{
    private IBrush? Res(string key) => host.Resources.FindResource(key) as IBrush;

    private IDisposable Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, host.Resources.GetResourceObservable(key));

    /// <summary>Controls a group's own builder draws for rows that have none of their own, by key.</summary>
    private readonly Dictionary<string, Control> _drawnByGroup = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, Control> DrawnByGroup => _drawnByGroup;

    /// <summary>One download at a time, and the row that is showing it.</summary>
    private bool _downloadingModel;


    /// <summary>The control for a row, its refresh action, and any block it draws under the row.</summary>
    public SettingControl Build(SettingRow row, StatusLine message)
    {
        // The whole Guardian Voice Effects group, drawn from its preset row.
        if (row is { Kind: SettingKind.Choice } && row.Key == SpeechCapability.GuardianPresetKey)
        {
            return BuildGuardianVoice(row, message);
        }

        var (control, refresh) = BuildControl(row, message);

        return new SettingControl(control, refresh);
    }

    private (Control Control, Action Refresh) BuildControl(SettingRow row, StatusLine message)
    {
        switch (row.Kind)
        {
            // Rows that open a page or a window instead of offering a value: the coverage page.
            case SettingKind.Info when row.Key == DiagnosticsCapability.CoverageKey && services.Coverage is not null:
                return BuildCoverage(row);

            // The macro editor window.
            case SettingKind.Info when row.Key == MacroCapability.ListKey && services.Macros is not null:
                return BuildMacros(row);

            // The persona editor page.
            case SettingKind.Info when row.Key == PersonaCapability.OwnKey && services.OwnPersonas is not null:
                return BuildOwnPersonas(row);

            // The third row that offers a window.
            case SettingKind.Info when row.Key == ChecklistCapability.SummaryKey && services.Checklists is not null:
                return BuildChecklists(row);

            // The saved binding profiles, each with a delete.
            case SettingKind.Info when row.Key == BindingProfilesCapability.ListKey && services.BindingProfiles is not null:
                return BuildBindingProfiles(row);

            // The HOTAS switches page.
            case SettingKind.Info when row.Key == SwitchCapability.ListKey && services.Switches is not null:
                return BuildSwitches(row);

            // The fifth row that offers a window.
            case SettingKind.Info when row.Key == LoreCapability.BookKey && services.Lore is not null:
                return BuildLore(row);

            // The sixth row that offers a window.
            case SettingKind.Info when row.Key == MemoryCapability.StoreKey && services.Memories is not null:
                return BuildMemories(row);

            // The seventh, and the only one whose button starts work rather than opening something.

            // The eighth, and the only one behind which a button spends money.
            case SettingKind.Info when row.Key == LogbookCapability.StoreKey && services.Logbook is not null:
                return BuildLogbook(row);

            // The audio recorder page, the only one whose row also clears what the page shows.
            case SettingKind.Info when row.Key == PrivacyCapability.AudioRecordingKey && services.Recording is not null:
                return BuildAudioRecording(row);

            // The tenth, and the only one behind which nothing is written down by D47 at all until the
            // Commander presses something.
            case SettingKind.Info when row.Key == DebriefCapability.DirectionsKey && services.Debrief is not null:
                return BuildDebrief(row);

            // An Info row that also clears the state it describes.
            case SettingKind.Info when row.Press is not null || row.PressAsync is not null:
                return BuildPressable(row, message);

            // A disclosure that is consulted rather than read.
            case SettingKind.Info when row.ValueAsHint:
                return (new Avalonia.Controls.Panel(), () => { });

            // A summary with its full detail behind a one-press disclosure (#339).
            case SettingKind.Info when row.DetailBinding is not null:
                return BuildEgressDisclosure(row);

            case SettingKind.Info:
                return BuildInfo(row);

            case SettingKind.Toggle:
                return BuildToggle(row, message);

            // Free text, or a list too long to step through: a tile that opens the picker page, which stays
            // usable when the list is empty because the value can be typed.
            case SettingKind.Choice when row.AllowsFreeText || IsLongList(row):
                return BuildDropdownTile(row, message);

            case SettingKind.Choice:
                // Segment or stepper, by option count — always a stepper when the list comes from
                // ChoiceSource, since a run-time list can grow past a segment row's four (#274).
                return BuildChoice(row, message);

            case SettingKind.Number:
                return BuildNumber(row, message);

            case SettingKind.Secret:
                return BuildSecret(row, message);

            // One control for both, which is the whole of #217: what a bind row asks is "press the thing you
            // want", and which mechanisms are armed to hear it follows from the row rather than from a second
            // builder.
            case SettingKind.Hotkey:
            case SettingKind.HotasButton:
                return BuildBind(row, message);

            default:
                return BuildText(row, message);
        }
    }

    /// <summary>
    /// The read-out an Info row shows: a data block on Slab, or one tile per figure where the row reads
    /// its value as <see cref="SettingRow.Tiles"/>.
    /// </summary>
    private (Control, Action) BuildInfo(SettingRow row)
    {
        var (inset, text) = SettingsView.Report();

        if (row.Tiles is { } tiles)
        {
            var holder = new ContentControl { Content = inset };

            void RefreshTiles()
            {
                var current = host.Settings.Current;
                var figures = tiles(current);

                if (figures is not { Count: > 0 })
                {
                    text.Text = row.Binding?.Read(current);
                    holder.Content = inset;
                    return;
                }

                var built = figures
                    .Select(figure =>
                    {
                        var tile = Controls.StatTile.Build(figure.Label, figure.Value, Controls.StatInk.Figure);
                        tile.Classes.Add(SettingsView.DataBlockClass);
                        return (Control)tile;
                    })
                    .ToList();

                holder.Content = Controls.StatTile.Grid(built, maxColumns: 3);
            }

            return (holder, RefreshTiles);
        }

        return row.Binding?.Read is { } read
            ? (inset, () => text.Text = read(host.Settings.Current))
            : (inset, () => { });
    }

    /// <summary>
    /// The summary <see cref="BuildInfo"/> already draws, plus the full text behind a SHOW tile that
    /// reads HIDE while it is open (#339).
    /// </summary>
    private (Control, Action) BuildEgressDisclosure(SettingRow row)
    {
        var (summary, refreshSummary) = BuildInfo(row);

        var detail = new SelectableTextBlock
        {
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        Themed(detail, SelectableTextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var block = new Border { Padding = new Thickness(14, 10), Child = detail, Classes = { SettingsView.DataBlockClass } };
        Themed(block, Border.BackgroundProperty, ThemeManager.SlabKey);
        block[!Visual.IsVisibleProperty] = detail[!Visual.IsVisibleProperty];

        var toggle = new Button
        {
            Name = $"Disclose_{row.Key.Replace('.', '_')}",
            Content = "Show",
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        toggle.Click += (_, _) =>
        {
            detail.IsVisible = !detail.IsVisible;
            toggle.Content = detail.IsVisible ? "Hide" : "Show";
        };

        var stack = new StackPanel { Spacing = 8, Children = { summary, toggle, block } };

        void Refresh()
        {
            refreshSummary();

            if (row.DetailBinding is { } read)
            {
                detail.Text = read(host.Settings.Current);
            }
        }

        return (stack, Refresh);
    }

    /// <summary>A disclosure with the button that clears it.</summary>
    private (Control, Action) BuildMemories(SettingRow row)
    {
        var (inset, refresh) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenMemories",
            Content = "Open what D47 remembers",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += async (_, _) =>
        {
            if (services.Memories is not { } memories || TopLevel.GetTopLevel(open) is not Window owner)
            {
                return;
            }

            await new Controls.MemoryDialog(memories.Book, memories.Now).Over(owner);

            // The window writes the file; this is what puts the new count on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh);
    }

    /// <summary>The debrief summary, plus the way into the proposals.</summary>
    private (Control, Action) BuildDebrief(SettingRow row)
    {
        var (inset, refresh) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenDebrief",
            Content = "Open what D47 has drafted",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += async (_, _) =>
        {
            if (services.Debrief is not { } debrief || open.FindAncestorOfType<Panel.PanelView>() is not { } panel)
            {
                return;
            }

            await panel.Open(new Controls.DebriefPage(debrief.Book, debrief.Now, debrief.Core));

            // The page writes the file; this is what puts the new count on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh);
    }

    private (Control, Action) BuildLore(SettingRow row)
    {
        var (inset, refresh) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenLore",
            Content = "Open your notes",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += async (_, _) =>
        {
            if (services.Lore is not { } editing || TopLevel.GetTopLevel(open) is not Window owner)
            {
                return;
            }

            await new Controls.LoreDialog(editing).Over(owner);

            // The window writes the file; this is what puts the new count on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh);
    }

    /// <summary>The Commander's log, behind a button (Phase 33).</summary>
    private (Control, Action) BuildLogbook(SettingRow row)
    {
        var (inset, refresh) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenLogbook",
            Content = "Write up a session",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += async (_, _) =>
        {
            if (services.Logbook is not { } logbook || open.FindAncestorOfType<Panel.PanelView>() is not { } panel)
            {
                return;
            }

            await panel.Open(new Controls.LogbookPage(logbook));
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        if (services.Logbook is { } book)
        {
            void OnChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(refresh);

            stack.AttachedToVisualTree += (_, _) => book.Changed += OnChanged;
            stack.DetachedFromVisualTree += (_, _) => book.Changed -= OnChanged;
        }

        return (stack, refresh);
    }

    /// <summary>The binding profile summary, and one line per saved profile with a Delete button (#80).</summary>
    private (Control, Action) BuildBindingProfiles(SettingRow row)
    {
        var (inset, refreshSummary) = BuildInfo(row);
        var list = new StackPanel { Name = "BindingProfiles", Spacing = 4, Margin = new Thickness(14, 0, 0, 0) };

        void Refresh()
        {
            refreshSummary();
            list.Children.Clear();

            if (services.BindingProfiles is not { } profiles)
            {
                return;
            }

            foreach (var name in profiles.Names)
            {
                var label = new TextBlock
                {
                    Text = name,
                    FontSize = TypeScale.Body,
                    MinWidth = 160,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var delete = new Button
                {
                    Content = "Delete",
                    FontSize = TypeScale.Body,
                    Padding = new Thickness(8, 4),
                    Classes = { SettingsView.DestructiveClass },
                };

                delete.Click += (_, _) => profiles.Delete(name);

                var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { label, delete } };
                list.Children.Add(line);
            }
        }

        var stack = new StackPanel { Spacing = 8, Children = { inset, list } };

        if (services.BindingProfiles is { } store)
        {
            void OnChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);

            stack.AttachedToVisualTree += (_, _) => store.Changed += OnChanged;
            stack.DetachedFromVisualTree += (_, _) => store.Changed -= OnChanged;
        }

        return (stack, Refresh);
    }

    /// <summary>What the audio recorder holds, the way into reviewing it, and the wipe (#164).</summary>
    private (Control, Action) BuildAudioRecording(SettingRow row)
    {
        var (inset, refresh) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenAudioRecorder",
            Content = "Review the recording",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += async (_, _) =>
        {
            if (services.Recording is not { } recording || open.FindAncestorOfType<Panel.PanelView>() is not { } panel)
            {
                return;
            }

            await panel.Open(new Controls.AudioRecorderPage(recording.Log, recording.Now));

            // Keeping a row changes what the summary says, and the page is where keeping happens — so the
            // row is re-read on the way out rather than left stating what was true when it opened.
            host.Refresh();
        };

        var wipe = new Button
        {
            Name = $"Press_{row.Key.Replace('.', '_')}",
            Content = row.PressLabel,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        wipe.Click += (_, _) =>
        {
            row.Press!();
            host.Refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open, wipe } };

        return (stack, refresh);
    }

    private (Control, Action) BuildPressable(SettingRow row, StatusLine message)
    {
        var (inset, baseRefresh) = BuildInfo(row);

        var press = new Button
        {
            Name = $"Press_{row.Key.Replace('.', '_')}",
            Content = row.PressLabelFor?.Invoke() ?? row.PressLabel,
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (row.Destructive)
        {
            press.Classes.Add(SettingsView.DestructiveClass);
        }

        // A label computed from state discovered after the row was built — a pending update's version —
        // has to be re-read on every refresh, not only when the button is first drawn (#193).
        void refresh()
        {
            baseRefresh();

            if (row.PressLabelFor is { } label)
            {
                press.Content = label();
            }

            if (row.PressEnabled is { } enabled)
            {
                press.IsEnabled = enabled(host.Settings.Current);
            }

            if (row.PressVisible is { } visible)
            {
                press.IsVisible = visible();
            }
        }

        refresh();

        // Along the bottom of the button rather than across the row, because it is the button's work it is
        // reporting.
        var bar = new ProgressBar
        {
            Name = $"Progress_{row.Key.Replace('.', '_')}",
            Height = 3,
            Minimum = 0,
            Maximum = 1,
            IsVisible = false,
        };

        if (row.ConfirmPress)
        {
            WireConfirmPress(row, press, bar, message);
        }
        else if (row.PressAsync is { } running)
        {
            press.Click += async (_, _) => await RunPressAsync(row, running, press, bar, message);
        }
        else
        {
            press.Click += (_, _) =>
            {
                row.Press!();

                // The whole surface rather than this row, because a press is not always about the row it is
                // on: binding a core to a ship changes what the row above says *and* what the list below it
                // says, and refreshing only the one pressed left the other one stating the state before the
                // press (Phase 35).
                host.Refresh();
            };
        }

        var pressed = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { press, bar },
        };

        // Nothing to read means nothing to show above the button, so the inset stays out (#78).
        var stack = row.Binding?.Read is null
            ? new StackPanel { Spacing = 8, Children = { pressed } }
            : new StackPanel { Spacing = 8, Children = { inset, pressed } };

        if (row.Watch is { } watch)
        {
            Action? unwatch = null;

            stack.AttachedToVisualTree += (_, _) =>
                unwatch = watch(() => Dispatcher.UIThread.Post(refresh));
            stack.DetachedFromVisualTree += (_, _) => unwatch?.Invoke();
        }

        return (stack, refresh);
    }

    /// <summary>
    /// A press this row acts on only asks the first time: it arms the button, and the actual work
    /// waits for a second press inside the window. Nothing else on the panel is touched by the first
    /// press, and the ask lapses back to the row's own label on its own if the second press does not
    /// come — pressable exactly like any other row, so the headset ray reaches it too (#85).
    /// </summary>
    private void WireConfirmPress(SettingRow row, Button press, ProgressBar bar, StatusLine message)
    {
        var armed = false;
        DispatcherTimer? lapse = null;

        void Disarm()
        {
            armed = false;
            press.Content = row.PressLabel;
            lapse?.Stop();
        }

        void Arm()
        {
            armed = true;
            press.Content = "Press again to confirm";

            lapse?.Stop();
            lapse = new DispatcherTimer { Interval = SettingsView.ConfirmPressWindow };
            lapse.Tick += (_, _) => Disarm();
            lapse.Start();
        }

        press.Click += async (_, _) =>
        {
            if (!armed)
            {
                Arm();
                return;
            }

            Disarm();

            if (row.PressAsync is { } running)
            {
                await RunPressAsync(row, running, press, bar, message);
            }
            else
            {
                row.Press!();
                host.Refresh();
            }
        };
    }

    /// <summary>Whether a long press is already running.</summary>
    private bool _pressing;

    /// <summary>A press that takes long enough to watch (#101).</summary>
    private async Task RunPressAsync(
        SettingRow row,
        LongPress running,
        Button press,
        ProgressBar bar,
        StatusLine message)
    {
        if (_pressing)
        {
            return;
        }

        _pressing = true;
        press.IsEnabled = false;
        bar.Value = 0;
        bar.IsVisible = true;
        message.IsVisible = false;

        try
        {
            var progress = new Progress<double>(fraction => bar.Value = fraction);
            var said = await running(progress, CancellationToken.None);

            if (said is { Length: > 0 })
            {
                message.Say(said);
            }
        }
        catch (Exception ex) when (ex is D47.Core.Audio.TtsException or HttpRequestException or IOException
                                       or TaskCanceledException)
        {
            message.Fail($"{row.Label} could not be done: {ex.Message}");
        }
        finally
        {
            _pressing = false;
            press.IsEnabled = true;
            bar.IsVisible = false;

            // The whole surface, for the reason the plain press refreshes it: what a press changes is not
            // always the row it was on.
            host.Refresh();
        }
    }

    /// <summary>The coverage summary, plus the way into the whole list.</summary>
    private (Control, Action) BuildCoverage(SettingRow row)
    {
        var (inset, refresh) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenCoverage",
            Content = "Show the list",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += async (_, _) =>
        {
            if (services.Coverage is not null && open.FindAncestorOfType<Panel.PanelView>() is { } panel)
            {
                await panel.Open(new Controls.CoveragePage(services.Coverage()));
            }
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh);
    }

    /// <summary>The Commander's own cores, plus the way into the editor (remediation.md 11, item 9).</summary>
    private (Control, Action) BuildOwnPersonas(SettingRow row)
    {
        var (inset, refresh) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenOwnPersonas",
            Content = "Write a core",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += async (_, _) =>
        {
            if (services.OwnPersonas is null || open.FindAncestorOfType<Panel.PanelView>() is not { } panel)
            {
                return;
            }

            await panel.Open(new Controls.PersonaPage(services.OwnPersonas));

            // The editor writes the file; this is what puts the new summary on the row without waiting for
            // something else to notice.
            refresh();
        };

        return (new StackPanel { Spacing = 8, Children = { inset, open } }, refresh);
    }

    /// <summary>The macro summary, plus the way into the editor.</summary>
    private (Control, Action) BuildMacros(SettingRow row)
    {
        var (inset, refresh) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenMacros",
            Content = "Edit macros",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += async (_, _) =>
        {
            if (services.Macros is null || TopLevel.GetTopLevel(open) is not Window owner)
            {
                return;
            }

            await new Controls.MacroDialog(services.Macros) { ReservedPhrases = services.Reserved }.Over(owner);

            // The editor writes the file; this is what puts the new summary on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh);
    }

    /// <summary>The checklist summary, plus the way into the panel.</summary>
    private (Control, Action) BuildChecklists(SettingRow row)
    {
        var (inset, refresh) = BuildInfo(row);

        // A tab of this panel since Phase 25, rather than a dialog over it: a Window cannot appear in the
        // headset at all, so the checklist was unreachable there for a Commander wearing one.
        var open = new Button
        {
            Name = "OpenChecklist",
            Content = "Open the checklist",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += (_, _) =>
        {
            // Its own panel, found up the tree rather than handed in.
            if (open.GetSelfAndVisualAncestors().OfType<Panel.PanelView>().FirstOrDefault() is { } panel)
            {
                panel.Nav.Show("checklist");
            }

            // The tab writes the file; this is what puts the new summary on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh);
    }

    /// <summary>The switch summary, plus the way into the walk.</summary>
    private (Control, Action) BuildSwitches(SettingRow row)
    {
        var (inset, refresh) = BuildInfo(row);

        var open = new Button
        {
            Name = "OpenSwitches",
            Content = "Assign switches",
            FontSize = TypeScale.Body,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        open.Click += async (_, _) =>
        {
            if (services.Switches is not { } editing || open.FindAncestorOfType<Panel.PanelView>() is not { } panel)
            {
                return;
            }

            await panel.Open(new Controls.SwitchPage(
                editing.Store,
                editing.Reader,
                editing.Reconciler,
                editing.Now,
                editing.ExportPath,
                editing.Destinations()));

            // The editor writes the file; this is what puts the new summary on the row without waiting for
            // something else to notice.
            refresh();
        };

        var stack = new StackPanel { Spacing = 8, Children = { inset, open } };

        return (stack, refresh);
    }
}
