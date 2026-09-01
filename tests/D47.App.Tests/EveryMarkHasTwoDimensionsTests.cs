using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// No mark in <see cref="Glyphs"/> is flat
/// (reported 2026-09-01 — <i>"Collapse all is wrong"</i>).
/// <para>
/// <b>The minus rendered as a dot, and nothing failed.</b> <c>Glyphs.Made</c> fits a mark to its
/// control with <c>Stretch.Uniform</c>, which scales by the smaller of the two ratios — so a
/// horizontal line, having no height at all, divides by zero and collapses. Measured at the time:
/// defining bounds 14 × 0, rendered bounds 0 × 0. What was left was the round cap of a zero-length
/// stroke.
/// </para>
/// <para>
/// <b>The fault is in the stretch and the fix was in the mark</b>, so this is what stops the next
/// one-dimensional glyph disappearing the same way: a plain minus, an underline, a horizontal rule
/// — every one of them is a shape somebody would reasonably write, and every one of them would
/// vanish silently.
/// </para>
/// </summary>
public class EveryMarkHasTwoDimensionsTests
{
    /// <summary>
    /// Every path constant in the file, including the nested groups, by name. Reflected rather
    /// than listed: a mark added next week has to be covered without anybody remembering.
    /// </summary>
    public static TheoryData<string, string> Marks()
    {
        var found = new TheoryData<string, string>();

        foreach (var type in new[] { typeof(Glyphs) }.Concat(typeof(Glyphs).GetNestedTypes()))
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(field => field is { IsLiteral: true, FieldType.Name: nameof(String) }))
            {
                if (field.GetRawConstantValue() is string data && data.Length > 0)
                {
                    found.Add($"{type.Name}.{field.Name}", data);
                }
            }
        }

        return found;
    }

    [AvaloniaTheory]
    [MemberData(nameof(Marks))]
    public void AMarkWithNoHeightOrNoWidthWouldCollapseToNothing(string name, string data)
    {
        var bounds = Geometry.Parse(data).Bounds;

        // Not "greater than zero" — a mark a fraction of a unit tall scales by thousands and
        // arrives as a smear. One whole unit is half the stroke, which is the least any visible
        // mark has.
        Assert.True(
            bounds.Width >= 1,
            $"{name} is {bounds.Width} units wide, so Stretch.Uniform will collapse it: {data}");

        Assert.True(
            bounds.Height >= 1,
            $"{name} is {bounds.Height} units tall, so Stretch.Uniform will collapse it: {data}");
    }

    /// <summary>
    /// <b>And the pair still matches.</b> The plus is stroked two units wide and the minus is a
    /// filled bar two units tall — different constructions, on purpose, because only one of them
    /// can be flattened away. They have to come out the same weight and the same length all the
    /// same, or the two halves of one control read as two controls.
    /// </summary>
    [AvaloniaFact]
    public void ThePlusAndTheMinusAreTheSameWeightAndLength()
    {
        var plus = Geometry.Parse(Glyphs.ExpandAll).Bounds;
        var minus = Geometry.Parse(Glyphs.CollapseAll).Bounds;

        Assert.Equal(plus.Width, minus.Width, 1);
        Assert.Equal(2, minus.Height, 1);
    }
}
