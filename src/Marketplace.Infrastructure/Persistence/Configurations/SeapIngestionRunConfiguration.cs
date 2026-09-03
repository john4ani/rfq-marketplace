using Marketplace.Domain.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SeapIngestionRunConfiguration : IEntityTypeConfiguration<SeapIngestionRun>
{
    public void Configure(EntityTypeBuilder<SeapIngestionRun> builder)
    {
        builder.ToTable("SeapIngestionRuns");
        builder.HasKey(run => run.Id);
        builder.HasIndex(run => run.TenderId);
        // One active ingestion run per tender — concurrent resolves of the same notice reuse the running
        // job instead of downloading/analyzing twice. Terminal runs keep the history for diagnostics.
        builder.HasIndex(run => run.TenderId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Pending', 'FetchingDocuments', 'Analyzing')");
        builder.Property(run => run.NoticeNo).HasMaxLength(64).IsRequired();
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(run => run.Purpose).HasConversion<string>().HasMaxLength(24)
            .HasDefaultValue(SeapIngestionRunPurpose.DigestAndAnalyze).IsRequired();
        builder.Property(run => run.AttemptCount).HasDefaultValue(0);
        // Optimistic-concurrency fencing token: EF adds "WHERE ClaimVersion = <original>" to the run UPDATE on
        // every SaveChanges, so a worker whose claim was superseded (lease expired + reclaimed) affects zero
        // rows and the whole checkpoint transaction rolls back (SaveChanges is atomic via the caller's
        // explicit transaction).
        builder.Property(run => run.ClaimVersion).HasDefaultValue(0L).IsConcurrencyToken();
        builder.Property(run => run.ClaimedBy).HasMaxLength(128);
        builder.Property(run => run.AnalysisAttemptId).HasMaxLength(64);
        builder.Property(run => run.AnalysisRequestHash).HasMaxLength(64);
        builder.Property(run => run.ExtractedText).HasColumnType("text");
        builder.Property(run => run.LastErrorCode).HasMaxLength(64);
        builder.Property(run => run.LastErrorMessage).HasColumnType("text");
        builder.HasOne<SeapTender>().WithMany().HasForeignKey(run => run.TenderId).OnDelete(DeleteBehavior.Cascade);
    }
}
