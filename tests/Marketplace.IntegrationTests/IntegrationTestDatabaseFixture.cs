using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Marketplace.IntegrationTests;

/// <summary>
/// Provisions a disposable PostgreSQL 17 + pgvector container (Testcontainers) for the whole integration
/// test run, per the V3 testing addendum: real Postgres, never EF InMemory, never the shared local
/// development database. The container connection string is exposed to the app host via the same
/// environment variable the production configuration contract already honours.
/// </summary>
public sealed class IntegrationTestDatabaseFixture : IAsyncLifetime
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__MarketplaceDatabase";
    private PostgreSqlContainer? _postgres;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_test")
            .Build();
        await _postgres.StartAsync();

        DbContextOptionsBuilder<MarketplaceDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(_postgres.GetConnectionString(),
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(MarketplaceDbContext).Assembly.FullName);
                npgsql.UseVector();
            });
        await using MarketplaceDbContext dbContext = new(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();

        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, _postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, null);
        if (_postgres is not null) await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationTestGroup : ICollectionFixture<IntegrationTestDatabaseFixture>
{
    public const string Name = "Integration database";
}

