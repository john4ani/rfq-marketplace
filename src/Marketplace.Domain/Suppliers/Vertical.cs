namespace Marketplace.Domain.Suppliers;

public sealed class Vertical
{
    private Vertical() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static Vertical Create(Guid id, string name, string slug, bool isActive = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("Vertical id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Vertical name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Vertical slug is required.", nameof(slug));
        return new Vertical { Id = id, Name = name.Trim(), Slug = slug.Trim(), IsActive = isActive };
    }
}
