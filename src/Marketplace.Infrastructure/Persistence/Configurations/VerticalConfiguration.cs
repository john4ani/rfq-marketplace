using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class VerticalConfiguration : IEntityTypeConfiguration<Vertical>
{
    public static readonly Guid ConstructionId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    public void Configure(EntityTypeBuilder<Vertical> builder)
    {
        builder.ToTable("Verticals");
        builder.HasKey(vertical => vertical.Id);
        builder.Property(vertical => vertical.Name).HasMaxLength(120).IsRequired();
        builder.Property(vertical => vertical.Slug).HasMaxLength(120).IsRequired();
        builder.HasIndex(vertical => vertical.Slug).IsUnique();
        builder.HasData(Vertical.Create(ConstructionId, "Construcții", "constructii"));
    }
}
