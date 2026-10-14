namespace RideMatching.Api.Services;

public static class DistanceCalculator
{
    public static double CalculateKm(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a =
            Math.Pow(Math.Sin(dLat / 2), 2) +
            Math.Cos(ToRadians(lat1)) *
            Math.Cos(ToRadians(lat2)) *
            Math.Pow(Math.Sin(dLon / 2), 2);

        a = Math.Clamp(a, 0.0, 1.0);

        return earthRadiusKm * 2 *
            Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) =>
        degrees * Math.PI / 180.0;
}
