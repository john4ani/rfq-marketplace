using Marketplace.Application.AutoBidding;
using Marketplace.Application.Companies;
using Marketplace.Domain.Vault;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/companies/{companyId:guid}/vault")]
[Authorize]
public sealed class CompanyVaultController(
    ICompanyVaultService vaultService,
    IAutoBiddingService autoBiddingService,
    ICompanyAuthorizationService companyAuthorizationService) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue("sub"), out Guid id) ? id : Guid.Empty;

    [HttpGet]
    public async Task<IActionResult> GetVault(Guid companyId, CancellationToken cancellationToken)
    {
        if (UserId == Guid.Empty || !await companyAuthorizationService.CanRepresentCompanyAsync(UserId, companyId, cancellationToken))
            return NotFound();
        return Ok(await vaultService.GetVaultAsync(companyId, cancellationToken));
    }

    [HttpPost("documents")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(Guid companyId, IFormFile file, CancellationToken cancellationToken)
    {
        if (UserId == Guid.Empty || !await companyAuthorizationService.CanRepresentCompanyAsync(UserId, companyId, cancellationToken))
            return NotFound();
        if (file is null || file.Length == 0) return DocumentValidationProblem("Alege un fișier PDF de încărcat.");
        if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return DocumentValidationProblem("Încarcă un fișier PDF.");

        await using Stream content = file.OpenReadStream();
        VaultDocumentView? document = await vaultService.UploadDocumentAsync(
            companyId, file.FileName, content, VaultDocumentType.Other, UserId, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    /// <summary>
    /// Auto-bidding pipeline for a Caiet de sarcini: upload the PDF (R2) → extract Markdown via Azure
    /// Document Intelligence → persist it → generate a basic technical proposal through the LLM.
    /// </summary>
    [HttpPost("specifications")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadSpecification(Guid companyId, IFormFile file, CancellationToken cancellationToken)
    {
        if (UserId == Guid.Empty || !await companyAuthorizationService.CanRepresentCompanyAsync(UserId, companyId, cancellationToken))
            return NotFound();
        if (file is null || file.Length == 0) return DocumentValidationProblem("Alege un fișier PDF de încărcat.");
        if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return DocumentValidationProblem("Încarcă un fișier PDF (Caiet de sarcini).");

        await using Stream content = file.OpenReadStream();
        SpecificationBidResult result = await autoBiddingService.ProcessSpecificationAsync(
            companyId, file.FileName, content, UserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Re-runs the LLM proposal step for an already-extracted Caiet de sarcini document.</summary>
    [HttpPost("specifications/{documentId:guid}/generate")]
    public async Task<IActionResult> GenerateFromSpecification(Guid companyId, Guid documentId, CancellationToken cancellationToken)
    {
        if (UserId == Guid.Empty || !await companyAuthorizationService.CanRepresentCompanyAsync(UserId, companyId, cancellationToken))
            return NotFound();
        SpecificationBidResult result = await autoBiddingService.GenerateFromSpecificationAsync(companyId, documentId, cancellationToken);
        return Ok(result);
    }

    private ActionResult DocumentValidationProblem(string detail) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["document"] = [detail] }));
}
