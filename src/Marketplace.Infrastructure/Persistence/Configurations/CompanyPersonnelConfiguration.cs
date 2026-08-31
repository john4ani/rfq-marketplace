using Marketplace.Domain.Qualification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class CompanyPersonnelConfiguration : IEntityTypeConfiguration<CompanyPersonnel>
{
    public void Configure(EntityTypeBuilder<CompanyPersonnel> builder)
    {
        builder.ToTable("CompanyPersonnel");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.FullName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Role).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ProfessionalCertificates).HasColumnType("text[]");
        builder.Property(item => item.CvUrl).HasMaxLength(1024);
    }
}
