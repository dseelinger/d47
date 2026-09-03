using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// The closed list an entry prompt can offer, narrowing as the Commander types (#282).
/// <para>
/// <b>Narrowing rather than resolving.</b> Nothing here refuses a value or asks for a spelling —
/// a query that matches nothing leaves an empty list, and what was typed is still what the box
/// holds. That is the rule the searchable chooser already keeps, and the whole point of a picker
/// is that the Commander never has to find out afterwards that a hull was not a hull.
/// </para>
/// </summary>
public class AClosedListNarrowsAsItIsTypedTests
{
    private static readonly string[] Hulls =
        ["Anaconda", "Krait MkII", "Type-6 Transporter", "Type-9 Heavy"];

    [Fact]
    public void AnEmptyQueryLeavesEverythingShowing()
    {
        Assert.Equal(Hulls, EntrySuggestions.Narrow(Hulls, string.Empty));
        Assert.Equal(Hulls, EntrySuggestions.Narrow(Hulls, "   "));
        Assert.Equal(Hulls, EntrySuggestions.Narrow(Hulls, null));
    }

    /// <summary>
    /// Anywhere in the name rather than only at the front, and without regard for case: a
    /// Commander types what they remember of a hull, which is as often the middle of it.
    /// </summary>
    [Fact]
    public void AQueryKeepsEveryNameItAppearsIn()
    {
        Assert.Equal(
            ["Type-6 Transporter", "Type-9 Heavy"],
            EntrySuggestions.Narrow(Hulls, "type"));

        Assert.Equal(["Krait MkII"], EntrySuggestions.Narrow(Hulls, "MK"));
        Assert.Equal(["Type-6 Transporter"], EntrySuggestions.Narrow(Hulls, " transp "));
    }

    /// <summary>
    /// A query that matches nothing narrows to nothing rather than falling back to everything.
    /// An empty list says "not that" where a full one would say "any of these", and only one of
    /// those is true.
    /// </summary>
    [Fact]
    public void AQueryThatMatchesNothingShowsNothing()
    {
        Assert.Empty(EntrySuggestions.Narrow(Hulls, "Millennium Falcon"));
    }

    [Fact]
    public void NoListAtAllIsNotAList()
    {
        Assert.Empty(EntrySuggestions.Narrow(null, "Anaconda"));
        Assert.Empty(EntrySuggestions.Narrow([], "Anaconda"));
    }
}
