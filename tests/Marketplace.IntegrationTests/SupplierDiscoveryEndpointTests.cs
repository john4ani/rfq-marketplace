using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Marketplace.Domain.Identity;
using Marketplace.Infrastructure.Identity;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class SupplierDiscoveryEndpointTests : IClassFixture<MarketplaceWebApplicationFactory>
{
    private static readonly string[] BuyerOnly = ["Buyer"];
    private static readonly string[] SupplierOnly = ["Supplier"];
    private readonly MarketplaceWebApplicationFactory _factory;

    public SupplierDiscoveryEndpointTests(MarketplaceWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task AdministratorCanValidateCandidateAndSupplierCanClaimRequestInvitation()
    {
        using HttpClient publicClient = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await publicClient.GetAsync("/api/v1/admin/supplier-candidates")).StatusCode);

        string adminEmail = $"discovery-admin-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await publicClient.PostAsJsonAsync("/api/v1/auth/register",
            new { email = adminEmail, password, accountTypes = BuyerOnly })).StatusCode);
        await GrantAdministratorRole(adminEmail);
        using HttpClient adminClient = await AuthenticatedClient(adminEmail, password);
        Guid adminId = await UserId(adminEmail);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync($"/api/v1/buyers/{adminId}", new
        {
            displayName = "Administrator Discovery",
            buyerType = "Company",
            companyName = "CompaSpot Operations",
            taxIdentifier = "ROADMIN",
            phoneNumber = "0700000000",
            defaultCity = "Marginea",
            defaultCounty = "Suceava"
        })).StatusCode);

        CategoryResponse[] categories = (await adminClient.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid constructionRootId = categories.Single(category => category.Slug == "constructii").Id;
        Guid categoryId = categories.First(category => category.ParentId == constructionRootId).Id;
        string customCategoryName = $"Categorie centrală {Guid.NewGuid():N}";
        CategoryCreationResponse customCategory = (await (await adminClient.PostAsJsonAsync(
            "/api/v1/categories", new { name = customCategoryName })).Content.ReadFromJsonAsync<CategoryCreationResponse>())!;
        Assert.False(customCategory.AlreadyExisted);
        CategoryCreationResponse existingCategory = (await (await adminClient.PostAsJsonAsync(
            "/api/v1/categories", new { name = customCategoryName })).Content.ReadFromJsonAsync<CategoryCreationResponse>())!;
        Assert.True(existingCategory.AlreadyExisted);
        HttpResponseMessage similarCategory = await adminClient.PostAsJsonAsync(
            "/api/v1/categories", new { name = $"{customCategoryName}e" });
        Assert.Equal(HttpStatusCode.Conflict, similarCategory.StatusCode);
        ProjectTemplateResponse template = (await (await adminClient.PostAsJsonAsync(
            "/api/v1/project-templates", new {
                name = $"Proiect test {Guid.NewGuid():N}", description = "Șablon editabil", categoryIds = new[] { categoryId }
            })).Content.ReadFromJsonAsync<ProjectTemplateResponse>())!;
        Assert.Single(template.CategoryIds);
        Assert.Contains((await adminClient.GetFromJsonAsync<ProjectTemplateResponse[]>("/api/v1/project-templates"))!, item => item.Id == template.Id);
        HttpResponseMessage rfqCreated = await adminClient.PostAsJsonAsync("/api/v1/rfqs", new
        {
            title = "Cerere reală pentru furnizor descoperit",
            projectName = (string?)null,
            description = "O oportunitate comercială asociată unei invitații controlate.",
            primaryCategoryId = categoryId,
            deliveryAddress = "Adresă privată 14",
            deliveryLocality = "Marginea",
            county = "Suceava",
            supplierSearchRadiusKm = 30,
            latitude = (decimal?)null,
            longitude = (decimal?)null,
            preferredDeliveryDate = (DateOnly?)null,
            quotationDeadline = DateTimeOffset.UtcNow.AddDays(7),
            alternativeProductsAccepted = true,
            deliveryRequired = false,
            estimatedBudget = 2500m,
            buyerNotes = (string?)null,
            items = new[]
            {
                new
                {
                    productName = "Produs local",
                    manufacturerOrBrand = (string?)null,
                    specification = "Specificație test",
                    quantity = 10m,
                    unit = "Piece",
                    alternativesAccepted = true,
                    buyerNotes = (string?)null,
                    categoryId
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, rfqCreated.StatusCode);
        RfqResponse rfq = (await rfqCreated.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal(HttpStatusCode.OK,
            (await adminClient.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/publish", new { })).StatusCode);

        DiscoveryOpportunityResponse[] opportunities = (await adminClient.GetFromJsonAsync<DiscoveryOpportunityResponse[]>(
            "/api/v1/admin/discovery-opportunities"))!;
        DiscoveryOpportunityResponse opportunity = Assert.Single(opportunities, item => item.Id == rfq.Id);
        Assert.Contains(categoryId, opportunity.RequirementCategoryIds);

        string candidateCompanyName = $"Furnizor Descoperit {Guid.NewGuid():N} SRL";
        object candidateRequest = new
        {
            companyName = candidateCompanyName,
            contactEmail = "contact@descoperit.example",
            phoneNumber = "0711111111",
            website = "https://descoperit.example",
            city = "Marginea",
            county = "Suceava",
            categoryIds = new[] { categoryId },
            source = "Site-ul companiei verificat manual",
            notes = "Confirmă persoana de contact înainte de trimitere."
        };
        HttpResponseMessage candidateCreated = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/supplier-candidates", candidateRequest);
        Assert.Equal(HttpStatusCode.Created, candidateCreated.StatusCode);
        CandidateResponse candidate = (await candidateCreated.Content.ReadFromJsonAsync<CandidateResponse>())!;
        Assert.Equal("Unverified", candidate.Status);
        Assert.Contains(candidate.Categories, category => category.Id == categoryId);

        Assert.Equal(HttpStatusCode.Conflict,
            (await adminClient.PostAsJsonAsync("/api/v1/admin/supplier-candidates", candidateRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await adminClient.PostAsJsonAsync($"/api/v1/admin/supplier-candidates/{candidate.Id}/invitations",
                new { rfqId = rfq.Id })).StatusCode);

        DiscoveryNeedReviewResponse needReview = (await adminClient.GetFromJsonAsync<DiscoveryNeedReviewResponse>(
            $"/api/v1/admin/supplier-discovery/needs/{rfq.Id}"))!;
        OpportunityMatchResponse opportunityMatch = Assert.Single(needReview.Candidates, item => item.CandidateId == candidate.Id);
        Assert.Equal("PendingReview", opportunityMatch.Status);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
            $"/api/v1/admin/supplier-discovery/opportunity-matches/{opportunityMatch.Id}/review",
            new { approved = true, note = "Relevantă pentru această oportunitate concretă." })).StatusCode);

        HttpResponseMessage reviewed = await adminClient.PutAsJsonAsync(
            $"/api/v1/admin/supplier-candidates/{candidate.Id}/review",
            new { approved = true, notes = "Categoria, zona și activitatea publică sunt relevante." });
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        Assert.Equal("EligibleForInvitation",
            (await reviewed.Content.ReadFromJsonAsync<CandidateResponse>())!.Status);

        HttpResponseMessage invitationCreated = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/supplier-candidates/{candidate.Id}/invitations",
            new { rfqId = rfq.Id });
        Assert.Equal(HttpStatusCode.Created, invitationCreated.StatusCode);
        InvitationCreatedResponse invitation =
            (await invitationCreated.Content.ReadFromJsonAsync<InvitationCreatedResponse>())!;
        Assert.NotEmpty(invitation.Token);
        Assert.DoesNotContain("=", invitation.Token, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict,
            (await adminClient.PostAsJsonAsync(
                $"/api/v1/admin/supplier-candidates/{candidate.Id}/invitations",
                new { rfqId = rfq.Id })).StatusCode);

        InvitationResponse publicInvitation = (await publicClient.GetFromJsonAsync<InvitationResponse>(
            $"/api/v1/supplier-invitations/{invitation.Token}"))!;
        Assert.Equal("Active", publicInvitation.Status);
        Assert.Contains("neverificate", publicInvitation.CandidateLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Marginea", publicInvitation.DeliveryLocality);

        string supplierEmail = $"discovered-supplier-{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Created, (await publicClient.PostAsJsonAsync("/api/v1/auth/register",
            new { email = supplierEmail, password, accountTypes = SupplierOnly })).StatusCode);
        using HttpClient supplierClient = await AuthenticatedClient(supplierEmail, password);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await supplierClient.PostAsJsonAsync($"/api/v1/supplier-invitations/{invitation.Token}/claim", new { })).StatusCode);
        Guid supplierId = await UserId(supplierEmail);
        Assert.Equal(HttpStatusCode.OK, (await supplierClient.PutAsJsonAsync($"/api/v1/suppliers/{supplierId}", new
        {
            tradingName = candidateCompanyName,
            companyRegistrationNumber = "J33/1/2026",
            taxIdentifier = "RODISCOVERY",
            contactPerson = "Persoană Contact",
            contactEmail = supplierEmail,
            phoneNumber = "0711111111",
            website = "https://descoperit.example",
            address = "Strada Furnizorului 1",
            city = "Marginea",
            county = "Suceava",
            latitude = (decimal?)null,
            longitude = (decimal?)null,
            description = "Furnizor local"
        })).StatusCode);

        HttpResponseMessage claimedResponse = await supplierClient.PostAsJsonAsync(
            $"/api/v1/supplier-invitations/{invitation.Token}/claim", new { });
        Assert.Equal(HttpStatusCode.OK, claimedResponse.StatusCode);
        Assert.True((await claimedResponse.Content.ReadFromJsonAsync<InvitationResponse>())!.Claimed);
        Assert.Equal(HttpStatusCode.OK,
            (await supplierClient.PostAsJsonAsync($"/api/v1/supplier-invitations/{invitation.Token}/claim", new { })).StatusCode);
        SupplierCategoryResponse supplierCategories = (await supplierClient.GetFromJsonAsync<SupplierCategoryResponse>(
            $"/api/v1/suppliers/{supplierId}/categories"))!;
        Assert.Contains(categoryId, supplierCategories.CategoryIds);

        CandidateResponse[] candidates =
            (await adminClient.GetFromJsonAsync<CandidateResponse[]>("/api/v1/admin/supplier-candidates"))!;
        CandidateResponse claimed = Assert.Single(candidates, item => item.Id == candidate.Id);
        Assert.Equal("Claimed", claimed.Status);
        Assert.Equal(supplierId, claimed.ClaimedBySupplierId);
        Assert.Equal("Claimed", Assert.Single(claimed.Invitations).Status);
        await Cleanup(adminEmail, supplierEmail, candidateCompanyName, template.Id, customCategory.Id);
    }

    private async Task<HttpClient> AuthenticatedClient(string email, string password)
    {
        HttpClient client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        TokenResponse login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    private async Task GrantAdministratorRole(string email)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = (await users.FindByEmailAsync(email))!;
        IdentityResult result = await users.AddToRoleAsync(user, MarketplaceRoles.Administrator);
        Assert.True(result.Succeeded, string.Join(' ', result.Errors.Select(error => error.Description)));
    }

    private async Task<Guid> UserId(string email)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await users.FindByEmailAsync(email))!.Id;
    }

    private async Task Cleanup(string adminEmail, string supplierEmail, string candidateCompanyName, Guid templateId, Guid customCategoryId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser? administrator = await users.FindByEmailAsync(adminEmail);
        ApplicationUser? supplier = await users.FindByEmailAsync(supplierEmail);
        Guid[] candidateIds = await db.SupplierCandidates
            .Where(candidate => candidate.CompanyName == candidateCompanyName)
            .Select(candidate => candidate.Id)
            .ToArrayAsync();
        await db.OpportunityMatches.Where(item => item.SupplierCandidateId != null && candidateIds.Contains(item.SupplierCandidateId.Value)).ExecuteDeleteAsync();
        await db.SupplierDiscoveryAuditEvents.Where(item => candidateIds.Contains(item.CandidateId)).ExecuteDeleteAsync();
        await db.SupplierInvitations.Where(item => candidateIds.Contains(item.CandidateId)).ExecuteDeleteAsync();
        await db.SupplierCandidates.Where(item => candidateIds.Contains(item.Id)).ExecuteDeleteAsync();
        await db.ProjectTemplates.Where(item => item.Id == templateId).ExecuteDeleteAsync();
        await db.Categories.Where(item => item.Id == customCategoryId).ExecuteDeleteAsync();
        if (administrator is not null)
        {
            Guid[] administratorRfqIds = await db.Rfqs.Where(rfq => rfq.BuyerId == administrator.Id).Select(rfq => rfq.Id).ToArrayAsync();
            await db.OpportunityMatches.Where(item => administratorRfqIds.Contains(item.RfqId)).ExecuteDeleteAsync();
            await db.Rfqs.Where(rfq => rfq.BuyerId == administrator.Id).ExecuteDeleteAsync();
            await db.BuyerProfiles.Where(profile => profile.Id == administrator.Id).ExecuteDeleteAsync();
            await users.DeleteAsync(administrator);
        }
        if (supplier is not null)
        {
            await db.SupplierProfiles.Where(profile => profile.Id == supplier.Id).ExecuteDeleteAsync();
            await users.DeleteAsync(supplier);
        }
    }

    private sealed record TokenResponse(string AccessToken);
    private sealed record CategoryResponse(Guid Id, Guid? ParentId, string Slug);
    private sealed record CategoryCreationResponse(Guid Id, bool AlreadyExisted);
    private sealed record ProjectTemplateResponse(Guid Id, Guid[] CategoryIds);
    private sealed record RfqResponse(Guid Id);
    private sealed record DiscoveryOpportunityResponse(Guid Id, Guid[] RequirementCategoryIds);
    private sealed record DiscoveryNeedReviewResponse(OpportunityMatchResponse[] Candidates);
    private sealed record OpportunityMatchResponse(Guid Id, Guid? CandidateId, string Status);
    private sealed record CandidateResponse(
        Guid Id,
        string Status,
        Guid? ClaimedBySupplierId,
        CandidateCategoryResponse[] Categories,
        InvitationSummaryResponse[] Invitations);
    private sealed record CandidateCategoryResponse(Guid Id, string Name);
    private sealed record SupplierCategoryResponse(Guid[] CategoryIds);
    private sealed record InvitationSummaryResponse(string Status);
    private sealed record InvitationCreatedResponse(Guid InvitationId, string Token, DateTimeOffset ExpiresAt);
    private sealed record InvitationResponse(
        string CandidateLabel,
        string Status,
        string DeliveryLocality,
        bool Claimed);
}
