using D47.Core.Callouts;
using D47.Core.Configuration;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// A callout in the shipped catalogue follows a settings change on the next tick rather than the next
/// launch (#139): the change goes through <see cref="SettingsService.Replace"/> and nothing else.
/// </summary>
public class ACalloutSwitchTakesEffectWithoutARestartTests
{
    private static T Shipped<T>(CalloutEngine engine) => engine.Callouts.OfType<T>().Single();

    private static void Speech(SettingsService settings, Func<SpeechSettings, SpeechSettings> change) =>
        settings.Replace("test", current => current with { Speech = change(current.Speech) });

    private static void Callouts(SettingsService settings, Func<CalloutSettings, CalloutSettings> change) =>
        settings.Replace("test", current => current with { Callouts = change(current.Callouts) });

    private static void Personality(SettingsService settings, bool on) =>
        settings.Replace("test", current => current with { Llm = current.Llm with { PersonalityEnabled = on } });

    [Fact]
    public void IncomingMessagesFollowTheirTwoSwitches()
    {
        var (engine, settings) = ShippedCatalogue.Build();
        var messages = Shipped<IncomingMessages>(engine);

        Assert.False(messages.Enabled());
        Assert.False(messages.IncludeNpcs());

        Speech(settings, speech => speech with { SpeakIncomingMessages = true });
        Assert.True(messages.Enabled());
        Assert.False(messages.IncludeNpcs());

        Speech(settings, speech => speech with { SpeakNpcMessages = true });
        Assert.True(messages.IncludeNpcs());

        Speech(settings, speech => speech with { SpeakIncomingMessages = false, SpeakNpcMessages = false });
        Assert.False(messages.Enabled());
        Assert.False(messages.IncludeNpcs());
    }

    [Theory]
    [InlineData("starsystem")]
    [InlineData("local")]
    [InlineData("wing")]
    [InlineData("squadron")]
    [InlineData("squadleaders")]
    [InlineData("player")]
    public void EachChannelFollowsItsSwitch(string channel)
    {
        var (engine, settings) = ShippedCatalogue.Build();
        var messages = Shipped<IncomingMessages>(engine);

        Assert.True(messages.ChannelEnabled(channel));

        Speech(settings, speech => Channel(speech, channel, on: false));
        Assert.False(messages.ChannelEnabled(channel));

        Speech(settings, speech => Channel(speech, channel, on: true));
        Assert.True(messages.ChannelEnabled(channel));
    }

    private static SpeechSettings Channel(SpeechSettings speech, string channel, bool on) => channel switch
    {
        "starsystem" => speech with { SpeakSystemChat = on },
        "local" => speech with { SpeakLocalChat = on },
        "wing" => speech with { SpeakWingChat = on },
        "squadron" or "squadleaders" => speech with { SpeakSquadronChat = on },
        "player" => speech with { SpeakDirectMessages = on },
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
    };

    [Fact]
    public void TheAmbientRemarkFollowsItsSwitchAndPersonality()
    {
        var (engine, settings) = ShippedCatalogue.Build();
        var ambient = Shipped<AmbientCallout>(engine);

        Assert.True(ambient.Enabled());

        Callouts(settings, callouts => callouts with { Ambient = false });
        Assert.False(ambient.Enabled());

        Callouts(settings, callouts => callouts with { Ambient = true });
        Assert.True(ambient.Enabled());

        Personality(settings, on: false);
        Assert.False(ambient.Enabled());

        Personality(settings, on: true);
        Assert.True(ambient.Enabled());
    }

    [Fact]
    public void NpcChatterFollowsItsSwitchAndPersonality()
    {
        var (engine, settings) = ShippedCatalogue.Build();
        var chatter = Shipped<NpcChatterCallout>(engine);

        Assert.True(chatter.Enabled());

        Callouts(settings, callouts => callouts with { NpcChatter = false });
        Assert.False(chatter.Enabled());

        Callouts(settings, callouts => callouts with { NpcChatter = true });
        Assert.True(chatter.Enabled());

        Personality(settings, on: false);
        Assert.False(chatter.Enabled());

        Personality(settings, on: true);
        Assert.True(chatter.Enabled());
    }

    [Fact]
    public void TheSessionLengthFollowsItsRow()
    {
        var (engine, settings) = ShippedCatalogue.Build();
        var length = Shipped<SessionLengthCallout>(engine);

        Callouts(settings, callouts => callouts with { SessionLengthHours = 7 });
        Assert.Equal(7, length.Hours());

        Callouts(settings, callouts => callouts with { SessionLengthHours = 2 });
        Assert.Equal(2, length.Hours());
    }

    /// <summary>
    /// No code line of <see cref="ShippedCallouts"/> reads <c>settings.</c> other than through
    /// <c>settings.Current.</c>: a bare read would mean <c>settings</c> is a snapshot again.
    /// </summary>
    [Trait("Category", "Gate")]
    public class TheCatalogueSource
    {
        [Fact]
        public void EverySettingsReadInTheCatalogueGoesThroughCurrent()
        {
            var bareReads = CatalogueLines()
                .Where(line => line.Contains("settings.", StringComparison.Ordinal))
                .Where(line => !line.Contains("settings.Current.", StringComparison.Ordinal))
                .Where(line => !line.Contains("SettingsService settings", StringComparison.Ordinal))
                .ToList();

            Assert.Empty(bareReads);
        }

        private static List<string> CatalogueLines()
        {
            var root = RepositoryRoot();

            var type = Directory.EnumerateFiles(Path.Combine(root, "src", "D47.Core"), "*.cs", SearchOption.AllDirectories)
                .SelectMany(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file)
                    .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Single(declared => declared.Identifier.Text == nameof(ShippedCallouts));

            var lines = type.ToFullString()
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal))
                .ToList();

            Assert.Contains(lines, line => line.Contains("settings.Current.", StringComparison.Ordinal));

            return lines;
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary.");
        }
    }
}
