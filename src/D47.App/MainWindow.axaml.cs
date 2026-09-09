using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Updates;
using D47.App.Windowing;
using D47.App.Controls;
using D47.App.Input;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Diagnostics.Donation;
using D47.Core.Listening;
using D47.Core.Audio;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

using D47.Core.Updates;

namespace D47.App;

/// <summary>The desktop host for <see cref="PanelView"/>.</summary>
public partial class MainWindow : Window
{
    private readonly AppHost? _host;
    private readonly GlobalHotkey _shutUp;

    /// <summary>The two keys that reach the flat mini panel (Phase 48).</summary>
    private readonly GlobalHotkey _showOverlay;

    private readonly GlobalHotkey _moveOverlay;
    private readonly PanelViewModel _model;

    private AvailableUpdate? _availableUpdate;
    private bool _turnInFlight;

    /// <summary>Whether the input waiting in the ask box got there by being spoken.</summary>
    private bool _spoken;

    /// <summary>Whether the window is actually on screen — not minimised, and visible.</summary>
    private volatile bool _onScreen = true;

    /// <summary>Reads the two window halves on the UI thread, for <see cref="_onScreen"/>.</summary>
    private void SampleOnScreen() => _onScreen = WindowState != WindowState.Minimized && IsVisible;

    public MainWindow() : this(host: null)
    {
    }

    public MainWindow(AppHost? host)
    {
        _host = host;

        // The frame border in the theme's own dark, not the system's light (2026-08-31) — the dialogs get the
        // same through Dialogs.Over.
        Windowing.DarkWindowBorder.Apply(this);

        // The host's model, not one of this window's own.
        _model = host?.Panel ?? new PanelViewModel();
        var hotkeyLogger = host?.Loggers.CreateLogger<GlobalHotkey>()
                           ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GlobalHotkey>.Instance;

        // Why the log will say d47 stopped (remediation.md 10, item 7).
        if (host is not null)
        {
            Closing += (_, _) => host.StoppingBecause = "the window was closed";
        }

        _shutUp = new GlobalHotkey(hotkeyLogger);
        _showOverlay = new GlobalHotkey(hotkeyLogger);
        _moveOverlay = new GlobalHotkey(hotkeyLogger);

        InitializeComponent();

        // The push-to-talk key must never reach a control in this window, and these tunnel so they run before
        // the focused control rather than after it — a bubbling handler is too late, because the text box has
        // already inserted the character by then.
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnTunnelKeyUp, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTunnelTextInput, RoutingStrategies.Tunnel);

        if (host is not null)
        {
            PushToTalkGesture = () => host.Settings.Current.Listening.PushToTalkKey;
        }

        // The version lives in the chrome that is on screen anyway, and it is set here rather than on load
        // because it does not depend on the host - a window with no version in its title, however briefly, is
        // a window that cannot answer the one question a title bar is good at.
        Title = $"Directive 47 — {BuildInfo.Semantic}";

        Panel.DataContext = _model;

        // The Commander's own frames, where they have supplied them.
        Panel.Avatar.Library = host?.Avatars;
        _model.AskRequested += () => _ = AskAsync();

        // The desktop window is the one surface with a browser to open the site in; the headset copy is not
        // handed this and so shows no help button (change-requests.md 24).
        Panel.EnableHelp(url => Process.Start(
            new ProcessStartInfo(url) { UseShellExecute = true }));
        _model.UpdateAccepted += OnUpdateAccepted;
        _model.UpdateDismissed += () => _model.UpdateText = null;

