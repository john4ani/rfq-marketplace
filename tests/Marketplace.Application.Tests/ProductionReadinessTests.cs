using Marketplace.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Marketplace.Application.Tests;

public sealed class ProductionReadinessTests
{
    private const string SigningKey = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void ProductionWithCompleteHttpsConfigurationDoesNotThrow()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MarketplaceDatabase"] = "Host=postgres;Database=compaspot_prod;Username=marketplace;Password=secret",
            ["Frontend:Origin"] = "https://compaspot.com",
            ["Jwt:SigningKey"] = SigningKey,
            ["AllowedHosts"] = "compaspot.com;www.compaspot.com",
            ["DataProtection:KeysPath"] = "/var/lib/marketplace/data-protection-keys",
            ["PasswordReset:ResetPageUrl"] = "https://compaspot.com/reset-password",
            ["Email:Enabled"] = "true",
            ["Email:Host"] = "smtp-relay.gmail.com",
            ["Email:Port"] = "587",
            ["Email:FromAddress"] = "no-reply@compaspot.com",
            ["Email:ReplyToAddress"] = "office@compaspot.com",
            ["EmailVerification:ConfirmationPageUrl"] = "https://compaspot.com/verify-email",
            ["ReverseProxy:KnownProxies:0"] = "172.28.0.2"
        });

        ProductionReadiness.Validate(config, Environment("Production"));
    }

    [Fact]
    public void ProductionWithoutResetPageUrlThrows()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MarketplaceDatabase"] = "Host=postgres;Database=compaspot_prod;Username=marketplace;Password=secret",
            ["Frontend:Origin"] = "https://compaspot.com",
            ["Jwt:SigningKey"] = SigningKey,
            ["AllowedHosts"] = "compaspot.com",
            ["DataProtection:KeysPath"] = "/var/lib/marketplace/data-protection-keys",
            ["Email:Enabled"] = "false",
            ["ReverseProxy:KnownProxies:0"] = "172.28.0.2"
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ProductionReadiness.Validate(config, Environment("Production")));
        Assert.Contains("PasswordReset_ResetPageUrl", exception.Message);
    }

    [Fact]
    public void StagingWithPlainHttpOriginAndMatchingUrlsDoesNotThrow()
    {
        // The internal-only Staging site is plain HTTP on the Lightsail static IP :8081 (see
        // docs/operations/environments.md); only Production requires HTTPS origins.
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MarketplaceDatabase"] = "Host=postgres;Database=compaspot_staging;Username=marketplace;Password=secret",
            ["Frontend:Origin"] = "http://3.64.174.227:8081",
            ["Jwt:SigningKey"] = SigningKey,
            ["AllowedHosts"] = "3.64.174.227:8081;127.0.0.1:8080",
            ["DataProtection:KeysPath"] = "/var/lib/marketplace/data-protection-keys",
            ["PasswordReset:ResetPageUrl"] = "http://3.64.174.227:8081/reset-password",
            ["Email:Enabled"] = "true",
            ["Email:Host"] = "smtp-relay.gmail.com",
            ["Email:Port"] = "587",
            ["Email:FromAddress"] = "no-reply@compaspot.com",
            ["Email:ReplyToAddress"] = "office@compaspot.com",
            ["EmailVerification:ConfirmationPageUrl"] = "http://3.64.174.227:8081/verify-email",
            ["ReverseProxy:KnownProxies:0"] = "172.28.0.2"
        });

        ProductionReadiness.Validate(config, Environment("Staging"));
    }

    [Fact]
    public void StagingStillRejectsMissingRequiredUrls()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MarketplaceDatabase"] = "Host=postgres;Database=compaspot_staging;Username=marketplace;Password=secret",
            ["Frontend:Origin"] = "http://3.64.174.227:8081",
            ["Jwt:SigningKey"] = SigningKey,
            ["AllowedHosts"] = "3.64.174.227:8081;127.0.0.1:8080",
            ["DataProtection:KeysPath"] = "/var/lib/marketplace/data-protection-keys",
            ["Email:Enabled"] = "false",
            ["ReverseProxy:KnownProxies:0"] = "172.28.0.2"
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ProductionReadiness.Validate(config, Environment("Staging")));
        Assert.Contains("PasswordReset_ResetPageUrl", exception.Message);
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static TestEnvironment Environment(string name) => new(name);

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Marketplace.Api";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}