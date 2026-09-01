using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Marketplace.Application.Companies;
using Marketplace.Domain.Profiles;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class CompanyMembershipFoundationTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory factory;

    public CompanyMembershipFoundationTests(MarketplaceWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task MembershipFoundationBackfillsNewLegacySupplierAndAuthorizesOnlyActiveRepresentatives()
    {
        const string password = "password";
        string supplierEmail = $"company-owner-{Guid.NewGuid():N}@example.test";
        Guid ownerId = await RegisterAndGetId(supplierEmail, password, "/api/v1/auth/register/supplier");
        using HttpClient ownerClient = await AuthenticatedClient(supplierEmail, password);
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.PutAsJsonAsync($"/api/v1/suppliers/{ownerId}", SupplierProfileRequest(supplierEmail))).StatusCode);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            ICompanyAuthorizationService authorization = scope.ServiceProvider.GetRequiredService<ICompanyAuthorizationService>();
            SupplierProfile before = await db.SupplierProfiles.AsNoTracking().SingleAsync(profile => profile.Id == ownerId);
            CompanyMembership ownerMembership = await db.CompanyMemberships.SingleAsync(item => item.CompanyId == ownerId && item.UserId == ownerId);
            Assert.Equal(CompanyMembershipRole.Owner, ownerMembership.Role);
            Assert.True(ownerMembership.IsActive);
            Assert.True(await authorization.CanRepresentCompanyAsync(ownerId, ownerId, CancellationToken.None));

            string buyerEmail = $"company-buyer-{Guid.NewGuid():N}@example.test";
            Guid buyerId = await RegisterAndGetId(buyerEmail, password, "/api/v1/auth/register");
            Assert.False(await db.CompanyMemberships.AnyAsync(item => item.UserId == buyerId));
            Assert.False(await authorization.CanRepresentCompanyAsync(buyerId, ownerId, CancellationToken.None));

            string representativeEmail = $"company-representative-{Guid.NewGuid():N}@example.test";
            Guid representativeId = await RegisterAndGetId(representativeEmail, password, "/api/v1/auth/register");
            CompanyMembership representative = CompanyMembership.Create(representativeId, ownerId, CompanyMembershipRole.Representative, DateTimeOffset.UtcNow);
            db.CompanyMemberships.Add(representative);
            await db.SaveChangesAsync();
            Assert.True(await authorization.CanRepresentCompanyAsync(representativeId, ownerId, CancellationToken.None));

            db.ChangeTracker.Clear();
            db.CompanyMemberships.Add(CompanyMembership.Create(representativeId, ownerId, CompanyMembershipRole.Representative, DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            CompanyMembership persistedRepresentative = await db.CompanyMemberships.SingleAsync(item => item.CompanyId == ownerId && item.UserId == representativeId);
            persistedRepresentative.Revoke(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
            Assert.False(await authorization.CanRepresentCompanyAsync(representativeId, ownerId, CancellationToken.None));

            SupplierProfile after = await db.SupplierProfiles.AsNoTracking().SingleAsync(profile => profile.Id == ownerId);
            Assert.Equal(before.TradingName, after.TradingName);
            Assert.Equal(before.CompanyRegistrationNumber, after.CompanyRegistrationNumber);
            Assert.Equal(before.ContactEmail, after.ContactEmail);
            Assert.Equal(before.CreatedAt, after.CreatedAt);
        }
    }

    [Fact]
    public async Task ConcurrentFirstTimeCompanyRegistrationDoesNotFailWithADuplicateMembership()
    {
        const string password = "password";
        string email = $"legacy-owner-{Guid.NewGuid():N}@example.test";
        Guid ownerId = await RegisterAndGetId(email, password, "/api/v1/auth/register/supplier");
        using HttpClient ownerClient = await AuthenticatedClient(email, password);

        // Two simultaneous submissions of the same first-time registration (e.g. a double-click):
        // both see the profile as absent and race to create the shared-key company + Owner membership.
        Task<HttpResponseMessage>[] submissions = Enumerable.Range(0, 4)
            .Select(_ => ownerClient.PutAsJsonAsync($"/api/v1/suppliers/{ownerId}", SupplierProfileRequest(email)))
            .ToArray();
        HttpResponseMessage[] responses = await Task.WhenAll(submissions);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext verify = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(1, await verify.SupplierProfiles.CountAsync(profile => profile.Id == ownerId));
        Assert.Equal(1, await verify.CompanyMemberships.CountAsync(item => item.CompanyId == ownerId && item.UserId == ownerId));
    }

    private async Task<Guid> RegisterAndGetId(string email, string password, string route)
    {
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        object request = route.EndsWith("/register/supplier", StringComparison.Ordinal)
            ? new { email, password }
            : new { email, password };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(route, request)).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>())!;
        return UserId(token.AccessToken);
    }

    private async Task<HttpClient> AuthenticatedClient(string email, string password)
    {
        HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    private static object SupplierProfileRequest(string email) => new
    {
        tradingName = "Companie membership test", companyRegistrationNumber = $"J00/{Guid.NewGuid():N}", taxIdentifier = "RO123",
        contactPerson = "Owner test", contactEmail = email, phoneNumber = "0700000000", website = (string?)null,
        address = "Strada Test 1", city = "Iași", county = "Iași", latitude = (decimal?)null,
        longitude = (decimal?)null, description = "Profil pentru verificarea membership-ului"
    };

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(System.Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
}
