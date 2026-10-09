using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Messages;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A beat holds up to three lines, each with its own speaker, said and posted in order (#867).</summary>
[Trait("Category", "Integration")]
public sealed class ABeatCanCarryACastLineAndANarratorLineTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-beat-lines", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static readonly StorySecret Cast = Secret with
    {
        Cast =
        [
            .. Secret.Cast,
            new StorySpeaker { Id = "caller", Name = "The caller", Who = "A frightened pilot on an open channel.", Provider = StorySpeaker.Kokoro, Voice = "bm_lewis" },
        ],
    };

    private const string BeatsWithTwoLines = """
        {"opening": "A voice on the open channel.", "openingSpeaker": "narrator", "reply": "Here it is.", "beats": [
          {"title": "Yard Paint", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "lines": [
            {"text": "Anyone. Please.", "speaker": "caller"},
            {"text": "The channel hisses, and the yard lights stay on.", "speaker": "narrator"}
          ]},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "lines": [{"text": "To one name.", "speaker": "ship"}]},
          {"title": "The Beacon", "function": "resolution", "kind": "beacon", "system": "Ossen's Lantern", "line": "Scan it."}
        ]}
        """;

    private static RoundScriptedLlmProvider Scripted(string beats) => new(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(beats));

    [Fact]
    public async Task TheCastLineAndTheNarratorLineAreSaidInOrderInTwoVoicesAndPostedAsTwoMessages()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsWithTwoLines), Cast);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var key = fixtures.Stories.Current("F1")!.CurrentChapter!;
        var chapter = fixtures.Book.Store.Find("F1", key)!;
        var said = PauseSupport.Said(new AdventureCallout(fixtures.Book), Now.AddMinutes(1), PauseSupport.FirstBeat(fixtures, key, Now.AddMinutes(1)))
            .Where(announcement => AdventureCallout.Spoken(announcement.Key) is { Beat: 0 }
                || announcement.Key == $"{AdventureCallout.HandOffPrefix}{key}.0")
            .ToList();

        Assert.Equal(
            [$"adventure.{key}.0", $"{AdventureCallout.LinePrefix}{key}.0.1", $"{AdventureCallout.HandOffPrefix}{key}.0"],
            said.Select(announcement => announcement.Key));
        Assert.Equal("Anyone. Please.", said[0].Text);
        Assert.Equal("The channel hisses, and the yard lights stay on.", said[1].Text);
        Assert.StartsWith("Next: dock at Maren Anchorage", said[2].Text, StringComparison.Ordinal);

        var voices = said.Take(2)
            .Select(announcement => AdventureCallout.Spoken(announcement.Key)!.Value)
            .Select(spoken => fixtures.Director.LineVoice("F1", chapter.SpeakerOf(spoken.Beat, spoken.Line))!)
            .ToList();

        Assert.Equal("The caller", voices[0].Cast?.Name);
        Assert.Equal(VoiceRole.Narrator, voices[1].Role);
        Assert.NotEqual(voices[0].Role, voices[1].Role);

        var messages = new MessageStore(Path.Combine(_folder, "messages.json"), new MemoryFileSystem(), NullLogger<MessageStore>.Instance);

        foreach (var (announcement, voice) in said.Take(2).Zip(voices))
        {
            AdventureMessages.Post(messages, voice.From, chapter, key, 0, announcement.Text, Now, cast: voice.Cast?.Picture);
        }

        Assert.Equal(["Anyone. Please.", "The channel hisses, and the yard lights stay on."], messages.All.OrderBy(message => message.Sent).Select(message => message.Body));
        Assert.Equal(2, messages.All.Select(message => message.From).Distinct().Count());
    }

    [Fact]
    public async Task ABeatsLinesAreReadBackFromDiskUnchanged()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsWithTwoLines), Cast);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var key = fixtures.Stories.Current("F1")!.CurrentChapter!;
        var reopened = new AdventureStore(fixtures.AdventuresPath, NullLogger<AdventureStore>.Instance);
        Assert.True(reopened.Poll());

        Assert.Equal(fixtures.Book.Store.Find("F1", key)!.Beats, reopened.Find("F1", key)!.Beats);
        Assert.Equal(
            [new AdventureLine { Text = "Anyone. Please.", Speaker = "caller" }, new AdventureLine { Text = "The channel hisses, and the yard lights stay on.", Speaker = "narrator" }],
            reopened.Find("F1", key)!.Beats[0].Lines);
    }

    [Fact]
    public void AFileWrittenWithOneLineAndSpeakerPerBeatStillLoads()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "adventures.json");
        File.WriteAllText(path, """
            {"commanders": [{"frontierId": "F1", "adventures": [{"key": "old", "name": "Old", "beats": [
              {"title": "Yard Paint", "trigger": {"kind": "arrive", "systemAddress": 10477373803}, "line": "Anyone. Please.", "speaker": "caller"}
            ]}]}]}
            """);

        var store = new AdventureStore(path, NullLogger<AdventureStore>.Instance);
        store.Poll();

        var beat = Assert.Single(Assert.Single(store.For("F1")).Beats);
        Assert.Equal([new AdventureLine { Text = "Anyone. Please.", Speaker = "caller" }], beat.Lines);
    }

    [Fact]
    public async Task AnUnknownSpeakerInALaterLineIsRefused()
    {
        var stranger = BeatsWithTwoLines.Replace("\"speaker\": \"narrator\"}", "\"speaker\": \"stranger\"}", StringComparison.Ordinal);
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine),
                RoundScriptedLlmProvider.Saying(stranger),
                RoundScriptedLlmProvider.Saying(stranger)),
            Cast);

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.NotNull(refusal);
        Assert.Contains("\"stranger\"", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheChapterWriterIsAskedForALineListSpokenInOrder()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsWithTwoLines), Cast);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var prompt = fixtures.Provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("\"lines\": [{\"text\": string, \"speaker\": string}]", prompt, StringComparison.Ordinal);
        Assert.Contains("An objective's lines are spoken in the order given.", prompt, StringComparison.Ordinal);
        Assert.Contains("only where a cast member speaks and the scene also needs narrating", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ABeatWithMoreThanThreeLinesIsRefused()
    {
        var line = new AdventureLine { Text = "A line." };
        var adventure = new Adventure
        {
            Key = "four",
            Name = "Four",
            Beats = [new AdventureBeat { Title = "Too many", Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1 }, Lines = [line, line, line, line] }],
        };

        Assert.Contains(AdventureValidation.Problems(adventure), problem => problem.Contains("4 lines; at most 3", StringComparison.Ordinal));
    }
}
