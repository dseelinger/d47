using D47.Core.Knowledge;
using D47.Core.Watches;
using Xunit;

namespace D47.Core.Tests.Watches;

public class ACommunityGoalLaunchIsRecognisedFromGalNetWordingTests
{
    private static GalNetArticle Article(string title, string last) => new(
        "id", title, ["An opening paragraph.", "A middle paragraph.", last], DateTimeOffset.UnixEpoch, "3310");

    [Theory]
    [InlineData("Faulcon DeLacy Plans Anaconda Mk II Field Trials", "Pilots are invited to support the Anaconda Mk II's launch by registering at the megaship.")]
    [InlineData("Wreaken Calls for Surface Mining Support Tests", "Interested pilots should head to the megaship Zero Squared to take part.")]
    [InlineData("Brewer Corporation's Carrier Jump Satellite Plans", "Pilots are invited to sign up for the initiative at any Brewer outpost.")]
    [InlineData("Colonia Calls for Assistance as Celebrations Get Underway", "Commanders should make their way to Jaques Station.")]
    [InlineData("Vista Genomics Seeks Exploration Data From Sanguineous Rim", "Vista Genomics is calling on all Commanders to deliver exploration data.")]
    [InlineData("October Consortium Seeks Guardian Artefacts for New Study", "The consortium has called upon Commanders with ancient relics to help.")]
    [InlineData("Exploration Data Requested for New Deep-Space Station", "Pilots wishing to register their interest can do so at Hutton Orbital.")]
    [InlineData("Radicoida Unica Begins Blooming as New Research Initiative Announced", "Those who wish to take part are registering at Bluford Orbital.")]
    [InlineData("Corporate Forces Trigger Counterattack Against Lady Zi", "Pilots willing to join the defence can sign up at Worlidge Hub in Exphiay and Hencke Orbital in Sorbago respectively.")]
    [InlineData("Lady Zi Declares 'Counter-Megacorporation' Assault", "The Fire Lotus Coalition megaships are seemingly recruiting independent pilots to join her cause.")]
    [InlineData("Core Dynamics Unveils Kestrel Fighter", "Core Dynamics has called for interested independent pilots to sign up at Polyakov Station.")]
    [InlineData("Trading Blocs Compete in Alliance Space", "Trade pilots looking to help Nakato Kaine's trade bloc can sign up at Fuca Dock in Apura.")]
    public void ALaunchTitleWithACallToActionIsRecognised(string title, string last) =>
        Assert.True(CommunityGoalLaunch.Recognises(Article(title, last)));

    [Theory]
    [InlineData("Faulcon DeLacy Multi-Faceted Initiatives Conclude", "Pilots are invited to support the next phase by registering at the megaship.")]
    [InlineData("Brewer Corp Jump Satellite Initiative Hailed a Success", "Interested pilots should head to the megaship Zero Squared.")]
    [InlineData("Brewer Corp Initiative Deemed a Triumph", "Interested pilots should head to the megaship Zero Squared.")]
    public void AClosingTitleIsNotALaunchWhateverItsLastLineSays(string title, string last) =>
        Assert.False(CommunityGoalLaunch.Recognises(Article(title, last)));

    [Theory]
    [InlineData("Lady Zi Breaks Silence", "The Princess has declined to comment further.")]
    [InlineData("Prison Ship Break Subdued in Redonesses", "Local security forces have restored order.")]
    public void AnArticleWithNoCallToActionIsNotALaunch(string title, string last) =>
        Assert.False(CommunityGoalLaunch.Recognises(Article(title, last)));

    [Fact]
    public void ACallToActionBeforeTheLastThreeParagraphsDoesNotCount()
    {
        var article = new GalNetArticle(
            "id",
            "Wreaken Calls for Support",
            ["Pilots should head to the megaship.", "One.", "Two.", "Three."],
            DateTimeOffset.UnixEpoch,
            "3310");

        Assert.False(CommunityGoalLaunch.Recognises(article));
    }
}
