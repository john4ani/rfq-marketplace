using Marketplace.Domain.Suppliers;

namespace Marketplace.Domain.Seap;

/// <summary>
/// Persistent cache CPV code → CompaSpot category. CPV codes are standardized (45342000-6 is always
/// "Montare de garduri"), so the AI categorizer is called once per unique code instead of once per
/// tender; every following tender with the same code resolves its category instantly from this table.
/// </summary>
public sealed class CpvCategoryMapping
{
    private CpvCategoryMapping() { }

    public string CpvCode { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static CpvCategoryMapping Create(string cpvCode, Guid categoryId, DateTimeOffset now) => new()
    {
        CpvCode = cpvCode.Trim(),
        CategoryId = categoryId,
        UpdatedAt = now
    };
}
