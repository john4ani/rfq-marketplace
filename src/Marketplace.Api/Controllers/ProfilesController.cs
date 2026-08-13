using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Companies;
using Marketplace.Application.Profiles;
using Marketplace.Domain.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ProfilesController(
    IProfileService profileService,
    ICompanyAuthorizationService companyAuthorizationService) : ControllerBase
{
    [Authorize]
    [HttpGet("buyers/{buyerId:guid}")]
    public async Task<IActionResult> GetBuyer(Guid buyerId, CancellationToken cancellationToken)
    {
        if (!Owns(buyerId)) return NotFound();
        BuyerProfile? profile = await profileService.GetBuyerAsync(buyerId, cancellationToken);
        return profile is null ? NotFound() : Ok(BuyerResponse.From(profile));
    }

    [Authorize]
    [HttpPut("buyers/{buyerId:guid}")]
    public async Task<IActionResult> SaveBuyer(Guid buyerId, BuyerProfileRequest request, CancellationToken cancellationToken)
    {
        if (!Owns(buyerId)) return NotFound();
        BuyerProfile profile = await profileService.SaveBuyerAsync(buyerId, new(request.DisplayName.Trim(), request.BuyerType, Clean(request.CompanyName), Clean(request.TaxIdentifier), request.PhoneNumber.Trim(), request.DefaultCity.Trim(), request.DefaultCounty.Trim(), request.MarketRadiusKm ?? 30), cancellationToken);
        return Ok(BuyerResponse.From(profile));
    }

    [Authorize]
    [HttpGet("suppliers/{supplierId:guid}")]
    public async Task<IActionResult> GetSupplier(Guid supplierId, CancellationToken cancellationToken)
    {
        if (!await CanRepresentCompany(supplierId, cancellationToken)) return NotFound();
        SupplierProfile? profile = await profileService.GetSupplierAsync(supplierId, cancellationToken);
        return profile is null ? NotFound() : Ok(SupplierResponse.From(profile));
    }

    [Authorize]
    [HttpPut("suppliers/{supplierId:guid}")]
    public async Task<IActionResult> SaveSupplier(Guid supplierId, SupplierProfileRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageCompany(supplierId, cancellationToken)) return NotFound();
        SupplierProfile profile = await profileService.SaveSupplierAsync(supplierId, new(request.TradingName.Trim(), request.CompanyRegistrationNumber.Trim(), Clean(request.TaxIdentifier), request.ContactPerson.Trim(), request.ContactEmail.Trim(), request.PhoneNumber.Trim(), Clean(request.Website), request.Address.Trim(), request.City.Trim(), request.County.Trim(), request.Latitude, request.Longitude, Clean(request.Description)), cancellationToken);
        return Ok(SupplierResponse.From(profile));
    }

    private bool Owns(Guid id)
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid accountId) && accountId == id;
    }

    private async Task<bool> CanManageCompany(Guid companyId, CancellationToken cancellationToken)
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out Guid userId)) return false;
        return await companyAuthorizationService.CanRepresentCompanyAsync(userId, companyId, cancellationToken)
            || await companyAuthorizationService.CanCreateLegacyCompanyAsync(userId, companyId, cancellationToken);
    }

    private async Task<bool> CanRepresentCompany(Guid companyId, CancellationToken cancellationToken)
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid userId)
            && await companyAuthorizationService.CanRepresentCompanyAsync(userId, companyId, cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record BuyerProfileRequest(
    [Required, MaxLength(160)] string DisplayName, BuyerType BuyerType,
    [MaxLength(200)] string? CompanyName, [MaxLength(32)] string? TaxIdentifier,
    [Required, MaxLength(32)] string PhoneNumber, [Required, MaxLength(120)] string DefaultCity,
    [Required, MaxLength(80)] string DefaultCounty, [Range(1, 1000)] int? MarketRadiusKm);

public sealed record SupplierProfileRequest(
    [Required, MaxLength(200)] string TradingName, [Required, MaxLength(64)] string CompanyRegistrationNumber,
    [MaxLength(32)] string? TaxIdentifier, [Required, MaxLength(160)] string ContactPerson,
    [Required, EmailAddress] string ContactEmail, [Required, MaxLength(32)] string PhoneNumber,
    [Url, MaxLength(300)] string? Website, [Required, MaxLength(300)] string Address,
    [Required, MaxLength(120)] string City, [Required, MaxLength(80)] string County,
    [Range(-90, 90)] decimal? Latitude, [Range(-180, 180)] decimal? Longitude,
    [MaxLength(2000)] string? Description);

public sealed record BuyerResponse(Guid Id, string DisplayName, BuyerType BuyerType, string? CompanyName, string? TaxIdentifier, string PhoneNumber, string DefaultCity, string DefaultCounty, int MarketRadiusKm, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static BuyerResponse From(BuyerProfile profile) => new(profile.Id, profile.DisplayName, profile.BuyerType, profile.CompanyName, profile.TaxIdentifier, profile.PhoneNumber, profile.DefaultCity, profile.DefaultCounty, profile.MarketRadiusKm, profile.CreatedAt, profile.UpdatedAt);
}

public sealed record SupplierResponse(Guid Id, string TradingName, string CompanyRegistrationNumber, string? TaxIdentifier, string ContactPerson, string ContactEmail, string PhoneNumber, string? Website, string Address, string City, string County, decimal? Latitude, decimal? Longitude, string? Description, SupplierApprovalStatus ApprovalStatus, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static SupplierResponse From(SupplierProfile profile) => new(profile.Id, profile.TradingName, profile.CompanyRegistrationNumber, profile.TaxIdentifier, profile.ContactPerson, profile.ContactEmail, profile.PhoneNumber, profile.Website, profile.Address, profile.City, profile.County, profile.Latitude, profile.Longitude, profile.Description, profile.ApprovalStatus, profile.IsActive, profile.CreatedAt, profile.UpdatedAt);
}
