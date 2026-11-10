using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>
/// Opening a story's page, before it is begun, asks for its unpinned cast to be paired on the ship's provider, and the
/// Cast section shows the paired voice once the pairing lands.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AStorysCastIsPairedWhenItsPageOpensTests
{
    private const string Eleven = TtsProviderCatalog.ElevenLabsId;

    private static readonly string Juno = $"{StoryFixture.Story.Id}.juno";

    private static readonly StoryCatalog Catalog = new(
        [StoryFixture.Story],
        () =>
        [
            StoryFixture.Secret with
            {
                Cast = [new StorySpeaker { Id = "juno", Name = "Juno", Who = "A stowaway.", Provider = StorySpeaker.Kokoro, Voice = "af_river", Primary = true }],
            },
        ]);

    private static List<string> Drawn(Control control) =>
        [.. control.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public async Task TheCastRowShowsThePairedVoiceOnceThePairingLands()
    {
        var paths = new AppPaths(TempFolders.Create("d47-cast-paired-on-open"));
        paths.EnsureCreated();

        var surface = AdventureFixture.Surface(paths);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), new DiskFileSystem(), NullLogger<StoryStore>.Instance);
        var pairings = new Dictionary<string, StoryVoiceChoice>(StringComparer.Ordinal);
        var director = new StoryDirector(
            stories, surface.Book, () => Catalog, surface.Generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance)
        {
            Ship = () => new ShipSpeech(Eleven, pairings),
        };

        var asked = new List<string>();
        var landed = new TaskCompletionSource();

        var voices = new CastVoiceSurface(
            director.CastMember,
            (_, _, _, _) => { },
            (_, _) => Task.FromResult(VoiceCatalogue.Silent),
            (_, _) => { },
            _ => null,
            PairStory: (id, _) =>
            {
                lock (asked)
                {
                    asked.Add(id);
                }

                if (pairings.ContainsKey(Juno))
                {
                    return Task.FromResult(false);
                }

                pairings[Juno] = new StoryVoiceChoice(Eleven, "v-george") { VoiceName = "George" };
                landed.TrySetResult();
                return Task.FromResult(true);
            });

        var nav = new PanelNavigator();
        var crumb = new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Story.Id, StoryFixture.Story.Title) { Level = StoriesView.ReadPrefix };
        nav.Register(PanelTab.Stories, StoriesView.Crumb);
        Assert.True(nav.GoTo(PanelTab.Stories, StoriesView.Crumb, crumb));

        var view = new StoriesView(surface with { Stories = director, CastVoices = voices, Messages = null }, director, nav, new PanelPrompts(nav, new Avalonia.Controls.Panel()));
        var page = view.Build(crumb)!;
        var window = new Window { Content = page, Width = 640, Height = 900 };
        window.Show();

        try
        {
            await landed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

            while (!Drawn(page).Contains("ElevenLabs · George (the story's voice)"))
            {
                Assert.True(DateTime.UtcNow < deadline, string.Join(" | ", Drawn(page)));
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }

            Assert.DoesNotContain(Drawn(page), text => text.Contains("the crew voice", StringComparison.Ordinal));

            lock (asked)
            {
                Assert.NotEmpty(asked);
                Assert.All(asked, id => Assert.Equal(StoryFixture.Story.Id, id));
            }
        }
        finally
        {
            window.Close();
        }
    }
}
