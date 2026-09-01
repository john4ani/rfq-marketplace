using Xunit;

// The integration suite shares ONE PostgreSQL Testcontainers container (the IntegrationTestGroup
// collection fixture) and every app host writes to that same database. Run the whole assembly
// sequentially instead of xUnit's default per-collection parallelism: on CI (2 vCPU) parallel
// container/DB I/O is the main source of race-condition flakiness.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
