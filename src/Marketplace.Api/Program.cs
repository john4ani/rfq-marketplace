using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Marketplace.Api.Configuration;
using Marketplace.Api.Health;
using Marketplace.Api.Middleware;
using Marketplace.Infrastructure;
using Marketplace.Infrastructure.Identity;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
ProductionReadiness.Validate(builder.Configuration, builder.Environment);
string? dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    Directory.CreateDirectory(dataProtectionKeysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("rfq-marketplace");
}
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => { options.IncludeScopes = true; options.TimestampFormat = "O"; options.JsonWriterOptions = new JsonWriterOptions { Indented = false }; });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AutoBiddingExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
string frontendOrigin = builder.Configuration["Frontend:Origin"]
    ?? throw new InvalidOperationException("Frontend origin is required.");
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = 1;
    foreach (string proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(proxy));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("category-creation", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    options.AddPolicy("assisted-intake", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
});
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]).AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"]);

WebApplication app = builder.Build();
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
// The Local profile mirrors Staging/Prod for the auto-bid flow but stays a local dev host:
// development conveniences (OpenAPI, no forced HTTPS redirect) apply, and migrations run at startup.
bool isLocalDevelopment = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local");
if (isLocalDevelopment) app.MapOpenApi();
else app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("live"), ResponseWriter = HealthResponseWriter.WriteAsync });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("ready"), ResponseWriter = HealthResponseWriter.WriteAsync });
// Local-first developer experience: in Development the database schema is applied automatically at
// startup (idempotent) so `dotnet run` works right after `docker compose up -d postgres`, without
// requiring a manual `dotnet ef database update`. Prod/Staging migrate through the explicit migration
// bundle (migrate-prod / migrate-staging) before the API starts.
if (isLocalDevelopment)
{
    using IServiceScope scope = app.Services.CreateScope();
    MarketplaceDbContext dbContext = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
    await dbContext.Database.MigrateAsync();
}
await IdentitySeeder.SeedRolesAsync(app.Services);
app.Run();
public partial class Program;
