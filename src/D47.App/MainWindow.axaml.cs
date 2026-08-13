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
using D47.Core.Listening;
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
    private readonly PanelViewModel _model;

    private AvailableUpdate? _availableUpdate;
    private bool _turnInFlight;
    private bool _downloading;

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

        _shutUp = new GlobalHotkey(hotkeyLogger);
        _reanchor = new GlobalHotkey(hotkeyLogger);

        InitializeComponent();

        Panel.DataContext = _model;
        _model.AskRequested += () => _ = AskAsync();
        _model.SettingsRequested += OpenSettings;
        _model.UpdateAccepted += OnUpdateAccepted;
        _model.UpdateDismissed += () => _model.UpdateText = null;

        if (host is not null)
        {
            // Both before the window is shown. Sizing after the fact is a visible resize, and
            // wrapping the content after the first layout pass is a visible reflow.
            WindowPlacementMemory.Attach(this, host.ViewState);
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
            _model.Append("No host: the window is running under the designer.");
            return;
        }

        _model.VersionLine = $"Optimize Inferior Systems  ·  build {_host.Version}";

        var errors = new List<string>();
        if (_host.StartupError is { } startupError)
        {
            errors.Add(startupError);
        }

        // The Phase 1 claim is that a request produces a real tool call that runs and returns a
        // result. This is that call, dispatched by name through the registry.
        var status = await _host.Capabilities.InvokeAsync("get_app_status", ToolArguments.Empty);
        _model.Append(status.Content);

        if (status.IsError)
        {
            errors.Add(status.Content);
        }

        // Say plainly whether the model is available. Silence here is indistinguishable from a
        // model with nothing to say, and the keyword router still answers either way.
        var availability = _host.LlmAvailability;
        _model.Append(availability.Current == LlmAvailability.Available
            ? "\nLanguage model: ready."
            : $"\nLanguage model: unavailable. {availability.Reason} " +
              "Keyword commands still work — try \"where am I\" or \"status\".");

        if (errors.Count > 0)
        {
            _model.ErrorText = string.Join(Environment.NewLine, errors);
        }

        DescribeHotkeys();
        BindShutUp();
        BindReanchor();

        // Spoken input runs the same turn as typed input, deliberately. A second path would be
        // a second place for the in-flight gate, the interrupt vocabulary and the cancellation
        // slot to be got wrong, and the Commander expects "where am I" to mean the same thing
        // whichever way they said it (list.md Phase 6).
        _host.Heard += text => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _model.AskText = text;
            _ = AskAsync();
        });

        _host.ModelNeeded += model => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _ = OfferModelDownloadAsync(model));
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
        });

        Panel.FocusAsk();

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

        base.OnKeyDown(e);
    }

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

    private void OpenSettings()
    {
        if (_host is not null)
        {
            SettingsWindow.Show(this, _host.Settings, _host.ViewState, _host.Paths);
        }
    }

    private async Task AskAsync()
    {
        if (_host is null)
        {
            return;
        }

        var input = _model.AskText?.Trim();
        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        // Asked before the in-flight gate, never after. A silence command is only ever wanted
        // while d47 is mid-sentence, which is exactly when _turnInFlight is true — so gating it
        // on that would drop it at the one moment it matters (list.md Phase 5, "never gated
        // behind a turn completing"). The registry decides what may interrupt; this does not.
        //
        // Only consulted when there is actually something to interrupt, which is what lets the
        // vocabulary include a bare "stop". Idle, "stop" is the opening word of "stop the ship"
        // and belongs to whatever answers that; mid-sentence it has one meaning. Context is the
        // disambiguator, so context is the gate.
        if ((_turnInFlight || _host.Audio.IsSpeaking)
            && _host.Router.MatchInterrupting(input) is { } interrupting)
        {
            _model.AskText = string.Empty;
            var stopped = await _host.Capabilities.InvokeAsync(interrupting.ToolName, ToolArguments.Empty);
            _model.Append($"\n\n> {input}\n{stopped.Content}");
            return;
        }

        if (_turnInFlight)
        {
            return;
        }

        _turnInFlight = true;
        _model.CanAsk = false;
        _model.AskText = string.Empty;
        _model.Append($"\n\n> {input}\n");

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
                _host.Turns.RunAsync(input, cancelling.Token),
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
                            _model.TurnLine = DescribeTurn(completed.Result, _host.Spend);
                            break;
                    }
                });
        }
        catch (Exception ex)
        {
            // A turn that throws is a bug, not a provider failure — provider failures arrive as
            // events. Surface it rather than losing it.
            _model.Append($"\n[turn failed: {ex.Message}]");
        }
        finally
        {
            _turnInFlight = false;
            _model.CanAsk = true;
            Panel.FocusAsk();
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
    private async Task OfferModelDownloadAsync(WhisperModel model)
    {
        if (_host is null || _downloading)
        {
            return;
        }

        _downloading = true;

        try
        {
            // Progress<T> captures the synchronisation context it was constructed on, which is
            // this one, so the callback already arrives on the UI thread. Posting again would be
            // a second hop for no reason.
            var progress = new Progress<ModelProgress>(report =>
                _model.TurnLine = $"Downloading {report.ModelId}: {report.Fraction:P0}");

            var result = await _host.InstallModelAsync(
                model,
                async offer =>
                {
                    var dialog = new ConfirmWindow(
                        "Download a speech model",
                        offer.ConsentPrompt(),
                        "Download",
                        "Not now");

                    return await dialog.AskAsync(this);
                },
                progress);

            _model.TurnLine = result.Outcome switch
            {
                ModelInstall.Installed => $"{model.Id} is installed. I can understand you now.",
                ModelInstall.AlreadyPresent => $"{model.Id} was already installed.",
                ModelInstall.Declined => $"{model.Id} was not downloaded.",
                ModelInstall.ChecksumMismatch => result.Detail ?? "The download did not verify.",
                _ => result.Detail ?? $"{model.Id} could not be downloaded.",
            };

            if (result.Outcome is ModelInstall.Failed or ModelInstall.ChecksumMismatch)
            {
                // A failed download is worth the banner rather than a status line that scrolls
                // away: without a model, holding the key produces nothing and looks like a bug.
                _model.ErrorText = _model.TurnLine;
            }
        }
        finally
        {
            _downloading = false;
        }
    }

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
            _model.ErrorText = $"The silence hotkey {gesture} could not be registered system-wide. " +
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
            _model.ErrorText = $"The re-anchor hotkey {gesture} could not be registered system-wide. " +
                               "Another application is probably holding it — pick another in Settings.";
        }
    }

    private static string DescribeTurn(TurnResult result, SpendTracker spend)
    {
        var line = new StringBuilder($"{result.Outcome} via {result.Route}");

        if (result.Effort is { } effort)
        {
            line.Append($", effort {effort}");
        }

        if (result.Cost is { } cost)
        {
            line.Append(
                $", {cost.Usage.TotalInputTokens} in ({cost.Usage.CacheReadInputTokens} cached), " +
                $"{cost.Usage.OutputTokens} out");

            line.Append(cost.Priced
                ? $", {cost.Dollars:C4} this turn, {spend.RunningTotalDollars:C4} session"
                : ", unpriced model");

            if (spend.UnexplainedColdPrefixes > 0)
            {
                // A profile switch is the only sanctioned cause of a cold prefix, so this counter
                // being non-zero is a caching regression rather than a cost curiosity.
                line.Append($" — {spend.UnexplainedColdPrefixes} unexplained cold prefix(es)");
            }
        }

        return line.ToString();
    }

    private async Task CheckForUpdateAsync(AppHost host)
    {
        var update = await host.Updates.CheckAsync(host.Version, CancellationToken.None);
        if (update is null)
        {
            return;
        }

        _availableUpdate = update;
        _model.UpdateText = $"d47 {update.Version} is available — you're on {host.Version}.";
    }

    /// <summary>
    /// Downloads the new build, verifies it, puts it where this one is and starts it
    /// (list.md Phase 17: "the user is given an opportunity to exit, install it, and restart").
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
        _model.UpdateText = $"Downloading d47 {update.Version}…";

        var progress = new Progress<double>(fraction =>
            _model.UpdateText = $"Downloading d47 {update.Version} — {fraction:P0}");

        var (file, failure) = await _host.Installer
            .DownloadAsync(update, progress, CancellationToken.None);

        if (file is null)
        {
            _model.UpdateBusy = false;
            OpenReleasePage(update, Explain(failure));
            return;
        }

        _model.UpdateText = $"Installing d47 {update.Version}…";

        if (Environment.ProcessPath is not { } running
            || !_host.Installer.TrySwap(running, file))
        {
            _model.UpdateBusy = false;
            OpenReleasePage(update, Explain(UpdateFailure.CouldNotReplace));
            return;
        }

        // Started before this one exits, so the Commander sees d47 come back rather than
        // watching it vanish and having to find it again.
        Process.Start(new ProcessStartInfo(running) { UseShellExecute = true });
        Close();
    }

    private void OpenReleasePage(AvailableUpdate update, string reason)
    {
        _model.UpdateText = $"{reason} Opening the release page.";

        Process.Start(new ProcessStartInfo(update.ReleaseUrl) { UseShellExecute = true });
    }

    private static string Explain(UpdateFailure? failure) => failure switch
    {
        UpdateFailure.ChecksumMismatch =>
            "The download did not match the checksum published with it, so d47 did not run it.",
        UpdateFailure.CouldNotReplace =>
            "d47 could not replace itself where it is installed.",
        UpdateFailure.NothingToInstall =>
            "This release has no installable build attached.",
        _ => "The download did not finish.",
    };
}
