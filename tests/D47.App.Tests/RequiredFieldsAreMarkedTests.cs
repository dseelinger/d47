using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A Commander can tell which fields they have to fill
/// (<a href="https://github.com/dseelinger/d47/issues/253">#253</a>).
/// <para>
/// <b>The placeholder was carrying three incompatible meanings.</b> On one Routing card it was an
/// example (<c>Colonia</c>), on the next a source of fact (<c>this ship's</c>), on the next a
/// static default (<c>60</c>) — and in one cell the literal word <c>required</c>, which is the
/// tell: somebody hit exactly this problem, had nowhere to put the answer, and put it where it
/// vanishes the moment you type.
/// </para>
/// <para>
/// <b>An example placeholder actively misled.</b> <c>Colonia</c> sat in the same grey and the same
/// slot as the <c>60</c> below it, and on that card the grey text genuinely is what happens if you
/// type nothing — so the required fields were the ones that looked most answered.
/// </para>
/// </summary>
public sealed class RequiredFieldsAreMarkedTests
{
    private static IEnumerable<TextBlock> Captions(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>();

    /// <summary>
    /// The mark is on the label, so it is still there once the box is full — which is the whole
    /// argument against the placeholder, and is asserted by typing.
    /// </summary>
    [AvaloniaFact]
    public void TheMarkSurvivesTheFieldBeingFilled()
    {
        var field = new FormField("Destination", "a system", FieldNeed.Required);
        var window = new Window { Content = field.Control, Width = 400, Height = 200 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Captions(window), block => block.Text == FormField.RequiredMark);

        field.Box.Text = "Colonia";
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Captions(window), block => block.Text == FormField.RequiredMark);

        window.Close();
    }

    /// <summary>Optional fields are unmarked, because required is the minority by a distance.</summary>
    [AvaloniaFact]
    public void AnOptionalFieldCarriesNoMark()
    {
        var window = new Window
        {
            Content = new FormField("Efficiency", "60").Control,
            Width = 400,
            Height = 200,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(
            Captions(window),
            block => block.Text == FormField.RequiredMark || block.Text == FormField.SuppliedMark);

        window.Close();
    }

    /// <summary>
    /// The third state gets a shape of its own rather than a second asterisk. An asterisk means
    /// one thing, and giving it two meanings is the problem this issue is about.
    /// </summary>
    [AvaloniaFact]
    public void TheShipSuppliedMarkIsNotTheRequiredOne()
    {
        Assert.NotEqual(FormField.RequiredMark, FormField.SuppliedMark);

        var window = new Window
        {
            Content = new FormField("Jump range (ly)", "this ship's", FieldNeed.Supplied).Control,
            Width = 400,
            Height = 200,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Captions(window), block => block.Text == FormField.SuppliedMark);
        Assert.DoesNotContain(Captions(window), block => block.Text == FormField.RequiredMark);

        window.Close();
    }

    /// <summary>
    /// A ship-supplied placeholder quotes the figure d47 would actually use, and follows it when
    /// it changes — a placeholder naming the ship a Commander was flying an hour ago is worse than
    /// the bare phrase, because the phrase was never wrong.
    /// </summary>
    [AvaloniaFact]
    public void TheSuppliedPlaceholderShowsTheLiveValueAndFollowsIt()
    {
        var range = "28.4 ly";

        var field = new FormField("Jump range (ly)", "this ship's", FieldNeed.Supplied, () => range);

        Assert.Equal("this ship's (28.4 ly)", field.Box.PlaceholderText);

        range = "62.1 ly";
        field.Refresh();

        Assert.Equal("this ship's (62.1 ly)", field.Box.PlaceholderText);
    }

    /// <summary>
    /// No value, no parenthesis. Before the journal has been read, or with no ship known, it falls
    /// back to the bare phrase rather than drawing <c>this ship's ()</c> or a zero.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData("")]
    public void WithNothingToQuoteItDrawsTheBarePhrase(string? value)
    {
        var field = new FormField("From", "where you are now", FieldNeed.Supplied, () => value);

        Assert.Equal("where you are now", field.Box.PlaceholderText);
    }

    /// <summary>
    /// What a screen reader hears. The visible mark is a glyph and a glyph is not read aloud, so
    /// the state is spelled into the name — which is the property peers surface.
    /// </summary>
    /// <remarks>
    /// <b><c>IsRequiredForForm</c> announces nothing on Avalonia 12.1.1</b>, measured rather than
    /// assumed: no member of <c>TextBoxAutomationPeer</c> or of <c>AutomationPeer</c> mentions it,
    /// and the platform provider builds its answers from the peer. It is set anyway and asserted
    /// here so that the day it starts working is not the day this quietly says two things.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(FieldNeed.Required, "Destination, required", true)]
    [InlineData(FieldNeed.Supplied, "Destination, optional, filled from your ship", false)]
    [InlineData(FieldNeed.Optional, "Destination", false)]
    public void AScreenReaderIsToldWhatTheFieldNeeds(FieldNeed need, string spoken, bool flagged)
    {
        var field = new FormField("Destination", "a system", need);

        Assert.Equal(spoken, AutomationProperties.GetName(field.Box));
        Assert.Equal(flagged, AutomationProperties.GetIsRequiredForForm(field.Box));
    }

    /// <summary>
    /// The legend names only the marks the form uses. A key to a mark that is not on the card
    /// sends a Commander looking for something that is not there — Road to Riches has four fields
    /// and none of them is required.
    /// </summary>
    [AvaloniaFact]
    public void TheLegendNamesOnlyTheMarksInUse()
    {
        var both = new Window { Content = FormField.Legend(required: true, supplied: true) };
        var one = new Window { Content = FormField.Legend(required: true) };

        both.Show();
        one.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Captions(both), block => block.Text == "required");
        Assert.Contains(Captions(both), block => block.Text == "filled from your ship");

        Assert.Contains(Captions(one), block => block.Text == "required");
        Assert.DoesNotContain(Captions(one), block => block.Text == "filled from your ship");

        both.Close();
        one.Close();
    }

    /// <summary>
    /// And no field anywhere still says <c>required</c> in the slot where a default goes. This is
    /// the cell the issue was written about, and a string search is the only thing that keeps it
    /// from coming back on the next form somebody adds.
    /// </summary>
    [Fact]
    public void NoPlaceholderIsTheWordRequired()
    {
        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path)
                .Contains("PlaceholderText = \"required\"", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"""
             A placeholder is the word "required". It belongs on the label, where it is still true
             once the Commander has typed something: {string.Join(", ", offenders)}
             """);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
