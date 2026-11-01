using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>"Delete the thumbs-down tracks" is a phrase for the music library's maintainer only (#2034).</summary>
public sealed class DeletingTheThumbsDownTracksIsTheMaintainersTests
{
    private bool _token = true;
    private int _started;

    [Theory]
    [InlineData("delete the thumbs-down tracks")]
    [InlineData("delete the thumbs down tracks")]
    [InlineData("Delete the thumbs-down tracks.")]
    public void WithATokenThePhraseReachesTheDeleteToolAndNotThumbsDown(string said)
    {
        var match = Router().MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.Equal(AudioCapability.DeleteRejectsTool, match.ToolName);
    }

    [Fact]
    public void WithoutATokenThePhraseIsNotMatched()
    {
        _token = false;

        Assert.Null(Router().MatchToolCommand("delete the thumbs-down tracks"));
    }

    [Fact]
    public async Task WithATokenThePhraseStartsTheDeletion()
    {
        var match = Router().MatchToolCommand("delete the thumbs-down tracks")!;

        var result = await CapabilityRegistry.Build([Capability()])
            .InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken, ToolCaller.Commander);

        Assert.False(result.IsError);
        Assert.Equal("Deleting 2 tracks in 1 folder from the music library.", result.Content);
        Assert.Equal(1, _started);
    }

    [Fact]
    public void NoToolListOfferedToTheModelHoldsIt()
    {
        var registry = CapabilityRegistry.Build([Capability()]);

        foreach (var profile in ToolSurface.All(registry))
        {
            Assert.DoesNotContain(profile.Tools, tool => tool.Name == AudioCapability.DeleteRejectsTool);
        }
    }

    [Fact]
    public async Task TheModelCannotCallIt()
    {
        var registry = CapabilityRegistry.Build([Capability()]);

        var result = await registry.InvokeAsync(
            AudioCapability.DeleteRejectsTool,
            new ToolArguments(new Dictionary<string, string>()),
            TestContext.Current.CancellationToken,
            ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.Equal(0, _started);
    }

    [Fact]
    public void RemovingRowsKeepsARowQueuedSinceTheyWereRead()
    {
        var files = new MemoryFileSystem();
        var rejects = new MusicRejects(@"C:\data\music-rejects.tsv", files);
        rejects.Add(Situations.Docked, "berth.mp3");
        rejects.Add(Situations.General, "hum.mp3");
        var read = rejects.Rows.Where(row => row.Folder == Situations.Docked).ToList();

        rejects.Add(Situations.Docked, "hangar.mp3");
        rejects.Remove(read);

        Assert.Equal([(Situations.General, "hum.mp3"), (Situations.Docked, "hangar.mp3")], rejects.Rows);
    }

    private CapabilityDescriptor Capability() =>
        AudioCapability.Create(
            maintainer: () => _token,
            deleteRejects: () =>
            {
                _started++;
                return "Deleting 2 tracks in 1 folder from the music library.";
            });

    private KeywordRouter Router() => new(CapabilityRegistry.Build([Capability()]));
}
