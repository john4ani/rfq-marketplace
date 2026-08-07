using System.Globalization;
using System.Text;
using Marketplace.Application.Rfqs;
using Marketplace.Domain.Rfqs;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Rfqs;

public sealed class ProjectTemplateService(MarketplaceDbContext dbContext, TimeProvider timeProvider) : IProjectTemplateService
{
    public async Task<IReadOnlyCollection<ProjectTemplateView>> GetAsync(CancellationToken cancellationToken) =>
        (await dbContext.ProjectTemplates.AsNoTracking().Include(template => template.Categories)
            .Where(template => template.IsActive)
            .OrderBy(template => template.Name)
            .ToArrayAsync(cancellationToken))
        .Select(ToView).ToArray();

    public async Task<ProjectTemplateView> CreateAsync(ProjectTemplateInput input, CancellationToken cancellationToken)
    {
        string name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 180)
            throw new ProjectTemplateValidationException("Denumirea șablonului trebuie să conțină între 2 și 180 de caractere.");
        Guid[] categoryIds = input.CategoryIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (categoryIds.Length == 0)
            throw new ProjectTemplateValidationException("Selectează cel puțin o categorie pentru șablon.");
        int existingCategories = await dbContext.Categories.CountAsync(category =>
            category.IsActive && category.VerticalId == VerticalConfiguration.ConstructionId && categoryIds.Contains(category.Id), cancellationToken);
        if (existingCategories != categoryIds.Length)
            throw new ProjectTemplateValidationException("Una dintre categoriile selectate nu este disponibilă în verticala Construcții.");

        string slug = Slug(name);
        if (await dbContext.ProjectTemplates.AnyAsync(template =>
                template.VerticalId == VerticalConfiguration.ConstructionId && template.Slug == slug, cancellationToken))
            throw new ProjectTemplateValidationException("Există deja un șablon de proiect cu această denumire.");

        ProjectTemplate template = ProjectTemplate.Create(
            Guid.NewGuid(), VerticalConfiguration.ConstructionId, name, slug, input.Description,
            categoryIds, timeProvider.GetUtcNow());
        dbContext.ProjectTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToView(template);
    }

    private static ProjectTemplateView ToView(ProjectTemplate template) => new(
        template.Id, template.VerticalId, template.Name, template.Slug, template.Description,
        template.Categories.Select(category => category.CategoryId).ToArray());

    private static string Slug(string value)
    {
        var result = new StringBuilder();
        bool separator = false;
        foreach (char character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                if (separator && result.Length > 0) result.Append('-');
                result.Append(char.ToLowerInvariant(character));
                separator = false;
            }
            else separator = true;
        }
        return result.ToString();
    }
}
