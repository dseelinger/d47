using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// Whether there is still a speech model to ask the Commander about.
/// <para>
/// The question is raised while listening settings are applied, which happens once while the
/// host is starting — before any window exists to have subscribed — and choosing the same model
/// again is an unchanged value, which raises nothing. Between them the question was asked once
/// into an empty room and never again, leaving a push-to-talk key that captured audio nothing
/// could read.
/// </para>
/// </summary>
public class AwaitingDownloadTests
{
    [Fact]
    public void AChosenModelThatIsNotOnDiskIsOutstanding()
    {
        var pending = WhisperModels.AwaitingDownload("small.en", new FakeStore());

        Assert.NotNull(pending);
        Assert.Equal("small.en", pending.Id);
    }

    [Fact]
    public void AModelAlreadyOnDiskIsNot()
    {
        Assert.Null(WhisperModels.AwaitingDownload("small.en", new FakeStore("small.en")));
    }

    /// <summary>Choosing no model is a decision, not a question to re-ask at every launch.</summary>
    [Theory]
    [InlineData(WhisperModels.NoneId)]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-model")]
    public void NothingIsOutstandingWhenNothingWasChosen(string? selected)
    {
        Assert.Null(WhisperModels.AwaitingDownload(selected, new FakeStore()));
    }

    private sealed class FakeStore(params string[] installed) : IModelStore
    {
        public string Directory => "nowhere";

        public bool IsInstalled(WhisperModel model) => installed.Contains(model.Id);

        public string? PathOf(WhisperModel model) => IsInstalled(model) ? "nowhere" : null;

        public IReadOnlyList<string> Installed() => installed;

        public Task<ModelOffer?> DescribeAsync(WhisperModel model, CancellationToken cancellationToken = default) =>
            Task.FromResult<ModelOffer?>(null);

        public bool Remove(WhisperModel model) => false;

        public Task<ModelInstallResult> InstallAsync(
            WhisperModel model,
            Func<ModelOffer, Task<bool>> consent,
            IProgress<ModelProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModelInstallResult(ModelInstall.Declined));
    }
}
