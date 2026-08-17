using System.Text;
using System.Text.Json;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The action manifest and its bindings, checked as files.
/// <para>
/// Every way this can be wrong is silent. SteamVR reads these in another process and reports a
/// malformed or incomplete one by binding nothing at all — the handles still resolve, the calls
/// still succeed, and the trigger simply never fires. The only trace is a line in
/// <c>vrserver.txt</c> nobody thinks to read. So the shape is asserted here, where a mistake is a
/// failing test rather than an afternoon in a headset.
/// </para>
/// </summary>
public class VrActionManifestTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "d47-actions-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// <b>Both Oculus profiles, and that is not redundancy.</b> The Oculus driver asks for each in
    /// turn and one missing binding disables input entirely, so shipping <c>oculus_touch</c>
    /// without <c>rift</c> is a Touch controller that does nothing.
    /// </summary>
    [Theory]
    [InlineData("oculus_touch")]
    [InlineData("rift")]
    [InlineData("knuckles")]
    [InlineData("vive_controller")]
    public void EveryDeclaredProfileHasABindingFileBesideIt(string profile)
    {
        var manifest = Read(VrActionManifest.Write(_folder));

        var declared = manifest.GetProperty("default_bindings")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("controller_type").GetString() == profile)
            .GetProperty("binding_url")
            .GetString()!;

        // Relative to the manifest, which is how SteamVR resolves it — an absolute path here binds
        // nothing on a machine whose folder is somewhere else.
        Assert.False(Path.IsPathRooted(declared));

        var binding = Read(Path.Combine(_folder, declared));

        Assert.Equal(profile, binding.GetProperty("controller_type").GetString());
    }

    /// <summary>
    /// The action the bindings output to has to be the one the manifest declares, character for
    /// character. A mismatch is a binding file SteamVR accepts and files against nothing.
    /// </summary>
    [Fact]
    public void TheBindingsOutputToTheActionTheManifestDeclares()
    {
        var manifest = Read(VrActionManifest.Write(_folder));

        Assert.Equal(
            VrActionManifest.GrabAction,
            manifest.GetProperty("actions").EnumerateArray().Single().GetProperty("name").GetString());

        foreach (var file in Directory.GetFiles(_folder, "binding_*.json"))
        {
            var sources = Read(file)
                .GetProperty("bindings")
                .GetProperty(VrActionManifest.ActionSet)
                .GetProperty("sources")
                .EnumerateArray()
                .ToArray();

            // Both hands, bound to the one action: the panel does not care which hand takes it,
            // and which one it was is recovered from the ray rather than from the button.
            Assert.Equal(2, sources.Length);

            Assert.All(sources, source => Assert.Equal(
                VrActionManifest.GrabAction,
                source.GetProperty("inputs").GetProperty("click").GetProperty("output").GetString()));

            Assert.Contains(sources, source =>
                source.GetProperty("path").GetString() == "/user/hand/left/input/trigger");

            Assert.Contains(sources, source =>
                source.GetProperty("path").GetString() == "/user/hand/right/input/trigger");
        }
    }

    /// <summary>
    /// The application manifest names this process and points back at the action manifest by
    /// absolute path. Without the registration it enables, SteamVR has no application key to file
    /// bindings under and the action stays bound to nothing forever.
    /// </summary>
    [Fact]
    public void TheApplicationManifestNamesThisProcessAndItsActions()
    {
        var actions = VrActionManifest.Write(_folder);
        var application = Read(VrActionManifest.WriteAppManifest(_folder, actions));

        var app = application.GetProperty("applications").EnumerateArray().Single();

        Assert.Equal(VrActionManifest.AppKey, app.GetProperty("app_key").GetString());
        Assert.Equal(actions, app.GetProperty("action_manifest_path").GetString());
        Assert.True(Path.IsPathRooted(app.GetProperty("action_manifest_path").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(app.GetProperty("binary_path_windows").GetString()));
    }

    /// <summary>
    /// No byte-order mark. SteamVR's reader takes one as the first character of the document and
    /// rejects the file — which presents, again, as a manifest that loaded and bound nothing.
    /// </summary>
    [Fact]
    public void NothingIsWrittenWithAByteOrderMark()
    {
        VrActionManifest.WriteAppManifest(_folder, VrActionManifest.Write(_folder));

        foreach (var file in Directory.GetFiles(_folder))
        {
            var head = new byte[3];
            using var stream = File.OpenRead(file);
            _ = stream.Read(head);

            Assert.False(
                head.SequenceEqual(Encoding.UTF8.Preamble.ToArray()),
                $"{Path.GetFileName(file)} was written with a BOM");
        }
    }

    /// <summary>Writing twice is what a rebuilt session does, and it must not accumulate or throw.</summary>
    [Fact]
    public void WritingAgainOverAnExistingFolderIsFine()
    {
        var first = VrActionManifest.Write(_folder);
        var again = VrActionManifest.Write(_folder);

        Assert.Equal(first, again);
        Assert.Equal(5, Directory.GetFiles(_folder).Length);
    }

    private static JsonElement Read(string path) =>
        JsonDocument.Parse(File.ReadAllText(path)).RootElement;
}
