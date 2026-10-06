using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Commander's turns carry <c>commander.&lt;FID&gt;</c> of the Commander flying, so one Commander's picture is
/// never drawn beside another's words, and none before a Frontier id is known (#770).
/// </summary>
public sealed class TheCommandersPictureIsTheirsAloneTests
{
    [AvaloniaFact]
    public void CommanderF123SeesTheirPicture()
    {
        var fixture = new SpeakerPictureFixture("d47-commanders-picture-f123");
        fixture.Choose(SpeakerPictures.Commander("F123"), Colors.Teal);
        fixture.Model.CommanderPictureSource = () => SpeakerPictures.Commander("F123");

        var (_, panel) = fixture.Open();
        fixture.Model.Append("Where am I?", voice: TranscriptVoice.Commander);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(SpeakerPictureFixture.Pictures(panel));
    }

    [AvaloniaFact]
    public void CommanderF456DoesNotSeeF123sPicture()
    {
        var fixture = new SpeakerPictureFixture("d47-commanders-picture-f456");
        fixture.Choose(SpeakerPictures.Commander("F123"), Colors.Teal);
        fixture.Model.CommanderPictureSource = () => SpeakerPictures.Commander("F456");

        var (_, panel) = fixture.Open();
        fixture.Model.Append("Where am I?", voice: TranscriptVoice.Commander);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(SpeakerPictureFixture.Pictures(panel));
    }

    [Fact]
    public void ACommanderTurnBeforeTheFrontierIdIsKnownHasNoPicture()
    {
        var model = new PanelViewModel { CommanderPictureSource = () => null };

        model.Append("Hello", voice: TranscriptVoice.Commander);

        Assert.Null(model.Segments(TranscriptPage.Conversation)[0].Picture);
    }
}
