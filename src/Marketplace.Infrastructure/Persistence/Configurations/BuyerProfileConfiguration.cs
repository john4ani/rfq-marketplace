using Marketplace.Domain.Profiles;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class BuyerProfileConfiguration : IEntityTypeConfiguration<BuyerProfile>
{
    public void Configure(EntityTypeBuilder<BuyerProfile> builder)
    {
        builder.ToTable("BuyerProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.CompanyName).HasMaxLength(200);
        builder.Property(profile => profile.TaxIdentifier).HasMaxLength(32);
        builder.Property(profile => profile.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(profile => profile.DefaultCity).HasMaxLength(120).IsRequired();
        builder.Property(profile => profile.DefaultCounty).HasMaxLength(80).IsRequired();
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<BuyerProfile>(profile => profile.Id).OnDelete(DeleteBehavior.Cascade);
    }
}
