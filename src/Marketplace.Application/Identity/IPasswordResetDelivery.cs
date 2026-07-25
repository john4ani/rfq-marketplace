namespace Marketplace.Application.Identity;

public interface IPasswordResetDelivery
{
    Task SendAsync(string email, string token, CancellationToken cancellationToken);
}

public sealed class PasswordResetDeliveryException(string message, Exception innerException)
    : Exception(message, innerException);
