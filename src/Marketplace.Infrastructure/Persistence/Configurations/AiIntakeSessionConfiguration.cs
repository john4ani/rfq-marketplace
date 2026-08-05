using Marketplace.Domain.AiIntake;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class AiIntakeSessionConfiguration : IEntityTypeConfiguration<AiIntakeSession>
{
    public void Configure(EntityTypeBuilder<AiIntakeSession> builder)
    {
        builder.ToTable("AiIntakeSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.InitialInput).HasMaxLength(4000);
        builder.Property(session => session.TranscriptJson).HasColumnType("jsonb");
        builder.Property(session => session.ExtractionJson).HasColumnType("jsonb");
        builder.Property(session => session.Provider).HasMaxLength(80);
        builder.Property(session => session.Model).HasMaxLength(120);
        builder.Property(session => session.PromptVersion).HasMaxLength(40);
        builder.Property(session => session.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasIndex(session => new { session.BuyerId, session.UpdatedAt });
        builder.HasIndex(session => session.ApprovedRfqId).IsUnique();
        builder.HasOne<Marketplace.Domain.Profiles.BuyerProfile>().WithMany()
            .HasForeignKey(session => session.BuyerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Marketplace.Domain.Rfqs.Rfq>().WithOne()
            .HasForeignKey<AiIntakeSession>(session => session.ApprovedRfqId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
