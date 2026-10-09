using D47.App.Input;
using D47.Core.Hotas;
using D47.Core.Ticking;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The tick reads the controllers from the sampler's latest sample, so a Windows.Gaming.Input call that
/// waits holds up the sampler's thread and not push-to-talk (#916).
/// </summary>
public class TheTickReadsTheSticksWithoutWaitingOnWindowsTests
{
    private const string Stick = "stick-1";

    [Fact]
    public async Task ASampleThatHangsLeavesTheTickTheSampleBeforeIt()
    {
        var cancel = TestContext.Current.CancellationToken;
        using var reader = new HangingReader(new FakeHotasReader().Holding(Stick, 8, 3));
        using var sampler = new ControllerSampler(reader, NullLogger<ControllerSampler>.Instance);

        sampler.Sample();
        reader.Hang();

        var hung = Task.Run(sampler.Sample, cancel);

        Assert.True(reader.Entered.Wait(TimeSpan.FromSeconds(5), cancel));
        Assert.True(sampler.IsSettled);
        Assert.Single(sampler.Poll(), reading => reading.Id == Stick && reading.Buttons[3]);

        reader.Release();
        await hung.WaitAsync(TimeSpan.FromSeconds(5), cancel);
    }

    [Fact]
    public void AnUnsettledReaderIsSampledAsNothingAndNotPolled()
    {
        var reader = new FakeHotasReader { IsSettled = false }.Holding(Stick, 8, 3);
        using var sampler = new ControllerSampler(reader, NullLogger<ControllerSampler>.Instance);

        sampler.Sample();

        Assert.False(sampler.IsSettled);
        Assert.Empty(sampler.Poll());
        Assert.Equal(0, reader.Polls);
    }

    [Fact]
    public void ThePushToTalkButtonIsReadFromTheLatestSample()
    {
        var reader = new FakeHotasReader().Holding(Stick, 8);
        using var sampler = new ControllerSampler(reader, NullLogger<ControllerSampler>.Instance);
        var pushToTalk = new BoundButton();
        var presses = 0;

        pushToTalk.Bind(new HotasButton(Stick, 3));
        pushToTalk.Pressed += () => presses++;

        sampler.Sample();
        pushToTalk.Poll(sampler.Poll());

        reader.Holding(Stick, 8, 3);
        sampler.Sample();
        pushToTalk.Poll(sampler.Poll());

        Assert.Equal(1, presses);
    }

    [Fact]
    public void AHeldButtonIsReleasedOnceTheSampleGoesStale()
    {
        var now = TimeSpan.Zero;
        var reader = new FakeHotasReader().Holding(Stick, 8, 3);
        using var sampler = new ControllerSampler(reader, NullLogger<ControllerSampler>.Instance, () => now);
        var pushToTalk = new BoundButton();
        var releases = 0;

        pushToTalk.Bind(new HotasButton(Stick, 3));
        pushToTalk.Released += () => releases++;

        sampler.Sample();
        pushToTalk.Poll(sampler.Poll());

        now += ControllerSampler.Stale;
        pushToTalk.Poll(sampler.Poll());

        Assert.Equal(0, releases);

        now += TimeSpan.FromMilliseconds(1);
        pushToTalk.Poll(sampler.Poll());

        Assert.Empty(sampler.Poll());
        Assert.Equal(1, releases);
    }

    [Fact]
    public void ASampleIsTakenMoreOftenThanATick()
    {
        Assert.True(ControllerSampler.Period < TickLoop.DefaultPeriod);
    }

    [Fact]
    public void TheTickSubscribersReadTheSamplerAndNotTheControllers()
    {
        Assert.NotEmpty(AppSource.CodeLines("PollTheStick(sampledControllers,"));
        Assert.NotEmpty(AppSource.CodeLines("Readings = sampledControllers.Poll(),"));
        Assert.Empty(AppSource.CodeLinesMatchingOutside(
            "SelfTest",
            new System.Text.RegularExpressions.Regex(@"\b_?controllers\??\.(Poll\(|IsSettled)")));
    }

    /// <summary>A reader whose poll can be made to wait, as a Windows call might.</summary>
    private sealed class HangingReader(IHotasReader inner) : IHotasReader, IDisposable
    {
        private readonly ManualResetEventSlim _released = new(true);

        public ManualResetEventSlim Entered { get; } = new(false);

        public bool IsSettled => inner.IsSettled;

        public string? Unavailable => inner.Unavailable;

        public void Hang() => _released.Reset();

        public void Release() => _released.Set();

        public IReadOnlyList<HotasReading> Poll()
        {
            if (!_released.IsSet)
            {
                Entered.Set();
                _released.Wait();
            }

            return inner.Poll();
        }

        public void Dispose()
        {
            _released.Dispose();
            Entered.Dispose();
        }
    }
}
