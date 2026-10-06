using System.IO.Compression;
using System.Text.Json;
using D47.Core.Configuration;
using D47.Core.Messages;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;
using D47.Core.Interface;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A line from a primary cast member carries its picture; another member's carries one when its picture is on disk;
/// the ship, the narrator and a member without one carry none. The message keeps the picture name across a reload.
/// </summary>
public sealed class AMessageCarriesItsSpeakersPictureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "d47-cast-pictures-" + Guid.NewGuid().ToString("N"));

    private readonly SpeakerPictures _pictures;

    public AMessageCarriesItsSpeakersPictureTests()
    {
        var paths = new AppPaths(_root);
        Directory.CreateDirectory(paths.Stories);
        _pictures = new SpeakerPictures(paths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static StorySpeaker DockHand => Versioned.Cast.Single(speaker => speaker.Id == "dock-hand");

    private static StorySpeaker Cray => Versioned.Cast.Single(speaker => speaker.Id == "cray");

    [Fact]
    public void APrimaryMembersLineCarriesItsPicture() =>
        Assert.Equal($"{Id}.dock-hand", _pictures.For((DockHand with { Primary = true }).Shown(Id, null)));

    [Fact]
    public void AnotherMembersLineCarriesAPictureOnDisk()
    {
        Assert.Null(_pictures.For(DockHand.Shown(Id, null)));

        File.WriteAllBytes(_pictures.Default($"{Id}.dock-hand"), [1]);

        Assert.Equal($"{Id}.dock-hand", _pictures.For(DockHand.Shown(Id, null)));
    }

    [Fact]
    public void TheShipAndTheNarratorCarryNone()
    {
        Assert.Null(Versioned.Speaker(StorySpeaker.Ship, null));
        Assert.Null(Versioned.Speaker(StorySpeaker.Narrator, null));
        Assert.Null(_pictures.For(null));
    }

    [Theory]
    [InlineData(CommanderGender.Man, "for-man")]
    [InlineData(CommanderGender.Woman, "for-woman")]
    public void AMemberInTwoVersionsCarriesTheCommandersVersion(string gender, string suffix)
    {
        var picture = _pictures.For((Cray with { Primary = true }).Shown(Id, gender));

        Assert.Equal($"{Id}.cray.{suffix}", picture);
        Assert.EndsWith(Path.Combine("pictures", $"{Id}.cray.{suffix}.png"), _pictures.Chosen(picture!), StringComparison.Ordinal);
    }

    [Fact]
    public void PrimarySurvivesSealing()
    {
        var secret = Versioned with { Cast = [DockHand with { Primary = true }, Cray] };
        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            JsonSerializer.Serialize(deflate, new[] { secret }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        var cast = StoryCatalog.Unseal(Convert.ToBase64String(packed.ToArray())).Single().Cast;

        Assert.True(cast.Single(speaker => speaker.Id == "dock-hand").Primary);
        Assert.False(cast.Single(speaker => speaker.Id == "cray").Primary);
    }

    [Fact]
    public void TheMessageKeepsItsPictureAcrossAReload()
    {
        var path = Path.Combine(_root, "messages.json");
        new MessageStore(path, NullLogger<MessageStore>.Instance)
            .Post("Ren", "Ride Along", "Mind the hatch.", Now, picture: $"{Id}.dock-hand");

        Assert.Equal($"{Id}.dock-hand", Assert.Single(new MessageStore(path, NullLogger<MessageStore>.Instance).All).Picture);
    }

    [Theory]
    [InlineData("../secrets")]
    [InlineData("a\\b")]
    [InlineData("")]
    public void APathIsNotAPictureName(string picture)
    {
        Assert.False(SpeakerPictures.IsName(picture));
        Assert.Null(_pictures.Find(picture));
    }

    [Fact]
    public void ThePicturesYouChoseAreDisclosedAsKeptHere()
    {
        Assert.Contains(EgressDisclosure.ChosenPictures, EgressDisclosure.Ids);
        Assert.False(EgressDisclosure.Entry(EgressDisclosure.ChosenPictures, new D47Settings(), llmKeyPresent: false).Active);
    }
}
