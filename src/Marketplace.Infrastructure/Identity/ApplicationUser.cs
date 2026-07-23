using Microsoft.AspNetCore.Identity;

namespace Marketplace.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; }
}
