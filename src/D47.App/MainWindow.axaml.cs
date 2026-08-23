using System.Diagnostics;
using System.Text;
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
using D47.Core.Listening;
using D47.Core.Audio;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.App;

/// <summary>
/// The desktop host for <see cref="PanelView"/>. What is left here is what genuinely belongs
/// to a window: gestures scoped to it, dialogs parented by it, a hotkey registration that
/// needs its handle, and navigating away from it.
/// <para>
/// Everything the panel shows and does moved to <see cref="PanelViewModel"/>, which the VR
/// overlay binds a second instantiation of the same view to (list.md Phase 9, "TheApp's panel
/// works in VR"). The split is what makes the windowed surface unable to be more functional
/// than the headset one — not a rule anybody has to remember, just where the code is.
/// </para>
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppHost? _host;
    private readonly GlobalHotkey _shutUp;
    private readonly GlobalHotkey _reanchor;
    private readonly GlobalHotkey _bindShipCore;
    private readonly PanelViewModel _model;

    private AvailableUpdate? _availableUpdate;
    private bool _turnInFlight;

    /// <summary>
    /// Whether the input waiting in the ask box got there by being spoken. Set by the
    /// transcriber's handler and cleared as the turn starts, because it describes one input
    /// rather than the window.
    /// </summary>
    private bool _spoken;

    public MainWindow() : this(host: null)
    {
    }

    public MainWindow(AppHost? host)
    {
        _host = host;

        // The host's model, not one of this window's own. The headset overlay binds a second
        // instantiation of the same view to it, and a model owned by the window would make
        // the overlay a guest of a surface that can be closed.
        _model = host?.Panel ?? new PanelViewModel();
        var hotkeyLogger = host?.Loggers.CreateLogger<GlobalHotkey>()
                           ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GlobalHotkey>.Instance;

        // Why the log will say d47 stopped (remediation.md 10, item 7). Closing this window is
        // how d47 is quit -- there is no tray icon -- so anything that reaches Dispose without
        // passing through here was not the Commander's doing, and the default says so rather
        // than guessing which of the several other reasons it was.
        if (host is not null)
        {
            Closing += (_, _) => host.StoppingBecause = "the window was closed";
        }

        _shutUp = new GlobalHotkey(hotkeyLogger);
        _reanchor = new GlobalHotkey(hotkeyLogger);
        _bindShipCore = new GlobalHotkey(hotkeyLogger);

        InitializeComponent();

        // The push-to-talk key must never reach a control in this window, and these tunnel so
        // they run before the focused control rather than after it — a bubbling handler is too
        // late, because the text box has already inserted the character by then.
        //
        // The key is polled, not hooked (architecture.md D4), so d47 does not consume it
        // system-wide and Windows keeps delivering it to whatever has focus. Hold a
        // push-to-talk bound to a printable key with the caret in the Ask box and the box fills
        // with that character on auto-repeat. Suppressing it here is the trade the binding
        // already implies: a key given to push-to-talk stops being a key that types inside
        // d47's own panel. It is unaffected everywhere else.
        //
        // Settings is a page of this window now rather than a window of its own, so this runs
        // over the hotkey binder too — which is why the binder listens with handledEventsToo,
        // or rebinding push-to-talk to the key push-to-talk already holds would be the one
        // rebind it could not make.
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnTunnelKeyUp, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTunnelTextInput, RoutingStrategies.Tunnel);

        if (host is not null)
        {
            PushToTalkGesture = () => host.Settings.Current.Listening.PushToTalkKey;
        }

        // The version lives in the chrome that is on screen anyway, and it is set here rather
        // than on load because it does not depend on the host - a window with no version in its
        // title, however briefly, is a window that cannot answer the one question a title bar is
        // good at. Short form only: the commit hash is in the About dialog, where forty
        // characters can be read and copied rather than merely occupying the chrome.
        Title = $"Directive 47 — {BuildInfo.Semantic}";

        Panel.DataContext = _model;

        // The Commander's own frames, where they have supplied them. Handed to the view rather
        // than looked up by it: the view is instantiated by the headset overlay too, and a
        // control that read the data folder for itself would be a control that knows where it
        // is installed.
        Panel.Avatar.Library = host?.Avatars;
        _model.AskRequested += () => _ = AskAsync();

        // The desktop window is the one surface with a browser to open the site in; the headset
        // copy is not handed this and so shows no help button (change-requests.md 24).
        // Any address a help page names, not just the site root: a band's "where to go next" is
        // drawn as a button here and as plain text in the headset, which has no browser to hand.
        Panel.EnableHelp(url => Process.Start(
            new ProcessStartInfo(url) { UseShellExecute = true }));
        _model.UpdateAccepted += OnUpdateAccepted;
        _model.UpdateDismissed += () => _model.UpdateText = null;

        if (host is not null)
        {
            // The log page reads through this rather than knowing a path, so the view model
            // stays free of a disk and a test can hand it a string.
            _model.LogSource = () => Logging.LogTail.Read(host.Paths.Logs);

            // The window that can show settings says so; the headset's copy of this same view
            // is handed nothing and therefore has no Settings tab (list.md Phase 12).
            Panel.EnableSettings(BuildSettingsPage);

            // The checklist, on the other hand, goes to both surfaces — which is the whole
            // headline of the item that moved it out of a Window. A Window cannot appear in the
            // headset, so a Commander in VR could not see their checklist at all (list.md
            // Phase 25).
            Panel.EnableChecklist(host.Checklists, host.Goals?.Book, host.Goals?.Backfill);

            // The stories the Commander flies (list.md Phase 47). **Both surfaces from
            // 2026-08-22**, on the Commander's instruction: the tab was desktop-only on the
            // reasoning that the editor and the ask form want a keyboard, and that was the wrong
            // half to weigh — a Commander wearing a headset is exactly the one who has just
            // arrived somewhere and wants to know what the story made of it, and the prompts have
            // taken a spoken value since Phase 25. Kept rather than rebuilt for the headset: the
            // record holds delegates and no visual, so one instance serves both trees, and two of
            // them would be two lists of what an adventure surface needs wired to it.
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

            // The fleet and its builds, what the Commander is wearing, and the arithmetic
            // between them (list.md Phases 26 and 27). This window only: withdrawn from the
            // headset during the panel redesign and left there when the checklist went back
            // (list.md Phase 39), because a three-level drill ending in a search field is a
            // bigger surface than one list of short rows.
            Panel.EnableLoadout(
                host.Ships,
                host.Checklists,
                () => host.GameState.Active,
                host.OnFootPlans,
                () => host.ModulePower);

            // Who to go and unlock next, read across both plan stores (list.md Phase 28). Both
            // surfaces again, because a Commander deciding where to fly is usually already in
            // the ship.
            Panel.EnableEngineers(
                host.Unlocks, host.Ships, () => host.GameState.Active, host.OnFootPlans);

            // Where the Commander is going, in three readings of one journey (list.md Phase 37).
            // This window only: the plan forms want a keyboard the headset has not got. If VR
            // ever gets this tab it gets Progress and nothing else, which is a different set of
            // flags on this same call rather than a second page.
            Panel.EnableRouting(new RoutingSurface(
                () => host.Route,
                () => host.GameState.Active?.Location.StarSystem,
                host.Capabilities,
                host.Plans,
                () => host.Settings.Current.Knowledge.GalaxySearch,
                OpenSettings));

            // And the clocks, timers and alarms (list.md Phase 24). Both surfaces, like the
            // checklist: a Commander in a headset is exactly the Commander who cannot glance at
            // a wall clock.
            Panel.EnableUtilities(
                host.Timekeeper,
                host.Alarms,
                () => D47.Core.SystemWallClock.Instance.UtcNow,
                () => TimeZoneInfo.Local);

            // And the same window is the one with a keyboard, so it is the one that gets a
            // search box. Two calls rather than one, because they are two affordances — but they
            // are made from the same line of the same file, which is where "desktop only" lives.
            Panel.EnableSearch();

            // And the same window is the one with somewhere to open a dialog, which is what the
            // turn line's figures need. The headset's copy is handed nothing.
            Panel.EnableTurnDetails(() => _ = ShowSpendAsync());

            // A value being said rather than typed reaches this surface's open prompt, if it has
            // one (list.md Phase 25). Registered rather than assigned: the headset's copy of this
            // panel registers its own, and either can be the one asking.
            host.RoutePrompts(heard =>
            {
                if (!Panel.Prompts.IsListening)
                {
                    return false;
                }

                // Onto the thread that owns the controls it is about to write into. Speech
                // arrives from the transcriber's own task, which is not this one.
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Panel.Prompts.Hear(heard));
                return true;
            });

            // And a spoken "show me the checklist" moves this surface (list.md Phase 25), as does
            // a switch (list.md Phase 46) — which arrives from the tick thread, so it is given the
            // dispatcher captured here rather than left to read the static one from a worker.
            var ui = Avalonia.Threading.Dispatcher.UIThread;
            host.RouteNavigation(Panel.Nav, move => ui.Post(move));

            // A clock is the one page whose content changes with nothing having happened, so it
            // is pushed rather than pulled (list.md Phase 24). Posted, because the tick loop runs
            // on its own thread and every control here belongs to this one; and it does nothing
            // at all until the tab has been opened once.
            host.Tick.Add("clocks", _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(Panel.TickClocks));

            // The engineer pages move for a different reason: nothing has to happen for a clock
            // to change, and everything has to happen for a ranking to. So this one asks whether
            // the Commander has moved, re-fitted or unlocked somebody, and redraws only then
            // (list.md Phase 28).
            host.Tick.Add("engineers", _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(Panel.TickEngineers));

            // And the "d47 is composing" animation on the Adventures tab, by the same route again
            // (asked for 2026-08-22). It is a third reason a page moves with nothing having
            // happened: a beat has fired and the line for it is still being written.
            host.Tick.Add("adventures", _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Panel.TickAdventures()));

            // And the ship pages, for the same reason and by the same route (remediation.md 17,
            // item 7). Half of what a ship page shows is the journal's — what is fitted, which
            // slots can be seen, whether this is the ship being flown — and none of it moved the
            // page, so one left open across a ship swap kept its first answer all session.
            //
            // Desktop only, deliberately: the Loadout tab is not furnished in the headset.
            host.Tick.Add("loadout", _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(Panel.TickLoadout));

            // And the route being flown, by the same route again (list.md Phase 37). A jump
            // rewrites NavRoute.json and moves the Commander along it, and neither of those is
            // something the page can notice by itself.
            //
            // Desktop only, like the loadout: the Routing tab is not furnished in the headset.
            host.Tick.Add("routing", _ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(Panel.TickRouting));

            // Both before the window is shown. Sizing after the fact is a visible resize, and
            // wrapping the content after the first layout pass is a visible reflow.
            WindowPlacementMemory.Attach(this, host.ViewState);

            // Read before the first paint for the same reason: the worked example appearing and
            // then vanishing is worse than either state, and it is the Commander who has already
            // asked — the one who does not need it — who would see it happen.
            _model.HasAsked = host.ViewState.Load().HasAsked;

            // One zoom host, on the one window. The settings surface is inside this widget tree
            // now, so it scales with everything else rather than needing a host of its own.
            ZoomHost.Attach(this, host.Settings);
        }
    }

    /// <summary>
    /// What the panel is showing. Exposed so the VR overlay binds its own instantiation of
    /// <see cref="PanelView"/> to the same one rather than building a second.
    /// </summary>
    public PanelViewModel Model => _model;

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (_host is null)
        {
            _model.Append("No host: the window is running under the designer.", TranscriptKind.Technical);
            return;
        }

        var errors = new List<string>();
        if (_host.StartupError is { } startupError)
        {
            errors.Add(startupError);
        }

        // The Phase 1 claim is that a request produces a real tool call that runs and returns a
        // result. This is that call, dispatched by name through the registry.
        var status = await _host.Capabilities.InvokeAsync("get_app_status", ToolArguments.Empty);
        _model.Append(status.Content, TranscriptKind.Technical);

        if (status.IsError)
        {
            errors.Add(status.Content);
        }

        // Say plainly whether the model is available. Silence here is indistinguishable from a
        // model with nothing to say, and the keyword router still answers either way.
        var availability = _host.LlmAvailability;
        _model.Append(
            availability.Current == LlmAvailability.Available
                ? "\nLanguage model: ready."
                : $"\nLanguage model: unavailable. {availability.Reason} " +
                  "Keyword commands still work — try \"where am I\" or \"status\".",
            TranscriptKind.Technical);

        if (errors.Count > 0)
        {
            _model.ErrorText = string.Join(Environment.NewLine, errors);
        }

        // A voice companion that cannot speak has to say so. This event was raised and nobody
        // was listening, so a provider rejecting every sentence — a voice id belonging to the
        // provider selected before this one, say — looked exactly like d47 having nothing to
        // say. The cues still played, which made it read as a deliberate silence.
        //
        // Posted, because synthesis fails on whichever thread was doing the synthesising.
        _host.Voice.SynthesisFailed += reason => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _model.ErrorText = reason);

        DescribeHotkeys();
        BindShutUp();
        BindReanchor();
        BindShipCore();

        // Spoken input runs the same turn as typed input, deliberately. A second path would be
        // a second place for the in-flight gate, the interrupt vocabulary and the cancellation
        // slot to be got wrong, and the Commander expects "where am I" to mean the same thing
        // whichever way they said it (list.md Phase 6).
        _host.Heard += text => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Spoken and typed run the same turn - the Commander expects "where am I" to mean
            // the same thing either way - but the router is told which it was, because a couple
            // of phrases only mean what they say when they arrived through a microphone.
            _spoken = true;
            _model.AskText = text;
            _ = AskAsync();
        });

        // Anything d47 says without a turn behind it still belongs in the transcript, so what
        // was heard and what can be read back are the same set.
        _host.Said += text => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _model.Append($"\n{text}\n"));

        // And what happened to the conversation rather than in it - the core changing under it.
        // Marked rather than appended, so it reads as the panel and not as whoever is aboard.
        _host.Noted += text => Avalonia.Threading.Dispatcher.UIThread.Post(() => _model.Mark(text));

        // In-game comms. On the Technical page rather than the conversation, because a station
        // and a police interceptor are not talking to the Commander's companion - and because on
        // a station approach there are a lot of them, and the conversation is the one page that
        // has to stay readable.
        _host.Transcribed += text => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _model.Append(text, TranscriptKind.Technical));


        _host.Settings.Changed += change => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            DescribeHotkeys();

            if (change.Key == SpeechCapability.ShutUpHotkeyKey)
            {
                BindShutUp();
            }

            if (change.Key == InterfaceCapability.ReanchorHotkeyKey)
            {
                BindReanchor();
            }

            if (change.Key == InterfaceCapability.BindShipCoreHotkeyKey)
            {
                BindShipCore();
            }
        });

        // Deliberately not focusing the Ask box. A text field with the caret in it is a trap
        // for a voice-first application: push-to-talk is a polled key that d47 does not consume,
        // so holding it types into whatever has focus, and a Commander who bound "[" and held it
        // got a line of "[[[[[[[[" instead of a transcript. Nothing is focused until the
        // Commander asks for it — by clicking, tabbing, or the focus-ask hotkey.

        // Said aloud as well as shown, because a misconfigured provider otherwise presents as
        // silence, and silence is indistinguishable from a model with nothing to say
        // (list.md Phase 5). Not awaited: it must never delay the panel.
        _ = _host.AnnounceStartupProblemsAsync();

        // Optional in two senses: it must never delay the status the Commander is here for, and
        // it is the one network call d47 makes on its own — so it is a setting, and it is
        // disclosed (list.md Phase 4, "Say what each provider receives").
        if (_host.Settings.Current.Updates.CheckOnStartup)
        {
            _ = CheckForUpdateAsync(_host);
        }

        // Before the Start Menu offer, because it is the one that decides whether d47 can answer
        // at all — and a Commander who has just been asked about a shortcut has already formed a
        // view about how much this app asks of them.
        await OfferKeysAsync();

        // Last, and awaited rather than fired and forgotten: it is modal, so it must not appear
        // over a panel that is still assembling itself. Returns immediately on every run after
        // the first.
        await OfferStartMenuEntryAsync();

    }

    /// <summary>
    /// The guided key setup, shown when there is no usable language-model key (list.md Phase 16).
    /// <para>
    /// <b>Driven by state, never by a flag.</b> There is nothing recorded about having shown
    /// this, which is the point: a Commander who restored a <c>data\</c> folder onto a new
    /// machine has a <c>secrets.json</c> that DPAPI cannot decrypt, <see cref="SecretStore"/>
    /// reports those values absent, and this offers to fix exactly that. A "have we done this?"
    /// flag would have shown it once on the machine that could read its secrets and never again
    /// on the one that could not.
    /// </para>
    /// </summary>
    private async Task OfferKeysAsync()
    {
        if (_host is not { } host)
        {
            return;
        }

        // The one gate, and it is on the *offer* rather than on the window. Reopening from About
        // deliberately skips it: keys get rotated and revoked, and a Commander who came looking
        // for the key screen should find it rather than be told they do not need it.
        if (!FirstRun.IsNeeded(
                LlmProviderCatalog.Selected(host.Settings.Current.Llm.Provider),
                host.Secrets.Has))
        {
            return;
        }

        await ShowKeySetupAsync();
    }

    /// <summary>
    /// The guided key setup, shown because it was asked for. Ungated, so a fully configured
    /// install sees its rows with their keys already stored and a Check button beside each —
    /// which is what somebody arriving here from About came to use.
    /// </summary>
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

            // The voice key, offered because a companion that talks back is most of the point —
            // and offered second, because one that does not is still a companion. Inara's row
            // joins this list in the phase that adds it, beside its own row rather than here.
            [SpeechCapability.KeyRowFor(TtsProviderCatalog.ElevenLabs)]);

        if (steps.Count == 0)
        {
            return;
        }

        await new FirstRunWindow(steps, host.Settings).Over(this);
    }

    /// <summary>
    /// Window-scoped gestures, matched against the bound settings. Protection matters here: a
    /// hotkey is one of the callers allowed to reach a protected row, which is exactly why the
    /// rows holding these gestures are themselves protected (architecture.md §7).
    /// </summary>
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
        }

        // Escape leaves the settings page for the one it covered up. Answered here rather than
        // by the page itself, because this is the level that knows there is nothing else for an
        // unhandled Escape to close now that settings is not a window — and it runs last, so
        // anything with a better claim on the key (a picker, a search box with a query in it)
        // has already taken it.
        if (e.Key == Key.Escape && !e.Handled && Panel.GoBack())
        {
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// Whether the push-to-talk key is down right now, as seen by this window rather than by
    /// the ten-times-a-second poll. Text input carries no key, so suppressing the character
    /// needs this rather than <c>PushToTalkKey.IsDown</c>, which can lag a keystroke.
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
            // Cleared even when the key is not matched as handled below, so a rebind while the
            // key is held cannot leave text input suppressed for the rest of the session.
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

    /// <summary>
    /// The bound push-to-talk gesture. A function rather than a read of the host so the
    /// suppression can be driven in a headless test, which has no host to bind a key on.
    /// </summary>
    internal Func<string?> PushToTalkGesture { get; set; } = () => null;

    private bool IsPushToTalk(KeyEventArgs e) => Matches(PushToTalkGesture(), e);

    /// <summary>
    /// Gestures are stored in the form <see cref="KeyGesture"/> writes, so an unparseable one is
    /// a gesture that never matches rather than an exception on every keystroke.
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
            // A hand-edited settings file can hold anything. An unbound action is a better
            // outcome than an exception on every keypress.
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

        // Read from settings rather than hardcoded, so rebinding the gesture updates the tip
        // instead of leaving a "Ctrl+," that quietly became a lie.
        ToolTip.SetTip(
            Panel.SettingsAffordance,
            open is null ? "Settings" : $"Settings ({Gestures.Describe(open)})");
    }

    private void OpenSettings() => Panel.Tab = PanelTab.Settings;

    /// <summary>
    /// The settings surface, built the first time the tab is selected.
    /// <para>
    /// A <see cref="UserControl"/> handed to the panel rather than a window shown over it. Every
    /// dialog it opens takes its owner from <c>TopLevel.GetTopLevel(this)</c>, so About, the
    /// picker, the macro editor and the confirm all re-parent onto this window without knowing
    /// anything changed.
    /// </para>
    /// </summary>
    /// <summary>
    /// Builds a settings page, wired to this app's host. Public because the headset's copy of the
    /// panel needs one too, and it must be the same builder: two of them would be two lists of
    /// which stores, recorders and callbacks a settings surface needs, and the headset's would be
    /// the one nobody noticed had fallen behind (remediation.md, "The VR big panel should carry
    /// the Settings tab").
    /// <para>
    /// A new instance per call, which is required rather than incidental — a <c>Visual</c> belongs
    /// to exactly one visual tree, so the window and the quad cannot share one.
    /// </para>
    /// </summary>
    /// <summary>
    /// What the Adventures tab reads, built once for both surfaces (list.md Phase 47, amended
    /// 2026-08-22). Null where the host has no adventures — the designer, and a test that is not
    /// about them. The headset is handed this same instance; it carries delegates and no visual,
    /// which is exactly what makes that safe.
    /// </summary>
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

                // The choice is the go-ahead: it states its size in the list it was made from,
                // and the row shows what it is doing while it does it.
                (model, progress) => _host.InstallModelAsync(model, progress),

                // About's way back in. The window rather than the offer, because the offer is
                // gated on there being no usable key and this path is for the Commander who came
                // looking — a rotated key, a revoked one, or just checking the one they have.
                ShowKeySetupAsync,

                // The Commander's own notes, and the search that decides how one is filed. The
                // host owns both halves of "can this search at all", which is the thing the
                // window has to say before anything is typed (list.md Phase 23).
                _host.LoreEditing,
                _host.Memories,

                // And what it has noticed about them, with the way to look again (list.md Phase
                // 32). The action is the host's, because mining runs off the UI thread and the
                // panel is not the thing that decides where work happens.
                _host.Habits,

                // And the log those journals can be turned into (list.md Phase 33). The book
                // rather than an action, because writing one is two acts and the window is what
                // holds the figure between them.
                _host.Logbook,

                // And the cores the Commander wrote themselves (remediation.md 11, item 9).
                _host.OwnPersonas);

            // The gap reaction happens in the host, on whatever thread resolved the switch, and
            // the affordance it belongs to is a row on this surface. Joined here because this is
            // the one place that holds both.
            _host.PersonaSettling += settling => Avalonia.Threading.Dispatcher.UIThread.Post(
                () => view.ShowBusy(PersonaCapability.PersonaKey, settling));

            // A file dropped into data/audio rebuilds the cue library without any setting having
            // changed, so the row that says what was found has no other way to know. Posted
            // because the rescan runs on the tick thread (list.md Phase 12).
            _host.AudioReloaded += () => Avalonia.Threading.Dispatcher.UIThread.Post(view.Refresh);
        }

        return view;
    }

    /// <summary>
    /// Remembers that this Commander has asked something, once and for good.
    /// <para>
    /// Written rather than worked out later. The signal that would let this be derived — a
    /// conversation with anything in it — does not survive a restart, so a launch with nothing
    /// asked yet and a launch by someone who has been flying for a month look identical from
    /// live state. That is why this differs from <c>FirstRun</c>, which records nothing and
    /// decides from what it can see each time.
    /// </para>
    /// <para>
    /// Guarded on the flag it sets, so the common case — every ask after the first — is a
    /// boolean and not a file write.
    /// </para>
    /// </summary>
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

        // Taken once, whether or not this turn goes ahead, so a discarded input cannot leave
        // the next typed one looking spoken.
        var source = _spoken ? InputSource.Spoken : InputSource.Typed;
        _spoken = false;

        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        // Recorded here rather than in either modality's own path, because this is where typed
        // and spoken meet — and the hint retires on "has asked at all", not on "has used this
        // control". A Commander who has only ever spoken to d47 has still been taught.
        MarkAsked();

        // Asked before the in-flight gate, never after. A silence command is only ever wanted
        // while d47 is mid-sentence, which is exactly when _turnInFlight is true — so gating it
        // on that would drop it at the one moment it matters (list.md Phase 5, "never gated
        // behind a turn completing"). The registry decides what may interrupt; this does not.
        //
        // Only consulted when there is actually something to interrupt, which is what lets the
        // vocabulary include a bare "stop". Idle, "stop" is the opening word of "stop the ship"
        // and belongs to whatever answers that; mid-sentence it has one meaning. Context is the
        // disambiguator, so context is the gate.
        // Moving the panel by saying so (list.md Phase 25). Before the in-flight gate for the same
        // reason the silence command is: navigating is a thing about the surface rather than about
        // the conversation, and a Commander who wants their checklist while d47 is mid-sentence
        // should get it. Deterministic, provider-free, and never a tool — nothing an in-game
        // message says gets to move the Commander's panel.
        if (_host.Navigate(input) is { } moved)
        {
            _model.AskText = string.Empty;
            _model.Append($"\n\n> {input}\n{moved}\n", TranscriptKind.Technical);
            return;
        }

        if ((_turnInFlight || _host.Audio.IsSpeaking)
            && _host.Router.MatchInterrupting(input) is { } interrupting)
        {
            _model.AskText = string.Empty;
            var stopped = await _host.Capabilities.InvokeAsync(interrupting.ToolName, ToolArguments.Empty);

            // Two writes rather than one interpolation, because they are two voices. The flat
            // pages put them back together exactly as they were.
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

        // Kept before the crew scope rewrites `input` below: the adventure feed files an exchange
        // under the Commander's own words, not under the question as it reached a crew member.
        var asked = input;

        // Addressed to somebody in the fighter bay rather than to the ship's AI? The scope swaps
        // the prompt block and the voice and puts them back in its Dispose, so a crew turn
        // cannot leak the wrong persona into the next one (list.md Phase 11, "Ship Crew").
        using var crew = _host.BeginCrewTurn(input);

        if (crew is not null)
        {
            input = crew.Question;
            _model.Append($"[{crew.Member.Name}] ");
        }

        // Claimed before the turn starts and released in the finally. Without this the token
        // reaching the provider is CancellationToken.None, "cancel" has nothing to act on, and
        // a runaway turn keeps generating — and billing — with no way to call it off.
        var cancelling = _host.Cancellation.Begin();

        try
        {
            // Through the voice pipeline rather than straight off the turn loop, so the panel
            // and the speaker are fed from one traversal of one stream. Rendering as it arrives
            // is what lets speech start at the first sentence boundary rather than at end of
            // turn (list.md Phase 5).
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

                            // And onto the story's own feed, if it was about one (asked for
                            // 2026-08-22). `asked` rather than `input`: a crew turn rewrites the
                            // latter, and what the Commander said is what the heuristic reads.
                            _host.NoteTurn(asked, completed.Result.Text);
                            break;
                    }
                });
        }
        catch (Exception ex)
        {
            // A turn that throws is a bug, not a provider failure — provider failures arrive as
            // events. Surface it rather than losing it.
            //
            // Logged as well as shown, and with the stack trace. The panel gets one line of
            // message, and the logs are the first thing read on a bug report — a cross-thread
            // failure here left the log with nothing in it at all, not even at Information,
            // which made a reproducible crash look like it had happened nowhere.
            _host?.Loggers.CreateLogger<MainWindow>().LogError(ex, "The turn threw");

            // One voice. The conversation gets a sentence in the same register as everything
            // else D47 says, and the part that is only useful to somebody debugging goes to the
            // page for that — a bracketed exception message is not a reply to anybody.
            _model.Append("\nI couldn't answer that. The details are on the Technical page.");
            _model.Append($"\n[turn failed: {ex.Message}]", TranscriptKind.Technical);
        }
        finally
        {
            _turnInFlight = false;
            _model.CanAsk = true;

            // Focus follows the way the turn was started. Typing another question after typing
            // one is the obvious next move; after speaking, putting the caret in a text box is
            // actively harmful, because the next thing the Commander does is hold the
            // push-to-talk key and that key would land in the box.
            if (source == InputSource.Typed)
            {
                Panel.FocusAsk();
            }
        }
    }

    /// <summary>
    /// Asks the Commander whether to download a speech model, and downloads it if they say yes.
    /// <para>
    /// The question is asked only after d47 has asked the host how big the file actually is, so
    /// what the Commander agrees to is a real number and a named host rather than an estimate.
    /// Declining leaves the setting alone: they may want the model later, and silently reverting
    /// their choice would be answering for them.
    /// </para>
    /// </summary>
    /// <summary>
    /// Registers the system-wide silence key.
    /// <para>
    /// Deferred to here rather than done in <see cref="AppHost"/> because a registration needs a
    /// window handle, and the handle does not exist until the window does. The key itself is not
    /// scoped to that window — that is the entire point of it (list.md Phase 5, "Shut up").
    /// </para>
    /// </summary>
    private void BindShutUp()
    {
        if (_host is null)
        {
            return;
        }

        var gesture = _host.Settings.Current.Speech.ShutUpHotkey;

        if (!_shutUp.Bind(gesture, _host.Audio.Silence) && !string.IsNullOrWhiteSpace(gesture))
        {
            // Reported rather than swallowed: the symptom of a failed registration is a key
            // that does nothing, which reads as d47 ignoring the Commander.
            _model.ErrorText =
                $"The silence hotkey {Gestures.Describe(gesture)} could not be registered system-wide. " +
                "Another application is probably holding it — pick another in Settings.";
        }
    }

    /// <summary>
    /// Registers the system-wide re-anchor key (list.md Phase 9, "Re-anchor the panels").
    /// <para>
    /// System-wide for the same reason the silence key is, and more so: the case it exists for
    /// is Elite holding the foreground with the panels drifted somewhere the Commander cannot
    /// aim at. A gesture that needs d47 focused is a gesture that does not work when it is
    /// wanted, and reaching for the panel is exactly what a drifted panel prevents.
    /// </para>
    /// </summary>
    private void BindReanchor()
    {
        if (_host is null)
        {
            return;
        }

        var gesture = _host.Settings.Current.Hotkeys.Reanchor;

        if (!_reanchor.Bind(gesture, () => _host.Vr?.Reanchor()) && !string.IsNullOrWhiteSpace(gesture))
        {
            _model.ErrorText =
                $"The re-anchor hotkey {Gestures.Describe(gesture)} could not be registered system-wide. " +
                "Another application is probably holding it — pick another in Settings.";
        }
    }

    /// <summary>
    /// The gesture that binds the core aboard to the ship the Commander is in (list.md Phase 35).
    /// <para>
    /// System-wide, because the moment it is wanted is a moment they are sitting in the ship with
    /// Elite in front of them. Pressing it again with the same core already bound takes it back,
    /// which is the only reading of a second press that means anything.
    /// </para>
    /// <para>
    /// The answer is spoken rather than only shown, for the reason every act performed while the
    /// game has the foreground is: the Commander pressing this is not looking at d47's window,
    /// and a confirmation they cannot see is a press they will make twice.
    /// </para>
    /// </summary>
    private void BindShipCore()
    {
        if (_host is null)
        {
            return;
        }

        var gesture = _host.Settings.Current.Hotkeys.BindShipCore;

        if (!_bindShipCore.Bind(gesture, Toggle) && !string.IsNullOrWhiteSpace(gesture))
        {
            _model.ErrorText =
                $"The ship-core hotkey {Gestures.Describe(gesture)} could not be registered system-wide. " +
                "Another application is probably holding it — pick another in Settings.";
        }

        void Toggle()
        {
            var aboard = _host.Personas.Current.Id;

            var said = _host.ShipCores.Current is { } bound
                       && string.Equals(bound.Core, aboard, StringComparison.Ordinal)
                ? _host.ShipCores.Forget()
                : _host.ShipCores.Bind(aboard);

            _model.Mark(said);
            _ = _host.Voice.AnnounceAsync(said);
        }
    }

    /// <summary>
    /// One short line of provenance, with the figures behind a link
    /// (docs/plans/change-requests.md item 2).
    /// <para>
    /// This used to carry outcome, route, effort, three token counts, the turn's cost, the
    /// session's cost, a cold-prefix counter, a character count and a voice price — eleven
    /// numbers on one row beside a running game, which is a wall rather than a status.
    /// </para>
    /// <para>
    /// What stays is what a glance is actually asking: did that work, which path answered, and
    /// what did it cost. Everything else moved to <see cref="SpendWindow"/>, including all of
    /// the numbers that are only interesting when something is wrong — a cold-prefix count is
    /// worth chasing and worth nothing on a row nobody reads.
    /// </para>
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

    /// <summary>
    /// Opens the figures. Here rather than in the panel because the panel opens no dialogs —
    /// one view definition serves the desktop window and the headset, and only one of them has
    /// somewhere to put a window.
    /// <para>
    /// The zone is the machine's own, read at the moment of asking. "This week" and "this month"
    /// are local-calendar ideas and a Commander who has flown to another timezone means them
    /// there, not where the rows happened to be written.
    /// </para>
    /// </summary>
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
            TimeZoneInfo.Local).Over(this);
    }

    private async Task CheckForUpdateAsync(AppHost host)
    {
        var update = await host.Updates.CheckAsync(host.Version, CancellationToken.None);
        if (update is null)
        {
            return;
        }

        _availableUpdate = update;
        _model.UpdateText = $"D47 {update.Version} is available — you're on {host.Version}.";
    }

    /// <summary>
    /// Downloads the new build, verifies it, puts it where this one is and starts it
    /// (list.md Phase 19: "the user is given an opportunity to exit, install it, and restart").
    /// <para>
    /// Every failure ends at the release page rather than at a dead end — the Commander asked to
    /// be updated, and the browser is the path that always works. The reason is said out loud
    /// first, because "it opened a web page" is otherwise indistinguishable from this having
    /// been what d47 meant to do.
    /// </para>
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

        // The successor starts before this one has exited, so the slot has to be handed over
        // first or it would find d47 "already running" and close itself — an accepted update
        // that looks like the app simply quitting.
        _host.StoppingBecause = "an accepted update is replacing this build";
        _host.ReleaseSingleInstance?.Invoke();

        // Started before this one exits, so the Commander sees d47 come back rather than
        // watching it vanish and having to find it again. Same path as the build it replaces,
        // which is what keeps a taskbar pin pointing at the new one.
        Process.Start(new ProcessStartInfo(running) { UseShellExecute = true });
        Close();
    }

    /// <summary>
    /// Offers a Start Menu entry, once, on the first run that does not already have one.
    /// <para>
    /// d47 does not install — one file the Commander put wherever they put it — so without this
    /// the program is only findable by remembering where that was. Asked rather than assumed,
    /// because writing into someone's Start Menu uninvited is what the no-installer choice was
    /// avoiding in the first place. Asked <em>once</em>: the answer is recorded either way, so a
    /// no stays no.
    /// </para>
    /// </summary>
    private async Task OfferStartMenuEntryAsync()
    {
        if (_host is null)
        {
            return;
        }

        // Read fresh and written back immediately, never cached: WindowPlacementMemory writes
        // the same file, and two holders of a stale copy is how one of them loses its changes.
        if (_host.ViewState.Load().StartMenuOffered)
        {
            return;
        }

        // Nothing to offer if there is one already — a Commander who made one by hand should
        // never see this.
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

        // Recorded before acting, so failing to write the shortcut does not turn into the
        // question coming back every launch.
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
                "I could not add the Start Menu entry. You can still run D47 from where it is.",
                TranscriptKind.Technical);
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
