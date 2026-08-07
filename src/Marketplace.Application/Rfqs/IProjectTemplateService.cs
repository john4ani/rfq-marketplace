namespace Marketplace.Application.Rfqs;

public interface IProjectTemplateService
{
    Task<IReadOnlyCollection<ProjectTemplateView>> GetAsync(CancellationToken cancellationToken);
    Task<ProjectTemplateView> CreateAsync(ProjectTemplateInput input, CancellationToken cancellationToken);
}

public sealed record ProjectTemplateInput(
    string Name,
    string? Description,
    IReadOnlyCollection<Guid> CategoryIds);

public sealed record ProjectTemplateView(
    Guid Id,
    Guid VerticalId,
    string Name,
    string Slug,
    string? Description,
    IReadOnlyCollection<Guid> CategoryIds);

public sealed class ProjectTemplateValidationException(string message) : InvalidOperationException(message);
