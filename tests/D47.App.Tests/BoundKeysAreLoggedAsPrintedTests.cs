using D47.App.Input;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The log says what is printed on the key, not what the enum calls it.
/// <para>
/// <see cref="GestureDisplayTests"/> already asserts that <see cref="Gestures.Describe(string?)"/>
/// turns <c>Oem4</c> into <kbd>[</kbd>, and it passed throughout — because the log line never
/// called it. A Commander reading their own log file found "Push-to-talk is bound to Oem4"
/// (remediation.md 10, item 8). Asserting the helper is not asserting the message, so this
/// reads the message.
/// </para>
/// </summary>
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

    /// <summary>
    /// The stored spelling still round-trips. Describing a key is about the message and must not
    /// reach what is written down, or a settings file stops parsing.
    /// </summary>
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
