using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Stories;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Under a chapter's told beat line, Change voice appears for a story cast member only (#2206).</summary>
public class ACastMembersToldLineHasChangeVoiceTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private static readonly CastVoiceSurface Voices = new(
        key => key == "story.juno"
            ? new StoryCastMember("story", "Story", new StorySpeakerShown("juno", "Juno", "kokoro", "af_river", key), new PinnedVoice("kokoro", "af_river"), new PinnedVoice("kokoro", "af_river"))
            : null,
        (_, _, _, _) => { },
        (_, _) => Task.FromResult(VoiceCatalogue.Silent),
        (_, _) => { },
        _ => null);

    private static AdventureTold Beat(string speaker, string picture, int line) => new()
    {
        Kind = AdventureToldKind.Beat,
        Text = "A line.",
        At = At,
        Beat = 0,
        Line = line,
        Speaker = speaker,
        Picture = picture,
    };

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ChangeVoiceIsUnderTheCastLineOnlyAndOpensForThatMember()
    {
        var surface = AdventureFixture.Surface(new AppPaths(TempFolders.Create("d47-told-voice"))) with { CastVoices = Voices };
        var entries = new ToldEntries(surface, () => null);
        var adventure = new Adventure { Key = "k", Name = "K", Source = AdventureSource.Generated, AcceptedAt = At };
        var opened = new List<string>();

        var cast = entries.Entry(adventure, Beat("Juno", "story.juno", 1), null, null, opened.Add);
        var narrator = entries.Entry(adventure, Beat("Narrator", SpeakerPictures.Narrator, 1), null, null, opened.Add);
        var without = entries.Entry(adventure, Beat("Juno", "story.juno", 1), null, null);

        Assert.Single(Buttons(cast));
        Assert.Empty(Buttons(narrator));
        Assert.Empty(Buttons(without));

        Buttons(cast)[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(["story.juno"], opened);
    }

    private static List<Button> Buttons(Control control) =>
        [.. control.GetVisualDescendants().OfType<Button>().Where(button => button.Content as string == "Change voice")];
}
