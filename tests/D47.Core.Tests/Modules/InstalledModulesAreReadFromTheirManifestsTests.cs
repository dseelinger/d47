using D47.Core.Configuration;
using D47.Core.Modules;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Modules;

public class InstalledModulesAreReadFromTheirManifestsTests
{
    private readonly MemoryInstall _install = new();

    private string Folder(string name) => Path.Combine(_install.Paths.Modules, name);

    private static string Manifest(
        string id = "weather",
        int contract = 1,
        string command = "weather.exe",
        int interval = 300,
        string name = "Weather",
        string extra = "") => $$"""
        {
          "contract": {{contract}},
          "id": "{{id}}",
          "name": "{{name}}",
          "author": "A. Author",
          "version": "1.0.0",
          "command": "{{command}}",
          "minimumIntervalSeconds": {{interval}}{{extra}}
        }
        """;

    private void Install(string folder, string manifest, string? executable = "weather.exe")
    {
        _install.Files.WriteText(Path.Combine(Folder(folder), "module.json"), manifest);

        if (executable is not null)
        {
            _install.Files.WriteBytes(Path.Combine(Folder(folder), executable), [1]);
        }
    }

    private InstalledModule Only()
    {
        var catalog = new ModuleCatalog(_install.Files, _install.Paths.Modules);
        return Assert.Single(catalog.Reload());
    }

    private void AssertRefused(ModuleRefusalReason reason) =>
        Assert.Equal(reason, Assert.IsType<RefusedModule>(Only()).Reason);

    [Fact]
    public void AModulesFolderIsDataModules() =>
        Assert.Equal(Path.Combine(_install.Paths.Data, "modules"), _install.Paths.Modules);

    [Fact]
    public void AGoodManifestIsReadyWithEveryField()
    {
        Install("weather", Manifest(extra: """
            , "args": ["--quiet"],
              "pages": [{ "id": "now", "title": "Now" }],
              "character": { "name": "Isla", "voices": [{ "provider": "edge", "voice": "en-GB-SoniaNeural" }] }
            """));

        var ready = Assert.IsType<ReadyModule>(Only());

        Assert.Equal("weather", ready.Manifest.Id);
        Assert.Equal("Weather", ready.Manifest.Name);
        Assert.Equal(["--quiet"], ready.Manifest.Args);
        Assert.Equal(new ModulePage("now", "Now"), Assert.Single(ready.Manifest.Pages));
        Assert.Equal("Isla", ready.Manifest.Character?.Name);
        Assert.Equal(new ModuleVoice("edge", "en-GB-SoniaNeural"), Assert.Single(ready.Manifest.Character!.Voices));
        Assert.Equal(Path.Combine(Folder("weather"), "weather.exe"), ready.CommandPath);
    }

    [Fact]
    public void ACommandInASubfolderIsReady()
    {
        Install("weather", Manifest(command: "bin/weather.exe"), executable: Path.Combine("bin", "weather.exe"));

        Assert.IsType<ReadyModule>(Only());
    }

    [Fact]
    public void AnUnknownContractIsRefused()
    {
        Install("weather", Manifest(contract: 2));

        AssertRefused(ModuleRefusalReason.UnknownContract);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("""{ "contract": 1 }""")]
    public void AManifestThatDoesNotParseIsRefused(string text)
    {
        Install("weather", text);

        AssertRefused(ModuleRefusalReason.Unparseable);
    }

    [Fact]
    public void ANameOverThirtyTwoCharactersIsRefused()
    {
        Install("weather", Manifest(name: new string('n', 33)));

        AssertRefused(ModuleRefusalReason.Unparseable);
    }

    [Fact]
    public void ANameOfThirtyTwoCharactersIsReady()
    {
        Install("weather", Manifest(name: new string('n', 32)));

        Assert.IsType<ReadyModule>(Only());
    }

    [Theory]
    [InlineData("Weather")]
    [InlineData("1weather")]
    [InlineData("w")]
    [InlineData("weather-app")]
    [InlineData("abcdefghijklmnopq")]
    public void AMalformedIdIsRefused(string id)
    {
        Install("weather", Manifest(id: id));

        AssertRefused(ModuleRefusalReason.MalformedId);
    }

