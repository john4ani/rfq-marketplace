using Marketplace.Domain.Profiles;

namespace Marketplace.Application.Profiles;

public interface IProfileService
{
    Task<BuyerProfile?> GetBuyerAsync(Guid id, CancellationToken cancellationToken);
    Task<BuyerProfile> SaveBuyerAsync(Guid id, BuyerProfileInput input, CancellationToken cancellationToken);
    Task<SupplierProfile?> GetSupplierAsync(Guid id, CancellationToken cancellationToken);
    Task<SupplierProfile> SaveSupplierAsync(Guid id, SupplierProfileInput input, CancellationToken cancellationToken);
}

public sealed record BuyerProfileInput(string DisplayName, BuyerType BuyerType, string? CompanyName, string? TaxIdentifier, string PhoneNumber, string DefaultCity, string DefaultCounty);
public sealed record SupplierProfileInput(string TradingName, string CompanyRegistrationNumber, string? TaxIdentifier, string ContactPerson, string ContactEmail, string PhoneNumber, string? Website, string Address, string City, string County, decimal? Latitude, decimal? Longitude, string? Description);
