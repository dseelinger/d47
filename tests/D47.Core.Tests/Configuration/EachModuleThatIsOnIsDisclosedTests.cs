using D47.Core.Configuration;
using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class EachModuleThatIsOnIsDisclosedTests
{
    private static ReadyModule Module(string id, string name) =>
        new(
            Path.Combine("modules", id),
            new ModuleManifest(1, id, name, "Someone", "1.0", "run.exe", [], 120, [], null),
            Path.Combine("modules", id, "run.exe"));

    private static readonly IReadOnlyList<InstalledModule> Installed = [Module("alpha", "Alpha Module"), Module("beta", "Beta Module")];

    private static D47Settings On(string id) =>
        new() { Modules = new Dictionary<string, ModuleSettings> { [id] = new ModuleSettings { Enabled = true } } };

    [Fact]
    public void TwoInstalledAndOneOnGivesExactlyOneModuleEntry()
    {
        var entries = EgressDisclosure.For(On("beta"), llmKeyPresent: false, installedModules: Installed);

        var entry = Assert.Single(entries, e => e.Id.StartsWith("module.", StringComparison.Ordinal));
        Assert.Equal("module.beta", entry.Id);
        Assert.Equal("Beta Module runs as its own program on this PC. D47 does not see what it sends or where.", entry.What);
        Assert.True(entry.Active);
        Assert.Equal(EgressDisclosure.Ids.Count + 1, entries.Count);
        Assert.Equal("module.beta", entries[^1].Id);
    }

    [Fact]
    public void ModulesThatAreOffGetNoEntry() =>
        Assert.DoesNotContain(
            EgressDisclosure.For(new D47Settings(), llmKeyPresent: false, installedModules: Installed),
            e => e.Id.StartsWith("module.", StringComparison.Ordinal));

    [Fact]
    public void TheSpokenReportListsTheModule() =>
        Assert.Contains(
            "Beta Module runs as its own program on this PC.",
            EgressDisclosure.Describe(On("beta"), llmKeyPresent: false, installedModules: Installed),
            StringComparison.Ordinal);
}
