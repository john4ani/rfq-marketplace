using Marketplace.Domain.Trust;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Marketplace.Infrastructure.Persistence.Configurations;
public sealed class AccountTrustRecordConfiguration : IEntityTypeConfiguration<AccountTrustRecord>
{
    public void Configure(EntityTypeBuilder<AccountTrustRecord> builder)
    {
        builder.ToTable("AccountTrustRecords"); builder.HasKey(item => item.AccountId); builder.Property(item => item.VerificationNote).HasMaxLength(1000);
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<AccountTrustRecord>(item => item.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.PhoneVerifiedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CompanyVerifiedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
