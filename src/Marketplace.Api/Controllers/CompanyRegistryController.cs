using Marketplace.Application.Companies;
using Marketplace.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/admin/companies")]
[Authorize(Roles = MarketplaceRoles.Administrator)]
public sealed class CompanyRegistryController(ICompanyRegistryService registry) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await registry.GetAsync(cancellationToken));

    [HttpPut("discovered/{candidateId:guid}")]
    public async Task<IActionResult> UpdateDiscovered(Guid candidateId, DiscoveredCompanyUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            CompanyRegistryEntry? company = await registry.UpdateDiscoveredAsync(candidateId, new(
                request.CompanyName, request.Locality, request.County, request.Email, request.Phone, request.Website,
                request.CategoryIds ?? []), cancellationToken);
            return company is null ? NotFound() : Ok(company);
        }
        catch (InvalidOperationException exception)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["company"] = [exception.Message] }));
        }
    }

    [HttpDelete("discovered/{candidateId:guid}")]
    public async Task<IActionResult> DeleteDiscovered(Guid candidateId, CancellationToken cancellationToken)
    {
        try { return await registry.DeleteDiscoveredAsync(candidateId, cancellationToken) ? NoContent() : NotFound(); }
        catch (InvalidOperationException exception)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["company"] = [exception.Message] }));
        }
    }

    [HttpPost("registered/{companyId:guid}/activation")]
    public async Task<IActionResult> SetRegisteredActive(Guid companyId, SetRegisteredActiveRequest request, CancellationToken cancellationToken)
    {
        CompanyRegistryEntry? company = await registry.SetRegisteredActiveAsync(companyId, request.IsActive, cancellationToken);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpPost("registered/{companyId:guid}/free-generation")]
    public async Task<IActionResult> GrantFreeGeneration(Guid companyId, CancellationToken cancellationToken)
    {
        CompanyRegistryEntry? company = await registry.GrantFreeGenerationAsync(companyId, cancellationToken);
        return company is null ? NotFound() : Ok(company);
    }
}

public sealed record SetRegisteredActiveRequest(bool IsActive);

public sealed record DiscoveredCompanyUpdateRequest(
    [Required, MinLength(2), MaxLength(200)] string CompanyName,
    [Required, MinLength(2), MaxLength(120)] string Locality,
    [Required, MinLength(2), MaxLength(120)] string County,
    [EmailAddress, MaxLength(254)] string? Email,
    [MaxLength(40)] string? Phone,
    [MaxLength(500)] string? Website,
    IReadOnlyCollection<Guid>? CategoryIds);
