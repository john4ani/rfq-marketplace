namespace Marketplace.Application.Identity;

public interface IIdentityService
{
    Task<RegistrationResult> RegisterAsync(string email, string password, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
    Task<AuthenticationResult?> LoginAsync(string email, string password, CancellationToken cancellationToken);
    Task<AuthenticationResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task<ProfileAdditionResult> AddProfileAsync(Guid currentUserId, string accountType, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LinkedAccount>> GetLinkedAccountsAsync(Guid currentUserId, CancellationToken cancellationToken);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
    Task<string?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken);
}

public sealed record AuthenticationResult(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);
public sealed record RegistrationResult(bool Succeeded, IReadOnlyCollection<string> Errors);
public sealed record ProfileAdditionResult(AuthenticationResult? Authentication, IReadOnlyCollection<string> Errors)
{
    public bool Succeeded => Authentication is not null;
}
public sealed record LinkedAccount(Guid UserId, string Email, string AccountType);
