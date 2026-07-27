using Marketplace.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Marketplace.IntegrationTests;

public sealed class ProductionReadinessTests
{
    [Fact]
    public void ProductionRejectsDevelopmentConfiguration()
    {
        IConfiguration configuration = Configuration(new Dictionary<string, string?>());

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ProductionReadiness.Validate(configuration, Environment(Environments.Production)));

        Assert.Contains("Production configuration is incomplete", error.Message, StringComparison.Ordinal);
        Assert.Contains("Jwt__SigningKey", error.Message, StringComparison.Ordinal);
        Assert.Contains("PasswordReset_ResetPageUrl", error.Message, StringComparison.Ordinal);
        Assert.Contains("ReverseProxy__KnownProxies", error.Message, StringComparison.Ordinal);
        Assert.Contains("DataProtection__KeysPath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionAcceptsCompleteNonPlaceholderConfiguration()
    {
        IConfiguration configuration = Configuration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MarketplaceDatabase"] = "Host=postgres;Database=marketplace;Username=app;Password=a-secret",
            ["Frontend:Origin"] = "https://app.marketplace.test",
            ["Jwt:SigningKey"] = "a-production-signing-key-with-more-than-32-bytes",
            ["AllowedHosts"] = "api.marketplace.test",
            ["DataProtection:KeysPath"] = "/var/lib/marketplace/data-protection-keys",
            ["PasswordReset:ResetPageUrl"] = "https://app.marketplace.test/reset-password",
            ["PasswordReset:SmtpHost"] = "email-smtp.eu-central-1.amazonaws.com",
            ["PasswordReset:SmtpPort"] = "587",
            ["PasswordReset:SmtpUsername"] = "smtp-user",
            ["PasswordReset:SmtpPassword"] = "smtp-secret",
            ["PasswordReset:FromAddress"] = "no-reply@marketplace.test",
            ["ReverseProxy:KnownProxies:0"] = "172.20.0.2"
        });

        ProductionReadiness.Validate(configuration, Environment(Environments.Production));
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static TestHostEnvironment Environment(string name) => new() { EnvironmentName = name };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Marketplace.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
