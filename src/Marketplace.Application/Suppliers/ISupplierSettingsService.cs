using Marketplace.Domain.Suppliers;

namespace Marketplace.Application.Suppliers;

public interface ISupplierSettingsService
{
    Task<IReadOnlyCollection<Category>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<CategoryCreationResult> AddCategoryAsync(string name, bool confirmSimilar, CancellationToken cancellationToken);
    Task<SupplierCategorySelection?> GetCategoriesAsync(Guid supplierId, CancellationToken cancellationToken);
    Task<SupplierCategorySelection?> SaveCategoriesAsync(Guid supplierId, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken);
    Task<SupplierDeliverySelection?> GetDeliverySettingsAsync(Guid supplierId, CancellationToken cancellationToken);
    Task<SupplierDeliverySelection?> SaveDeliverySettingsAsync(Guid supplierId, SupplierDeliveryInput input, CancellationToken cancellationToken);
}

public sealed record SupplierCategorySelection(IReadOnlyCollection<Guid> CategoryIds);
public sealed record CategoryCreationResult(Category? Category, bool AlreadyExisted, IReadOnlyCollection<Category> SimilarCategories);
public sealed record SupplierDeliverySelection(
    int NormalDeliveryRadiusKm,
    int MaximumDeliveryRadiusKm,
    decimal? MinimumOrderValue,
    bool NationwideDelivery,
    IReadOnlyCollection<string> Counties);
public sealed record SupplierDeliveryInput(
    int NormalDeliveryRadiusKm,
    int MaximumDeliveryRadiusKm,
    decimal? MinimumOrderValue,
    bool NationwideDelivery,
    IReadOnlyCollection<string> Counties);
