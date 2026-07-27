using System.Net;
using System.Net.Mail;
using System.Text;

namespace Marketplace.Api.Configuration;

public static class ProductionReadiness
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment()) return;

        var errors = new List<string>();
        string connectionString = configuration.GetConnectionString("MarketplaceDatabase") ?? string.Empty;
        string frontendOrigin = configuration["Frontend:Origin"] ?? string.Empty;
        string signingKey = configuration["Jwt:SigningKey"] ?? string.Empty;
        string allowedHosts = configuration["AllowedHosts"] ?? string.Empty;
        string dataProtectionKeysPath = configuration["DataProtection:KeysPath"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(connectionString)
            || ContainsPlaceholder(connectionString))
            errors.Add("ConnectionStrings__MarketplaceDatabase must use a production credential.");
        if (!Uri.TryCreate(frontendOrigin, UriKind.Absolute, out Uri? frontendUri)
            || frontendUri.Scheme != Uri.UriSchemeHttps)
            errors.Add("Frontend__Origin must be an absolute HTTPS origin.");
        if (Encoding.UTF8.GetByteCount(signingKey) < 32 || ContainsPlaceholder(signingKey))
            errors.Add("Jwt__SigningKey must contain at least 32 non-placeholder bytes.");
        if (string.IsNullOrWhiteSpace(allowedHosts)
            || allowedHosts is "*" or "localhost"
            || allowedHosts.Contains("example.com", StringComparison.OrdinalIgnoreCase))
            errors.Add("AllowedHosts must list the production API host.");
        if (string.IsNullOrWhiteSpace(dataProtectionKeysPath)
            || !Path.IsPathRooted(dataProtectionKeysPath))
            errors.Add("DataProtection__KeysPath must be an absolute persistent directory.");

        Require(configuration, errors,
            "PasswordReset:ResetPageUrl",
            "PasswordReset:SmtpHost",
            "PasswordReset:SmtpUsername",
            "PasswordReset:SmtpPassword",
            "PasswordReset:FromAddress");
        string resetPageUrl = configuration["PasswordReset:ResetPageUrl"] ?? string.Empty;
        if (!Uri.TryCreate(resetPageUrl, UriKind.Absolute, out Uri? resetUri)
            || resetUri.Scheme != Uri.UriSchemeHttps)
            errors.Add("PasswordReset__ResetPageUrl must be an absolute HTTPS URL.");
        else if (frontendUri is not null
                 && !string.Equals(resetUri.GetLeftPart(UriPartial.Authority), frontendUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            errors.Add("PasswordReset__ResetPageUrl must use the configured frontend origin.");
        if (!int.TryParse(configuration["PasswordReset:SmtpPort"], out int smtpPort)
            || smtpPort is < 1 or > 65535)
            errors.Add("PasswordReset__SmtpPort must be between 1 and 65535.");
        if (!MailAddress.TryCreate(configuration["PasswordReset:FromAddress"], out _))
            errors.Add("PasswordReset__FromAddress must be a valid email address.");

        string[] trustedProxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        if (trustedProxies.Length == 0)
            errors.Add("ReverseProxy__KnownProxies must contain the Caddy proxy IP.");
        else if (trustedProxies.Any(proxy => !IPAddress.TryParse(proxy, out _)))
            errors.Add("Every ReverseProxy__KnownProxies value must be an IP address.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Production configuration is incomplete:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}");
    }

    private static void Require(IConfiguration configuration, List<string> errors, params string[] keys)
    {
        foreach (string key in keys)
            if (string.IsNullOrWhiteSpace(configuration[key]) || ContainsPlaceholder(configuration[key]!))
                errors.Add($"{key.Replace(':', '_')} is required and cannot contain a placeholder.");
    }

    private static bool ContainsPlaceholder(string value) =>
        value.Contains("development", StringComparison.OrdinalIgnoreCase)
        || value.Contains("local-only", StringComparison.OrdinalIgnoreCase)
        || value.Contains("replace", StringComparison.OrdinalIgnoreCase);
}
