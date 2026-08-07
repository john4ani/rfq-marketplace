namespace Marketplace.Domain.Rfqs;

public sealed class ProjectTemplate
{
    private readonly List<ProjectTemplateCategory> _categories = [];
    private ProjectTemplate() { }

    public Guid Id { get; private set; }
    public Guid VerticalId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<ProjectTemplateCategory> Categories => _categories;

    public static ProjectTemplate Create(
        Guid id,
        Guid verticalId,
        string name,
        string slug,
        string? description,
        IEnumerable<Guid> categoryIds,
        DateTimeOffset now)
    {
        if (id == Guid.Empty || verticalId == Guid.Empty)
            throw new RfqRuleException("Verticala și identificatorul șablonului sunt obligatorii.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug))
            throw new RfqRuleException("Denumirea și identificatorul URL al șablonului sunt obligatorii.");
        Guid[] categories = categoryIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        if (categories.Length == 0)
            throw new RfqRuleException("Selectează cel puțin o categorie pentru șablonul de proiect.");

        var template = new ProjectTemplate
        {
            Id = id,
            VerticalId = verticalId,
            Name = name.Trim(),
            Slug = slug.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive = true,
            CreatedAt = now
        };
        template._categories.AddRange(categories.Select(categoryId => ProjectTemplateCategory.Create(id, categoryId)));
        return template;
    }
}

public sealed class ProjectTemplateCategory
{
    private ProjectTemplateCategory() { }
    public Guid ProjectTemplateId { get; private set; }
    public Guid CategoryId { get; private set; }

    internal static ProjectTemplateCategory Create(Guid templateId, Guid categoryId) =>
        new() { ProjectTemplateId = templateId, CategoryId = categoryId };
}
