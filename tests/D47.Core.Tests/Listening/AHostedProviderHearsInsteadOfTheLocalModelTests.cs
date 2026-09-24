using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>The hearing provider setting: the model plan, the rows, the status and the disclosure.</summary>
public class AHostedProviderHearsInsteadOfTheLocalModelTests
{
    private static D47Settings With(string provider) => new()
    {
        Listening = new ListeningSettings { Provider = provider, Model = "small.en", PushToTalkKey = "Oem4" },
    };

    [Fact]
    public void TheShippedProviderIsThisComputer()
    {
        Assert.Equal(SttProviderCatalog.LocalId, new D47Settings().Listening.Provider);
        Assert.False(SttProviderCatalog.Selected(new D47Settings().Listening.Provider).Hosted);
    }

    [Fact]
    public void AnUnknownProviderIsTreatedAsLocal()
    {
        Assert.Same(SttProviderCatalog.Local, SttProviderCatalog.Selected("whisper.cpp"));
        Assert.Same(SttProviderCatalog.Local, SttProviderCatalog.Selected(null));
    }

    [Theory]
    [InlineData(SttProviderCatalog.GroqId)]
    [InlineData(SttProviderCatalog.OpenAiId)]
    [InlineData(SttProviderCatalog.DeepgramId)]
    [InlineData(SttProviderCatalog.ElevenLabsId)]
    public void AHostedProviderUnloadsAModelThatIsOnDisk(string provider)
    {
        var plan = ListeningWiring.PlanModel(With(provider).Listening, new FakeModelStore("small.en"));

        Assert.Equal(SpeechModelAction.Unload, plan.Action);
    }

    [Fact]
    public void TheLocalProviderStillLoadsTheModel()
    {
        var plan = ListeningWiring.PlanModel(
            With(SttProviderCatalog.LocalId).Listening, new FakeModelStore("small.en"));

        Assert.Equal(SpeechModelAction.Load, plan.Action);
    }

    [Fact]
    public void OpenAiHearsWithTheKeyTheOtherOpenAiServicesRead()
    {
        Assert.Equal("openai.apiKey", SttProviderCatalog.OpenAi.KeySecretName);
        Assert.Equal("groq.apiKey", SttProviderCatalog.Groq.KeySecretName);
        Assert.Equal("deepgram.apiKey", SttProviderCatalog.Deepgram.KeySecretName);
        Assert.Equal("elevenlabs.apiKey", SttProviderCatalog.ElevenLabs.KeySecretName);
        Assert.Equal(TtsProviderCatalog.ElevenLabs.KeySecretName, SttProviderCatalog.ElevenLabs.KeySecretName);
        Assert.Null(SttProviderCatalog.Local.KeySecretName);
    }

