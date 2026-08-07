using Marketplace.Domain.Trust;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Marketplace.Infrastructure.Persistence.Configurations;
public sealed class AccountAuditEventConfiguration : IEntityTypeConfiguration<AccountAuditEvent>
{
    public void Configure(EntityTypeBuilder<AccountAuditEvent> builder)
    {
        builder.ToTable("AccountAuditEvents"); builder.HasKey(item => item.Id); builder.Property(item => item.Details).HasMaxLength(1000); builder.HasIndex(item => new { item.AccountId, item.OccurredAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
