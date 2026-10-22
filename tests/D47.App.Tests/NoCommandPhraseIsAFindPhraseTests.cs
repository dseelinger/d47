using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Find runs ahead of every keyword route in the ask path, so no declared setting or tool command phrase may
/// read as a Find phrase (#1473).
/// </summary>
public class NoCommandPhraseIsAFindPhraseTests
{
    [Fact]
    public void NoSettingOrToolCommandIsTakenByFind()
    {
        var (_, _, _, registry, _) = TestSurface.CreateFull();

        var phrases = registry.All
            .SelectMany(capability => capability.Descriptor.Settings.SelectMany(row => row.Commands.Select(command => command.Phrase))
                .Concat(capability.Descriptor.Tools.SelectMany(tool => tool.Commands.Select(command => command.Phrase))))
            .ToList();

        Assert.NotEmpty(phrases);
        Assert.DoesNotContain(phrases, phrase => PanelFindPhrases.Query(phrase) is not null);
    }
}
