using Marketplace.Domain.Profiles;
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
        builder.Property(profile => profile.Latitude).HasPrecision(9, 6);
        builder.Property(profile => profile.Longitude).HasPrecision(9, 6);
        builder.Property(profile => profile.Description).HasMaxLength(2000);
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<SupplierProfile>(profile => profile.Id).OnDelete(DeleteBehavior.Cascade);
    }
}
