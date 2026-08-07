namespace Marketplace.Application.Identity;

public interface IEmailVerificationDelivery { Task SendAsync(string email, string token, CancellationToken cancellationToken); }
public sealed class EmailVerificationDeliveryException(string message, Exception innerException) : Exception(message, innerException);
