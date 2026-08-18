using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task LiveHealthReturnsHealthyResponseAndCorrelationId()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "integration-test");

        using HttpResponseMessage response = await _client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("integration-test", response.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Contains("\"status\":\"Healthy\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApiDocumentIsAvailableInDevelopment()
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json");
        string content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"openapi\"", content, StringComparison.Ordinal);
    }
}
