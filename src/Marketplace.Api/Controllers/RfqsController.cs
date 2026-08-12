using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Rfqs;
using Marketplace.Domain.Rfqs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/rfqs")]
[Authorize]
public sealed class RfqsController(IRfqService rfqService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        Ok((await rfqService.GetMineAsync(BuyerId(), cancellationToken)).Select(RfqResponse.From));

    [HttpGet("{rfqId:guid}")]
    public async Task<IActionResult> Get(Guid rfqId, CancellationToken cancellationToken)
    {
        Rfq? rfq = await rfqService.GetAsync(BuyerId(), rfqId, cancellationToken);
        return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
    }

    [HttpGet("{rfqId:guid}/history")]
    public async Task<IActionResult> History(Guid rfqId, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<ProcurementNeedAuditEvent>? history =
            await rfqService.GetHistoryAsync(BuyerId(), rfqId, cancellationToken);
        return history is null ? NotFound() : Ok(history.Select(ProcurementNeedAuditResponse.From));
    }

    [HttpPost]
    public async Task<IActionResult> Create(RfqRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Rfq rfq = await rfqService.CreateAsync(BuyerId(), request.ToDraft(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { rfqId = rfq.Id }, RfqResponse.From(rfq));
        }
        catch (RfqRuleException exception) { return InvalidRfq(exception.Message); }
    }

    [HttpPut("{rfqId:guid}")]
    public async Task<IActionResult> Update(Guid rfqId, RfqRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq = await rfqService.UpdateAsync(BuyerId(), rfqId, request.ToDraft(), cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqConcurrencyException exception) { return StaleRfq(exception.Message); }
        catch (RfqRuleException exception) { return InvalidRfq(exception.Message); }
    }

    [HttpPost("{rfqId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid rfqId, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq = await rfqService.PublishAsync(BuyerId(), rfqId, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqConcurrencyException exception) { return StaleRfq(exception.Message); }
        catch (RfqRuleException exception) { return InvalidRfq(exception.Message); }
    }

    [HttpPost("{rfqId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid rfqId, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq = await rfqService.CancelAsync(BuyerId(), rfqId, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqConcurrencyException exception) { return StaleRfq(exception.Message); }
        catch (RfqRuleException exception) { return InvalidRfq(exception.Message); }
    }

    [HttpPost("{rfqId:guid}/outcome")]
    public async Task<IActionResult> RecordOutcome(
        Guid rfqId, RecordProcurementOutcomeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq = await rfqService.RecordOutcomeAsync(
                BuyerId(), rfqId, request.Outcome, request.Reason, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqConcurrencyException exception) { return StaleRfq(exception.Message); }
        catch (RfqRuleException exception) { return InvalidRfq(exception.Message); }
    }

    [HttpPost("{rfqId:guid}/extend-deadline")]
    public async Task<IActionResult> ExtendDeadline(
        Guid rfqId, ExtendRfqDeadlineRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq = await rfqService.ExtendQuotationDeadlineAsync(
                BuyerId(), rfqId, request.QuotationDeadline, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqConcurrencyException exception) { return StaleRfq(exception.Message); }
        catch (RfqRuleException exception) { return InvalidRfq(exception.Message); }
    }

    private Guid BuyerId()
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid id) ? id : throw new UnauthorizedAccessException();
    }

    private ConflictObjectResult StaleRfq(string message) => Conflict(new ProblemDetails
    {
        Title = "Cererea a fost modificată.",
        Detail = message
    });

    private ActionResult InvalidRfq(string message) => ValidationProblem(
        new ValidationProblemDetails(new Dictionary<string, string[]> { ["rfq"] = [message] })
        { Title = "Cererea nu a putut fi salvată." });
}

public sealed record RfqRequest(
    string Title, string? ProjectName, string? Description, Guid PrimaryCategoryId,
    string DeliveryAddress, string DeliveryLocality, string County, int? SupplierSearchRadiusKm, decimal? Latitude, decimal? Longitude,
    DateOnly? PreferredDeliveryDate, DateTimeOffset QuotationDeadline, bool AlternativeProductsAccepted,
    decimal? EstimatedBudget, string? BuyerNotes, IReadOnlyCollection<RfqItemRequest>? Items, bool AllowSplit = false,
    bool DeliveryRequired = false, ProcurementNeedSource Source = ProcurementNeedSource.Manual,
    ProcurementInputType InputType = ProcurementInputType.StructuredForm,
    IReadOnlyCollection<Guid>? CategoryIds = null,
    ProcurementNeedType? NeedType = null,
    Guid? ProjectTemplateId = null)
{
    public RfqDraft ToDraft() => new(
        Title ?? string.Empty, ProjectName, Description, PrimaryCategoryId,
        DeliveryAddress ?? string.Empty, DeliveryLocality ?? string.Empty, County ?? string.Empty, SupplierSearchRadiusKm ?? 30,
        Latitude, Longitude, PreferredDeliveryDate, QuotationDeadline, AlternativeProductsAccepted,
        EstimatedBudget, BuyerNotes, Items?.Select(item => item.ToDraft()).ToArray() ?? [], AllowSplit, DeliveryRequired,
        Source, InputType, CategoryIds, NeedType, ProjectTemplateId);
}

