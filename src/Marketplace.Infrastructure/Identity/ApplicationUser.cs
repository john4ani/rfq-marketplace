using Microsoft.AspNetCore.Identity;

namespace Marketplace.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Launch offer: each account gets one free document generation (.docx/.xlsx). Decremented by
    /// the API when a generation consumes the free entitlement; the fair-pricing tiers apply afterwards.</summary>
    public int FreeGenerationsRemaining { get; set; } = 1;
}
