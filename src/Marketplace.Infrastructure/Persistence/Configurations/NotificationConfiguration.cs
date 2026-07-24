using Marketplace.Domain.Notifications;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).HasMaxLength(180).IsRequired();
        builder.Property(item => item.Message).HasMaxLength(500).IsRequired();
        builder.Property(item => item.Link).HasMaxLength(300).IsRequired();
        builder.Property(item => item.DeduplicationKey).HasMaxLength(180).IsRequired();
        builder.Ignore(item => item.IsRead);
        builder.HasIndex(item => item.DeduplicationKey).IsUnique();
        builder.HasIndex(item => new { item.RecipientId, item.ReadAt, item.CreatedAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.RecipientId).OnDelete(DeleteBehavior.Cascade);
    }
}
