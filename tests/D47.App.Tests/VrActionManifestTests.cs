using System.Text;
using System.Text.Json;
using D47.Core.Storage;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>The action manifest and its bindings, checked as files.</summary>
[Trait("Category", "Integration")]
public class VrActionManifestTests
{
    private readonly MemoryFileSystem _files = new();

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-actions");

    /// <summary>Both Oculus profiles, and that is not redundancy.</summary>
    [Theory]
    [InlineData("oculus_touch")]
    [InlineData("rift")]
    [InlineData("knuckles")]
    [InlineData("vive_controller")]
    public void EveryDeclaredProfileHasABindingFileBesideIt(string profile)
    {
        var manifest = Read(VrActionManifest.Write(_files, _folder));

        var declared = manifest.GetProperty("default_bindings")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("controller_type").GetString() == profile)
            .GetProperty("binding_url")
            .GetString()!;

        // Relative to the manifest, which is how SteamVR resolves it — an absolute path here binds nothing on
        // a machine whose folder is somewhere else.
        Assert.False(Path.IsPathRooted(declared));

        var binding = Read(Path.Combine(_folder, declared));

        Assert.Equal(profile, binding.GetProperty("controller_type").GetString());
    }

    /// <summary>
    /// The action the bindings output to has to be the one the manifest declares, character for
    /// character.
    /// </summary>
    [Fact]
    public void TheBindingsOutputToTheActionTheManifestDeclares()
    {
        var manifest = Read(VrActionManifest.Write(_files, _folder));

        var declared = manifest.GetProperty("actions").EnumerateArray()
            .Select(action => action.GetProperty("name").GetString() ?? string.Empty)
            .ToList();

        Assert.Equal(
            new List<string>
            {
                VrActionManifest.GrabAction,
                VrActionManifest.BackAction,
                VrActionManifest.PrimaryAction,
                VrActionManifest.SecondaryAction,
                VrActionManifest.MenuAction,
                VrActionManifest.StickClickAction,
                VrActionManifest.StickAction,
            },
            declared);

        foreach (var file in _files.Enumerate(_folder, "binding_*.json"))
        {
            var outputs = Outputs(Read(file));

            Assert.All(outputs, output => Assert.Contains(output, declared));
            Assert.Contains(VrActionManifest.GrabAction, outputs);
            Assert.Contains(VrActionManifest.BackAction, outputs);
        }
    }

    /// <summary>Each profile binds every action its controller can supply.</summary>
    [Theory]
    [InlineData("oculus_touch", new[] { "primary", "secondary", "menu", "stickclick", "stick" })]
    [InlineData("rift", new[] { "primary", "secondary", "menu", "stickclick", "stick" })]
    [InlineData("knuckles", new[] { "primary", "secondary", "stickclick", "stick" })]
    [InlineData("vive_controller", new[] { "menu", "stickclick", "stick" })]
    public void EachProfileBindsEveryActionItsControllerCanSupply(string profile, string[] expected)
    {
        VrActionManifest.Write(_files, _folder);

        var bound = Outputs(Read(Path.Combine(_folder, $"binding_{profile}.json")));
        var want = expected.Select(name => $"/actions/panel/in/{name}")
            .Append(VrActionManifest.GrabAction)
            .Append(VrActionManifest.BackAction)
            .Order(StringComparer.Ordinal);

        Assert.Equal(want, bound.Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>A Vive controller's trackpad is its stick.</summary>
    [Fact]
    public void AViveTrackpadIsBoundAsTheStick()
    {
        VrActionManifest.Write(_files, _folder);

        var sources = Read(Path.Combine(_folder, "binding_vive_controller.json"))
            .GetProperty("bindings").GetProperty(VrActionManifest.ActionSet).GetProperty("sources")
            .EnumerateArray().ToArray();

        Assert.Contains(sources, source =>
            source.GetProperty("path").GetString() == "/user/hand/left/input/trackpad"
            && source.GetProperty("inputs").GetProperty("position").GetProperty("output").GetString()
               == VrActionManifest.StickAction);
    }

    private static List<string> Outputs(JsonElement binding) =>
        binding.GetProperty("bindings").GetProperty(VrActionManifest.ActionSet).GetProperty("sources")
            .EnumerateArray()
            .SelectMany(source => source.GetProperty("inputs").EnumerateObject())
            .Select(input => input.Value.GetProperty("output").GetString() ?? string.Empty)
            .ToList();

    /// <summary>
    /// The application manifest names this process and points back at the action manifest by absolute
    /// path.
    /// </summary>
    [Fact]
    public void TheApplicationManifestNamesThisProcessAndItsActions()
    {
        var actions = VrActionManifest.Write(_files, _folder);
        var application = Read(VrActionManifest.WriteAppManifest(_files, _folder, actions));

        var app = application.GetProperty("applications").EnumerateArray().Single();

        Assert.Equal(VrActionManifest.AppKey, app.GetProperty("app_key").GetString());
        Assert.Equal(actions, app.GetProperty("action_manifest_path").GetString());
        Assert.True(Path.IsPathRooted(app.GetProperty("action_manifest_path").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(app.GetProperty("binary_path_windows").GetString()));
    }

    [Fact]
    public void NothingIsWrittenWithAByteOrderMark()
    {
        VrActionManifest.WriteAppManifest(_files, _folder, VrActionManifest.Write(_files, _folder));

        foreach (var file in _files.Enumerate(_folder, "*"))
        {
            var head = _files.ReadBytes(file)!.Take(3);

            Assert.False(
                head.SequenceEqual(Encoding.UTF8.Preamble.ToArray()),
                $"{Path.GetFileName(file)} was written with a BOM");
        }
    }

    /// <summary>Writing twice is what a rebuilt session does, and it must not accumulate or throw.</summary>
    [Fact]
    public void WritingAgainOverAnExistingFolderIsFine()
    {
        var first = VrActionManifest.Write(_files, _folder);
        var again = VrActionManifest.Write(_files, _folder);

        Assert.Equal(first, again);
        Assert.Equal(5, _files.Enumerate(_folder, "*").Count);
    }

    private JsonElement Read(string path) =>
        JsonDocument.Parse(_files.ReadText(path)!).RootElement;
}
