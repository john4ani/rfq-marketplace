using Marketplace.Domain.Messaging;
using Marketplace.Domain.Offers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.OfferId).IsUnique();
        builder.HasOne<Offer>().WithOne().HasForeignKey<Conversation>(item => item.OfferId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(item => item.OfferId).IsRequired();
        builder.Property(item => item.BuyerId).IsRequired();
        builder.Property(item => item.SupplierId).IsRequired();
    }
}
