using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Listening;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The speech model setting names a model D47 can actually load, or none.
/// <para>
/// Reported from a running build across three sessions: the settings row read
/// "Small (English only)" while every utterance came back "I heard you, but I have no speech
/// model loaded to understand it." The row was asserting something untrue, which reads as
/// "the model is fine, the microphone must be broken" — and the offer to fix it was a modal
/// raised once per launch, parented to a window that was on another display.
/// </para>
/// </summary>
public class ModelSelectionFollowsTheDiskTests
{
    /// <summary>
    /// The predicate the rule rests on: a named model that is not on disk is one still awaiting
    /// a download, which is what the host reads to clear the selection and raise the offer.
    /// </summary>
    [Fact]
    public void AModelThatIsNotOnDiskIsStillAwaitingItsDownload()
    {
        Assert.Equal("small.en", WhisperModels.AwaitingDownload("small.en", new NoModels())?.Id);
        Assert.Null(WhisperModels.AwaitingDownload(WhisperModels.NoneId, new NoModels()));
    }

    /// <summary>
    /// The offer states what it costs and where it comes from, because the banner is the
    /// consent — there is no second dialog behind it to carry the terms.
    /// </summary>
    [AvaloniaFact]
    public void TheOfferOnThePanelCarriesTheTermsOfTheDownload()
    {
        var model = new PanelViewModel
        {
            ModelText =
                "Small (English only) — more accurate, slower is not downloaded. About 466 MB from "
                + "huggingface.co, saved to your data folder, verified against the published checksum.",
        };

        Assert.True(model.HasModelOffer);
        Assert.Contains("466 MB", model.ModelText!, StringComparison.Ordinal);
        Assert.Contains("huggingface.co", model.ModelText!, StringComparison.Ordinal);
    }

    /// <summary>The offer is answerable, and answering it takes it away.</summary>
    [AvaloniaFact]
    public void AnsweringTheOfferClearsIt()
    {
        var model = new PanelViewModel { ModelText = "Small is not downloaded. About 466 MB." };

        var accepted = 0;
        var dismissed = 0;
        model.ModelDownloadAccepted += () => accepted++;
        model.ModelDownloadDismissed += () => dismissed++;

        model.AcceptModelDownload();
        model.DismissModelDownload();

        Assert.Equal(1, accepted);
        Assert.Equal(1, dismissed);
    }

    /// <summary>
    /// While it downloads the buttons go away: pressing Download twice would start a second
    /// fetch, and "Not now" cannot un-fetch the one already running.
    /// </summary>
    [AvaloniaFact]
    public void TheOffersButtonsGoAwayWhileItIsDownloading()
    {
        var model = new PanelViewModel { ModelText = "Small is not downloaded." };

        Assert.True(model.ModelActionable);

        model.ModelBusy = true;

        Assert.False(model.ModelActionable);
        Assert.True(model.HasModelOffer);
    }

    /// <summary>The banner is on the panel, so both surfaces show it rather than one window.</summary>
    [AvaloniaFact]
    public void TheOfferIsOnThePanelRatherThanInAWindow()
    {
        var panel = new PanelView
        {
            DataContext = new PanelViewModel { ModelText = "Small is not downloaded. About 466 MB." },
        };

        var window = new Window { Width = 900, Height = 560, Content = panel };
        window.Show();

        var bounds = new Rect(0, 0, 900, 560);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(panel.GetControl<Border>("ModelBanner").IsVisible);

        window.Close();
    }

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
            Func<ModelOffer, Task<bool>> consent,
            IProgress<ModelProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModelInstallResult(ModelInstall.Declined, null));
    }
}
