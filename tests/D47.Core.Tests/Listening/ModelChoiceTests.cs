using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>The model catalogue and the egress disclosure.</summary>
public class ModelChoiceTests
{
    [Fact]
    public void TheSmallestEnglishModelIsSelectedOnAFreshInstall()
    {
        // A fresh install selects this and fetches it on first launch, so the choice of which model ships is
        // the choice of what every new Commander downloads without being asked.
        Assert.Equal("base.en", new D47Settings().Listening.Model);
        Assert.Equal(WhisperModels.DefaultId, new D47Settings().Listening.Model);

        var model = WhisperModels.Find(new D47Settings().Listening.Model);

        Assert.NotNull(model);
        Assert.True(model.EnglishOnly);

        // Still small enough to fetch on somebody's behalf without asking.
        Assert.True(model.ApproximateMegabytes <= 150);
    }

 /// <summary>Every model d47 offers is English-only.</summary>
    [Fact]
    public void NoMultilingualModelIsOffered()
    {
        Assert.All(WhisperModels.All, model => Assert.True(model.EnglishOnly, $"{model.Id} is not English-only"));
    }

    /// <summary>
    /// A settings file naming a retired multilingual model runs its English twin rather than falling
    /// through to no transcription at all, which is what an unknown id means everywhere else (see
    /// <c>NothingSelectedReleasesWhateverIsLoaded</c>).
    /// </summary>
    [Theory]
    [InlineData("tiny", "tiny.en")]
    [InlineData("base", "base.en")]
    [InlineData("small", "small.en")]
    [InlineData("medium", "medium.en")]
    public void ARetiredMultilingualModelAdoptsItsEnglishTwin(string retired, string expected)
    {
        Assert.Equal(expected, WhisperModels.AdoptedId(retired));
        Assert.NotNull(WhisperModels.Find(WhisperModels.AdoptedId(retired)));

        // And the row shows what is running rather than a choice this build does not offer.
        var settings = new D47Settings { Listening = new ListeningSettings { Model = retired } };

        Assert.Equal(expected, Row(ListeningCapability.ModelKey).Binding!.Read!(settings));
    }

    /// <summary>Anything still offered, and anything unknown, is left exactly as it is.</summary>
    [Theory]
    [InlineData("small.en")]
    [InlineData(WhisperModels.NoneId)]
    [InlineData("not-a-model")]
    public void AdoptionTouchesNothingElse(string id) => Assert.Equal(id, WhisperModels.AdoptedId(id));

    [Fact]
    public void TheGpuIsOffByDefault()
    {
        // In VR the GPU is the scarce resource, and a large model there surfaces as dropped frames rather
        // than as anything resembling a speech problem.
        Assert.False(new D47Settings().Listening.UseGpu);
    }

    [Fact]
    public void NoneIsAFirstClassChoiceInTheCatalogue()
    {
        Assert.Contains(WhisperModels.NoneId, WhisperModels.Ids);
        Assert.Null(WhisperModels.Find(WhisperModels.NoneId));
        Assert.Contains("None", WhisperModels.LabelOf(WhisperModels.NoneId));
    }

    [Fact]
    public void EveryModelHasAGgmlFileNameAndADownloadUrlOnTheDeclaredHost()
    {
        foreach (var model in WhisperModels.All)
        {
            Assert.StartsWith("ggml-", model.FileName);
            Assert.EndsWith(".bin", model.FileName);

            var url = WhisperModels.DownloadUrl(model);

            // The host is named in exactly one place, because the egress disclosure quotes it and a second
            // literal is a way for the disclosure to be wrong.
            Assert.StartsWith($"https://{WhisperModels.Host}/", url);
            Assert.EndsWith(model.FileName, url);
        }
    }

    [Fact]
    public void EnglishOnlyModelsAreIdentifiedBySuffix()
    {
        Assert.True(WhisperModels.Find("base.en")!.EnglishOnly);

        // Nothing in the catalogue answers false any more, so the property is asserted against a model d47
        // does not offer rather than against a row (#187 retired all four).
        Assert.False(new WhisperModel("base", "Base (multilingual)", 142).EnglishOnly);
    }

    // ---- Egress ---------------------------------------------------------------------------

