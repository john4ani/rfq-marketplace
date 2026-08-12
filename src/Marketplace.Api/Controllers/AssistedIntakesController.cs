using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.AiIntake;
using Marketplace.Domain.AiIntake;
using Marketplace.Domain.Rfqs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/assisted-intakes")]
[Authorize]
public sealed class AssistedIntakesController(IAssistedIntakeService service) : ControllerBase
{
    [HttpGet("availability")]
    public IActionResult Availability() => Ok(service.Availability);

    [HttpPost]
    [EnableRateLimiting("assisted-intake")]
    public async Task<IActionResult> Start(StartAssistedIntakeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            AssistedIntakeResult result = await service.StartAsync(BuyerId(), request.Input, cancellationToken);
            return CreatedAtAction(nameof(Get), new { sessionId = result.SessionId }, result);
        }
        catch (AiIntakeUnavailableException exception) { return AiUnavailable(exception.Message); }
        catch (AiIntakeRuleException exception) { return InvalidIntake(exception.Message); }
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            AssistedIntakeResult? result = await service.GetAsync(BuyerId(), sessionId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AiIntakeRuleException exception) { return InvalidIntake(exception.Message); }
    }

    [HttpPost("{sessionId:guid}/clarifications")]
    [EnableRateLimiting("assisted-intake")]
    public async Task<IActionResult> Clarify(
        Guid sessionId, ClarifyAssistedIntakeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            AssistedIntakeResult? result = await service.ClarifyAsync(BuyerId(), sessionId, request.Answer, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AiIntakeUnavailableException exception) { return AiUnavailable(exception.Message); }
        catch (AiIntakeConcurrencyException exception) { return Conflict(new ProblemDetails { Title = "Draftul s-a modificat.", Detail = exception.Message }); }
        catch (AiIntakeRuleException exception) { return InvalidIntake(exception.Message); }
    }

    [HttpPost("{sessionId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid sessionId, ApproveAssistedIntakeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq = await service.ApproveAsync(BuyerId(), sessionId, request.Draft.ToDraft(),
                request.CompletedInAdvancedForm, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (AiIntakeRuleException exception) { return InvalidIntake(exception.Message); }
        catch (RfqRuleException exception) { return InvalidIntake(exception.Message); }
    }

    private Guid BuyerId()
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid id) ? id : throw new UnauthorizedAccessException();
    }

    private ActionResult InvalidIntake(string message) => ValidationProblem(
        new ValidationProblemDetails(new Dictionary<string, string[]> { ["intake"] = [message] })
        { Title = "Draftul asistat nu a putut fi procesat." });

    private ObjectResult AiUnavailable(string message) => StatusCode(StatusCodes.Status503ServiceUnavailable,
        new ProblemDetails { Title = "Asistentul AI este temporar indisponibil.", Detail = message });
}

public sealed record StartAssistedIntakeRequest(string Input);
public sealed record ClarifyAssistedIntakeRequest(string Answer);
public sealed record ApproveAssistedIntakeRequest(RfqRequest Draft, bool CompletedInAdvancedForm = false);
