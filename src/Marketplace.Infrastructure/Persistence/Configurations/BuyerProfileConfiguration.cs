using Marketplace.Domain.Profiles;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class BuyerProfileConfiguration : IEntityTypeConfiguration<BuyerProfile>
{
    public void Configure(EntityTypeBuilder<BuyerProfile> builder)
    {
        builder.ToTable("BuyerProfiles", table => table.HasCheckConstraint("CK_BuyerProfiles_MarketRadiusKm", "\"MarketRadiusKm\" BETWEEN 1 AND 1000"));
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.CompanyName).HasMaxLength(200);
        builder.Property(profile => profile.TaxIdentifier).HasMaxLength(32);
        builder.Property(profile => profile.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(profile => profile.DefaultAddress).HasMaxLength(300);
        builder.Property(profile => profile.DefaultCity).HasMaxLength(120).IsRequired();
        builder.Property(profile => profile.DefaultCounty).HasMaxLength(80).IsRequired();
        builder.Property(profile => profile.DefaultCountry).HasMaxLength(80).HasDefaultValue("România").IsRequired();
        builder.Property(profile => profile.DefaultLatitude).HasPrecision(9, 6);
        builder.Property(profile => profile.DefaultLongitude).HasPrecision(9, 6);
        builder.Property(profile => profile.MarketRadiusKm).HasDefaultValue(30);
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<BuyerProfile>(profile => profile.Id).OnDelete(DeleteBehavior.Cascade);
    }
}
