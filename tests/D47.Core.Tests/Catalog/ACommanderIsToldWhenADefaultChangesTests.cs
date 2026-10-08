using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Conversation;

using Xunit;

namespace D47.Core.Tests.Catalog;

public class ACommanderIsToldWhenADefaultChangesTests
{
    private const string Anthropic = "llm:anthropic";

    private const string ElevenLabs = "speech:elevenlabs";

    [Fact]
    public void AFirstRunRecordsTheDefaultsAndSaysNothing()
    {
        IReadOnlyDictionary<string, string> told = new Dictionary<string, string>();

        var change = DefaultChanges.Find(new D47Settings(), Catalog("claude-a"), ref told);

        Assert.Null(change);
        Assert.Equal("claude-a", told[Anthropic]);
        Assert.Equal("eleven_v4_turbo", told[ElevenLabs]);
    }

    [Fact]
    public void ACommanderWhoNeverChoseIsToldAndCanKeepTheOldOne()
    {
        var told = Told(Anthropic, "claude-a");

        var change = DefaultChanges.Find(new D47Settings(), Catalog("claude-b"), ref told);

        Assert.NotNull(change);
        Assert.Equal("D47 now answers with Claude B.", change.Text);
        Assert.Equal("Keep Claude A", change.ActionLabel);
        Assert.Equal("llm.model", change.SettingKey);
        Assert.Equal("claude-a", change.Value);
        Assert.Equal("claude-a", told[Anthropic]);
    }

    [Fact]
    public void ACommanderOnAnotherModelIsToldAndCanTakeTheNewOne()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Model = "claude-a" } };
        var told = Told(Anthropic, "claude-a");

        var change = DefaultChanges.Find(settings, Catalog("claude-b"), ref told);

        Assert.NotNull(change);
        Assert.Equal("Claude B is the new default. You are on Claude A.", change.Text);
        Assert.Equal("Use it", change.ActionLabel);
        Assert.Equal("claude-b", change.Value);
    }

    [Fact]
    public void AModelWithoutALabelIsNamedByItsId()
    {
        var told = Told(Anthropic, "claude-a");

        var change = DefaultChanges.Find(new D47Settings(), Catalog("claude-plain"), ref told);

        Assert.Equal("D47 now answers with claude-plain.", change!.Text);
    }

    [Fact]
    public void ADefaultAlreadyToldIsNotToldAgain()
    {
        var told = Told(Anthropic, "claude-b");

        Assert.Null(DefaultChanges.Find(new D47Settings(), Catalog("claude-b"), ref told));
    }

    [Fact]
    public void AChoiceThatMatchesTheNewDefaultIsRecordedSilently()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Model = "claude-b" } };
        var told = Told(Anthropic, "claude-a");

        Assert.Null(DefaultChanges.Find(settings, Catalog("claude-b"), ref told));
        Assert.Equal("claude-b", told[Anthropic]);
    }

    [Fact]
    public void AnUnselectedProviderSaysNothing()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Provider = LlmProviderCatalog.OpenAiId } };
        var told = Told(Anthropic, "claude-a");

        Assert.Null(DefaultChanges.Find(settings, Catalog("claude-b"), ref told));
        Assert.Equal("claude-b", told[Anthropic]);
    }

    [Fact]
    public void ElevenLabsIsToldOnlyWhenItSpeaks()
    {
        var told = Told(ElevenLabs, "eleven_v3_conversational");
        var other = new D47Settings { Speech = new SpeechSettings { Provider = "edge" } };

        Assert.Null(DefaultChanges.Find(other, Catalog("claude-a"), ref told));

        told = Told(ElevenLabs, "eleven_v3_conversational");
        var speaking = new D47Settings { Speech = new SpeechSettings { Provider = "elevenlabs" } };

        var change = DefaultChanges.Find(speaking, Catalog("claude-a"), ref told);

        Assert.NotNull(change);
        Assert.Equal("D47 now speaks with v4 Turbo.", change.Text);
        Assert.Equal("Keep v3", change.ActionLabel);
        Assert.Equal("speech.elevenlabs.model", change.SettingKey);
        Assert.Equal("eleven_v3_conversational", change.Value);
    }

    [Fact]
    public void ACommanderWhoseQuietCallsChangeModelIsToldOnceAndCanKeepTheOldOne()
    {
        var told = Told(Anthropic, "claude-a");

        var change = DefaultChanges.Find(new D47Settings(), Catalog("claude-a", "claude-b"), ref told);

        Assert.NotNull(change);
        Assert.Equal("The quiet calls now use Claude B.", change.Text);
        Assert.Equal("Keep Claude A", change.ActionLabel);
        Assert.Equal("llm.backgroundModel", change.SettingKey);
        Assert.Equal("claude-a", change.Value);

        told = new Dictionary<string, string>(told) { [change.Key] = change.Default };

        Assert.Null(DefaultChanges.Find(new D47Settings(), Catalog("claude-a", "claude-b"), ref told));
    }

    [Fact]
    public void AFirstRunIsNotToldAboutTheQuietCalls()
    {
        IReadOnlyDictionary<string, string> told = new Dictionary<string, string>();

        Assert.Null(DefaultChanges.Find(new D47Settings(), Catalog("claude-a", "claude-b"), ref told));
    }

    [Fact]
    public void AChosenQuietModelOrACustomEndpointIsNotToldAboutTheQuietCalls()
    {
        var chosen = new D47Settings { Llm = new LlmSettings { BackgroundModel = "claude-a" } };
        var custom = new D47Settings { Llm = new LlmSettings { Endpoint = "http://localhost:1234" } };

        var told = Told(Anthropic, "claude-a");
        Assert.Null(DefaultChanges.Find(chosen, Catalog("claude-a", "claude-b"), ref told));

        told = Told(Anthropic, "claude-a");
        Assert.Null(DefaultChanges.Find(custom, Catalog("claude-a", "claude-b"), ref told));
    }

    private static IReadOnlyDictionary<string, string> Told(string key, string value) =>
        new Dictionary<string, string> { [key] = value };

    private static ModelCatalog Catalog(string anthropicDefault, string? background = null) => ModelCatalog.Parse($$"""
        {
          "schema": 1,
          "published": "2026-10-06",
          "providers": {
            "anthropic": {
              "default": "{{anthropicDefault}}",
              "backgroundDefault": {{(background is null ? "null" : $"\"{background}\"")}},
              "models": [
                { "id": "claude-a", "label": "Claude A", "offered": true, "price": { "input": 3, "output": 15 } },
                { "id": "claude-b", "label": "Claude B", "offered": true, "price": { "input": 3, "output": 15 } },
                { "id": "claude-plain", "offered": true, "price": { "input": 3, "output": 15 } }
              ]
            },
            "openai": {
              "default": "gpt-a",
              "backgroundDefault": null,
              "models": [ { "id": "gpt-a", "offered": true, "price": { "input": 3, "output": 15 } } ]
            }
          },
          {{SpeechSection.Json()}}
        }
        """);
}
