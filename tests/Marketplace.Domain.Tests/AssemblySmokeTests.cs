namespace Marketplace.Domain.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void DomainAssemblyHasExpectedName()
    {
        Assert.Equal("Marketplace.Domain", typeof(Marketplace.Domain.AssemblyMarker).Assembly.GetName().Name);
    }
}
