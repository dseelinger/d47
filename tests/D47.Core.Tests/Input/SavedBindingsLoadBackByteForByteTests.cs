using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>A binding profile saves Elite's set and loads it back unchanged, and never while Elite runs (#80).</summary>
public sealed class SavedBindingsLoadBackByteForByteTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("d47-binding-profiles-");
    private bool _running;

    private string Bindings => Path.Combine(_root.FullName, "Bindings");

    private string Profiles => Path.Combine(_root.FullName, "binding-profiles");

    public SavedBindingsLoadBackByteForByteTests()
    {
        Directory.CreateDirectory(Bindings);
        File.WriteAllText(Path.Combine(Bindings, "StartPreset.4.start"), "Custom\r\nCustom\r\nSrvOnly\r\nCustom");
        File.WriteAllBytes(Path.Combine(Bindings, "Custom.4.2.binds"), Bytes(1));
        File.WriteAllBytes(Path.Combine(Bindings, "SrvOnly.binds"), Bytes(2));
        File.WriteAllBytes(Path.Combine(Bindings, "Custom.4.2.binds.123.backup"), Bytes(3));
        File.WriteAllBytes(Path.Combine(Bindings, "Unused.4.2.binds"), Bytes(4));
    }

    public void Dispose() => _root.Delete(recursive: true);

    private BindingProfiles Store() =>
        new(Bindings, Profiles, () => _running, NullLogger.Instance);

    private static byte[] Bytes(int seed)
    {
        var bytes = new byte[4096];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    private Dictionary<string, byte[]> Snapshot() =>
        Directory.GetFiles(Bindings).ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void SaveThenLoadPutsBackEveryFileOfTheSetByteForByte()
    {
        var store = Store();
        var before = Snapshot();

        Assert.True(store.Save("sim pit").Done);

        File.WriteAllText(Path.Combine(Bindings, "StartPreset.4.start"), "Other\r\nOther\r\nOther\r\nOther");
        File.WriteAllBytes(Path.Combine(Bindings, "Custom.4.2.binds"), Bytes(9));
        File.WriteAllBytes(Path.Combine(Bindings, "SrvOnly.binds"), Bytes(10));
        File.WriteAllText(Path.Combine(Bindings, "StartPreset.5.start"), "Other");

        var loaded = store.Load("SIM PIT");

        Assert.True(loaded.Done, loaded.Reply);
        Assert.False(File.Exists(Path.Combine(Bindings, "StartPreset.5.start")));

        foreach (var name in new[] { "StartPreset.4.start", "Custom.4.2.binds", "SrvOnly.binds" })
        {
            Assert.Equal(before[name], File.ReadAllBytes(Path.Combine(Bindings, name)));
        }
    }

    [Fact]
    public void ASaveTakesTheStartFilesAndTheBindsTheyNameAndNothingElse()
    {
        Assert.True(Store().Save("sim pit").Done);

        var saved = Directory.GetFiles(Path.Combine(Profiles, "sim pit")).Select(Path.GetFileName).Order();

        Assert.Equal(["Custom.4.2.binds", "SrvOnly.binds", "StartPreset.4.start"], saved);
    }

    [Fact]
    public void ALoadKeepsTheSetItReplacesAsBeforeLastLoad()
    {
        var store = Store();
        store.Save("sim pit");
        File.WriteAllBytes(Path.Combine(Bindings, "Custom.4.2.binds"), Bytes(9));

        var loaded = store.Load("sim pit");

        Assert.Contains(BindingProfiles.BeforeLastLoad, loaded.Reply, StringComparison.Ordinal);
        Assert.Equal(
            Bytes(9),
            File.ReadAllBytes(Path.Combine(Profiles, BindingProfiles.BeforeLastLoad, "Custom.4.2.binds")));
        Assert.Equal(["before last load", "sim pit"], store.Names);
    }

    [Fact]
    public void LoadingBeforeLastLoadSwapsBackRatherThanLosingIt()
    {
        var store = Store();
        store.Save("sim pit");
        File.WriteAllBytes(Path.Combine(Bindings, "Custom.4.2.binds"), Bytes(9));
        store.Load("sim pit");

        Assert.True(store.Load(BindingProfiles.BeforeLastLoad).Done);

        Assert.Equal(Bytes(9), File.ReadAllBytes(Path.Combine(Bindings, "Custom.4.2.binds")));
    }

    [Fact]
    public void ALoadWhileEliteRunsIsRefusedWithTheReasonAndTouchesNothing()
    {
        var store = Store();
        store.Save("sim pit");
        File.WriteAllBytes(Path.Combine(Bindings, "Custom.4.2.binds"), Bytes(9));
        var before = Snapshot();
        _running = true;

        var loaded = store.Load("sim pit");

        Assert.False(loaded.Done);
        Assert.Equal(BindingProfiles.EliteIsRunning, loaded.Reply);
        Assert.Equal(before.Keys.Order(), Snapshot().Keys.Order());
        Assert.Equal(Bytes(9), File.ReadAllBytes(Path.Combine(Bindings, "Custom.4.2.binds")));
        Assert.Equal(["sim pit"], store.Names);
    }

    [Fact]
    public void ASaveWhileEliteRunsIsRefusedWithTheReason()
    {
        _running = true;

        var saved = Store().Save("sim pit");

        Assert.False(saved.Done);
        Assert.Equal(BindingProfiles.EliteIsRunning, saved.Reply);
        Assert.False(Directory.Exists(Profiles));
    }

    [Fact]
    public void ASaveThatFailsKeepsTheProfileItWouldHaveReplaced()
    {
        var store = Store();
        store.Save("sim pit");
        File.WriteAllBytes(Path.Combine(Bindings, "Custom.4.2.binds"), Bytes(9));

        using (File.Open(Path.Combine(Bindings, "Custom.4.2.binds"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(store.Save("sim pit").Done);
        }

        Assert.Equal(Bytes(1), File.ReadAllBytes(Path.Combine(Profiles, "sim pit", "Custom.4.2.binds")));
    }

    [Fact]
    public void AProfileWithNoStartFileIsRefusedAndTouchesNothing()
    {
        Directory.CreateDirectory(Path.Combine(Profiles, "loose"));
        File.WriteAllBytes(Path.Combine(Profiles, "loose", "Custom.4.2.binds"), Bytes(9));
        var before = Snapshot();

        Assert.False(Store().Load("loose").Done);

        Assert.Equal(before.Keys.Order(), Snapshot().Keys.Order());
        Assert.Equal(Bytes(1), File.ReadAllBytes(Path.Combine(Bindings, "Custom.4.2.binds")));
    }

    [Fact]
    public void DeleteRemovesTheCopyAndLeavesEliteAlone()
    {
        var store = Store();
        store.Save("sim pit");
        var before = Snapshot();

        Assert.True(store.Delete("sim pit").Done);

        Assert.Empty(store.Names);
        Assert.Equal(before.Keys.Order(), Snapshot().Keys.Order());
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("c:\\temp")]
    [InlineData("")]
    [InlineData("con")]
    [InlineData("COM1")]
    public void ANameThatIsNotAFolderNameIsRefused(string name)
    {
        Assert.False(Store().Save(name).Done);
        Assert.False(Directory.Exists(Profiles));
    }

    [Fact]
    public async Task TheLoadToolAnswersWithTheRefusalWhileEliteRuns()
    {
        _running = true;
        var tool = BindingProfilesCapability.Create(Store()).Tools.Single(t => t.Name == BindingProfilesCapability.LoadTool);

        var result = await tool.Handler(BindingProfilesCapability.ArgumentsFor("sim pit"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(BindingProfiles.EliteIsRunning, result.Content);
    }

    [Theory]
    [InlineData("save these bindings as sim pit", BindingProfilesCapability.SaveTool, "sim pit")]
    [InlineData("Save my binds as desk.", BindingProfilesCapability.SaveTool, "desk")]
    [InlineData("load the sim pit bindings", BindingProfilesCapability.LoadTool, "sim pit")]
    [InlineData("load my desk binds", BindingProfilesCapability.LoadTool, "desk")]
    [InlineData("load bindings called sim pit", BindingProfilesCapability.LoadTool, "sim pit")]
    [InlineData("save my controls as desk", BindingProfilesCapability.SaveTool, "desk")]
    public void TheNameIsReadFromTheSentence(string said, string tool, string name)
    {
        Assert.Equal(new BindingProfileReading(tool, name), BindingProfilePhrase.Read(said));
    }

    [Theory]
    [InlineData("load the bindings")]
    [InlineData("save the bindings")]
    [InlineData("what bindings do I have")]
    [InlineData("save this as a bookmark")]
    [InlineData("load the flight controls")]
    [InlineData("switch to the galaxy map controls")]
    public void ASentenceWithNoNameIsNotTaken(string said)
    {
        Assert.Null(BindingProfilePhrase.Read(said));
    }
}
