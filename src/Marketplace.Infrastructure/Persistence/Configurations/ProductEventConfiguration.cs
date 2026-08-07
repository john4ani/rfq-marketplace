using Marketplace.Domain.Operations;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class ProductEventConfiguration : IEntityTypeConfiguration<ProductEvent>
{
    public void Configure(EntityTypeBuilder<ProductEvent> builder)
    {
        builder.ToTable("ProductEvents");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(100).IsRequired();
        builder.Property(item => item.SubjectType).HasMaxLength(60).IsRequired();
        builder.Property(item => item.Metadata).HasMaxLength(2000);
        builder.HasIndex(item => new { item.Name, item.OccurredAt });
        builder.HasIndex(item => new { item.SubjectType, item.SubjectId, item.OccurredAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.SetNull);
    }
}
