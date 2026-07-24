using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Suppliers;
using Marketplace.Domain.Identity;
using Marketplace.Domain.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class SupplierSettingsController(ISupplierSettingsService settingsService) : ControllerBase
{
    [Authorize]
    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Category> categories = await settingsService.GetCategoriesAsync(cancellationToken);
        return Ok(categories.Select(category => new CategoryResponse(category.Id, category.ParentId, category.Name, category.Slug)));
    }

    [Authorize(Roles = MarketplaceRoles.Supplier)]
    [HttpPost("categories")]
    public async Task<IActionResult> AddCategory(AddCategoryRequest request, CancellationToken cancellationToken)
    {
        string name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120)
            return ValidationProblem(ProblemErrors("name", "Denumirea categoriei trebuie să conțină între 2 și 120 de caractere."));
        if (!name.Any(char.IsLetterOrDigit))
            return ValidationProblem(ProblemErrors("name", "Denumirea categoriei trebuie să conțină litere sau cifre."));
        Category category = await settingsService.AddCategoryAsync(name, cancellationToken);
        if (category.ParentId is null)
            return ValidationProblem(ProblemErrors("name", "Aceasta este deja o categorie principală. Alegeți o categorie de produse."));
        return Ok(new CategoryResponse(category.Id, category.ParentId, category.Name, category.Slug));
    }

    [Authorize(Roles = MarketplaceRoles.Supplier)]
    [HttpGet("suppliers/{supplierId:guid}/categories")]
    public async Task<IActionResult> SupplierCategories(Guid supplierId, CancellationToken cancellationToken)
    {
        if (!Owns(supplierId)) return NotFound();
        SupplierCategorySelection? selection = await settingsService.GetCategoriesAsync(supplierId, cancellationToken);
        return selection is null ? MissingProfile() : Ok(selection);
    }

    [Authorize(Roles = MarketplaceRoles.Supplier)]
    [HttpPut("suppliers/{supplierId:guid}/categories")]
    public async Task<IActionResult> SaveSupplierCategories(Guid supplierId, SupplierCategoriesRequest request, CancellationToken cancellationToken)
    {
        if (!Owns(supplierId)) return NotFound();
        IReadOnlyCollection<Category> categories = await settingsService.GetCategoriesAsync(cancellationToken);
        HashSet<Guid> parentIds = categories.Where(category => category.ParentId.HasValue).Select(category => category.ParentId!.Value).ToHashSet();
        HashSet<Guid> selectableIds = categories.Where(category => !parentIds.Contains(category.Id)).Select(category => category.Id).ToHashSet();
        if (request.CategoryIds is null || request.CategoryIds.Count == 0)
            return ValidationProblem(ProblemErrors("categoryIds", "Selectați cel puțin o categorie de produse."));
        if (request.CategoryIds.Any(id => !selectableIds.Contains(id)))
            return ValidationProblem(ProblemErrors("categoryIds", "Una dintre categoriile selectate nu este disponibilă."));
        SupplierCategorySelection? selection = await settingsService.SaveCategoriesAsync(supplierId, request.CategoryIds, cancellationToken);
        return selection is null ? MissingProfile() : Ok(selection);
    }

    [Authorize(Roles = MarketplaceRoles.Supplier)]
    [HttpGet("suppliers/{supplierId:guid}/delivery-settings")]
    public async Task<IActionResult> DeliverySettings(Guid supplierId, CancellationToken cancellationToken)
    {
        if (!Owns(supplierId)) return NotFound();
        SupplierDeliverySelection? selection = await settingsService.GetDeliverySettingsAsync(supplierId, cancellationToken);
        return selection is null ? MissingProfile() : Ok(selection);
    }

    [Authorize(Roles = MarketplaceRoles.Supplier)]
    [HttpPut("suppliers/{supplierId:guid}/delivery-settings")]
    public async Task<IActionResult> SaveDeliverySettings(Guid supplierId, SupplierDeliveryRequest request, CancellationToken cancellationToken)
    {
        if (!Owns(supplierId)) return NotFound();
        if (request.NormalDeliveryRadiusKm is < 1 or > 1000)
            return ValidationProblem(ProblemErrors("normalDeliveryRadiusKm", "Raza normală trebuie să fie între 1 și 1.000 km."));
        if (request.MaximumDeliveryRadiusKm is < 1 or > 1000)
            return ValidationProblem(ProblemErrors("maximumDeliveryRadiusKm", "Raza maximă trebuie să fie între 1 și 1.000 km."));
        if (request.MaximumDeliveryRadiusKm < request.NormalDeliveryRadiusKm)
            return ValidationProblem(ProblemErrors("maximumDeliveryRadiusKm", "Raza maximă trebuie să fie cel puțin egală cu raza normală."));
        if (request.MinimumOrderValue < 0)
            return ValidationProblem(ProblemErrors("minimumOrderValue", "Valoarea minimă a comenzii nu poate fi negativă."));
        string[] counties = request.Counties?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (!request.NationwideDelivery && counties.Length == 0)
            return ValidationProblem(ProblemErrors("counties", "Selectați cel puțin un județ sau livrarea națională."));
        if (counties.Any(county => !RomanianCounties.All.Contains(county)))
            return ValidationProblem(ProblemErrors("counties", "Lista conține un județ invalid."));
        SupplierDeliverySelection? selection = await settingsService.SaveDeliverySettingsAsync(
            supplierId,
            new(request.NormalDeliveryRadiusKm, request.MaximumDeliveryRadiusKm, request.MinimumOrderValue, request.NationwideDelivery, counties),
            cancellationToken);
        return selection is null ? MissingProfile() : Ok(selection);
    }

    private bool Owns(Guid id)
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid accountId) && accountId == id;
    }

    private NotFoundObjectResult MissingProfile() => NotFound(Problem(title: "Completați mai întâi profilul companiei."));
    private static ValidationProblemDetails ProblemErrors(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] }) { Title = "Setările nu au putut fi salvate." };
}

public sealed record CategoryResponse(Guid Id, Guid? ParentId, string Name, string Slug);
public sealed record AddCategoryRequest(string? Name);
public sealed record SupplierCategoriesRequest(IReadOnlyCollection<Guid>? CategoryIds);
public sealed record SupplierDeliveryRequest(
    int NormalDeliveryRadiusKm,
    int MaximumDeliveryRadiusKm,
    decimal? MinimumOrderValue,
    bool NationwideDelivery,
    IReadOnlyCollection<string>? Counties);
