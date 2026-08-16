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
        var pending = WhisperModels.AwaitingDownload("small.en", new FakeModelStore());

        Assert.NotNull(pending);
        Assert.Equal("small.en", pending.Id);
    }

    [Fact]
    public void AModelAlreadyOnDiskIsNot()
    {
        Assert.Null(WhisperModels.AwaitingDownload("small.en", new FakeModelStore("small.en")));
    }

    /// <summary>Choosing no model is a decision, not a question to re-ask at every launch.</summary>
    [Theory]
    [InlineData(WhisperModels.NoneId)]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-model")]
    public void NothingIsOutstandingWhenNothingWasChosen(string? selected)
    {
        Assert.Null(WhisperModels.AwaitingDownload(selected, new FakeModelStore()));
    }

    /// <summary>
    /// A fresh install has the shipped model selected and none of it on disk, which is what
    /// makes the first launch fetch it. This is the assertion behind "d47 can hear you after one
    /// download it starts itself": if the shipped default were ever unselected, or installed by
    /// definition, the first launch would quietly do nothing.
    /// </summary>
    [Fact]
    public void AFreshInstallHasTheDefaultModelOutstandingRatherThanInstalled()
    {
        var shipped = new D47.Core.Configuration.D47Settings().Listening.Model;
        var pending = WhisperModels.AwaitingDownload(shipped, new FakeModelStore());

        Assert.NotNull(pending);
        Assert.Equal(shipped, pending.Id);

        // And once it is on disk nothing is outstanding, so the fetch cannot repeat at every
        // launch.
        Assert.Null(WhisperModels.AwaitingDownload(shipped, new FakeModelStore(shipped)));
    }

}
