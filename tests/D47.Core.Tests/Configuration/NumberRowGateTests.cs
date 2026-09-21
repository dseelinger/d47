using System.Globalization;
using System.Text.RegularExpressions;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Every registered number row, held to rules it is easy to break one row at a time.</summary>
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

    [Theory]
    [MemberData(nameof(NumberRows))]
    public void ARowThatStepsInFractionsCanHoldOne(string key)
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        var row = settings.Find(key)!;

        if (row.Step >= 1)
        {
            // A row that counts things is not required to hold a fraction of one, and rounding 3.7 jumps to 4
            // is the correct answer rather than a gap.
            Assert.Equal("0", row.NumberFormat);
            return;
        }

        var from = double.Parse(settings.Read(key)!, NumberStyles.Float, CultureInfo.InvariantCulture);

        // Up, unless the row is already sitting at its ceiling — a level defaulting to 1 out of 1 is a row
        // that can move, and only one of the two directions says so.
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

    /// <summary>And the numbers it writes are the ones another machine would read.</summary>
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

    /// <summary>The unit is drawn inside the control from <see cref="SettingRow.Unit"/>, so a label naming it says it twice.</summary>
    [Fact]
    public void ANumberRowsLabelLeavesItsUnitToTheControl()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        var unitWord = new Regex(
            @"\b(milliseconds|seconds|minutes|decibels|percent|metres|tonnes|days)\b",
            RegexOptions.IgnoreCase);

        var named = from section in settings.Sections
                    from row in section.Rows
                    where row.Kind == SettingKind.Number && unitWord.IsMatch(row.Label)
                    select $"{row.Key}: {row.Label}";

        Assert.Empty(named);
    }

    [Fact]
    public void CaptureBeforeTheKeyIsCountedInMilliseconds()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        var row = settings.Find(D47.Core.Capabilities.Builtin.ListeningCapability.PreRollKey)!;

        Assert.Equal("Capture before the key", row.Label);
        Assert.Equal("ms", row.Unit);
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
