using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.AiIntake;
using Marketplace.Domain.AiIntake;
using Marketplace.Domain.Rfqs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Marketplace.Api.Controllers;

// Distinct route for the Construction Projects intake workflow. It mirrors the assisted-intake contract but is a
// separate interpretation workflow (project understanding first), converging on the shared Procurement Core on approve.
[ApiController]
[Route("api/v1/construction-intakes")]
[Authorize]
public sealed class ConstructionIntakesController(IConstructionIntakeService service) : ControllerBase
{
    [HttpGet("availability")]
    public IActionResult Availability() => Ok(service.Availability);

    [HttpPost]
    [EnableRateLimiting("assisted-intake")]
    public async Task<IActionResult> Start(StartConstructionIntakeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ConstructionIntakeResult result = await service.StartAsync(UserId(), request.Input, cancellationToken);
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
            ConstructionIntakeResult? result = await service.GetAsync(UserId(), sessionId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AiIntakeRuleException exception) { return InvalidIntake(exception.Message); }
    }

    [HttpPost("{sessionId:guid}/clarifications")]
    [EnableRateLimiting("assisted-intake")]
    public async Task<IActionResult> Clarify(
        Guid sessionId, ClarifyConstructionIntakeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ConstructionIntakeResult? result = await service.ClarifyAsync(UserId(), sessionId, request.Answer, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AiIntakeUnavailableException exception) { return AiUnavailable(exception.Message); }
        catch (AiIntakeConcurrencyException exception) { return Conflict(new ProblemDetails { Title = "Proiectul s-a modificat.", Detail = exception.Message }); }
        catch (AiIntakeRuleException exception) { return InvalidIntake(exception.Message); }
    }

    [HttpPost("{sessionId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid sessionId, ApproveConstructionIntakeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq = await service.ApproveAsync(UserId(), sessionId, request.Draft.ToDraft(),
                request.CompletedInAdvancedForm, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (AiIntakeRuleException exception) { return InvalidIntake(exception.Message); }
        catch (RfqRuleException exception) { return InvalidIntake(exception.Message); }
    }

    private Guid UserId()
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid id) ? id : throw new UnauthorizedAccessException();
    }

    private ActionResult InvalidIntake(string message) => ValidationProblem(
        new ValidationProblemDetails(new Dictionary<string, string[]> { ["intake"] = [message] })
        { Title = "Proiectul de construcții nu a putut fi procesat." });

    private ObjectResult AiUnavailable(string message) => StatusCode(StatusCodes.Status503ServiceUnavailable,
        new ProblemDetails { Title = "Asistentul AI este temporar indisponibil.", Detail = message });
}

public sealed record StartConstructionIntakeRequest(string Input);
public sealed record ClarifyConstructionIntakeRequest(string Answer);
public sealed record ApproveConstructionIntakeRequest(RfqRequest Draft, bool CompletedInAdvancedForm = false);
