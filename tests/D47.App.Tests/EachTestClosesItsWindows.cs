using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D47.App.Tests;
using Xunit.v3;

[assembly: EachTestClosesItsWindows]

namespace D47.App.Tests;

/// <summary>
/// Closes every window a test left open once the test ends, so its visual tree, timers and static event
/// subscriptions are released before the next test. The headless session runs one test at a time on the UI
/// thread, so every window still open at the end belongs to the test that just ran.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class EachTestClosesItsWindowsAttribute : BeforeAfterTestAttribute
{
    private static readonly HashSet<Window> Open = [];
    private static bool _watching;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (_watching || !OnTheUiThread(methodUnderTest))
        {
            return;
        }

        _watching = true;
        Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) => Open.Add(window), RoutingStrategies.Direct);
        Window.WindowClosedEvent.AddClassHandler<Window>((window, _) => Open.Remove(window), RoutingStrategies.Direct);
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (!OnTheUiThread(methodUnderTest))
        {
            return;
        }

        foreach (var window in Open.ToArray())
        {
            if (Open.Contains(window))
            {
                window.Close();
            }
        }

        Open.Clear();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Only Avalonia tests run on the UI thread; reading the dispatcher from any other thread claims it.</summary>
    private static bool OnTheUiThread(MethodInfo method) =>
        method.IsDefined(typeof(AvaloniaFactAttribute)) || method.IsDefined(typeof(AvaloniaTheoryAttribute));
}
