using Marketplace.Domain.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SeapRawIngestionConfiguration : IEntityTypeConfiguration<SeapRawIngestion>
{
    public void Configure(EntityTypeBuilder<SeapRawIngestion> builder)
    {
        builder.ToTable("SeapRawIngestion");
        builder.HasKey(entry => entry.Id);
        builder.HasIndex(entry => entry.NumarAnunt);
        builder.Property(entry => entry.NumarAnunt).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.JsonPayload).HasColumnType("text").IsRequired();
        builder.Property(entry => entry.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(entry => entry.ErrorMessage).HasColumnType("text");
    }
}
