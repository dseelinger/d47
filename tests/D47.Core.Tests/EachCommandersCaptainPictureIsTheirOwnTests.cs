using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests;

public sealed class EachCommandersCaptainPictureIsTheirOwnTests
{
    private readonly MemoryInstall _install = new();

    private readonly SpeakerPictures _pictures;

    public EachCommandersCaptainPictureIsTheirOwnTests()
    {
        _pictures = new SpeakerPictures(_install.Files, _install.Paths);
        _install.Files.WriteBytes(_pictures.Shipped("captain.woman"), [2]);
    }

    private static string Name(string frontierId) =>
        SpeakerPictures.ByVoice(VoiceRole.CarrierCaptain, VoiceGender.Feminine, frontierId)!;

    [Fact]
    public void TheNameCarriesTheFrontierIdOfTheCommanderFlying() =>
        Assert.Equal("captain.woman.F123", Name("F123"));

    [Fact]
    public void WithNoFrontierIdTheNameIsTheGenderAlone() =>
        Assert.Equal("tower.man", SpeakerPictures.ByVoice(VoiceRole.TowerControl, VoiceGender.Masculine));

    [Fact]
    public void ACommanderWithNoFileSeesTheShippedPicture() =>
        Assert.Equal(_pictures.Shipped("captain.woman"), _pictures.Find(Name("F123")));

    [Fact]
    public void ACommandersFileIsShownToThemAndNotToAnother()
    {
        _install.Files.WriteBytes(_pictures.Chosen(Name("F123")), [1]);

        Assert.Equal(_pictures.Chosen(Name("F123")), _pictures.Find(Name("F123")));
        Assert.Equal(_pictures.Shipped("captain.woman"), _pictures.Find(Name("F456")));
        Assert.True(_pictures.IsChosen(Name("F123")));
        Assert.False(_pictures.IsChosen(Name("F456")));
    }

    [Fact]
    public void UsingTheDefaultDeletesOnlyThatCommandersFileForThatGender()
    {
        _install.Files.WriteBytes(_pictures.Chosen("captain.woman.F123"), [1]);
        _install.Files.WriteBytes(_pictures.Chosen("captain.man.F123"), [1]);
        _install.Files.WriteBytes(_pictures.Chosen("captain.woman.F456"), [1]);

        _pictures.UseDefault("captain.woman.F123");

        Assert.False(_pictures.IsChosen("captain.woman.F123"));
        Assert.True(_pictures.IsChosen("captain.man.F123"));
        Assert.True(_pictures.IsChosen("captain.woman.F456"));
        Assert.Equal(_pictures.Shipped("captain.woman"), _pictures.Find("captain.woman.F123"));
    }
}
