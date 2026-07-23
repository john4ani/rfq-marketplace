using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(token => token.ReplacedByTokenHash).HasMaxLength(64);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.FamilyId);
        builder.HasOne(token => token.User).WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
