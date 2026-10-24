using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>The channel keys, stick buttons and phrases move the comms selection, and the model cannot (#1601).</summary>
public sealed class TheCommanderMovesTheCommsSelectionTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    /// <summary>CARRIER, TARGET and SYSTEM open; CARRIER selected, on the captain.</summary>
    private static CommsChannels Channels()
    {
        var channels = new CommsChannels();

        channels.Update(new CommsInputs
        {
            CarrierOwned = true,
            Target = new TargetedShip("Cobra Mk III", "Mara Okafor"),
            Heard =
            [
                new NpcHeard("Kestrel Vane", NpcKind.Invented, null, null, Start.AddMinutes(2)),
                new NpcHeard("Ida Rook", NpcKind.Invented, null, null, Start),
            ],
        });

        return channels;
    }

    private static CapabilityRegistry Registry(CommsChannels channels) =>
        CapabilityRegistry.Build([CommsCapability.Create(ActionSurface.Inert, () => false, channels: channels)]);

    private static async Task<ToolResult> Say(CommsChannels channels, string utterance)
    {
        var registry = Registry(channels);
        var router = new KeywordRouter(registry, () => CommsCapability.ChannelPhrases(channels));

        var match = router.MatchToolCommand(utterance);

        Assert.NotNull(match);
        Assert.Equal(CommsCapability.SelectTool, match.ToolName);

        return await registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ChannelUpMovesToTheNextOpenChannel()
    {
        var channels = Channels();

        var said = CommsCapability.Choose(channels, CommsCapability.Next, null);

        Assert.Equal(CommsChannel.Target, channels.Selected!.Channel);
        Assert.Equal("Target, Mara Okafor.", said.Spoken);
    }

    [Fact]
    public void ChannelDownMovesToThePreviousOpenChannelAndWraps()
    {
        var channels = Channels();

        var said = CommsCapability.Choose(channels, CommsCapability.Previous, null);

        Assert.Equal(CommsChannel.System, channels.Selected!.Channel);
        Assert.Equal("System, Kestrel Vane.", said.Spoken);
    }

    [Fact]
    public void SubChannelUpMovesWithinTheChannel()
    {
        var channels = Channels();

        var said = CommsCapability.Choose(channels, CommsCapability.Current, CommsCapability.Next);

        Assert.Equal("Carrier, Tower.", said.Spoken);
    }

    [Fact]
    public void SubChannelDownMovesWithinTheChannelAndWraps()
    {
        var channels = Channels();

        var said = CommsCapability.Choose(channels, CommsCapability.Current, CommsCapability.Previous);

        Assert.Equal("Carrier, Tower.", said.Spoken);
    }

    [Fact]
    public void AChannelWithNoOneSelectedSaysNoOne()
    {
        var channels = new CommsChannels();
        channels.Update(new CommsInputs { StoryRunning = true });

        Assert.Equal("Story, no one.", CommsCapability.Choose(channels, CommsCapability.Current, null).Spoken);
    }

    [Fact]
    public void NoOpenChannelSaysSo()
    {
        var said = CommsCapability.Choose(new CommsChannels(), CommsCapability.Next, null);

        Assert.Equal("No comms channel is open.", said.Spoken);
    }

    [Fact]
    public void EachActionHasAStickButtonSlot()
    {
        Assert.Contains(ActionButtons.Slots, slot => slot.Key == CommsCapability.ChannelUpButtonKey);
        Assert.Contains(ActionButtons.Slots, slot => slot.Key == CommsCapability.ChannelDownButtonKey);
        Assert.Contains(ActionButtons.Slots, slot => slot.Key == CommsCapability.SubChannelUpButtonKey);
        Assert.Contains(ActionButtons.Slots, slot => slot.Key == CommsCapability.SubChannelDownButtonKey);
    }

    [Theory]
    [InlineData("channel up", "Target, Mara Okafor.")]
    [InlineData("channel down", "System, Kestrel Vane.")]
    [InlineData("next sub-channel", "Carrier, Tower.")]
    [InlineData("next sub channel", "Carrier, Tower.")]
    [InlineData("previous sub-channel", "Carrier, Tower.")]
    [InlineData("comms system", "System, Kestrel Vane.")]
    [InlineData("comms target", "Target, Mara Okafor.")]
    [InlineData("comms tower", "Carrier, Tower.")]
    [InlineData("comms Ida Rook", "System, Ida Rook.")]
    [InlineData("comms Rook", "System, Ida Rook.")]
    public async Task EachPhraseReachesTheTool(string utterance, string expected)
    {
        var said = await Say(Channels(), utterance);

        Assert.False(said.IsError, said.Content);
        Assert.Equal(expected, said.Spoken);
    }

    [Fact]
    public async Task AChannelThatIsNotOpenIsRefusedAndTheSelectionStays()
    {
        var channels = Channels();

        var said = await Say(channels, "comms crew");

        Assert.True(said.IsError);
        Assert.Equal("Crew is not open.", said.Spoken);
        Assert.Equal(CommsChannel.Carrier, channels.Selected!.Channel);
    }

    [Fact]
    public void ANameOnTheCurrentChannelSelectsThem()
    {
        var channels = Channels();

        Assert.Equal("Carrier, Tower.", CommsCapability.Choose(channels, CommsCapability.Current, "Tower").Spoken);
    }

    [Fact]
    public void ANameNotOnTheCurrentChannelIsRefusedAndTheSelectionStays()
    {
        var channels = Channels();

        var said = CommsCapability.Choose(channels, CommsCapability.Current, "Ida Rook");

        Assert.True(said.IsError);
        Assert.Equal("Carrier, Captain.", CommsCapability.Say(channels.Selected!));
    }

    [Fact]
    public void MovingTheChannelAndNamingSomeoneTogetherIsRefused()
    {
        var channels = Channels();

        Assert.True(CommsCapability.Choose(channels, CommsCapability.Next, CommsCapability.Next).IsError);
        Assert.Equal(CommsChannel.Carrier, channels.Selected!.Channel);
    }

    [Fact]
    public void ALastNameTwoPartiesShareIsNotAPhrase()
    {
        var channels = new CommsChannels();
        channels.Update(new CommsInputs
        {
            Heard =
            [
                new NpcHeard("Mara Vane", NpcKind.Invented, null, null, Start.AddMinutes(1)),
                new NpcHeard("Jon Vane", NpcKind.Invented, null, null, Start),
                new NpcHeard("Duty Crew", NpcKind.Invented, null, null, Start),
            ],
        });

        var phrases = CommsCapability.ChannelPhrases(channels).Select(command => command.Phrase).ToList();

        Assert.Contains("comms Mara Vane", phrases);
        Assert.Contains("comms Jon Vane", phrases);
        Assert.DoesNotContain("comms Vane", phrases);
        Assert.DoesNotContain("comms Crew", phrases);
    }

    [Fact]
    public async Task TheModelIsRefused()
    {
        var channels = Channels();
        var registry = Registry(channels);

        var said = await registry.InvokeAsync(
            CommsCapability.SelectTool,
            new ToolArguments(new Dictionary<string, string> { ["channel"] = "system" }),
            TestContext.Current.CancellationToken,
            ToolCaller.Model);

        Assert.True(said.IsError);
        Assert.Equal(CommsChannel.Carrier, channels.Selected!.Channel);
    }
}
