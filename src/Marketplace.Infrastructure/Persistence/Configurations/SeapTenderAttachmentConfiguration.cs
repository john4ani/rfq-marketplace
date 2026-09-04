using Marketplace.Domain.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SeapTenderAttachmentConfiguration : IEntityTypeConfiguration<SeapTenderAttachment>
{
    public void Configure(EntityTypeBuilder<SeapTenderAttachment> builder)
    {
        builder.ToTable("SeapTenderAttachments");
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.FileName).HasMaxLength(255).IsRequired();
        builder.Property(attachment => attachment.SourceUrl).HasMaxLength(1024);
        builder.Property(attachment => attachment.StorageReference).HasMaxLength(1024);
        builder.Property(attachment => attachment.SourceDocumentId).HasMaxLength(64);
        builder.Property(attachment => attachment.SourceSection).HasMaxLength(96);
        builder.Property(attachment => attachment.ContentSha256).HasMaxLength(64);
        builder.Property(attachment => attachment.DownloadErrorCode).HasMaxLength(64);
        // Direct Hit dossier state (Stage 1 DocumentImport): functional group + durable-download state, both
        // persisted as strings and defaulting to their neutral enum value so pre-dossier rows read back cleanly.
        builder.Property(attachment => attachment.DocumentGroup).HasConversion<string>().HasMaxLength(24)
            .HasDefaultValue(SeapDocumentGroup.None);
        builder.Property(attachment => attachment.DownloadStatus).HasConversion<string>().HasMaxLength(24)
            .HasDefaultValue(SeapDocumentDownloadStatus.None);
        builder.HasIndex(attachment => attachment.TenderId);
        // Idempotent durable document lists: a published document (stable upstream id) can exist at most once
        // per tender even when two resolve/fetch executions race (the service also de-duplicates in memory;
        // this index is the DB-level guarantee). Multiple NULL SourceDocumentId rows (legacy browsing copies
        // that never resolved a code) are allowed by both PostgreSQL and SQL Server.
        builder.HasIndex(attachment => new { attachment.TenderId, attachment.SourceDocumentId })
            .IsUnique();
        builder.HasOne<SeapTender>().WithMany().HasForeignKey(attachment => attachment.TenderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
