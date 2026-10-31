using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Conversation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class CommsChannelsRememberTheirLastPartyTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static NpcHeard Npc(string name, int minutes) =>
        new(name, NpcKind.Invented, null, null, Start.AddMinutes(minutes));

    private static CustomCharacter Character(string id, string name) => new(id, name, null, null, true, null);

    private static AdventureSpeaker Cast(string id) => new(id, id.ToUpperInvariant(), string.Empty);

    private static CommsInputs Story(string storyId, params string[] cast) =>
        new() { StoryRunning = true, StoryId = storyId, StoryCast = [.. cast.Select(Cast)] };

    private static CommsChannels With(CommsInputs inputs)
    {
        var channels = new CommsChannels();
        channels.Update(inputs);
        return channels;
    }

    private static string? Party(CommsChannels channels) => channels.Selected?.SubChannel?.Name;

    [Fact]
    public void CyclingBackToAChannelSelectsWhoYouLeftOnIt()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 0)] });
        channels.NextSubChannel();
        Assert.Equal("Tower", Party(channels));

        channels.NextChannel();
        channels.NextChannel();

        Assert.Equal(CommsChannel.Carrier, channels.Selected?.Channel);
        Assert.Equal("Tower", Party(channels));
    }

    [Fact]
    public void LeavingCharactersAndComingBackSelectsTheSameCharacter()
    {
        var channels = With(new CommsInputs
        {
            CarrierOwned = true,
            Characters = [Character("0a1b2c3d", "Wren"), Character("4e5f6a7b", "Tamsin")],
        });
        channels.Select(CommsChannel.Characters, "Tamsin");

        channels.Select(CommsChannel.Carrier);
        channels.Select(CommsChannel.Characters);

        Assert.Equal("Tamsin", Party(channels));
    }

    [Fact]
    public void AHailedNpcIsRememberedOnSystem()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 3), Npc("Vance", 1)] });
        channels.Hailed("Vance");

        channels.Select(CommsChannel.Carrier);
        channels.Select(CommsChannel.System);

        Assert.Equal("Vance", Party(channels));
    }

    [Fact]
    public void AStrangerWhoSignsOffAndIsHeardAgainIsSelectedAgain()
    {
        var both = new CommsInputs { Heard = [Npc("Okafor", 3), Npc("Vance", 1)] };
        var channels = With(both);
        channels.Hailed("Vance");

        channels.Update(new CommsInputs { Heard = [Npc("Okafor", 3)] });
        Assert.Equal(new CommsSelection(CommsChannel.System, null), channels.Selected);

        channels.Update(both);

        Assert.Equal("Vance", Party(channels));
    }

    [Fact]
    public void EachStoryRemembersItsOwnCastMember()
    {
        var channels = With(Story("one", "a", "b"));
        channels.NextSubChannel();
        Assert.Equal("B", Party(channels));

        channels.Update(Story("two", "c", "d"));
        Assert.Equal("C", Party(channels));

        channels.Update(Story("one", "a", "b"));
        Assert.Equal("B", Party(channels));
    }

    [Fact]
    public void ASwitchBetweenStoriesRaisesChanged()
    {
        var channels = With(Story("one", "a", "b"));
        var raised = 0;
        channels.Changed += () => raised++;

        channels.Update(Story("two", "c", "d"));

        Assert.True(raised > 0);
    }

    [Fact]
    public void ARememberedPartyWhoIsGoneIsPassedOver()
    {
        var channels = With(new CommsInputs
        {
            CarrierOwned = true,
            Characters = [Character("0a1b2c3d", "Wren"), Character("4e5f6a7b", "Tamsin")],
        });
        channels.Select(CommsChannel.Characters, "Tamsin");
        channels.Select(CommsChannel.Carrier);

        channels.Update(new CommsInputs { CarrierOwned = true, Characters = [Character("0a1b2c3d", "Wren")] });
        channels.Select(CommsChannel.Characters);

        Assert.Equal("Wren", Party(channels));
    }

    [Fact]
    public void RestoringAnUnavailableChannelReadsNoOneUntilItsPartyIsFed()
    {
        var channels = new CommsChannels();
        channels.Restore(new CommsMemory
        {
            Channel = CommsChannel.Carrier,
            Parties = new Dictionary<CommsChannel, string> { [CommsChannel.Carrier] = CommsChannels.TowerKey },
        });

        Assert.Equal(new CommsSelection(CommsChannel.Carrier, null), channels.Selected);

        channels.Update(new CommsInputs { CarrierOwned = true });

        Assert.Equal(CommsChannel.Carrier, channels.Selected?.Channel);
        Assert.Equal("Tower", Party(channels));
    }

    [Fact]
    public void RestoringNothingReturnsToTheLaunchState()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 0)] });
        channels.Select(CommsChannel.System);

        channels.Restore(null);
        Assert.Null(channels.Selected);

        channels.Update(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 0)] });

        Assert.Equal(CommsChannel.Carrier, channels.Selected?.Channel);
        Assert.Equal("Captain", Party(channels));
    }

    [Fact]
    public void AMemoryRestoredIntoANewInstanceGivesTheSameSelection()
    {
        var inputs = new CommsInputs
        {
            CarrierOwned = true,
            Heard = [Npc("Okafor", 3), Npc("Vance", 1)],
            Characters = [Character("0a1b2c3d", "Wren"), Character("4e5f6a7b", "Tamsin")],
        };
        var channels = With(inputs);
        channels.NextSubChannel();
        channels.Select(CommsChannel.Characters, "Tamsin");
        channels.Hailed("Vance");

        var restored = new CommsChannels();
        restored.Restore(channels.Memory);
        restored.Update(inputs);

        Assert.Equal(channels.Selected, restored.Selected);
    }

    [Fact]
    public void RestoreAndEveryChangeToTheMemoryRaiseChanged()
    {
        var channels = With(new CommsInputs { CarrierOwned = true });
        var raised = 0;
        channels.Changed += () => raised++;

        channels.NextSubChannel();
        Assert.Equal(1, raised);

        channels.Restore(new CommsMemory { Channel = CommsChannel.Carrier });
        Assert.Equal(2, raised);
    }
}
