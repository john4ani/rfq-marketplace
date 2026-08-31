using Marketplace.Application.Billing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Marketplace.Infrastructure.Billing;

public sealed partial class StripeGateway : IStripeGateway
{
    private readonly StripeOptions settings;
    private readonly ILogger<StripeGateway> logger;

    public StripeGateway(IOptions<StripeOptions> options, ILogger<StripeGateway> logger)
    {
        settings = options.Value;
        this.logger = logger;
    }

    public async Task<StripeCheckoutSession> CreateCheckoutSessionAsync(StripeCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException("Stripe nu este configurat.");
        StripeConfiguration.ApiKey = settings.ApiKey;
        SessionCreateOptions options = new()
        {
            Mode = "payment",
            ClientReferenceId = request.TenderId.ToString(),
            SuccessUrl = $"{settings.SuccessUrl}?tenderId={request.TenderId}",
            CancelUrl = settings.CancelUrl,
            Metadata = request.CompanyId is Guid companyId
                ? new Dictionary<string, string> { ["companyId"] = companyId.ToString() }
                : new Dictionary<string, string>(),
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "eur",
                        UnitAmount = (long)Math.Round(request.AmountEur * 100m),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Ofertare automată — {request.TenderTitle ?? "anunț SEAP"}"
                        }
                    }
                }
            ]
        };
        Session session = await new SessionService().CreateAsync(options, cancellationToken: cancellationToken);
        LogCheckoutCreated(logger, session.Id, request.AmountEur);
        return new(session.Id, session.Url);
    }

    public Task<StripeCompletedCheckout?> ParseCompletedCheckoutEventAsync(string payload, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(settings.WebhookSecret)) return Task.FromResult<StripeCompletedCheckout?>(null);
        try
        {
            Event stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, settings.WebhookSecret, throwOnApiVersionMismatch: false);
            if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted || stripeEvent.Data.Object is not Session session)
                return Task.FromResult<StripeCompletedCheckout?>(null);
            if (!Guid.TryParse(session.ClientReferenceId, out Guid tenderId))
                return Task.FromResult<StripeCompletedCheckout?>(null);
            Guid? companyId = session.Metadata is { } metadata &&
                              metadata.TryGetValue("companyId", out string? value) &&
                              Guid.TryParse(value, out Guid parsed)
                ? parsed
                : null;
            return Task.FromResult<StripeCompletedCheckout?>(new(tenderId, companyId));
        }
        catch (StripeException)
        {
            return Task.FromResult<StripeCompletedCheckout?>(null);
        }
    }

    [LoggerMessage(EventId = 5401, Level = LogLevel.Information, Message = "Stripe checkout session {SessionId} created for {AmountEur} EUR.")]
    private static partial void LogCheckoutCreated(ILogger logger, string sessionId, decimal amountEur);
}
