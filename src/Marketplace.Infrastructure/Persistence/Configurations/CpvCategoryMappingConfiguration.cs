using Marketplace.Domain.Seap;
using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class CpvCategoryMappingConfiguration : IEntityTypeConfiguration<CpvCategoryMapping>
{
    public void Configure(EntityTypeBuilder<CpvCategoryMapping> builder)
    {
        builder.ToTable("CpvCategoryMappings");
        builder.HasKey(mapping => mapping.CpvCode);
        builder.Property(mapping => mapping.CpvCode).HasMaxLength(16).IsRequired();
        builder.HasOne(mapping => mapping.Category).WithMany().HasForeignKey(mapping => mapping.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(mapping => mapping.CategoryId);
    }
}