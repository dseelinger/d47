using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Controls;
using D47.Core.Capabilities;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Filtering the picker, and the two faults behind
/// <a href="https://github.com/dseelinger/d47/issues/146">#146</a>.
/// <para>
/// <b>The substring one first.</b> Matching was <c>Contains</c>, and <c>"female".Contains("male")</c>
/// is true — so typing <em>male</em> in the voice picker listed every female voice, and there was no
/// way to type your way out of it: <em>female</em> worked and <em>male</em> could not.
/// </para>
/// <para>
/// <b>And the one underneath it.</b> A voice carries its gender as a field and the label merely
/// renders it, so searching the label for a word was the wrong question to be asking at all. The
/// answer to that is a facet, and it lives here beside the search box.
/// </para>
/// </summary>
public class TypingMaleDoesNotListEveryWomanTests
{
    /// <summary>Both spellings, because the two providers disagree and only on capitalisation.</summary>
    private const string Ava = "ava";
    private const string Emma = "emma";
    private const string George = "george";
    private const string Nobody = "nova";

    private static readonly Dictionary<string, string?> Genders = new(StringComparer.OrdinalIgnoreCase)
    {
        [Ava] = "Female",   // Edge writes it this way
        [Emma] = "female",  // ElevenLabs writes it this way
        [George] = "male",
        [Nobody] = null,    // and some providers tag nothing at all
    };

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        [Ava] = "Ava — Female, en-US",
        [Emma] = "Emma — female, en-GB",
        [George] = "George — male, en-GB",
        [Nobody] = "Nova (en-US)",
    };

    private static SettingFacet Gender() => new()
    {
        Label = "Gender",
        Options =
        [
            new SettingFacetOption("All", null),
            new SettingFacetOption("Female", id => Is(id, "female")),
            new SettingFacetOption("Male", id => Is(id, "male")),
            new SettingFacetOption("Unlabelled", id => Genders[id] is null),
        ],
    };

    private static bool Is(string id, string gender) =>
        string.Equals(Genders[id], gender, StringComparison.OrdinalIgnoreCase);

    private static PickerRequest Voices(bool faceted = true) => new()
    {
        Prompt = "Voice",
        Choices = [Ava, Emma, George, Nobody],
        Describe = id => Labels[id],
        AllowsFreeText = true,
        Facet = faceted ? Gender() : null,
    };

    /// <summary>
    /// A picker on screen with its template applied, which the filter box and the facet both need:
    /// the handlers that re-filter are wired by the template, so a window that was never shown
    /// answers every question with its opening list.
    /// </summary>
    private static PickerWindow Shown(PickerRequest? request = null)
    {
        var picker = PickerWindow.For(request ?? Voices());

        picker.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return picker;
    }

    private static PickerWindow Typing(string filter, PickerRequest? request = null)
    {
        var picker = Shown(request);

        picker.GetControl<TextBox>("FilterBox").Text = filter;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return picker;
    }

    private static void Choose(PickerWindow picker, int option)
    {
        picker.GetControl<ComboBox>("FacetBox").SelectedIndex = option;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static IReadOnlyList<string> Listed(PickerWindow picker) =>
        [.. picker.GetControl<ListBox>("Choices").ItemsSource!.Cast<PickerChoice>().Select(choice => choice.Value)];

    // ---- The reported bug ------------------------------------------------------------------

    /// <summary>
    /// <b>The report, in one line:</b> <i>"'male' includes female voices too."</i> Asserted with
    /// both spellings, since a fix that only handled the lower-case one would leave Edge broken.
    /// </summary>
    [AvaloniaFact]
    public void TypingMaleListsNoVoiceTaggedFemale()
    {
        var listed = Listed(Typing("male"));

        Assert.DoesNotContain(Ava, listed);
        Assert.DoesNotContain(Emma, listed);
        Assert.Contains(George, listed);
    }

    /// <summary>And the direction that always worked still does.</summary>
    [AvaloniaFact]
    public void TypingFemaleStillListsTheWomen()
    {
        var listed = Listed(Typing("female"));

        Assert.Contains(Ava, listed);
        Assert.Contains(Emma, listed);
        Assert.DoesNotContain(George, listed);
    }

    // ---- What the fix must not take away -----------------------------------------------------

    /// <summary>
    /// <b>Word starts rather than whole words, asserted in both directions.</b> Whole-word matching
    /// would fix <em>male</em> and break every partial search in the app, so the choice between them
    /// is not cosmetic.
    /// </summary>
    [Theory]
    [InlineData("eng", "Engineering", true)]
    [InlineData("kra", "Krait MkII", true)]
    [InlineData("male", "Emma — female, en-GB", false)]
    [InlineData("male", "FEMALE VOICE", false)]
    [InlineData("male", "George — male, en-GB", true)]
    [InlineData("us", "Ava — Female, en-US", true)]
    [InlineData("nde", "Understood", false)]
    public void APrefixOfAWordMatchesAndAMiddleOfOneDoesNot(string filter, string text, bool matches) =>
        Assert.Equal(matches, ChoiceMatch.Matches(text, filter));

    /// <summary>
    /// <b>A capital after a lower-case letter starts a word too</b>, which is what keeps the Edge
    /// catalogue searchable: most of it is named <c>en-US-AndrewMultilingualNeural</c>, and a rule
    /// without this would have silently stopped <em>Multilingual</em> finding anything.
    /// </summary>
    [Theory]
    [InlineData("multilingual", true)]
    [InlineData("neural", true)]
    [InlineData("andrew", true)]
    [InlineData("ndrew", false)]
    public void ACamelHumpIsAWordStart(string filter, bool matches) =>
        Assert.Equal(matches, ChoiceMatch.Matches("en-US-AndrewMultilingualNeural", filter));

    /// <summary>
    /// A run of capitals is one word, so the fix holds however a provider capitalises its labels —
    /// <c>FEMALE</c> must not match <em>male</em> at its M.
    /// </summary>
    [Fact]
    public void ARunOfCapitalsIsOneWord()
    {
        Assert.False(ChoiceMatch.Matches("FEMALE", "male"));
        Assert.True(ChoiceMatch.Matches("FEMALE", "fem"));
    }

    /// <summary>An empty filter is the picker's resting state and hides nothing.</summary>
    [AvaloniaFact]
    public void AnEmptyFilterListsEverything() =>
        Assert.Equal(4, Listed(Typing(string.Empty)).Count);

    /// <summary>Ids are still searchable, which is how a Commander who knows one finds it.</summary>
    [AvaloniaFact]
    public void TheStoredValueIsStillSearchable() =>
        Assert.Equal([George], Listed(Typing("george")));

    // ---- The facet --------------------------------------------------------------------------

    /// <summary>
    /// <b>Offered where the choices carry one and absent where they do not</b>, the way the play
    /// glyph is absent on a microphone picker rather than present and inert.
    /// </summary>
    [AvaloniaFact]
    public void TheFilterIsOfferedOnlyWhereTheChoicesCarryAGender()
    {
        Assert.True(Shown().GetControl<StackPanel>("FacetPanel").IsVisible);
        Assert.False(Shown(Voices(faceted: false)).GetControl<StackPanel>("FacetPanel").IsVisible);
    }

    /// <summary>
    /// It opens on the option that hides nothing. A list that arrives pre-narrowed looks like a list
    /// with things missing from it.
    /// </summary>
    [AvaloniaFact]
    public void ItOpensShowingEverything()
    {
        var picker = Shown();

        Assert.Equal(0, picker.GetControl<ComboBox>("FacetBox").SelectedIndex);
        Assert.Equal(4, Listed(picker).Count);
    }

    /// <summary>
    /// Choosing one narrows the list without anything being typed, which is the whole point: the
    /// Commander no longer has to guess a word that happens to appear in a label.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(1, new[] { Ava, Emma })]
    [InlineData(2, new[] { George })]
    [InlineData(3, new[] { Nobody })]
    public void ChoosingAGenderNarrowsTheListWithNothingTyped(int option, string[] expected)
    {
        var picker = Shown();

        Choose(picker, option);

        Assert.Equal(expected, Listed(picker));
    }

    /// <summary>
    /// <b>Untagged voices are never silently dropped</b>, which is the decision the issue asked to
    /// be taken rather than defaulted into. They are in <em>All</em>, they have an option of their
    /// own, and they are not swept in with the men — a filter that hid them would look like a
    /// shorter list rather than like a filter.
    /// </summary>
    [AvaloniaFact]
    public void AnUntaggedVoiceIsReachableAndIsNotCountedAsAMan()
    {
        var picker = Shown();

        Assert.Contains(Nobody, Listed(picker));

        Choose(picker, 2);
        Assert.DoesNotContain(Nobody, Listed(picker));

        Choose(picker, 3);
        Assert.Equal([Nobody], Listed(picker));
    }

    /// <summary>The facet and the search box narrow together rather than one replacing the other.</summary>
    [AvaloniaFact]
    public void TheFacetAndTheSearchBoxBothApply()
    {
        var picker = Typing("en-GB");

        Choose(picker, 1);

        Assert.Equal([Emma], Listed(picker));
    }

    /// <summary>
    /// A facet that has emptied the list says so, because clearing the search box will not bring
    /// anything back — the box is not what emptied it.
    /// </summary>
    [AvaloniaFact]
    public void AFacetThatHidesEverythingExplainsItself()
    {
        var picker = Shown(new PickerRequest
        {
            Prompt = "Voice",
            Choices = [George],
            Describe = id => Labels[id],
            Facet = Gender(),
        });

        Choose(picker, 1);

        var hint = picker.GetControl<TextBlock>("EmptyHint");

        Assert.True(hint.IsVisible);
        Assert.Contains("Female", hint.Text!, StringComparison.Ordinal);
        Assert.Contains("All", hint.Text!, StringComparison.Ordinal);
    }

    /// <summary>
    /// Narrowing to a facet that excludes the highlighted row moves the highlight, rather than
    /// leaving a selection nobody can see that Enter would take anyway.
    /// </summary>
    [AvaloniaFact]
    public void NarrowingMovesTheHighlightToSomethingVisible()
    {
        var picker = Shown(new PickerRequest
        {
            Prompt = "Voice",
            Choices = [Ava, Emma, George, Nobody],
            Describe = id => Labels[id],
            Current = George,
            Facet = Gender(),
        });

        var choices = picker.GetControl<ListBox>("Choices");

        Assert.Equal(George, ((PickerChoice)choices.SelectedItem!).Value);

        Choose(picker, 1);

        Assert.True(choices.SelectedIndex >= 0);
        Assert.Equal(Ava, ((PickerChoice)choices.SelectedItem!).Value);
    }
}
