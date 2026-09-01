using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Seap;
using Marketplace.Domain.Vault;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Embeddings;
using Marketplace.Infrastructure.Identity;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Marketplace.IntegrationTests;

/// <summary>
/// Multi-Document Context Assembly: the SEAP auto-bid generation must use ALL of the requesting
/// company's CompanyVault documents, and only those — never another company's documents — with a
/// re-uploaded file superseding its previous version (newest <c>UploadedAt</c> wins). Requirements of the
/// tender that have no coverage in any vault document must produce a visible „[Document lipsă: …]"
/// placeholder instead of invented data. The vault documents are seeded directly in Postgres (the real
/// domain factories) with fixed embeddings so the pgvector retrieval is deterministic; the OCR provider is
/// not involved in this flow.
/// </summary>
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapBidMultiDocumentContextTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private const int EmbeddingDimensions = 1536;
    private static readonly float[] FixedEmbedding = Enumerable.Repeat(0.5f, EmbeddingDimensions).ToArray();

    private readonly MarketplaceWebApplicationFactory factory;
    public SeapBidMultiDocumentContextTests(MarketplaceWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task AutoBidAssemblesContextFromAllThreeVaultDocumentsAndMarksUncoveredRequirements()
    {
        ContextEchoLlm echo = new();
        using WebApplicationFactory<Program> bidFactory = NewBidFactory(echo);
        using HttpClient client = bidFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        Guid companyId = await RegisterCompanyAsync(client);
        await SeedVaultAsync(bidFactory, companyId);
        Guid tenderId = await SeedTenderAsync();

        HttpResponseMessage autoBid = await client.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/auto-bid",
            new { companyId });
        Assert.Equal(HttpStatusCode.OK, autoBid.StatusCode);

        // The SEAP background worker also calls the LLM (summary/categorization) on its own schedule, so
        // select the auto-bid call deterministically by its characteristic prompt marker.
        LlmCall bidCall = echo.Calls.Single(call => call.User.Contains("Cerințele anunțului:", StringComparison.Ordinal));

        // 1. The LLM prompt must combine the content of ALL THREE documents of the company.
        Assert.Contains("RO99999999", bidCall.User);      // Certificat_ONRC (newest version)
        Assert.Contains("Mihai Popescu", bidCall.User);   // administrator from Certificat_ONRC
        Assert.Contains("Putere nominală", bidCall.User); // Fisa_Tehnica_Produs
        Assert.Contains("Garanție: 24 luni", bidCall.User); // Fisa_Tehnica_Produs
        Assert.Contains("Primăria Udești", bidCall.User); // Experienta_Similara

        // 2. Versioning: the OLD Certificat_ONRC version must NOT be part of the context.
        Assert.DoesNotContain("RO12345678", bidCall.User);

        // 3. Company isolation: another company's vault document must never leak into the prompt.
        Assert.DoesNotContain("INTRUDER", bidCall.User);

        // 4. The non-hallucination rule for uncovered requirements must be in the system prompt.
        Assert.Contains("[Document lipsă:", bidCall.System);

        // 5. The generated .docx must carry the real combined data AND the visible placeholder for the
        //    uncovered requirement („Autorizație de mediu") instead of invented data.
        byte[] docx = await DownloadAsync(client, tenderId, "TechnicalProposal");
        string docText = ExtractDocxText(docx);
        Assert.Contains("RO99999999", docText);
        Assert.Contains("24 luni", docText);
        Assert.Contains("Primăria Udești", docText);
        Assert.Contains("[Document lipsă: Autorizație de mediu]", docText);
    }

    private WebApplicationFactory<Program> NewBidFactory(ContextEchoLlm echo) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Features:AutoBiddingEnabled"] = "true" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILlmProvider>();
                services.AddSingleton<ILlmProvider>(echo);
                // Deterministic embeddings so the pgvector RAG returns the seeded chunks predictably.
                services.RemoveAll<IEmbeddingService>();
                services.AddSingleton<IEmbeddingService>(new FixedEmbeddingService());
            });
        });

    private static async Task<Guid> RegisterCompanyAsync(HttpClient client)
    {
        string email = $"seap-multidoc-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid companyId = UserId(token.AccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync($"/api/v1/suppliers/{companyId}", SupplierProfileRequest(email))).StatusCode);
        return companyId;
    }

    /// <summary>Seeds the company's vault (3 documents + a re-uploaded certificate version) and an
    /// intruder company's vault directly in Postgres with deterministic embeddings.</summary>
    private static async Task SeedVaultAsync(WebApplicationFactory<Program> bidFactory, Guid companyId)
    {
        using IServiceScope scope = bidFactory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompanyVault vault = CompanyVault.Create(companyId, now);
        db.CompanyVaults.Add(vault);

        // a) Certificat_ONRC.pdf — V1, superseded by the re-upload below.
        VaultDocument certificateV1 = vault.AddDocument("Certificat_ONRC.pdf",
            "local://vault/compaspot/certificat-v1.pdf", VaultDocumentType.Certificate, companyId, now.AddHours(-2));
        certificateV1.SetExtractedMarkdown("CUI RO12345678; administrator: Mihai Popescu; sediu: Str. Școlii nr. 5, Cluj-Napoca");
        certificateV1.MarkExtracted(1);
        certificateV1.AddChunk(0,
            "CUI RO12345678; administrator: Mihai Popescu; sediu: Str. Școlii nr. 5, Cluj-Napoca (versiune veche)",
            FixedEmbedding, now.AddHours(-2));

        // a) Certificat_ONRC.pdf — V2 (latest UploadedAt): the context-retriever must pick this one.
        VaultDocument certificateV2 = vault.AddDocument("Certificat_ONRC.pdf",
            "local://vault/compaspot/certificat-v2.pdf", VaultDocumentType.Certificate, companyId, now);
        certificateV2.SetExtractedMarkdown("CUI RO99999999; administrator: Mihai Popescu; sediu: Str. Școlii nr. 5, Cluj-Napoca (versiune actualizată)");
        certificateV2.MarkExtracted(1);
        certificateV2.AddChunk(0,
            "CUI RO99999999; administrator: Mihai Popescu; sediu: Str. Școlii nr. 5, Cluj-Napoca (versiune actualizată)",
            FixedEmbedding, now);

        // b) Fisa_Tehnica_Produs.pdf — technical specifications.
        VaultDocument technicalSheet = vault.AddDocument("Fisa_Tehnica_Produs.pdf",
            "local://vault/compaspot/fisa-tehnica.pdf", VaultDocumentType.Other, companyId, now);
        technicalSheet.SetExtractedMarkdown("Putere nominală: 10 kW; Dimensiuni: 120 x 60 x 5 cm; Garanție: 24 luni");
        technicalSheet.MarkExtracted(1);
        technicalSheet.AddChunk(0, "Putere nominală: 10 kW; Dimensiuni: 120 x 60 x 5 cm; Garanție: 24 luni", FixedEmbedding, now);

        // c) Experienta_Similara.pdf — previous similar contracts.
        VaultDocument experience = vault.AddDocument("Experienta_Similara.pdf",
            "local://vault/compaspot/experienta.pdf", VaultDocumentType.Other, companyId, now);
        experience.SetExtractedMarkdown("Contracte similare: Primăria Udești — Reabilitare drum (2024), valoare 100.000 RON, CPV 45233120-6");
        experience.MarkExtracted(1);
        experience.AddChunk(0, "Contracte similare: Primăria Udești — Reabilitare drum (2024), valoare 100.000 RON, CPV 45233120-6", FixedEmbedding, now);

        // Intruder company's vault — must never appear in the first company's RAG context.
        Guid intruderId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = intruderId,
            UserName = $"intruder-{intruderId:N}", NormalizedUserName = $"INTRUDER-{intruderId:N}",
            Email = $"intruder-{intruderId:N}@test.local", NormalizedEmail = $"INTRUDER-{intruderId:N}@TEST.LOCAL",
            EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString()
        });
        db.SupplierProfiles.Add(SupplierProfile.Create(intruderId, "Firma Concurenta SRL", "J40/999/2026", "ROINTRUDER",
            "Concurent", $"intruder-{intruderId:N}@test.local", "0700000000", null,
            new GeographicLocation("Strada 1", "Cluj-Napoca", "Cluj", "România", 47.7m, 25.8m), 50, null, now));
        CompanyVault intruderVault = CompanyVault.Create(intruderId, now);
        db.CompanyVaults.Add(intruderVault);
        VaultDocument intruderDocument = intruderVault.AddDocument("Certificat_ONRC.pdf",
            "local://vault/other/certificat.pdf", VaultDocumentType.Certificate, intruderId, now);
        intruderDocument.SetExtractedMarkdown("CUI INTRUDER-0000; firma concurentă");
        intruderDocument.MarkExtracted(1);
        intruderDocument.AddChunk(0, "CUI INTRUDER-0000; firma concurentă", FixedEmbedding, now);

        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedTenderAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        // The description requires a „Autorizație de mediu" which none of the vault documents covers —
        // the generator must mark it with a visible placeholder instead of inventing it.
        SeapTender tender = SeapTender.Import(
            $"EXT-{Guid.NewGuid():N}", "Achiziție echipamente și execuție lucrări",
            "Se solicită ofertă pentru echipamente cu putere minimă 10 kW și garanție de minim 24 de luni. " +
            "Se solicită autorizație de mediu pentru organizarea de șantier.",
            ["45214100-1"], ["Lucrari de constructii"], 1_000_000m, "RON",
            "Primaria Comunei Poiana Dambovita", "Dâmbovița",
            DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return tender.Id;
    }

    private static async Task<byte[]> DownloadAsync(HttpClient client, Guid tenderId, string kind)
    {
        HttpResponseMessage response = await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/bid-file?kind={kind}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static string ExtractDocxText(byte[] docx)
    {
        using MemoryStream stream = new(docx);
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart?.Document?.Body?
            .Descendants<Text>()
            .Select(node => node.Text)
            .Aggregate(new StringBuilder(), (builder, value) => builder.Append(value))
            .ToString() ?? string.Empty;
    }

    private static object SupplierProfileRequest(string email) => new
    {
        tradingName = "Companie multi-document test", companyRegistrationNumber = $"J00/{Guid.NewGuid():N}", taxIdentifier = "RO123",
        contactPerson = "Owner test", contactEmail = email, phoneNumber = "0700000000", website = (string?)null,
        address = "Strada Test 1", city = "Iași", county = "Iași", latitude = (decimal?)null,
        longitude = (decimal?)null, description = "Profil pentru testul de context multi-document SEAP"
    };

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = Encoding.UTF8.GetString(Convert.FromBase64String(
            payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    /// <summary>Deterministic embeddings for the pgvector RAG: every seeded chunk uses the same vector,
    /// so the distance query returns all of the company's chunks within the top-N limit.</summary>
    private sealed class FixedEmbeddingService : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(FixedEmbedding);
    }

    /// <summary>Records every system/user prompt pair (the SEAP background worker also calls the LLM) and
    /// returns a fixed proposal that echoes the vault data found in the prompt (newest certificate CUI,
    /// warranty, similar experience) and emits a visible „[Document lipsă: …]\" placeholder for the
    /// requirement the vault does not cover — mirroring the non-hallucination rules the real LLM is
    /// instructed to follow.</summary>
    private sealed class ContextEchoLlm : ILlmProvider
    {
        public List<LlmCall> Calls { get; } = [];

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            Calls.Add(new LlmCall(systemPrompt, userPrompt));

            string cui = Match(userPrompt, @"CUI (RO\d+)", "[Document lipsă: Certificat ONRC]");
            string warranty = Match(userPrompt, @"Garanție: ([\d ]+ luni)", "[Document lipsă: Fișă tehnică]");
            string experience = Match(userPrompt, @"(Primăria Udești[^\r\n]*)", "[Document lipsă: Experiență similară]");
            string uncovered = systemPrompt.Contains("[Document lipsă:", StringComparison.Ordinal)
                && userPrompt.Contains("Autorizație de mediu", StringComparison.OrdinalIgnoreCase)
                && !VaultSection(userPrompt).Contains("Autorizație", StringComparison.OrdinalIgnoreCase)
                ? "[Document lipsă: Autorizație de mediu]"
                : string.Empty;

            string json = "{" +
                "\"title\": \"Ofertă tehnică — Echipamente și lucrări\"," +
                "\"technicalProposal\": {\"sections\": [" +
                "{\"heading\": \"Date firmă\", \"content\": \"CUI: " + cui + "\"}," +
                "{\"heading\": \"Specificații tehnice\", \"content\": \"Garanție: " + warranty + "\"}," +
                "{\"heading\": \"Experiență\", \"content\": \"" + experience + "\"}," +
                "{\"heading\": \"Conformitate\", \"content\": \"" + uncovered + "\"}" +
                "]}," +
                "\"billOfQuantities\": [{\"position\": 1, \"description\": \"Transport materiale\", \"unit\": \"buc\", \"quantity\": 2, \"unitPrice\": 250, \"total\": 500}]" +
                "}";
            return Task.FromResult(json);
        }

        private static string VaultSection(string userPrompt)
        {
            const string marker = "Date despre firmă (fragmente relevante din CompanyVault):";
            int index = userPrompt.IndexOf(marker, StringComparison.Ordinal);
            return index < 0 ? string.Empty : userPrompt[(index + marker.Length)..];
        }

        private static string Match(string input, string pattern, string fallback)
        {
            Match match = Regex.Match(input, pattern, RegexOptions.Compiled);
            return match.Success ? match.Groups[1].Value.Trim() : fallback;
        }
    }

    private sealed record LlmCall(string System, string User);
    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
}
