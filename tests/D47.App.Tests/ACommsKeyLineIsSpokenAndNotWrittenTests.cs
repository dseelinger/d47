using Xunit;

namespace D47.App.Tests;

/// <summary>A comms selection made by key or stick is spoken and reaches neither the transcript nor the model's history.</summary>
public sealed class ACommsKeyLineIsSpokenAndNotWrittenTests
{
    [Fact]
    [Trait("Category", "Gate")]
    public void SayAloudReachesTheVoiceAndNeitherTheTranscriptNorTheTurns()
    {
        var text = AppSource.Method("SayAloud").Text;

        Assert.Contains("Voice.AnnounceAsync(", text);
        Assert.DoesNotContain("_said(", text);
        Assert.DoesNotContain("Turns.", text);
    }

    [Fact]
    [Trait("Category", "Gate")]
    public void TheCommsKeyAndStickPathsSpeakWithSayAloud()
    {
        var move = AppSource.Method("MoveComms").Text;
        var step = AppSource.Method("StepComms").Text;

        Assert.Contains("SayAloud", move);
        Assert.Contains("SayAloud", step);
        Assert.DoesNotContain("SayAside", move);
        Assert.DoesNotContain("SayAside", step);
    }
}
