using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public static readonly Guid RootId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(category => category.Id);
        builder.HasOne<Vertical>().WithMany().HasForeignKey(category => category.VerticalId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(category => category.Name).HasMaxLength(120).IsRequired();
        builder.Property(category => category.Slug).HasMaxLength(120).IsRequired();
        builder.HasIndex(category => category.Slug).IsUnique();
        builder.HasOne<Category>().WithMany().HasForeignKey(category => category.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(
            Category.Create(RootId, VerticalConfiguration.ConstructionId, null, "Construcții", "constructii", 0),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000002"), VerticalConfiguration.ConstructionId, RootId, "Izolații", "izolatii", 10),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000003"), VerticalConfiguration.ConstructionId, RootId, "Ciment și mortare", "ciment-mortare", 20),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000004"), VerticalConfiguration.ConstructionId, RootId, "Zidărie", "zidarie", 30),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000005"), VerticalConfiguration.ConstructionId, RootId, "Acoperișuri", "acoperisuri", 40),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000006"), VerticalConfiguration.ConstructionId, RootId, "Lemn și plăci", "lemn-placi", 50),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000007"), VerticalConfiguration.ConstructionId, RootId, "Gips-carton", "gips-carton", 60),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000008"), VerticalConfiguration.ConstructionId, RootId, "Finisaje", "finisaje", 70),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000009"), VerticalConfiguration.ConstructionId, RootId, "Instalații sanitare", "instalatii-sanitare", 80),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000010"), VerticalConfiguration.ConstructionId, RootId, "Instalații electrice", "instalatii-electrice", 90));
    }
}
