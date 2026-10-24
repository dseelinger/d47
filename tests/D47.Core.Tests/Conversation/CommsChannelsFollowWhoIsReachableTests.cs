using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Conversation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class CommsChannelsFollowWhoIsReachableTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static NpcHeard Npc(string name, int minutes) =>
        new(name, NpcKind.Invented, null, null, Start.AddMinutes(minutes));

    private static CustomCharacter Character(string id, string name) => new(id, name, null, null, true, null);

    private static ShipCrew Crew(params CrewMember[] members) => new() { Members = members };

    private static TargetedShip NpcTarget(string? pilot = "Mara Okafor") => new("Cobra Mk III", pilot);

    private static TargetedShip CommanderTarget() => new("Krait Mk II", IsCommander: true);

    private static CommsChannels With(CommsInputs inputs)
    {
        var channels = new CommsChannels();
        channels.Update(inputs);
        return channels;
    }

    private static string? Party(CommsChannels channels) => channels.Selected?.SubChannel?.Name;

    [Fact]
    public void AnOwnedCarrierOffersTheCaptainAndTheTower()
    {
        var channels = With(new CommsInputs { CarrierOwned = true });

        Assert.Equal([CommsChannel.Carrier], channels.Available);
        Assert.Equal(["Captain", "Tower"], channels.SubChannels(CommsChannel.Carrier).Select(party => party.Name));
    }

    [Fact]
    public void NoCarrierOffersNoCarrierChannel()
    {
        var channels = With(new CommsInputs { Heard = [Npc("Okafor", 0)] });

        Assert.DoesNotContain(CommsChannel.Carrier, channels.Available);
        Assert.Empty(channels.SubChannels(CommsChannel.Carrier));
    }

    [Fact]
    public void AnNpcTargetOffersTheTargetByPilotName()
    {
        var channels = With(new CommsInputs { Target = NpcTarget() });

        Assert.Equal([CommsChannel.Target], channels.Available);
        Assert.Equal("Mara Okafor", Party(channels));
    }

    [Fact]
    public void AnUnscannedNpcTargetIsOfferedByItsShip()
    {
        var channels = With(new CommsInputs { Target = NpcTarget(pilot: null) });

        Assert.Equal("Cobra Mk III", Party(channels));
    }

    [Fact]
    public void ACommanderTargetNeverMakesTheTargetAvailable()
    {
        var channels = With(new CommsInputs { Target = CommanderTarget() });

        Assert.Empty(channels.Available);
        Assert.Null(channels.Selected);
        Assert.False(channels.Select(CommsChannel.Target));
    }

    [Fact]
    public void TheSystemChannelListsTheRosterNewestFirst()
    {
        var channels = With(new CommsInputs { Heard = [Npc("Vance", 5), Npc("Okafor", 2)] });

        Assert.Equal([CommsChannel.System], channels.Available);
        Assert.Equal(["Vance", "Okafor"], channels.SubChannels(CommsChannel.System).Select(party => party.Name));
        Assert.Equal("Vance", Party(channels));
    }

    [Fact]
    public void ARunningStoryOffersItsIntroducedCast()
    {
        var channels = With(new CommsInputs
        {
            StoryRunning = true,
            StoryCast = [new AdventureSpeaker("ilse", "Ilse Varga", "a salvager"), new AdventureSpeaker("rho", "Rho", "a broker")],
        });

        Assert.Equal([CommsChannel.Story], channels.Available);
        Assert.Equal(["Ilse Varga", "Rho"], channels.SubChannels(CommsChannel.Story).Select(party => party.Name));
    }

    [Fact]
    public void ARunningStoryWithNoOneMetIsAvailableWithNoOne()
    {
        var channels = With(new CommsInputs { StoryRunning = true });

        Assert.Equal([CommsChannel.Story], channels.Available);
        Assert.True(channels.Selected!.IsNoOne);
    }

    [Fact]
    public void NoRunningStoryOffersNoStoryChannelWhateverTheCast()
    {
        var channels = With(new CommsInputs
        {
            StoryCast = [new AdventureSpeaker("ilse", "Ilse Varga", "a salvager")],
            NarratorAnswers = true,
        });

        Assert.Empty(channels.Available);
    }

    [Fact]
    public void TheNarratorIsOfferedOnlyWhenItAnswers()
    {
        var cast = new AdventureSpeaker("ilse", "Ilse Varga", "a salvager");

        var off = With(new CommsInputs { StoryRunning = true, StoryCast = [cast] });
        var on = With(new CommsInputs { StoryRunning = true, StoryCast = [cast], NarratorAnswers = true });

        Assert.Equal(["Ilse Varga"], off.SubChannels(CommsChannel.Story).Select(party => party.Name));
        Assert.Equal(["Ilse Varga", "Narrator"], on.SubChannels(CommsChannel.Story).Select(party => party.Name));
    }

    [Fact]
    public void TheCrewChannelOffersOnlyTheActivePilot()
    {
        var channels = With(new CommsInputs
        {
            Crew = Crew(new CrewMember("Jo Hale", 1), new CrewMember("Sam Kerr", 2, Active: true)),
        });

        Assert.Equal([CommsChannel.Crew], channels.Available);
        Assert.Equal(["Sam Kerr"], channels.SubChannels(CommsChannel.Crew).Select(party => party.Name));
    }

    [Fact]
    public void ACrewOnShoreLeaveOffersNoCrewChannel()
    {
        var channels = With(new CommsInputs { Crew = Crew(new CrewMember("Jo Hale", 1)) });

        Assert.Empty(channels.Available);
    }

    [Fact]
    public void CustomCharactersAreOfferedOnTheirChannel()
    {
        var channels = With(new CommsInputs { Characters = [Character("0a1b2c3d", "Wren"), Character("4e5f6a7b", "Tamsin Holt")] });

        Assert.Equal([CommsChannel.Characters], channels.Available);
        Assert.Equal(["Wren", "Tamsin Holt"], channels.SubChannels(CommsChannel.Characters).Select(party => party.Name));
    }

    [Fact]
    public void AtLaunchTheSelectionIsTheFirstAvailableChannel()
    {
        var channels = new CommsChannels();
        Assert.Null(channels.Selected);

        channels.Update(new CommsInputs { Heard = [Npc("Okafor", 0)], Characters = [Character("0a1b2c3d", "Wren")] });

        Assert.Equal(new CommsSelection(CommsChannel.System, new CommsSubChannel("Okafor", "Okafor")), channels.Selected);
    }

    [Fact]
    public void ALaterChannelDoesNotMoveTheSelection()
    {
        var channels = With(new CommsInputs { Characters = [Character("0a1b2c3d", "Wren")] });

        channels.Update(new CommsInputs { CarrierOwned = true, Characters = [Character("0a1b2c3d", "Wren")] });

        Assert.Equal(CommsChannel.Characters, channels.Selected!.Channel);
    }

    [Fact]
    public void ChannelUpAndDownSkipUnavailableChannelsAndWrap()
    {
        var channels = With(new CommsInputs
        {
            CarrierOwned = true,
            StoryRunning = true,
            Characters = [Character("0a1b2c3d", "Wren")],
        });

        Assert.Equal(CommsChannel.Carrier, channels.Selected!.Channel);

        channels.NextChannel();
        Assert.Equal(CommsChannel.Story, channels.Selected!.Channel);

        channels.NextChannel();
        Assert.Equal(CommsChannel.Characters, channels.Selected!.Channel);

        channels.NextChannel();
        Assert.Equal(CommsChannel.Carrier, channels.Selected!.Channel);

        channels.PreviousChannel();
        Assert.Equal(CommsChannel.Characters, channels.Selected!.Channel);
    }

    [Fact]
    public void EnteringAChannelSelectsItsFirstParty()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Vance", 5), Npc("Okafor", 2)] });

        channels.NextChannel();

        Assert.Equal("Vance", Party(channels));
    }

    [Fact]
    public void SubChannelUpAndDownWrapWithinTheChannel()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 0)] });

        Assert.Equal("Captain", Party(channels));

        channels.NextSubChannel();
        Assert.Equal("Tower", Party(channels));

        channels.NextSubChannel();
        Assert.Equal("Captain", Party(channels));

        channels.PreviousSubChannel();
        Assert.Equal("Tower", Party(channels));
        Assert.Equal(CommsChannel.Carrier, channels.Selected!.Channel);
    }

    [Fact]
    public void TheSelectedNpcStaysSelectedWhenAnotherIsHeard()
    {
        var channels = With(new CommsInputs { Heard = [Npc("Vance", 5), Npc("Okafor", 2)] });
        channels.NextSubChannel();
        Assert.Equal("Okafor", Party(channels));

        channels.Update(new CommsInputs { Heard = [Npc("Brand", 7), Npc("Vance", 5), Npc("Okafor", 2)] });

        Assert.Equal("Okafor", Party(channels));
    }

    [Theory]
    [InlineData("tower", "Tower")]
    [InlineData("CAPTAIN", "Captain")]
    public void SelectingByNameIgnoresCase(string asked, string selected)
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 0)] });
        channels.Select(CommsChannel.System);

        Assert.True(channels.Select(CommsChannel.Carrier, asked));

        Assert.Equal(new CommsSelection(CommsChannel.Carrier, channels.SubChannels(CommsChannel.Carrier).Single(party => party.Name == selected)), channels.Selected);
    }

    [Fact]
    public void SelectingByLastWordFindsTheFullName()
    {
        var channels = With(new CommsInputs { Characters = [Character("0a1b2c3d", "Wren"), Character("4e5f6a7b", "Tamsin Holt")] });

        Assert.True(channels.Select(CommsChannel.Characters, "holt"));

        Assert.Equal("Tamsin Holt", Party(channels));
    }

    [Fact]
    public void AWholeNameBeatsALastWord()
    {
        var channels = With(new CommsInputs { Characters = [Character("0a1b2c3d", "Ada Holt"), Character("4e5f6a7b", "Holt")] });

        Assert.True(channels.Select(CommsChannel.Characters, "Holt"));

        Assert.Equal("4e5f6a7b", channels.Selected!.SubChannel!.Key);
    }

    [Fact]
    public void TwoSharingALastWordSelectTheFirstListed()
    {
        var channels = With(new CommsInputs { Heard = [Npc("Ada Vance", 5), Npc("Kit Vance", 2)] });
        channels.NextSubChannel();

        Assert.True(channels.Select(CommsChannel.System, "Vance"));

        Assert.Equal("Ada Vance", Party(channels));
    }

    [Fact]
    public void SelectingWithoutAPartyTakesTheChannelsFirst()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Vance", 5), Npc("Okafor", 2)] });

        Assert.True(channels.Select(CommsChannel.System));

        Assert.Equal("Vance", Party(channels));
    }

    [Fact]
    public void ANameThatMatchesNoOneLeavesTheSelection()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Characters = [Character("0a1b2c3d", "Wren")] });

        Assert.False(channels.Select(CommsChannel.Characters, "Holt"));
        Assert.False(channels.Select(CommsChannel.Crew));

        Assert.Equal("Captain", Party(channels));
    }

    [Fact]
    public void AHailSelectsTheHailer()
    {
        var channels = With(new CommsInputs { CarrierOwned = true });

        channels.Update(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 3), Npc("Vance", 1)] });
        Assert.True(channels.Hailed("vance"));

        Assert.Equal(new CommsSelection(CommsChannel.System, new CommsSubChannel("Vance", "Vance")), channels.Selected);
    }

    [Fact]
    public void AHailFromSomeoneNotOnTheRosterLeavesTheSelection()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 3)] });

        Assert.False(channels.Hailed("Vance"));

        Assert.Equal("Captain", Party(channels));
    }

    [Fact]
    public void AStrangerSigningOffLeavesTheChannelWithNoOne()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 3), Npc("Vance", 1)] });
        channels.Hailed("Vance");

        channels.Update(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 3)] });

        Assert.Equal(new CommsSelection(CommsChannel.System, null), channels.Selected);
    }

    [Fact]
    public void ADroppedTargetLeavesTheChannelWithNoOne()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Target = NpcTarget() });
        channels.Select(CommsChannel.Target);

        channels.Update(new CommsInputs { CarrierOwned = true });

        Assert.Equal(new CommsSelection(CommsChannel.Target, null), channels.Selected);
        Assert.DoesNotContain(CommsChannel.Target, channels.Available);
    }

    [Fact]
    public void TargetingACommanderLeavesTheTargetChannelWithNoOne()
    {
        var channels = With(new CommsInputs { Target = NpcTarget() });

        channels.Update(new CommsInputs { Target = CommanderTarget() });

        Assert.Equal(new CommsSelection(CommsChannel.Target, null), channels.Selected);
    }

    [Fact]
    public void TheTargetChannelFollowsANewTarget()
    {
        var channels = With(new CommsInputs { Target = NpcTarget() });

        channels.Update(new CommsInputs { Target = NpcTarget("Ivo Brandt") });

        Assert.Equal("Ivo Brandt", Party(channels));
    }

    [Fact]
    public void ChannelUpFromAChannelThatWentAwayMovesOn()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 3)], Characters = [Character("0a1b2c3d", "Wren")] });
        channels.Select(CommsChannel.System);
        channels.Update(new CommsInputs { CarrierOwned = true, Characters = [Character("0a1b2c3d", "Wren")] });

        channels.NextChannel();
        Assert.Equal(CommsChannel.Characters, channels.Selected!.Channel);

        channels.Select(CommsChannel.Carrier);
        channels.Update(new CommsInputs { CarrierOwned = true, Target = NpcTarget(), Characters = [Character("0a1b2c3d", "Wren")] });
        channels.Select(CommsChannel.Target);
        channels.Update(new CommsInputs { CarrierOwned = true, Characters = [Character("0a1b2c3d", "Wren")] });

        channels.PreviousChannel();
        Assert.Equal(CommsChannel.Carrier, channels.Selected!.Channel);
    }

    [Fact]
    public void ChangedIsRaisedWhenTheSelectionOrTheAvailableSetChanges()
    {
        var channels = new CommsChannels();
        var raised = 0;
        channels.Changed += () => raised++;

        channels.Update(new CommsInputs { CarrierOwned = true });
        Assert.Equal(1, raised);

        channels.NextSubChannel();
        Assert.Equal(2, raised);

        channels.Update(new CommsInputs { CarrierOwned = true, Characters = [Character("0a1b2c3d", "Wren")] });
        Assert.Equal(3, raised);
    }

    [Fact]
    public void AnUpdateThatChangesNothingRaisesNothing()
    {
        var channels = With(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 3)] });
        var raised = 0;
        channels.Changed += () => raised++;

        channels.Update(new CommsInputs { CarrierOwned = true, Heard = [Npc("Okafor", 9)] });
        channels.NextChannel();
        channels.NextChannel();
        channels.Select(CommsChannel.Carrier, "Holt");

        Assert.Equal(2, raised);
    }
}
