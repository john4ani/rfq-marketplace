using Marketplace.Domain.Rfqs;
using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class NeedCategoryConfiguration : IEntityTypeConfiguration<NeedCategory>
{
    public void Configure(EntityTypeBuilder<NeedCategory> builder)
    {
        builder.ToTable("ProcurementNeedCategories");
        builder.HasKey(needCategory => needCategory.Id);
        builder.HasIndex(needCategory => new { needCategory.ProcurementNeedId, needCategory.CategoryId }).IsUnique();
        builder.HasIndex(needCategory => new { needCategory.ProcurementNeedId, needCategory.SortOrder }).IsUnique();
        builder.HasOne<Category>().WithMany().HasForeignKey(needCategory => needCategory.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
