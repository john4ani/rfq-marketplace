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

    [Fact]
    public void BackfillNoticeSourceRecordsTheSeapIdsForPreDocumentImports()
    {
        SeapTender tender = Tender("4305857 - Municipiul Cluj-Napoca", "Cluj");

        tender.BackfillNoticeSource(100159118, 2, DateTimeOffset.UtcNow);
        Assert.Equal(100159118, tender.NoticeId);
        Assert.Equal(2, tender.SysNoticeTypeId);

        // Idempotent — never overwrites an already-recorded id.
        tender.BackfillNoticeSource(999, 17, DateTimeOffset.UtcNow);
        Assert.Equal(100159118, tender.NoticeId);
        Assert.Equal(2, tender.SysNoticeTypeId);
    }

    [Fact]
    public void ApplyAiExtractionStoresDetailFieldsAndSummaryBullets()
    {
        SeapTender tender = Tender("4305857 - Municipiul Cluj-Napoca", "Cluj");

        tender.ApplyAiExtraction(new SeapAiExtraction(
            "Rezumat achiziție.", "Prețul cel mai scăzut", "120.000 RON", "Buget local", "12 luni",
            "Minim 2 contracte similare în ultimii 3 ani", "Manager de proiect, Inginer RTE",
            ["Garanție: 120.000 RON", "Experiență: minim 2 contracte"]), DateTimeOffset.UtcNow);

        Assert.Equal("Rezumat achiziție.", tender.Description);
        Assert.Equal("Prețul cel mai scăzut", tender.AwardCriterion);
        Assert.Equal("120.000 RON", tender.ParticipationGuarantee);
        Assert.Equal("Buget local", tender.FundingSource);
        Assert.Equal("12 luni", tender.ContractDuration);
        Assert.Equal("Minim 2 contracte similare în ultimii 3 ani", tender.QualificationRequirements);
        Assert.Equal("Manager de proiect, Inginer RTE", tender.KeyPersonnel);
        Assert.Equal(["Garanție: 120.000 RON", "Experiență: minim 2 contracte"], tender.SummaryPoints);
    }

    [Fact]
    public void ApplyAiExtractionFillsDescriptionOnlyWhenTheNoticeHasNoFullText()
    {
        // The search payload never carries a description → the AI 2-3 sentence summary becomes the description.
        SeapTender withoutText = Tender("4305857 - Municipiul Cluj-Napoca", "Cluj");
        withoutText.ApplyAiExtraction(new SeapAiExtraction(
            "Rezumat AI.", "Prețul cel mai scăzut", null, null, null, null, null, []), DateTimeOffset.UtcNow);
        Assert.Equal("Rezumat AI.", withoutText.Description);

        // A parser-delivered full text always wins over the AI summary.
        SeapTender withText = SeapTender.Import("CN-X", "Anunț", "Textul integral al anunțului", ["45233000-1"],
            [], 1000m, "RON", "Autoritate", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow);
        withText.ApplyAiExtraction(new SeapAiExtraction(
            "Rezumat AI.", "Prețul cel mai scăzut", null, null, null, null, null, []), DateTimeOffset.UtcNow);
        Assert.Equal("Textul integral al anunțului", withText.Description);
    }

    [Fact]
    public void ApplyAiExtractionNormalizesBulletsAndTouchesUpdatedAt()
    {
        SeapTender tender = Tender("4305857 - Municipiul Cluj-Napoca", "Cluj");
        DateTimeOffset before = tender.UpdatedAt;

        tender.ApplyAiExtraction(new SeapAiExtraction(
            null, "Prețul cel mai scăzut", null, null, null, null, null,
            ["  Primul punct  ", "", "  ", "Al doilea punct", "Al treilea", "Al patrulea", "Al cincilea", "Al șaselea"]),
            before.AddMinutes(5));

        // Blank bullets are dropped; the list is capped at 5 (the „Sinteză AI" tab contract).
        Assert.Equal(5, tender.SummaryPoints.Count);
        Assert.Equal("Primul punct", tender.SummaryPoints.First());
        Assert.Equal(before.AddMinutes(5), tender.UpdatedAt);
    }
}
