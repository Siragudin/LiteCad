using LiteCad.Tools;
using Xunit;

namespace LiteCad.Tests;

public class SectorGeometryTests
{
    [Theory]
    [InlineData(10, 1)]
    [InlineData(20, 2)]
    [InlineData(31, 4)]
    [InlineData(90, 9)]
    [InlineData(180, 18)]
    [InlineData(360, 36)]
    public void ComputeSegmentCount_MatchesSpec(int angleDegrees, int expectedCount)
    {
        Assert.Equal(expectedCount, SectorGeometry.ComputeSegmentCount(angleDegrees));
    }

    [Fact]
    public void ComputeSegmentSweepAngles_31Degrees_UsesTenDegreeStepsPlusRemainder()
    {
        var sweeps = SectorGeometry.ComputeSegmentSweepAngles(31);

        Assert.Equal(4, sweeps.Length);
        Assert.Equal(10, sweeps[0], 6);
        Assert.Equal(10, sweeps[1], 6);
        Assert.Equal(10, sweeps[2], 6);
        Assert.Equal(1, sweeps[3], 6);
    }

    [Fact]
    public void ComputeSegmentSweepAngles_NegativeAngle_PreservesSign()
    {
        var sweeps = SectorGeometry.ComputeSegmentSweepAngles(-31);

        Assert.Equal(-10, sweeps[0], 6);
        Assert.Equal(-1, sweeps[^1], 6);
    }

    [Theory]
    [InlineData(360, true)]
    [InlineData(-360, true)]
    [InlineData(359.999, false)]
    [InlineData(90, false)]
    public void IsFullCircle_DetectsCompleteRevolution(double angle, bool expected)
    {
        Assert.Equal(expected, SectorGeometry.IsFullCircle(angle));
    }
}
