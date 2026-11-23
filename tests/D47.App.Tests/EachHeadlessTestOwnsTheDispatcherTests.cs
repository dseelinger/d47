using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The headless session resets the dispatcher before each test, and the first thread to read
/// <see cref="Dispatcher.UIThread"/> after that owns it. A thread left over from another test, or a plain test
/// running in parallel, can read it before the session thread does; setup then fails in the render loop.
/// </summary>
public sealed class EachHeadlessTestOwnsTheDispatcherTests
{
    [AvaloniaFact]
    public void AThreadThatReadsTheDispatcherBeforeSetupDoesNotKeepIt()
    {
        typeof(Dispatcher).GetMethod("ResetBeforeUnitTests", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);

        var stray = new Thread(() => _ = Dispatcher.UIThread);
        stray.Start();
        stray.Join();
        Assert.False(Dispatcher.UIThread.CheckAccess());

        TestAppBuilder.BuildAvaloniaApp().RuntimePlatformServicesInitializer!();

        Dispatcher.UIThread.VerifyAccess();
    }
}
