using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A hull, module or blueprint asked about by name is answered from the shipped tables without the model (#1957).</summary>
public class AHullModuleOrBlueprintAskedByNameSkipsTheModelTests
{
    [Fact]
    public void AHullAskedAboutByNameIsAShipLookup()
    {
        var lookup = Assert.IsType<NamedLookup>(NamedLookupPhrase.Read("what is the Python like"));

        Assert.Equal(SpecificationCapability.Id, lookup.CapabilityId);
        Assert.Equal("get_ship_specification", lookup.Tool);
        Assert.Equal("Python", lookup.Arguments["ship"]);
    }

    [Fact]
    public void AModuleWithASizeAndRatingIsAModuleLookup()
    {
        var lookup = Assert.IsType<NamedLookup>(NamedLookupPhrase.Read("what does a 6A power plant weigh?"));

        Assert.Equal("get_module_specification", lookup.Tool);
        Assert.Equal("power plant", lookup.Arguments["module"]);
        Assert.Equal("6", lookup.Arguments["size"]);
        Assert.Equal("A", lookup.Arguments["rating"]);
    }

    [Fact]
    public void ABlueprintWithAGradeIsABlueprintLookup()
    {
        var lookup = Assert.IsType<NamedLookup>(NamedLookupPhrase.Read("what does a grade 5 dirty drive cost"));

        Assert.Equal(EngineeringCapability.Id, lookup.CapabilityId);
        Assert.Equal("get_blueprint", lookup.Tool);
        Assert.Equal("dirty drive", lookup.Arguments["blueprint"]);
    }

    [Theory]
    [InlineData("what does increased FSD range cost")]
    [InlineData("what does a grade 3 clean drive cost")]
    public void ABlueprintNamedInFullOrWithAGradeIsRead(string said)
    {
        Assert.Equal("get_blueprint", NamedLookupPhrase.Read(said)?.Tool);
    }

    [Theory]
    [InlineData("what is the weather like")]
    [InlineData("what is it like")]
    [InlineData("what does a 6A banana weigh")]
    [InlineData("what does the fuel cost")]
    [InlineData("what does a grade 5 sandwich cost")]
    [InlineData("what does a limpet cost")]
    [InlineData("what does mining cost")]
    [InlineData("what does a module cost")]
    [InlineData("what does a trip cost")]
    [InlineData("what does armour cost")]
    [InlineData("what does clean cost")]
    [InlineData("what does high capacity cost")]
    public void ANameTheTablesDoNotKnowIsLeftForTheModel(string said)
    {
        Assert.Null(NamedLookupPhrase.Read(said));
    }

    [Fact]
    public void NoDeclaredPhraseIsTakenByTheNameLookup()
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;
        var taken = new KeywordRouter(registry).Book.Entries
            .Select(entry => entry.Phrase)
            .Where(phrase => NamedLookupPhrase.Read(phrase) is not null)
            .ToList();

        Assert.Empty(taken);
    }

    [Theory]
    [InlineData("what is the Python like", "Python")]
    [InlineData("what does a 6A power plant weigh", "Power Plant")]
    [InlineData("what does a grade 5 dirty drive cost", "Dirty Drive")]
    public async Task TheTurnAnswersFromTheTablesWithNoModelCall(string said, string answered)
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;
        var model = FakeLlmProvider.Answering("From the model.");
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            model,
            clock: new InstantClock());

        var text = new System.Text.StringBuilder();

        await foreach (var turnEvent in loop.RunAsync(said, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.TextDelta delta)
            {
                text.Append(delta.Text);
            }
        }

        Assert.Equal(0, model.CallCount);
        Assert.Contains(answered, text.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
