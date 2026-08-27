namespace Marketplace.Domain.Suppliers;

public sealed class Category
{
    private Category() { }
    public Guid Id { get; private set; }
    public Guid VerticalId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    /// <summary>
    /// CPV main-division prefixes (e.g. "45" for construction works, "44" for construction materials)
    /// that SEAP tenders are matched against when a supplier filters Anunțuri SEAP by this category.
    /// Empty = the category does not restrict the CPV filter.
    /// </summary>
    public string[] CpvPrefixes { get; private set; } = [];

    public static Category Create(Guid id, Guid verticalId, Guid? parentId, string name, string slug, int sortOrder) =>
        new() { Id = id, VerticalId = verticalId, ParentId = parentId, Name = name, Slug = slug, SortOrder = sortOrder, IsActive = true };

    public Category SetCpvPrefixes(string[] prefixes)
    {
        CpvPrefixes = prefixes.Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Select(prefix => prefix.Trim()).Distinct().ToArray();
        return this;
    }
}
