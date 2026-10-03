using D47.Core.Speech;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A story whose cast needs a voice that is not ready cannot be picked: the pick is refused with one message from the
/// ship listing what is missing, worked out from the cast. There is no fallback voice (#714).
/// </summary>
public sealed class AStoryWithAnUnreadyVoiceCannotBePickedTests
{
    /// <summary>A cast with a Kokoro member and a caller in the Commander's own voice.</summary>
    internal static readonly StorySecret WithOwnVoice = Secret with
    {
        Cast =
        [
            .. Secret.Cast,
            new StorySpeaker { Id = "caller", Name = "The caller", Who = "A frightened pilot.", Provider = StorySpeaker.Chatterbox, Voice = StorySpeaker.Own },
        ],
    };

    internal static StoryFixtures Fixtures(StorySecret secret, Func<CastVoicesHere> here, List<(string Title, string Message)> posted)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)), secret);

        fixtures.Director.VoicesHere = here;
        fixtures.Director.VoicesNotReady += (title, message) => posted.Add((title, message));
        return fixtures;
    }

    [Fact]
    public async Task WithNoRecordingThePickIsRefusedAndOneMessageNamesTheRecording()
    {
        var posted = new List<(string Title, string Message)>();
        using var fixtures = Fixtures(WithOwnVoice, () => new CastVoicesHere(Kokoro: true, Chatterbox: true, OwnRecording: false), posted);

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Equal($"{Card.Title} cannot start until its voices are ready. Record your voice under Settings, Your voice.", refusal);
        Assert.Equal([(Card.Title, refusal!)], posted);
        Assert.Null(fixtures.Stories.Current("F1"));
        Assert.Null(fixtures.Backstory);
        Assert.Equal(0, fixtures.Provider.CallCount);
    }

    [Fact]
    public async Task WithoutChatterboxTheMessageNamesTheDownloadAndItsSize()
    {
        var posted = new List<(string Title, string Message)>();
        using var fixtures = Fixtures(WithOwnVoice, () => new CastVoicesHere(Kokoro: true, Chatterbox: false, OwnRecording: true), posted);

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Contains($"Download Chatterbox, about {ChatterboxAssets.TotalMegabytes:0} MB, under Settings, Its voice.", refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("Record your voice", refusal, StringComparison.Ordinal);
        Assert.Single(posted);
    }

    [Fact]
    public async Task WithoutKokoroAKokoroMemberNamesItsDownload()
    {
        var posted = new List<(string Title, string Message)>();
        using var fixtures = Fixtures(Secret, () => new CastVoicesHere(Kokoro: false, Chatterbox: false, OwnRecording: false), posted);

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Equal(
            $"{Card.Title} cannot start until its voices are ready. Download the Kokoro local voice, about {KokoroAssets.TotalMegabytes:0} MB, under Settings, Its voice.",
            refusal);
    }

    [Fact]
    public async Task WithBothReadyThePickSucceeds()
    {
        var posted = new List<(string Title, string Message)>();
        using var fixtures = Fixtures(WithOwnVoice, () => new CastVoicesHere(Kokoro: true, Chatterbox: true, OwnRecording: true), posted);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Empty(posted);
        Assert.NotNull(fixtures.Stories.Current("F1"));
    }

    [Fact]
    public async Task SwitchingToAnUnreadyStoryKeepsTheCurrentOne()
    {
        var ready = true;
        var posted = new List<(string Title, string Message)>();
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        fixtures.Director.VoicesHere = () => new CastVoicesHere(Kokoro: ready, Chatterbox: true, OwnRecording: true);
        fixtures.Director.VoicesNotReady += (title, message) => posted.Add((title, message));

        Assert.Null(await fixtures.Director.PickAsync("F1", Other.Id, Now, CancellationToken.None));

        ready = false;

        Assert.NotNull(await fixtures.Director.SwitchAsync("F1", Id, Now, CancellationToken.None));
        Assert.Equal(Other.Id, fixtures.Stories.Current("F1")!.Id);
        Assert.Single(posted);
    }

    [Fact]
    public void AMemberWithVersionsIsCheckedInTheCommandersVersion()
    {
        var noChatterbox = new CastVoicesHere(Kokoro: true, Chatterbox: false, OwnRecording: true);

        Assert.Empty(StoryVoices.Missing(Versioned, CommanderGender.Man, noChatterbox));
        Assert.Single(StoryVoices.Missing(Versioned, CommanderGender.Woman, noChatterbox));
    }
}
