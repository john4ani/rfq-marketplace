namespace Marketplace.Infrastructure.Identity;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public required Guid FamilyId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
