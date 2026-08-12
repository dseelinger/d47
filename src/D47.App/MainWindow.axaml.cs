using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using D47.App.Updates;
using D47.Core.Capabilities;
using D47.Core.Conversation;

namespace D47.App;

public partial class MainWindow : Window
{
    private readonly AppHost? _host;
    private readonly StringBuilder _transcript = new();
    private AvailableUpdate? _availableUpdate;
    private bool _turnInFlight;

    public MainWindow() : this(host: null)
    {
    }

    public MainWindow(AppHost? host)
    {
        _host = host;
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (_host is null)
        {
            Transcript.Text = "No host: the window is running under the designer.";
            return;
        }

        VersionLine.Text = $"build {_host.Version}";

        var errors = new List<string>();
        if (_host.StartupError is { } startupError)
        {
            errors.Add(startupError);
        }

        // The Phase 1 claim is that a request produces a real tool call that runs and returns a
        // result. This is that call, dispatched by name through the registry.
        var status = await _host.Capabilities.InvokeAsync("get_app_status", ToolArguments.Empty);
        Append(status.Content);

        if (status.IsError)
        {
            errors.Add(status.Content);
        }

        // Say plainly whether the model is available. Silence here is indistinguishable from a
        // model with nothing to say, and the keyword router still answers either way.
        var availability = _host.LlmAvailability;
        Append(availability.Current == LlmAvailability.Available
            ? "\nLanguage model: ready."
            : $"\nLanguage model: unavailable. {availability.Reason} " +
              "Keyword commands still work — try \"where am I\" or \"status\".");

        if (errors.Count > 0)
        {
            ErrorText.Text = string.Join(Environment.NewLine, errors);
            ErrorBanner.IsVisible = true;
        }

        AskBox.Focus();

        // Fire-and-forget: an update check is optional and must never delay the status the
        // Commander is actually here for. UpdateChecker swallows its own failures.
        _ = CheckForUpdateAsync(_host);
    }

    private void OnAskBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = AskAsync();
        }
    }

    private void OnAskClick(object? sender, RoutedEventArgs e) => _ = AskAsync();

    private async Task AskAsync()
    {
        if (_host is null || _turnInFlight)
        {
            return;
        }

        var input = AskBox.Text?.Trim();
        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        _turnInFlight = true;
        AskButton.IsEnabled = false;
        AskBox.Text = string.Empty;
        Append($"\n\n> {input}\n");

        try
        {
            // Rendered as it arrives rather than at the end of the turn. The same streaming is
            // what will let speech start at the first sentence boundary in Phase 5.
            await foreach (var turnEvent in _host.Turns.RunAsync(input))
            {
                switch (turnEvent)
                {
                    case TurnEvent.Routed routed:
                        TurnLine.Text = routed.Effort is { } effort
                            ? $"routed: {routed.Route}, effort {effort}"
                            : $"routed: {routed.Route}";
                        break;

                    case TurnEvent.TextDelta text:
                        Append(text.Text);
                        break;

                    case TurnEvent.Completed completed:
                        TurnLine.Text = DescribeTurn(completed.Result, _host.Spend);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            // A turn that throws is a bug, not a provider failure — provider failures arrive as
            // events. Surface it rather than losing it.
            Append($"\n[turn failed: {ex.Message}]");
        }
        finally
        {
            _turnInFlight = false;
            AskButton.IsEnabled = true;
            AskBox.Focus();
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

    private void Append(string text)
    {
        _transcript.Append(text);
        Transcript.Text = _transcript.ToString();
        TranscriptScroller.ScrollToEnd();
    }

    private async Task CheckForUpdateAsync(AppHost host)
    {
        var update = await host.Updates.CheckAsync(host.Version, CancellationToken.None);
        if (update is null)
        {
            return;
        }

        _availableUpdate = update;
        UpdateText.Text = $"d47 {update.Version} is available — you're on {host.Version}.";
        UpdateBanner.IsVisible = true;
    }

    private void OnUpdateNowClick(object? sender, RoutedEventArgs e)
    {
        if (_availableUpdate is null)
        {
            return;
        }

        // Opens the release page for a manual download; d47 exits so the new build can overwrite
        // this running exe on the Commander's next launch (list.md Phase 17).
        Process.Start(new ProcessStartInfo(_availableUpdate.ReleaseUrl) { UseShellExecute = true });
        Close();
    }

    private void OnUpdateLaterClick(object? sender, RoutedEventArgs e) => UpdateBanner.IsVisible = false;
}
