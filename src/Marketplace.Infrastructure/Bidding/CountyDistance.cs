using System.Globalization;
using System.Text;

namespace Marketplace.Infrastructure.Bidding;

/// <summary>
/// Approximate county-level distance for the SEAP radius filter. Each Romanian county is represented by
/// its residence city (municipiu reședință de județ) coordinates; the distance between two counties is
/// the great-circle distance between their seats. Honest approximation for MVP: a tender in the same
/// county always passes, and neighbouring counties fall inside most configured radii.
/// </summary>
public static class CountyDistance
{
    private const double EarthRadiusKm = 6371.0;

    // Canonical county names (with diacritics, matching CountyParser output) → seat coordinates.
    private static readonly (string County, double Latitude, double Longitude)[] Locations =
    [
        ("Alba", 46.07, 23.57),
        ("Arad", 46.18, 21.32),
        ("Argeș", 44.86, 24.87),
        ("Bacău", 46.57, 26.91),
        ("Bihor", 47.05, 21.93),
        ("Bistrița-Năsăud", 47.13, 24.50),
        ("Botoșani", 47.75, 26.67),
        ("Brașov", 45.65, 25.61),
        ("Brăila", 45.27, 27.96),
        ("București", 44.43, 26.10),
        ("Buzău", 45.15, 26.82),
        ("Caraș-Severin", 45.30, 21.89),
        ("Călărași", 44.20, 27.32),
        ("Cluj", 46.77, 23.59),
        ("Constanța", 44.17, 28.64),
        ("Covasna", 45.86, 25.79),
        ("Dâmbovița", 44.93, 25.46),
        ("Dolj", 44.33, 23.80),
        ("Galați", 45.42, 28.05),
        ("Giurgiu", 43.90, 25.97),
        ("Gorj", 45.03, 23.27),
        ("Harghita", 46.36, 25.80),
        ("Hunedoara", 45.87, 22.91),
        ("Ialomița", 44.56, 27.37),
        ("Iași", 47.16, 27.59),
        ("Ilfov", 44.56, 25.95),
        ("Maramureș", 47.66, 23.57),
        ("Mehedinți", 44.63, 22.66),
        ("Mureș", 46.55, 24.56),
        ("Neamț", 46.93, 26.38),
        ("Olt", 44.43, 24.37),
        ("Prahova", 44.94, 26.02),
        ("Satu Mare", 47.79, 22.89),
        ("Sălaj", 47.18, 23.06),
        ("Sibiu", 45.80, 24.15),
        ("Suceava", 47.65, 26.26),
        ("Teleorman", 43.98, 25.33),
        ("Timiș", 45.75, 21.23),
        ("Tulcea", 45.18, 28.81),
        ("Vaslui", 46.64, 27.73),
        ("Vâlcea", 45.10, 24.37),
        ("Vrancea", 45.70, 27.18)
    ];

    /// <summary>Great-circle distance in km between two counties, or null when either name is unknown.</summary>
    public static double? KilometersBetween(string countyA, string countyB)
    {
        if (TryGetLocation(countyA, out double latA, out double lonA) &&
            TryGetLocation(countyB, out double latB, out double lonB))
            return Haversine(latA, lonA, latB, lonB);
        return null;
    }

    private static bool TryGetLocation(string county, out double latitude, out double longitude)
    {
        string key = Normalize(county);
        foreach ((string name, double lat, double lon) in Locations)
        {
            if (Normalize(name) == key)
            {
                latitude = lat;
                longitude = lon;
                return true;
            }
        }
        latitude = 0;
        longitude = 0;
        return false;
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static string Normalize(string value)
    {
        string upper = value.ToUpperInvariant();
        return new(upper.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
    }
}
