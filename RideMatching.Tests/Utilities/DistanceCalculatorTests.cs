
using RideMatching.Api.Services;
using Xunit;

namespace RideMatching.Tests.Utilities;
public class DistanceCalculatorTests
{
    [Fact]
    public void CalculateKm_SameCoordinates_ReturnsZero()
    {
        var distance = DistanceCalculator.CalculateKm(
            20.0000, 73.0000,
            20.0000, 73.0000);

        Assert.Equal(0, distance, precision: 6);
    }

    [Fact]
    public void CalculateKm_KnownCoordinates_ReturnsExpectedDistance()
    {
        // Approximately 111 metres for a latitude difference
        // of 0.001 degrees near latitude 20 degrees.
        var distance = DistanceCalculator.CalculateKm(
            20.0000, 73.0000,
            20.0010, 73.0000);

        Assert.InRange(distance, 0.10, 0.12);
    }

    [Fact]
    public void CalculateKm_IsSymmetric()
    {
        var forward = DistanceCalculator.CalculateKm(
            20.0000, 73.0000,
            20.0200, 73.0300);

        var reverse = DistanceCalculator.CalculateKm(
            20.0200, 73.0300,
            20.0000, 73.0000);

        Assert.Equal(forward, reverse, precision: 6);
    }

    [Fact]
    public void CalculateKm_DifferentCoordinates_ReturnsPositiveDistance()
    {
        var distance = DistanceCalculator.CalculateKm(
            20.0000, 73.0000,
            20.0100, 73.0100);

        Assert.True(distance > 0);
    }
}
