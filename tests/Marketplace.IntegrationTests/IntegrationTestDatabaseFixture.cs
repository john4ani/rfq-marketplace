using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Marketplace.IntegrationTests;

/// <summary>
/// Provisions a dedicated, disposable "marketplace_test" database for the whole integration test run so
/// tests never write into the shared local development database. Recreated from scratch on every run;
/// dropped again on teardown (best effort - the next run recreates it regardless of leftover state).
/// </summary>
public sealed class IntegrationTestDatabaseFixture : IAsyncLifetime
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__MarketplaceDatabase";
    private const string DatabaseName = "marketplace_test";
    private const string DefaultDevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=marketplace;Username=marketplace;Password=marketplace-local-only";

    private string _maintenanceConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        string baseConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
            ?? DefaultDevelopmentConnectionString;
        NpgsqlConnectionStringBuilder builder = new(baseConnectionString);
        _maintenanceConnectionString = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = "postgres" }.ConnectionString;
        string owner = builder.Username ?? "marketplace";
        builder.Database = DatabaseName;
        string testConnectionString = builder.ConnectionString;

        await RecreateDatabaseAsync(owner);

        DbContextOptionsBuilder<MarketplaceDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(testConnectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(MarketplaceDbContext).Assembly.FullName));
        await using MarketplaceDbContext dbContext = new(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();

        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, testConnectionString);
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, null);
        try
        {
            await DropDatabaseAsync();
        }
        catch
        {
            // Best-effort cleanup only; the next run drops and recreates the database regardless.
        }
    }

    private async Task RecreateDatabaseAsync(string owner)
    {
        await DropDatabaseAsync();
        await using NpgsqlConnection maintenance = new(_maintenanceConnectionString);
        await maintenance.OpenAsync();
        await using NpgsqlCommand create = maintenance.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{DatabaseName}\" OWNER \"{owner}\";";
        await create.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync()
    {
        await using NpgsqlConnection maintenance = new(_maintenanceConnectionString);
        await maintenance.OpenAsync();
        await using (NpgsqlCommand terminate = maintenance.CreateCommand())
        {
            terminate.CommandText =
                $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{DatabaseName}' AND pid <> pg_backend_pid();";
            await terminate.ExecuteNonQueryAsync();
        }
        await using NpgsqlCommand drop = maintenance.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\";";
        await drop.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationTestGroup : ICollectionFixture<IntegrationTestDatabaseFixture>
{
    public const string Name = "Integration database";
}
