using Marketplace.Domain.Profiles;
using Marketplace.Domain.Qualification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class CompanyLegalProfileConfiguration : IEntityTypeConfiguration<CompanyLegalProfile>
{
    public void Configure(EntityTypeBuilder<CompanyLegalProfile> builder)
    {
        builder.ToTable("CompanyLegalProfiles");
        builder.HasKey(profile => profile.SupplierId);
        builder.Property(profile => profile.LegalRepresentativeName).HasMaxLength(200).IsRequired();
        builder.Property(profile => profile.LegalRepresentativeRole).HasMaxLength(120);
        builder.Property(profile => profile.LegalForm).HasMaxLength(80);
        builder.OwnsMany(profile => profile.BankAccounts, account =>
        {
            account.ToTable("CompanyLegalProfiles_BankAccounts");
            account.Property(item => item.BankName).HasMaxLength(160).IsRequired();
            account.Property(item => item.Iban).HasMaxLength(34).IsRequired();
            account.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        });
        builder.Navigation(profile => profile.BankAccounts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
