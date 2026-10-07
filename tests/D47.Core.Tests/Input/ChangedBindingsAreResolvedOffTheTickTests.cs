using System.Security.AccessControl;
using System.Security.Principal;
using D47.Core.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>The tick notices a rebind and the resolve, which walks the game folders, runs elsewhere (#913).</summary>
public class ChangedBindingsAreResolvedOffTheTickTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "d47-binds-off-tick-tests", Guid.NewGuid().ToString("N"));

    private readonly List<Task> _dispatched = [];

    private string Bindings => Path.Combine(_root, "Options", "Bindings");

    private string Game => Path.Combine(_root, "Game");

    public ChangedBindingsAreResolvedOffTheTickTests()
    {
        Directory.CreateDirectory(Bindings);
        Directory.CreateDirectory(Game);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder is not worth failing a test over.
        }

        GC.SuppressFinalize(this);
    }

    private void Rebind(string key, int minutes)
    {
        File.WriteAllText(Path.Combine(Bindings, "StartPreset.4.start"), "Custom\nCustom\n");

        var binds = Path.Combine(Bindings, "Custom.4.2.binds");

        File.WriteAllText(
            binds,
            $"""
             <Root PresetName="Custom">
               <YawLeftButton>
                 <Primary Device="Keyboard" Key="{key}" />
               </YawLeftButton>
             </Root>
             """);

        File.SetLastWriteTimeUtc(binds, DateTime.UtcNow.AddMinutes(minutes));
    }

    /// <summary>Each resolve waits for <paramref name="release"/> before it starts, on the pool.</summary>
    private BindsWatch Watch(ManualResetEventSlim release) =>
        new(Bindings, [Game], NullLogger.Instance)
        {
            Dispatch = work => _dispatched.Add(Task.Run(() =>
            {
                release.Wait(TestContext.Current.CancellationToken);
                work();
            })),
        };

    [Fact]
    public async Task APollReturnsBeforeASlowResolveFinishes()
    {
        Rebind("Key_Q", minutes: 0);

        using var release = new ManualResetEventSlim();
        var watch = Watch(release);

        Rebind("Key_Z", minutes: 1);

        Assert.False(watch.Poll(), "the resolve is still held");
        Assert.Equal("Key_Q", Assert.Single(watch.Current.Bindings).Key);

        Assert.False(watch.Poll(), "still held");
        _ = Assert.Single(_dispatched);

        release.Set();
        await _dispatched[0].WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(watch.Poll(), "a later poll adopts the finished resolve");
        Assert.Equal("Key_Z", Assert.Single(watch.Current.Bindings).Key);

        Assert.False(watch.Poll());
        _ = Assert.Single(_dispatched);
    }

    /// <summary>A bindings folder that cannot be listed gives an unreadable stamp, which starts nothing.</summary>
    [Fact]
    public void AnUnreadableStampStartsNoResolve()
    {
        Rebind("Key_Q", minutes: 0);

        using var release = new ManualResetEventSlim(initialState: true);
        var watch = Watch(release);

        var folder = new DirectoryInfo(Bindings);
        var deny = new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!,
            FileSystemRights.ListDirectory,
            AccessControlType.Deny);

        var security = folder.GetAccessControl();
        security.AddAccessRule(deny);
        folder.SetAccessControl(security);

        try
        {
            for (var poll = 0; poll < 5; poll++)
            {
                Assert.False(watch.Poll());
            }

            Assert.Empty(_dispatched);
            Assert.Equal("Key_Q", Assert.Single(watch.Current.Bindings).Key);
        }
        finally
        {
            security.RemoveAccessRule(deny);
            folder.SetAccessControl(security);
        }
    }
}
