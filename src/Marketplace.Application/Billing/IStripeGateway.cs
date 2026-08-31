namespace Marketplace.Application.Billing;

public sealed record StripeCheckoutRequest(Guid TenderId, decimal AmountEur, string? TenderTitle, Guid? CompanyId = null);

public sealed record StripeCheckoutSession(string SessionId, string CheckoutUrl);

/// <summary>Completed checkout payload: the tender to generate and the company whose SEAP qualification
/// vault feeds the proposal (null when the checkout was started without a company context).</summary>
public sealed record StripeCompletedCheckout(Guid TenderId, Guid? CompanyId);

public interface IStripeGateway
{
    Task<StripeCheckoutSession> CreateCheckoutSessionAsync(StripeCheckoutRequest request, CancellationToken cancellationToken);
    Task<StripeCompletedCheckout?> ParseCompletedCheckoutEventAsync(string payload, string signatureHeader);
}
