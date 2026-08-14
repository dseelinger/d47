using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Choosing a speech model fetches it, on the row that was used to choose it.
/// <para>
/// Reported from a running build: the model could not be selected at all — it snapped back to
/// "None — do not transcribe" every time. The rule that a setting may only name a model on
/// disk was doing that correctly, and the offer to put one there was a banner on the main
/// window, which is the window the settings dialog is covering. A question asked behind the
/// thing that asked it.
/// </para>
/// </summary>
public class ModelSelectionFollowsTheDiskTests
{
    /// <summary>
    /// The predicate the startup rule rests on: a named model that is not on disk is one still
    /// awaiting a download.
    /// </summary>
    [Fact]
    public void AModelThatIsNotOnDiskIsStillAwaitingItsDownload()
    {
        Assert.Equal("small.en", WhisperModels.AwaitingDownload("small.en", new NoModels())?.Id);
        Assert.Null(WhisperModels.AwaitingDownload(WhisperModels.NoneId, new NoModels()));
    }

    /// <summary>
    /// Selecting one that is not installed downloads it, and the setting is written only once
    /// the file is there — so the row can never name a model D47 cannot load.
    /// </summary>
    [AvaloniaFact]
    public void ChoosingAModelFetchesItAndThenSelectsIt()
    {
        var asked = new List<string>();

        var (window, settings) = Open((model, _) =>
        {
            asked.Add(model.Id);
            return Task.FromResult(new ModelInstallResult(ModelInstall.Installed, null));
        });

        Choose(window, "small.en");

        Assert.Equal(["small.en"], asked);
        Assert.Equal("small.en", settings.Current.Listening.Model);

        window.Close();
    }

    /// <summary>A download that does not happen leaves the setting where it was.</summary>
    [AvaloniaFact]
    public void AFailedDownloadDoesNotBecomeASelection()
    {
        var (window, settings) = Open((_, _) =>
            Task.FromResult(new ModelInstallResult(ModelInstall.Failed, "the host refused")));

        // Whatever was selected before the attempt — the shipped default on a fresh surface —
        // rather than a literal, because "where it was" is the property under test and the
        // shipped default is free to move.
        var before = settings.Current.Listening.Model;

        Choose(window, "small.en");

        Assert.Equal(before, settings.Current.Listening.Model);
        Assert.NotEqual("small.en", settings.Current.Listening.Model);

        window.Close();
    }

    /// <summary>
    /// The row narrates. A download is neither instant nor visible, and the panel otherwise
    /// speaks only on failure.
    /// </summary>
    [AvaloniaFact]
    public void TheRowSaysWhatWentWrongWhereItWasChosen()
    {
        var (window, _) = Open((_, _) =>
            Task.FromResult(new ModelInstallResult(ModelInstall.Failed, "the host refused")));

        Choose(window, "small.en");

        // Searched from the window: a row's message line is a sibling of its three-column grid
        // rather than a child of it, and what is being asserted is that it is said on the
        // surface the choice was made on - not which panel holds the text block.
        var said = window
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)
            .ToList();

        Assert.Contains(said, text => text.Contains("the host refused", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>And it has somewhere to show progress, under the control that starts it.</summary>
    [AvaloniaFact]
    public void TheModelRowCarriesAProgressBar()
    {
        var (window, _) = Open((_, _) =>
            Task.FromResult(new ModelInstallResult(ModelInstall.Installed, null)));

        Assert.NotEmpty(Row(window).GetVisualDescendants().OfType<ProgressBar>());

        window.Close();
    }

    /// <summary>
    /// The choice is shut while the download runs. A second choice mid-fetch would either be
    /// ignored - which reads as a dead control - or start a second download over the first.
    /// </summary>
    [AvaloniaFact]
    public void TheChoiceIsDisabledWhileItDownloadsAndEnabledAfter()
    {
        var duringDownload = true;
        SettingsWindow? opened = null;

        var (window, _) = Open((_, _) =>
        {
            // Read from inside the download, which is the only moment the answer means
            // anything.
            duringDownload = Combo(opened!).IsEnabled;
            return Task.FromResult(new ModelInstallResult(ModelInstall.Installed, null));
        });

        opened = window;

        Choose(window, "small.en");

        Assert.False(duringDownload, "The model could still be changed while it was downloading.");
        Assert.True(Combo(window).IsEnabled, "The model could not be changed after the download.");

        window.Close();
    }

    /// <summary>
    /// A finished download leaves nothing behind to read. The row speaks only when something
    /// went wrong; a line describing a completed fetch is one the Commander has to dismiss by
    /// reading it.
    /// </summary>
    [AvaloniaFact]
    public void NothingIsLeftOnTheRowAfterASuccessfulDownload()
    {
        var (window, settings) = Open((_, _) =>
            Task.FromResult(new ModelInstallResult(ModelInstall.Installed, null)));

        Choose(window, "small.en");

        Assert.Equal("small.en", settings.Current.Listening.Model);

        var said = window
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(said, text => text.Contains("Fetching", StringComparison.Ordinal));
        Assert.DoesNotContain(said, text => text.Contains("466 MB.", StringComparison.Ordinal));

        window.Close();
    }

    private static ComboBox Combo(SettingsWindow window) =>
        Row(window).GetVisualDescendants().OfType<ComboBox>().First();

    private static (SettingsWindow Window, SettingsService Settings) Open(
        Func<WhisperModel, IProgress<ModelProgress>, Task<ModelInstallResult>> download)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var window = new SettingsWindow();
        window.Attach(settings, viewState, paths, downloadModel: download);
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (window, settings);
    }

    private static void Choose(SettingsWindow window, string modelId)
    {
        var combo = Row(window).GetVisualDescendants().OfType<ComboBox>().First();

        // By label rather than by index: the list carries a clear item above the choices when
        // the row is clearable, so an index into the choices is not an index into the box.
        var wanted = combo.Items
            .Select((item, i) => (Text: item as string ?? string.Empty, Index: i))
            .First(pair => pair.Text.StartsWith(WhisperModels.LabelOf(modelId), StringComparison.Ordinal))
            .Index;

        combo.SelectedIndex = wanted;

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// The compact row itself, which is the three-column grid - not the first grid in the tree
    /// that happens to contain the words, which is the whole surface.
    /// </summary>
    private static Grid Row(SettingsWindow window) =>
        window
            .GetVisualDescendants()
            .OfType<Grid>()
            .Where(grid => grid.ColumnDefinitions.Count == 3)
            .First(grid => grid.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(text => text.Text == "Speech model"));

    private sealed class NoModels : IModelStore
    {
        public string Directory => "nowhere";

        public bool IsInstalled(WhisperModel model) => false;

        public string? PathOf(WhisperModel model) => null;

        public IReadOnlyList<string> Installed() => [];

        public bool Remove(WhisperModel model) => false;

        public Task<ModelOffer?> DescribeAsync(WhisperModel model, CancellationToken cancellationToken = default) =>
            Task.FromResult<ModelOffer?>(null);

        public Task<ModelInstallResult> InstallAsync(
            WhisperModel model,
            IProgress<ModelProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModelInstallResult(ModelInstall.Failed, "nothing is fetched in a test"));
    }
}
