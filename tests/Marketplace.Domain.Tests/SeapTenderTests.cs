using Marketplace.Domain.Seap;

namespace Marketplace.Domain.Tests;

public sealed class SeapTenderTests
{
    private static SeapTender Tender(string buyerName, string? county) =>
        SeapTender.Import("CN-BF-1", "Anunț", null, ["45233000-1"], [], 1000m, "RON", buyerName, county,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow);

    [Fact]
    public void BackfillCountyAssignsDeterministicCountyForTendersImportedWithoutOne()
    {
        SeapTender tender = Tender("4305857 - Municipiul Cluj-Napoca", null);

        tender.BackfillCounty("Cluj", DateTimeOffset.UtcNow);

        Assert.Equal("Cluj", tender.County);
    }

    [Fact]
    public void BackfillCountyIsIdempotentAndNeverOverwritesAnExistingCounty()
    {
        SeapTender tender = Tender("4305857 - Municipiul Cluj-Napoca", "Cluj");

        tender.BackfillCounty("Vâlcea", DateTimeOffset.UtcNow);

        Assert.Equal("Cluj", tender.County);
    }

    [Fact]
    public void BackfillCountyIgnoresUnrecognizableValues()
    {
        SeapTender tender = Tender("4374873 - SPITALUL DE URGENTA PETROSANI", null);

        tender.BackfillCounty(null, DateTimeOffset.UtcNow);
        tender.BackfillCounty("   ", DateTimeOffset.UtcNow);

        Assert.Null(tender.County);
    }
}
