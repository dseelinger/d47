using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using D47.Core.Lore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The notes dialog shows the shipped lore for the current system, read-only, above the Commander's notes (#544).</summary>
[Trait("Category", "Integration")]
public class TheLoreDialogShowsWhatD47ShipsForTheSystemTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static Window Open(long address, string name)
    {
        var root = TempFolders.Create("d47-lore-shipped");
        var book = new LoreBook(new LoreStore(Path.Combine(root, "lore.json"), NullLogger<LoreStore>.Instance));

        book.Add(address, name, "My own note.", LoreArrival.Panel, Instant, null);

        var editing = new LoreEditing(
            book,
            () => new LoreCapability.LorePlace(address, name, null),
            () => false,
            (_, _) => Task.FromResult<string?>(null),
            () => Instant);

        var window = new Window { Content = new LoreDialog(editing), Width = 720, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static string[] Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToArray();

    [AvaloniaFact]
    public void AShippedSystemShowsItsLoreAboveTheNotesWithNoForget()
    {
        var shipped = LoreDirectory.All.First();
        var window = Open(shipped.SystemAddress, shipped.Name);
        var texts = Texts(window);

        var ours = Array.IndexOf(texts, shipped.Spoken());
        var mine = Array.IndexOf(texts, "My own note.");

        Assert.True(ours >= 0, "the shipped text is shown");
        Assert.True(ours < mine, "the shipped text is above the notes");
        Assert.Contains(texts, t => t.Equals("from d47's table", StringComparison.OrdinalIgnoreCase));

        var forgets = window.GetVisualDescendants().OfType<Button>().Count(b => Equals(b.Content, "Forget"));
        Assert.Equal(1, forgets);
    }

    [AvaloniaFact]
    public void ASystemWithNoShippedLoreIsAsItWasBefore()
    {
        var window = Open(-12345L, "Nowhere");
        var texts = Texts(window);

        Assert.DoesNotContain(texts, t => t.Equals("from d47's table", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(texts, t => t.Equals("What D47 knows here", StringComparison.OrdinalIgnoreCase));
    }
}
