using Marketplace.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ConversationId, item.CreatedAt });
        builder.HasOne<Conversation>().WithMany().HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(item => item.Body).HasMaxLength(2_000).IsRequired();
    }
}
