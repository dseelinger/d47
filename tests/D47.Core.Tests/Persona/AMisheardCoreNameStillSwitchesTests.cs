using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// "Switch to" a core reaches that core without a model when the transcriber spells its name wrong, and
/// reaches nothing else's core.
/// </summary>
public class AMisheardCoreNameStillSwitchesTests
{
    private static TurnLoop Build(CapabilityRegistry registry, SettingsService settings, ILlmProvider? provider = null)
    {
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(provider is not null),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            settings: settings,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static async Task<TurnResult> RunAsync(TurnLoop loop, string input)
    {
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        return result;
    }

    /// <summary>The same sounds and nearly the same spelling: set without asking.</summary>
    [Theory]
    [InlineData("switch to Quora", "cora")]
    [InlineData("switch to Kora", "cora")]
    [InlineData("switch to core a", "cora")]
    [InlineData("switch to Kovas", "covas")]
    [InlineData("switch to Worden", "warden")]
    [InlineData("switch to analyst prime", "analyst-prime")]
    [InlineData("switch to Llama", "llamo")]
    [InlineData("switch to Lame-O", "llamo")]
    [InlineData("switch to Mentor", "mender")]
    [InlineData("switch to the cartographer", "cartographer")]
    [InlineData("switch to quarter master", "quartermaster")]
    [InlineData("switch to heretic", "heretic")]
    public void AMisspeltNameIsTheCoreItSoundsLike(string said, string core)
    {
        var heard = PersonaCapability.CoresHeardIn(said);

        Assert.Equal([core], heard.Values);
        Assert.True(heard.Certain);
    }

    /// <summary>Close in sound only, which ordinary words also are: asked, not set.</summary>
    [Theory]
    [InlineData("switch to Kecks", "kex")]
    [InlineData("switch to Centennial", "sentinel")]
    [InlineData("switch to covers", "covas")]
    [InlineData("switch to Charts", "cartographer")]
    [InlineData("switch to archivists", "archivist")]
    [InlineData("switch to carrier", "cora")]
    [InlineData("switch to monitor", "mender")]
    [InlineData("switch to cruise", "covas")]
    [InlineData("switch to comms", "covas")]
    [InlineData("switch to keys", "kex")]
    public void AWordThatOnlySoundsLikeACoreIsAsked(string said, string core)
    {
        var heard = PersonaCapability.CoresHeardIn(said);

        Assert.Equal([core], heard.Values);
        Assert.False(heard.Certain);
    }

    [Theory]
    [InlineData("switch to elite")]
    [InlineData("switch to the game")]
    [InlineData("switch to commander")]
    [InlineData("switch to search")]
    [InlineData("switch to settings")]
    [InlineData("switch to stories")]
    [InlineData("switch to navigation")]
    [InlineData("switch to commander Kestrel Vane")]
    [InlineData("switch to rex")]
    [InlineData("switch to trade")]
    [InlineData("be kind")]
    [InlineData("wake up")]
    public void APhraseThatIsNotACoreReadsAsNone(string said) =>
        Assert.Empty(PersonaCapability.CoresHeardIn(said).Values);

    [Fact]
    public async Task SwitchToQuoraPutsCoraAboardWithNoModel()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(surface.Registry, surface.Settings, provider);

        var result = await RunAsync(loop, "Switch to Quora.");

        Assert.Equal(TurnRoute.SettingCommand, result.Route);
        Assert.Equal("Core is now Cora.", result.Text);
        Assert.Equal("cora", surface.Settings.Current.Persona.Id);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task SwitchToCovasPutsTheStockCoreBack()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface.Registry, surface.Settings);

        await RunAsync(loop, "switch to Cora");
        var result = await RunAsync(loop, "Switch to COVAS.");

        Assert.Equal(TurnRoute.SettingCommand, result.Route);
        Assert.Equal("Core is now COVAS.", result.Text);
        Assert.Equal(PersonaCatalog.DefaultId, surface.Settings.Current.Persona.Id);
    }

    [Fact]
    public async Task SwitchToEliteStillLeavesTheCoreAlone()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface.Registry, surface.Settings);

        var result = await RunAsync(loop, "switch to elite");

        Assert.NotEqual(TurnRoute.SettingCommand, result.Route);
        Assert.Equal(PersonaCatalog.DefaultId, surface.Settings.Current.Persona.Id);
    }

    [Fact]
    public async Task APoliteTailDoesNotStopIt()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface.Registry, surface.Settings);

        var result = await RunAsync(loop, "switch to Quora please");

        Assert.Equal(TurnRoute.SettingCommand, result.Route);
        Assert.Equal("cora", surface.Settings.Current.Persona.Id);
    }

    [Fact]
    public async Task SwitchToCarrierAsksBeforeCoraComesAboard()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface.Registry, surface.Settings);

        var offered = await RunAsync(loop, "switch to carrier");

        Assert.Equal(TurnRoute.Offer, offered.Route);
        Assert.Equal("Did you mean 'Cora'?", offered.Text);
        Assert.Equal(PersonaCatalog.DefaultId, surface.Settings.Current.Persona.Id);

        await RunAsync(loop, "yes");

        Assert.Equal("cora", surface.Settings.Current.Persona.Id);
    }

    /// <summary>Two equally close values are offered by name, only they are, and naming one sets it.</summary>
    [Fact]
    public async Task TwoEquallyCloseValuesAreOfferedAndTheOneNamedIsSet()
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
        var settings = new SettingsService(store, secrets, new D47Settings(), NullLogger<SettingsService>.Instance);

        var registry = CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "heard",
                Group = "Test",
                Name = "Heard",
                Summary = "A row named by sound.",
                Settings =
                [
                    new SettingRow
                    {
                        Key = "heard.core",
                        Label = "Core",
                        Help = "Which core.",
                        Kind = SettingKind.Choice,
                        Choices = ["warden", "mender"],
                        ChoiceLabel = id => PersonaCatalog.Resolve(id).Name,
                        ValuesBySound = said => said == "switch to wender"
                            ? new ValuesHeard(["warden", "mender"], Certain: true)
                            : ValuesHeard.None,
                        Binding = new SettingBinding
                        {
                            Read = s => s.Persona.Id,
                            Write = (s, value) => s with { Persona = s.Persona with { Id = value! } },
                        },
                    },
                ],
            },
        ]);

        settings.Bind(registry);
        var loop = Build(registry, settings);

        var offered = await RunAsync(loop, "switch to wender");

        Assert.Equal(TurnRoute.Offer, offered.Route);
        Assert.Equal("Did you mean 'Warden', or 'Mender'?", offered.Text);

        await RunAsync(loop, "Mender");

        Assert.Equal("mender", settings.Current.Persona.Id);
    }
}