    [Fact]
    public void NoModelSelectedMeansTheDownloadRowIsInactive()
    {
        // Explicitly none, which is a Commander's choice rather than the shipped default — a fresh install
        // selects the smallest model, so the row is live until that is answered.
        var chosenNone = new D47Settings
        {
            Listening = new ListeningSettings { Model = WhisperModels.NoneId },
        };

        var entry = EgressDisclosure.Entry(
            EgressDisclosure.SpeechModels, chosenNone, llmKeyPresent: false);

        Assert.False(entry.Active);
        Assert.Contains("nothing", entry.Line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectingAModelLightsTheDownloadRowBeforeAnythingIsFetched()
    {
        var settings = new D47Settings
        {
            Listening = new ListeningSettings { Model = "base.en" },
        };

        var entry = EgressDisclosure.Entry(
            EgressDisclosure.SpeechModels, settings, llmKeyPresent: false);

        // Active because the setting can cause a transfer, not because one is happening.
        Assert.True(entry.Active);
        Assert.Equal(WhisperModels.Host, entry.Destination);
        Assert.Contains("downloads it from this host", entry.What);
        Assert.Contains("entirely on this machine", entry.What);

        // The row states what actually happens.
        Assert.DoesNotContain("until you agree", entry.What);
    }

    [Fact]
    public void EverySwitchableDestinationCanStillBeTurnedOffWithListeningConfigured()
    {
        // Every provider off, with listening set up: turning everything switchable off has to stay reachable
        // the moment the Commander wants to talk to d47.
        var settings = new D47Settings
        {
            Llm = new LlmSettings { Provider = Core.Conversation.LlmProviderCatalog.NoneId },
            Updates = new UpdateSettings { CheckOnStartup = false },
            Listening = new ListeningSettings { Model = WhisperModels.NoneId },

            // Every provider off means the voice one too: Edge Neural is free, not local.
            Speech = new SpeechSettings { Provider = Core.Audio.TtsProviderCatalog.NoneId },

 // And the hull art, which fetches a picture and a turntable on a press.
            Ui = new UiSettings { HullArt = false },
        };

        var entries = EgressDisclosure.For(settings, llmKeyPresent: false);

        var active = Assert.Single(entries, entry => entry.Active);
        Assert.Equal(EgressDisclosure.Donation, active.Id);
        Assert.Contains(
            $"1 of {entries.Count} destinations are active",
            EgressDisclosure.Describe(settings, false));
    }

    [Fact]
    public void EveryDisclosureIdRendersIncludingTheNewOne()
    {
        // The set is exhaustive by construction, and Entry throws on an id it does not know — so this is what
        // catches an id added to the list and nowhere else.
        foreach (var id in EgressDisclosure.Ids)
        {
            var entry = EgressDisclosure.Entry(id, new D47Settings(), llmKeyPresent: false);
            Assert.False(string.IsNullOrWhiteSpace(entry.What));
        }
    }

    // ---- The settings row -------------------------------------------------------------------

    [Fact]
    public void AnUnknownModelNameFallsBackToNoneRatherThanBeingStored()
    {
        var row = Row(ListeningCapability.ModelKey);
        var written = row.Binding!.Write!(new D47Settings(), "not-a-real-model");

        // A hand-edited settings file can contain anything, and a model id d47 cannot resolve would otherwise
        // sit there looking selected while nothing loads.
        Assert.Equal(WhisperModels.NoneId, written.Listening.Model);
    }

    [Fact]
    public void TheGpuRowDisappearsWhenNoModelIsSelected()
    {
        var row = Row(ListeningCapability.GpuKey);

        // A row that does not apply is absent rather than disabled — a greyed-out control still asserts the
        // setting exists.
        Assert.False(row.Applies(new D47Settings
        {
            Listening = new ListeningSettings { Model = WhisperModels.NoneId },
        }));
        Assert.True(row.Applies(new D47Settings
        {
            Listening = new ListeningSettings { Model = "base.en" },
        }));
    }

 /// <summary>Both costs on the row, and the fallback said out loud.</summary>
    [Fact]
    public void TheGpuRowStatesBothItsCostsAndItsFallback()
    {
        var help = Row(ListeningCapability.GpuKey).Help;

        Assert.Contains("VR", help);
        Assert.Contains("dropped frames", help);
        Assert.Contains("video memory", help);
        Assert.Contains("says so", help);
    }

    private static SettingRow Row(string key)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        return surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Single(row => row.Key == key);
    }
}
