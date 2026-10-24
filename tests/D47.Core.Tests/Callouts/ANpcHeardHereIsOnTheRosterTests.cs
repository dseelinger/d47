using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class ANpcHeardHereIsOnTheRosterTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static Announcement Received(string from, string message, string channel, string? localised = null)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-02-10T09:00:00Z",
            ["event"] = "ReceiveText",
            ["From"] = from,
            ["Message"] = message,
            ["Message_Localised"] = localised ?? message,
            ["Channel"] = channel,
        });

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        var reader = new IncomingMessages { Enabled = () => true, IncludeNpcs = () => true };
        return reader.Read(parsed!)!;
    }

    private static Announcement Invented(string name, string text) =>
        new(NpcChatter.LineKey, text)
        {
            Speaker = name,
            CommsChannel = "npc",
            Invented = new NpcChatterHeard(new NpcChatterLine(name, text, null, null), 1, false),
        };

    [Fact]
    public void AnInventedLineAddsOneEntry()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(Invented("Okafor", "Docking queue is long."), "v1", Start);

        var npc = Assert.Single(roster.Present);
        Assert.Equal(("Okafor", NpcKind.Invented, null, "v1"), (npc.Name, npc.Kind, npc.Family, npc.VoiceId));
    }

    [Fact]
    public void ANpcChannelMessageAddsOneEntryWithItsFamily()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(Received("Dread Pirate", "$Pirate_Scan01;", "npc", "Hand over the cargo."), "v2", Start);

        var npc = Assert.Single(roster.Present);
        Assert.Equal(("Dread Pirate", NpcKind.Comms, "Pirate"), (npc.Name, npc.Kind, npc.Family));
    }

    [Theory]
    [InlineData("local")]
    [InlineData("wing")]
    [InlineData("squadron")]
    [InlineData("player")]
    public void APlayerChannelMessageAddsNone(string channel)
    {
        var roster = new NpcsHeardHere();
        roster.Heard(Received("CMDR Someone", "o7", channel), "v3", Start);

        Assert.Empty(roster.Present);
    }

    [Fact]
    public void AJumpClearsTheRoster()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(Invented("Okafor", "Hello."), "v1", Start);
        roster.EnteredSystem();

        Assert.Empty(roster.Present);
    }

    [Fact]
    public void ASignOffRemovesTheSpeaker()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(Invented("Okafor", "Hello."), "v1", Start);
        roster.Heard(Invented("Brandt", "Hi."), "v2", Start.AddSeconds(1));
        roster.Heard(Invented("Okafor", NpcsHeardHere.SignOff("Okafor")), "v1", Start.AddSeconds(2));

        Assert.Equal(["Brandt"], roster.Present.Select(npc => npc.Name));
    }

    [Fact]
    public void TheRosterIsNewestHeardFirst()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(Invented("Okafor", "One."), "v1", Start);
        roster.Heard(Invented("Brandt", "Two."), "v2", Start.AddSeconds(1));
        roster.Heard(Invented("Okafor", "Three."), "v1", Start.AddSeconds(2));

        Assert.Equal(["Okafor", "Brandt"], roster.Present.Select(npc => npc.Name));
    }
}
