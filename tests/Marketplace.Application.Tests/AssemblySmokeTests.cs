namespace Marketplace.Application.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void ApplicationAssemblyHasExpectedName()
    {
        Assert.Equal(
            "Marketplace.Application",
            typeof(Marketplace.Application.AssemblyMarker).Assembly.GetName().Name);
    }
}
