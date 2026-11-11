using System.Globalization;
using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class ANpcThatRepeatsItselfQuipsThenGoesQuietTests
{
    private static readonly DateTimeOffset Start = new(2026, 2, 10, 9, 0, 0, TimeSpan.Zero);

    private const string Liner = "$ShipName_CruiseLiner;";
    private const string Line = "$CruiseLiner_SCPatrol01;";

    private static JournalEvent Event(string kind, DateTimeOffset at, Dictionary<string, object?>? fields = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["timestamp"] = at.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["event"] = kind,
        };

        foreach (var (name, value) in fields ?? [])
        {
            body[name] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(body), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Said(string from, string message, DateTimeOffset at) =>
        Event("ReceiveText", at, new()
        {
            ["From"] = from,
            ["Message"] = message,
            ["Message_Localised"] = "This Liner is in the top one percent.",
            ["Channel"] = "npc",
        });

    private static IncomingMessages Reader() => new() { Enabled = () => true, IncludeNpcs = () => true };

    private static bool IsCanned(Announcement? read) =>
        read is not null && read.Key.StartsWith(IncomingMessages.CannedKeyPrefix, StringComparison.Ordinal);

    private static bool IsQuip(Announcement? read) =>
        read is not null && read.Key.StartsWith(IncomingMessages.QuipKeyPrefix, StringComparison.Ordinal);

    [Fact]
    public void TheThirdTimeALineIsSaidTheSpeakerQuipsInItsOwnVoice()
    {
        var reader = Reader();

        var first = reader.Read(Said(Liner, Line, Start));
        var second = reader.Read(Said(Liner, Line, Start.AddSeconds(10)));
        var third = reader.Read(Said(Liner, Line, Start.AddSeconds(20)));

        Assert.True(IsCanned(first));
        Assert.True(IsCanned(second));
        Assert.True(IsQuip(third));
        Assert.Equal(first!.Speaker, third!.Speaker);
        Assert.Equal(first.Voice, third.Voice);
        Assert.Equal(first.Text, third.RepeatedLine);
        Assert.NotEqual(first.Text, third.Text);
    }

    [Fact]
    public void AFourthAndLaterOccurrenceGivesNothing()
    {
        var reader = Reader();

        for (var i = 0; i < 3; i++)
        {
            Assert.NotNull(reader.Read(Said(Liner, Line, Start.AddSeconds(i))));
        }

        Assert.Null(reader.Read(Said(Liner, Line, Start.AddSeconds(3))));
        Assert.Null(reader.Read(Said(Liner, Line, Start.AddSeconds(4))));
    }

    [Fact]
    public void EveryLineFromASpeakerAfterTheQuipGivesNothing()
    {
        var reader = Reader();

        for (var i = 0; i < 3; i++)
        {
            reader.Read(Said(Liner, Line, Start.AddSeconds(i)));
        }

        for (var n = 2; n <= 10; n++)
        {
            Assert.Null(reader.Read(Said(Liner, $"$CruiseLiner_SCPatrol{n:00};", Start.AddSeconds(10 + n))));
        }
    }

    [Fact]
    public void ATenLinePatrolSpeaksEightLinesThenQuipsThenGoesQuiet()
    {
        var reader = Reader();
        const string patrol = "$ShipName_Military_Independent;";

        for (var i = 0; i < 8; i++)
        {
            Assert.True(IsCanned(reader.Read(Said(patrol, $"$Military_Passthrough{i + 1:00};", Start.AddSeconds(i)))));
        }

        reader.Read(Said(patrol, "$Military_Passthrough01;", Start.AddSeconds(20)));
        var quip = reader.Read(Said(patrol, "$Military_Passthrough01;", Start.AddSeconds(21)));
        Assert.True(IsQuip(quip));
        Assert.Null(reader.Read(Said(patrol, "$Military_Passthrough09;", Start.AddSeconds(22))));
        Assert.Null(reader.Read(Said(patrol, "$Military_Passthrough10;", Start.AddSeconds(23))));
    }

    [Fact]
    public void ASecondSpeakerIsUnaffectedByTheFirstSpeakersQuip()
    {
        var reader = Reader();

        for (var i = 0; i < 3; i++)
        {
            reader.Read(Said(Liner, Line, Start.AddSeconds(i)));
        }

        Assert.True(IsCanned(reader.Read(Said("$ShipName_CruiseLiner2;", "$CruiseLiner_SCPatrol05;", Start.AddSeconds(5)))));
    }

    [Fact]
    public void ALineSaidDuringTheStartupReplayIsCountedButNeverAnnounced()
    {
        var reader = Reader();
        var primed = new CalloutContext(
            Start, IsPriming: true, null, GameStatus.Unknown, new NavRoute(),
            [Said(Liner, Line, Start), Said(Liner, Line, Start.AddSeconds(1)), Said(Liner, Line, Start.AddSeconds(2))]);

        Assert.Empty(reader.Examine(primed));
        Assert.Null(reader.Read(Said(Liner, "$CruiseLiner_SCPatrol07;", Start.AddSeconds(10))));
    }

    [Fact]
    public void TheSameLineFromTwoSpeakersIsCountedSeparately()
    {
        var reader = Reader();

        for (var i = 0; i < 2; i++)
        {
            reader.Read(Said(Liner, Line, Start.AddSeconds(i)));
        }

        Assert.True(IsCanned(reader.Read(Said("$ShipName_CruiseLiner2;", Line, Start.AddSeconds(5)))));
    }

    [Fact]
    public void ALineIsTheSameWhateverItsParameters()
    {
        var reader = Reader();

        reader.Read(Said("$ShipName_Pirate;", "$Pirate_ReminderUnits01:#units=4;", Start));
        reader.Read(Said("$ShipName_Pirate;", "$Pirate_ReminderUnits01:#units=2;", Start.AddSeconds(5)));
        var third = reader.Read(Said("$ShipName_Pirate;", "$Pirate_ReminderUnits01:#units=9;", Start.AddSeconds(10)));

        Assert.True(IsQuip(third));
    }

    [Fact]
    public void ASpeakerQuietForFiveMinutesStartsAgain()
    {
        var reader = Reader();

        for (var i = 0; i < 4; i++)
        {
            reader.Read(Said(Liner, Line, Start.AddSeconds(i)));
        }

        Assert.True(IsCanned(reader.Read(Said(Liner, Line, Start.AddMinutes(6)))));
    }

    [Theory]
    [InlineData("FSDJump")]
    [InlineData("CarrierJump")]
    public void AJumpStartsTheCountAgain(string jump)
    {
        var reader = Reader();

        for (var i = 0; i < 4; i++)
        {
            reader.Read(Said(Liner, Line, Start.AddSeconds(i)));
        }

        var examined = reader.Examine(new CalloutContext(
            Start.AddSeconds(11),
            false,
            null,
            GameStatus.Unknown,
            NavRoute.None,
            [Event(jump, Start.AddSeconds(10)), Said(Liner, Line, Start.AddSeconds(11))])).ToList();

        Assert.True(IsCanned(Assert.Single(examined)));
    }

    [Fact]
    public void TheSameJournalGivesTheSameQuips()
    {
        string?[] Run()
        {
            var reader = Reader();
            return [.. Enumerable.Range(0, 3).Select(i => reader.Read(Said(Liner, Line, Start.AddSeconds(i * 7)))?.Text)];
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void StationNoticesAndPlayerChannelsAreNeverCounted()
    {
        var reader = Reader();

        for (var i = 0; i < 6; i++)
        {
            Assert.True(IsCanned(reader.Read(Said("$STATION_Traffic;", "$STATION_Greeting01;", Start.AddSeconds(i)))));
            Assert.NotNull(reader.Read(Event("ReceiveText", Start.AddSeconds(i), new()
            {
                ["From"] = "Vex",
                ["Message"] = "hello",
                ["Channel"] = "local",
            })));
        }
    }

    [Fact]
    public void TheQuipBriefKeepsTheSpeakerAndNamesTheLine()
    {
        var reader = Reader();
        Announcement? quip = null;

        for (var i = 0; i < 3; i++)
        {
            quip = reader.Read(Said("$ShipName_Pirate;", "$Pirate_Reminder01;", Start.AddSeconds(i)));
        }

        var brief = FlavourBriefs.For(quip!, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Equal(CannedSpeakers.For("Pirate"), brief.Speaker);
        Assert.Contains(quip!.RepeatedLine!, brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("lightly humorous", brief.Instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("dry", brief.Instruction, StringComparison.Ordinal);
        Assert.True(new RewordChance().ShouldReword(quip, 0));
        Assert.Null(FlavourBriefs.For(quip, personalityEnabled: false));
    }
}
