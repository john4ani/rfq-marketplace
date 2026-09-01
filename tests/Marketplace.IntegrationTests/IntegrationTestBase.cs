namespace Marketplace.IntegrationTests;

/// <summary>
/// Base class for every integration test that touches the shared database. xUnit invokes
/// <see cref="IAsyncLifetime.InitializeAsync"/> before EACH test, so the database is reset (truncated)
/// per test — a test can never see residual rows written by an earlier test in the same class or in
/// another class of the shared <see cref="IntegrationTestGroup"/> collection.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    public virtual Task InitializeAsync() => TestDatabase.ResetAsync();

    public virtual Task DisposeAsync() => Task.CompletedTask;
}
