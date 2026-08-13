using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// In-game chat, read aloud in somebody else's voice (list.md Phase 11).
/// </summary>
public class IncomingMessageTests
{
    private static JournalEvent Message(string from, string message, string channel, string? localised = null)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-02-10T09:00:00Z",
            ["event"] = "ReceiveText",
            ["From"] = from,
            ["Message"] = message,
            ["Message_Localised"] = localised,
            ["Channel"] = channel,
        });

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static IncomingMessages Reader(bool npcs = true) => new()
    {
        Enabled = () => true,
        IncludeNpcs = () => npcs,
    };

    [Fact]
    public void APlayerMessageIsSpokenInThatPlayersVoice()
    {
        var read = Reader().Read(Message("$cmdr_decorate:#name=Vex;", "watch your six", "wing"));

        Assert.NotNull(read);
        Assert.Equal(VoiceRole.Comms, read.Voice);
        Assert.Equal("Vex", read.Speaker);
        Assert.True(read.SpeakerIsPlayer);
        Assert.Contains("watch your six", read.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NpcChatterIsItsOwnDecision()
    {
        var npc = Message("$ShipName_Police_Federation;", "Scanning.", "npc", localised: "Scanning.");

        Assert.Null(Reader(npcs: false).Read(npc));

        var read = Reader(npcs: true).Read(npc);
        Assert.NotNull(read);
        Assert.False(read.SpeakerIsPlayer);
    }

    [Fact]
    public void AnUnlocalisedTokenIsNotSpokenAsText()
    {
        // Elite writes `$Pirate_Attack;` style ids when it has no localised form to give. Saying
        // "dollar pirate underscore attack semicolon" out loud is worse than silence — and the
        // danger callouts already treat these as ids rather than as prose.
        Assert.Null(Reader().Read(Message("$npc_name_decorate:#name=Ilse;", "$Pirate_Attack;", "npc")));
    }

    [Fact]
    public void TheCommandersOwnMessagesAreNotReadBackToThem()
    {
        // Elite echoes what you send on the channel it went out on. Dictating into wing chat and
        // then hearing yourself in a stranger's voice is the most confusing thing this could do.
        var reader = Reader();
        reader.CommanderName = "Vex";

        Assert.Null(reader.Read(Message("$cmdr_decorate:#name=Vex;", "on my way", "wing")));
        Assert.NotNull(reader.Read(Message("$cmdr_decorate:#name=Ilse;", "on my way", "wing")));
    }

    [Theory]
    [InlineData("$cmdr_decorate:#name=Vex;", "Vex")]
    [InlineData("$npc_name_decorate:#name=Ilse Bruhn;", "Ilse Bruhn")]
    [InlineData("Vex", "Vex")]
    [InlineData("$ShipName_Police_Federation;", "ShipName Police Federation")]
    public void SenderNamesAreUnwrappedRatherThanSpokenWithTheirScaffolding(string from, string expected)
    {
        Assert.Equal(expected, IncomingMessages.Undecorate(from));
    }

    [Fact]
    public void NothingIsSpokenFromTheBacklog()
    {
        // Starting d47 after an hour of flying must not read out an hour of chat, for the same
        // reason the material milestones prime silently.
        var reader = Reader();

        var priming = new CalloutContext(
            DateTimeOffset.UnixEpoch,
            IsPriming: true,
            State: null,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [Message("$cmdr_decorate:#name=Vex;", "hello", "wing")]);

        Assert.Empty(reader.Examine(priming));
    }

    [Fact]
    public void NothingIsSpokenWhileTheSettingIsOff()
    {
        var reader = new IncomingMessages { Enabled = () => false, IncludeNpcs = () => true };

        var context = new CalloutContext(
            DateTimeOffset.UnixEpoch,
            IsPriming: false,
            State: null,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [Message("$cmdr_decorate:#name=Vex;", "hello", "wing")]);

        Assert.Empty(reader.Examine(context));
    }

    [Fact]
    public void MessagesAreNotSuppressedAsRepeats()
    {
        // Two messages on one channel are two messages, not one warning said twice. The callout
        // cooldown exists for conditions that stay true across hundreds of ticks.
        var read = Reader().Read(Message("$cmdr_decorate:#name=Vex;", "one", "wing"));

        Assert.NotNull(read);
        Assert.Equal(TimeSpan.Zero, read.Cooldown);
    }
}
