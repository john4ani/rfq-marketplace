using Marketplace.Domain.Qualification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class CompanyExperienceConfiguration : IEntityTypeConfiguration<CompanyExperience>
{
    public void Configure(EntityTypeBuilder<CompanyExperience> builder)
    {
        builder.ToTable("CompanyExperiences");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ClientName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ContractTitle).HasMaxLength(300).IsRequired();
        builder.Property(item => item.ContractNumberDate).HasMaxLength(120);
        builder.Property(item => item.ValueAmount).HasPrecision(18, 2);
        builder.Property(item => item.ValueCurrency).HasMaxLength(3);
        builder.Property(item => item.CpvCode).HasMaxLength(32);
        builder.Property(item => item.DocumentRefUrl).HasMaxLength(1024);
        builder.HasIndex(item => new { item.SupplierId, item.CompletionDate });
    }
}
