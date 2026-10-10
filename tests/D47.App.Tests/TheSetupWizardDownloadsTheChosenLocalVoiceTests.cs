using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>START fetches a chosen local voice that is not installed, on a step of its own.</summary>
[Trait("Category", "Integration")]
public class TheSetupWizardDownloadsTheChosenLocalVoiceTests
{
    private sealed class StubFetch
    {
        public TaskCompletionSource<string?> Done { get; } = new();

        public IProgress<double>? Progress { get; private set; }

        public CancellationToken Token { get; private set; }

        public int Calls { get; private set; }

        public Task<string?> Run(IProgress<double> progress, CancellationToken token)
        {
            Calls++;
            Progress = progress;
            Token = token;

            return Done.Task;
        }
    }

    private static (SetupWizard Wizard, Window Host, StubFetch Fetch, Func<bool> Closed) Open(bool installed = false)
    {
        var (settings, _, _, _, _) = TestSurface.CreateFull();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var fetch = new StubFetch();
        var wizard = new SetupWizard(
            settings,
            localVoice: id => id == TtsProviderCatalog.ChatterboxId && !installed
                ? new LocalVoiceDownload("Chatterbox", 690, fetch.Run)
                : null);

        var closed = false;
        wizard.Closed += (_, _) => closed = true;
        var host = wizard.Show();

        wizard.Go(SetupWizard.Step.Voice);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Press(wizard, $"SetupOption-{TtsProviderCatalog.ChatterboxId}");
        wizard.Go(SetupWizard.Step.Ready);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (wizard, host, fetch, () => closed);
    }

    private static void Press(Control root, string name)
    {
        var button = root.GetVisualDescendants().OfType<Button>().First(candidate => candidate.Name == name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static string Text(Control root) => string.Join(
        "\n",
        root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty));

    [AvaloniaFact]
    public void StartWithAnUninstalledLocalVoiceShowsTheDownloadStepInsteadOfClosing()
    {
        var (wizard, host, fetch, closed) = Open();

        Assert.Contains("690 MB from huggingface.co", Text(wizard));

        Press(wizard, "SetupNext");

        Assert.True(wizard.IsDownloading);
        Assert.False(closed());
        Assert.Equal(1, fetch.Calls);
        Assert.Contains("Fetched once from huggingface.co.", Text(wizard));

        fetch.Progress!.Report(0.5);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Contains("345 of 690 MB", Text(wizard));

        host.Close();
    }

    [AvaloniaFact]
    public void TheWizardClosesWhenTheDownloadFinishes()
    {
        var (wizard, host, fetch, closed) = Open();

        Press(wizard, "SetupNext");
        fetch.Done.SetResult(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(closed());

        host.Close();
    }

    [AvaloniaFact]
    public void CancellingStopsTheDownloadAndKeepsTheSavedChoice()
    {
        var (wizard, host, fetch, closed) = Open();

        Press(wizard, "SetupNext");
        Press(wizard, "SetupDownloadCancel");

        Assert.True(closed());
        Assert.True(fetch.Token.IsCancellationRequested);

        host.Close();
    }

    [AvaloniaFact]
    public void AFailedDownloadShowsItsDetailAndRetryFetchesAgain()
    {
        var (wizard, host, fetch, closed) = Open();

        Press(wizard, "SetupNext");
        fetch.Done.SetResult("The network dropped.");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(closed());
        Assert.Contains("The network dropped.", Text(wizard));

        Press(wizard, "SetupDownloadRetry");
        Assert.Equal(2, fetch.Calls);

        host.Close();
    }

    [AvaloniaFact]
    public void StartWithAnInstalledLocalVoiceClosesAsBefore()
    {
        var (wizard, host, fetch, closed) = Open(installed: true);

        Assert.DoesNotContain("START downloads", Text(wizard));

        Press(wizard, "SetupNext");

        Assert.True(closed());
        Assert.Equal(0, fetch.Calls);

        host.Close();
    }
}
