using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>With "Break the fourth wall" off the Narrator is not offered and never speaks to the Commander; on, it is and may.</summary>
public class TheNarratorBreaksTheFourthWallOnlyWhenToldToTests
{
    private const string Address = "You are standing in the dark, Commander, and I can see you.";

    private static readonly CommsInputs Story = new() { StoryRunning = true };

    private static CommsSubChannel[] Offered(bool on) =>
        [.. new CommsInputsFeed(on).SubChannels(CommsChannel.Story)];

    private sealed class CommsInputsFeed
    {
        private readonly CommsChannels _channels = new();

        public CommsInputsFeed(bool on) => _channels.Update(Story with { NarratorAnswers = on });

        public IReadOnlyList<CommsSubChannel> SubChannels(CommsChannel channel) => _channels.SubChannels(channel);
    }

    [Fact]
    public void TheSettingIsOffByDefault() => Assert.False(D47Settings.Defaults.Speech.NarratorFourthWall);

    [Fact]
    public void TheNarratorIsOfferedOnlyWhenTheSettingIsOn()
    {
        Assert.DoesNotContain(Offered(false), party => party.Key == CommsChannels.NarratorKey);
        Assert.Contains(Offered(true), party => party.Key == CommsChannels.NarratorKey);
    }

    [Fact]
    public void TheSettingIsSetThroughItsKey()
    {
        var surface = TestSurface.For(new MemoryInstall());

        surface.Settings.Apply(SpeechCapability.NarratorFourthWallKey, "true", SettingsCaller.Panel);

        Assert.True(surface.Settings.Current.Speech.NarratorFourthWall);
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, Address)]
    public async Task ASecondPersonReplyIsSpokenOnlyWhenTheSettingIsOn(bool on, string spoken)
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering(Address);
        var registry = TestSurface.For(install);
        var line = new NarratorLine(() => null, fourthWall: () => on);

        var loop = new TurnLoop(
            registry.Registry,
            registry.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = "You are the ship's AI.",
            ShipAiNames = () => ["Warden"],
            CommsSelected = () => OnComms.Narrator,
        };

        loop.Lines.Add(line);

        var events = await OnComms.SayAsync(loop, "who is there?");

        var text = events.OfType<TurnEvent.Completed>().Select(done => done.Result.Text).SingleOrDefault() ?? string.Empty;
        Assert.Equal(spoken, text);
        Assert.Equal(on, provider.LastRequest!.Prompt.Persona!.Contains("You may address the Commander", StringComparison.Ordinal));
    }
}