        if (host is not null)
        {
            // The log page reads through this rather than knowing a path, so the view model stays free of a
            // disk and a test can hand it a string.
            _model.LogSource = () => Logging.LogTail.Read(host.Paths.Logs);

            // Elite's journal, from the events the tick loop already polled (#51).
            _model.JournalSource = noise => host.JournalLog.Read(noise);
            _model.JournalDocumentSource = noise => host.JournalLog.Document(noise);

            // And the JSON behind them, which is this window's alone: a wall of fields is there to be
            // selected and pasted into a bug report, which is an act with no meaning in mid-air.
            Panel.EnableRawJournal();

            // And it keeps the switch where the Commander left it (#267).
            Panel.RememberJournalReading(new JournalReadingMemory(host.ViewState));

            // The sharper half of that same act (#160).
            Panel.EnableDonation(() => _ = ShowDonationAsync(host));

            // The window that can show settings says so; the headset's copy of this same view is handed
            // nothing and therefore has no Settings tab (Phase 12).
            Panel.EnableSettings(BuildSettingsPage, RevealSetting);

            // The checklist, on the other hand, goes to both surfaces — which is the whole headline of the
            // item that moved it out of a Window.
            Panel.EnableChecklist(
                host.Checklists,
                host.Goals?.Book,
                host.Goals?.Backfill,
                () => new SourcingPage(
                    host.Capabilities,
                    host.Sourcing,
                    host.Carrier,
                    () => host.GameState.Active,
                    () => host.Settings.Current.Knowledge.GalaxySearch,
                    OpenSettings));

            // The stories the Commander flies (Phase 47). **Both surfaces from 2026-08-22**, on the
            // Commander's instruction: the tab was desktop-only on the reasoning that the editor and the ask
            // form want a keyboard, and that was the wrong half to weigh — a Commander wearing a headset is
            // exactly the one who has just arrived somewhere and wants to know what the story made of it, and
            // the prompts have taken a spoken value since Phase 25.
            if (host.Adventures is { } adventures)
            {
                Adventures = new AdventureSurface(
                    adventures.Book,
                    adventures.Generator,
                    () => host.GameState.Active,
                    () => host.GameState.Active?.Identity.FrontierId,
                    () => DateTimeOffset.Now,
                    host.SayAside,
                    () => host.Turns.Provider is not null,
                    () => host.Settings.Current.Knowledge.GalaxySearch,
                    () => host.Galaxy is { } galaxy && host.Settings.Current.Knowledge.GalaxySearch
                        ? new D47.Core.Adventures.AdventureResolver(galaxy)
                        : null,
                    OpenSettings);

                Panel.EnableAdventures(Adventures);
            }

            // The fleet and its builds, what the Commander is wearing, and the arithmetic between them
            // (Phases 26 and 27).
            Panel.EnableLoadout(
                host.Ships,
                host.Checklists,
                () => host.GameState.Active,
                host.OnFootPlans,
                () => host.ModulePower,
                new ShipsDrawingsMemory(host.ViewState));

            // Where the hull art is read from, in the order it is searched.
            ShipArt.Folder = host.Paths.Ships;
            ShipArt.Shipped = host.Paths.ShippedShips;

            // And where the two large files per hull come from when they are wanted.
            ShipArtStore.Enable(
                host.Paths.Ships,
                () => host.Settings.Current.Ui.HullArt,
                host.Loggers.CreateLogger("D47.App.Panel.ShipArtStore"));

            // Who to go and unlock next, read across both plan stores (Phase 28).
            Panel.EnableEngineers(
                host.Unlocks, host.Ships, () => host.GameState.Active, host.OnFootPlans);

            // Where the Commander is going, in three readings of one journey (Phase 37).
            Panel.EnableRouting(new RoutingSurface(
                () => host.Route,
                () => host.GameState.Active?.Location.StarSystem,
                host.Capabilities,
                host.Plans,
                () => host.Settings.Current.Knowledge.GalaxySearch,
                OpenSettings,

                // And the Market page beside them (Phase 49), for the same reason the plan forms are here and
                // not in the headset: it wants a keyboard.
                host.Commodities,

                // What the Neutron Plotter's jump range placeholder quotes (#253).
                () => host.GameState.Active?.Ship.MaxJumpRange,

                // And the Community Goal page (#296): the saved search, its ledger, and who and when to ask
                // them about.
                new CommunityGoalSurface(
                    host.CommunityGoalSearch,
                    host.CommodityLedger,
                    () => host.GameState.Active?.Identity.FrontierId,
                    () => DateTimeOffset.Now,
                    at => CommodityLedger.Week(
                        at,
                        host.Settings.Current.Callouts.WeekBoundaryDay,
                        host.Settings.Current.Callouts.WeekBoundaryHourUtc))));

            // "Refresh" by voice means this search only while its page is what the window is showing (#296),
            // and the window is the one thing that knows that.
            SampleOnScreen();
            PropertyChanged += (_, change) =>
            {
                if (change.Property == WindowStateProperty || change.Property == IsVisibleProperty)
                {
                    SampleOnScreen();
                }
            };

            host.CommunityGoalSearch.Showing = () =>
                Panel.Nav.Tab == PanelTab.Routing
                && Panel.Nav.Root.Key == RoutingPages.CommunityGoalRoot
                && !Panel.Nav.Modal
                && _onScreen;

            // And the clocks, timers and alarms (Phase 24).
            Panel.EnableUtilities(
                host.Timekeeper,
                host.Alarms,
                () => D47.Core.SystemWallClock.Instance.UtcNow,
                () => TimeZoneInfo.Local);

            // And the same window is the one with a keyboard, so it is the one that gets a search box.
            Panel.EnableSearch();

            // And the same window is the one with a mouse, which is the only thing the ask lets drag a pane
            // (Phase 55).
            Panel.EnableDraggablePanes(new PaneWidthMemory(host.ViewState));

            // And the same window is the one with somewhere to open a dialog, which is what the turn line's
            // figures need.
            Panel.EnableTurnDetails(() => _ = ShowSpendAsync());

            // A value being said rather than typed reaches this surface's open prompt, if it has one (Phase
            // 25).
            host.RoutePrompts(heard =>
            {
                if (!Panel.Prompts.IsListening)
                {
                    return false;
                }

                // Onto the thread that owns the controls it is about to write into.
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Panel.Prompts.Hear(heard));
                return true;
            });

            // And a spoken "show me the checklist" moves this surface (Phase 25), as does a switch (Phase 46)
            // — which arrives from the tick thread, so it is given the dispatcher captured here rather than
            // left to read the static one from a worker.
            var ui = Avalonia.Threading.Dispatcher.UIThread;
            // The window leads: its tab carries to any surface that furnished the same one
            // (change-requests.md 34).
            host.RouteNavigation(Panel.Nav, move => ui.Post(move), leads: true);

            // And a spoken "page down" moves whatever page this surface is showing (#34).
            host.RouteScrolling(Panel.Scroll);

