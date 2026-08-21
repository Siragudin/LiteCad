using LiteCad.Services;
using LiteCad.UI;
using Xunit;

namespace LiteCad.Tests;

public class UnitDisplayFormatterTests
{
    [Theory]
    [InlineData(100, "100")]
    [InlineData(100.4, "100")]
    [InlineData(100.5, "101")]
    [InlineData(1250.9, "1251")]
    public void FormatLinear_Millimeters_RoundsToWholeNumbers(double internalValue, string expected)
    {
        Assert.Equal(expected, UnitDisplayFormatter.FormatLinear(internalValue, LinearDisplayUnit.Millimeters));
    }

    [Theory]
    [InlineData(1000, "1")]
    [InlineData(1200, "1.2")]
    [InlineData(1250, "1.25")]
    [InlineData(1255, "1.26")]
    public void FormatLinear_Meters_TrimsTrailingZeros(double internalValue, string expected)
    {
        Assert.Equal(expected, UnitDisplayFormatter.FormatLinear(internalValue, LinearDisplayUnit.Meters));
    }

    [Theory]
    [InlineData(1_000_000, "1 m²")]
    [InlineData(1_200_000, "1.2 m²")]
    [InlineData(1_250_000, "1.25 m²")]
    public void FormatArea_SquareMeters_TrimsTrailingZeros(double internalArea, string expected)
    {
        Assert.Equal(expected, UnitDisplayFormatter.FormatArea(internalArea));
    }

    [Fact]
    public void TryParse_Millimeters_ReturnsInternalValue()
    {
        Assert.True(LinearInputParser.TryParse("1250", LinearDisplayUnit.Millimeters, false, false, out var value));
        Assert.Equal(1250, value, 3);
    }

    [Fact]
    public void TryParse_Meters_ConvertsToMillimeters()
    {
        Assert.True(LinearInputParser.TryParse("1.25", LinearDisplayUnit.Meters, false, false, out var value));
        Assert.Equal(1250, value, 3);
    }

    [Fact]
    public void TryParseSigned_AllowsNegativeValues()
    {
        Assert.True(LinearInputParser.TryParseSigned("-1.5", LinearDisplayUnit.Meters, out var value));
        Assert.Equal(-1500, value, 3);
    }

    [Fact]
    public void TryParseSigned_AllowsEmptyInput()
    {
        Assert.True(LinearInputParser.TryParseSigned(string.Empty, LinearDisplayUnit.Millimeters, out var value));
        Assert.Equal(0, value, 3);
    }

    [Fact]
    public void TryParsePositiveDistance_RejectsNonPositiveValues()
    {
        Assert.False(LinearInputParser.TryParsePositiveDistance("0", LinearDisplayUnit.Millimeters, out _));
        Assert.False(LinearInputParser.TryParsePositiveDistance("-10", LinearDisplayUnit.Millimeters, out _));
    }

    [Fact]
    public void TryParse_RejectsInvalidInput()
    {
        Assert.False(LinearInputParser.TryParse("abc", LinearDisplayUnit.Millimeters, false, false, out _));
    }
}
