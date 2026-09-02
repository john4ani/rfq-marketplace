using Marketplace.Domain.Seap;
using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SeapTenderConfiguration : IEntityTypeConfiguration<SeapTender>
{
    public void Configure(EntityTypeBuilder<SeapTender> builder)
    {
        builder.ToTable("SeapTenders");
        builder.HasKey(tender => tender.Id);
        builder.HasIndex(tender => tender.SeapExternalId).IsUnique();
        builder.HasIndex(tender => tender.NormalizedExternalId).IsUnique();
        builder.Property(tender => tender.NoticeId);
        builder.Property(tender => tender.SysNoticeTypeId);
        builder.Property(tender => tender.SeapExternalId).HasMaxLength(64).IsRequired();
        builder.Property(tender => tender.NormalizedExternalId).HasMaxLength(64).IsRequired();
        builder.Property(tender => tender.Title).HasMaxLength(500).IsRequired();
        builder.Property(tender => tender.Description).HasColumnType("text");
        builder.Property(tender => tender.CpvCodes).HasColumnType("text[]");
        builder.Property(tender => tender.CpvNames).HasColumnType("text[]");
        builder.Property(tender => tender.Currency).HasMaxLength(8);
        builder.Property(tender => tender.BuyerName).HasMaxLength(300).IsRequired();
        builder.Property(tender => tender.County).HasMaxLength(48);
        builder.HasIndex(tender => tender.County);
        builder.Property(tender => tender.ImportMode).HasConversion<string>().HasMaxLength(32).HasDefaultValue(SeapImportMode.Standard);
        builder.Property(tender => tender.DocumentSetHash).HasColumnType("text");
        builder.Property(tender => tender.AnalyzedDocumentSetHash).HasColumnType("text");
        builder.Property(tender => tender.DocumentDigestStatus).HasConversion<string>().HasMaxLength(12);
        builder.Property(tender => tender.AnalysisVersion);
        builder.Property(tender => tender.AttemptCount).HasDefaultValue(0);
        builder.Property(tender => tender.StartedAt);
        builder.Property(tender => tender.CompletedAt);
        builder.Property(tender => tender.LastErrorCode).HasMaxLength(64);
        builder.Property(tender => tender.LastErrorMessage).HasColumnType("text");
        // Feed hot paths: the discovery list filters on Status + Deadline and orders by PublicationDate;
        // the generated-offers history filters on Status alone (composite index leads with Status, so it
        // serves both queries).
        builder.HasIndex(tender => new { tender.Status, tender.Deadline });
        builder.HasIndex(tender => tender.PublicationDate).IsDescending();
        builder.HasOne(tender => tender.Category).WithMany().HasForeignKey(tender => tender.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(tender => tender.CategoryId);
        builder.Property(tender => tender.EstimatedValue).HasPrecision(18, 2);
        builder.Property(tender => tender.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(tender => tender.SummaryPoints).HasColumnType("text[]");
        builder.Property(tender => tender.AwardCriterion).HasColumnType("text");
        builder.Property(tender => tender.ParticipationGuarantee).HasColumnType("text");
        builder.Property(tender => tender.FundingSource).HasColumnType("text");
        builder.Property(tender => tender.ContractDuration).HasColumnType("text");
        builder.Property(tender => tender.QualificationRequirements).HasColumnType("text");
        builder.Property(tender => tender.KeyPersonnel).HasColumnType("text");
        builder.Property(tender => tender.Certificates).HasColumnType("text");
        builder.Property(tender => tender.SummaryGroundedInDocuments).HasDefaultValue(false);
        builder.HasMany(tender => tender.Attachments).WithOne().HasForeignKey(attachment => attachment.TenderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(tender => tender.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
