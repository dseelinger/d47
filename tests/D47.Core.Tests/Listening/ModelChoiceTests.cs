using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// The model catalogue, the consent wording and the egress disclosure. Nothing here touches the
/// network — which is the point: the interesting behaviour is what d47 does <em>before</em> it
/// would fetch anything.
/// </summary>
public class ModelChoiceTests
{
    [Fact]
    public void NoModelIsSelectedOnAFreshInstall()
    {
        // A default that downloaded several hundred megabytes at first launch would be the
        // thing the consent gate exists to prevent, arranged by the default rather than by a
        // bug.
        Assert.Equal(WhisperModels.NoneId, new D47Settings().Listening.Model);
    }

    [Fact]
    public void TheGpuIsOffByDefault()
    {
        // In VR the GPU is the scarce resource, and a large model there surfaces as dropped
        // frames rather than as anything resembling a speech problem.
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

            // The host is named in exactly one place, because the egress disclosure quotes it
            // and a second literal is a way for the disclosure to be wrong.
            Assert.StartsWith($"https://{WhisperModels.Host}/", url);
            Assert.EndsWith(model.FileName, url);
        }
    }

    [Fact]
    public void EnglishOnlyModelsAreIdentifiedBySuffix()
    {
        Assert.True(WhisperModels.Find("base.en")!.EnglishOnly);
        Assert.False(WhisperModels.Find("base")!.EnglishOnly);
    }

    [Fact]
    public void TheConsentPromptStatesTheRealSizeAndTheHost()
    {
        var offer = new ModelOffer(WhisperModels.Find("base.en")!, 147_951_465, "abc123");
        var prompt = offer.ConsentPrompt();

        // What, how big, from where — in one sentence, because a consent prompt nobody reads
        // is not consent. The size is the one the host reported, not the catalogue's estimate.
        Assert.Contains("141.1 MB", prompt);
        Assert.Contains(WhisperModels.Host, prompt);
        Assert.Contains("verify", prompt);
    }

    [Fact]
    public void AnOfferWithNoPublishedChecksumSaysSoRatherThanImplyingOne()
    {
        var offer = new ModelOffer(WhisperModels.Find("tiny.en")!, 77_000_000, Sha256: null);

        // Claiming verification that is not happening is worse than admitting it is not.
        Assert.Contains("no checksum", offer.ConsentPrompt(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("will verify", offer.ConsentPrompt());
    }

    // ---- Egress ---------------------------------------------------------------------------

    [Fact]
    public void NoModelSelectedMeansTheDownloadRowIsInactive()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.SpeechModels, new D47Settings(), llmKeyPresent: false);

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

        // Active because the setting can cause a transfer, not because one is happening. A row
        // that only lit up mid-download would tell the Commander nothing they could act on.
        Assert.True(entry.Active);
        Assert.Equal(WhisperModels.Host, entry.Destination);
        Assert.Contains("until you agree", entry.What);
        Assert.Contains("entirely on this machine", entry.What);
    }

    [Fact]
    public void LocalOnlyOperationIsStillReachableWithListeningConfigured()
    {
        // Every provider off, but a model already chosen: the disclosure has to be able to say
        // nothing is leaving once the model is on disk, or "local-only" stops being reachable
        // the moment the Commander wants to talk to d47.
        var settings = new D47Settings
        {
            Llm = new LlmSettings { Provider = Core.Conversation.LlmProviderCatalog.NoneId },
            Updates = new UpdateSettings { CheckOnStartup = false },
            Listening = new ListeningSettings { Model = WhisperModels.NoneId },
        };

        var entries = EgressDisclosure.For(settings, llmKeyPresent: false);

        Assert.DoesNotContain(entries, entry => entry.Active);
        Assert.Contains("Nothing is leaving this machine", EgressDisclosure.Describe(settings, false));
    }

    [Fact]
    public void EveryDisclosureIdRendersIncludingTheNewOne()
    {
        // The set is exhaustive by construction, and Entry throws on an id it does not know —
        // so this is what catches an id added to the list and nowhere else.
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

        // A hand-edited settings file can contain anything, and a model id d47 cannot resolve
        // would otherwise sit there looking selected while nothing loads.
        Assert.Equal(WhisperModels.NoneId, written.Listening.Model);
    }

    [Fact]
    public void TheGpuRowDisappearsWhenNoModelIsSelected()
    {
        var row = Row(ListeningCapability.GpuKey);

        // A row that does not apply is absent rather than disabled — a greyed-out control still
        // asserts the setting exists.
        Assert.False(row.Applies(new D47Settings()));
        Assert.True(row.Applies(new D47Settings
        {
            Listening = new ListeningSettings { Model = "base.en" },
        }));
    }

    [Fact]
    public void TheGpuRowStatesItsCostInVr()
    {
        // The checklist asks for the cost to be stated on the row, because a Commander who sees
        // reprojection after enabling this has no reason to connect the two otherwise.
        var help = Row(ListeningCapability.GpuKey).Help;

        Assert.Contains("VR", help);
        Assert.Contains("dropped frames", help);
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
