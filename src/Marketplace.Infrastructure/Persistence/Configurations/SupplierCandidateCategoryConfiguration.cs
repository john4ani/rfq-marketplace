using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SupplierCandidateCategoryConfiguration : IEntityTypeConfiguration<SupplierCandidateCategory>
{
    public void Configure(EntityTypeBuilder<SupplierCandidateCategory> builder)
    {
        builder.ToTable("SupplierCandidateCategories");
        builder.HasKey(category => new { category.CandidateId, category.CategoryId });
        builder.HasOne<Category>().WithMany().HasForeignKey(category => category.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
