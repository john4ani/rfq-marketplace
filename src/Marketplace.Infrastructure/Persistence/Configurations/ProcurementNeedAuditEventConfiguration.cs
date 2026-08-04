using Marketplace.Domain.Rfqs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class ProcurementNeedAuditEventConfiguration : IEntityTypeConfiguration<ProcurementNeedAuditEvent>
{
    public void Configure(EntityTypeBuilder<ProcurementNeedAuditEvent> builder)
    {
        builder.ToTable("ProcurementNeedAuditEvents");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.Details).HasMaxLength(1000);
        builder.HasIndex(auditEvent => new { auditEvent.ProcurementNeedId, auditEvent.OccurredAt });
        builder.HasIndex(auditEvent => auditEvent.ActorId);
        builder.HasOne<Rfq>().WithMany().HasForeignKey(auditEvent => auditEvent.ProcurementNeedId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
