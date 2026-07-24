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
        builder.Property(category => category.Name).HasMaxLength(120).IsRequired();
        builder.Property(category => category.Slug).HasMaxLength(120).IsRequired();
        builder.HasIndex(category => category.Slug).IsUnique();
        builder.HasOne<Category>().WithMany().HasForeignKey(category => category.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(
            Category.Create(RootId, null, "Materiale de construcții", "materiale-constructii", 0),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000002"), RootId, "Izolații", "izolatii", 10),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000003"), RootId, "Ciment și mortare", "ciment-mortare", 20),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000004"), RootId, "Zidărie", "zidarie", 30),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000005"), RootId, "Acoperișuri", "acoperisuri", 40),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000006"), RootId, "Lemn și plăci", "lemn-placi", 50),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000007"), RootId, "Gips-carton", "gips-carton", 60),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000008"), RootId, "Finisaje", "finisaje", 70),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000009"), RootId, "Instalații sanitare", "instalatii-sanitare", 80),
            Category.Create(Guid.Parse("10000000-0000-0000-0000-000000000010"), RootId, "Instalații electrice", "instalatii-electrice", 90));
    }
}
