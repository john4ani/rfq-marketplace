using Marketplace.Domain.Profiles;
using Marketplace.Domain.Qualification;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SupplierProfileConfiguration : IEntityTypeConfiguration<SupplierProfile>
{
    public void Configure(EntityTypeBuilder<SupplierProfile> builder)
    {
        builder.ToTable("SupplierProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.TradingName).HasMaxLength(200).IsRequired();
        builder.Property(profile => profile.CompanyRegistrationNumber).HasMaxLength(64).IsRequired();
        builder.Property(profile => profile.TaxIdentifier).HasMaxLength(32);
        builder.Property(profile => profile.ContactPerson).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.ContactEmail).HasMaxLength(256).IsRequired();
        builder.Property(profile => profile.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(profile => profile.Website).HasMaxLength(300);
        builder.Property(profile => profile.Address).HasMaxLength(300).IsRequired();
        builder.Property(profile => profile.City).HasMaxLength(120).IsRequired();
        builder.Property(profile => profile.County).HasMaxLength(80).IsRequired();
        builder.Property(profile => profile.Country).HasMaxLength(80).HasDefaultValue("România").IsRequired();
        builder.Property(profile => profile.Latitude).HasPrecision(9, 6);
        builder.Property(profile => profile.Longitude).HasPrecision(9, 6);
        builder.Property(profile => profile.MarketRadiusKm).HasDefaultValue(30);
        builder.Property(profile => profile.Description).HasMaxLength(2000);
        builder.Property(profile => profile.SeapCpvPrefixes).HasColumnType("text[]");
        builder.Property(profile => profile.SeapCounties).HasColumnType("text[]");
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<SupplierProfile>(profile => profile.Id).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(profile => profile.LegalProfile).WithOne().HasForeignKey<CompanyLegalProfile>(item => item.SupplierId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(profile => profile.Experience).WithOne().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(profile => profile.Certificates).WithOne().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(profile => profile.Personnel).WithOne().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(profile => profile.LegalProfile).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Experience).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Certificates).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Personnel).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
