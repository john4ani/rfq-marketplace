using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

/// <summary>
/// Reproduces the deployed container contract: ASPNETCORE_ENVIRONMENT=Production with the configuration
/// rendered by deploy/scripts/render-environment.sh. Guards the startup path (ProductionReadiness,
/// HostFiltering, HttpsRedirection) that a plain Development host does not exercise. Environment
/// variables are restored afterwards so other tests in the shared collection keep their Development host.
/// </summary>
[Collection(IntegrationTestGroup.Name)]
public sealed class ProductionHealthEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>, IDisposable
{
    private const string Origin = "https://compaspot.com";
    private readonly MarketplaceWebApplicationFactory factory;
    private readonly Dictionary<string, string?> previousValues = [];

    public ProductionHealthEndpointTests(MarketplaceWebApplicationFactory factory)
    {
        this.factory = factory;
        foreach ((string name, string value) in ProductionConfig())
        {
            previousValues[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose()
    {
        foreach ((string name, string? previous) in previousValues)
            Environment.SetEnvironmentVariable(name, previous);
    }

    [Fact]
    public async Task HealthReadyIsHealthyInProductionWithCompleteConfiguration()
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        using HttpRequestMessage request = new(HttpMethod.Get, "/health/ready");
        request.Headers.Host = "compaspot.com";

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Healthy\"", content, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ProductionConfig() => new()
    {
        ["ASPNETCORE_ENVIRONMENT"] = "Production",
        ["Frontend__Origin"] = Origin,
        ["Jwt__SigningKey"] = "0123456789abcdef0123456789abcdef",
        ["AllowedHosts"] = "compaspot.com;www.compaspot.com",
        ["DataProtection__KeysPath"] = Path.Combine(Path.GetTempPath(), "marketplace-dpk-production-test"),
        ["PasswordReset__ResetPageUrl"] = $"{Origin}/reset-password",
        ["Email__Enabled"] = "false",
        ["ReverseProxy__KnownProxies__0"] = "172.28.0.2"
    };
}
