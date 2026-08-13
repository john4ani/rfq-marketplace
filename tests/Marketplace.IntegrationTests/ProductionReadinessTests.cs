using Marketplace.Api.Configuration;
using Marketplace.Application.Identity;
using Marketplace.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
            ["Email:Enabled"] = "true",
            ["Email:Host"] = "smtp-relay.gmail.com",
            ["Email:Port"] = "587",
            ["Email:UseTls"] = "true",
            ["Email:FromAddress"] = "notifications@compaspot.com",
            ["Email:FromName"] = "CompaSpot",
            ["Email:ReplyToAddress"] = "support@compaspot.com",
            ["ReverseProxy:KnownProxies:0"] = "172.20.0.2"
        });

        ProductionReadiness.Validate(configuration, Environment(Environments.Production));
    }

    [Fact]
    public void ProductionRejectsIncompleteEnabledEmailConfiguration()
    {
        Dictionary<string, string?> settings = ProductionSettings();
        settings["Email:Enabled"] = "true";
        IConfiguration configuration = Configuration(settings);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ProductionReadiness.Validate(configuration, Environment(Environments.Production)));

        Assert.Contains("Email_Host", error.Message, StringComparison.Ordinal);
        Assert.Contains("Email_FromAddress", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmailTransportUsesConfiguredSenderAndReplyToWithoutSendingWhenDisabled()
    {
        var options = new EmailOptions
        {
            Enabled = false,
            Host = "smtp-relay.gmail.com",
            Port = 587,
            UseTls = true,
            FromAddress = "notifications@compaspot.com",
            FromName = "CompaSpot",
            ReplyToAddress = "support@compaspot.com"
        };
        using var message = SmtpEmailSender.CreateMailMessage(options,
            new("user@example.test", "Subject", "Plain body", "<p>HTML body</p>"));

        Assert.Equal("notifications@compaspot.com", message.From!.Address);
        Assert.Equal("CompaSpot", message.From.DisplayName);
        Assert.Equal("support@compaspot.com", Assert.Single(message.ReplyToList).Address);
        Assert.Equal("user@example.test", Assert.Single(message.To).Address);
        Assert.True(message.IsBodyHtml);
        Assert.Equal("<p>HTML body</p>", message.Body);

        var sender = new SmtpEmailSender(Options.Create(options), NullLogger<SmtpEmailSender>.Instance);
        await sender.SendAsync(new("user@example.test", "Subject", "Plain body"), CancellationToken.None);
    }

    private static Dictionary<string, string?> ProductionSettings() => new()
    {
        ["ConnectionStrings:MarketplaceDatabase"] = "Host=postgres;Database=marketplace;Username=app;Password=a-secret",
        ["Frontend:Origin"] = "https://app.marketplace.test",
        ["Jwt:SigningKey"] = "a-production-signing-key-with-more-than-32-bytes",
        ["AllowedHosts"] = "api.marketplace.test",
        ["DataProtection:KeysPath"] = "/var/lib/marketplace/data-protection-keys",
        ["PasswordReset:ResetPageUrl"] = "https://app.marketplace.test/reset-password",
        ["ReverseProxy:KnownProxies:0"] = "172.20.0.2"
    };

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
