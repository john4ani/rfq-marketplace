namespace Marketplace.Application.Billing;

public sealed record StripeCheckoutRequest(Guid TenderId, decimal AmountEur, string? TenderTitle);

public sealed record StripeCheckoutSession(string SessionId, string CheckoutUrl);

public interface IStripeGateway
{
    Task<StripeCheckoutSession> CreateCheckoutSessionAsync(StripeCheckoutRequest request, CancellationToken cancellationToken);
    Task<string?> ParseCompletedCheckoutEventAsync(string payload, string signatureHeader);
}
