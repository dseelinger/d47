using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Characters;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

public sealed class AllIsTheLastStopOfTheCommsSubChannelCycleTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static NpcHeard Npc(string name, int minutes) =>
        new(name, NpcKind.Invented, null, null, Start.AddMinutes(minutes));

    private static CustomCharacter Character(string id, string name) => new(id, name, null, null, true, null);

    private static AdventureSpeaker Cast(string id) => new(id, id.ToUpperInvariant(), string.Empty);

    private static CommsChannels With(CommsInputs inputs)
    {
        var channels = new CommsChannels();
        channels.Update(inputs);
        return channels;
    }

    private static List<string?> Cycle(CommsChannels channels, int steps)
    {
        List<string?> seen = [channels.Selected?.SubChannel?.Name];

        for (var i = 0; i < steps; i++)
        {
            channels.NextSubChannel();
            seen.Add(channels.Selected?.SubChannel?.Name);
        }

        return seen;
    }

    [Fact]
    public void AllEndsTheCycleOnCarrier()
    {
        var channels = With(new CommsInputs { CarrierOwned = true });

        Assert.Equal(["Captain", "Tower", "All", "Captain"], Cycle(channels, 3));
    }

    [Fact]
    public void AllEndsTheCycleOnSystem()
    {
        var channels = With(new CommsInputs { Heard = [Npc("Okafor", 3), Npc("Vance", 1)] });

        Assert.Equal(["Okafor", "Vance", "All", "Okafor"], Cycle(channels, 3));
    }

    [Fact]
    public void AllEndsTheCycleOnCharacters()
    {
        var channels = With(new CommsInputs { Characters = [Character("a", "Wren"), Character("b", "Tamsin")] });

        Assert.Equal(["Wren", "Tamsin", "All", "Wren"], Cycle(channels, 3));
    }

    [Fact]
    public void OnePartyOnSystemOrCharactersShowsNoAll()
    {
        var system = With(new CommsInputs { Heard = [Npc("Okafor", 3)] });
        var characters = With(new CommsInputs { Characters = [Character("a", "Wren")] });

        Assert.Equal(["Okafor", "Okafor"], Cycle(system, 1));
        Assert.Equal(["Wren", "Wren"], Cycle(characters, 1));
    }

    [Fact]
    public void StoryCountsCastMembersOnly()
    {
        var narrated = new CommsInputs { StoryRunning = true, StoryId = "s", NarratorAnswers = true, StoryCast = [Cast("mara")] };
        var channels = With(narrated);

        Assert.Equal(["MARA", "Narrator", "MARA"], Cycle(channels, 2));

        channels = With(narrated with { StoryCast = [Cast("mara"), Cast("juno")] });

        Assert.Equal(["MARA", "JUNO", "Narrator", "All", "MARA"], Cycle(channels, 4));
    }

    [Fact]
    public void CrewTargetAndShuttleNeverShowAll()
    {
        var crew = new ShipCrew
        {
            Members =
            [
                new CrewMember("Vance", CrewId: 1, CombatRank: 6, Active: true),
                new CrewMember("Okoro", CrewId: 2, CombatRank: 6, Active: true),
            ],
        };
        var channels = With(new CommsInputs
        {
            Crew = crew,
            Target = new TargetedShip("Cobra Mk III", "Mara Okafor"),
            Shuttle = new ShuttleRide(ShuttleKind.Apex, null, null, false),
        });

        Assert.Equal(3, channels.Available.Count);

        foreach (var channel in channels.Available)
        {
            Assert.DoesNotContain(channels.SubChannels(channel), party => party.Key == CommsChannels.AllKey);
        }
    }

    [Fact]
    public void AllReachesTheNamedPartiesWithoutTheNarrator()
    {
        var channels = With(new CommsInputs
        {
            StoryRunning = true,
            StoryId = "s",
            NarratorAnswers = true,
            StoryCast = [Cast("mara"), Cast("juno")],
        });

        Assert.True(channels.Select(CommsChannel.Story, "all"));

        var selected = channels.Selected!;
        Assert.True(selected.IsAll);
        Assert.False(selected.IsNoOne);
        Assert.Equal(["mara", "juno"], selected.Everyone.Select(party => party.Key));
    }

    [Theory]
    [InlineData("all")]
    [InlineData("everyone")]
    [InlineData("ALL")]
    public void SelectTakesAllAndEveryoneBeforeAnyName(string word)
    {
        var channels = With(new CommsInputs { CarrierOwned = true });

        Assert.True(channels.Select(CommsChannel.Carrier, word));
        Assert.True(channels.Selected!.IsAll);
    }

    [Fact]
    public void SelectAllIsFalseWhereAllIsNotOffered()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 3)] });
        channels.Select(CommsChannel.Carrier, "Tower");

        Assert.False(channels.Select(CommsChannel.System, "all"));
        Assert.False(channels.Select(CommsChannel.System, "everyone"));
        Assert.Equal("Tower", channels.Selected!.SubChannel!.Name);
    }

    [Fact]
    public async Task CommsAllAndCommsEveryoneSelectAllOnTheSelectedChannel()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 3), Npc("Vance", 1)] });
        var registry = CapabilityRegistry.Build([CommsCapability.Create(ActionSurface.Inert, () => false, channels: channels)]);
        var router = new KeywordRouter(registry, () => CommsCapability.ChannelPhrases(channels));

        foreach (var phrase in new[] { "comms all", "comms everyone" })
        {
            channels.Select(CommsChannel.Carrier, "Captain");
            channels.Select(CommsChannel.System, "Okafor");

            var match = router.MatchToolCommand(phrase);

            Assert.NotNull(match);
            Assert.Equal(CommsCapability.SelectTool, match.ToolName);

            var result = await registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

            Assert.Equal("System, all.", result.Spoken);
            Assert.True(channels.Selected!.IsAll);
            Assert.Equal(CommsChannel.System, channels.Selected.Channel);
        }
    }

    [Fact]
    public void ChannelPhrasesIssueNoPhraseForAllOrAPartyNamedAllOrEveryone()
    {
        var channels = With(new CommsInputs { Heard = [Npc("All", 3), Npc("Everyone", 2), Npc("Vance", 1)] });

        var said = CommsCapability.ChannelPhrases(channels).Select(command => command.Phrase).ToList();

        Assert.DoesNotContain("comms all", said, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("comms everyone", said, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("comms Vance", said);
    }

    [Fact]
    public void AllIsSaidAsCarrierAll()
    {
        var channels = With(new CommsInputs { CarrierOwned = true });
        channels.Select(CommsChannel.Carrier, "all");

        Assert.Equal("Carrier, all.", CommsCapability.Say(channels.Selected!));
    }

    [Fact]
    public void AllIsKeptForEachChannelAndForEachStory()
    {
        CommsInputs Inputs(string story) =>
            new() { CarrierOwned = true, StoryRunning = true, StoryId = story, StoryCast = [Cast("a"), Cast("b")] };

        var channels = With(Inputs("one"));
        channels.Select(CommsChannel.Carrier, "all");
        channels.Select(CommsChannel.Story, "all");

        var restored = new CommsChannels();
        restored.Restore(channels.Memory);
        restored.Update(Inputs("one"));

        Assert.Equal(CommsChannel.Story, restored.Selected!.Channel);
        Assert.True(restored.Selected.IsAll);

        restored.Select(CommsChannel.Carrier);
        Assert.True(restored.Selected!.IsAll);

        restored.Update(Inputs("two"));
        restored.Select(CommsChannel.Story);
        Assert.Equal("A", restored.Selected!.SubChannel!.Name);
    }

    [Fact]
    public void BelowTwoPartiesTheFirstIsSelectedAndAllReturnsWithTheSecond()
    {
        var two = new CommsInputs { Heard = [Npc("Okafor", 3), Npc("Vance", 1)] };
        var channels = With(two);
        channels.Select(CommsChannel.System, "all");

        channels.Update(new CommsInputs { Heard = [Npc("Okafor", 3)] });

        Assert.Equal("Okafor", channels.Selected!.SubChannel!.Name);
        Assert.False(channels.Selected.IsAll);
        Assert.Equal(CommsChannels.AllKey, channels.Memory.Parties[CommsChannel.System]);

        channels.Update(two);

        Assert.True(channels.Selected!.IsAll);
    }

    private sealed class RefusingLine(List<string> seen) : ILine
    {
        public Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken)
        {
            seen.Add(selected.SubChannel!.Key);
            return Task.FromResult<LineDecision>(new LineDecision.Refused($"No answer from {selected.SubChannel.Key}.", TurnRoute.NoOneOnComms));
        }
    }

    private static TurnLoop Loop(Func<CommsSelection?> selected, params ILine[] lines)
    {
        var surface = TestSurface.For(new MemoryInstall());
        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            FakeLlmProvider.Answering("Copy that."))
        {
            CommsSelected = selected,
        };

        loop.Lines.AddRange(lines);
        return loop;
    }

    [Fact]
    public async Task ACommsTurnToAllIsAnsweredByTheFirstPartyWhoseLineTakesIt()
    {
        var commander = new CommanderGameState(new CommanderIdentity("F1", "Tester"))
        {
            Carrier = new CarrierState { CallSign = "BNH-T2F", Name = "Sacred Fire", StarSystem = "Sol" },
        };
        var channels = With(new CommsInputs { CarrierOwned = true });
        channels.Select(CommsChannel.Carrier, "all");
        var loop = Loop(
            () => channels.Selected,
            new CaptainLine(() => commander, () => "Sol", () => DateTimeOffset.UnixEpoch),
            new TowerLine(() => commander, () => "Sol", () => DateTimeOffset.UnixEpoch));

        var events = await OnComms.SayAsync(loop, "are you receiving");

        Assert.Equal(NpcChatter.CaptainName, Assert.Single(events.OfType<TurnEvent.Addressed>()).Name);
    }

    [Fact]
    public async Task WhenEveryLineRefusesAllSaysTheFirstRefusalAndNoLineSeesTheAllKey()
    {
        var channels = With(new CommsInputs { Heard = [Npc("Okafor", 3), Npc("Vance", 1)] });
        channels.Select(CommsChannel.System, "all");
        List<string> seen = [];
        var loop = Loop(() => channels.Selected, new RefusingLine(seen));

        var events = await OnComms.SayAsync(loop, "are you receiving");

        Assert.Equal("No answer from Okafor.", Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);
        Assert.Equal(["Okafor", "Vance"], seen);
        Assert.DoesNotContain(CommsChannels.AllKey, seen);
    }
}
