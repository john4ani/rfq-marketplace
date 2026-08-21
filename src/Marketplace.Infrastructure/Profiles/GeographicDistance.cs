namespace Marketplace.Infrastructure.Profiles;

public static class GeographicDistance
{
    public static decimal Kilometers(decimal latitudeA, decimal longitudeA, decimal latitudeB, decimal longitudeB)
    {
        const double EarthRadiusKm = 6371d;
        static double Radians(decimal value) => (double)value * Math.PI / 180d;
        double dLatitude = Radians(latitudeB - latitudeA);
        double dLongitude = Radians(longitudeB - longitudeA);
        double a = Math.Sin(dLatitude / 2) * Math.Sin(dLatitude / 2)
            + Math.Cos(Radians(latitudeA)) * Math.Cos(Radians(latitudeB)) * Math.Sin(dLongitude / 2) * Math.Sin(dLongitude / 2);
        return (decimal)(EarthRadiusKm * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a)));
    }
}
