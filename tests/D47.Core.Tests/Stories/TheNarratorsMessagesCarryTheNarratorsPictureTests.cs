using D47.Core.Interface;
using D47.Core.Messages;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A Narrator message with no picture of its own gets the Narrator's; a cast message keeps its own; a ship message gets none (#1141).</summary>
public sealed class TheNarratorsMessagesCarryTheNarratorsPictureTests
{
    [Fact]
    public void ANarratorMessageWithNoPictureGetsTheNarrators()
    {
        var message = new D47Message { From = MessageStore.Narrator };

        Assert.Equal(SpeakerPictures.Narrator, SpeakerPictures.OfMessage(message));
    }

    [Fact]
    public void ACastMessageKeepsItsOwnPicture()
    {
        var message = new D47Message { From = "Vasquez", Picture = "story.night-watch.vasquez" };

        Assert.Equal("story.night-watch.vasquez", SpeakerPictures.OfMessage(message));
    }

    [Fact]
    public void AShipMessageGetsNoPicture()
    {
        var message = new D47Message { From = "warden" };

        Assert.Null(SpeakerPictures.OfMessage(message));
    }
}
