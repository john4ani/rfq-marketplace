using Marketplace.Application.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Marketplace.Api.Controllers;

/// <summary>
/// Company SEAP qualification vault: legal/administrative data, similar-experience portfolio,
/// certificates and key experts. Every endpoint is guarded by <c>CanRepresentCompany</c> — a caller
/// that cannot represent the company sees 404 (no existence leak), mirroring the document vault.
/// </summary>
[ApiController]
[Route("api/v1/companies/{companyId:guid}/qualification")]
[Authorize]
public sealed class CompanyQualificationController(
    ICompanyQualificationService qualificationService,
    ICompanyAuthorizationService companyAuthorizationService) : ControllerBase
{
    // MapInboundClaims (default) remaps the JWT "sub" claim to ClaimTypes.NameIdentifier, so read both.
    private Guid UserId => Guid.TryParse(
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
        out Guid id) ? id : Guid.Empty;

    private Task<bool> CanRepresentAsync(Guid companyId, CancellationToken cancellationToken) =>
        UserId == Guid.Empty
            ? Task.FromResult(false)
            : companyAuthorizationService.CanRepresentCompanyAsync(UserId, companyId, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> GetQualification(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        CompanyQualificationDto? result = await qualificationService.GetQualificationAsync(companyId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("legal")]
    public async Task<IActionResult> UpdateLegalProfile(Guid companyId, CompanyLegalProfileInput input, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        CompanyLegalProfileDto? result = await qualificationService.UpdateLegalProfileAsync(companyId, input, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("experience")]
    public async Task<IActionResult> AddExperience(Guid companyId, CompanyExperienceInput input, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        CompanyExperienceDto? result = await qualificationService.AddExperienceAsync(companyId, input, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("experience/{experienceId:guid}")]
    public async Task<IActionResult> UpdateExperience(Guid companyId, Guid experienceId, CompanyExperienceInput input, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        CompanyExperienceDto? result = await qualificationService.UpdateExperienceAsync(companyId, experienceId, input, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("experience/{experienceId:guid}")]
    public async Task<IActionResult> RemoveExperience(Guid companyId, Guid experienceId, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        return await qualificationService.RemoveExperienceAsync(companyId, experienceId, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPost("certificates")]
    public async Task<IActionResult> AddCertificate(Guid companyId, CompanyCertificateInput input, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        CompanyCertificateDto? result = await qualificationService.AddCertificateAsync(companyId, input, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("certificates/{certificateId:guid}")]
    public async Task<IActionResult> UpdateCertificate(Guid companyId, Guid certificateId, CompanyCertificateInput input, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        CompanyCertificateDto? result = await qualificationService.UpdateCertificateAsync(companyId, certificateId, input, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("certificates/{certificateId:guid}")]
    public async Task<IActionResult> RemoveCertificate(Guid companyId, Guid certificateId, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        return await qualificationService.RemoveCertificateAsync(companyId, certificateId, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPost("personnel")]
    public async Task<IActionResult> AddPersonnel(Guid companyId, CompanyPersonnelInput input, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        CompanyPersonnelDto? result = await qualificationService.AddPersonnelAsync(companyId, input, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("personnel/{personnelId:guid}")]
    public async Task<IActionResult> UpdatePersonnel(Guid companyId, Guid personnelId, CompanyPersonnelInput input, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        CompanyPersonnelDto? result = await qualificationService.UpdatePersonnelAsync(companyId, personnelId, input, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("personnel/{personnelId:guid}")]
    public async Task<IActionResult> RemovePersonnel(Guid companyId, Guid personnelId, CancellationToken cancellationToken)
    {
        if (!await CanRepresentAsync(companyId, cancellationToken)) return NotFound();
        return await qualificationService.RemovePersonnelAsync(companyId, personnelId, cancellationToken) ? NoContent() : NotFound();
    }
}
