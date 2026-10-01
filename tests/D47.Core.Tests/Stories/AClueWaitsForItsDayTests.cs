using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Persona;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A clue is due a set number of real days after the beacon scan, at most one per four sessions, and the hidden
/// layer every speaker reads names only the clues already given.
/// </summary>
public sealed class AClueWaitsForItsDayTests
{
    private static readonly Story Scanned = new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        PickedAt = Now.AddDays(-1),
        BeaconScanAt = Now,
    };

    private static JournalEvent LoadGame(DateTimeOffset at) => BeaconFixture.Event(
        $$"""{ "timestamp":"{{at.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"F1", "Commander":"Test" }""");

    [Fact]
    public void TheFirstClueWaitsSevenDaysAfterTheBeacon()
    {
        Assert.Null(StoryClues.Due(Scanned, Now.AddDays(7).AddMinutes(-1)));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(Scanned, Now.AddDays(7)));
    }

    [Fact]
    public void NoClueComesBeforeTheBeaconIsScanned() =>
        Assert.Null(StoryClues.Due(Scanned with { BeaconScanAt = null }, Now.AddDays(400)));

    [Fact]
    public void TheNextClueWaitsForFourMoreSessions()
    {
        var oneGiven = Scanned with { CluesGiven = 1, ClueSession = 2, Sessions = 5 };

        Assert.Null(StoryClues.Due(oneGiven, Now.AddDays(61)));
        Assert.Equal(new StoryClueDue(Id, 1), StoryClues.Due(oneGiven with { Sessions = 6 }, Now.AddDays(61)));
    }

    [Fact]
    public void AGivenClueEntersTheHiddenLayerAndALaterOneDoesNot()
    {
        var before = StoryClues.Brief(Scanned, Secret);

        Assert.Contains(Secret.Secret, before);
        Assert.Contains(StoryClues.Rule, before);
        Assert.DoesNotContain(Secret.Clues[0].Text, before);

        var after = StoryClues.Brief(Scanned with { CluesGiven = 1 }, Secret);

        Assert.Contains(Secret.Clues[0].Text, after);
        Assert.DoesNotContain(Secret.Clues[1].Text, after);
        Assert.DoesNotContain(Secret.Clues[2].Text, after);
    }

    [Fact]
    public async Task TheDirectorCountsSessionsAndMarksAClueGiven()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        fixtures.Director.Observe(BeaconFixture.JumpTo(Beacon, BeaconAddress), "F1");
        fixtures.Director.Observe(BeaconFixture.DataPoint(), "F1");

        var scan = fixtures.Stories.Current("F1")!.BeaconScanAt!.Value;
        var due = fixtures.Director.ClueDue("F1", scan.AddDays(7));

        Assert.Equal(new StoryClueDue(Id, 0), due);
        Assert.Equal((Card.Title, Secret.Clues[0].Text), fixtures.Director.Clue("F1", due!));
        Assert.DoesNotContain(Secret.Clues[0].Text, fixtures.Director.HiddenBrief("F1")!);

        fixtures.Director.ClueGiven("F1", due!);

        Assert.Contains(Secret.Clues[0].Text, fixtures.Director.HiddenBrief("F1")!);
        Assert.Null(fixtures.Director.Clue("F1", due!));
        Assert.Null(fixtures.Director.ClueDue("F1", scan.AddDays(61)));

        for (var session = 1; session <= StoryClues.SessionsApart; session++)
        {
            fixtures.Director.Observe(LoadGame(scan.AddDays(session)), "F1");
        }

        // The backlog replayed when D47 starts again counts nothing twice.
        fixtures.Director.Observe(LoadGame(scan.AddDays(StoryClues.SessionsApart)), "F1");

        Assert.Equal(StoryClues.SessionsApart, fixtures.Stories.Current("F1")!.Sessions);
        Assert.Equal(new StoryClueDue(Id, 1), fixtures.Director.ClueDue("F1", scan.AddDays(61)));
    }

    [Fact]
    public void TheMarkerCarriesNoClueTextAndIsQueuedOncePerRun()
    {
        var callout = new StoryClueCallout(new NearbyFight())
        {
            Due = _ => new StoryClueDue(Id, 0),
            NarratorOn = () => true,
        };

        var docked = GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip, ReadAt = Now };
        var context = new CalloutContext(Now, false, null, docked, NavRoute.None, [], null);

        var marker = Assert.Single(callout.Examine(context));

        Assert.Equal(string.Empty, marker.Text);
        Assert.Equal(VoiceRole.Narrator, marker.Voice);
        Assert.Equal(new StoryClueDue(Id, 0), StoryClueCallout.Parse(marker.Key));
        Assert.Empty(callout.Examine(context with { Now = Now.AddHours(1) }));
    }
}