public sealed record RfqItemRequest(
    string ProductName, string? ManufacturerOrBrand, string? Specification, decimal Quantity,
    UnitOfMeasure Unit, bool AlternativesAccepted, string? BuyerNotes, Guid? CategoryId,
    RequirementKind Kind = RequirementKind.Product)
{
    public RfqItemDraft ToDraft() => new(ProductName ?? string.Empty, ManufacturerOrBrand, Specification, Quantity, Unit, AlternativesAccepted, BuyerNotes, CategoryId, Kind);
}

public sealed record ExtendRfqDeadlineRequest(DateTimeOffset QuotationDeadline);
public sealed record RecordProcurementOutcomeRequest(ProcurementOutcome Outcome, string? Reason);

public sealed record RfqResponse(
    Guid Id, Guid BuyerId, string Title, string? ProjectName, string? Description, Guid PrimaryCategoryId,
    IReadOnlyCollection<Guid> CategoryIds, ProcurementNeedType NeedType, Guid? ProjectTemplateId,
    string DeliveryAddress, string DeliveryLocality, string County, int SupplierSearchRadiusKm, decimal? Latitude, decimal? Longitude,
    DateOnly? PreferredDeliveryDate, DateTimeOffset QuotationDeadline, bool AlternativeProductsAccepted,
    decimal? EstimatedBudget, string? BuyerNotes, bool AllowSplit, RfqStatus Status, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? PublishedAt, DateTimeOffset? ClosedAt, Guid? SelectedOfferId,
    int ReopenCount, bool DeliveryRequired, FulfillmentStatus FulfillmentStatus,
    DateTimeOffset? DeliveryReportedAt, DateTimeOffset? DeliveredAt,
    ProcurementNeedSource Source, ProcurementInputType InputType, ProcurementNeedStatus NeedStatus,
    ProjectContextResponse? ProjectContext, DeliveryConstraintsResponse DeliveryConstraints,
    ProcurementOutcome? Outcome, string? OutcomeReason, DateTimeOffset? OutcomeRecordedAt,
    IReadOnlyCollection<RfqItemResponse> Items)
{
    public static RfqResponse From(Rfq rfq) => new(
        rfq.Id, rfq.BuyerId, rfq.Title, rfq.ProjectName, rfq.Description, rfq.PrimaryCategoryId,
        rfq.RequiredCategoryIds, rfq.NeedType, rfq.ProjectTemplateId,
        rfq.DeliveryAddress, rfq.DeliveryLocality, rfq.County, rfq.SupplierSearchRadiusKm, rfq.Latitude, rfq.Longitude,
        rfq.PreferredDeliveryDate, rfq.QuotationDeadline, rfq.AlternativeProductsAccepted,
        rfq.EstimatedBudget, rfq.BuyerNotes, rfq.AllowSplit, rfq.Status, rfq.CreatedAt, rfq.UpdatedAt,
        rfq.PublishedAt, rfq.ClosedAt, rfq.SelectedOfferId, rfq.ReopenCount,
        rfq.DeliveryRequired, rfq.FulfillmentStatus, rfq.DeliveryReportedAt, rfq.DeliveredAt,
        rfq.Source, rfq.InputType, rfq.NeedStatus,
        ProjectContextResponse.From(rfq.ProjectContext), DeliveryConstraintsResponse.From(rfq.DeliveryConstraints),
        rfq.Outcome, rfq.OutcomeReason, rfq.OutcomeRecordedAt,
        rfq.Items.OrderBy(item => item.ItemNumber).Select(RfqItemResponse.From).ToArray());
}

public sealed record RfqItemResponse(
    Guid Id, int ItemNumber, string ProductName, string? ManufacturerOrBrand, string? Specification,
    decimal Quantity, UnitOfMeasure Unit, bool AlternativesAccepted, string? BuyerNotes, Guid? CategoryId,
    RequirementKind Kind)
{
    public static RfqItemResponse From(RfqItem item) => new(
        item.Id, item.ItemNumber, item.ProductName, item.ManufacturerOrBrand, item.Specification,
        item.Quantity, item.Unit, item.AlternativesAccepted, item.BuyerNotes, item.CategoryId, item.Kind);
}

public sealed record ProjectContextResponse(string? Name, string? Description)
{
    public static ProjectContextResponse? From(ProjectContext? context) =>
        context is null ? null : new(context.Name, context.Description);
}

public sealed record DeliveryConstraintsResponse(
    string Address, string Locality, string County, int SupplierSearchRadiusKm,
    decimal? Latitude, decimal? Longitude, DateOnly? PreferredDeliveryDate, bool TransportRequired)
{
    public static DeliveryConstraintsResponse From(DeliveryConstraints constraints) => new(
        constraints.Address, constraints.Locality, constraints.County, constraints.SupplierSearchRadiusKm,
        constraints.Latitude, constraints.Longitude, constraints.PreferredDeliveryDate, constraints.TransportRequired);
}

public sealed record ProcurementNeedAuditResponse(
    Guid Id, Guid ProcurementNeedId, Guid ActorId, ProcurementNeedAuditEventType Type,
    ProcurementNeedStatus FromStatus, ProcurementNeedStatus ToStatus, string? Details, DateTimeOffset OccurredAt)
{
    public static ProcurementNeedAuditResponse From(ProcurementNeedAuditEvent auditEvent) => new(
        auditEvent.Id, auditEvent.ProcurementNeedId, auditEvent.ActorId, auditEvent.Type,
        auditEvent.FromStatus, auditEvent.ToStatus, auditEvent.Details, auditEvent.OccurredAt);
}
