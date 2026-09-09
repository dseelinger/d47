using D47.App.Input;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.App.Tests;

/// <summary>The log says what is printed on the key, not what the enum calls it.</summary>
public class BoundKeysAreLoggedAsPrintedTests
{
    [Fact]
    public void ThePushToTalkKeyIsLoggedAsItIsPrinted()
    {
        var log = new Capture();
        var key = new PushToTalkKey(new CaptureLogger<PushToTalkKey>(log));

        Assert.True(key.Bind("Oem4"));

        var said = Assert.Single(log.Messages, message => message.Contains("Push-to-talk is bound"));

        Assert.Equal("Push-to-talk is bound to [", said);
        Assert.DoesNotContain("Oem4", said, StringComparison.Ordinal);
    }

    /// <summary>The stored spelling still round-trips.</summary>
    [Fact]
    public void WhatIsStoredIsUntouched()
    {
        var key = new PushToTalkKey(new CaptureLogger<PushToTalkKey>(new Capture()));

        Assert.True(key.Bind("Oem4"));
        Assert.Equal("Oem4", key.Gesture);
    }

    private sealed class Capture
    {
        public List<string> Messages { get; } = [];
    }

    private sealed class CaptureLogger<T>(Capture capture) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            capture.Messages.Add(formatter(state, exception));
    }
}
