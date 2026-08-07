using Marketplace.Application.Rfqs;
using Marketplace.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/project-templates")]
[Authorize]
public sealed class ProjectTemplatesController(IProjectTemplateService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(cancellationToken));

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpPost]
    public async Task<IActionResult> Create(ProjectTemplateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ProjectTemplateView result = await service.CreateAsync(
                new(request.Name ?? string.Empty, request.Description, request.CategoryIds ?? []), cancellationToken);
            return Ok(result);
        }
        catch (ProjectTemplateValidationException exception)
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["projectTemplate"] = [exception.Message] }));
        }
    }
}

public sealed record ProjectTemplateRequest(string? Name, string? Description, IReadOnlyCollection<Guid>? CategoryIds);