            // A clock is the one page whose content changes with nothing having happened, so it is pushed
            // rather than pulled (Phase 24).
            host.Tick.Add("clocks", _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Panel.TickClocks()));

            // The engineer pages move for a different reason: nothing has to happen for a clock to change,
            // and everything has to happen for a ranking to.
            host.Tick.Add("engineers", _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Panel.TickEngineers()));

            // And the "d47 is composing" animation on the Adventures tab, by the same route again (asked for
            // 2026-08-22).
            host.Tick.Add("adventures", _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Panel.TickAdventures()));

            // And the ship pages, for the same reason and by the same route (remediation.md 17, item 7).
            host.Tick.Add("loadout", _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(Panel.TickLoadout));

            // And the route being flown, by the same route again (Phase 37).
            host.Tick.Add("routing", _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(Panel.TickRouting));

            // And the same window is the one with a keyboard, so it is the one whose mini keeps the ask line
            // (Phase 51).
            Panel.EnableAskInMini();

            // And a control you can see, which is the way out a Commander finds without being told (asked for
            // 2026-08-24).
            Panel.EnableModeToggle(mode => host.Settings.Apply(
                InterfaceCapability.WindowModeKey,
                mode == PanelMode.Mini ? "mini" : "full",
                SettingsCaller.Panel));

            // And every tab back on the reading it was left on (#268).
            Panel.RememberRoots(new PanelRootMemory(host.ViewState));

            // And the tab itself, back where it was left (#276).
            Panel.RememberTab(new PanelTabMemory(host.ViewState));

            // Both before the window is shown.
            var mini = IsMini(host.Settings.Current);

            Panel.Mode = mini ? PanelMode.Mini : PanelMode.Full;

            _placement = WindowPlacementMemory.Attach(
                this, host.ViewState, startMini: mini, miniSize: mini ? MiniSize() : null);

            // Read before the first paint for the same reason: the worked example appearing and then
            // vanishing is worse than either state, and it is the Commander who has already asked — the one
            // who does not need it — who would see it happen.
            _model.HasAsked = host.ViewState.Load().HasAsked;

            // One zoom host, on the one window.
            ZoomHost.Attach(this, host.Settings);
        }
    }

    /// <summary>What the panel is showing.</summary>
    public PanelViewModel Model => _model;

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (_host is null)
        {
            _model.Append("No host: the window is running under the designer.");
            return;
        }

        var errors = new List<string>();
        if (_host.StartupError is { } startupError)
        {
            errors.Add(startupError);
        }

        // The Phase 1 claim is that a request produces a real tool call that runs and returns a result.
        var status = await _host.Capabilities.InvokeAsync("get_app_status", ToolArguments.Empty);

        if (status.IsError)
        {
            errors.Add(status.Content);
        }

        // Say plainly whether the model is available.
        var availability = _host.LlmAvailability;
        _model.Append(
            availability.Current == LlmAvailability.Available
                ? "\nLanguage model: ready."
                : $"\nLanguage model: unavailable. {availability.Reason} " +
                  "Keyword commands still work — try \"where am I\" or \"status\".");

        if (errors.Count > 0)
        {
            _model.ErrorText = string.Join(Environment.NewLine, errors);
        }

        // A voice companion that cannot speak has to say so.
        _host.Voice.SynthesisFailed += reason => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _model.ErrorText = reason);

        DescribeHotkeys();
        BindShutUp();
        BindOverlayKeys();

        // Spoken input runs the same turn as typed input, deliberately.
        _host.Heard += text => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Spoken and typed run the same turn - the Commander expects "where am I" to mean the same thing
            // either way - but the router is told which it was, because a couple of phrases only mean what
            // they say when they arrived through a microphone.
            _spoken = true;
            _model.AskText = text;
            _ = AskAsync();
        });

        // What was heard, where no response is going to carry it — an utterance a chooser took, or one the
        // wake policy reworded on the way in (change-requests.md 31).
        _host.HeardText += text => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _model.Append("\n" + text + "\n"));

        // Anything d47 says without a turn behind it still belongs in the transcript, so what was heard and
        // what can be read back are the same set.
        _host.Said += text => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _model.Append($"\n{text}\n"));

        // And what happened to the conversation rather than in it - the core changing under it.
        _host.Noted += text => Avalonia.Threading.Dispatcher.UIThread.Post(() => _model.Mark(text));

        // In-game comms are deliberately not written to the transcript (#260), and the reasoning is the one
        // they were kept off the conversation with in the first place: a station and a police interceptor are
        // not talking to the Commander's companion, and a station approach brings a lot of them.

        _host.Settings.Changed += change => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            DescribeHotkeys();

            if (change.Key == ListeningCapability.CancelHotkeyKey)
            {
                BindShutUp();
            }

            if (change.Key == InterfaceCapability.ShowOverlayHotkeyKey
                || change.Key == InterfaceCapability.MoveOverlayHotkeyKey)
            {
                BindOverlayKeys();
            }

            // Mini and back, with no restart (Phase 51, and Phase 4's rule about every setting).
            if (change.Key == InterfaceCapability.WindowModeKey)
            {
                ApplyWindowMode();
            }
            else if (change.Key == InterfaceCapability.ZoomKey)
            {
                _placement?.Remeasured(MiniSize());
            }
        });

        // Deliberately not focusing the Ask box.

        // Said aloud as well as shown, because a misconfigured provider otherwise presents as silence, and
        // silence is indistinguishable from a model with nothing to say (Phase 5).
        _ = _host.AnnounceStartupProblemsAsync();

        // Optional in two senses: it must never delay the status the Commander is here for, and it is the one
        // network call d47 makes on its own — so it is a setting, and it is disclosed (Phase 4, "Say what
        // each provider receives").
        if (_host.Settings.Current.Updates.CheckOnStartup)
        {
            _ = CheckForUpdateAsync(_host);
        }

        // Before the Start Menu offer, because it is the one that decides whether d47 can answer at all — and
        // a Commander who has just been asked about a shortcut has already formed a view about how much this
        // app asks of them.
        await OfferKeysAsync();

        // Last, and awaited rather than fired and forgotten: it is modal, so it must not appear over a panel
        // that is still assembling itself.
        await OfferStartMenuEntryAsync();

    }

    /// <summary>The guided key setup, shown when there is no usable language-model key (Phase 16).</summary>
    private async Task OfferKeysAsync()
    {
        if (_host is not { } host)
        {
            return;
        }

        // The one gate, and it is on the *offer* rather than on the window.
        if (!FirstRun.IsNeeded(
                LlmProviderCatalog.Selected(host.Settings.Current.Llm.Provider),
                host.Secrets.Has))
        {
            return;
        }

        await ShowKeySetupAsync();
    }

    /// <summary>The guided key setup, shown because it was asked for.</summary>
    private async Task ShowKeySetupAsync()
    {
        if (_host is not { } host)
        {
            return;
        }

        var provider = LlmProviderCatalog.Selected(host.Settings.Current.Llm.Provider);

        var steps = FirstRun.Steps(
            host.Capabilities,
            host.Settings.Current,
            provider,
            host.Secrets.Has,
            ConversationCapability.KeyRowFor(provider),

            // The voice key, offered because a companion that talks back is most of the point — and offered
            // second, because one that does not is still a companion.
            [SpeechCapability.KeyRowFor(TtsProviderCatalog.ElevenLabs)]);

        if (steps.Count == 0)
        {
            return;
        }

        await new FirstRunWindow(steps, host.Settings).Over(this);
    }

    /// <summary>Window-scoped gestures, matched against the bound settings.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_host is not null && !e.Handled)
        {
            if (Matches(_host.Settings.Current.Hotkeys.OpenSettings, e))
            {
                e.Handled = true;
                OpenSettings();
            }
            else if (Matches(_host.Settings.Current.Hotkeys.FocusAsk, e))
            {
                e.Handled = true;
                Panel.FocusAsk();
            }
            else if (Matches(_host.Settings.Current.Hotkeys.WindowMode, e))
            {
                // The way back that works when there is nothing at all on the surface (Phase 51).
                e.Handled = true;

                _host.Settings.Apply(
                    InterfaceCapability.WindowModeKey,
                    Panel.Mode == PanelMode.Full ? "mini" : "full",
                    SettingsCaller.Hotkey);
            }
        }

        // Escape leaves the settings page for the one it covered up.
        if (e.Key == Key.Escape && !e.Handled && Panel.GoBack())
        {
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// Whether the push-to-talk key is down right now, as seen by this window rather than by the
    /// ten-times-a-second poll.
    /// </summary>
    private bool _pushToTalkHeld;

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsPushToTalk(e))
        {
            _pushToTalkHeld = true;
            e.Handled = true;
        }
    }

    private void OnTunnelKeyUp(object? sender, KeyEventArgs e)
    {
        if (IsPushToTalk(e))
        {
            // Cleared even when the key is not matched as handled below, so a rebind while the key is held
            // cannot leave text input suppressed for the rest of the session.
            _pushToTalkHeld = false;
            e.Handled = true;
        }
    }

    private void OnTunnelTextInput(object? sender, TextInputEventArgs e)
    {
        if (_pushToTalkHeld)
        {
            e.Handled = true;
        }
    }

    /// <summary>The bound push-to-talk gesture.</summary>
    internal Func<string?> PushToTalkGesture { get; set; } = () => null;

    private bool IsPushToTalk(KeyEventArgs e) => Matches(PushToTalkGesture(), e);

    /// <summary>
    /// Gestures are stored in the form <see cref="KeyGesture"/> writes, so an unparseable one is a
    /// gesture that never matches rather than an exception on every keystroke.
    /// </summary>
    private static bool Matches(string? gesture, KeyEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        try
        {
            return KeyGesture.Parse(gesture).Matches(e);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            // A hand-edited settings file can hold anything.
            return false;
        }
    }

    private void DescribeHotkeys()
    {
        if (_host is null)
        {
            return;
        }

        var open = _host.Settings.Current.Hotkeys.OpenSettings;

        // Read from settings rather than hardcoded, so rebinding the gesture updates the tip instead of
        // leaving a "Ctrl+," that quietly became a lie.
        ToolTip.SetTip(
            Panel.SettingsAffordance,
            open is null ? "Settings" : $"Settings ({Gestures.Describe(open)})");
    }

    private void OpenSettings() => Panel.Tab = PanelTab.Settings;

    /// <summary>The two rectangles this window remembers, and which one it is in.</summary>
    private WindowPlacementMemory? _placement;

    private static bool IsMini(D47Settings settings) =>
        string.Equals(settings.Ui.Mode, "mini", StringComparison.OrdinalIgnoreCase);

    /// <summary>Puts the window into the shape the setting names, and back (Phase 51).</summary>
    private void ApplyWindowMode()
    {
        if (_host is null)
        {
            return;
        }

        var mini = IsMini(_host.Settings.Current);

        if (mini == (Panel.Mode == PanelMode.Mini))
        {
            return;
        }

        _placement?.Resize(mini, MiniSize());

        Panel.Mode = mini ? PanelMode.Mini : PanelMode.Full;
    }

    /// <summary>What mini wants: measured rather than typed (Phase 51).</summary>
    private Size MiniSize()
    {
        var scale = ZoomLadder.ScaleOf(
            ZoomLadder.Snap(_host?.Settings.Current.Ui.ZoomPercent ?? ZoomLadder.Default));

        return new Size(
            PanelResolution.Mini.Width * scale,
            (PanelResolution.Mini.Height + Panel.MiniExtraHeight(PanelResolution.Mini.Width)) * scale);
    }

    /// <summary>The settings surface, built the first time the tab is selected.</summary>
    internal AdventureSurface? Adventures { get; }

    public Control BuildSettingsPage()
    {
        var view = new SettingsView();

        if (_host is not null)
        {
            view.Attach(
                _host.Settings,
                _host.ViewState,
                _host.Paths,
                _host.CoverageRecorder is { } recorder ? recorder.Report : null,
                _host.Macros,
                _host.Checklists,
                _host.ReservedPhrases,
                _host.SwitchEditing,

                // The choice is the go-ahead: it states its size in the list it was made from, and the row
                // shows what it is doing while it does it.
                (model, progress) => _host.InstallModelAsync(model, progress),

                // About's way back in.
                ShowKeySetupAsync,

                // The Commander's own notes, and the search that decides how one is filed.
                _host.LoreEditing,
                _host.Memories,

                // And the log those journals can be turned into (Phase 33).
                _host.Logbook,

                // And the cores the Commander wrote themselves (remediation.md 11, item 9).
                _host.OwnPersonas,

                // And what the audio recorder kept, when this process was asked to record (#164).
                _host.AudioRecorder is { } recording
                    ? (recording.Log, (Func<DateTimeOffset>)(() => DateTimeOffset.Now))
                    : null,

                // And what the debrief drafted from the last session (#162).
                _host.Debrief is { } debrief
                    ? (debrief.Book, debrief.Now, (Func<D47.Core.Persona.Persona>)(() => _host.Personas.Current))
                    : null);

            // The gap reaction happens in the host, on whatever thread resolved the switch, and the
            // affordance it belongs to is a row on this surface.
            _host.PersonaSettling += settling => Avalonia.Threading.Dispatcher.UIThread.Post(
                () => view.ShowBusy(PersonaCapability.PersonaKey, settling));

            // A file dropped into data/audio rebuilds the cue library without any setting having changed, so
            // the row that says what was found has no other way to know.
            _host.AudioReloaded += () => Avalonia.Threading.Dispatcher.UIThread.Post(view.Refresh);

            // The two About rows that need a window to open one over (#50).
            _host.ShowChangelog = () =>
                _ = new Controls.ChangelogWindow(D47.Core.Help.Changelog.Text).Over(this);

            _host.SetUpKeys = ShowKeySetupAsync;
        }

        // A card's question mark draws help in the panel rather than launching a browser (asked for
        // 2026-08-23).
        view.EnableHelp(capabilityId => Panel.OpenHelpFor(capabilityId));

        _settingsPage = view;
        return view;
    }

    /// <summary>
    /// The settings page once something has asked for it, so a help card can reach the instance that is
    /// actually on screen rather than build a second one.
    /// </summary>
    private SettingsView? _settingsPage;

    /// <summary>Shows one settings section, for a help card that names it (asked for 2026-08-23).</summary>
    private void RevealSetting(string capabilityId) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _settingsPage?.Reveal(capabilityId),
            Avalonia.Threading.DispatcherPriority.Loaded);

    /// <summary>Remembers that this Commander has asked something, once and for good.</summary>
    private void MarkAsked()
    {
        if (_host is null || _model.HasAsked)
        {
            return;
        }

        _model.HasAsked = true;
        _host.ViewState.Save(_host.ViewState.Load() with { HasAsked = true });
    }

    private async Task AskAsync()
    {
        if (_host is null)
        {
            return;
        }

        var input = _model.AskText?.Trim();

        // Taken once, whether or not this turn goes ahead, so a discarded input cannot leave the next typed
        // one looking spoken.
        var source = _spoken ? InputSource.Spoken : InputSource.Typed;
        _spoken = false;

        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        // Recorded here rather than in either modality's own path, because this is where typed and spoken
        // meet — and the hint retires on "has asked at all", not on "has used this control".
        MarkAsked();

        // Asked before the in-flight gate, never after.
        if (_host.Navigate(input) is { } moved)
        {
            _model.AskText = string.Empty;
            _model.Append($"\n\n> {input}\n{moved}\n");
            return;
        }

        // And moving the page rather than the panel (#34).
        if (_host.Scroll(input) is { } scrolled)
        {
            _model.AskText = string.Empty;
            _model.Append($"\n\n> {input}\n{scrolled}\n");
            return;
        }

        if ((_turnInFlight || _host.Audio.IsSpeaking)
            && _host.Router.MatchInterrupting(input) is { } interrupting)
        {
            // Feedback nobody typed (#162).
            if (_host.Audio.IsSpeaking)
            {
                _host.NoteInterrupted();
            }

            _model.AskText = string.Empty;
            var stopped = await _host.Capabilities.InvokeAsync(interrupting.ToolName, ToolArguments.Empty);

            // Two writes rather than one interpolation, because they are two voices.
            _model.Append(input, voice: TranscriptVoice.Commander);
            _model.Append(stopped.Content);
            return;
        }

        if (_turnInFlight)
        {
            return;
        }

        _turnInFlight = true;
        _model.CanAsk = false;
        _model.AskText = string.Empty;
        _model.Append(input, voice: TranscriptVoice.Commander);

        // Kept before the crew scope rewrites `input` below: the adventure feed files an exchange under the
        // Commander's own words, not under the question as it reached a crew member.
        var asked = input;

        // Addressed to somebody in the fighter bay rather than to the ship's AI?
        using var crew = _host.BeginCrewTurn(input);

        if (crew is not null)
        {
            input = crew.Question;
            _model.Append($"[{crew.Member.Name}] ");
        }

        // Claimed before the turn starts and released in the finally.
        var cancelling = _host.Cancellation.Begin();

        try
        {
            // Through the voice pipeline rather than straight off the turn loop, so the panel and the speaker
            // are fed from one traversal of one stream.
            await _host.Voice.RunAsync(
                _host.Turns.RunAsync(input, source, cancelling.Token),
                turnEvent =>
                {
                    switch (turnEvent)
                    {
                        case TurnEvent.Routed routed:
                            _model.TurnLine = routed.Effort is { } effort
                                ? $"routed: {routed.Route}, effort {effort}"
                                : $"routed: {routed.Route}";
                            break;

                        case TurnEvent.TextDelta text:
                            _model.Append(text.Text);
                            break;

                        case TurnEvent.Retrying retry:
                            _model.TurnLine =
                                $"retrying ({retry.Attempt}/{retry.Of}) in {retry.Wait.TotalSeconds:0.#}s — {retry.Because}";
                            break;

                        case TurnEvent.Completed completed:
                            _model.TurnLine = DescribeTurn(completed.Result, _host);

                            // And onto the story's own feed, if it was about one (asked for 2026-08-22).
                            // `asked` rather than `input`: a crew turn rewrites the latter, and what the
                            // Commander said is what the heuristic reads.
                            _host.NoteTurn(asked, completed.Result.Text);
                            break;
                    }
                });
        }
        catch (Exception ex)
        {
            // Which of the two this is — a turn the Commander called off, or one that threw — decided in one
            // place and pinned by its own tests (#222).
            var ending = TurnEnding.For(ex, cancelling.IsCancellationRequested);

            if (ending.Technical is not null)
            {
                // A response that throws is a bug, not a provider failure — provider failures arrive as
                // events.
                _host?.Loggers.CreateLogger<MainWindow>().LogError(ex, "The turn threw");
            }

            // One voice.
            _model.Append(ending.Conversation);
        }
        finally
        {
            // **Released here, which the comment above always said and the code never did** (reported
            // 2026-09-04: every turn after the first logged "a new turn started while one was in flight"
            // against a turn that had finished).
            _host.Cancellation.End(cancelling);

            _turnInFlight = false;
            _model.CanAsk = true;

            // Focus follows the way the turn was started.
            if (source == InputSource.Typed)
            {
                Panel.FocusAsk();
            }
        }
    }

    /// <summary>Asks the Commander whether to download a speech model, and downloads it if they say yes.</summary>
    private void BindShutUp()
    {
        if (_host is null)
        {
            return;
        }

        var gesture = _host.Settings.Current.Speech.ShutUpHotkey;

        // Cancel rather than only silence since #221: the same press abandons the turn, so a Commander who
        // has changed their mind about a long web search stops paying for it.
        if (!_shutUp.Bind(gesture, () => _host.CancelNow()) && !string.IsNullOrWhiteSpace(gesture))
        {
            // Reported rather than swallowed: the symptom of a failed registration is a key that does
            // nothing, which reads as d47 ignoring the Commander.
            _model.ErrorText =
                $"The cancel hotkey {Gestures.Describe(gesture)} could not be registered system-wide. " +
                "Another application is probably holding it — pick another in Settings.";
        }
    }

    /// <summary>The two gestures that reach the flat mini panel (Phase 48).</summary>
    private void BindOverlayKeys()
    {
        if (_host is null)
        {
            return;
        }

        Bind(_showOverlay, _host.Settings.Current.Hotkeys.ShowOverlay, "overlay", ToggleOverlay);
        Bind(_moveOverlay, _host.Settings.Current.Hotkeys.MoveOverlay, "move-the-overlay",
            () => Avalonia.Threading.Dispatcher.UIThread.Post(() => _host.Overlay?.Place()));

        void ToggleOverlay() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            _host.Settings.Apply(
                InterfaceCapability.OverlayKey,
                (!_host.Settings.Current.Ui.Overlay.Enabled).ToString(),
                SettingsCaller.Hotkey));

        void Bind(GlobalHotkey key, string? gesture, string named, Action pressed)
        {
            if (!key.Bind(gesture, pressed) && !string.IsNullOrWhiteSpace(gesture))
            {
                _model.ErrorText =
                    $"The {named} hotkey {Gestures.Describe(gesture)} could not be registered " +
                    "system-wide. Another application is probably holding it — pick another in Settings.";
            }
        }
    }

    /// <summary>
    /// One short line of provenance, with the figures behind a link (docs/plans/change-requests.md item
    /// 2).
    /// </summary>
    private static string DescribeTurn(TurnResult result, AppHost host)
    {
        var line = new StringBuilder($"{result.Outcome} via {result.Route}");

        if (result.Effort is { } effort)
        {
            line.Append($", effort {effort}");
        }

        if (result.Cost is { } cost)
        {
            line.Append(cost.Priced ? $" — {cost.Dollars:C4}" : " — unpriced model");
        }

        return line.ToString();
    }

    /// <summary>Opens the figures.</summary>
    private async Task ShowSpendAsync()
    {
        if (_host is null)
        {
            return;
        }

        await new SpendWindow(
            _host.Spend.Last,
            _host.Spend,
            _host.SpeechSpend,
            _host.SpendLedger,
            _host.Settings.Current,
            TimeZoneInfo.Local,

            // What makes the Reset button appear, and what "this session" means to it (#197).
            _host.LaunchedAt).Over(this);
    }

    /// <summary>
    /// Cuts an incident out of what is already in memory, and puts it in front of the Commander (#160).
    /// </summary>
    private static Donation.DonationDispatch DonationDispatchFor(AppHost host) =>
        Donation.DonationDispatch.For(
            host.Paths, static () => D47.Core.Configuration.DonationSettings.Address, host.Loggers);

    /// <summary>
    /// One window for both shapes of sharing since #238, offered under one button — and the same window
    /// wherever the button is pressed.
    /// </summary>
    private async Task ShowDonationAsync(AppHost host)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Longest first, so the folder is replaced before the account name inside it and the second pass has
        // nothing left to half-match.
        var machine = new List<KeyValuePair<string, string>>
        {
            new(profile, "%USERPROFILE%"),
            new(Environment.UserName, "%USERNAME%"),
        };

        var paperwork = new ExcerptPaperwork(BuildInfo.Full, DateTimeOffset.Now);
        var folder = host.JournalDirectory ?? D47.Core.Journal.JournalFolder.DefaultPath();
        var now = DateTimeOffset.Now;

        var dispatch = DonationDispatchFor(host);

        // **Read from disk, per window, on a worker** (#173): the span is the thing being chosen, so it
        // happens per render rather than once.
        Func<ExcerptRequest, string> build = request =>
        {
            var journal = IncidentSources.Journals(folder, request.From, request.To, _host?.Loggers.CreateLogger("Excerpt"));
            var log = IncidentSources.Logs(host.Paths.Logs, request.From, request.To, TimeZoneInfo.Local);

            return ExcerptReport.Render(
                IncidentExcerpt.Take(
                    journal,
                    log,
                    request,
                    machine,
                    host.GameState.Active?.Identity,
                    host.GameState.Active?.Carrier),
                paperwork);
        };

        var logger = _host?.Loggers.CreateLogger("Corpus");

        Pseudonyms? names = null;
        var from = DateTimeOffset.MinValue;

        Func<CorpusScope, IProgress<int>, CancellationToken, Task<Controls.HelpImproveWindow.CorpusReading>> read =
            (scope, progress, cancel) => Task.Run(
                () =>
                {
                    names = IncidentExcerpt.Seeded(
                        host.GameState.Active?.Identity,
                        host.GameState.Active?.Carrier);

                    from = scope.From(now);

                    var survey = CorpusDonation.Survey(folder, from, now, names, logger, progress, cancel);

                    return new Controls.HelpImproveWindow.CorpusReading(
                        survey,
                        CorpusReport.Render(survey, paperwork));
                },
                cancel);

        // **Declared once and used twice** (#181).
        Func<Stream, IProgress<int>, CancellationToken, Task> write =
            (stream, progress, cancel) => Task.Run(
                () =>
                {
                    if (names is not { } standIns)
                    {
                        return;
                    }

                    // **No BOM, and the caller is the one that owns the stream.** A byte order mark would sit
                    // in front of the first event and stop it parsing as JSON.
                    using var writer = new StreamWriter(
                        stream,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                        bufferSize: 65536,
                        leaveOpen: true);

                    CorpusDonation.Write(folder, from, now, standIns, writer, logger, progress, cancel);
                },
                cancel);

        // The sends are null where there is nowhere to send, which is what makes each send button appear only
        // when it can work — the same rule the button itself follows.
        await new Controls.HelpImproveWindow(
            now,
            build,
            dispatch.CanSend
                ? (text, cancel) => dispatch.SendExcerptAsync(text, paperwork, cancel)
                : null,
            dispatch.Destination,
            read,
            write,
            dispatch.CanSend
                ? (report, progress, cancel) =>
                    dispatch.SendCorpusAsync(report, write, paperwork, progress, cancel)
                : null,

            // The way back out, on the page that sent it (#295).
            async cancel =>
            {
                var forgotten = await dispatch.ForgetAsync(cancel);

                return forgotten.Receipt is { } receipt
                    ? $"{forgotten.Outcome.Said} A record of it is in {receipt}."
                    : forgotten.Outcome.Said;
            }).Over(this);
    }

    private async Task CheckForUpdateAsync(AppHost host)
    {
        // Asked alongside the update check because it is the same trip and the same failure policy: anything
        // that goes wrong leaves the channel Unknown and shows no marker (#92).
        await ShowReleaseChannelAsync(host);

        var update = await host.Updates.CheckAsync(host.Version, CancellationToken.None);
        if (update is null)
        {
            return;
        }

        _availableUpdate = update;
        _model.UpdateText = $"D47 {update.Version} is available — you're on {host.Version}.";
    }

    /// <summary>Puts the pre-release mark in the three places it belongs, or leaves them bare (#92).</summary>
    private async Task ShowReleaseChannelAsync(AppHost host)
    {
        // A build from a working tree is answered from the binary and GitHub is not asked, because the answer
        // it would give is true about a different one: a local build's version compares equal to the release
        // it was cut from, so 0.84.3-local came up wearing 0.84.3's pre-release badge and claimed to be a
        // published build it was not.
        host.Channel = BuildInfo.IsLocal
            ? ReleaseChannel.Local
            : await host.Updates.ChannelAsync(host.Version, CancellationToken.None);

        Title = $"Directive 47 — {ReleaseChannelText.Marked(BuildInfo.Semantic, host.Channel)}";

        Panel?.ShowChannel(host.Channel);
    }

    /// <summary>
    /// Downloads the new build, verifies it, puts it where this one is and starts it (Phase 19: "the
    /// user is given an opportunity to exit, install it, and restart").
    /// </summary>
    private async void OnUpdateAccepted()
    {
        if (_availableUpdate is not { } update || _host is null)
        {
            return;
        }

        if (!update.CanInstall)
        {
            OpenReleasePage(update, "This release has no installable build attached.");
            return;
        }

        _model.UpdateBusy = true;
        _model.UpdateText = $"Downloading D47 {update.Version}…";

        var progress = new Progress<double>(fraction =>
            _model.UpdateText = $"Downloading D47 {update.Version} — {fraction:P0}");

        var (payload, failure) = await _host.Installer
            .DownloadAsync(update, progress, CancellationToken.None);

        if (payload is null)
        {
            _model.UpdateBusy = false;
            OpenReleasePage(update, Explain(failure));
            return;
        }

        _model.UpdateText = $"Installing D47 {update.Version}…";

        if (Environment.ProcessPath is not { } running
            || !_host.Installer.TrySwap(running, payload))
        {
            _model.UpdateBusy = false;
            OpenReleasePage(update, Explain(UpdateFailure.CouldNotReplace));
            return;
        }

        // The successor starts before this one has exited, so the slot has to be handed over first or it
        // would find d47 "already running" and close itself — an accepted update that looks like the app
        // simply quitting.
        _host.StoppingBecause = "an accepted update is replacing this build";
        _host.ReleaseSingleInstance?.Invoke();

        // Started before this one exits, so the Commander sees d47 come back rather than watching it vanish
        // and having to find it again.
        Process.Start(new ProcessStartInfo(running) { UseShellExecute = true });
        Close();
    }

    /// <summary>Offers a Start Menu entry, once, on the first run that does not already have one.</summary>
    private async Task OfferStartMenuEntryAsync()
    {
        if (_host is null)
        {
            return;
        }

        // Read fresh and written back immediately, never cached: WindowPlacementMemory writes the same file,
        // and two holders of a stale copy is how one of them loses its changes.
        if (_host.ViewState.Load().StartMenuOffered)
        {
            return;
        }

        // Nothing to offer if there is one already — a Commander who made one by hand should never see this.
        if (StartMenuShortcut.Exists() || Environment.ProcessPath is not { } executable)
        {
            MarkOffered();
            return;
        }

        var wanted = await new ConfirmWindow(
            "Add to the Start Menu?",
            $"D47 runs from {executable}. A Start Menu entry means "
            + "you can find it by name instead of by remembering where you put it. It is one "
            + "shortcut, for you only, and you can delete it like any other.",
            confirmLabel: "Add it",
            declineLabel: "No thanks").AskAsync(this);

        // Recorded before acting, so failing to write the shortcut does not turn into the question coming
        // back every launch.
        MarkOffered();

        if (!wanted)
        {
            return;
        }

        if (!StartMenuShortcut.TryCreate(
                StartMenuShortcut.DefaultPath,
                executable,
                _host.Loggers.CreateLogger<MainWindow>()))
        {
            _model.Append(
                "I could not add the Start Menu entry. You can still run D47 from where it is.");
        }
    }

    private void MarkOffered()
    {
        if (_host is not null)
        {
            _host.ViewState.Save(_host.ViewState.Load() with { StartMenuOffered = true });
        }
    }

    private void OpenReleasePage(AvailableUpdate update, string reason)
    {
        _model.UpdateText = $"{reason} Opening the release page.";

        Process.Start(new ProcessStartInfo(update.ReleaseUrl) { UseShellExecute = true });
    }

    private static string Explain(UpdateFailure? failure) => failure switch
    {
        UpdateFailure.ChecksumMismatch =>
            "The download did not match the checksum published with it, so D47 did not run it.",
        UpdateFailure.BadArchive =>
            "The download was not a D47 build, so D47 did not install it.",
        UpdateFailure.CouldNotReplace =>
            "D47 could not replace itself where it is installed.",
        UpdateFailure.NothingToInstall =>
            "This release has no installable build attached.",
        _ => "The download did not finish.",
    };
}
