using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.AiIntake;
using Marketplace.Application.Companies;
using Marketplace.Application.Seap;
using Marketplace.Domain.AiIntake;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Marketplace.Api.Controllers;

// SEAP Tender Match: a third distinct interpretation workflow (like assisted-intakes / construction-intakes)
// that translates a free-text description into rigid SEAP filter parameters (CPV prefixes + counties) and
// persists the confirmed filters on the represented company. It never creates a Procurement Need.
[ApiController]
[Route("api/v1/seap/tender-match")]
[Authorize]
public sealed class SeapTenderMatchController(
    ISeapTenderMatchService service,
    ICompanyAuthorizationService companyAuthorizationService) : ControllerBase
{
    [HttpGet("availability")]
    public IActionResult Availability() => Ok(service.Availability);

    [HttpPost]
    [EnableRateLimiting("assisted-intake")]
    public async Task<IActionResult> Interpret(InterpretSeapTenderMatchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyCollection<SeapClarificationTurn> turns = (request.Turns ?? [])
                .Select(turn => new SeapClarificationTurn(turn.Question?.Trim() ?? string.Empty, turn.Answer?.Trim() ?? string.Empty))
                .ToArray();
            SeapTenderMatchResult result = await service.InterpretAsync(request.Input, turns, cancellationToken);
            return Ok(result);
        }
        catch (AiIntakeUnavailableException exception) { return AiUnavailable(exception.Message); }
        catch (AiIntakeRuleException exception) { return InvalidMatch(exception.Message); }
    }

    [HttpPut("profile")]
    public async Task<IActionResult> SaveProfile(SaveSeapTenderMatchProfileRequest request, CancellationToken cancellationToken)
    {
        if (!await CanRepresentCompany(request.CompanyId, cancellationToken)) return NotFound();
        try
        {
            SeapTenderMatchProfile profile = await service.SaveFiltersAsync(
                request.CompanyId, request.CpvPrefixes ?? [], request.Counties ?? [], cancellationToken);
            return Ok(profile);
        }
        catch (AiIntakeRuleException exception) { return InvalidMatch(exception.Message); }
    }

    private async Task<bool> CanRepresentCompany(Guid companyId, CancellationToken cancellationToken)
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid userId)
            && await companyAuthorizationService.CanRepresentCompanyAsync(userId, companyId, cancellationToken);
    }

    private ActionResult InvalidMatch(string message) => ValidationProblem(
        new ValidationProblemDetails(new Dictionary<string, string[]> { ["match"] = [message] })
        { Title = "Potrivirea de anunțuri nu a putut fi procesată." });

    private ObjectResult AiUnavailable(string message) => StatusCode(StatusCodes.Status503ServiceUnavailable,
        new ProblemDetails { Title = "Asistentul de potrivire este temporar indisponibil.", Detail = message });
}

public sealed record InterpretSeapTenderMatchRequest(string Input, IReadOnlyCollection<SeapTurnRequest>? Turns = null);
public sealed record SeapTurnRequest(string? Question, string? Answer);
public sealed record SaveSeapTenderMatchProfileRequest(Guid CompanyId, IReadOnlyCollection<string>? CpvPrefixes, IReadOnlyCollection<string>? Counties);
