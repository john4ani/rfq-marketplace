namespace Marketplace.Infrastructure.AiIntake;

public enum AiIntakeProvider
{
    OpenAi,
    AzureOpenAi
}

public sealed class AiIntakeOptions
{
    public const string SectionName = "AiIntake";
    public bool Enabled { get; init; } = true;
    public AiIntakeProvider Provider { get; init; } = AiIntakeProvider.OpenAi;
    public string ApiKey { get; init; } = string.Empty;
    public string Endpoint { get; init; } = "https://api.openai.com/v1/responses";
    public string Model { get; init; } = "gpt-5.6-luna";
    public string ReasoningEffort { get; init; } = "none";
    public string TextVerbosity { get; init; } = "low";
    public string PromptVersion { get; init; } = "2026-08-08.1";
    public int RequestTimeoutSeconds { get; init; } = 60;
}
