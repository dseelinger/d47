using System.Reflection;
using System.Text.Json.Serialization;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>#368.</summary>
public class AFileFromANewerBuildStillLoadsTests
{
    private const string FromANewerBuild = """
        {
          "schemaVersion": 1,
          "ui": { "theme": "dark" },
          "vr": { "enabled": false, "controlers": { "handedness": "left" } }
        }
        """;

    /// <summary>
    /// The whole of the first defence: the file loads, what this build understands is honoured, and
    /// what it does not is still there after a save.
    /// </summary>
    [Fact]
    public void AnUnknownKeyUnderVrIsLoadedKeptAndWrittenBackUnchanged()
    {
        using var install = new TempInstall();
        File.WriteAllText(install.Paths.SettingsFile, FromANewerBuild);

        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);
        var settings = store.Load();

        // The known settings are honoured rather than lost with the file.
        Assert.False(settings.Vr.Enabled);
        Assert.Equal(ThemeCatalog.Dark, settings.Ui.Theme);

        store.Save(settings);

        var written = File.ReadAllText(install.Paths.SettingsFile);
        Assert.Contains("controlers", written, StringComparison.Ordinal);
        Assert.Contains("left", written, StringComparison.Ordinal);

        // And it comes back the same way on the next run, rather than only surviving the first.
        var restarted = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);
        restarted.Load();
        Assert.Equal(["vr.controlers"], restarted.UnknownKeys);
    }

    /// <summary>
    /// The key is named, by its path through the document, so a typo still surfaces now that it no
    /// longer refuses the load.
    /// </summary>
    [Fact]
    public void TheUnknownKeyIsLoggedByNameAndPath()
    {
        using var install = new TempInstall();
        File.WriteAllText(install.Paths.SettingsFile, FromANewerBuild);

        var logger = new RecordingLogger<SettingsStore>();
        var store = new SettingsStore(install.Paths, logger);

        store.Load();

        Assert.Equal(["vr.controlers"], store.UnknownKeys);

        var warning = Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning
                && entry.Message.Contains("controlers", StringComparison.Ordinal));

        // The path as well as the key: a Commander told to fix a file needs to know which one.
        Assert.Contains(install.Paths.SettingsFile, warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A record d47 keeps one of per provider is reached through a dictionary, not a property, and a
    /// key inside one has to be kept and named like any other.
    /// </summary>
    [Fact]
    public void AnUnknownKeyInsideAKeyedRecordIsKeptAndNamedToo()
    {
        using var install = new TempInstall();
        File.WriteAllText(install.Paths.SettingsFile, """
            {
              "schemaVersion": 1,
              "speech": { "providerVoices": { "edge": { "ship": "en-GB-RyanNeural", "timbre": "warm" } } }
            }
            """);

        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);
        var settings = store.Load();

        Assert.Equal(["speech.providerVoices.edge.timbre"], store.UnknownKeys);
        Assert.Equal("en-GB-RyanNeural", settings.Speech.ProviderVoices["edge"].Ship);

        store.Save(settings);
        Assert.Contains("timbre", File.ReadAllText(install.Paths.SettingsFile), StringComparison.Ordinal);
    }

    /// <summary>The cost the bags must not have.</summary>
    [Fact]
    public void AFileThisBuildFullyUnderstandsCarriesNoBagAtAll()
    {
        using var install = new TempInstall();
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        store.Save(new D47Settings { Ui = new UiSettings { Theme = ThemeCatalog.Dark } });

        var first = store.Load();
        var second = store.Load();

        Assert.Empty(store.UnknownKeys);
        Assert.Null(first.Extra);
        Assert.Null(first.Vr.Extra);
        Assert.Null(first.Vr.Panel.Extra);
        Assert.Null(first.Audio.Bed.Extra);

        // The property that matters, asserted on the records that hold no dictionary of their own — the whole
        // document never compared equal across two loads, because Subsystems, ProviderVoices and their like
        // are references and always have been.
        Assert.Equal(first.Vr.Panel, second.Vr.Panel);
        Assert.Equal(first.Ui.Overlay, second.Ui.Overlay);
        Assert.Equal(first.Audio, second.Audio);
        Assert.Equal(first.Callouts, second.Callouts);
    }

    /// <summary>The floor, independent of the other two.</summary>
    [Fact]
    public void ARefusedFileIsAppliedInMemoryAndNeverWrittenOver()
    {
        using var install = new TempInstall();
        const string Unreadable = "{ this is not json";
        File.WriteAllText(install.Paths.SettingsFile, Unreadable);

        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);
        Assert.Throws<SettingsLoadException>(() => store.Load());

        var surface = TestSurface.For(install, settings: new D47Settings(), loadFailed: true);

        var result = surface.Settings.Apply(
            InterfaceCapability.ThemeKey, ThemeCatalog.Guardian, SettingsCaller.Panel);

        // Applied in memory, so it is live for this run...
        Assert.Equal(ThemeCatalog.Guardian, surface.Settings.Current.Ui.Theme);

        // ...and the row says why it will not outlive it.
        Assert.Equal(SettingApplyStatus.Failed, result.Status);
        Assert.Contains("will not be remembered", result.Message, StringComparison.Ordinal);
        Assert.Contains(install.Paths.SettingsFile, result.Message, StringComparison.Ordinal);

        // Nothing was written: the Commander's file is exactly as they left it.
        Assert.Equal(Unreadable, File.ReadAllText(install.Paths.SettingsFile));
    }

    /// <summary>The same floor, reached by the other door.</summary>
    [Fact]
    public void ARefusedFileIsNotWrittenOverByAResetEither()
    {
        using var install = new TempInstall();
        const string Unreadable = "{ this is not json";
        File.WriteAllText(install.Paths.SettingsFile, Unreadable);

        var surface = TestSurface.For(install, settings: new D47Settings(), loadFailed: true);
        surface.Settings.UseCommander("F1", "HADESD");

        // A Commander-scoped row, so the reset takes the forget-my-answer path rather than a write of null.
        surface.Settings.Apply("llm.aboutMe", "my own story", SettingsCaller.Panel);

        var result = surface.Settings.Reset("llm.aboutMe", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Failed, result.Status);
        Assert.Contains("will not be remembered", result.Message, StringComparison.Ordinal);
        Assert.Equal(Unreadable, File.ReadAllText(install.Paths.SettingsFile));
    }

    /// <summary>
    /// The bag is per record, so a record added without one is a hole in the guarantee that only shows
    /// up as a Commander's lost configuration two releases later.
    /// </summary>
    [Fact]
    public void EverySettingsRecordCarriesTheBag()
    {
        var seen = new HashSet<Type>();
        var without = new List<string>();

        Visit(typeof(D47Settings), seen, without);

        Assert.Empty(without);
    }

    private static void Visit(Type type, HashSet<Type> seen, List<string> without)
    {
        if (!seen.Add(type))
        {
            return;
        }

        if (!type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => property.IsDefined(typeof(JsonExtensionDataAttribute), inherit: true)))
        {
            without.Add(type.FullName!);
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            foreach (var carried in Records(property.PropertyType))
            {
                Visit(carried, seen, without);
            }
        }
    }

    /// <summary>
    /// The record types a property can put in the document: the type itself, what a list of them holds,
    /// and what a dictionary of them holds.
    /// </summary>
    private static IEnumerable<Type> Records(Type type)
    {
        var bare = Nullable.GetUnderlyingType(type) ?? type;

        if (bare.IsClass && bare != typeof(string) && bare.Assembly == typeof(D47Settings).Assembly)
        {
            yield return bare;
            yield break;
        }

        if (!bare.IsGenericType)
        {
            yield break;
        }

        foreach (var argument in bare.GetGenericArguments().SelectMany(Records))
        {
            yield return argument;
        }
    }
}
