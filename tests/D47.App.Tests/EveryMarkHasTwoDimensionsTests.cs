using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>No mark in <see cref="Glyphs"/> is flat.</summary>
public class EveryMarkHasTwoDimensionsTests
{
    /// <summary>Every path constant in the file, including the nested groups, by name.</summary>
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

        // Not "greater than zero" — a mark a fraction of a unit tall scales by thousands and arrives as a
        // smear.
        Assert.True(
            bounds.Width >= 1,
            $"{name} is {bounds.Width} units wide, so Stretch.Uniform will collapse it: {data}");

        Assert.True(
            bounds.Height >= 1,
            $"{name} is {bounds.Height} units tall, so Stretch.Uniform will collapse it: {data}");
    }

    /// <summary>And the pair still matches.</summary>
    [AvaloniaFact]
    public void ThePlusAndTheMinusAreTheSameWeightAndLength()
    {
        var plus = Geometry.Parse(Glyphs.ExpandAll).Bounds;
        var minus = Geometry.Parse(Glyphs.CollapseAll).Bounds;

        Assert.Equal(plus.Width, minus.Width, 1);
        Assert.Equal(2, minus.Height, 1);
    }
}