    [Fact]
    public void AnIdUsedTwiceRefusesBothModules()
    {
        Install("one", Manifest());
        Install("two", Manifest());

        var catalog = new ModuleCatalog(_install.Files, _install.Paths.Modules);
        var modules = catalog.Reload();

        Assert.Equal(2, modules.Count);
        Assert.All(modules, m => Assert.Equal(ModuleRefusalReason.DuplicateId, Assert.IsType<RefusedModule>(m).Reason));
    }

    [Theory]
    [InlineData(@"..\other\weather.exe")]
    [InlineData(@"C:\Windows\System32\notepad.exe")]
    [InlineData(@"\Windows\notepad.exe")]
    public void ACommandOutsideTheFolderIsRefused(string command)
    {
        Install("weather", Manifest(command: command.Replace(@"\", @"\\")));
        _install.Files.WriteBytes(Path.Combine(Folder("other"), "weather.exe"), [1]);

        var catalog = new ModuleCatalog(_install.Files, _install.Paths.Modules);
        var refused = Assert.IsType<RefusedModule>(Assert.Single(catalog.Reload()));

        Assert.Equal(ModuleRefusalReason.CommandOutsideFolder, refused.Reason);
    }

    [Fact]
    public void ACommandThatIsNotAnExeIsRefused()
    {
        Install("weather", Manifest(command: "weather.bat"), executable: "weather.bat");

        AssertRefused(ModuleRefusalReason.CommandMissing);
    }

    [Fact]
    public void AnExeThatDoesNotExistIsRefused()
    {
        Install("weather", Manifest(), executable: null);

        AssertRefused(ModuleRefusalReason.CommandMissing);
    }

    [Fact]
    public void AMinimumIntervalBelowTwoMinutesIsRefused()
    {
        Install("weather", Manifest(interval: 119));

        AssertRefused(ModuleRefusalReason.IntervalTooShort);
    }

    [Fact]
    public void AMinimumIntervalOfTwoMinutesIsReady()
    {
        Install("weather", Manifest(interval: 120));

        Assert.IsType<ReadyModule>(Only());
    }

    [Fact]
    public void AManifestDeeperThanOneFolderIsNotAModule()
    {
        _install.Files.WriteText(Path.Combine(Folder("weather"), "nested", "module.json"), Manifest());

        var catalog = new ModuleCatalog(_install.Files, _install.Paths.Modules);

        Assert.Empty(catalog.Reload());
    }

    [Fact]
    public void TheFolderIsReadAgainOnlyWhenAsked()
    {
        var catalog = new ModuleCatalog(_install.Files, _install.Paths.Modules);
        Assert.Empty(catalog.Reload());

        Install("weather", Manifest());
        Assert.Empty(catalog.Modules);

        Assert.IsType<ReadyModule>(Assert.Single(catalog.Reload()));
        Assert.IsType<ReadyModule>(Assert.Single(catalog.Modules));
    }

    [Fact]
    public void AModuleFoundForTheFirstTimeIsOff()
    {
        Install("weather", Manifest());

        var settings = D47Settings.Defaults.ModuleFor("weather");

        Assert.False(settings.Enabled);
        Assert.Null(settings.SpeaksAs);
        Assert.False(settings.Muted);
        Assert.Equal(ModuleInterval.Own, settings.Interval);
    }

    [Fact]
    public void ModuleSettingsRoundTripAndSurviveTheModuleBeingRemoved()
    {
        var store = new SettingsStore(_install.Paths, _install.Files, NullLogger<SettingsStore>.Instance);
        var chosen = new ModuleSettings { Enabled = true, SpeaksAs = "abcd1234", Muted = true, Interval = ModuleInterval.Hourly };

        store.Save(new D47Settings
        {
            Modules = new Dictionary<string, ModuleSettings>(StringComparer.Ordinal) { ["gone"] = chosen },
        });

        var loaded = store.Load();
        store.Save(loaded);
        var again = store.Load().ModuleFor("gone");

        Assert.Equal(chosen with { Extra = again.Extra }, again);
        Assert.Contains("hourly", _install.Files.ReadText(_install.Paths.SettingsFile), StringComparison.Ordinal);
    }
}
