using D47.Core.Interface;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests;

public sealed class CoresAlwaysDrawTheirShippedPortraitTests
{
    private readonly MemoryInstall _install = new();

    private readonly SpeakerPictures _pictures;

    public CoresAlwaysDrawTheirShippedPortraitTests() => _pictures = new SpeakerPictures(_install.Files, _install.Paths);

    [Fact]
    public void AChosenFileDoesNotShadowACoresShippedPortrait()
    {
        var core = SpeakerPictures.Core("warden");
        _install.Files.WriteBytes(_pictures.Chosen(core), [1]);
        _install.Files.WriteBytes(_pictures.Shipped(core), [2]);

        Assert.Equal(_pictures.Shipped(core), _pictures.Find(core));
    }

    [Fact]
    public void AChosenFileForACoreIsNotChosen()
    {
        var core = SpeakerPictures.Core("warden");
        _install.Files.WriteBytes(_pictures.Chosen(core), [1]);

        Assert.False(_pictures.IsChosen(core));
    }

    [Theory]
    [InlineData("commander.abc")]
    [InlineData("crew.7")]
    [InlineData("captain.man")]
    [InlineData("tower.woman")]
    [InlineData("story.cast")]
    public void OtherSpeakersStillPreferTheChosenFile(string picture)
    {
        _install.Files.WriteBytes(_pictures.Chosen(picture), [1]);
        _install.Files.WriteBytes(_pictures.Shipped(picture), [2]);

        Assert.True(_pictures.IsChosen(picture));
        Assert.Equal(_pictures.Chosen(picture), _pictures.Find(picture));
    }
}
