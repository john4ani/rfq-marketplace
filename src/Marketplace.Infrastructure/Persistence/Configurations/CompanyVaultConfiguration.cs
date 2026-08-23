using Marketplace.Domain.Profiles;
using Marketplace.Domain.Vault;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class CompanyVaultConfiguration : IEntityTypeConfiguration<CompanyVault>
{
    public void Configure(EntityTypeBuilder<CompanyVault> builder)
    {
        builder.ToTable("CompanyVaults");
        builder.HasKey(vault => vault.Id);
        builder.HasIndex(vault => vault.SupplierId).IsUnique();
        builder.HasOne<SupplierProfile>().WithOne().HasForeignKey<CompanyVault>(vault => vault.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(vault => vault.Documents).WithOne().HasForeignKey(document => document.VaultId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(vault => vault.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
