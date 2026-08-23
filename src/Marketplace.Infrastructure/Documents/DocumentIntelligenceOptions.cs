namespace Marketplace.Infrastructure.Documents;

public sealed class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";
    public bool Enabled { get; init; }
    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}
