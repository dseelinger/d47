using System.Globalization;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// Every registered number row, held to two rules it is easy to break one row at a time.
/// <para>
/// This exists because fixing the rows that were wrong is not the same as making them right.
/// The speaking rate row could not hold the 1.2 its own help text offered, and then — once it
/// could — read it back in a different format from the one it wrote. Both were invisible: a
/// rejected value in the panel snaps back, which looks like a control that did not take the
/// click, and a value that reads back differently is still the right value. Neither would ever
/// have been noticed by using d47.
/// </para>
/// <para>
/// So the assertion is over the registry rather than over a list of keys. A row added in a
/// later phase is covered on the day it is added.
/// </para>
/// </summary>
public class NumberRowGateTests
{
    public static TheoryData<string> NumberRows
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var key in Keys())
            {
                data.Add(key);
            }

            return data;
        }
    }

    /// <summary>
    /// A row reads back exactly what it writes.
    /// <para>
    /// When it does not, <see cref="SettingsService.Apply"/>'s unchanged check can never fire:
    /// the written value and the read one never compare equal, so every apply persists the file
    /// again and announces a change that did not happen. The value stays correct throughout,
    /// which is why this needs a test rather than a user.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(NumberRows))]
    public void ANumberRowReadsBackWhatItWrites(string key)
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        var current = settings.Read(key);
        Assert.NotNull(current);

        var again = settings.Apply(key, current, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Unchanged, again.Status);
    }

    /// <summary>
    /// A row that steps in fractions can hold one.
    /// <para>
    /// The step is the row's own claim about its granularity, and the panel builds a control
    /// that offers exactly it — so a row declaring half-seconds while the store keeps whole ones
    /// is a stepper whose every other click is silently refused.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(NumberRows))]
    public void ARowThatStepsInFractionsCanHoldOne(string key)
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        var row = settings.Find(key)!;

        if (row.Step >= 1)
        {
            // A row that counts things is not required to hold a fraction of one, and rounding
            // 3.7 jumps to 4 is the correct answer rather than a gap.
            Assert.Equal("0", row.NumberFormat);
            return;
        }

        var from = double.Parse(settings.Read(key)!, NumberStyles.Float, CultureInfo.InvariantCulture);

        // Up, unless the row is already sitting at its ceiling — a level defaulting to 1 out of
        // 1 is a row that can move, and only one of the two directions says so.
        var target = from + row.Step <= (row.Maximum ?? double.PositiveInfinity)
            ? from + row.Step
            : from - row.Step;

        Assert.True(
            target >= (row.Minimum ?? double.NegativeInfinity),
            $"{key}: a range of {row.Minimum} to {row.Maximum} is narrower than one step of {row.Step}");

        var stepped = target.ToString(row.NumberFormat, CultureInfo.InvariantCulture);

        var applied = settings.Apply(key, stepped, SettingsCaller.Panel);

        Assert.True(applied.Ok, $"{key}: one step from {from} to {stepped} was {applied.Status} — {applied.Message}");
        Assert.Equal(stepped, settings.Read(key));
    }

    /// <summary>
    /// And the numbers it writes are the ones another machine would read. The store parses and
    /// formats invariantly, so a row reading in the machine's own culture turns 20.5 into 205
    /// wherever the decimal separator is a comma.
    /// </summary>
    [Theory]
    [MemberData(nameof(NumberRows))]
    public void ANumberRowIsCultureInvariant(string key)
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        var value = settings.Read(key);

        Assert.NotNull(value);
        Assert.DoesNotContain(',', value);
    }

    private static IEnumerable<string> Keys()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        return
        [
            .. from section in settings.Sections
               from row in section.Rows
               where row.Kind == SettingKind.Number && row.Applies(settings.Current)
               select row.Key,
        ];
    }
}
