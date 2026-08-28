using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Logbook;
using Xunit;

namespace D47.Core.Tests.Logbook;

/// <summary>
/// What the model can reach, and what only a person can (Phase 33).
/// </summary>
public class LogbookCapabilityTests
{
    /// <summary>
    /// <b>The assertion that keeps item 4 true.</b> Phase 32 kept its tools away from the model
    /// because the model is the component that reads untrusted text. The same reasoning one step
    /// further: it is also not the component that gets to authorise the largest single request d47
    /// makes.
    /// </summary>
    [Fact]
    public void NothingHereIsReachableByTheModel()
    {
        var descriptor = LogbookCapability.Create(null);

        Assert.NotEmpty(descriptor.Tools);
        Assert.All(descriptor.Tools, tool => Assert.True(tool.Protected, $"{tool.Name} is not protected"));
    }

    /// <summary>
    /// Both acts have to be reachable without a model in the path, which the closed router grammar
    /// only allows for phrases carrying no extracted argument at all.
    /// </summary>
    [Fact]
    public void BothActsHaveArgumentFreePhrases()
    {
        var descriptor = LogbookCapability.Create(null);

        var estimate = descriptor.Tools.Single(tool => tool.Name == "estimate_log");
        var write = descriptor.Tools.Single(tool => tool.Name == "write_log");

        Assert.Contains(estimate.Commands, phrase => phrase.Phrase == "write my commander's log");
        Assert.Contains(write.Commands, phrase => phrase.Phrase == "write the log");

        // Quoting and spending are different sentences. A single phrase that did both would be the
        // whole of item 4 undone by a convenience.
        Assert.Empty(estimate.Commands.Select(phrase => phrase.Phrase)
            .Intersect(write.Commands.Select(phrase => phrase.Phrase), StringComparer.Ordinal));

        Assert.All(write.Commands, phrase => Assert.Empty(phrase.Arguments));
    }

    /// <summary>
    /// A phrase cannot carry a date, so the values it can carry are exactly the spans the settings
    /// row offers — and the tool's enum has to agree, or a declared phrase writes a value the
    /// handler will not recognise.
    /// </summary>
    [Fact]
    public void TheSpansTheToolAcceptsAreTheSpansTheRowOffers()
    {
        var descriptor = LogbookCapability.Create(null);
        var estimate = descriptor.Tools.Single(tool => tool.Name == "estimate_log");
        var range = estimate.Parameters.Single(parameter => parameter.Name == "range");

        Assert.Equal(LogRanges.Ids, range.AllowedValues);
        Assert.False(range.Required);

        var row = descriptor.Settings.Single(setting => setting.Key == LogbookCapability.RangeKey);
        Assert.Equal(LogRanges.Ids, row.Choices);
    }

    /// <summary>
    /// Item 3: "the shipped default is the plain one". The ship's-AI log is the thing only d47 can
    /// do, and a thing only d47 can do is a thing to opt into.
    /// </summary>
    [Fact]
    public void TheShippedDefaultVoiceIsTheCommandersOwn()
    {
        Assert.Equal("first-person", new D47Settings().Logbook.Voice);
        Assert.Equal(LogVoice.FirstPerson, LogVoices.Parse(new D47Settings().Logbook.Voice));
    }

    /// <summary>
    /// The voice row is protected for the same reason the memory rows are: a log written in a voice
    /// the Commander did not choose is d47 having spoken at length as somebody else.
    /// </summary>
    [Fact]
    public void TheVoiceCannotBeChangedFromTheToolSurface()
    {
        var row = LogbookCapability.Create(null).Settings
            .Single(setting => setting.Key == LogbookCapability.VoiceKey);

        Assert.True(row.Protected);
        Assert.NotEmpty(row.Commands);
    }

    /// <summary>
    /// The folder row is <see cref="SettingKind.Info"/>, which <see cref="SettingsService.Apply"/>
    /// refuses outright — the mechanism Phases 31 and 32 used to keep an expensive act off the tool
    /// surface, used a third time for the only act that costs money.
    /// </summary>
    [Fact]
    public void TheFolderRowIsUnreachableFromTheToolSurface()
    {
        var row = LogbookCapability.Create(null).Settings
            .Single(setting => setting.Key == LogbookCapability.StoreKey);

        Assert.Equal(SettingKind.Info, row.Kind);
        Assert.NotNull(row.Binding);
        Assert.Null(row.Binding!.Write);
    }

    /// <summary>
    /// With nothing composed, every tool says there is nothing set up rather than throwing — the
    /// same treatment every other optional service gets.
    /// </summary>
    [Fact]
    public async Task WithNothingComposedTheToolsSayThereIsNothingToWriteWith()
    {
        var descriptor = LogbookCapability.Create(null);

        foreach (var tool in descriptor.Tools)
        {
            var result = await tool.Handler(new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)), CancellationToken.None);
            Assert.Contains("configuration", result.Content, StringComparison.Ordinal);
        }
    }
}
