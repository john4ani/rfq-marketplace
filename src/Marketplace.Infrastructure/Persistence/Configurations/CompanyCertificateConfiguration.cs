using Marketplace.Domain.Qualification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class CompanyCertificateConfiguration : IEntityTypeConfiguration<CompanyCertificate>
{
    public void Configure(EntityTypeBuilder<CompanyCertificate> builder)
    {
        builder.ToTable("CompanyCertificates");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CertificateType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.SerialNumber).HasMaxLength(120);
        builder.Property(item => item.Issuer).HasMaxLength(200);
        builder.HasIndex(item => new { item.SupplierId, item.ValidUntil });
    }
}
