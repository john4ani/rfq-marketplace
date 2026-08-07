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

    public static Category Create(Guid id, Guid verticalId, Guid? parentId, string name, string slug, int sortOrder) =>
        new() { Id = id, VerticalId = verticalId, ParentId = parentId, Name = name, Slug = slug, SortOrder = sortOrder, IsActive = true };
}
