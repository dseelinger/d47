using System.IO.Compression;
using System.Text;
using System.Text.Json;
using D47.Core.Tests.Conversation;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A cast member marked <c>recorded</c> is heard only in recordings or messages: never answered, never a reactor (#1727).</summary>
public sealed class ARecordedCastMemberNeverSpeaksLiveTests
{
    private const string DockHand = "dock-hand";

    private static StorySecret With(bool recorded) => Secret with
    {
        Cast = [Secret.Cast[0] with { Primary = true, Recorded = recorded, Role = "the dock hand who signs the Commander's berth" }],
    };

    private static StoryFixtures Picked(bool recorded)
    {
        var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)),
            With(recorded));

        Assert.Null(fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None).GetAwaiter().GetResult());
        fixtures.Director.Introduced("F1", Id, DockHand);
        return fixtures;
    }

    [Fact]
    public void ARecordedMemberIsNotAnswered()
    {
        using var fixtures = Picked(recorded: true);

        Assert.Null(fixtures.Director.Answering("F1", DockHand));
    }

    [Fact]
    public void AMemberNotMarkedRecordedIsAnsweredAsBefore()
    {
        using var fixtures = Picked(recorded: false);

        Assert.NotNull(fixtures.Director.Answering("F1", DockHand));
    }

    [Fact]
    public void ARecordedPrimaryMemberIsNotAReactor()
    {
        using var recorded = Picked(recorded: true);
        using var live = Picked(recorded: false);

        Assert.Empty(recorded.Director.ReactionCast("F1", new StoryReactionDue(Id, 0)));
        Assert.Single(live.Director.ReactionCast("F1", live.Director.ReactionWindow("F1")!.Due));
    }

    [Fact]
    public void ARecordedMemberStaysInTheirStorysCast()
    {
        using var fixtures = Picked(recorded: true);

        var member = Assert.Single(fixtures.Director.PrimaryCast(Id));

        Assert.True(member.Recorded);
    }

    [Fact]
    public void AnEntryWithoutTheFieldLoadsAsNotRecorded()
    {
        var loaded = Assert.Single(StoryCatalog.Unseal(Seal(Json())));

        Assert.False(Assert.Single(loaded.Cast).Recorded);
    }

    [Fact]
    public void ARecordedMemberAboardIsAFault()
    {
        var riding = Secret with
        {
            Cast = [Secret.Cast[0] with { Recorded = true, Aboard = [new StorySpeakerAboard(null, null)] }],
        };

        Assert.Contains(StoryCatalog.Faults(Card, riding), fault => fault.Contains("recorded", StringComparison.Ordinal));
        Assert.DoesNotContain(
            StoryCatalog.Faults(Card, riding with { Cast = [riding.Cast[0] with { Recorded = false }] }),
            fault => fault.Contains("recorded", StringComparison.Ordinal));
    }

    [Fact]
    public void ARecordedThatIsNotABoolDoesNotLoad()
    {
        Assert.ThrowsAny<JsonException>(() => StoryCatalog.Unseal(Seal(Json().Replace("\"id\":\"dock-hand\"", "\"id\":\"dock-hand\",\"recorded\":\"yes\"", StringComparison.Ordinal))));
    }

    private static string Json() => JsonSerializer.Serialize(
        new[] { Secret },
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

    private static string Seal(string json)
    {
        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(Encoding.UTF8.GetBytes(json));
        }

        return Convert.ToBase64String(packed.ToArray());
    }
}