    [Fact]
    public void TheDisclosureIsSilentForTheLocalProvider()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.SpeechRecognition, With(SttProviderCatalog.LocalId), llmKeyPresent: false);

        Assert.False(entry.Active);
        Assert.Equal("Speech recognition", entry.Name);
    }

    [Theory]
    [InlineData(SttProviderCatalog.GroqId, "api.groq.com")]
    [InlineData(SttProviderCatalog.OpenAiId, "api.openai.com")]
    [InlineData(SttProviderCatalog.DeepgramId, "api.deepgram.com")]
    [InlineData(SttProviderCatalog.ElevenLabsId, "api.elevenlabs.io")]
    public void TheDisclosureNamesTheHostAndWhatGoesThere(string provider, string host)
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.SpeechRecognition, With(provider), llmKeyPresent: false);

        Assert.True(entry.Active);
        Assert.Equal(host, entry.Destination);
        Assert.Contains("audio of every utterance", entry.What, StringComparison.Ordinal);
        Assert.Contains("API key", entry.What, StringComparison.Ordinal);
        Assert.Contains("names from your journal", entry.What, StringComparison.Ordinal);
        Assert.Contains("not addressed to D47", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void SpeechRecognitionFollowsSpokenReplies()
    {
        var ids = EgressDisclosure.Ids.ToList();

        Assert.Equal(ids.IndexOf(EgressDisclosure.TextToSpeech) + 1, ids.IndexOf(EgressDisclosure.SpeechRecognition));
    }

    [Fact]
    public void AHostedProviderDownloadsNoSpeechModel()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.SpeechModels, With(SttProviderCatalog.GroqId), llmKeyPresent: false);

        Assert.False(entry.Active);
    }

    [Fact]
    public void TheModelAndGpuRowsApplyOnlyToTheLocalProvider()
    {
        var rows = Rows();
        var model = rows.Single(row => row.Key == ListeningCapability.ModelKey);
        var gpu = rows.Single(row => row.Key == ListeningCapability.GpuKey);

        Assert.True(model.AppliesWhen!(With(SttProviderCatalog.LocalId)));
        Assert.True(gpu.AppliesWhen!(With(SttProviderCatalog.LocalId)));
        Assert.False(model.AppliesWhen!(With(SttProviderCatalog.GroqId)));
        Assert.False(gpu.AppliesWhen!(With(SttProviderCatalog.GroqId)));
    }

    [Fact]
    public void OnlyTheSelectedProvidersKeyRowApplies()
    {
        var rows = Rows();
        var groq = rows.Single(row => row.Key == ListeningCapability.KeyRowFor(SttProviderCatalog.Groq));
        var openAi = rows.Single(row => row.Key == ListeningCapability.KeyRowFor(SttProviderCatalog.OpenAi));

        Assert.Equal(SettingKind.Secret, groq.Kind);
        Assert.Equal("groq.apiKey", groq.SecretName);
        Assert.True(groq.AppliesWhen!(With(SttProviderCatalog.GroqId)));
        Assert.False(groq.AppliesWhen!(With(SttProviderCatalog.OpenAiId)));
        Assert.False(openAi.AppliesWhen!(With(SttProviderCatalog.LocalId)));
    }

    [Fact]
    public void ScribesKeyRowIsTheVoicesStoredKey()
    {
        var row = Rows().Single(row => row.Key == ListeningCapability.KeyRowFor(SttProviderCatalog.ElevenLabs));

        Assert.Equal(SettingKind.Secret, row.Kind);
        Assert.Equal("elevenlabs.apiKey", row.SecretName);
        Assert.Contains("ElevenLabs voice", row.Help, StringComparison.Ordinal);
        Assert.True(row.AppliesWhen!(With(SttProviderCatalog.ElevenLabsId)));
        Assert.False(row.AppliesWhen!(With(SttProviderCatalog.DeepgramId)));
    }

    /// <summary>Sending the Commander's voice to a third party is not the model's decision.</summary>
    [Fact]
    public void TheProviderRowIsRefusedToTheModel()
    {
        var row = Rows().Single(row => row.Key == ListeningCapability.ProviderKey);

        Assert.True(row.Protected);
        Assert.Equal(EgressDisclosure.SpeechRecognition, row.EgressId);
    }

    [Fact]
    public void TheStatusNamesTheProviderAndThatItsKeyIsStored()
    {
        var text = ListeningCapability.DescribeInDetail(With(SttProviderCatalog.GroqId), Surface(keyStored: true));

        Assert.Contains("Transcription: Groq, hosted, with whisper-large-v3-turbo. Its key is stored.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("tiny.en", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoKeyTheStatusSaysSo()
    {
        var text = ListeningCapability.Describe(With(SttProviderCatalog.GroqId), Surface(keyStored: false));

        Assert.StartsWith("No", text, StringComparison.Ordinal);
        Assert.Contains("Groq needs an API key. Add it in Settings.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WithAKeyCanYouHearMeSaysWhoIsListening()
    {
        var text = ListeningCapability.Describe(With(SttProviderCatalog.GroqId), Surface(keyStored: true));

        Assert.StartsWith("Yes", text, StringComparison.Ordinal);
        Assert.Contains("Groq turns what you say into words", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TranscriptionFailure.Unreachable, "I couldn't reach Groq. Say it again, or type it.")]
    [InlineData(TranscriptionFailure.KeyRejected, "Groq refused the key. Check it in Settings.")]
    [InlineData(TranscriptionFailure.RateLimited, "Groq is limiting requests. Try again in a moment.")]
    public void EachFailureHasItsSentence(TranscriptionFailure reason, string said)
    {
        Assert.Equal(said, SttProviderCatalog.Problem(new TranscriptionUnavailableException("Groq", reason, "x")));
    }

    [Fact]
    public void AnyOtherFailureReadsTheServicesOwnMessage()
    {
        var failure = new TranscriptionUnavailableException(
            "Groq", TranscriptionFailure.Failed, "Groq could not transcribe: file too short.")
        {
            Detail = "file too short.",
        };

        Assert.Equal("Groq couldn't transcribe that: file too short.", SttProviderCatalog.Problem(failure));
    }

    [Fact]
    public void ALongServiceMessageIsCut()
    {
        var failure = new TranscriptionUnavailableException("Groq", TranscriptionFailure.Failed, "x")
        {
            Detail = new string('a', 500),
        };

        Assert.True(SttProviderCatalog.Problem(failure).Length < 220);
    }

    private static IReadOnlyList<SettingRow> Rows()
    {
        using var install = new TempInstall();

        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        return ListeningCapability.Create(settings, Surface(keyStored: false)).Settings;
    }

    private static ListeningCapability.ListeningSurface Surface(bool keyStored) => new()
    {
        InputDevices = () => ["mic-1"],
        DeviceLabel = id => id,
        CaptureState = () => (true, null),
        TranscriberState = () => (false, null, "No speech model is selected."),
        Binds = () => new D47.Core.Input.EliteBinds(),
        InstalledModels = () => [],
        KeyStored = name => keyStored && name == "groq.apiKey",
    };
}
