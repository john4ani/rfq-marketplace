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
        builder.Property(attachment => attachment.S3Url).HasMaxLength(1024).IsRequired();
        builder.Property(attachment => attachment.SourceDocumentId).HasMaxLength(64);
        builder.Property(attachment => attachment.ContentSha256).HasMaxLength(64);
        // Direct Hit dossier state (Stage 1 DocumentImport): functional group + durable-download state, both
        // persisted as strings and defaulting to their neutral enum value so pre-dossier rows read back cleanly.
        builder.Property(attachment => attachment.DocumentGroup).HasConversion<string>().HasMaxLength(24)
            .HasDefaultValue(SeapDocumentGroup.None);
        builder.Property(attachment => attachment.DownloadStatus).HasConversion<string>().HasMaxLength(24)
            .HasDefaultValue(SeapDocumentDownloadStatus.None);
        builder.HasIndex(attachment => attachment.TenderId);
        builder.HasOne<SeapTender>().WithMany().HasForeignKey(attachment => attachment.TenderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
