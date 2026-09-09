using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>Whether there is still a speech model to ask the Commander about.</summary>
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
    /// A fresh install has the shipped model selected and none of it on disk, which is what makes the
    /// first launch fetch it.
    /// </summary>
    [Fact]
    public void AFreshInstallHasTheDefaultModelOutstandingRatherThanInstalled()
    {
        var shipped = new D47.Core.Configuration.D47Settings().Listening.Model;
        var pending = WhisperModels.AwaitingDownload(shipped, new FakeModelStore());

        Assert.NotNull(pending);
        Assert.Equal(shipped, pending.Id);

        // And once it is on disk nothing is outstanding, so the fetch cannot repeat at every launch.
        Assert.Null(WhisperModels.AwaitingDownload(shipped, new FakeModelStore(shipped)));
    }

}
