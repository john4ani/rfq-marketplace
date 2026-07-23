using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Marketplace.Application.Identity;
using Marketplace.Domain.Identity;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    MarketplaceDbContext dbContext,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IIdentityService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<RegistrationResult> RegisterAsync(string email, string password, string role, CancellationToken cancellationToken)
    {
        if (role is not (MarketplaceRoles.Buyer or MarketplaceRoles.Supplier))
            return new(false, ["Only buyer and supplier self-registration is allowed."]);

        string normalizedEmail = email.Trim();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = AccountName(normalizedEmail, role), Email = normalizedEmail, CreatedAt = timeProvider.GetUtcNow() };
        IdentityResult created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            string[] errors = created.Errors.Select(error => error.Code switch
            {
                "DuplicateEmail" or "DuplicateUserName" => $"Există deja un cont de tip {RoleLabel(role)} pentru această adresă de e-mail.",
                "InvalidEmail" or "InvalidUserName" => "Introduceți o adresă de e-mail validă.",
                "PasswordTooShort" => "Parola trebuie să conțină cel puțin 8 caractere.",
                _ => error.Description
            }).Distinct(StringComparer.Ordinal).ToArray();
            return new(false, errors);
        }

        IdentityResult assigned = await userManager.AddToRoleAsync(user, role);
        if (!assigned.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return new(false, assigned.Errors.Select(error => error.Description).ToArray());
        }

        return new(true, []);
    }

    public async Task<AuthenticationResult?> LoginAsync(string email, string password, string role, CancellationToken cancellationToken)
    {
        if (role is not (MarketplaceRoles.Buyer or MarketplaceRoles.Supplier)) return null;
        ApplicationUser? user = await userManager.FindByNameAsync(AccountName(email.Trim(), role));
        if (user is null || !await userManager.CheckPasswordAsync(user, password)) return null;
        return await IssueAsync(user, Guid.NewGuid(), cancellationToken);
    }

    public async Task<AuthenticationResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        string hash = Hash(refreshToken);
        RefreshToken? stored = await dbContext.RefreshTokens.Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (stored is null) return null;
        if (stored.RevokedAt is not null)
        {
            await RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            return null;
        }
        if (stored.ExpiresAt <= now) return null;

        string replacement = CreateOpaqueToken();
        stored.RevokedAt = now;
        stored.ReplacedByTokenHash = Hash(replacement);
        AuthenticationResult result = await IssueAsync(stored.User, stored.FamilyId, cancellationToken, replacement);
        return result;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        string hash = Hash(refreshToken);
        RefreshToken? stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<string?> CreatePasswordResetTokenAsync(string email, string role, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByNameAsync(AccountName(email.Trim(), role));
        return user is null ? null : await userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<bool> ResetPasswordAsync(string email, string role, string token, string newPassword, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByNameAsync(AccountName(email.Trim(), role));
        return user is not null && (await userManager.ResetPasswordAsync(user, token, newPassword)).Succeeded;
    }

    private async Task<AuthenticationResult> IssueAsync(ApplicationUser user, Guid familyId, CancellationToken cancellationToken, string? refreshToken = null)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset accessExpiry = now.AddMinutes(_options.AccessTokenMinutes);
        IList<string> roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(JwtRegisteredClaimNames.Email, user.Email!), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256);
        string accessToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(_options.Issuer, _options.Audience, claims, now.UtcDateTime, accessExpiry.UtcDateTime, credentials));
        string opaque = refreshToken ?? CreateOpaqueToken();
        dbContext.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Hash(opaque), FamilyId = familyId, CreatedAt = now, ExpiresAt = now.AddDays(_options.RefreshTokenDays) });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(accessToken, opaque, accessExpiry);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens.Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(updates => updates.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    private static string CreateOpaqueToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string AccountName(string email, string role) => $"{role}+{email}".ToUpperInvariant();
    private static string RoleLabel(string role) => role == MarketplaceRoles.Supplier ? "Furnizor" : "Cumpărător";
}
