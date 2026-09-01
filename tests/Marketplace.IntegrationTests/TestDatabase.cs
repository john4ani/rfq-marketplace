using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.IntegrationTests;

/// <summary>
/// Shared PostgreSQL access for the integration suite. The container itself is provisioned exactly once
/// by <see cref="IntegrationTestDatabaseFixture"/> (an <c>ICollectionFixture</c> for the whole
/// <see cref="IntegrationTestGroup"/> collection), which exposes the connection string through the same
/// environment variable the production configuration contract honours. Between tests
/// <see cref="ResetAsync"/> truncates every business table (keeping only migration/seed reference data),
/// so each test starts from a clean database regardless of what earlier tests wrote.
/// </summary>
public static class TestDatabase
{
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__MarketplaceDatabase";

    /// <summary>Tables holding migration/seed reference data (roles, category taxonomy, CPV prefixes on
    /// categories) that must survive the per-test reset; everything else is truncated.</summary>
    private static readonly string[] KeepTables =
    [
        "__EFMigrationsHistory",
        "AspNetRoles",
        "AspNetRoleClaims",
        "Categories",
        "Verticals"
    ];

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
        ?? throw new InvalidOperationException(
            $"{ConnectionStringEnvironmentVariable} is not set. Tests must run inside the " +
            $"{IntegrationTestGroup.Name} collection so the shared container fixture provisions it.");

    public static DbContextOptions<MarketplaceDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
                npgsql.MigrationsAssembly(typeof(MarketplaceDbContext).Assembly.FullName);
                npgsql.UseVector();
            })
            .Options;

    /// <summary>
    /// Truncates every EF-mapped table except the keep-list. Fast (TRUNCATE, no per-row deletes) and
    /// safe: the keep-list tables are only ever parents of the truncated data, so CASCADE never touches
    /// them. The table list is derived from the EF model, so new entities are covered automatically.
    /// </summary>
    public static async Task ResetAsync()
    {
        await using MarketplaceDbContext db = new(CreateOptions());
        string[] tables = db.Model.GetEntityTypes()
            .Select(type => type.GetTableName())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Where(name => !KeepTables.Contains(name, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (tables.Length == 0) return;
        string truncate = $"TRUNCATE TABLE {string.Join(", ", tables.Select(name => $"\"{name}\""))} RESTART IDENTITY CASCADE";
        await db.Database.ExecuteSqlRawAsync(truncate);
    }
}
