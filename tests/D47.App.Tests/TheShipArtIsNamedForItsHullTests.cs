using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every file in <c>assets\ships</c> is named for a hull Elite actually writes
/// (<a href="https://github.com/dseelinger/d47/issues/289">#289</a>).
/// <para>
/// <b>This is a gate rather than a nicety, and it exists because the mistake shipped.</b> The
/// render pipeline names its work for people — <c>python-mk2</c>, <c>type-9-heavy</c>,
/// <c>fer-de-lance</c> — and <c>ShipArt</c> finds a hull's picture by the symbol the journal
/// writes and by nothing else. The first collection of the art copied the pipeline's names
/// straight across, which looked entirely correct: thirty-five hulls whose readable name happens
/// to equal their symbol drew, and twelve did not. Nothing failed. The Python MkII's card was
/// simply blank, next to the Anaconda's, which was not.
/// </para>
/// <para>
/// <b>What is checked is what the repository holds.</b> The card stills are committed, because
/// they ship inside the download. The 4K pictures and the turntables are not — 260 MB that change
/// when somebody re-renders a hull and never otherwise, published as release assets and fetched
/// (see <c>.gitignore</c> and <c>ShipArtStore.Source</c>) — so a clean checkout has only the
/// stills and these run against whatever is actually there. The one thing deliberately not
/// asserted is that every hull has art: a hull nobody has captured yet is the ordinary case and
/// always will be.
/// </para>
/// </summary>
public class TheShipArtIsNamedForItsHullTests
{
    private static string Assets
    {
        get
        {
            var at = new DirectoryInfo(AppContext.BaseDirectory);

            while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "assets", "ships")))
            {
                at = at.Parent;
            }

            return at is null
                ? throw new DirectoryNotFoundException("assets/ships not found above the test binary")
                : Path.Combine(at.FullName, "assets", "ships");
        }
    }

    /// <summary>Every hull the art speaks for, once, with what it has.</summary>
    private static Dictionary<string, List<string>> Hulls()
    {
        var hulls = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var path in Directory.GetFiles(Assets))
        {
            var name = Path.GetFileName(path);

            var suffix = name.EndsWith(".spin.mp4", StringComparison.Ordinal) ? ".spin.mp4"
                : name.EndsWith(".4k.png", StringComparison.Ordinal) ? ".4k.png"
                : name.EndsWith(".png", StringComparison.Ordinal) ? ".png"
                : throw new Xunit.Sdk.XunitException(
                    $"{name} is not one of a hull's three files (.png, .4k.png, .spin.mp4).");

            var symbol = name[..^suffix.Length];

            if (!hulls.TryGetValue(symbol, out var has))
            {
                hulls[symbol] = has = [];
            }

            has.Add(suffix);
        }

        return hulls;
    }

    [Fact]
    public void EveryDrawingNamesAHullEliteWrites()
    {
        var strangers = Hulls().Keys
            .Where(symbol => EliteSpecifications.HullName(symbol) is null)
            .ToArray();

        Assert.True(
            strangers.Length == 0,
            "Art named for something that is not a hull symbol: " + string.Join(", ", strangers)
            + ". The render pipeline's folder names are not Elite's; tools\\ship-art.ps1 carries "
            + "the table that turns one into the other.");
    }

    /// <summary>
    /// A large file with no card still behind it is art nothing can ever reach: the still is what
    /// puts a hull on a card, and a page nobody can open is a page nobody fetches a picture for.
    /// </summary>
    [Fact]
    public void EveryHullWithLargeArtHasTheStillThatShips()
    {
        var unreachable = Hulls()
            .Where(hull => !hull.Value.Contains(".png") && hull.Value.Count > 0)
            .Select(hull => $"{hull.Key} ({string.Join(" ", hull.Value.Order())})")
            .ToArray();

        Assert.True(
            unreachable.Length == 0,
            "Large hull art with no card still behind it: " + string.Join(", ", unreachable));
    }

    /// <summary>
    /// The card still is what the installer carries, so its size is a number somebody should have
    /// to change on purpose. Eleven megabytes buys a fleet with pictures on it; a hundred would be
    /// the download this design exists to avoid.
    /// </summary>
    [Fact]
    public void TheStillsThatShipStayWorthShipping()
    {
        var shipped = Directory.GetFiles(Assets, "*.png")
            .Where(file => !file.EndsWith(".4k.png", StringComparison.Ordinal))
            .Sum(file => new FileInfo(file).Length);

        Assert.True(
            shipped < 25 * 1024 * 1024,
            $"The card stills are {shipped / (1024 * 1024)} MB and every installation carries them.");
    }
}
