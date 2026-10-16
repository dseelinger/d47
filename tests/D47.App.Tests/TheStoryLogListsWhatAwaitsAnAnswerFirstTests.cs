using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheStoryLogListsWhatAwaitsAnAnswerFirstTests
{
    private static MessageStore Store() =>
        new("messages.json", new MemoryFileSystem(), NullLogger<MessageStore>.Instance);

    private static List<string> Drawn(MessagesView view)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(TestSurface.Settings().Current.Ui.Theme);

        var window = new Window { Content = view, Width = 640, Height = 600 };
        window.Show();

        var drawn = view.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToList();
        window.Close();
        return drawn;
    }

    [AvaloniaFact]
    public void TheSectionAwaitingAnAnswerComesBeforeEveryStory()
    {
        var store = Store();
        var now = DateTimeOffset.Now;

        store.Post("narrator", "Narration", "A newer line.", now);
        store.Post("covas", "Ending", "Pick one.", now.AddMinutes(-5), "story.end.gone", [new MessageAnswer("a", "Stay")]);

        var drawn = Drawn(new MessagesView(store, new PanelNavigator()));
        var waiting = drawn.FindIndex(text => text.Equals(MessagesView.WaitingHeading, StringComparison.OrdinalIgnoreCase));
        var untied = drawn.FindIndex(text => text.Equals(StoryLog.NotTiedToAStory, StringComparison.OrdinalIgnoreCase));

        Assert.True(waiting >= 0, "no waiting section");
        Assert.True(waiting < untied, "the waiting section is not first");
        Assert.Contains("Narrator — ", string.Join('\n', drawn), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AnEmptyLogSaysNothingHasBeenSaid()
    {
        Assert.Contains(MessagesView.Empty, Drawn(new MessagesView(Store(), new PanelNavigator())));
    }
}
