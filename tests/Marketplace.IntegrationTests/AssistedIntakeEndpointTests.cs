using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Marketplace.Application.AiIntake;
using Marketplace.Domain.Rfqs;
using Marketplace.Infrastructure.AiIntake;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Marketplace.IntegrationTests;

public sealed class AssistedIntakeEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public AssistedIntakeEndpointTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public async Task AiFlowClarifiesResearchesAndCreatesDraftOnlyAfterHumanApproval()
    {
        using WebApplicationFactory<Program> aiFactory = AiFactory();
        using HttpClient client = aiFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await AuthenticateBuyerAsync(client);

        HttpResponseMessage started = await client.PostAsJsonAsync("/api/v1/assisted-intakes",
            new { input = "Am nevoie de 3 uși din lemn pentru garaj și șură." });
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        IntakeResponse intake = (await started.Content.ReadFromJsonAsync<IntakeResponse>())!;
        Assert.Equal("OpenAi", intake.Mode);
        Assert.Equal("InProgress", intake.Status);
        Assert.NotNull(intake.Clarification);

        // Reading or reopening an existing draft must not consume the quota reserved for AI calls.
        for (int attempt = 0; attempt < 25; attempt++)
            Assert.Equal(HttpStatusCode.OK,
                (await client.GetAsync($"/api/v1/assisted-intakes/{intake.SessionId}")).StatusCode);

        IntakeResponse clarified = (await (await client.PostAsJsonAsync(
            $"/api/v1/assisted-intakes/{intake.SessionId}/clarifications",
            new { answer = "Garaj 250x220 cm; intrare și ieșire șură 180x210 cm." }))
            .Content.ReadFromJsonAsync<IntakeResponse>())!;
        Assert.Equal("ReadyForApproval", clarified.Status);
        Assert.Null(clarified.Clarification);
        Assert.Equal(3, clarified.Draft.Lines.Length);
        Assert.Equal("https://example.test/wooden-doors", Assert.Single(clarified.ResearchSources).Url);
        Assert.Single(clarified.Answers);

        HttpResponseMessage approved = await client.PostAsJsonAsync(
            $"/api/v1/assisted-intakes/{intake.SessionId}/approve", new { draft = DraftFrom(clarified) });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        RfqResponse rfq = (await approved.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal("Draft", rfq.Status);
        Assert.Equal("Assisted", rfq.Source);
        Assert.Contains("Garaj 250x220 cm", rfq.BuyerNotes);
    }

    [Fact]
    public async Task MissingAiConfigurationReturnsServiceUnavailableInsteadOfPseudoAiResult()
    {
        using WebApplicationFactory<Program> unavailableFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AiIntake:ApiKey"] = string.Empty })));
        using HttpClient client = unavailableFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await AuthenticateBuyerAsync(client);
        AvailabilityResponse availability = (await (await client.GetAsync("/api/v1/assisted-intakes/availability"))
            .Content.ReadFromJsonAsync<AvailabilityResponse>())!;
        Assert.False(availability.ModelAvailable);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/assisted-intakes",
            new { input = "Am nevoie de o cerere suficient de detaliată." });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task AdvancedFormCanCompleteAnInProgressAiSession()
    {
        using WebApplicationFactory<Program> aiFactory = AiFactory();
        using HttpClient client = aiFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await AuthenticateBuyerAsync(client);
        IntakeResponse intake = (await (await client.PostAsJsonAsync("/api/v1/assisted-intakes",
            new { input = "Am nevoie de 3 uși din lemn pentru garaj și șură." }))
            .Content.ReadFromJsonAsync<IntakeResponse>())!;

        HttpResponseMessage approved = await client.PostAsJsonAsync(
            $"/api/v1/assisted-intakes/{intake.SessionId}/approve",
            new { draft = DraftFrom(intake), completedInAdvancedForm = true });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
    }

    private WebApplicationFactory<Program> AiFactory() => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<INeedExtractionProvider>();
            services.AddScoped<INeedExtractionProvider, FakeNeedExtractionProvider>();
        }));

    private static object DraftFrom(IntakeResponse intake) => new
    {
        source = "Manual", inputType = "StructuredForm", title = intake.Draft.Title,
        projectName = intake.Draft.ProjectName, description = intake.Draft.Description,
        primaryCategoryId = intake.Draft.PrimaryCategoryId, deliveryAddress = "Strada Test 10",
        deliveryLocality = intake.Draft.DeliveryLocality, county = intake.Draft.County,
        supplierSearchRadiusKm = intake.Draft.SupplierSearchRadiusKm, latitude = (decimal?)null, longitude = (decimal?)null,
        preferredDeliveryDate = (DateOnly?)null, quotationDeadline = DateTimeOffset.UtcNow.AddDays(2),
        alternativeProductsAccepted = true, estimatedBudget = (decimal?)null, buyerNotes = (string?)null,
        allowSplit = false, deliveryRequired = true,
        items = intake.Draft.Lines.Select(line => new { kind = line.Kind, productName = line.Name,
            manufacturerOrBrand = (string?)null, specification = line.Specification, quantity = line.Quantity ?? 1,
            unit = line.Unit ?? "Piece", alternativesAccepted = true, buyerNotes = (string?)null, categoryId = line.CategoryId })
    };

    private static async Task AuthenticateBuyerAsync(HttpClient client)
    {
        string email = $"assisted-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/buyer", new { email, password })).StatusCode);
        TokenResponse login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid buyerId = UserId(login.AccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/buyers/{buyerId}", new
        {
            displayName = "Cumpărător asistat", buyerType = "Individual", companyName = (string?)null,
            taxIdentifier = (string?)null, phoneNumber = "0700000000", defaultCity = "Iași", defaultCounty = "Iași"
        })).StatusCode);
    }

    private static Guid UserId(string token)
    {
        string part = token.Split('.')[1];
        string json = Encoding.UTF8.GetString(Convert.FromBase64String(
            part.PadRight(part.Length + (4 - part.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(json).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed class FakeNeedExtractionProvider : INeedExtractionProvider
    {
        public bool IsAvailable => true;
        public Task<NeedExtractionOutcome> ExtractAsync(NeedExtractionContext context, CancellationToken cancellationToken)
        {
            Guid? categoryId = context.Categories.FirstOrDefault()?.Id;
            bool clarified = context.Turns.Count > 0;
            AssistedRequirementLine[] lines = clarified
                ? [new(RequirementKind.Product, "Ușă garaj din lemn", "250x220 cm; de verificat cu furnizorul", 1, UnitOfMeasure.Piece, true, categoryId),
                   new(RequirementKind.Product, "Ușă intrare șură din lemn", "180x210 cm; de verificat cu furnizorul", 1, UnitOfMeasure.Piece, true, categoryId),
                   new(RequirementKind.Product, "Ușă ieșire șură din lemn", "180x210 cm; de verificat cu furnizorul", 1, UnitOfMeasure.Piece, true, categoryId)]
                : [new(RequirementKind.Product, "Uși din lemn", null, 3, UnitOfMeasure.Piece, true, categoryId)];
            var draft = new AssistedNeedDraft(ProcurementInputType.ProjectDescription, "Uși din lemn pentru garaj și șură",
                "Garaj și șură", context.InitialInput, categoryId, string.Empty, context.BuyerCity, context.BuyerCounty,
                context.BuyerRadiusKm, null, context.Now.AddDays(2), true, false, true, null, null, lines);
            AssistedClarification? question = clarified ? null : new(ClarificationField.Specification,
                "Care sunt dimensiunile fiecărei uși?", []);
            AssistedResearchSource[] sources = clarified
                ? [new("Ghid pentru uși din lemn", "https://example.test/wooden-doors", "Componentele se confirmă după dimensiuni și utilizare.")]
                : [];
            return Task.FromResult(new NeedExtractionOutcome(AssistedIntakeMode.OpenAi, "TestAI", "test-model", "test-v1",
                draft, [], clarified ? ["Feroneria și structura se confirmă cu furnizorul."] : [], sources, question, 100, 50));
        }
    }

    private sealed record TokenResponse(string AccessToken);
    private sealed record AvailabilityResponse(bool Enabled, bool ModelAvailable);
    private sealed record IntakeResponse(Guid SessionId, string InitialInput, string Status, string Mode, IntakeDraft Draft,
        Clarification? Clarification, IntakeAnswer[] Answers, ResearchSource[] ResearchSources);
    private sealed record Clarification(string Field, string Question);
    private sealed record IntakeAnswer(string Field, string Question, string Answer);
    private sealed record ResearchSource(string Title, string Url, string Finding);
    private sealed record IntakeDraft(string Title, string? ProjectName, string? Description, Guid? PrimaryCategoryId,
        string DeliveryAddress, string DeliveryLocality, string County, int SupplierSearchRadiusKm, IntakeLine[] Lines);
    private sealed record IntakeLine(string Kind, string Name, string? Specification, decimal? Quantity, string? Unit, Guid? CategoryId);
    private sealed record RfqResponse(Guid Id, string Status, string Source, string InputType, string? BuyerNotes);
}
