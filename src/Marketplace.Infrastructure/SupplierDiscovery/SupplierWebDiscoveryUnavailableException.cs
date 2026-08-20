namespace Marketplace.Infrastructure.SupplierDiscovery;

internal sealed class SupplierWebDiscoveryUnavailableException(string message) : InvalidOperationException(message);
