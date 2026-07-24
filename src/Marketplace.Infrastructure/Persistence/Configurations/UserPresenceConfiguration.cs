using Marketplace.Domain.LiveMarket;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class UserPresenceConfiguration : IEntityTypeConfiguration<UserPresence>
{
    public void Configure(EntityTypeBuilder<UserPresence> builder)
    {
        builder.ToTable("UserPresences");
        builder.HasKey(presence => presence.UserId);
        builder.HasIndex(presence => presence.LastSeenAt);
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserPresence>(presence => presence.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
