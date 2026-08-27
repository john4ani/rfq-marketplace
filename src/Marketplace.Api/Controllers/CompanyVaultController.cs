using Marketplace.Application.AutoBidding;
using Marketplace.Application.Companies;
using Marketplace.Domain.Vault;
using Marketplace.Infrastructure.AutoBidding;
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
    ICompanyAuthorizationService companyAuthorizationService,
    IPipelineProgressStore progressStore) : ControllerBase
{
    // MapInboundClaims (default) remaps the JWT "sub" claim to ClaimTypes.NameIdentifier, so read both.
    private Guid UserId => Guid.TryParse(
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
        out Guid id) ? id : Guid.Empty;

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
    /// The optional <paramref name="progressRequestId"/> lets the frontend poll real per-stage progress
    /// while the request is still running (see <c>GET specifications/progress/{requestId}</c>).
    /// </summary>
    [HttpPost("specifications")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadSpecification(
        Guid companyId, IFormFile file, [FromQuery] Guid? progressRequestId, CancellationToken cancellationToken)
    {
        if (UserId == Guid.Empty || !await companyAuthorizationService.CanRepresentCompanyAsync(UserId, companyId, cancellationToken))
            return NotFound();
        if (file is null || file.Length == 0) return DocumentValidationProblem("Alege un fișier PDF de încărcat.");
        if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return DocumentValidationProblem("Încarcă un fișier PDF (Caiet de sarcini).");

        try
        {
            await using Stream content = file.OpenReadStream();
            SpecificationBidResult result = await autoBiddingService.ProcessSpecificationAsync(
                companyId, file.FileName, content, UserId, cancellationToken, progressRequestId);
            if (progressRequestId is Guid requestId)
                progressStore.Report(requestId, PipelineStage.Completed, "Propunerea a fost generată.");
            return Ok(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (progressRequestId is Guid failedRequestId)
                progressStore.Report(failedRequestId, PipelineStage.Failed, exception.Message);
            throw;
        }
    }

    /// <summary>Re-runs the LLM proposal step for an already-extracted Caiet de sarcini document.</summary>
    [HttpPost("specifications/{documentId:guid}/generate")]
    public async Task<IActionResult> GenerateFromSpecification(
        Guid companyId, Guid documentId, [FromQuery] Guid? progressRequestId, CancellationToken cancellationToken)
    {
        if (UserId == Guid.Empty || !await companyAuthorizationService.CanRepresentCompanyAsync(UserId, companyId, cancellationToken))
            return NotFound();
        try
        {
            SpecificationBidResult result = await autoBiddingService.GenerateFromSpecificationAsync(
                companyId, documentId, cancellationToken, progressRequestId);
            if (progressRequestId is Guid requestId)
                progressStore.Report(requestId, PipelineStage.Completed, "Propunerea a fost regenerată.");
            return Ok(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (progressRequestId is Guid failedRequestId)
                progressStore.Report(failedRequestId, PipelineStage.Failed, exception.Message);
            throw;
        }
    }

    /// <summary>Returns the current stage of a running auto-bidding pipeline (frontend polls this while the
    /// upload/generate request is in flight to show real progress instead of an endless spinner).</summary>
    [HttpGet("specifications/progress/{requestId:guid}")]
    public IActionResult GetPipelineProgress(Guid requestId)
    {
        PipelineProgress? progress = progressStore.Find(requestId);
        return progress is null ? NotFound() : Ok(progress);
    }

    /// <summary>
    /// Streams one of the generated proposal files (technical .docx or bill of quantities .xlsx).
    /// The persisted storage reference (<c>local://…</c> or <c>s3://…</c>) is resolved server-side, so
    /// browsers never see a non-navigable scheme.
    /// </summary>
    [HttpGet("documents/{documentId:guid}/file")]
    public async Task<IActionResult> DownloadGeneratedFile(
        Guid companyId, Guid documentId, [FromQuery] GeneratedBidFileKind kind, CancellationToken cancellationToken)
    {
        if (UserId == Guid.Empty || !await companyAuthorizationService.CanRepresentCompanyAsync(UserId, companyId, cancellationToken))
            return NotFound();
        VaultDownload? download = await vaultService.DownloadGeneratedFileAsync(companyId, documentId, kind, cancellationToken);
        if (download is null) return NotFound();
        return File(download.Content, download.ContentType, download.FileName);
    }

    private ActionResult DocumentValidationProblem(string detail) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["document"] = [detail] }));
}
