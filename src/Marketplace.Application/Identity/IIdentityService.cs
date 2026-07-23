namespace Marketplace.Application.Identity;

public interface IIdentityService
{
    Task<RegistrationResult> RegisterAsync(string email, string password, string role, CancellationToken cancellationToken);
    Task<AuthenticationResult?> LoginAsync(string email, string password, string role, CancellationToken cancellationToken);
    Task<AuthenticationResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
    Task<string?> CreatePasswordResetTokenAsync(string email, string role, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(string email, string role, string token, string newPassword, CancellationToken cancellationToken);
}

public sealed record AuthenticationResult(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);
public sealed record RegistrationResult(bool Succeeded, IReadOnlyCollection<string> Errors);
