using Marketplace.Domain.Profiles;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class CompanyMembershipConfiguration : IEntityTypeConfiguration<CompanyMembership>
{
    public void Configure(EntityTypeBuilder<CompanyMembership> builder)
    {
        builder.ToTable("CompanyMemberships");
        builder.HasKey(item => new { item.CompanyId, item.UserId });
        builder.Property(item => item.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(item => new { item.CompanyId, item.UserId }).IsUnique();
        builder.HasIndex(item => new { item.UserId, item.RevokedAt });
        builder.HasOne<SupplierProfile>().WithMany().HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
