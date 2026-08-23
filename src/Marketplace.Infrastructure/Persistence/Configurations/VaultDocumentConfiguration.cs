using Marketplace.Domain.Vault;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class VaultDocumentConfiguration : IEntityTypeConfiguration<VaultDocument>
{
    public void Configure(EntityTypeBuilder<VaultDocument> builder)
    {
        builder.ToTable("VaultDocuments");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.FileName).HasMaxLength(255).IsRequired();
        builder.Property(document => document.S3Url).HasMaxLength(1024).IsRequired();
        builder.Property(document => document.DocumentType).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(document => new { document.VaultId, document.FileName });
        builder.HasMany(document => document.Chunks).WithOne().HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(document => document.Chunks).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
