using Marketplace.Domain.Vault;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("DocumentChunks");
        builder.HasKey(chunk => chunk.Id);
        builder.Property(chunk => chunk.Content).HasColumnType("text").IsRequired();
        builder.Property(chunk => chunk.Embedding)
            .HasColumnType("vector(1536)")
            .HasConversion(
                embedding => embedding == null ? null : new Vector(embedding),
                value => value == null ? null : value.ToArray());
        builder.HasIndex(chunk => new { chunk.DocumentId, chunk.ChunkIndex }).IsUnique();
        builder.HasOne<VaultDocument>().WithMany().HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
